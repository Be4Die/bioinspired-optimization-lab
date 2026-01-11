using System.Collections.Generic;
using System.Linq;

namespace Lab.PSO;

/// <summary>
/// Данные для визуализации работы алгоритма
/// </summary>
public class VisualizationData
{
    /// <summary>
    /// Данные для графика сходимости
    /// </summary>
    public ConvergenceChartData ConvergenceChart { get; set; } = new();

    /// <summary>
    /// Данные для графика распределения задач по машинам
    /// </summary>
    public DistributionChartData DistributionChart { get; set; } = new();

    /// <summary>
    /// Данные для графика Ганта (расписание)
    /// </summary>
    public GanttChartData GanttChart { get; set; } = new();

    /// <summary>
    /// Данные для тепловой карты утилизации ресурсов
    /// </summary>
    public HeatmapData ResourceHeatmap { get; set; } = new();

    /// <summary>
    /// Сводная информация о решении
    /// </summary>
    public SolutionSummary Summary { get; set; } = new();

    /// <summary>
    /// Создает данные визуализации из решения
    /// </summary>
    public static VisualizationData CreateFromSolution(Solution solution, ProblemInstance instance)
    {
        var data = new VisualizationData();

        if (solution == null)
            return data;

        // График сходимости
        data.ConvergenceChart = CreateConvergenceChartData(solution);

        // График распределения
        data.DistributionChart = CreateDistributionChartData(solution, instance);

        // Диаграмма Ганта
        data.GanttChart = CreateGanttChartData(solution);

        // Тепловая карта
        data.ResourceHeatmap = CreateHeatmapData(solution, instance);

        // Сводка
        data.Summary = CreateSolutionSummary(solution, instance);

        return data;
    }

    private static ConvergenceChartData CreateConvergenceChartData(Solution solution)
    {
        var data = new ConvergenceChartData();

        if (solution.FitnessHistory != null && solution.FitnessHistory.Count > 0)
        {
            for (int i = 0; i < solution.FitnessHistory.Count; i++)
            {
                data.Iterations.Add(i + 1);
                data.BestFitness.Add(solution.FitnessHistory[i]);
            }
        }

        return data;
    }

    private static DistributionChartData CreateDistributionChartData(Solution solution, ProblemInstance instance)
    {
        var data = new DistributionChartData();

        if (solution.Assignment != null)
        {
            // Группируем задачи по машинам
            var groups = solution.Assignment
                .GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var machine in instance.VirtualMachines.Values)
            {
                data.MachineIds.Add(machine.Id);
                data.TaskCounts.Add(groups.GetValueOrDefault(machine.Id, 0));
            }
        }

        return data;
    }

    private static GanttChartData CreateGanttChartData(Solution solution)
    {
        var data = new GanttChartData();

        if (solution.ScheduledTasks != null)
        {
            foreach (var task in solution.ScheduledTasks.Values.OrderBy(t => t.StartTime))
            {
                data.TaskIds.Add(task.Id);
                data.StartTimes.Add(task.StartTime);
                data.Durations.Add(task.CompletionTime - task.StartTime);
                data.MachineIds.Add(task.AssignedMachineId ?? 0);
            }
        }

        return data;
    }

    private static HeatmapData CreateHeatmapData(Solution solution, ProblemInstance instance)
    {
        var data = new HeatmapData();

        if (solution.ScheduledMachines != null)
        {
            foreach (var machine in instance.VirtualMachines.Values)
            {
                data.MachineIds.Add(machine.Id);

                if (solution.ScheduledMachines.ContainsKey(machine.Id))
                {
                    var scheduledMachine = solution.ScheduledMachines[machine.Id];
                    double utilization = scheduledMachine.LastCompletionTime / solution.Makespan;
                    data.UtilizationValues.Add(utilization);
                }
                else
                {
                    data.UtilizationValues.Add(0);
                }
            }
        }

        return data;
    }

    private static SolutionSummary CreateSolutionSummary(Solution solution, ProblemInstance instance)
    {
        var analysis = ResultAnalyzer.Analyze(solution, instance);

        return new SolutionSummary
        {
            Makespan = solution.Makespan,
            TotalPenalty = solution.TotalPenalty,
            Fitness = solution.Fitness,
            IsFeasible = analysis.ConstraintAnalysis.IsFeasible,
            MachineCount = instance.VirtualMachines.Count,
            TaskCount = instance.Tasks.Count,
            ComputationTime = solution.ComputationTime,
            IterationFound = solution.IterationFound,
            QualityScore = analysis.QualityScore,
            AverageMachineUtilization = analysis.MachineUtilization.Values.Average(),
            MemoryViolations = analysis.ConstraintAnalysis.MemoryViolations.Count,
            PrecedenceViolations = analysis.ConstraintAnalysis.PrecedenceViolations.Count
        };
    }
}

/// <summary>
/// Данные для графика сходимости
/// </summary>
public class ConvergenceChartData
{
    public List<int> Iterations { get; set; } = new();
    public List<double> BestFitness { get; set; } = new();
}

/// <summary>
/// Данные для графика распределения
/// </summary>
public class DistributionChartData
{
    public List<int> MachineIds { get; set; } = new();
    public List<int> TaskCounts { get; set; } = new();
}

/// <summary>
/// Данные для диаграммы Ганта
/// </summary>
public class GanttChartData
{
    public List<int> TaskIds { get; set; } = new();
    public List<double> StartTimes { get; set; } = new();
    public List<double> Durations { get; set; } = new();
    public List<int> MachineIds { get; set; } = new();
}

/// <summary>
/// Данные для тепловой карты
/// </summary>
public class HeatmapData
{
    public List<int> MachineIds { get; set; } = new();
    public List<double> UtilizationValues { get; set; } = new();
}

/// <summary>
/// Сводка по решению
/// </summary>
public class SolutionSummary
{
    public double Makespan { get; set; }
    public double TotalPenalty { get; set; }
    public double Fitness { get; set; }
    public bool IsFeasible { get; set; }
    public int MachineCount { get; set; }
    public int TaskCount { get; set; }
    public System.TimeSpan ComputationTime { get; set; }
    public int IterationFound { get; set; }
    public double QualityScore { get; set; }
    public double AverageMachineUtilization { get; set; }
    public int MemoryViolations { get; set; }
    public int PrecedenceViolations { get; set; }

    public override string ToString()
    {
        return $"Makespan: {Makespan:F2}, Tasks: {TaskCount}, Machines: {MachineCount}, " +
               $"Feasible: {IsFeasible}, Quality: {QualityScore:F2}%";
    }
}