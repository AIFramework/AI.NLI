using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AI.NLI.Samples;

/// <summary>
/// Подключение к OpenRouter. Ключ берется из OPENROUTER_API_KEY или из key.txt в каталоге приложения
/// либо выше по дереву (корень репозитория; файл в .gitignore).
/// </summary>
public static class OpenRouter
{
    /// <summary>Модель по умолчанию.</summary>
    public const string DefaultModel = "google/gemini-2.5-flash";

    private static readonly Uri Endpoint = new("https://openrouter.ai/api/v1");

    /// <summary>Клиент модели; <c>null</c>, если ключ не найден.</summary>
    public static IChatClient? CreateClient(string? model)
    {
        var key = FindKey();
        if (key is null)
            return null;

        var name = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        return new ChatClient(name, new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = Endpoint }).AsIChatClient();
    }

    private static string? FindKey()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment.Trim();

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName, "key.txt");
            if (File.Exists(file))
                return File.ReadAllText(file).Trim();
        }

        return null;
    }
}
