namespace AI.NLI;

/// <summary>
/// Один ход заполнения формы: реплика пользователя, затем источники, затем вопросы. Если спрашивать
/// нечего, пустые поля закрываются умолчанием или догадкой по политике поля.
/// </summary>
/// <param name="extractor">Извлечение значений из текста.</param>
/// <param name="sources">Источники в порядке опроса: каждое поле ищется в них один раз за диалог.</param>
public sealed class FormFiller(FormExtractor extractor, IReadOnlyList<FormSource> sources)
{
    /// <summary>Обновляет состояние по реплике; возвращает вопросы, которые надо задать пользователю.</summary>
    public async Task<IReadOnlyList<Question>> FillAsync(FormSchema schema, FormState state, string message, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Apply(schema, state, await extractor.ExtractAsync(schema.Fields, new SourceText(message), ValueSource.User, state.Context(), ct));

        await SearchAsync(schema, state, ct);

        var questions = state.Conflicts
            .Select(conflict => Question.ForConflict(schema.Fields.Named(conflict.Field)!, conflict))
            .Concat(schema.Fields.Missing(state.Values)
                .Where(field => field.Missing == MissingPolicy.Ask && !state.Unknown.Contains(field.Name))
                .Select(field => Question.For(field, Question.NoData)))
            .ToList();
        if (questions.Count == 0)
            await AssumeAsync(schema, state, message, ct);

        return questions;
    }

    // Источники читают по всем полям, а не только по пустым: так видно, где документ расходится со словами пользователя
    private async Task SearchAsync(FormSchema schema, FormState state, CancellationToken ct)
    {
        var toSearch = schema.Fields.Missing(state.Values).Where(field => !state.Searched.Contains(field.Name)).ToList();
        foreach (var source in sources)
        {
            var missing = toSearch.Where(field => !state.Values.ContainsKey(field.Name)).ToList();
            if (missing.Count == 0)
                break;

            foreach (var text in await source.ReadAsync(missing, ct))
                Apply(schema, state, await extractor.ExtractAsync(schema.Fields, text, source.Kind, "", ct));
        }

        state.Searched.UnionWith(toSearch.Select(field => field.Name));
    }

    private async Task AssumeAsync(FormSchema schema, FormState state, string message, CancellationToken ct)
    {
        foreach (var field in schema.Fields.Missing(state.Values).Where(field => field.Default is not null).ToList())
            state.Apply(field, new FieldValue(FieldFormat.Normalize(field, field.Default!), ValueSource.Default));

        var toGuess = schema.Fields.Missing(state.Values).Where(field => field.Missing == MissingPolicy.Guess).ToList();
        if (toGuess.Count == 0)
            return;

        foreach (var (name, value) in await extractor.GuessAsync(toGuess, $"{message}\n\n{state.Context()}", ct))
            state.Apply(schema.Fields.Named(name)!, value);
    }

    private static void Apply(FormSchema schema, FormState state, Extraction found)
    {
        foreach (var (name, value) in found.Values)
            state.Apply(schema.Fields.Named(name)!, value);
        foreach (var name in found.Unknown)
            state.MarkUnknown(name);
    }
}
