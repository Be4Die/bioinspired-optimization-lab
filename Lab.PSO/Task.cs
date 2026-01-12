using System.Text.Json.Serialization;

namespace Lab.PSO;

/// <summary>
/// Представляет вычислительную задачу с требованиями к ресурсам и зависимостями
/// </summary>
public class Task
{
    /// <summary>
    /// Уникальный идентификатор задачи
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Объем вычислений (в условных единицах)
    /// </summary>
    public double ComputationVolume { get; init; }

    /// <summary>
    /// Требуемый объем памяти
    /// </summary>
    public double MemoryRequirement { get; init; }

    /// <summary>
    /// Идентификаторы непосредственных предшественников
    /// </summary>
    public List<int> PredecessorIds { get; init; } = new();

    /// <summary>
    /// Момент начала выполнения (рассчитывается при планировании)
    /// </summary>
    [JsonIgnore]
    public double StartTime { get; set; }

    /// <summary>
    /// Момент завершения (рассчитывается при планировании)
    /// </summary>
    [JsonIgnore]
    public double CompletionTime { get; set; }

    /// <summary>
    /// Идентификатор назначенной виртуальной машины
    /// </summary>
    [JsonIgnore]
    public int? AssignedMachineId { get; set; }

    /// <summary>
    /// Состояние готовности задачи (все предшественники завершены)
    /// </summary>
    [JsonIgnore]
    public bool IsReady { get; set; }

    /// <summary>
    /// Проверяет, готовы ли все предшественники задачи
    /// </summary>
    /// <param name="tasks">Словарь всех задач</param>
    /// <returns>True, если все предшественники завершены</returns>
    public bool CheckPredecessorsReady(Dictionary<int, Task> tasks)
    {
        if (PredecessorIds.Count == 0)
            return true;
                
        foreach (var predId in PredecessorIds)
        {
            if (!tasks.TryGetValue(predId, out var predecessor) || predecessor.CompletionTime <= 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Получает максимальное время завершения среди предшественников
    /// </summary>
    /// <param name="tasks">Словарь всех задач</param>
    /// <returns>Максимальное время завершения предшественников</returns>
    public double GetMaxPredecessorCompletionTime(Dictionary<int, Task> tasks)
    {
        if (PredecessorIds.Count == 0)
            return 0;
                
        double maxTime = 0;
        foreach (var predId in PredecessorIds)
        {
            if (tasks.TryGetValue(predId, out var predecessor))
            {
                maxTime = Math.Max(maxTime, predecessor.CompletionTime);
            }
        }
        return maxTime;
    }
}