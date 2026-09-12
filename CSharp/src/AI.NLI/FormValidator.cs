namespace AI.NLI;

/// <summary>
/// Критик без модели: обязательность, тип, диапазон, варианты и правила между полями проверяются правилами, а не догадкой.
/// </summary>
public static class FormValidator
{
    /// <summary>Проблемы формы; пустой список означает, что форму можно отдавать экспертной системе.</summary>
    public static IReadOnlyList<FieldIssue> Check(FormSchema schema, IReadOnlyDictionary<string, FieldValue> values)
    {
        var issues = new List<FieldIssue>();
        foreach (var field in schema.Fields.Active(values))
        {
            var problem = values.TryGetValue(field.Name, out var value)
                ? FieldFormat.Parse(field, value.Value).Problem
                : field.Required ? "не заполнено" : null;
            if (problem is not null)
                issues.Add(new FieldIssue(field.Name, problem));
        }

        var typed = schema.Fields.Typed(values);
        foreach (var rule in schema.Rules)
            if (rule.Violation(typed) is { } violation)
                issues.Add(new FieldIssue(rule.Check, violation));

        return issues;
    }
}
