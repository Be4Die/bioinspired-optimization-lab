namespace Lab.Domain;

/// <summary>
/// Анализатор результатов работы алгоритма
/// </summary>
public class ResultAnalyzer
{
    /// <summary>
    /// Анализирует качество решения
    /// </summary>
    public static AnalysisResult Analyze(Solution? solution, ProblemInstance instance)
    {
        var result = new AnalysisResult();

        if (solution?.ScheduledTasks == null)
            return result;

        // Основные метрики
        result.Makespan = solution.Makespan;
        result.TotalPenalty = solution.TotalPenalty;
        result.Fitness = solution.Fitness;

        // Анализ использования машин
        result.MachineUtilization = CalculateMachineUtilization(solution, instance);

        // Анализ загрузки памяти
        result.MemoryUtilization = CalculateMemoryUtilization(solution, instance);

        // Анализ выполнения ограничений
        result.ConstraintAnalysis = AnalyzeConstraints(solution, instance);

        // Статистика по времени выполнения задач
        result.TaskStatistics = CalculateTaskStatistics(solution);

        // Оценка качества
        result.QualityScore = CalculateQualityScore(result);

        return result;
    }

    private static Dictionary<int, double> CalculateMachineUtilization(Solution solution, ProblemInstance instance)
    {
        var utilization = new Dictionary<int, double>();

        foreach (var machine in instance.VirtualMachines.Values)
        {
            double totalWorkTime = 0;

            if (solution.ScheduledMachines.TryGetValue(machine.Id, out var scheduledMachine))
            {
                totalWorkTime = scheduledMachine.LastCompletionTime;
            }

            // Время простоя = makespan - время работы
            double utilizationRate = (solution.Makespan > 0) ? totalWorkTime / solution.Makespan : 0;

            utilization[machine.Id] = utilizationRate;
        }

        return utilization;
    }

    private static Dictionary<int, double> CalculateMemoryUtilization(Solution solution, ProblemInstance instance)
    {
        var utilization = new Dictionary<int, double>();

        foreach (var machine in instance.VirtualMachines.Values)
        {
            double totalMemoryUsed = 0;

            if (solution.ScheduledMachines.TryGetValue(machine.Id, out var scheduledMachine))
            {
                totalMemoryUsed = scheduledMachine.AssignedTasks.Sum(t => t.MemoryRequirement);
            }

            double utilizationRate = (machine.AvailableMemory > 0) ? totalMemoryUsed / machine.AvailableMemory : 0;

            utilization[machine.Id] = utilizationRate;
        }

        return utilization;
    }

    private static ConstraintAnalysis AnalyzeConstraints(Solution solution, ProblemInstance instance)
    {
        var analysis = new ConstraintAnalysis();

        // Проверка ограничений по памяти
        foreach (var task in instance.Tasks.Values)
        {
            if (solution.Assignment.TryGetValue(task.Id, out int machineId))
            {
                var machine = instance.VirtualMachines[machineId];

                if (!machine.HasSufficientMemory(task.MemoryRequirement))
                {
                    analysis.MemoryViolations.Add(task.Id);
                }
            }
        }

        // Проверка предшествования (уже гарантируется планировщиком)
        analysis.PrecedenceViolations = CheckPrecedenceViolations(solution);

        analysis.IsFeasible = analysis.MemoryViolations.Count == 0 &&
                              analysis.PrecedenceViolations.Count == 0;

        return analysis;
    }

    private static List<int> CheckPrecedenceViolations(Solution solution)
    {
        var violations = new List<int>();

        foreach (var task in solution.ScheduledTasks.Values)
        {
            foreach (var predId in task.PredecessorIds)
            {
                if (solution.ScheduledTasks.TryGetValue(predId, out var predecessor))
                {
                    if (predecessor.CompletionTime > task.StartTime)
                    {
                        violations.Add(task.Id);
                        break;
                    }
                }
            }
        }

        return violations;
    }

    private static TaskStatistics CalculateTaskStatistics(Solution solution)
    {
        var stats = new TaskStatistics();

        if (solution.ScheduledTasks.Count == 0)
            return stats;

        var completionTimes = solution.ScheduledTasks.Values
            .Select(t => t.CompletionTime)
            .ToList();

        stats.AverageCompletionTime = completionTimes.Average();
        stats.MaxCompletionTime = completionTimes.Max();
        stats.MinCompletionTime = completionTimes.Min();
        stats.CompletionTimeStdDev = CalculateStdDev(completionTimes);

        return stats;
    }

    private static double CalculateStdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
            return 0;

        double avg = list.Average();
        double sum = list.Sum(d => Math.Pow(d - avg, 2));
        return Math.Sqrt(sum / list.Count);
    }

    private static double CalculateQualityScore(AnalysisResult result)
    {
        double score = 100;

        // Штраф за нарушение ограничений
        if (!result.ConstraintAnalysis.IsFeasible)
        {
            score -= 50;
        }

        // Штраф за высокий makespan
        if (result.Makespan > result.TaskStatistics.AverageCompletionTime * 2)
        {
            score -= 20;
        }

        // Бонус за хорошую утилизацию машин
        double avgUtilization = result.MachineUtilization.Values.Average();
        score += avgUtilization * 10;

        return Math.Max(0, score);
    }
}

/// <summary>
/// Результат анализа решения
/// </summary>
public class AnalysisResult
{
    public double Makespan { get; set; }
    public double TotalPenalty { get; set; }
    public double Fitness { get; set; }
    public Dictionary<int, double> MachineUtilization { get; set; } = new();
    public Dictionary<int, double> MemoryUtilization { get; set; } = new();
    public ConstraintAnalysis ConstraintAnalysis { get; set; } = new();
    public TaskStatistics TaskStatistics { get; set; } = new();
    public double QualityScore { get; set; }

    public override string ToString()
    {
        return $"Makespan: {Makespan:F2}, Penalty: {TotalPenalty:F2}, Fitness: {Fitness:F2}, " +
               $"Quality: {QualityScore:F2}, Feasible: {ConstraintAnalysis.IsFeasible}";
    }
}

/// <summary>
/// Анализ выполнения ограничений
/// </summary>
public class ConstraintAnalysis
{
    public List<int> MemoryViolations { get; set; } = new();
    public List<int> PrecedenceViolations { get; set; } = new();
    public bool IsFeasible { get; set; }

    public int TotalViolations => MemoryViolations.Count + PrecedenceViolations.Count;
}

/// <summary>
/// Статистика по задачам
/// </summary>
public class TaskStatistics
{
    public double AverageCompletionTime { get; set; }
    public double MaxCompletionTime { get; set; }
    public double MinCompletionTime { get; set; }
    public double CompletionTimeStdDev { get; set; }
}