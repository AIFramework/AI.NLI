namespace AI.NLI;

/// <summary>Что нашлось в тексте.</summary>
public sealed class Extraction
{
    /// <summary>Принятые значения.</summary>
    public Dictionary<string, FieldValue> Values { get; } = [];

    /// <summary>
    /// Поля, для которых названа только граница или диапазон («выше 4-го»): значение здесь это
    /// лучшая оценка модели, помеченная как приблизительная, а может быть и пустым.
    /// </summary>
    public Dictionary<string, FieldValue> Bounds { get; } = [];

    /// <summary>Поля, про которые пользователь прямо сказал, что не знает.</summary>
    public HashSet<string> Unknown { get; } = [];
}

/// <summary>Настройки извлечения.</summary>
public sealed record ExtractorOptions
{
    /// <summary>Длина фрагмента: длинный текст режется на фрагменты с перекрытием в десятую часть.</summary>
    public int ChunkSize { get; init; } = 8000;

    /// <summary>
    /// Дополнительно спросить модель, следует ли каждое значение из своей цитаты (проверка следования,
    /// NLI). Лишний запрос на каждый текст, зато отсекает значение, выведенное из цитаты, которая его не
    /// подтверждает («выше 4-го» не дает 5).
    /// </summary>
    public bool VerifyEntailment { get; init; } = true;

    /// <summary>Опорная дата для относительных дат («вчера»); по умолчанию сегодняшняя.</summary>
    public DateOnly? Today { get; init; }
}
