using System.Globalization;

namespace AI.NLI;

/// <summary>Итог проверки неточных значений.</summary>
/// <param name="Sensitive">Поля, от которых вывод зависит: их надо спросить или оговорить.</param>
/// <param name="Robust">Поля, от которых вывод почти не зависит: вопрос о них лишний.</param>
public sealed record AssumptionCheck(IReadOnlyList<FormField> Sensitive, IReadOnlyList<FormField> Robust);

/// <summary>
/// Проверка неточных значений: меняется ли вывод, если заменить догадку, умолчание или приблизительное
/// значение другим правдоподобным. Если меняется, поле надо спросить; если нет, вопрос лишний.
/// </summary>
public static class Sensitivity
{
    private const double Spread = 0.2;

    /// <summary>Какие неточные поля влияют на вывод, а какие нет.</summary>
    /// <param name="system">Экспертная система.</param>
    /// <param name="values">Значения формы.</param>
    /// <param name="baseline">Вывод при этих значениях.</param>
    /// <param name="tolerance">Относительное изменение числа, которое еще не считается изменением вывода.</param>
    /// <param name="ct">Отмена.</param>
    public static async Task<AssumptionCheck> CheckAsync(ExpertSystem system, IReadOnlyDictionary<string, FieldValue> values,
        IReadOnlyDictionary<string, object?> baseline, double tolerance, CancellationToken ct = default)
    {
        var (sensitive, robust) = (new List<FormField>(), new List<FormField>());
        foreach (var field in system.Schema.Fields.Active(values))
        {
            if (!values.TryGetValue(field.Name, out var value) || !(value.IsAssumed || value.Approximate))
                continue;

            var alternatives = Alternatives(field, value.Value, value.IsAssumed ? Spread : FieldFormat.ApproximateShare).ToList();
            if (alternatives.Count == 0)
                continue;

            var matters = false;
            foreach (var alternative in alternatives)
            {
                var changed = new Dictionary<string, FieldValue>(values) { [field.Name] = new FieldValue(alternative, ValueSource.Guess) };
                if (matters = Differs(baseline, await system.RunAsync(system.Schema.Fields.Typed(changed), ct), tolerance))
                    break;
            }

            (matters ? sensitive : robust).Add(field);
        }

        return new AssumptionCheck(sensitive, robust);
    }

    /// <summary>Выводы различаются: у чисел с учетом допуска, у остального по тексту.</summary>
    public static bool Differs(IReadOnlyDictionary<string, object?> a, IReadOnlyDictionary<string, object?> b, double tolerance) =>
        a.Keys.Union(b.Keys).Any(key => !Close(a.GetValueOrDefault(key), b.GetValueOrDefault(key), tolerance));

    private static IEnumerable<string> Alternatives(FormField field, string value, double spread)
    {
        IEnumerable<object> options = (field.Type, FieldFormat.Parse(field, value).Value) switch
        {
            (FieldType.Boolean, bool flag) => [!flag],
            (FieldType.Choice, string choice) => field.Choices.Where(item => item != choice),
            (FieldType.Number, double number) => [number * (1 - spread), number * (1 + spread)],
            (FieldType.Integer, long integer) => [integer - Step(integer, spread), integer + Step(integer, spread)],
            _ => []
        };
        return options
            .Select(option => option is bool flag ? (flag ? "true" : "false") : Convert.ToString(option, CultureInfo.InvariantCulture)!)
            .Where(option => FieldFormat.Parse(field, option).Problem is null);
    }

    private static long Step(long value, double spread) => Math.Max(1, (long)Math.Round(Math.Abs(value) * spread));

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
