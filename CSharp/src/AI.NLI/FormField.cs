namespace AI.NLI;

/// <summary>Тип значения поля.</summary>
public enum FieldType
{
    /// <summary>Произвольный текст.</summary>
    Text,
    /// <summary>Число с точкой.</summary>
    Number,
    /// <summary>Целое число.</summary>
    Integer,
    /// <summary>true или false.</summary>
    Boolean,
    /// <summary>Дата в формате ГГГГ-ММ-ДД.</summary>
    Date,
    /// <summary>Вариант из списка <see cref="FormField.Choices"/>.</summary>
    Choice,
    /// <summary>Повторяющиеся строки: JSON-массив объектов с колонками <see cref="FormField.Fields"/>.</summary>
    Table
}

/// <summary>Что делать с полем, если ни один источник не дал значения.</summary>
public enum MissingPolicy
{
    /// <summary>Спросить пользователя.</summary>
    Ask,
    /// <summary>Предположить значение моделью; значение помечается как догадка.</summary>
    Guess,
    /// <summary>Оставить пустым.</summary>
    Skip
}

/// <summary>Условие поля: оно нужно, только если у другого поля одно из значений.</summary>
public sealed record FieldCondition
{
    /// <summary>Поле, от которого зависит нужность.</summary>
    public required string Field { get; init; }

    /// <summary>Значения того поля в каноническом формате, при которых поле нужно.</summary>
    public string[] Is { get; init; } = [];
}

/// <summary>Поле формы входа или выхода экспертной системы.</summary>
public sealed record FormField
{
    /// <summary>Разделитель значений поля с <see cref="Multi"/>.</summary>
    public const string ListSeparator = ";";

    /// <summary>Имя параметра экспертной системы.</summary>
    public required string Name { get; init; }

    /// <summary>Тип значения.</summary>
    public FieldType Type { get; init; }

    /// <summary>Что означает поле: по описанию модель и находит значение в тексте.</summary>
    public string Description { get; init; } = "";

    /// <summary>Пустое поле попадает в список проблем формы.</summary>
    public bool Required { get; init; } = true;

    /// <summary>Допустимые варианты для <see cref="FieldType.Choice"/>.</summary>
    public string[] Choices { get; init; } = [];

    /// <summary>Значений может быть несколько, через <see cref="ListSeparator"/>.</summary>
    public bool Multi { get; init; }

    /// <summary>Нижняя граница числа.</summary>
    public double? Min { get; init; }

    /// <summary>Верхняя граница числа.</summary>
    public double? Max { get; init; }

    /// <summary>Единица измерения: в нее модель переводит найденное число.</summary>
    public string? Unit { get; init; }

    /// <summary>Значение, если его не дал ни один источник. Применяется раньше политики.</summary>
    public string? Default { get; init; }

    /// <summary>Последнее средство для пустого поля.</summary>
    public MissingPolicy Missing { get; init; }

    /// <summary>Поле нужно только при этом условии.</summary>
    public FieldCondition? When { get; init; }

    /// <summary>Колонки для <see cref="FieldType.Table"/>.</summary>
    public FormField[] Fields { get; init; } = [];

    /// <summary>Поле одной строкой: так его видят и модель, и пользователь в вопросе.</summary>
    public string Describe()
    {
        List<string> details = [TypeName(Type)];
        if (Unit is not null)
            details.Add(Unit);
        if (Min is not null)
            details.Add($"от {Min}");
        if (Max is not null)
            details.Add($"до {Max}");
        if (Choices.Length > 0)
            details.Add("варианты: " + string.Join(" | ", Choices));
        if (Multi)
            details.Add($"можно несколько через «{ListSeparator}»");
        if (Fields.Length > 0)
            details.Add("колонки: " + string.Join(", ", Fields.Select(column => $"{column.Name} ({TypeName(column.Type)})")));
        if (When is not null)
            details.Add($"нужно, если {When.Field} = {string.Join(" или ", When.Is)}");

        return $"{Name}: {Description} [{string.Join("; ", details)}]";
    }

    private static string TypeName(FieldType type) => type switch
    {
        FieldType.Number => "число",
        FieldType.Integer => "целое число",
        FieldType.Boolean => "true или false",
        FieldType.Date => "дата ГГГГ-ММ-ДД",
        FieldType.Choice => "вариант из списка",
        FieldType.Table => "таблица, JSON-массив объектов",
        _ => "текст"
    };
}
