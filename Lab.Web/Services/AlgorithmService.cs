using Lab.Domain;
using Lab.Algorithms;

namespace Lab.Web.Services;

/// <summary>
/// Статус выполнения алгоритма
/// </summary>
public enum AlgorithmStatus
{
    Idle,
    Ready,
    Running,
    Completed,
    Stopped,
    Error
}

/// <summary>
/// Тип алгоритма
/// </summary>
public enum AlgorithmType
{
    Pso,
    Genetic
}

/// <summary>
/// Конфигурация алгоритма PSO
/// </summary>
public class PsoConfiguration
{
    public int SwarmSize { get; set; } = 50;
    public int MaxIterations { get; set; } = 500;
    public double InertiaWeight { get; set; } = 0.7;
    public double CognitiveWeight { get; set; } = 1.5;
    public double SocialWeight { get; set; } = 1.5;
    public int NoImprovementLimit { get; set; } = 50;

    public static PsoConfiguration Default => new();
}

/// <summary>
/// Конфигурация генетического алгоритма
/// </summary>
public class GaConfiguration
{
    public int PopulationSize { get; set; } = 100;
    public int MaxGenerations { get; set; } = 500;
    public double CrossoverRate { get; set; } = 0.8;
    public double MutationRate { get; set; } = 0.1;
    public double EliteRatio { get; set; } = 0.1;
    public int TournamentSize { get; set; } = 3;
    public int MaxAge { get; set; } = 50;
    public int NoImprovementLimit { get; set; } = 50;

    public static GaConfiguration Default => new();
}

/// <summary>
/// Сервис для работы с алгоритмами оптимизации
/// </summary>
public class AlgorithmService
{
    // Текущий алгоритм
    private AlgorithmType _currentAlgorithmType = AlgorithmType.Pso;
    private object? _currentAlgorithm;
    
    // Состояние
    public AlgorithmStatus Status { get; private set; } = AlgorithmStatus.Idle;
    public ProblemInstance? CurrentInstance { get; private set; }
    public Solution? CurrentSolution { get; private set; }
    public VisualizationData? VisualizationData { get; private set; }
    
    // История выполнения
    private readonly List<double> _fitnessHistory = new();
    
    // Конфигурации
    public PsoConfiguration PsoConfig { get; set; } = PsoConfiguration.Default;
    public GaConfiguration GaConfig { get; set; } = GaConfiguration.Default;
    
    // События
    public event EventHandler<AlgorithmProgress>? OnProgressChanged;
    public event EventHandler<Solution>? OnSolutionFound;
    public event EventHandler<VisualizationData>? OnVisualizationDataUpdated;
    public event EventHandler<AlgorithmStatus>? OnStatusChanged;
    
    // Поток выполнения
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isStepMode = false;
    
    public AlgorithmType CurrentAlgorithmType
    {
        get => _currentAlgorithmType;
        set
        {
            if (_currentAlgorithmType != value)
            {
                _currentAlgorithmType = value;
                ResetCurrentAlgorithm();
            }
        }
    }
    
    /// <summary>
    /// Возвращает, может ли алгоритм выполнить шаг
    /// </summary>
    public bool CanStep => Status == AlgorithmStatus.Running && _isStepMode;
    
