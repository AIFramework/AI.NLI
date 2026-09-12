using System.Collections;
using System.Globalization;
using Microsoft.Extensions.AI;

namespace AI.NLI;

/// <summary>
/// Обработка выхода: объясняет вывод экспертной системы простым языком, отвечает на вопросы о нем
/// и сравнивает с прежним выводом. Неточные входные данные оговариваются всегда.
/// </summary>
/// <param name="chat">Любая модель за стандартным <see cref="IChatClient"/>.</param>
public sealed class OutputExplainer(IChatClient chat)
{
    /// <summary>Запрос по умолчанию: объяснить вывод.</summary>
    public const string ExplainRequest = "Объясни вывод";

    private const string Instruction =
        "Ты объясняешь пользователю вывод экспертной системы простым языком. Опирайся только на входные данные и вывод ниже, " +
        "ничего не добавляй от себя и не называй догадку фактом. Ответ короткий, не больше шести предложений: сначала сам " +
        "вывод одной фразой, затем главные причины, затем оговорки. Оговорки: значения с пометкой «умолчание», «догадка» " +
        "или «приблизительно»; если дана проверка допущений, скажи, от каких из них вывод почти не зависит, а от каких " +
        "зависит. Про незаполненные необязательные поля скажи одной фразой. Если дан прежний вывод, объясни, что " +
        "изменилось и из-за чего. Числа пиши с разделителями разрядов, без буквы ё и без длинных тире. " +
        "Ответь на запрос пользователя.";

    /// <summary>Ответ на запрос о выводе.</summary>
    /// <param name="schema">Описание системы.</param>
    /// <param name="values">Входные данные, на которых получен вывод.</param>
    /// <param name="output">Вывод экспертной системы.</param>
    /// <param name="request">Запрос пользователя; по умолчанию <see cref="ExplainRequest"/>.</param>
    /// <param name="before">Прежний вывод, если надо объяснить разницу.</param>
    /// <param name="check">Проверка неточных значений, если была.</param>
    /// <param name="ct">Отмена.</param>
    public async Task<string> ExplainAsync(FormSchema schema, IReadOnlyDictionary<string, FieldValue> values,
        IReadOnlyDictionary<string, object?> output, string request = ExplainRequest,
        IReadOnlyDictionary<string, object?>? before = null, AssumptionCheck? check = null, CancellationToken ct = default)
    {
        var inputs = schema.Fields.Active(values).Select(field => values.TryGetValue(field.Name, out var value)
            ? $"{field.Describe()} = {value.Value} ({value.SourceLabel}{(value.Approximate && !value.IsAssumed ? ", приблизительно" : "")})"
            : $"{field.Describe()} = не заполнено{(field.Required ? "" : ", необязательное")}");
        var text = (schema.Description.Length == 0 ? "" : $"О системе: {schema.Description}\n\n")
            + $"Входные данные:\n{string.Join('\n', inputs)}\n\nВывод экспертной системы:\n{Show(schema, output)}"
            + (before is null ? "" : $"\n\nПрежний вывод:\n{Show(schema, before)}")
            + (check is null ? "" : $"\n\n{Show(check)}")
            + $"\n\n{ModelCall.Quote("Запрос пользователя", request)}";

        var response = await chat.GetResponseAsync(ModelCall.Messages(Instruction, text), cancellationToken: ct);
        return response.Text;
    }

    private static string Show(FormSchema schema, IReadOnlyDictionary<string, object?> output) =>
        string.Join('\n', output.Select(pair => $"{schema.Outputs.Named(pair.Key)?.Describe() ?? pair.Key} = {Format(pair.Value)}"));

    private static string Show(AssumptionCheck check) =>
        "Проверка допущений (система вызвана с другими правдоподобными значениями):\n" +
        $"вывод почти не меняется от: {Names(check.Robust)}\nвывод заметно меняется от: {Names(check.Sensitive)}";

    private static string Names(IReadOnlyList<FormField> fields) =>
        fields.Count == 0 ? "нет" : string.Join(", ", fields.Select(field => $"{field.Name} ({field.Description})"));

    private static string Format(object? value) => value switch
    {
        null => "нет",
        string text => text,
        IEnumerable items => string.Join("; ", items.Cast<object?>().Select(Format)),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
}
