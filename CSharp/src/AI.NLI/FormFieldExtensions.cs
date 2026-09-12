namespace AI.NLI;

/// <summary>Операции над набором полей формы.</summary>
public static class FormFieldExtensions
{
    /// <summary>Поле по имени; <c>null</c>, если такого нет.</summary>
    public static FormField? Named(this IEnumerable<FormField> fields, string name) =>
        fields.FirstOrDefault(field => field.Name == name);

    /// <summary>Поля, которые нужны при текущих значениях: условие <see cref="FormField.When"/> выполнено.</summary>
    public static IEnumerable<FormField> Active(this IEnumerable<FormField> fields, IReadOnlyDictionary<string, FieldValue> values) =>
        fields.Where(field => field.When is not { } when
            || values.TryGetValue(when.Field, out var value)
            && when.Is.Any(item => string.Equals(item, value.Value, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Нужные поля без значения.</summary>
    public static IEnumerable<FormField> Missing(this IEnumerable<FormField> fields, IReadOnlyDictionary<string, FieldValue> values) =>
        fields.Active(values).Where(field => !values.ContainsKey(field.Name));

    /// <summary>
    /// Вход экспертной системы: типизированные значения нужных полей (<see cref="FieldFormat.Parse"/>),
    /// пустое поле дает <c>null</c>.
    /// </summary>
    public static Dictionary<string, object?> Typed(this IEnumerable<FormField> fields, IReadOnlyDictionary<string, FieldValue> values) =>
        fields.Active(values).ToDictionary(
            field => field.Name,
            field => values.TryGetValue(field.Name, out var value) ? FieldFormat.Parse(field, value.Value).Value : null);
}
