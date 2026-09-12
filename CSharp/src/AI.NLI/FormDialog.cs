using Microsoft.Extensions.AI;

namespace AI.NLI;

/// <summary>
/// Диалог с экспертной системой. Ход не ждет пользователя: вопросы возвращаются вместе с состоянием,
/// а ответ приходит следующим вызовом. Реплику разбирает классификатор: данные, «что если», вопрос о выводе
/// или новый запрос.
/// </summary>
public sealed class FormDialog
{
    private const string WhatIfContext = "Пользователь спрашивает, что будет при других данных: верни значения из его предположения.";

    private readonly IReadOnlyList<ExpertSystem> _systems;
    private readonly DialogOptions _options;
    private readonly FormExtractor _extractor;
    private readonly FormFiller _filler;
    private readonly QuestionWriter _writer;
    private readonly TurnClassifier _classifier;
    private readonly OutputExplainer _explainer;

    /// <param name="chat">Любая модель за стандартным <see cref="IChatClient"/>.</param>
    /// <param name="systems">Экспертные системы; если их несколько, систему под запрос выбирает классификатор.</param>
    /// <param name="sources">Источники недостающих данных в порядке опроса.</param>
    /// <param name="options">Настройки.</param>
    public FormDialog(IChatClient chat, IReadOnlyList<ExpertSystem> systems, IReadOnlyList<FormSource>? sources = null, DialogOptions? options = null)
    {
        if (systems.Count == 0)
            throw new ArgumentException("Нужна хотя бы одна экспертная система", nameof(systems));

        _systems = systems;
        _options = options ?? new DialogOptions();
        _extractor = new FormExtractor(chat, _options.Extractor);
        _filler = new FormFiller(_extractor, sources ?? []);
        _writer = new QuestionWriter(chat);
        _classifier = new TurnClassifier(chat);
        _explainer = new OutputExplainer(chat);
    }

    /// <summary>Ход по реплике пользователя.</summary>
    public async Task<DialogTurn> ReplyAsync(FormState state, string message, CancellationToken ct = default)
    {
        var (intent, chosen) = state.Output is null && (state.System is not null || _systems.Count == 1)
            ? (TurnIntent.Fill, null)
            : await _classifier.ClassifyAsync(_systems, state, message, ct);
        if (intent == TurnIntent.NewRequest)
            state.Reset();
        state.System ??= chosen ?? _systems[0].Schema.Name;

        var system = SystemOf(state);
        return intent switch
        {
            TurnIntent.WhatIf when state.Output is not null => await WhatIfAsync(system, state, message, ct),
            TurnIntent.AboutResult when state.Output is not null => new DialogTurn(TurnKind.Answer,
                await _explainer.ExplainAsync(system.Schema, state.Values, state.Output, message, ct: ct)) { Output = state.Output },
            _ => await FillAsync(system, state, message, ct)
        };
    }

    /// <summary>Ход по нажатию готового ответа: значение применяется без модели.</summary>
    public async Task<DialogTurn> ChooseAsync(FormState state, string field, string choice, CancellationToken ct = default)
    {
        var system = SystemOf(state);
        var target = system.Schema.Fields.Named(field) ?? throw new ArgumentException($"Нет поля «{field}»", nameof(field));
        if (choice == Question.DontKnow)
            state.MarkUnknown(field);
        else
            state.Apply(target, new FieldValue(FieldFormat.Normalize(target, choice), ValueSource.User, choice));

        return await FillAsync(system, state, "", ct);
    }

    private async Task<DialogTurn> FillAsync(ExpertSystem system, FormState state, string message, CancellationToken ct)
    {
        var needs = await _filler.FillAsync(system.Schema, state, message, ct);
        if (needs.Count > 0)
            return await AskAsync(state, needs, ct);

        state.Pending = [];
        var issues = FormValidator.Check(system.Schema, state.Values);
        if (issues.Count > 0)
            return new DialogTurn(TurnKind.Incomplete, string.Join('\n', issues.Select(issue => $"{issue.Field}: {issue.Problem}"))) { Issues = issues };

        var output = await system.RunAsync(system.Schema.Fields.Typed(state.Values), ct);
        var sensitive = (await Sensitivity.FieldsAsync(system, state.Values, output, _options.Tolerance, ct))
            .Where(field => !state.Asked.Contains(field.Name))
            .ToList();
        if (sensitive.Count > 0)
        {
            state.Asked.UnionWith(sensitive.Select(field => field.Name));
            return await AskAsync(state, sensitive.Select(field => Question.For(field, Question.AffectsResult)).ToList(), ct);
        }

        state.Output = new Dictionary<string, object?>(output);
        var text = await _explainer.ExplainAsync(system.Schema, state.Values, output, ct: ct);
        return new DialogTurn(TurnKind.Result, text) { Output = output };
    }

    private async Task<DialogTurn> WhatIfAsync(ExpertSystem system, FormState state, string message, CancellationToken ct)
    {
        var change = await _extractor.ExtractAsync(system.Schema.Fields, new SourceText(message), ValueSource.User, $"{WhatIfContext}\n\n{state.Context()}", ct);
        if (change.Values.Count == 0)
            return new DialogTurn(TurnKind.Answer, "Не понял, какие данные поменять. Назовите поле и новое значение.");

        var values = new Dictionary<string, FieldValue>(state.Values);
        foreach (var (name, value) in change.Values)
            values[name] = value;

        var output = await system.RunAsync(system.Schema.Fields.Typed(values), ct);
        var text = await _explainer.ExplainAsync(system.Schema, values, output, message, state.Output, ct);
        return new DialogTurn(TurnKind.Answer, text) { Output = output };
    }

    private async Task<DialogTurn> AskAsync(FormState state, IReadOnlyList<Question> needs, CancellationToken ct)
    {
        state.Pending = [.. await _writer.PhraseAsync(needs.Take(_options.QuestionLimit).ToList(), ct)];
        return new DialogTurn(TurnKind.Questions, string.Join('\n', state.Pending.Select(question => question.Text))) { Questions = state.Pending };
    }

    private ExpertSystem SystemOf(FormState state) =>
        _systems.FirstOrDefault(system => system.Schema.Name == state.System) ?? _systems[0];
}
