namespace AI.NLI;

/// <summary>
/// Состояние диалога по форме. Сериализуется в JSON целиком: так диалог переживает перезапуск
/// и продолжается следующим запросом, а не ожиданием ответа в памяти.
/// </summary>
public sealed class FormState
{
    /// <summary>Выбранная экспертная система.</summary>
    public string? System { get; set; }

    /// <summary>Значения по имени поля.</summary>
    public Dictionary<string, FieldValue> Values { get; init; } = [];

    /// <summary>Поля, про которые пользователь сказал «не знаю»: их больше не спрашивают.</summary>
    public HashSet<string> Unknown { get; init; } = [];

    /// <summary>Поля, по которым источники уже опрошены.</summary>
    public HashSet<string> Searched { get; init; } = [];

    /// <summary>Допущения, о которых уже спросили из-за их влияния на вывод.</summary>
    public HashSet<string> Asked { get; init; } = [];

    /// <summary>Нерешенные противоречия между источниками.</summary>
    public List<FieldConflict> Conflicts { get; init; } = [];

    /// <summary>Вопросы, заданные последним ходом и ждущие ответа.</summary>
    public List<Question> Pending { get; set; } = [];

    /// <summary>Последний вывод экспертной системы.</summary>
    public Dictionary<string, object?>? Output { get; set; }

    /// <summary>Журнал происхождения: кто, когда и откуда дал каждое значение.</summary>
    public List<JournalEntry> Journal { get; init; } = [];

    /// <summary>
    /// Принимает значение. Слово пользователя заменяет прежнее (исправление), подтвержденное текстом
    /// заменяет допущение, а расхождение двух подтвержденных источников становится противоречием.
    /// </summary>
    public void Apply(FormField field, FieldValue value)
    {
        if (!Values.TryGetValue(field.Name, out var current))
            Set(field.Name, value, "задано");
        else if (FieldFormat.Same(field, current, value))
            return;
        else if (value.Source == ValueSource.User)
            Set(field.Name, value, "исправлено");
        else if (current.IsAssumed && !value.IsAssumed)
            Set(field.Name, value, "уточнено");
        else if (!current.IsAssumed && !value.IsAssumed && Conflicts.All(conflict => conflict.Field != field.Name))
        {
            Conflicts.Add(new FieldConflict(field.Name, current, value));
            Log(field.Name, "противоречие", value);
        }
    }

    /// <summary>Пользователь не знает значения.</summary>
    public void MarkUnknown(string field)
    {
        if (Unknown.Add(field))
            Log(field, "не знаю", null);
    }

    /// <summary>Новый запрос: все, кроме журнала, начинается заново.</summary>
    public void Reset()
    {
        System = null;
        Output = null;
        Pending = [];
        Values.Clear();
        Unknown.Clear();
        Searched.Clear();
        Asked.Clear();
        Conflicts.Clear();
        Log("*", "новый запрос", null);
    }

    /// <summary>Что уже известно и что спрошено: контекст для разбора очередной реплики.</summary>
    public string Context() =>
        $"Уже известно:\n{string.Join('\n', Values.Select(pair => $"{pair.Key} = {pair.Value.Value}"))}\n\n" +
        $"Заданные пользователю вопросы:\n{string.Join('\n', Pending.Select(question => $"{question.Field}: {question.Text}"))}";

    private void Set(string field, FieldValue value, string action)
    {
        Values[field] = value;
        Unknown.Remove(field);
        Conflicts.RemoveAll(conflict => conflict.Field == field);
        Log(field, action, value);
    }

    private void Log(string field, string action, FieldValue? value) =>
        Journal.Add(new JournalEntry(DateTimeOffset.UtcNow, field, action, value));
}
