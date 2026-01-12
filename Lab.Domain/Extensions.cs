using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lab.Domain;

/// <summary>
/// Вспомогательные методы расширения
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Конвертирует объект в JSON строку
    /// </summary>
    public static string ToJson(this object obj, bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(obj, options);
    }

    /// <summary>
    /// Десериализует JSON строку в объект
    /// </summary>
    public static T? FromJson<T>(this string json)
    {
        return JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Нормализует список значений в диапазон [0, 1]
    /// </summary>
    public static List<double> Normalize(this IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
            return list;

        double min = list.Min();
        double max = list.Max();
        double range = max - min;

        if (range == 0)
            return list.Select(_ => 0.5).ToList();

        return list.Select(v => (v - min) / range).ToList();
    }

    /// <summary>
    /// Вычисляет скользящее среднее
    /// </summary>
    public static List<double> MovingAverage(this IEnumerable<double> values, int windowSize)
    {
        var list = values.ToList();
        var result = new List<double>();

        for (int i = 0; i < list.Count; i++)
        {
            int start = Math.Max(0, i - windowSize + 1);
            int count = i - start + 1;
            double sum = 0;

            for (int j = start; j <= i; j++)
            {
                sum += list[j];
            }

            result.Add(sum / count);
        }

        return result;
    }

    /// <summary>
    /// Перемешивает список с использованием алгоритма Фишера-Йетса
    /// </summary>
    public static List<T> Shuffle<T>(this IList<T> list, Random? random = null)
    {
        random ??= new Random();
        var shuffled = new List<T>(list);

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    /// <summary>
    /// Разбивает коллекцию на пакеты для параллельной обработки
    /// </summary>
    public static List<List<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        var batches = new List<List<T>>();
        var batch = new List<T>();

        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count >= batchSize)
            {
                batches.Add(batch);
                batch = new List<T>();
            }
        }

        if (batch.Count > 0)
        {
            batches.Add(batch);
        }

        return batches;
    }

    /// <summary>
    /// Вычисляет перцентиль
    /// </summary>
    public static double Percentile(this IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
            return 0;

        int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(sorted.Count - 1, index));
        return sorted[index];
    }
}