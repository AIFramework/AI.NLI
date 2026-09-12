using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AI.NLI;

/// <summary>Описание экспертной системы для интерфейса: поля входа, поля выхода и правила между полями.</summary>
public sealed record FormSchema
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>Имя системы: по нему ее выбирают, если систем несколько.</summary>
    public string Name { get; init; } = "";

    /// <summary>Что система делает: по описанию классификатор и выбирает систему под запрос.</summary>
    public string Description { get; init; } = "";

    /// <summary>Поля входа.</summary>
    public FormField[] Fields { get; init; } = [];

    /// <summary>Поля выхода: по ним вывод объясняется точнее. Могут отсутствовать.</summary>
    public FormField[] Outputs { get; init; } = [];

    /// <summary>Правила между полями входа.</summary>
    public FormRule[] Rules { get; init; } = [];

    /// <summary>Описание из YAML: <c>name</c>, <c>description</c>, <c>fields</c>, <c>outputs</c>, <c>rules</c>.</summary>
    /// <exception cref="InvalidDataException">Описание противоречиво.</exception>
    public static FormSchema FromYaml(string yaml) => Checked(Yaml.Deserialize<FormSchema?>(yaml) ?? new FormSchema());

    /// <summary>
    /// Описание из JSON Schema объекта: так подключается система, у которой описание входа уже есть
    /// (в том числе схема тела запроса из OpenAPI).
    /// </summary>
    /// <exception cref="InvalidDataException">Описание противоречиво.</exception>
    public static FormSchema FromJsonSchema(string json) => Checked(JsonSchemaImport.Read(json));

    private static FormSchema Checked(FormSchema schema)
    {
        var names = schema.Fields.Select(field => field.Name).ToList();
        if (names.Any(string.IsNullOrWhiteSpace) || names.Distinct().Count() != names.Count)
            throw new InvalidDataException("У каждого поля формы должно быть свое непустое имя");
        if (schema.Fields.FirstOrDefault(field => field.When is { } when && !names.Contains(when.Field)) is { } orphan)
            throw new InvalidDataException($"Условие поля «{orphan.Name}» ссылается на поле, которого нет");
        if (schema.Fields.FirstOrDefault(field => field.Type == FieldType.Table && field.Fields.Length == 0) is { } table)
            throw new InvalidDataException($"У таблицы «{table.Name}» не заданы колонки");
        if (schema.Rules.FirstOrDefault(rule => !rule.IsWellFormed) is { } broken)
            throw new InvalidDataException($"Правило «{broken.Check}» не разобрано: нужно «поле оператор поле-или-число»");

        return schema;
    }
}
