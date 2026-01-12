using System.Diagnostics;
using Lab.Domain;

namespace Lab.Algorithms;

/// <summary>
/// Реализация генетического алгоритма для задачи распределения задач
/// </summary>
public class GeneticAlgorithm
{
    // Параметры алгоритма
    public int PopulationSize { get; init; } = 100;
    public int MaxGenerations { get; init; } = 500;
    public double CrossoverRate { get; init; } = 0.8;
    public double MutationRate { get; init; } = 0.1;
    public double EliteRatio { get; init; } = 0.1;
    public int TournamentSize { get; init; } = 3;
    public int MaxAge { get; init; } = 50;
    public int NoImprovementLimit { get; init; } = 50;

    // Состояние алгоритма
    private List<Individual> Population { get; set; } = null!;
    public Individual? BestIndividual { get; private set; }
    public Solution? BestSolution => BestIndividual?.Solution;
    private double BestFitness { get; set; } = double.MaxValue;

    // История выполнения
    private List<double> BestFitnessHistory { get; set; } = new();
    private List<double> AverageFitnessHistory { get; set; } = new();
    private List<Solution> GenerationBestSolutions { get; set; } = new();

    // Вспомогательные объекты
    private readonly ProblemInstance _instance;
    private readonly Scheduler _scheduler;
    private readonly Random _random;
    private readonly object _lockObject = new();
    private Stopwatch? _stopwatch;
    private bool _isInitialized;
    private int _generation;
    private int _noImprovementCount;

    // События для визуализации
    public event EventHandler<GenerationCompletedEventArgs>? GenerationCompleted;
    public event EventHandler<AlgorithmCompletedEventArgs>? AlgorithmCompleted;

    public GeneticAlgorithm(ProblemInstance instance, int? randomSeed = null)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _scheduler = new Scheduler(instance);
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        Population = new List<Individual>();
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

