namespace AI.NLI.Eval;

/// <summary>Диалог из набора и что должно получиться.</summary>
internal sealed record EvalCase
{
    public string Name { get; init; } = "";

    public string[] Turns { get; init; } = [];

    public Dictionary<string, string> Expected { get; init; } = [];

    public string[] Absent { get; init; } = [];
}

/// <summary>Итог одного диалога.</summary>
internal sealed record EvalScore(string Case, int Correct, int Wrong, int Fabricated, int Expected, int Questions, bool AssumptionMattered)
{
    /// <summary>Что именно не сошлось: поле, ожидание и что оказалось в форме.</summary>
    public IReadOnlyList<string> Misses { get; init; } = [];

    /// <summary>Сверка формы с ожиданием. Допущения не считаются: их источник честно помечен.</summary>
    public static EvalScore Of(EvalCase eval, FormSchema schema, FormState state, int questions, bool mattered)
    {
        var stated = state.Values.Where(pair => !pair.Value.IsAssumed).ToDictionary();
        var misses = eval.Expected
            .Where(pair => !(stated.TryGetValue(pair.Key, out var value)
                && FieldFormat.Same(schema.Fields.Named(pair.Key)!, value, new FieldValue(pair.Value, ValueSource.User))))
            .Select(pair => $"{pair.Key}: ждали «{pair.Value}», в форме {Shown(state, pair.Key)}")
            .Concat(eval.Absent.Where(stated.ContainsKey).Select(name => $"{name}: выдумано {Shown(state, name)}"))
            .ToList();
        var correct = eval.Expected.Count - misses.Count(miss => !miss.Contains("выдумано"));
        var wrong = eval.Expected.Count(pair => stated.ContainsKey(pair.Key)) - correct;
        var fabricated = eval.Absent.Count(stated.ContainsKey);
        return new EvalScore(eval.Name, correct, wrong, fabricated, eval.Expected.Count, questions, mattered) { Misses = misses };
    }

    private static string Shown(FormState state, string name) =>
        state.Values.TryGetValue(name, out var value) ? $"«{value.Value}» ({value.SourceLabel})" : state.Unknown.Contains(name) ? "«не знаю»" : "пусто";
}
