using Lab.PSO;
using Microsoft.JSInterop;

namespace Lab.Web.Services;

/// <summary>
/// Сервис для работы с алгоритмом PSO
/// </summary>
public class PsoService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private PsoAlgorithm? _algorithm;
    private ProblemInstance? _currentInstance;
    private Solution? _currentSolution;
    private VisualizationData? _visualizationData;
    private IProgress<AlgorithmProgress>? _progress;
    private bool _isStepMode;

    public event EventHandler<AlgorithmProgress>? OnProgressChanged;
    public event EventHandler<Solution>? OnSolutionFound;
    public event EventHandler<VisualizationData>? OnVisualizationDataUpdated;

    /// <summary>
    /// Текущий экземпляр задачи
    /// </summary>
    public ProblemInstance? CurrentInstance => _currentInstance;

    /// <summary>
    /// Текущее решение
    /// </summary>
    public Solution? CurrentSolution => _currentSolution;

    /// <summary>
    /// Текущие данные визуализации
    /// </summary>
    public VisualizationData? VisualizationData => _visualizationData;

    /// <summary>
    /// Статус выполнения алгоритма
    /// </summary>
    public AlgorithmStatus Status { get; private set; } = AlgorithmStatus.Idle;

    /// <summary>
    /// История прогресса
    /// </summary>
    private List<AlgorithmProgress> ProgressHistory { get; set; } = new();

    public PsoService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Инициализирует случайный экземпляр задачи
    /// </summary>
    public void InitializeRandomInstance(int taskCount, int machineCount, int? seed = null, 
        TaskGenerationConfig? generationConfig = null)
    {
        generationConfig ??= new TaskGenerationConfig();
        _currentInstance = ProblemInstance.CreateRandomInstance(taskCount, machineCount, 
            seed ?? Environment.TickCount, generationConfig);
        _currentSolution = null;
        _visualizationData = null;
        Status = AlgorithmStatus.Ready;

        NotifyStateChanged();
    }

    /// <summary>
    /// Запускает алгоритм с текущими параметрами
    /// </summary>
    public async System.Threading.Tasks.Task RunAlgorithmAsync(PsoConfiguration configuration)
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("Проблема не инициализирована. Сначала создайте экземпляр задачи.");
        }

        _isStepMode = false;
        _progress = null;

        Status = AlgorithmStatus.Running;
        ProgressHistory.Clear();
        NotifyStateChanged();

        try
        {
            DetachAlgorithmEvents();

            // Создаем алгоритм с заданными параметрами
            _algorithm = new PsoAlgorithm(_currentInstance, configuration.RandomSeed)
            {
                SwarmSize = configuration.SwarmSize,
                MaxIterations = configuration.MaxIterations,
                InertiaWeight = configuration.InertiaWeight,
                CognitiveWeight = configuration.CognitiveWeight,
                SocialWeight = configuration.SocialWeight,
                NoImprovementLimit = configuration.NoImprovementLimit
            };

            // Подписываемся на события
            _algorithm.IterationCompleted += HandleIterationCompleted;
            _algorithm.AlgorithmCompleted += HandleAlgorithmCompleted;

            // Запускаем алгоритм
            _progress = new Progress<AlgorithmProgress>(p =>
            {
                ProgressHistory.Add(p);
                OnProgressChanged?.Invoke(this, p);
            });

            _currentSolution = await _algorithm.RunAsync(_progress);

            // Обновляем данные визуализации
            UpdateVisualizationData();

            Status = AlgorithmStatus.Completed;
        }
        catch (Exception ex)
        {
            Status = AlgorithmStatus.Error;
            await ShowErrorAsync($"Ошибка выполнения алгоритма: {ex.Message}");
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    public bool CanStep => _isStepMode
                          && Status == AlgorithmStatus.Running
                          && _algorithm != null
                          && !_algorithm.IsComplete;

    public async System.Threading.Tasks.Task StartStepModeAsync(PsoConfiguration configuration)
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("Проблема не инициализирована. Сначала создайте экземпляр задачи.");
        }

        _isStepMode = true;

        Status = AlgorithmStatus.Running;
        ProgressHistory.Clear();
        _currentSolution = null;
        _visualizationData = null;
        NotifyStateChanged();

        try
        {
            DetachAlgorithmEvents();

            _algorithm = new PsoAlgorithm(_currentInstance, configuration.RandomSeed)
            {
                SwarmSize = configuration.SwarmSize,
                MaxIterations = configuration.MaxIterations,
                InertiaWeight = configuration.InertiaWeight,
                CognitiveWeight = configuration.CognitiveWeight,
                SocialWeight = configuration.SocialWeight,
                NoImprovementLimit = configuration.NoImprovementLimit
            };

            _algorithm.IterationCompleted += HandleIterationCompleted;
            _algorithm.AlgorithmCompleted += HandleAlgorithmCompleted;

            _progress = new Progress<AlgorithmProgress>(p =>
            {
                ProgressHistory.Add(p);
                OnProgressChanged?.Invoke(this, p);
            });

            _algorithm.Start();
        }
        catch (Exception ex)
        {
            Status = AlgorithmStatus.Error;
            await ShowErrorAsync($"Ошибка подготовки пошагового режима: {ex.Message}");
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    public async System.Threading.Tasks.Task StepAsync()
    {
        if (!CanStep)
        {
            return;
        }

        try
        {
            await _algorithm!.StepAsync(_progress);

            if (_algorithm.IsComplete)
            {
                _currentSolution = _algorithm.GlobalBestSolution;
                UpdateVisualizationData();
                Status = AlgorithmStatus.Completed;
                _isStepMode = false;
            }
        }
        catch (Exception ex)
        {
            Status = AlgorithmStatus.Error;
            await ShowErrorAsync($"Ошибка выполнения шага: {ex.Message}");
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Останавливает выполнение алгоритма
    /// </summary>
    public void StopAlgorithm()
    {
        if (_algorithm != null && Status == AlgorithmStatus.Running)
        {
            if (_isStepMode)
            {
                DetachAlgorithmEvents();
                _algorithm = null;
                _progress = null;
                _isStepMode = false;
            }

            Status = AlgorithmStatus.Stopped;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Сбрасывает текущее состояние
    /// </summary>
    public void Reset()
    {
        DetachAlgorithmEvents();
        _algorithm = null;
        _progress = null;
        _isStepMode = false;

        _currentSolution = null;
        _visualizationData = null;
        ProgressHistory.Clear();
        Status = AlgorithmStatus.Ready;

        NotifyStateChanged();
    }

    /// <summary>
    /// Экспортирует текущее решение в JSON
    /// </summary>
    public async Task<string?> ExportSolutionAsync()
    {
        if (_currentSolution == null)
            return null;

        var exportData = new
        {
            Instance = _currentInstance,
            Solution = _currentSolution,
            Visualization = _visualizationData
        };

        return exportData.ToJson();
    }

    /// <summary>
    /// Импортирует решение из JSON
    /// </summary>
    public async Task<bool> ImportSolutionAsync(string json)
    {
        try
        {
            var data = json.FromJson<dynamic>();
            // Здесь можно реализовать полную десериализацию
            // Для простоты просто обновим состояние
            Status = AlgorithmStatus.Completed;
            NotifyStateChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Обновляет данные визуализации на основе текущего решения
    /// </summary>
    private void UpdateVisualizationData()
    {
        if (_currentSolution != null && _currentInstance != null)
        {
            _visualizationData = VisualizationData.CreateFromSolution(_currentSolution, _currentInstance);
            OnVisualizationDataUpdated?.Invoke(this, _visualizationData);
        }
    }

    private void HandleIterationCompleted(object? sender, IterationCompletedEventArgs e)
    {
        // Обновляем визуализацию при каждой итерации
        if (_currentInstance != null && e.BestSolution != null)
        {
            _currentSolution = e.BestSolution;
            UpdateVisualizationData();
        }
    }

    private void HandleAlgorithmCompleted(object? sender, AlgorithmCompletedEventArgs e)
    {
        if (e.BestSolution != null)
        {
            OnSolutionFound?.Invoke(this, e.BestSolution);
        }
    }

    private void NotifyStateChanged()
    {
        OnProgressChanged?.Invoke(this, new AlgorithmProgress
        {
            Iteration = ProgressHistory.Count > 0 ? ProgressHistory[^1].Iteration : 0,
            BestFitness = _currentSolution?.Fitness ?? double.MaxValue,
            IsComplete = Status == AlgorithmStatus.Completed || Status == AlgorithmStatus.Error
        });
    }

    private async System.Threading.Tasks.Task ShowErrorAsync(string message)
    {
        await _jsRuntime.InvokeVoidAsync("console.error", message);
    }

    private void DetachAlgorithmEvents()
    {
        if (_algorithm == null)
        {
            return;
        }

        _algorithm.IterationCompleted -= HandleIterationCompleted;
        _algorithm.AlgorithmCompleted -= HandleAlgorithmCompleted;
    }

    public async ValueTask DisposeAsync()
    {
        DetachAlgorithmEvents();
        await System.Threading.Tasks.Task.CompletedTask;
    }
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
    public int? RandomSeed { get; set; }

    public static PsoConfiguration Default => new();
}

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