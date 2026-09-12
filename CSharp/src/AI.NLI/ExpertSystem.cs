namespace AI.NLI;

/// <summary>Экспертная система за интерфейсом.</summary>
/// <param name="Schema">Поля входа и выхода.</param>
/// <param name="RunAsync">
/// Вызов: типизированные значения входа (<see cref="FormFieldExtensions.Typed"/>) дают поля выхода.
/// Вызывается и для проверки допущений, поэтому не должен иметь побочных эффектов.
/// </param>
public sealed record ExpertSystem(
    FormSchema Schema,
    Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<IReadOnlyDictionary<string, object?>>> RunAsync);
