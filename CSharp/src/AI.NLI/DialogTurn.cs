namespace AI.NLI;

/// <summary>Чем закончился ход.</summary>
public enum TurnKind
{
    /// <summary>Нужны ответы пользователя: <see cref="DialogTurn.Questions"/>.</summary>
    Questions,
    /// <summary>Спрашивать больше нечего, но форма не готова: <see cref="DialogTurn.Issues"/>.</summary>
    Incomplete,
    /// <summary>Экспертная система отработала: вывод и объяснение.</summary>
    Result,
    /// <summary>Ответ на вопрос о выводе или на вопрос «что если»; состояние формы не менялось.</summary>
    Answer
}

/// <summary>Итог хода диалога.</summary>
/// <param name="Kind">Чем закончился ход.</param>
/// <param name="Text">Что показать пользователю.</param>
public sealed record DialogTurn(TurnKind Kind, string Text)
{
    /// <summary>Вопросы пользователю.</summary>
    public IReadOnlyList<Question> Questions { get; init; } = [];

    /// <summary>Проблемы формы.</summary>
    public IReadOnlyList<FieldIssue> Issues { get; init; } = [];

    /// <summary>Вывод экспертной системы.</summary>
    public IReadOnlyDictionary<string, object?>? Output { get; init; }
}

/// <summary>Настройки диалога.</summary>
public sealed record DialogOptions
{
    /// <summary>Сколько вопросов задавать за ход: остальные подождут следующего.</summary>
    public int QuestionLimit { get; init; } = 3;

    /// <summary>Относительное изменение числа в выводе, которое еще не считается изменением вывода.</summary>
    public double Tolerance { get; init; } = 0.05;

    /// <summary>Настройки извлечения.</summary>
    public ExtractorOptions Extractor { get; init; } = new();
}
