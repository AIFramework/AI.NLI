using System.Globalization;

namespace AI.NLI.Demo;

/// <summary>
/// Учебная экспертная система: множественная линейная регрессия цены квартиры в Таганроге.
/// Коэффициенты учебные, выбраны вручную для демонстрации, а не обучены на реальных сделках.
/// </summary>
internal static class TaganrogRegression
{
    /// <summary>Описание входа и выхода; формула дописывается к описанию в <see cref="System"/>.</summary>
    public const string SchemaYaml = """
        name: taganrog
        description: Оценка рыночной стоимости квартиры в Таганроге
        fields:
          - name: area
            type: number
            description: Общая площадь
            unit: м²
            min: 10
            max: 500
          - name: rooms
            type: integer
            description: Число комнат
            min: 1
            max: 8
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
          - name: low_mln
            type: number
            description: Нижняя граница, млн руб.
          - name: high_mln
            type: number
            description: Верхняя граница, млн руб.
          - name: per_m2
            type: number
            description: Цена квадратного метра, руб.
        rules:
          - check: floor <= floors
            message: Этаж не может быть выше этажности дома
        """;

    private const double Intercept = 0.4;

    // Стандартная ошибка остатков, млн руб.: из нее границы 95% интервала
    private const double ResidualError = 0.35;

    // Пустое поле дает признаку ноль: для года это средний по выборке 1985-й
    private static readonly Term[] Terms =
    [
        new("площадь, м²", 0.082, input => Number(input, "area")),
        new("число комнат", 0.12, input => Number(input, "rooms")),
        new("первый этаж", -0.35, input => input.GetValueOrDefault("floor") is 1L ? 1 : 0),
        new("последний этаж", -0.2, input => input.GetValueOrDefault("floor") is long floor && input.GetValueOrDefault("floors") is long floors && floor == floors && floors > 1 ? 1 : 0),
        new("без ремонта", -0.45, input => Is(input, "condition", "без ремонта")),
        new("евроремонт", 0.6, input => Is(input, "condition", "евроремонт")),
        new("балкон", 0.12, input => input.GetValueOrDefault("balcony") is true ? 1 : 0),
        new("год постройки минус 1985", 0.01, input => input.GetValueOrDefault("year") is long year ? year - 1985 : 0),
        new("подземное машино-место", 0.5, input => Is(input, "parking_type", "подземный")),
        new("наземное машино-место", 0.2, input => Is(input, "parking_type", "наземный"))
    ];

    /// <summary>Система по описанию: к описанию дописывается формула, чтобы объяснение опиралось на нее.</summary>
    public static ExpertSystem System(FormSchema schema) =>
        new(schema with { Description = $"{schema.Description}. Множественная линейная регрессия: {Formula()}" }, RunAsync);

    private static Task<IReadOnlyDictionary<string, object?>> RunAsync(IReadOnlyDictionary<string, object?> input, CancellationToken ct)
    {
        var price = Intercept + Terms.Sum(term => term.Weight * term.Value(input));
        var area = Number(input, "area");
        IReadOnlyDictionary<string, object?> output = new Dictionary<string, object?>
        {
            ["price_mln"] = Math.Round(price, 2),
            ["low_mln"] = Math.Round(price - 1.96 * ResidualError, 2),
            ["high_mln"] = Math.Round(price + 1.96 * ResidualError, 2),
            ["per_m2"] = area > 0 ? Math.Round(price * 1e6 / area / 100) * 100 : 0.0
        };
        return Task.FromResult(output);
    }

    private static string Formula() =>
        "цена, млн руб. = " + Intercept.ToString(CultureInfo.InvariantCulture)
        + string.Concat(Terms.Select(term => $" {(term.Weight < 0 ? "-" : "+")} {Math.Abs(term.Weight).ToString(CultureInfo.InvariantCulture)} × {term.Title}"))
        + $"; стандартная ошибка {ResidualError.ToString(CultureInfo.InvariantCulture)} млн руб.";

    private static double Number(IReadOnlyDictionary<string, object?> input, string name) =>
        input.GetValueOrDefault(name) switch { double number => number, long integer => integer, _ => 0 };

    private static double Is(IReadOnlyDictionary<string, object?> input, string name, string value) =>
        input.GetValueOrDefault(name) as string == value ? 1 : 0;

    private sealed record Term(string Title, double Weight, Func<IReadOnlyDictionary<string, object?>, double> Value);
}
