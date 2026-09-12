namespace AI.NLI;

/// <summary>Проблема формы: поле пустое, хотя нужно, значение не прошло проверку или нарушено правило.</summary>
/// <param name="Field">Имя поля или текст правила.</param>
/// <param name="Problem">Что не так.</param>
public sealed record FieldIssue(string Field, string Problem);

/// <summary>Противоречие: два источника дали разные значения одного поля.</summary>
/// <param name="Field">Имя поля.</param>
/// <param name="Current">Значение, которое уже было.</param>
/// <param name="Offered">Значение, которое пришло позже.</param>
public sealed record FieldConflict(string Field, FieldValue Current, FieldValue Offered);

/// <summary>Запись журнала: что произошло с полем и когда.</summary>
/// <param name="At">Время.</param>
/// <param name="Field">Имя поля.</param>
/// <param name="Action">Что сделано: задано, исправлено, уточнено, противоречие, не знаю, новый запрос.</param>
/// <param name="Value">Значение с происхождением, если оно есть.</param>
public sealed record JournalEntry(DateTimeOffset At, string Field, string Action, FieldValue? Value);
