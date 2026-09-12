using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AI.NLI.Tests;

/// <summary>Модель-заглушка: ответ строится по тексту запроса, запросы запоминаются.</summary>
internal sealed class FakeChat(Func<string, string> reply) : IChatClient
{
    private const string Empty = """{"values":[],"items":[]}""";

    public List<string> Prompts { get; } = [];

    /// <summary>Отказывать запросам с JSON-схемой в формате ответа, как модель без ее поддержки.</summary>
    public bool RejectSchema { get; init; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (RejectSchema && options?.ResponseFormat is ChatResponseFormatJson { Schema: not null })
            throw new InvalidOperationException("Модель не поддерживает JSON-схему");

        var prompt = string.Join('\n', messages.Select(message => message.Text));
        Prompts.Add(prompt);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply(prompt))));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    /// <summary>Ответ по первой метке, найденной в запросе; без совпадения пустой ответ.</summary>
    public static Func<string, string> Route(params (string Marker, Func<string, string> Reply)[] routes) =>
        prompt => routes.FirstOrDefault(route => prompt.Contains(route.Marker)).Reply?.Invoke(prompt) ?? Empty;

    /// <summary>Ответ извлечения: точные значения с цитатами.</summary>
    public static string Values(params (string Name, string Value, string Evidence)[] values) =>
        Reply(values.Select(value => (value.Name, value.Value, value.Evidence, "exact")));

    /// <summary>Ответ извлечения: пользователь не знает значения.</summary>
    public static string Unknown(string name, string evidence) => Reply([(name, "", evidence, "unknown")]);

    /// <summary>Ответ извлечения: названа только граница.</summary>
    public static string Bound(string name, string estimate, string evidence) => Reply([(name, estimate, evidence, "bound")]);

    /// <summary>Ответ извлечения: значение названо примерно.</summary>
    public static string Approximate(string name, string value, string evidence) => Reply([(name, value, evidence, "approximate")]);

    /// <summary>Ответ извлечения с разной точностью значений: kind это exact, approximate, bound или unknown.</summary>
    public static string Mixed(params (string Name, string Value, string Evidence, string Kind)[] values) => Reply(values);

    private static string Reply(IEnumerable<(string Name, string Value, string Evidence, string Kind)> values) =>
        JsonSerializer.Serialize(new { values = values.Select(value => new { name = value.Name, value = value.Value, evidence = value.Evidence, kind = value.Kind }) });
}
