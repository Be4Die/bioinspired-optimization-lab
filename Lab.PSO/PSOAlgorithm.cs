using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lab.PSO;

/// <summary>
/// Реализация алгоритма роя частиц для задачи распределения задач
/// </summary>
public class PSOAlgorithm
{
    // Параметры алгоритма
    public int SwarmSize { get; set; } = 50;
    public int MaxIterations { get; set; } = 500;
    public double InertiaWeight { get; set; } = 0.7;
    public double CognitiveWeight { get; set; } = 1.5;
    public double SocialWeight { get; set; } = 1.5;
    public int NoImprovementLimit { get; set; } = 50;

    // Состояние алгоритма
    public List<Particle> Swarm { get; private set; }
    public Dictionary<int, int> GlobalBestPosition { get; private set; }
    public Solution GlobalBestSolution { get; private set; }
    public double GlobalBestFitness { get; private set; } = double.MaxValue;

    // История выполнения
    public List<double> GlobalBestFitnessHistory { get; private set; } = new();
    public List<double> AverageFitnessHistory { get; private set; } = new();
    public List<Solution> IterationBestSolutions { get; private set; } = new();

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
    public event EventHandler<IterationCompletedEventArgs> IterationCompleted;
    public event EventHandler<AlgorithmCompletedEventArgs> AlgorithmCompleted;

    public PSOAlgorithm(ProblemInstance instance, int? randomSeed = null)
    {
        _instance = instance;
        _scheduler = new Scheduler(instance);
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
    }

    /// <summary>
    /// Запускает выполнение алгоритма
    /// </summary>
    public async Task<Solution> RunAsync(IProgress<AlgorithmProgress> progress = null)
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

    public async Task<Solution> StepAsync(IProgress<AlgorithmProgress> progress = null)
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
        SaveIterationHistory(_iteration);

        if (GlobalBestFitness < GlobalBestFitnessHistory.LastOrDefault(double.MaxValue))
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
            AverageFitness = AverageFitnessHistory.Last(),
            IsComplete = false
        });

        OnIterationCompleted(new IterationCompletedEventArgs
        {
            Iteration = _iteration,
            BestSolution = GlobalBestSolution,
            BestFitness = GlobalBestFitness,
            AverageFitness = AverageFitnessHistory.Last()
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

    private void SaveIterationHistory(int iteration)
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
    public void Reset()
    {
        Swarm = null;
        GlobalBestPosition = null;
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

    private void Finish(IProgress<AlgorithmProgress> progress)
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
    public int Iteration { get; set; }
    public Solution BestSolution { get; set; }
    public double BestFitness { get; set; }
    public double AverageFitness { get; set; }
}

/// <summary>
/// Аргументы события завершения алгоритма
/// </summary>
public class AlgorithmCompletedEventArgs : EventArgs
{
    public Solution BestSolution { get; set; }
    public int TotalIterations { get; set; }
    public TimeSpan ComputationTime { get; set; }
}

/// <summary>
/// Прогресс выполнения алгоритма
/// </summary>
public class AlgorithmProgress
{
    public int Iteration { get; set; }
    public double BestFitness { get; set; }
    public double AverageFitness { get; set; }
    public bool IsComplete { get; set; }
}
