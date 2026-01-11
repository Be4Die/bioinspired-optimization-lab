using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lab.PSO;

/// <summary>
/// Планировщик для вычисления времени выполнения и проверки ограничений
/// </summary>
public class Scheduler
{
    private readonly ProblemInstance _instance;

    public Scheduler(ProblemInstance instance)
    {
        _instance = instance;
    }

    /// <summary>
    /// Вычисляет расписание для заданного назначения
    /// </summary>
    /// <param name="assignment">Назначение задач на машины</param>
    /// <returns>Решение с вычисленным расписанием</returns>
    public Solution CalculateSchedule(Dictionary<int, int> assignment)
    {
        var solution = new Solution
        {
            Assignment = assignment
        };

        // Создаем копии для планирования
        var tasks = _instance.Tasks.Values.Select(t => new Task
        {
            Id = t.Id,
            ComputationVolume = t.ComputationVolume,
            MemoryRequirement = t.MemoryRequirement,
            PredecessorIds = new List<int>(t.PredecessorIds)
        }).ToDictionary(t => t.Id);

        var machines = _instance.VirtualMachines.Values.Select(m => new VirtualMachine
        {
            Id = m.Id,
            Performance = m.Performance,
            AvailableMemory = m.AvailableMemory
        }).ToDictionary(m => m.Id);

        // Назначаем задачи на машины
        foreach (var (taskId, machineId) in assignment)
        {
            if (tasks.ContainsKey(taskId) && machines.ContainsKey(machineId))
            {
                var task = tasks[taskId];
                var machine = machines[machineId];

                task.AssignedMachineId = machineId;
                machine.AssignedTasks.Add(task);
            }
        }

        // Вычисляем штрафы и планируем выполнение
        var penalties = CalculatePenalties(assignment, tasks, machines);
        solution.TotalPenalty = penalties.totalPenalty;

        // Планируем выполнение задач
        if (penalties.hardConstraintViolated)
        {
            // Если есть нарушения жестких ограничений, задаем большое значение makespan
            solution.Makespan = double.MaxValue;
        }
        else
        {
            solution.Makespan = ScheduleTasks(tasks, machines);
        }

        // Сохраняем запланированные задачи и машины
        solution.ScheduledTasks = tasks;
        solution.ScheduledMachines = machines;

        return solution;
    }

    /// <summary>
    /// Параллельно вычисляет расписания для нескольких назначений
    /// </summary>
    /// <param name="assignments">Список назначений</param>
    /// <returns>Список решений</returns>
    public List<Solution> CalculateSchedulesParallel(List<Dictionary<int, int>> assignments)
    {
        var solutions = new List<Solution>();

        Parallel.ForEach(assignments, assignment =>
        {
            var solution = CalculateSchedule(assignment);
            lock (solutions)
            {
                solutions.Add(solution);
            }
        });

        return solutions;
    }

    private (double totalPenalty, bool hardConstraintViolated) CalculatePenalties(
        Dictionary<int, int> assignment,
        Dictionary<int, Task> tasks,
        Dictionary<int, VirtualMachine> machines)
    {
        double totalPenalty = 0;
        bool hardConstraintViolated = false;

        foreach (var (taskId, machineId) in assignment)
        {
            var task = tasks[taskId];
            var machine = machines[machineId];

            // Штраф за нарушение памяти
            if (!machine.HasSufficientMemory(task.MemoryRequirement))
            {
                double memoryDeficit = task.MemoryRequirement - machine.AvailableMemory;
                totalPenalty += memoryDeficit * _instance.MemoryPenaltyCoefficient;
                hardConstraintViolated = true;
            }
        }

        return (totalPenalty, hardConstraintViolated);
    }

    private double ScheduleTasks(Dictionary<int, Task> tasks, Dictionary<int, VirtualMachine> machines)
    {
        // Алгоритм спискового расписания с учетом предшествования

        var readyQueue = new List<Task>();
        var completedTasks = new HashSet<int>();
        double currentTime = 0;
        double maxCompletionTime = 0;

        // Инициализация: задачи без предшественников готовы
        foreach (var task in tasks.Values)
        {
            if (task.PredecessorIds.Count == 0)
            {
                task.IsReady = true;
                readyQueue.Add(task);
            }
        }

        readyQueue = readyQueue.OrderBy(t => t.Id).ToList();

        while (completedTasks.Count < tasks.Count)
        {
            // Находим готовые к выполнению задачи
            var tasksToSchedule = new List<Task>();

            foreach (var task in readyQueue)
            {
                if (task.CheckPredecessorsReady(tasks))
                {
                    tasksToSchedule.Add(task);
                }
            }

            if (tasksToSchedule.Count == 0)
            {
                // Если нет готовых задач, находим ближайшее время завершения предшественников
                double minNextTime = double.MaxValue;
                foreach (var task in tasks.Values.Where(t => !completedTasks.Contains(t.Id)))
                {
                    if (!task.IsReady)
                    {
                        double readyTime = task.GetMaxPredecessorCompletionTime(tasks);
                        minNextTime = Math.Min(minNextTime, readyTime);
                    }
                }

                if (minNextTime < double.MaxValue)
                {
                    currentTime = minNextTime;
                }

                // Обновляем состояние готовности
                foreach (var task in tasks.Values.Where(t => !completedTasks.Contains(t.Id) && !t.IsReady))
                {
                    if (task.CheckPredecessorsReady(tasks))
                    {
                        task.IsReady = true;
                        readyQueue.Add(task);
                    }
                }

                continue;
            }

            // Планируем готовые задачи
            foreach (var task in tasksToSchedule)
            {
                if (!task.AssignedMachineId.HasValue)
                    continue;

                var machine = machines[task.AssignedMachineId.Value];

                // Время начала = max(время готовности машины, время завершения предшественников)
                double startTime = Math.Max(machine.LastCompletionTime,
                    task.GetMaxPredecessorCompletionTime(tasks));

                double executionTime = machine.CalculateExecutionTime(task.ComputationVolume);
                task.StartTime = startTime;
                task.CompletionTime = startTime + executionTime;

                // Обновляем время завершения машины
                machine.LastCompletionTime = task.CompletionTime;

                // Добавляем задачу в список завершенных
                completedTasks.Add(task.Id);
                readyQueue.Remove(task);

                // Обновляем максимальное время завершения
                maxCompletionTime = Math.Max(maxCompletionTime, task.CompletionTime);

                // Обновляем текущее время
                currentTime = Math.Max(currentTime, task.CompletionTime);
            }

            // Проверяем, не появились ли новые готовые задачи
            foreach (var task in tasks.Values.Where(t => !completedTasks.Contains(t.Id) && !t.IsReady))
            {
                if (task.CheckPredecessorsReady(tasks))
                {
                    task.IsReady = true;
                    if (!readyQueue.Contains(task))
                        readyQueue.Add(task);
                }
            }
        }

        return maxCompletionTime;
    }
}