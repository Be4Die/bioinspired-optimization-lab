using System.Diagnostics;
using Lab.Domain;

namespace Lab.Algorithms;

/// <summary>
/// Реализация алгоритма роя частиц для задачи распределения задач
/// </summary>
public class PsoAlgorithm
{
    // Параметры алгоритма
    public int SwarmSize { get; init; } = 50;
    public int MaxIterations { get; init; } = 500;
    public double InertiaWeight { get; init; } = 0.7;
    public double CognitiveWeight { get; init; } = 1.5;
    public double SocialWeight { get; init; } = 1.5;
    public int NoImprovementLimit { get; init; } = 50;

    // Состояние алгоритма
    private List<Particle> Swarm { get; set; } = null!;
    private Dictionary<int, int> GlobalBestPosition { get; set; } = null!;
    public Solution? GlobalBestSolution { get; private set; }
    private double GlobalBestFitness { get; set; } = double.MaxValue;

    // История выполнения
    private List<double> GlobalBestFitnessHistory { get; set; } = new();
    private List<double> AverageFitnessHistory { get; set; } = new();
    private List<Solution> IterationBestSolutions { get; set; } = new();

    // Вспомогательные объекты
    private readonly ProblemInstance _instance;
    private readonly Scheduler _scheduler;
    private readonly Random _random;
    private readonly object _lockObject = new();
    private Stopwatch? _stopwatch;
    private bool _isInitialized;
    private int _iteration;
    private int _noImprovementCount;

    // События для визуализации
    public event EventHandler<IterationCompletedEventArgs>? IterationCompleted;
    public event EventHandler<AlgorithmCompletedEventArgs>? AlgorithmCompleted;

