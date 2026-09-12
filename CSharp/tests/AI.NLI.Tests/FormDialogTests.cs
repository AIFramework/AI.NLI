using System.Text.Json;

namespace AI.NLI.Tests;

public sealed class FormDialogTests
{
    private static readonly FormSchema Schema = FormSchema.FromYaml("""
        name: estimate
        description: Оценка квартиры
        fields:
          - name: city
          - name: area
            type: number
          - name: floor
            type: integer
          - name: condition
            type: choice
            choices: [без ремонта, косметический, евроремонт]
            default: косметический
            missing: skip
          - name: balcony
            type: boolean
            missing: guess
        outputs:
          - name: price
            type: number
        """);

    [Fact]
    public async Task ReturnsQuestionsInsteadOfWaiting()
    {
        var state = new FormState();

        var turn = await Dialog().ReplyAsync(state, "Казань, 54 квадрата");

        Assert.Equal(TurnKind.Questions, turn.Kind);
        Assert.Equal("floor", Assert.Single(turn.Questions).Field);
        Assert.Equal(turn.Questions, state.Pending);
    }

    [Fact]
    public async Task AssumptionThatChangesResultIsAskedAndChoiceNeedsNoModel()
    {
        var (dialog, state) = (Dialog(), new FormState());
        await dialog.ReplyAsync(state, "Казань, 54 квадрата");

        var turn = await dialog.ReplyAsync(state, "пятый этаж");

        // Балкон угадан, но меняет цену на 1%, а ремонт по умолчанию меняет ее на 10-15%: спрашиваем только про ремонт
        var question = Assert.Single(turn.Questions);
        Assert.Equal(("condition", Question.AffectsResult), (question.Field, question.Reason));

        var result = await dialog.ChooseAsync(state, "condition", "евроремонт");

        Assert.Equal(TurnKind.Result, result.Kind);
        Assert.Equal(54 * 1.15 * 1.01, (double)result.Output!["price"]!, 6);
        Assert.Equal(["balcony"], result.Check!.Robust.Select(field => field.Name));
        Assert.Empty(state.Pending);
    }

    [Fact]
    public async Task WhatIfAnswersWithoutChangingForm()
    {
        var (dialog, state) = await ResultAsync("what_if");

        var turn = await dialog.ReplyAsync(state, "а если бы площадь была 60");

        Assert.Equal(TurnKind.Answer, turn.Kind);
        Assert.Equal(60 * 1.15 * 1.01, (double)turn.Output!["price"]!, 6);
        Assert.Equal("54", state.Values["area"].Value);
    }

    [Fact]
    public async Task QuestionAboutResultIsAnswered()
    {
        var (dialog, state) = await ResultAsync("question");

        var turn = await dialog.ReplyAsync(state, "почему так дорого?");

        Assert.Equal((TurnKind.Answer, "Объяснение"), (turn.Kind, turn.Text));
    }

    [Fact]
    public async Task ClassifierChoosesSystem()
    {
        var state = new FormState();
        var dialog = new FormDialog(Chat("fill", "second"), [System("first"), System("second")]);

        await dialog.ReplyAsync(state, "Казань, 54 квадрата");

        Assert.Equal("second", state.System);
    }

    private static async Task<(FormDialog Dialog, FormState State)> ResultAsync(string intent)
    {
        var (dialog, state) = (Dialog(intent), new FormState());
        await dialog.ReplyAsync(state, "Казань, 54 квадрата");
        await dialog.ReplyAsync(state, "пятый этаж");
        Assert.Equal(TurnKind.Result, (await dialog.ChooseAsync(state, "condition", "евроремонт")).Kind);
        return (dialog, state);
    }

    private static FormDialog Dialog(string intent = "fill") => new(Chat(intent), [System()]);

    private static FakeChat Chat(string intent, string system = "") => new(FakeChat.Route(
        ("Определи, что делает реплика", _ => JsonSerializer.Serialize(new { intent, system })),
        ("Ты объясняешь", _ => "Объяснение"),
        ("правдоподобные", _ => FakeChat.Values(("balcony", "true", "обычно есть"))),
        ("площадь была 60", _ => FakeChat.Values(("area", "60", "площадь была 60"))),
        ("пятый этаж", _ => FakeChat.Values(("floor", "5", "пятый этаж"))),
        ("54 квадрата", _ => FakeChat.Values(("city", "Казань", "Казань"), ("area", "54", "54 квадрата")))));

    private static ExpertSystem System(string name = "estimate") => new(Schema with { Name = name }, (input, _) =>
    {
        var condition = input["condition"] switch { "евроремонт" => 1.15, "без ремонта" => 0.9, _ => 1.0 };
        var balcony = input["balcony"] is true ? 1.01 : 1.0;
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(
            new Dictionary<string, object?> { ["price"] = (double)input["area"]! * condition * balcony });
    });
}
