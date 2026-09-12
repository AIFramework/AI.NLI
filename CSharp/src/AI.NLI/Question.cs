namespace AI.NLI;

/// <summary>Вопрос пользователю о поле.</summary>
/// <param name="Field">Имя поля.</param>
/// <param name="Text">Формулировка.</param>
/// <param name="Choices">Готовые ответы: выбор применяется без модели.</param>
/// <param name="Reason">Почему спрашиваем: <see cref="NoData"/>, <see cref="Contradiction"/> или <see cref="AffectsResult"/>.</param>
public sealed record Question(string Field, string Text, string[] Choices, string Reason)
{
    /// <summary>Ответ «не знаю»: поле больше не спрашивают.</summary>
    public const string DontKnow = "не знаю";

    /// <summary>Значения нет ни в сообщении, ни в источниках.</summary>
    public const string NoData = "нет данных";

    /// <summary>Источники разошлись.</summary>
    public const string Contradiction = "противоречие";

    /// <summary>Значение было допущением, а вывод от него зависит.</summary>
    public const string AffectsResult = "от этого зависит вывод";

    /// <summary>Вопрос о поле с готовыми ответами по его типу.</summary>
    public static Question For(FormField field, string reason) => new(field.Name, field.Describe(), field.Type switch
    {
        FieldType.Choice => [.. field.Choices, DontKnow],
        FieldType.Boolean => ["да", "нет", DontKnow],
        _ => [DontKnow]
    }, reason);

    /// <summary>Вопрос о противоречии: ответы это оба значения.</summary>
    public static Question ForConflict(FormField field, FieldConflict conflict) => new(
        field.Name,
        $"{field.Describe()}: {conflict.Current.SourceLabel} дает «{conflict.Current.Value}», " +
        $"{conflict.Offered.SourceLabel} дает «{conflict.Offered.Value}». Какое значение верное?",
        [conflict.Current.Value, conflict.Offered.Value],
        Contradiction);
}
