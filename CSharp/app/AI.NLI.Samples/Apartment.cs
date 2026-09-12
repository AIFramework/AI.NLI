namespace AI.NLI.Samples;

/// <summary>
/// Учебная экспертная система: оценка квартиры по формуле. Нужна, чтобы на ней было видно весь цикл:
/// вопросы, допущения, проверку их влияния, «что если» и объяснение.
/// </summary>
public static class Apartment
{
    /// <summary>Описание системы.</summary>
    public const string SchemaYaml = """
        name: apartment
        description: Оценка рыночной стоимости квартиры
        fields:
          - name: city
            description: Город, где находится квартира
          - name: area
            type: number
            description: Общая площадь
            unit: м²
            min: 10
            max: 1000
          - name: rooms
            type: integer
            description: Число комнат
            min: 1
            max: 10
          - name: floor
            type: integer
            description: Этаж
            min: 1
          - name: floors
            type: integer
            description: Этажность дома
            required: false
            missing: skip
          - name: condition
            type: choice
            description: Состояние ремонта
            choices: [без ремонта, косметический, евроремонт]
            default: косметический
            missing: skip
          - name: balcony
            type: boolean
            description: Есть ли балкон
            missing: guess
          - name: year
            type: integer
            description: Год постройки дома
            required: false
            missing: skip
          - name: parking
            type: boolean
            description: Есть ли машино-место
            required: false
            missing: skip
          - name: parking_type
            type: choice
            description: Тип машино-места
            choices: [подземный, наземный]
            when: { field: parking, is: [true] }
        outputs:
          - name: price_mln
            type: number
            description: Оценка стоимости, млн руб.
          - name: per_m2
            type: number
            description: Цена квадратного метра в городе, руб.
        rules:
          - check: floor <= floors
            message: Этаж не может быть выше этажности дома
        """;

    /// <summary>Система целиком: описание и расчет.</summary>
    public static ExpertSystem System { get; } = new(FormSchema.FromYaml(SchemaYaml), EstimateAsync);

    /// <summary>Расчет по входу; подходит и для измененного описания, если имена полей те же.</summary>
    public static Task<IReadOnlyDictionary<string, object?>> EstimateAsync(IReadOnlyDictionary<string, object?> input, CancellationToken ct)
    {
        var city = (input.GetValueOrDefault("city") as string ?? "").ToLowerInvariant();
        var perMeter = city.Contains("москв") ? 300_000 : city.Contains("петербург") ? 200_000 : city.Contains("казан") ? 150_000 : 100_000;
        var factor = (input.GetValueOrDefault("condition") as string) switch { "без ремонта" => 0.9, "евроремонт" => 1.15, _ => 1.0 };
        if (input.GetValueOrDefault("balcony") is true)
            factor *= 1.02;
        if (input.GetValueOrDefault("floor") is 1L)
            factor *= 0.95;
        if (input.GetValueOrDefault("year") is long year && year < 1970)
            factor *= 0.9;
        if (input.GetValueOrDefault("parking_type") is "подземный")
            factor *= 1.05;

        var price = (input.GetValueOrDefault("area") as double? ?? 0) * perMeter * factor;
        IReadOnlyDictionary<string, object?> output = new Dictionary<string, object?>
        {
            ["price_mln"] = Math.Round(price / 1e6, 2),
            ["per_m2"] = (double)perMeter
        };
        return Task.FromResult(output);
    }
}