    public PsoAlgorithm(ProblemInstance instance, int? randomSeed = null)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _scheduler = new Scheduler(instance);
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        Swarm = new List<Particle>();
        GlobalBestPosition = new Dictionary<int, int>();
        IterationCompleted = null;
        AlgorithmCompleted = null;
    }

    /// <summary>
    /// Запускает выполнение алгоритма
    /// </summary>
    public async Task<Solution?> RunAsync(IProgress<AlgorithmProgress>? progress = null)
    {
        Start();

        while (!IsComplete)
        {
            await StepAsync(progress);
        }

        return GlobalBestSolution;
    }

    public bool IsComplete => _isInitialized && (_iteration >= MaxIterations || _noImprovementCount >= NoImprovementLimit);

    public void Start()
    {
        Reset();
        InitializeSwarm();
        _stopwatch = Stopwatch.StartNew();
        _isInitialized = true;
    }

    public async Task<Solution?> StepAsync(IProgress<AlgorithmProgress>? progress = null)
    {
        if (!_isInitialized)
        {
            Start();
        }

        if (IsComplete)
        {
            return GlobalBestSolution;
        }

        _iteration++;

        var positions = Swarm.Select(p => p.Position).ToList();
        var solutions = _scheduler.CalculateSchedulesParallel(positions);

        UpdateParticles(solutions);
        UpdateSwarm();
        SaveIterationHistory();

        if (GlobalBestFitness < (GlobalBestFitnessHistory.LastOrDefault(double.MaxValue)))
        {
            _noImprovementCount = 0;
        }
        else
        {
            _noImprovementCount++;
        }

        progress?.Report(new AlgorithmProgress
        {
            Iteration = _iteration,
            BestFitness = GlobalBestFitness,
            AverageFitness = AverageFitnessHistory.LastOrDefault(),
            IsComplete = false
        });

        OnIterationCompleted(new IterationCompletedEventArgs
        {
            Iteration = _iteration,
            BestSolution = GlobalBestSolution,
            BestFitness = GlobalBestFitness,
            AverageFitness = AverageFitnessHistory.LastOrDefault()
        });

        await System.Threading.Tasks.Task.Delay(10);

        if (IsComplete)
        {
            Finish(progress);
        }

        return GlobalBestSolution;
    }

    private void InitializeSwarm()
    {
        Swarm = new List<Particle>();

        for (int i = 0; i < SwarmSize; i++)
        {
            var particle = new Particle(_instance, _random);
            Swarm.Add(particle);
        }

        // Инициализация глобальной лучшей позиции
        GlobalBestPosition = new Dictionary<int, int>(Swarm[0].Position);
    }

    private void UpdateParticles(List<Solution> solutions)
    {
        Parallel.For(0, Swarm.Count, i =>
        {
            var particle = Swarm[i];
            var solution = solutions[i];

            // Обновление лучшей позиции частицы
            particle.UpdateBestPosition(solution);

            // Обновление глобальной лучшей позиции
            lock (_lockObject)
            {
                if (solution.Fitness < GlobalBestFitness)
                {
                    GlobalBestFitness = solution.Fitness;
                    GlobalBestPosition = new Dictionary<int, int>(particle.Position);
                    GlobalBestSolution = solution.DeepCopy();
                }
            }
        });
    }

    private void UpdateSwarm()
    {
        Parallel.ForEach(Swarm, particle =>
        {
            particle.UpdateVelocity(GlobalBestPosition, InertiaWeight, CognitiveWeight, SocialWeight);
            particle.UpdatePosition();
        });
    }

    private void SaveIterationHistory()
    {
        // Сохраняем лучшее фитнес-значение
        GlobalBestFitnessHistory.Add(GlobalBestFitness);

        // Вычисляем среднее фитнес-значение
        double averageFitness = Swarm.Average(p => p.CurrentSolution?.Fitness ?? double.MaxValue);
        AverageFitnessHistory.Add(averageFitness);

        // Сохраняем лучшее решение итерации
        if (GlobalBestSolution != null)
        {
            var iterationBest = GlobalBestSolution.DeepCopy();
            iterationBest.FitnessHistory = new List<double>(GlobalBestFitnessHistory);
            IterationBestSolutions.Add(iterationBest);
        }
    }

    /// <summary>
    /// Сбрасывает состояние алгоритма для нового запуска
    /// </summary>
    private void Reset()
    {
        Swarm = null!;
        GlobalBestPosition = null!;
        GlobalBestSolution = null;
        GlobalBestFitness = double.MaxValue;
        GlobalBestFitnessHistory.Clear();
        AverageFitnessHistory.Clear();
        IterationBestSolutions.Clear();
        _stopwatch = null;
        _isInitialized = false;
        _iteration = 0;
        _noImprovementCount = 0;
    }

    private void Finish(IProgress<AlgorithmProgress>? progress)
    {
        _stopwatch?.Stop();

        if (GlobalBestSolution != null && _stopwatch != null)
        {
            GlobalBestSolution.ComputationTime = _stopwatch.Elapsed;
            GlobalBestSolution.IterationFound = _iteration - _noImprovementCount;
        }

        OnAlgorithmCompleted(new AlgorithmCompletedEventArgs
        {
            BestSolution = GlobalBestSolution,
            TotalIterations = _iteration,
            ComputationTime = _stopwatch?.Elapsed ?? TimeSpan.Zero
        });

        progress?.Report(new AlgorithmProgress
        {
            Iteration = _iteration,
            BestFitness = GlobalBestFitness,
            AverageFitness = AverageFitnessHistory.LastOrDefault(),
            IsComplete = true
        });
    }

    /// <summary>
    /// Находит лучшие машины для каждой задачи на основе локального поиска
    /// </summary>
    public Dictionary<int, List<int>> GetMachineRankings()
    {
        var rankings = new Dictionary<int, List<int>>();
        var machineIds = _instance.VirtualMachines.Keys.ToList();

        foreach (var task in _instance.Tasks.Values)
        {
            // Сортируем машины по пригодности для задачи
            var rankedMachines = machineIds
                .OrderBy(machineId =>
                {
                    var machine = _instance.VirtualMachines[machineId];

                    // Штраф за недостаток памяти
                    double memoryPenalty = machine.HasSufficientMemory(task.MemoryRequirement) ? 0 : 1000;

                    // Время выполнения
                    double executionTime = machine.CalculateExecutionTime(task.ComputationVolume);

                    return executionTime + memoryPenalty;
                })
                .ToList();

            rankings[task.Id] = rankedMachines;
        }

        return rankings;
    }

    protected virtual void OnIterationCompleted(IterationCompletedEventArgs e)
    {
        IterationCompleted?.Invoke(this, e);
    }

    protected virtual void OnAlgorithmCompleted(AlgorithmCompletedEventArgs e)
    {
        AlgorithmCompleted?.Invoke(this, e);
    }
}

/// <summary>
/// Аргументы события завершения итерации
/// </summary>
public class IterationCompletedEventArgs : EventArgs
{
    public int Iteration { get; init; }
    public Solution? BestSolution { get; init; }
    public double BestFitness { get; init; }
    public double AverageFitness { get; init; }
}

/// <summary>
/// Аргументы события завершения алгоритма
/// </summary>
public class AlgorithmCompletedEventArgs : EventArgs
{
    public Solution? BestSolution { get; init; }
    public int TotalIterations { get; init; }
    public TimeSpan ComputationTime { get; init; }
}

/// <summary>
/// Прогресс выполнения алгоритма
/// </summary>
public class AlgorithmProgress
{
    public int Iteration { get; init; }
    public double BestFitness { get; init; }
    public double AverageFitness { get; init; }
    public bool IsComplete { get; init; }
}