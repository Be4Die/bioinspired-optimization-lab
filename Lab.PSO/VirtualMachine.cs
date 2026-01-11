using System.Text.Json.Serialization;

namespace Lab.PSO;

/// <summary>
/// Представляет виртуальную машину с характеристиками производительности
/// </summary>
public class VirtualMachine
{
    /// <summary>
    /// Уникальный идентификатор виртуальной машины
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Производительность (единиц вычислений в единицу времени)
    /// </summary>
    public double Performance { get; set; }

    /// <summary>
    /// Доступный объем памяти
    /// </summary>
    public double AvailableMemory { get; set; }

    /// <summary>
    /// Список задач, назначенных на эту машину
    /// </summary>
    [JsonIgnore]
    public List<Task> AssignedTasks { get; set; } = new();

    /// <summary>
    /// Время завершения последней задачи на машине
    /// </summary>
    [JsonIgnore]
    public double LastCompletionTime { get; set; }

    /// <summary>
    /// Проверяет, достаточно ли памяти для задачи
    /// </summary>
    /// <param name="taskMemory">Требуемый объем памяти задачи</param>
    /// <returns>True, если памяти достаточно</returns>
    public bool HasSufficientMemory(double taskMemory)
    {
        return AvailableMemory >= taskMemory;
    }

    /// <summary>
    /// Рассчитывает время выполнения задачи на этой машине
    /// </summary>
    /// <param name="computationVolume">Объем вычислений задачи</param>
    /// <returns>Время выполнения</returns>
    public double CalculateExecutionTime(double computationVolume)
    {
        if (Performance <= 0)
            return double.MaxValue;

        return computationVolume / Performance;
    }

    /// <summary>
    /// Сбрасывает состояние машины для нового планирования
    /// </summary>
    public void Reset()
    {
        AssignedTasks.Clear();
        LastCompletionTime = 0;
    }
}