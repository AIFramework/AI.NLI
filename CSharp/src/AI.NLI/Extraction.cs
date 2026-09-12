namespace AI.NLI;

/// <summary>Что нашлось в тексте.</summary>
public sealed class Extraction
{
    /// <summary>Принятые значения.</summary>
    public Dictionary<string, FieldValue> Values { get; } = [];

    /// <summary>Поля, про которые пользователь прямо сказал, что не знает.</summary>
    public HashSet<string> Unknown { get; } = [];
}

/// <summary>Настройки извлечения.</summary>
public sealed record ExtractorOptions
{
    /// <summary>Длина фрагмента: длинный текст режется на фрагменты с перекрытием в десятую часть.</summary>
    public int ChunkSize { get; init; } = 8000;

    /// <summary>
    /// Дополнительно спросить модель, следует ли каждое значение из своей цитаты (проверка
    /// следования, NLI). Лишний запрос на каждый текст, зато ловит значение, приписанное к чужой цитате.
    /// </summary>
    public bool VerifyEntailment { get; init; }

    /// <summary>Опорная дата для относительных дат («вчера»); по умолчанию сегодняшняя.</summary>
    public DateOnly? Today { get; init; }
}
