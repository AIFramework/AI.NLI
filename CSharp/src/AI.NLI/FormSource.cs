namespace AI.NLI;

/// <summary>Текст источника.</summary>
/// <param name="Text">Сам текст.</param>
/// <param name="Reference">Что это за текст: имя документа, адрес страницы.</param>
public sealed record SourceText(string Text, string? Reference = null);

/// <summary>
/// Источник недостающих данных: документы, поиск в сети, база. Источник отдает тексты, и значения
/// из них извлекаются так же, как из сообщения, с той же проверкой цитатой. Пользователь источником
/// не является: вопросы ему возвращаются ходом диалога.
/// </summary>
/// <param name="Kind">Чей текст: попадет в происхождение значений.</param>
/// <param name="ReadAsync">По списку еще пустых полей возвращает тексты; пустой список, если сказать нечего.</param>
public sealed record FormSource(ValueSource Kind, Func<IReadOnlyList<FormField>, CancellationToken, Task<IReadOnlyList<SourceText>>> ReadAsync);
