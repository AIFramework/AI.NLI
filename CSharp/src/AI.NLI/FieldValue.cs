namespace AI.NLI;

/// <summary>Откуда взялось значение поля.</summary>
public enum ValueSource
{
    /// <summary>Сообщение или ответ пользователя.</summary>
    User,
    /// <summary>Документ.</summary>
    Document,
    /// <summary>Поиск в сети.</summary>
    Web,
    /// <summary>Умолчание из описания поля.</summary>
    Default,
    /// <summary>Догадка модели.</summary>
    Guess
}

/// <summary>Значение поля вместе с происхождением.</summary>
/// <param name="Value">Значение в каноническом формате типа поля.</param>
/// <param name="Source">Откуда взято.</param>
/// <param name="Evidence">
/// Цитата из текста источника; у догадки это обоснование модели, у умолчания пусто.
/// </param>
public sealed record FieldValue(string Value, ValueSource Source, string? Evidence = null)
{
    /// <summary>Значение названо приблизительно («около 50»).</summary>
    public bool Approximate { get; init; }

    /// <summary>Где в источнике найдено: документ и место в нем.</summary>
    public string? Reference { get; init; }

    /// <summary>Значение не подтверждено текстом: вывод, который на нем держится, надо оговаривать.</summary>
    public bool IsAssumed => Source is ValueSource.Default or ValueSource.Guess;

    /// <summary>Происхождение по-русски, для людей и для модели.</summary>
    public string SourceLabel => Source switch
    {
        ValueSource.User => "пользователь",
        ValueSource.Document => "документ",
        ValueSource.Web => "сеть",
        ValueSource.Default => "умолчание",
        _ => "догадка"
    };
}
