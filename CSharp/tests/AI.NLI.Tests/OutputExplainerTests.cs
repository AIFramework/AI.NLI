namespace AI.NLI.Tests;

public sealed class OutputExplainerTests
{
    [Fact]
    public async Task ModelSeesSystemAssumptionsCheckAndPreviousResult()
    {
        var schema = FormSchema.FromYaml("""
            description: Линейная модель цены
            fields:
              - name: city
              - name: area
                type: number
              - name: balcony
                type: boolean
              - name: year
                type: integer
            outputs:
              - name: price
                type: number
                description: Цена, млн руб.
            """);
        var values = new Dictionary<string, FieldValue>
        {
            ["city"] = new("Таганрог", ValueSource.User, "в Таганроге"),
            ["area"] = new("50", ValueSource.User, "около 50") { Approximate = true },
            ["balcony"] = new("true", ValueSource.Guess)
        };
        var chat = new FakeChat(_ => "Объяснение");

        var text = await new OutputExplainer(chat).ExplainAsync(schema, values,
            new Dictionary<string, object?> { ["price"] = 9.8 }, "а если бы",
            new Dictionary<string, object?> { ["price"] = 9.5 }, new AssumptionCheck([schema.Fields[1]], [schema.Fields[2]]));

        Assert.Equal("Объяснение", text);
        var prompt = Assert.Single(chat.Prompts);
        Assert.Contains("О системе: Линейная модель цены", prompt);
        Assert.Contains("= true (догадка)", prompt);
        Assert.Contains("= 50 (пользователь, приблизительно)", prompt);
        Assert.Contains("year:  [целое число] = не заполнено", prompt);
        Assert.Contains("price: Цена, млн руб. [число] = 9.8", prompt);
        Assert.Contains("Прежний вывод:\nprice: Цена, млн руб. [число] = 9.5", prompt);
        Assert.Contains("вывод почти не меняется от: balcony", prompt);
        Assert.Contains("вывод заметно меняется от: area", prompt);
    }
}
