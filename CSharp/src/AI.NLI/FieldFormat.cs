using System.Globalization;
using System.Text.Json;

namespace AI.NLI;

/// <summary>Формат значений: приведение к каноническому виду и разбор в типизированное значение.</summary>
public static class FieldFormat
{
    /// <summary>Допуск приблизительного значения: «около 50» совпадает с 45–55.</summary>
    public const double ApproximateShare = 0.1;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "yyyy.MM.dd"];
    private static readonly string[] Yes = ["true", "да", "есть", "yes"];
    private static readonly string[] No = ["false", "нет", "no"];

    /// <summary>
    /// Значение в каноническом формате: «54,5» → «54.5», «да» → «true», «12.09.2026» → «2026-09-12».
    /// Что привести не удалось, остается как есть: что с ним не так, скажет <see cref="Parse"/>.
    /// </summary>
    public static string Normalize(FormField field, string raw) =>
        field.Type == FieldType.Table
            ? raw.Trim()
            : string.Join(FormField.ListSeparator + " ", Items(field, raw).Select(item => NormalizeItem(field, item)));

    /// <summary>
    /// Типизированное значение: double, long, bool, DateOnly или string; у поля с <see cref="FormField.Multi"/>
    /// список, у таблицы список строк-словарей. Если значение не годится, вместо него описание проблемы.
    /// </summary>
    public static (object? Value, string? Problem) Parse(FormField field, string value)
    {
        if (field.Type == FieldType.Table)
            return ParseTable(field, value);

        var items = Items(field, value);
        if (items.Length == 0 || items.Any(string.IsNullOrEmpty))
            return (null, "пустое значение");

        var parsed = new List<object?>();
        foreach (var item in items)
        {
            var (typed, problem) = ParseItem(field, NormalizeItem(field, item));
            if (problem is not null)
                return (null, problem);
            parsed.Add(typed);
        }

        return (field.Multi ? parsed : parsed[0], null);
    }

    /// <summary>Два значения поля совпадают; если хоть одно приблизительное, у чисел допуск 10%.</summary>
    public static bool Same(FormField field, FieldValue a, FieldValue b)
    {
        var (x, y) = (Parse(field, a.Value).Value, Parse(field, b.Value).Value);
        if (x is not (double or long) || y is not (double or long))
            return string.Equals(a.Value.Trim(), b.Value.Trim(), StringComparison.OrdinalIgnoreCase);

        var (dx, dy) = (Convert.ToDouble(x, CultureInfo.InvariantCulture), Convert.ToDouble(y, CultureInfo.InvariantCulture));
        var share = a.Approximate || b.Approximate ? ApproximateShare : 1e-9;
        return Math.Abs(dx - dy) <= share * Math.Max(Math.Abs(dx), Math.Abs(dy));
    }

    private static string[] Items(FormField field, string value) => field.Multi
        ? value.Split(FormField.ListSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        : [value.Trim()];

    private static string NormalizeItem(FormField field, string item) => field.Type switch
    {
        FieldType.Number => item.Replace(" ", "").Replace(" ", "").Replace(',', '.'),
        FieldType.Integer => item.Replace(" ", "").Replace(" ", ""),
        FieldType.Boolean => Yes.Contains(item.ToLowerInvariant()) ? "true" : No.Contains(item.ToLowerInvariant()) ? "false" : item,
        FieldType.Date => DateOnly.TryParseExact(item, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : item,
        FieldType.Choice => field.Choices.FirstOrDefault(choice => choice.Equals(item, StringComparison.OrdinalIgnoreCase)) ?? item,
        _ => item
    };

    private static (object? Value, string? Problem) ParseItem(FormField field, string item) => field.Type switch
    {
        FieldType.Number => double.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? InRange(field, number, number)
            : (null, "ожидается число"),
        FieldType.Integer => long.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            ? InRange(field, integer, integer)
            : (null, "ожидается целое число"),
        FieldType.Boolean => bool.TryParse(item, out var flag) ? (flag, null) : (null, "ожидается да или нет"),
        FieldType.Date => DateOnly.TryParseExact(item, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? (date, null)
            : (null, "ожидается дата ГГГГ-ММ-ДД"),
        FieldType.Choice => field.Choices.Contains(item) ? (item, null) : (null, $"«{item}» нет среди вариантов"),
        _ => (item, null)
    };

    private static (object? Value, string? Problem) InRange(FormField field, double number, object value) =>
        number < field.Min ? (null, $"меньше {field.Min}")
        : number > field.Max ? (null, $"больше {field.Max}")
        : (value, null);

    private static (object? Value, string? Problem) ParseTable(FormField field, string value)
    {
        try
        {
            using var json = JsonDocument.Parse(value);
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
                return (null, "ожидается непустой JSON-массив строк таблицы");

            var rows = new List<Dictionary<string, object?>>();
            foreach (var row in json.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    return (null, "строка таблицы должна быть объектом");

                var cells = new Dictionary<string, object?>();
                foreach (var column in field.Fields)
                {
                    var cell = row.TryGetProperty(column.Name, out var raw) && raw.ValueKind != JsonValueKind.Null
                        ? raw.ValueKind == JsonValueKind.String ? raw.GetString()! : raw.GetRawText()
                        : null;
                    var (typed, problem) = cell is null
                        ? (null, column.Required ? "не заполнено" : null)
                        : Parse(column, cell);
                    if (problem is not null)
                        return (null, $"{column.Name}: {problem}");
                    cells[column.Name] = typed;
                }

                rows.Add(cells);
            }

            return (rows, null);
        }
        catch (JsonException)
        {
            return (null, "ожидается JSON-массив строк таблицы");
        }
    }
}
