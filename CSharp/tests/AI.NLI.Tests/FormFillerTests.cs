namespace AI.NLI.Tests;

public sealed class FormFillerTests
{
    private static readonly FormSchema Schema = FormSchema.FromYaml("""
        fields:
          - name: city
          - name: area
            type: number
          - name: floor
            type: integer
          - name: balcony
            type: boolean
            missing: guess
          - name: condition
            type: choice
            choices: [косметический, евроремонт]
            default: косметический
            missing: skip
          - name: year
            type: integer
            missing: skip
        """);

    [Fact]
    public async Task AsksOnlyAboutFieldsWithAskPolicyAndAssumesNothingYet()
    {
        var state = new FormState();

        var questions = await Filler().FillAsync(Schema, state, "Квартира в Казани, 54 квадрата");

        Assert.Equal(["floor"], questions.Select(question => question.Field));
        Assert.Equal(["city", "area"], state.Values.Keys);
    }

    [Fact]
    public async Task WhenNothingToAskAppliesDefaultsAndGuesses()
    {
        var state = new FormState();
        await Filler().FillAsync(Schema, state, "Квартира в Казани, 54 квадрата");

        var questions = await Filler().FillAsync(Schema, state, "пятый этаж");

        Assert.Empty(questions);
        Assert.Equal(ValueSource.Guess, state.Values["balcony"].Source);
        Assert.Equal(new FieldValue("косметический", ValueSource.Default), state.Values["condition"]);
        Assert.DoesNotContain("year", state.Values.Keys);
    }

    [Fact]
    public async Task DocumentThatContradictsUserBecomesQuestion()
    {
        var document = new FormSource(ValueSource.Document, (_, _) =>
            Task.FromResult<IReadOnlyList<SourceText>>([new SourceText("Выписка: площадь 60 м², этаж 5", "выписка.pdf")]));
        var state = new FormState();

        var questions = await Filler(document).FillAsync(Schema, state, "Квартира в Казани, 54 квадрата");

        var conflict = Assert.Single(questions);
        Assert.Equal(("area", Question.Contradiction), (conflict.Field, conflict.Reason));
        Assert.Equal(["54", "60"], conflict.Choices);
        Assert.Equal("выписка.pdf", state.Values["floor"].Reference);
    }

    private static FormFiller Filler(params FormSource[] sources) => new(new FormExtractor(new FakeChat(FakeChat.Route(
        ("правдоподобные", _ => FakeChat.Values(("balcony", "true", "в таких домах балкон обычно есть"))),
        ("Выписка", _ => FakeChat.Values(("area", "60", "площадь 60"), ("floor", "5", "этаж 5"))),
        ("пятый этаж", _ => FakeChat.Values(("floor", "5", "пятый этаж"))),
        ("54 квадрата", _ => FakeChat.Values(("city", "Казань", "в Казани"), ("area", "54", "54 квадрата")))))), sources);
}
