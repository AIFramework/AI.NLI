using System.Globalization;

namespace AI.NLI;

/// <summary>Правило между полями вида «поле оператор поле-или-число», например <c>floor &lt;= floors</c>.</summary>
public sealed record FormRule
{
    private static readonly string[] Operators = ["<", "<=", ">", ">=", "=", "!="];

    /// <summary>Проверка: левый операнд, оператор и правый операнд через пробел.</summary>
    public required string Check { get; init; }

    /// <summary>Что сказать пользователю при нарушении.</summary>
    public string Message { get; init; } = "";

    /// <summary>Правило разобрано.</summary>
    public bool IsWellFormed => Parts() is [_, var op, _] && Operators.Contains(op);

    /// <summary>
    /// Текст нарушения; <c>null</c>, если правило выполнено или его нечем проверить (поле пустое или не число и не дата).
    /// </summary>
    public string? Violation(IReadOnlyDictionary<string, object?> values)
    {
        if (Parts() is not [var left, var op, var right] || Operand(left, values) is not { } x || Operand(right, values) is not { } y)
            return null;

        var holds = op switch
        {
            "<" => x < y,
            "<=" => x <= y,
            ">" => x > y,
            ">=" => x >= y,
            "=" => x == y,
            "!=" => x != y,
            _ => true
        };
        return holds ? null : Message.Length > 0 ? Message : $"нарушено правило {Check}";
    }

    private string[] Parts() => Check.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static double? Operand(string token, IReadOnlyDictionary<string, object?> values) =>
        values.TryGetValue(token, out var value)
            ? value switch { double number => number, long integer => integer, DateOnly date => date.DayNumber, _ => null }
            : double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var constant) ? constant : null;
}
