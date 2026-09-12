using Microsoft.Extensions.AI;

namespace AI.NLI;

/// <summary>
/// Формулирует вопросы человеческим языком. Состав вопросов и готовые ответы решены до модели,
/// она меняет только текст; если не ответила, остается описание поля.
/// </summary>
/// <param name="chat">Любая модель за стандартным <see cref="IChatClient"/>.</param>
public sealed class QuestionWriter(IChatClient chat)
{
    private const string Instruction =
        "Переформулируй вопросы к пользователю: коротко, вежливо, простым языком, по одному вопросу на поле. " +
        "Не добавляй новых вопросов и не меняй смысл. Если причина вопроса «противоречие», назови оба значения. " +
        "Если причина «от этого зависит вывод», скажи, что сейчас взято допущение. Верни field и text для каждого.";

    /// <summary>Те же вопросы с человеческими формулировками.</summary>
    public async Task<IReadOnlyList<Question>> PhraseAsync(IReadOnlyList<Question> questions, CancellationToken ct = default)
    {
        if (questions.Count == 0)
            return questions;

        var input = string.Join('\n', questions.Select(question => $"{question.Field}: {question.Text} (причина: {question.Reason})"));
        var reply = await ModelCall.AskAsync<Reply>(chat, ModelCall.Messages(Instruction, input), ct);
        var texts = (reply?.Items ?? [])
            .Where(item => item.Field is not null && !string.IsNullOrWhiteSpace(item.Text))
            .GroupBy(item => item.Field)
            .ToDictionary(group => group.Key, group => group.First().Text);

        return questions.Select(question => texts.TryGetValue(question.Field, out var text) ? question with { Text = text } : question).ToList();
    }

    private sealed record Item(string Field, string Text);

    private sealed record Reply(Item[] Items);
}
