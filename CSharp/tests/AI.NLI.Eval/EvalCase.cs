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
    /// <summary>Сверка формы с ожиданием. Допущения не считаются: их источник честно помечен.</summary>
    public static EvalScore Of(EvalCase eval, FormSchema schema, FormState state, int questions, bool mattered)
    {
        var stated = state.Values.Where(pair => !pair.Value.IsAssumed).ToDictionary();
        var correct = eval.Expected.Count(pair => stated.TryGetValue(pair.Key, out var value)
            && FieldFormat.Same(schema.Fields.Named(pair.Key)!, value, new FieldValue(pair.Value, ValueSource.User)));
        var wrong = eval.Expected.Count(pair => stated.ContainsKey(pair.Key)) - correct;
        var fabricated = eval.Absent.Count(stated.ContainsKey);
        return new EvalScore(eval.Name, correct, wrong, fabricated, eval.Expected.Count, questions, mattered);
    }
}
