using System.Globalization;

namespace AI.NLI;

/// <summary>
/// Проверка допущений: меняется ли вывод, если заменить догадку или умолчание другим правдоподобным
/// значением. Если меняется, поле надо спросить; если нет, вопрос лишний.
/// </summary>
public static class Sensitivity
{
    private const double Spread = 0.2;

    /// <summary>Поля-допущения, от которых зависит вывод.</summary>
    /// <param name="system">Экспертная система.</param>
    /// <param name="values">Значения формы.</param>
    /// <param name="baseline">Вывод при этих значениях.</param>
    /// <param name="tolerance">Относительное изменение числа, которое еще не считается изменением вывода.</param>
    /// <param name="ct">Отмена.</param>
    public static async Task<IReadOnlyList<FormField>> FieldsAsync(ExpertSystem system, IReadOnlyDictionary<string, FieldValue> values,
        IReadOnlyDictionary<string, object?> baseline, double tolerance, CancellationToken ct = default)
    {
        var sensitive = new List<FormField>();
        foreach (var field in system.Schema.Fields.Active(values).Where(field => values.TryGetValue(field.Name, out var value) && value.IsAssumed))
        {
            foreach (var alternative in Alternatives(field, values[field.Name].Value))
            {
                var changed = new Dictionary<string, FieldValue>(values) { [field.Name] = new FieldValue(alternative, ValueSource.Guess) };
                if (!Differs(baseline, await system.RunAsync(system.Schema.Fields.Typed(changed), ct), tolerance))
                    continue;

                sensitive.Add(field);
                break;
            }
        }

        return sensitive;
    }

    /// <summary>Выводы различаются: у чисел с учетом допуска, у остального по тексту.</summary>
    public static bool Differs(IReadOnlyDictionary<string, object?> a, IReadOnlyDictionary<string, object?> b, double tolerance) =>
        a.Keys.Union(b.Keys).Any(key => !Close(a.GetValueOrDefault(key), b.GetValueOrDefault(key), tolerance));

    private static IEnumerable<string> Alternatives(FormField field, string value)
    {
        IEnumerable<object> options = (field.Type, FieldFormat.Parse(field, value).Value) switch
        {
            (FieldType.Boolean, bool flag) => [!flag],
            (FieldType.Choice, string choice) => field.Choices.Where(item => item != choice),
            (FieldType.Number, double number) => [number * (1 - Spread), number * (1 + Spread)],
            (FieldType.Integer, long integer) => [integer - Step(integer), integer + Step(integer)],
            _ => []
        };
        return options
            .Select(option => option is bool flag ? (flag ? "true" : "false") : Convert.ToString(option, CultureInfo.InvariantCulture)!)
            .Where(option => FieldFormat.Parse(field, option).Problem is null);
    }

    private static long Step(long value) => Math.Max(1, (long)Math.Round(Math.Abs(value) * Spread));

    private static bool Close(object? a, object? b, double tolerance) => (Number(a), Number(b)) switch
    {
        ({ } x, { } y) => Math.Abs(x - y) <= tolerance * Math.Max(Math.Abs(x), Math.Abs(y)),
        _ => string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture))
    };

    private static double? Number(object? value) => value switch
    {
        double number => number,
        long integer => integer,
        int integer => integer,
        decimal number => (double)number,
        _ => null
    };
}