    /// <summary>
    /// Инициализирует случайный экземпляр задачи
    /// </summary>
    public void InitializeRandomInstance(int taskCount, int machineCount, int? seed = null, 
        TaskGenerationConfig? generationConfig = null)
    {
        try
        {
            CurrentInstance = ProblemInstance.CreateRandomInstance(taskCount, machineCount, 
                seed ?? new Random().Next(), generationConfig);
            
            if (!CurrentInstance.Validate())
            {
                throw new InvalidOperationException("Некорректный экземпляр задачи (обнаружены циклы в графе предшествования)");
            }
            
            Status = AlgorithmStatus.Ready;
            CurrentSolution = null;
            VisualizationData = null;
            _fitnessHistory.Clear();
            
            OnStatusChanged?.Invoke(this, Status);
            
            // Сбрасываем текущий алгоритм
            ResetCurrentAlgorithm();
        }
        catch (Exception ex)
        {
            Status = AlgorithmStatus.Error;
            OnStatusChanged?.Invoke(this, Status);
            throw new InvalidOperationException($"Ошибка инициализации задачи: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Запускает алгоритм в обычном режиме
    /// </summary>
    public async System.Threading.Tasks.Task RunAlgorithmAsync()
    {
        if (CurrentInstance == null)
            throw new InvalidOperationException("Сначала инициализируйте задачу");
        
        if (Status == AlgorithmStatus.Running)
            throw new InvalidOperationException("Алгоритм уже выполняется");
        
        try
        {
            Status = AlgorithmStatus.Running;
            _isStepMode = false;
            OnStatusChanged?.Invoke(this, Status);
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Создаем и настраиваем алгоритм
            if (CurrentAlgorithmType == AlgorithmType.Pso)
            {
                var algorithm = CreatePsoAlgorithm();
                algorithm.IterationCompleted += HandlePsoIterationCompleted;
                algorithm.AlgorithmCompleted += HandleAlgorithmCompleted;
                _currentAlgorithm = algorithm;
                
                var progress = new Progress<AlgorithmProgress>(p =>
                {
                    OnProgressChanged?.Invoke(this, p);
                });
                
                var solution = await algorithm.RunAsync(progress);
                
                if (solution != null)
                {
                    SetSolution(solution);
                }
                
                algorithm.IterationCompleted -= HandlePsoIterationCompleted;
                algorithm.AlgorithmCompleted -= HandleAlgorithmCompleted;
            }
            else
            {
                var algorithm = CreateGeneticAlgorithm();
                algorithm.GenerationCompleted += HandleGaGenerationCompleted;
                algorithm.AlgorithmCompleted += HandleAlgorithmCompleted;
                _currentAlgorithm = algorithm;
                
                var progress = new Progress<AlgorithmProgress>(p =>
                {
                    OnProgressChanged?.Invoke(this, p);
                });
                
                var solution = await algorithm.RunAsync(progress);
                
                if (solution != null)
                {
                    SetSolution(solution);
                }
                
                algorithm.GenerationCompleted -= HandleGaGenerationCompleted;
                algorithm.AlgorithmCompleted -= HandleAlgorithmCompleted;
            }
        }
        catch (OperationCanceledException)
        {
            Status = AlgorithmStatus.Stopped;
            OnStatusChanged?.Invoke(this, Status);
        }
        catch (Exception ex)
        {
            Status = AlgorithmStatus.Error;
            OnStatusChanged?.Invoke(this, Status);
            throw new InvalidOperationException($"Ошибка выполнения алгоритма: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Запускает пошаговый режим
    /// </summary>
    public System.Threading.Tasks.Task StartStepModeAsync()
    {
        if (CurrentInstance == null)
            throw new InvalidOperationException("Сначала инициализируйте задачу");
        
        if (Status == AlgorithmStatus.Running)
            throw new InvalidOperationException("Алгоритм уже выполняется");
        
        Status = AlgorithmStatus.Running;
        _isStepMode = true;
        OnStatusChanged?.Invoke(this, Status);
        
        // Создаем алгоритм для пошагового режима
        if (CurrentAlgorithmType == AlgorithmType.Pso)
        {
            var algorithm = CreatePsoAlgorithm();
            algorithm.IterationCompleted += HandlePsoIterationCompleted;
            algorithm.AlgorithmCompleted += HandleAlgorithmCompleted;
            _currentAlgorithm = algorithm;
        }
        else
        {
            var algorithm = CreateGeneticAlgorithm();
            algorithm.GenerationCompleted += HandleGaGenerationCompleted;
            algorithm.AlgorithmCompleted += HandleAlgorithmCompleted;
            _currentAlgorithm = algorithm;
        }
        
        return System.Threading.Tasks.Task.CompletedTask;
    }
    
    /// <summary>
    /// Выполняет один шаг алгоритма
    /// </summary>
    public async System.Threading.Tasks.Task StepAsync()
    {
        if (!CanStep || _currentAlgorithm == null)
            return;
        
        try
        {
            if (_currentAlgorithm is PsoAlgorithm pso)
            {
                await pso.StepAsync();
            }
            else if (_currentAlgorithm is GeneticAlgorithm ga)
            {
                await ga.StepAsync();
            }
        }
        catch (Exception ex)
        {
            Status = AlgorithmStatus.Error;
            OnStatusChanged?.Invoke(this, Status);
            throw new InvalidOperationException($"Ошибка выполнения шага: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Останавливает выполнение алгоритма
    /// </summary>
    public void StopAlgorithm()
    {
        if (Status != AlgorithmStatus.Running)
            return;
        
        _cancellationTokenSource?.Cancel();
        
        if (_currentAlgorithm != null)
        {
            // Отписываемся от событий
            if (_currentAlgorithm is PsoAlgorithm pso)
            {
                pso.IterationCompleted -= HandlePsoIterationCompleted;
                pso.AlgorithmCompleted -= HandleAlgorithmCompleted;
            }
            else if (_currentAlgorithm is GeneticAlgorithm ga)
            {
                ga.GenerationCompleted -= HandleGaGenerationCompleted;
                ga.AlgorithmCompleted -= HandleAlgorithmCompleted;
            }
        }
        
        Status = AlgorithmStatus.Stopped;
        OnStatusChanged?.Invoke(this, Status);
    }
    
    /// <summary>
    /// Сбрасывает состояние сервиса
    /// </summary>
    public void Reset()
    {
        StopAlgorithm();
        
        CurrentInstance = null;
        CurrentSolution = null;
        VisualizationData = null;
        _fitnessHistory.Clear();
        _currentAlgorithm = null;
        
        Status = AlgorithmStatus.Idle;
        OnStatusChanged?.Invoke(this, Status);
    }
    
    /// <summary>
    /// Возвращает данные визуализации для текущего решения
    /// </summary>
    private VisualizationData? GetVisualizationData()
    {
        if (CurrentSolution == null || CurrentInstance == null)
            return null;
        
        VisualizationData = VisualizationData.CreateFromSolution(CurrentSolution, CurrentInstance);
        OnVisualizationDataUpdated?.Invoke(this, VisualizationData);
        
        return VisualizationData;
    }
    
    // Вспомогательные методы
    
    private PsoAlgorithm CreatePsoAlgorithm()
    {
        if (CurrentInstance == null)
            throw new InvalidOperationException("Экземпляр задачи не инициализирован");
        
        var randomSeed = new Random().Next();
        
        return new PsoAlgorithm(CurrentInstance, randomSeed)
        {
            SwarmSize = PsoConfig.SwarmSize,
            MaxIterations = PsoConfig.MaxIterations,
            InertiaWeight = PsoConfig.InertiaWeight,
            CognitiveWeight = PsoConfig.CognitiveWeight,
            SocialWeight = PsoConfig.SocialWeight,
            NoImprovementLimit = PsoConfig.NoImprovementLimit
        };
    }
    
    private GeneticAlgorithm CreateGeneticAlgorithm()
    {
        if (CurrentInstance == null)
            throw new InvalidOperationException("Экземпляр задачи не инициализирован");
        
        var randomSeed = new Random().Next();
        
        return new GeneticAlgorithm(CurrentInstance, randomSeed)
        {
            PopulationSize = GaConfig.PopulationSize,
            MaxGenerations = GaConfig.MaxGenerations,
            CrossoverRate = GaConfig.CrossoverRate,
            MutationRate = GaConfig.MutationRate,
            EliteRatio = GaConfig.EliteRatio,
            TournamentSize = GaConfig.TournamentSize,
            MaxAge = GaConfig.MaxAge,
            NoImprovementLimit = GaConfig.NoImprovementLimit
        };
    }
    
    private void ResetCurrentAlgorithm()
    {
        _currentAlgorithm = null;
        _fitnessHistory.Clear();
    }
    
    private void SetSolution(Solution solution)
    {
        CurrentSolution = solution;
        CurrentSolution.FitnessHistory = new List<double>(_fitnessHistory);
        
        // Обновляем данные визуализации
        GetVisualizationData();
        
        Status = AlgorithmStatus.Completed;
        OnStatusChanged?.Invoke(this, Status);
        OnSolutionFound?.Invoke(this, solution);
    }
    
    // Обработчики событий
    
    private void HandlePsoIterationCompleted(object? sender, IterationCompletedEventArgs e)
    {
        _fitnessHistory.Add(e.BestFitness);
        
        if (e.BestSolution != null)
        {
            SetSolution(e.BestSolution);
        }
    }
    
    private void HandleGaGenerationCompleted(object? sender, GenerationCompletedEventArgs e)
    {
        _fitnessHistory.Add(e.BestFitness);
        
        if (e.BestSolution != null)
        {
            SetSolution(e.BestSolution);
        }
    }
    
    private void HandleAlgorithmCompleted(object? sender, AlgorithmCompletedEventArgs e)
    {
        if (e.BestSolution != null)
        {
            SetSolution(e.BestSolution);
        }
        
        if (_currentAlgorithm is PsoAlgorithm pso)
        {
            pso.IterationCompleted -= HandlePsoIterationCompleted;
            pso.AlgorithmCompleted -= HandleAlgorithmCompleted;
        }
        else if (_currentAlgorithm is GeneticAlgorithm ga)
        {
            ga.GenerationCompleted -= HandleGaGenerationCompleted;
            ga.AlgorithmCompleted -= HandleAlgorithmCompleted;
        }
        
        _currentAlgorithm = null;
    }
}