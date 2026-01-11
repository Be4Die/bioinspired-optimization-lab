using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Lab.PSO;

/// <summary>
/// Представляет решение задачи распределения
/// </summary>
public class Solution
{
    /// <summary>
    /// Вектор назначений: индекс - ID задачи, значение - ID машины
    /// </summary>
    public Dictionary<int, int> Assignment { get; set; } = new();

    /// <summary>
    /// Общее время выполнения (makespan)
    /// </summary>
    public double Makespan { get; set; }

    /// <summary>
    /// Общий штраф за нарушение ограничений
    /// </summary>
    public double TotalPenalty { get; set; }

    /// <summary>
    /// Фитнес-значение (makespan + штраф)
    /// </summary>
    public double Fitness => Makespan + TotalPenalty;

    /// <summary>
    /// История изменений фитнес-значения
    /// </summary>
    [JsonIgnore]
    public List<double> FitnessHistory { get; set; } = new();

    /// <summary>
    /// Время вычисления решения
    /// </summary>
    public TimeSpan ComputationTime { get; set; }

    /// <summary>
    /// Количество итераций, за которые найдено решение
    /// </summary>
    public int IterationFound { get; set; }

    /// <summary>
    /// Детализированная информация о задачах после планирования
    /// </summary>
    [JsonIgnore]
    public Dictionary<int, Task> ScheduledTasks { get; set; } = new();

    /// <summary>
    /// Детализированная информация о машинах после планирования
    /// </summary>
    [JsonIgnore]
    public Dictionary<int, VirtualMachine> ScheduledMachines { get; set; } = new();

    /// <summary>
    /// Создает глубокую копию решения
    /// </summary>
    /// <returns>Копия решения</returns>
    public Solution DeepCopy()
    {
        return new Solution
        {
            Assignment = new Dictionary<int, int>(Assignment),
            Makespan = Makespan,
            TotalPenalty = TotalPenalty,
            FitnessHistory = new List<double>(FitnessHistory),
            ComputationTime = ComputationTime,
            IterationFound = IterationFound,
            ScheduledTasks = ScheduledTasks?.ToDictionary(kvp => kvp.Key,
                kvp => new Task
                {
                    Id = kvp.Value.Id,
                    ComputationVolume = kvp.Value.ComputationVolume,
                    MemoryRequirement = kvp.Value.MemoryRequirement,
                    PredecessorIds = new List<int>(kvp.Value.PredecessorIds),
                    StartTime = kvp.Value.StartTime,
                    CompletionTime = kvp.Value.CompletionTime,
                    AssignedMachineId = kvp.Value.AssignedMachineId,
                    IsReady = kvp.Value.IsReady
                }),
            ScheduledMachines = ScheduledMachines?.ToDictionary(kvp => kvp.Key,
                kvp => new VirtualMachine
                {
                    Id = kvp.Value.Id,
                    Performance = kvp.Value.Performance,
                    AvailableMemory = kvp.Value.AvailableMemory,
                    AssignedTasks = kvp.Value.AssignedTasks?.Select(t => new Task
                    {
                        Id = t.Id,
                        ComputationVolume = t.ComputationVolume,
                        MemoryRequirement = t.MemoryRequirement,
                        StartTime = t.StartTime,
                        CompletionTime = t.CompletionTime,
                        AssignedMachineId = t.AssignedMachineId
                    }).ToList(),
                    LastCompletionTime = kvp.Value.LastCompletionTime
                })
        };
    }

    /// <summary>
    /// Проверяет, является ли решение допустимым (без нарушений жестких ограничений)
    /// </summary>
    /// <param name="instance">Экземпляр задачи</param>
    /// <returns>True, если решение допустимо</returns>
    public bool IsValid(ProblemInstance instance)
    {
        // Проверка назначения всех задач
        if (Assignment.Count != instance.Tasks.Count)
            return false;

        // Проверка ограничений по памяти
        foreach (var (taskId, machineId) in Assignment)
        {
            if (!instance.VirtualMachines.ContainsKey(machineId))
                return false;

            var task = instance.Tasks[taskId];
            var machine = instance.VirtualMachines[machineId];

            if (!machine.HasSufficientMemory(task.MemoryRequirement))
                return false;
        }

        return true;
    }
}