        return BestSolution;
    }

    public bool IsComplete => _isInitialized && (_generation >= MaxGenerations || _noImprovementCount >= NoImprovementLimit);

    public void Start()
    {
        Reset();
        InitializePopulation();
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
            return BestSolution;
        }

        _generation++;

        // Оценка популяции
        EvaluatePopulation();

        // Сохранение истории
        SaveGenerationHistory();

        // Проверка улучшения
        if (BestFitness < (BestFitnessHistory.LastOrDefault(double.MaxValue)))
        {
            _noImprovementCount = 0;
        }
        else
        {
            _noImprovementCount++;
        }

        // Формирование нового поколения
        var newPopulation = CreateNewPopulation();

        // Замена старой популяции новой
        Population = newPopulation;

        progress?.Report(new AlgorithmProgress
        {
            Iteration = _generation,
            BestFitness = BestFitness,
            AverageFitness = AverageFitnessHistory.LastOrDefault(),
            IsComplete = false
        });

        OnGenerationCompleted(new GenerationCompletedEventArgs
        {
            Generation = _generation,
            BestSolution = BestSolution,
            BestFitness = BestFitness,
            AverageFitness = AverageFitnessHistory.LastOrDefault(),
            PopulationSize = Population.Count
        });

        await System.Threading.Tasks.Task.Delay(10);

        if (IsComplete)
        {
            Finish(progress);
        }

        return BestSolution;
    }

    private void InitializePopulation()
    {
        Population = new List<Individual>();

        for (int i = 0; i < PopulationSize; i++)
        {
            var individual = new Individual(_instance, _random);
            Population.Add(individual);
        }

        // Оценка начальной популяции
        EvaluatePopulation();
    }

    private void EvaluatePopulation()
    {
        // Получаем хромосомы всех особей
        var chromosomes = Population.Select(ind => ind.Chromosome).ToList();
        
        // Параллельно вычисляем решения
        var solutions = _scheduler.CalculateSchedulesParallel(chromosomes);

        // Обновляем решения особей
        Parallel.For(0, Population.Count, i =>
        {
            var individual = Population[i];
            var solution = solutions[i];
            individual.UpdateSolution(solution);

            // Обновляем лучшую особь
            lock (_lockObject)
            {
                if (solution.Fitness < BestFitness)
                {
                    BestFitness = solution.Fitness;
                    BestIndividual = individual.Clone();
                }
            }
        });
    }

    private List<Individual> CreateNewPopulation()
    {
        var newPopulation = new List<Individual>();

        // Элитизм: сохраняем лучшие особи
        int eliteCount = Math.Max(1, (int)(PopulationSize * EliteRatio));
        var eliteIndividuals = Population
            .OrderBy(ind => ind.Fitness)
            .Take(eliteCount)
            .Select(ind => ind.Clone())
            .ToList();

        newPopulation.AddRange(eliteIndividuals);

        // Создаем остальных особей через селекцию и кроссовер
        while (newPopulation.Count < PopulationSize)
        {
            // Селекция турниром
            var parent1 = TournamentSelection();
            var parent2 = TournamentSelection();

            // Кроссовер
            var (child1, child2) = parent1.Crossover(parent2, CrossoverRate);

            // Мутация
            child1.Mutate(MutationRate);
            child2.Mutate(MutationRate);

            newPopulation.Add(child1);
            
            if (newPopulation.Count < PopulationSize)
            {
                newPopulation.Add(child2);
            }
        }

        // Увеличиваем возраст всех особей
        foreach (var individual in newPopulation)
        {
            individual.IncrementAge();
        }

        // Удаляем старые особи (кроме элитных)
        if (MaxAge > 0)
        {
            newPopulation = newPopulation
                .Where(ind => ind.Age <= MaxAge || eliteIndividuals.Contains(ind))
                .Take(PopulationSize)
                .ToList();
        }

        return newPopulation;
    }

    private Individual TournamentSelection()
    {
        var tournamentParticipants = new List<Individual>();

        for (int i = 0; i < TournamentSize; i++)
        {
            int index = _random.Next(Population.Count);
            tournamentParticipants.Add(Population[index]);
        }

        return tournamentParticipants
            .OrderBy(ind => ind.Fitness)
            .First()
            .Clone();
    }

    private void SaveGenerationHistory()
    {
        // Сохраняем лучшее фитнес-значение
        BestFitnessHistory.Add(BestFitness);

        // Вычисляем среднее фитнес-значение
        double averageFitness = Population.Average(ind => ind.Fitness);
        AverageFitnessHistory.Add(averageFitness);

        // Сохраняем лучшее решение поколения
        if (BestSolution != null)
        {
            var generationBest = BestSolution.DeepCopy();
            generationBest.FitnessHistory = new List<double>(BestFitnessHistory);
            GenerationBestSolutions.Add(generationBest);
        }
    }

    /// <summary>
    /// Сбрасывает состояние алгоритма для нового запуска
    /// </summary>
    private void Reset()
    {
        Population = null!;
        BestIndividual = null;
        BestFitness = double.MaxValue;
        BestFitnessHistory.Clear();
        AverageFitnessHistory.Clear();
        GenerationBestSolutions.Clear();
        _stopwatch = null;
        _isInitialized = false;
        _generation = 0;
        _noImprovementCount = 0;
    }

    private void Finish(IProgress<AlgorithmProgress>? progress)
    {
        _stopwatch?.Stop();

        if (BestSolution != null && _stopwatch != null)
        {
            BestSolution.ComputationTime = _stopwatch.Elapsed;
            BestSolution.IterationFound = _generation - _noImprovementCount;
        }

        OnAlgorithmCompleted(new AlgorithmCompletedEventArgs
        {
            BestSolution = BestSolution,
            TotalIterations = _generation,
            ComputationTime = _stopwatch?.Elapsed ?? TimeSpan.Zero
        });

        progress?.Report(new AlgorithmProgress
        {
            Iteration = _generation,
            BestFitness = BestFitness,
            AverageFitness = AverageFitnessHistory.LastOrDefault(),
            IsComplete = true
        });
    }

    /// <summary>
    /// Выполняет локальный поиск для улучшения решения
    /// </summary>
    public Solution? LocalSearch(Solution solution, int maxIterations = 100)
    {
        var currentSolution = solution.DeepCopy();
        var bestSolution = currentSolution.DeepCopy();
        double bestFitness = bestSolution.Fitness;

        for (int i = 0; i < maxIterations; i++)
        {
            bool improved = false;

            // Пробуем переместить каждую задачу на другую машину
            foreach (var (taskId, machineId) in currentSolution.Assignment)
            {
                var otherMachines = _instance.VirtualMachines.Keys
                    .Where(id => id != machineId)
                    .ToList();

                foreach (var newMachineId in otherMachines)
                {
                    // Создаем новое назначение
                    var newAssignment = new Dictionary<int, int>(currentSolution.Assignment);
                    newAssignment[taskId] = newMachineId;

                    // Оцениваем новое решение
                    var scheduler = new Scheduler(_instance);
                    var newSolution = scheduler.CalculateSchedule(newAssignment);

                    if (newSolution.Fitness < bestFitness)
                    {
                        bestSolution = newSolution;
                        bestFitness = newSolution.Fitness;
                        improved = true;
                    }
                }
            }

            if (!improved)
                break;

            currentSolution = bestSolution.DeepCopy();
        }

        return bestSolution;
    }

    protected virtual void OnGenerationCompleted(GenerationCompletedEventArgs e)
    {
        GenerationCompleted?.Invoke(this, e);
    }

    protected virtual void OnAlgorithmCompleted(AlgorithmCompletedEventArgs e)
    {
        AlgorithmCompleted?.Invoke(this, e);
    }
}

/// <summary>
/// Аргументы события завершения поколения
/// </summary>
public class GenerationCompletedEventArgs : EventArgs
{
    public int Generation { get; init; }
    public Solution? BestSolution { get; init; }
    public double BestFitness { get; init; }
    public double AverageFitness { get; init; }
    public int PopulationSize { get; init; }
}