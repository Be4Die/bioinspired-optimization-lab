using Lab.Domain;

namespace Lab.Algorithms;

/// <summary>
/// Представляет частицу в алгоритме роя частиц
/// </summary>
public class Particle
{
    private readonly Random _random;
    private readonly ProblemInstance _instance;

    /// <summary>
    /// Текущая позиция частицы (назначение задач)
    /// </summary>
    public Dictionary<int, int> Position { get; set; }

    /// <summary>
    /// Скорость частицы (вероятность изменения назначения)
    /// </summary>
    private Dictionary<int, double> Velocity { get; set; }

    /// <summary>
    /// Лучшая позиция, найденная частицей
    /// </summary>
    private Dictionary<int, int> BestPosition { get; set; }

    /// <summary>
    /// Фитнес-значение лучшей позиции
    /// </summary>
    private double BestFitness { get; set; } = double.MaxValue;

    /// <summary>
    /// Текущее решение
    /// </summary>
    public Solution? CurrentSolution { get; private set; }

    /// <summary>
    /// Лучшее решение, найденное частицей
    /// </summary>
    public Solution? BestSolution { get; private set; }

    public Particle(ProblemInstance instance, Random random)
    {
        _instance = instance;
        _random = random;

        // Инициализация позиции
        Position = InitializePosition();
        Velocity = InitializeVelocity();
        BestPosition = new Dictionary<int, int>(Position);
    }

    private Dictionary<int, int> InitializePosition()
    {
        var position = new Dictionary<int, int>();
        var machineIds = _instance.VirtualMachines.Keys.ToList();

        foreach (var task in _instance.Tasks.Values)
        {
            // Случайное назначение на машину
            int machineId = machineIds[_random.Next(machineIds.Count)];
            position[task.Id] = machineId;
        }

        // Применяем репарацию для соблюдения ограничений по памяти
        RepairPosition(position);

        return position;
    }

    private Dictionary<int, double> InitializeVelocity()
    {
        var velocity = new Dictionary<int, double>();

        // Инициализация скорости случайными значениями в диапазоне [0, 1]
        foreach (var task in _instance.Tasks.Values)
        {
            velocity[task.Id] = _random.NextDouble();
        }

        return velocity;
    }

    /// <summary>
    /// Обновляет скорость частицы
    /// </summary>
    public void UpdateVelocity(
        Dictionary<int, int> globalBestPosition,
        double inertiaWeight,
        double cognitiveWeight,
        double socialWeight)
    {
        foreach (var taskId in Position.Keys)
        {
            double currentVelocity = Velocity[taskId];
            double r1 = _random.NextDouble();
            double r2 = _random.NextDouble();

            // Дискретная версия PSO: вероятность изменения
            double cognitiveComponent = (BestPosition[taskId] != Position[taskId]) ? 1 : 0;
            double socialComponent = (globalBestPosition[taskId] != Position[taskId]) ? 1 : 0;

            double newVelocity = inertiaWeight * currentVelocity
                                 + cognitiveWeight * r1 * cognitiveComponent
                                 + socialWeight * r2 * socialComponent;

            // Ограничиваем скорость
            Velocity[taskId] = Math.Max(0, Math.Min(1, newVelocity));
        }
    }

    /// <summary>
    /// Обновляет позицию частицы на основе скорости
    /// </summary>
    public void UpdatePosition()
    {
        var machineIds = _instance.VirtualMachines.Keys.ToList();

        foreach (var taskId in Position.Keys)
        {
            // Интерпретируем скорость как вероятность изменения назначения
            if (_random.NextDouble() < Velocity[taskId])
            {
                // Случайно изменяем назначение
                int currentMachineId = Position[taskId];
                int newMachineId;

                do
                {
                    newMachineId = machineIds[_random.Next(machineIds.Count)];
                } while (newMachineId == currentMachineId && machineIds.Count > 1);

                Position[taskId] = newMachineId;
            }
        }

        // Применяем репарацию
        RepairPosition(Position);
    }

    /// <summary>
    /// Ремонтирует позицию для соблюдения ограничений по памяти
    /// </summary>
    private void RepairPosition(Dictionary<int, int> position)
    {
        var machineIds = _instance.VirtualMachines.Keys.ToList();

        foreach (var (taskId, machineId) in position)
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
                    position[taskId] = newMachineId;
                }
            }
        }
    }

    /// <summary>
    /// Обновляет лучшую позицию частицы
    /// </summary>
    public void UpdateBestPosition(Solution currentSolution)
    {
        CurrentSolution = currentSolution;

        if (currentSolution.Fitness < BestFitness)
        {
            BestFitness = currentSolution.Fitness;
            BestPosition = new Dictionary<int, int>(Position);
            BestSolution = currentSolution.DeepCopy();
        }
    }
}