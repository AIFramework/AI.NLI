namespace AI.NLI.Tests;

public sealed class SensitivityTests
{
    [Fact]
    public async Task SeparatesUncertainValuesThatMatterFromThoseThatDoNot()
    {
        var schema = FormSchema.FromYaml("""
            fields:
              - name: area
                type: number
              - name: balcony
                type: boolean
              - name: note
            """);
        var system = new ExpertSystem(schema, (input, _) => Task.FromResult<IReadOnlyDictionary<string, object?>>(
            new Dictionary<string, object?> { ["price"] = (double)input["area"]! * (input["balcony"] is true ? 1.02 : 1.0) }));
        var values = new Dictionary<string, FieldValue>
        {
            ["area"] = new("80", ValueSource.User, "около 80") { Approximate = true },
            ["balcony"] = new("true", ValueSource.Guess),
            ["note"] = new("что-то", ValueSource.Guess)
        };
        var baseline = await system.RunAsync(schema.Fields.Typed(values), default);

        var check = await Sensitivity.CheckAsync(system, values, baseline, 0.05);

        // Площадь «около 80» меняет цену на 10%, догаданный балкон на 2%, а текст варьировать не с чем
        Assert.Equal(["area"], check.Sensitive.Select(field => field.Name));
        Assert.Equal(["balcony"], check.Robust.Select(field => field.Name));
    }
}
