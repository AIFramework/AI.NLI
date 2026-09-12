using System.Globalization;
using System.Text.Json;

namespace AI.NLI;

/// <summary>Перевод JSON Schema объекта в описание формы.</summary>
internal static class JsonSchemaImport
{
    public static FormSchema Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new FormSchema
        {
            Name = Text(root, "title") ?? "",
            Description = Text(root, "description") ?? "",
            Fields = Fields(root)
        };
    }

    private static FormField[] Fields(JsonElement schema)
    {
        var required = Property(schema, "required") is { ValueKind: JsonValueKind.Array } list
            ? list.EnumerateArray().Select(item => item.GetString()).ToHashSet()
            : [];
        return Property(schema, "properties") is { ValueKind: JsonValueKind.Object } properties
            ? properties.EnumerateObject().Select(property => Field(property.Name, property.Value, required.Contains(property.Name))).ToArray()
            : [];
    }

    private static FormField Field(string name, JsonElement property, bool required)
    {
        var isArray = Text(property, "type") == "array";
        var element = isArray ? Property(property, "items") ?? default : property;
        var elementType = Text(element, "type");
        var isTable = isArray && elementType == "object";
        var choices = Property(element, "enum") is { ValueKind: JsonValueKind.Array } values
            ? values.EnumerateArray().Select(value => value.ToString()).ToArray()
            : [];

        return new FormField
        {
            Name = name,
            Description = Text(property, "description") ?? Text(property, "title") ?? "",
            Required = required,
            Type = isTable ? FieldType.Table
                : choices.Length > 0 ? FieldType.Choice
                : elementType switch
                {
                    "number" => FieldType.Number,
                    "integer" => FieldType.Integer,
                    "boolean" => FieldType.Boolean,
                    "string" when Text(element, "format") == "date" => FieldType.Date,
                    _ => FieldType.Text
                },
            Multi = isArray && !isTable,
            Choices = choices,
            Min = Number(element, "minimum"),
            Max = Number(element, "maximum"),
            Default = Property(property, "default")?.ToString(),
            Fields = isTable ? Fields(element) : []
        };
    }

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    private static string? Text(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static double? Number(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.Number } value
            ? double.Parse(value.GetRawText(), CultureInfo.InvariantCulture)
            : null;
}
