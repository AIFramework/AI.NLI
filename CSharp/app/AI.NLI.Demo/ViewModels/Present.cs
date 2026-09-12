using System.Globalization;
using Avalonia.Media;

namespace AI.NLI.Demo.ViewModels;

/// <summary>Как поля, значения и вопросы выглядят для человека.</summary>
internal static class Present
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly Dictionary<ValueSource, (string Back, string Fore)> SourceColors = new()
    {
        [ValueSource.User] = ("#E7F8EF", "#18794E"),
        [ValueSource.Document] = ("#E6F0FF", "#1D4ED8"),
        [ValueSource.Web] = ("#E0F2FE", "#0369A1"),
        [ValueSource.Default] = ("#F1F2F6", "#4B5563"),
        [ValueSource.Guess] = ("#FFF4DB", "#9A5B00")
    };

    public static string Title(FormField field) => string.IsNullOrWhiteSpace(field.Description) ? field.Name : field.Description;

    public static string Value(FormField field, FieldValue value)
    {
        var (typed, problem) = FieldFormat.Parse(field, value.Value);
        var text = problem is not null ? value.Value : typed switch
        {
            bool flag => flag ? "да" : "нет",
            double or long => Number(typed) + (field.Unit is null ? "" : " " + field.Unit),
            List<Dictionary<string, object?>> rows => $"{rows.Count} строк",
            _ => value.Value
        };
        return value.Approximate && !value.IsAssumed ? "≈ " + text : text;
    }

    public static string Number(object? value) => value switch
    {
        double number => number.ToString("#,##0.##", Russian),
        long integer => integer.ToString("#,##0", Russian),
        null => "нет",
        _ => value.ToString() ?? ""
    };

    public static string Choice(string choice) =>
        choice == Question.DontKnow ? "Не знаю" : char.ToUpperInvariant(choice[0]) + choice[1..];

    public static string Reason(string reason) => reason switch
    {
        Question.Contradiction => "источники расходятся",
        Question.AffectsResult => "влияет на оценку",
        Question.Vague => "нужно точнее",
        _ => ""
    };

    /// <summary>Строка формы: значение с источником, цитатой и оговоркой, либо состояние пустого поля.</summary>
    public static FieldRow Row(FormField field, FormState state)
    {
        var title = Title(field);
        if (state.Conflicts.FirstOrDefault(conflict => conflict.Field == field.Name) is { } conflict)
            return new FieldRow(title, Value(field, conflict.Current), Status("расхождение", true), null,
                $"{conflict.Offered.SourceLabel} дает {Value(field, conflict.Offered)}");

        if (state.Values.TryGetValue(field.Name, out var value))
            return new FieldRow(title, Value(field, value), Source(value),
                value.IsAssumed || value.Evidence is null ? null : $"«{value.Evidence}»{(value.Reference is null ? "" : $" · {value.Reference}")}",
                value.Source == ValueSource.Guess ? value.Evidence : null);

        if (state.Vague.TryGetValue(field.Name, out var bound))
            return new FieldRow(title, "—", Status("нужно точнее", true), $"«{bound.Evidence}»", null);

        return new FieldRow(title, "—", Empty(field, state.Unknown.Contains(field.Name)), null, null);
    }

    // Красным только то, о чем спросят: у поля с умолчанием или догадкой значение появится само
    private static Badge Empty(FormField field, bool unknown) => (unknown, field) switch
    {
        (true, _) => Status("не знаю", false),
        (_, { Default: { } fallback }) => Status($"по умолчанию: {fallback}", false),
        (_, { Missing: MissingPolicy.Guess }) => Status("предположу", false),
        (_, { Required: false }) => Status("необязательно", false),
        _ => Status("не заполнено", true)
    };

    private static Badge Source(FieldValue value)
    {
        var (back, fore) = SourceColors[value.Source];
        return new Badge(value.SourceLabel + (value.Approximate && !value.IsAssumed ? " · примерно" : ""), Brush.Parse(back), Brush.Parse(fore));
    }

    private static Badge Status(string text, bool alarm) =>
        new(text, Brush.Parse(alarm ? "#FDECEC" : "#F1F2F6"), Brush.Parse(alarm ? "#B42318" : "#6B7280"));
}
