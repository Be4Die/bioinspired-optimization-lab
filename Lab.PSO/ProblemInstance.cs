using System.Collections.Generic;
using System.Linq;

namespace Lab.PSO;

/// <summary>
/// Представляет экземпляр задачи распределения с наборами задач и машин
/// </summary>
public class ProblemInstance
{
    /// <summary>
    /// Словарь задач по их ID
    /// </summary>
    public Dictionary<int, Task> Tasks { get; set; } = new();

    /// <summary>
    /// Словарь виртуальных машин по их ID
    /// </summary>
    public Dictionary<int, VirtualMachine> VirtualMachines { get; set; } = new();

    /// <summary>
    /// Коэффициент штрафа за нарушение памяти
    /// </summary>
    public double MemoryPenaltyCoefficient { get; set; } = 1000.0;

    /// <summary>
    /// Коэффициент штрафа за нарушение предшествования
    /// </summary>
    public double PrecedencePenaltyCoefficient { get; set; } = 1000.0;

    /// <summary>
    /// Создает случайный экземпляр задачи для тестирования
    /// </summary>
    /// <param name="taskCount">Количество задач</param>
    /// <param name="machineCount">Количество машин</param>
    /// <param name="seed">Семя для генератора случайных чисел</param>
    /// <returns>Случайный экземпляр задачи</returns>
    public static ProblemInstance CreateRandomInstance(int taskCount, int machineCount, int seed = 42)
    {
        var random = new Random(seed);
        var instance = new ProblemInstance();

        // Создаем задачи
        for (int i = 0; i < taskCount; i++)
        {
            var task = new Task
            {
                Id = i + 1,
                ComputationVolume = random.Next(10, 100),
                MemoryRequirement = random.Next(1, 20)
            };

            // Добавляем предшественников (до 3 случайных задач с меньшим ID)
            var maxPredecessors = Math.Min(3, i);
            if (maxPredecessors > 0)
            {
                var predecessorCount = random.Next(0, maxPredecessors + 1);
                for (int j = 0; j < predecessorCount; j++)
                {
                    var predId = random.Next(1, i + 1);
                    if (!task.PredecessorIds.Contains(predId))
                    {
                        task.PredecessorIds.Add(predId);
                    }
                }
            }

            instance.Tasks[task.Id] = task;
        }

        // Создаем виртуальные машины
        for (int i = 0; i < machineCount; i++)
        {
            var vm = new VirtualMachine
            {
                Id = i + 1,
                Performance = random.Next(5, 25),
                AvailableMemory = random.Next(10, 30)
            };

            instance.VirtualMachines[vm.Id] = vm;
        }

        return instance;
    }

    /// <summary>
    /// Проверяет корректность задачи (отсутствие циклов в графе предшествования)
    /// </summary>
    /// <returns>True, если задача корректна</returns>
    public bool Validate()
    {
        // Проверка на циклы с помощью поиска в глубину
        var visited = new HashSet<int>();
        var recursionStack = new HashSet<int>();

        foreach (var task in Tasks.Values)
        {
            if (HasCycle(task.Id, visited, recursionStack))
                return false;
        }

        return true;
    }

    private bool HasCycle(int taskId, HashSet<int> visited, HashSet<int> recursionStack)
    {
        if (recursionStack.Contains(taskId))
            return true;

        if (visited.Contains(taskId))
            return false;

        visited.Add(taskId);
        recursionStack.Add(taskId);

        var task = Tasks[taskId];
        foreach (var predId in task.PredecessorIds)
        {
            if (HasCycle(predId, visited, recursionStack))
                return true;
        }

        recursionStack.Remove(taskId);
        return false;
    }
}