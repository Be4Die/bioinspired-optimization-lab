using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab.PSO;
using Microsoft.JSInterop;

namespace Lab.Web.Services;

/// <summary>
/// Сервис для работы с алгоритмом PSO
/// </summary>
public class PSOService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private PSOAlgorithm _algorithm;
    private ProblemInstance _currentInstance;
    private Solution _currentSolution;
    private VisualizationData _visualizationData;

    public event EventHandler<AlgorithmProgress> OnProgressChanged;
    public event EventHandler<Solution> OnSolutionFound;
    public event EventHandler<VisualizationData> OnVisualizationDataUpdated;

    /// <summary>
    /// Текущий экземпляр задачи
    /// </summary>
    public ProblemInstance CurrentInstance => _currentInstance;

    /// <summary>
    /// Текущее решение
    /// </summary>
    public Solution CurrentSolution => _currentSolution;

    /// <summary>
    /// Текущие данные визуализации
    /// </summary>
    public VisualizationData VisualizationData => _visualizationData;

    /// <summary>
    /// Статус выполнения алгоритма
    /// </summary>
    public AlgorithmStatus Status { get; private set; } = AlgorithmStatus.Idle;

    /// <summary>
    /// История прогресса
    /// </summary>
    public List<AlgorithmProgress> ProgressHistory { get; private set; } = new();

    public PSOService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Инициализирует случайный экземпляр задачи
    /// </summary>
    public void InitializeRandomInstance(int taskCount, int machineCount, int? seed = null)
    {
        _currentInstance = ProblemInstance.CreateRandomInstance(taskCount, machineCount, seed ?? Environment.TickCount);
        _currentSolution = null;
        _visualizationData = null;
        Status = AlgorithmStatus.Ready;

        NotifyStateChanged();
    }

    /// <summary>
    /// Запускает алгоритм с текущими параметрами
    /// </summary>
    public async System.Threading.Tasks.Task RunAlgorithmAsync(PSOConfiguration configuration)
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("Проблема не инициализирована. Сначала создайте экземпляр задачи.");
        }

        Status = AlgorithmStatus.Running;
        ProgressHistory.Clear();
        NotifyStateChanged();

        try
        {
            // Создаем алгоритм с заданными параметрами
            _algorithm = new PSOAlgorithm(_currentInstance, configuration.RandomSeed)
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
            var progress = new Progress<AlgorithmProgress>(p =>
            {
                ProgressHistory.Add(p);
                OnProgressChanged?.Invoke(this, p);
            });

            _currentSolution = await _algorithm.RunAsync(progress);

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

    /// <summary>
    /// Останавливает выполнение алгоритма
    /// </summary>
    public void StopAlgorithm()
    {
        if (_algorithm != null && Status == AlgorithmStatus.Running)
        {
            // В текущей реализации алгоритм не поддерживает остановку,
            // но мы можем изменить его состояние
            Status = AlgorithmStatus.Stopped;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Сбрасывает текущее состояние
    /// </summary>
    public void Reset()
    {
        _currentSolution = null;
        _visualizationData = null;
        ProgressHistory.Clear();
        Status = AlgorithmStatus.Ready;

        NotifyStateChanged();
    }

    /// <summary>
    /// Экспортирует текущее решение в JSON
    /// </summary>
    public async Task<string> ExportSolutionAsync()
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

    private void HandleIterationCompleted(object sender, IterationCompletedEventArgs e)
    {
        // Обновляем визуализацию при каждой итерации
        if (_currentInstance != null && e.BestSolution != null)
        {
            _currentSolution = e.BestSolution;
            UpdateVisualizationData();
        }
    }

    private void HandleAlgorithmCompleted(object sender, AlgorithmCompletedEventArgs e)
    {
        OnSolutionFound?.Invoke(this, e.BestSolution);
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

    public async ValueTask DisposeAsync()
    {
        if (_algorithm != null)
        {
            _algorithm.IterationCompleted -= HandleIterationCompleted;
            _algorithm.AlgorithmCompleted -= HandleAlgorithmCompleted;
        }
    }
}

/// <summary>
/// Конфигурация алгоритма PSO
/// </summary>
public class PSOConfiguration
{
    public int SwarmSize { get; set; } = 50;
    public int MaxIterations { get; set; } = 500;
    public double InertiaWeight { get; set; } = 0.7;
    public double CognitiveWeight { get; set; } = 1.5;
    public double SocialWeight { get; set; } = 1.5;
    public int NoImprovementLimit { get; set; } = 50;
    public int? RandomSeed { get; set; }

    public static PSOConfiguration Default => new();
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