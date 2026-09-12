using Microsoft.Extensions.AI;

namespace AI.NLI;

/// <summary>Что делает реплика пользователя.</summary>
public enum TurnIntent
{
    /// <summary>Сообщает или исправляет данные, отвечает на вопрос.</summary>
    Fill,
    /// <summary>Спрашивает, что будет при других данных, не меняя своих.</summary>
    WhatIf,
    /// <summary>Спрашивает о полученном выводе.</summary>
    AboutResult,
    /// <summary>Начинает новый, не связанный запрос.</summary>
    NewRequest
}

/// <summary>Классификатор реплики на модели: намерение и подходящая экспертная система.</summary>
/// <param name="chat">Любая модель за стандартным <see cref="IChatClient"/>.</param>
public sealed class TurnClassifier(IChatClient chat)
{
    private const string Instruction =
        "Определи, что делает реплика пользователя в диалоге с экспертной системой. intent: " +
        "fill (сообщает или исправляет свои данные, отвечает на вопрос), " +
        "what_if (спрашивает, что будет при других данных, не меняя своих), " +
        "question (спрашивает о полученном выводе), " +
        "new (начинает новый запрос, не связанный с текущим). " +
        "system: имя экспертной системы из списка, которая подходит к запросу.";

    /// <summary>Намерение и имя системы; имя <c>null</c>, если модель не выбрала ни одну из списка.</summary>
    public async Task<(TurnIntent Intent, string? System)> ClassifyAsync(
        IReadOnlyList<ExpertSystem> systems, FormState state, string message, CancellationToken ct = default)
    {
        var input =
            $"Экспертные системы:\n{string.Join('\n', systems.Select(system => $"{system.Schema.Name}: {system.Schema.Description}"))}\n\n" +
            $"Текущая система: {state.System ?? "не выбрана"}\nВывод уже получен: {(state.Output is null ? "нет" : "да")}\n\n" +
            ModelCall.Quote("Реплика", message);
        var reply = await ModelCall.AskAsync<Reply>(chat, ModelCall.Messages(Instruction, input), ct);

        var intent = reply?.Intent switch
        {
            "what_if" => TurnIntent.WhatIf,
            "question" => TurnIntent.AboutResult,
            "new" => TurnIntent.NewRequest,
            _ => TurnIntent.Fill
        };
        return (intent, systems.FirstOrDefault(system => system.Schema.Name == reply?.System)?.Schema.Name);
    }

    private sealed record Reply(string Intent, string System);
}
