using System.Text.Json.Serialization;
using Lab.Domain;

namespace Lab.Algorithms;

/// <summary>
/// Представляет особь (хромосому) в генетическом алгоритме
/// </summary>
public class Individual
{
    private readonly Random _random;
    private readonly ProblemInstance _instance;

    /// <summary>
    /// Гены особи (назначение задач на машины)
    /// </summary>
    public Dictionary<int, int> Chromosome { get; private set; }

    /// <summary>
    /// Решение, соответствующее данной хромосоме
    /// </summary>
    [JsonIgnore]
    public Solution? Solution { get; private set; }

    /// <summary>
    /// Фитнес-значение особи
    /// </summary>
    public double Fitness => Solution?.Fitness ?? double.MaxValue;

    /// <summary>
    /// Возраст особи (количество поколений без улучшения)
    /// </summary>
    public int Age { get; set; }

    public Individual(ProblemInstance instance, Random random)
    {
        _instance = instance;
        _random = random;
        Chromosome = InitializeChromosome();
        Age = 0;
    }

    private Individual(ProblemInstance instance, Random random, Dictionary<int, int> chromosome)
    {
        _instance = instance;
        _random = random;
        Chromosome = chromosome;
        Age = 0;
    }

    /// <summary>
    /// Инициализирует хромосому случайными назначениями
    /// </summary>
    private Dictionary<int, int> InitializeChromosome()
    {
        var chromosome = new Dictionary<int, int>();
        var machineIds = _instance.VirtualMachines.Keys.ToList();

        foreach (var task in _instance.Tasks.Values)
        {
            int machineId = machineIds[_random.Next(machineIds.Count)];
            chromosome[task.Id] = machineId;
        }

        // Применяем репарацию для соблюдения ограничений по памяти
        RepairChromosome(chromosome);

        return chromosome;
    }

    /// <summary>
    /// Ремонтирует хромосому для соблюдения ограничений по памяти
    /// </summary>
    private void RepairChromosome(Dictionary<int, int> chromosome)
    {
        var machineIds = _instance.VirtualMachines.Keys.ToList();

        foreach (var (taskId, machineId) in chromosome)
        {
            var task = _instance.Tasks[taskId];
            var machine = _instance.VirtualMachines[machineId];

            // Если не хватает памяти, переназначаем на другую машину
            if (!machine.HasSufficientMemory(task.MemoryRequirement))
            {
                // Ищем машину с достаточной памятью
                var suitableMachines = machineIds
                    .Where(id => _instance.VirtualMachines[id].HasSufficientMemory(task.MemoryRequirement))
                    .ToList();

                if (suitableMachines.Count > 0)
                {
                    // Выбираем случайную подходящую машину
                    int newMachineId = suitableMachines[_random.Next(suitableMachines.Count)];
                    chromosome[taskId] = newMachineId;
                }
            }
        }
    }

    /// <summary>
    /// Выполняет одноточечный кроссовер с другой особью
    /// </summary>
    public (Individual, Individual) Crossover(Individual other, double crossoverRate)
    {
        if (_random.NextDouble() > crossoverRate)
            return (this.Clone(), other.Clone());

        var child1Genes = new Dictionary<int, int>();
        var child2Genes = new Dictionary<int, int>();

        // Выбираем случайную точку кроссовера
        var taskIds = _instance.Tasks.Keys.ToList();
        int crossoverPoint = _random.Next(1, taskIds.Count - 1);

        // Первая часть от первого родителя, вторая - от второго
        for (int i = 0; i < taskIds.Count; i++)
        {
            int taskId = taskIds[i];
            if (i < crossoverPoint)
            {
                child1Genes[taskId] = this.Chromosome[taskId];
                child2Genes[taskId] = other.Chromosome[taskId];
            }
            else
            {
                child1Genes[taskId] = other.Chromosome[taskId];
                child2Genes[taskId] = this.Chromosome[taskId];
            }
        }

        // Применяем репарацию к потомкам
        RepairChromosome(child1Genes);
        RepairChromosome(child2Genes);

        var child1 = new Individual(_instance, _random, child1Genes);
        var child2 = new Individual(_instance, _random, child2Genes);

        return (child1, child2);
    }

    /// <summary>
    /// Выполняет мутацию особи
    /// </summary>
    public void Mutate(double mutationRate)
    {
        var machineIds = _instance.VirtualMachines.Keys.ToList();

        foreach (var taskId in Chromosome.Keys)
        {
            if (_random.NextDouble() < mutationRate)
            {
                // Случайно меняем назначение задачи
                int currentMachineId = Chromosome[taskId];
                int newMachineId;

                do
                {
                    newMachineId = machineIds[_random.Next(machineIds.Count)];
                } while (newMachineId == currentMachineId && machineIds.Count > 1);

                Chromosome[taskId] = newMachineId;
            }
        }

        // Применяем репарацию после мутации
        RepairChromosome(Chromosome);
    }

    /// <summary>
    /// Создает глубокую копию особи
    /// </summary>
    public Individual Clone()
    {
        var clone = new Individual(_instance, _random, new Dictionary<int, int>(Chromosome))
        {
            Solution = Solution?.DeepCopy(),
            Age = Age
        };

        return clone;
    }

    /// <summary>
    /// Обновляет решение особи
    /// </summary>
    public void UpdateSolution(Solution solution)
    {
        Solution = solution;
        Age = 0;
    }

    /// <summary>
    /// Увеличивает возраст особи
    /// </summary>
    public void IncrementAge()
    {
        Age++;
    }
}