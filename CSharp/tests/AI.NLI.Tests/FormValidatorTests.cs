namespace AI.NLI.Tests;

public sealed class FormValidatorTests
{
    private static readonly FormSchema Schema = FormSchema.FromYaml("""
        fields:
          - name: floor
            type: integer
          - name: floors
            type: integer
            required: false
          - name: parking
            type: boolean
          - name: parking_type
            type: choice
            choices: [подземный, наземный]
            when: { field: parking, is: [true] }
        rules:
          - check: floor <= floors
            message: Этаж выше этажности дома
        """);

    [Fact]
    public void FieldWithUnmetConditionIsNotRequired()
    {
        var values = Values(("floor", "5"), ("parking", "false"));

        Assert.Empty(FormValidator.Check(Schema, values));
        Assert.Equal("parking_type", Assert.Single(FormValidator.Check(Schema, Values(("floor", "5"), ("parking", "true")))).Field);
    }

    [Fact]
    public void RuleIsCheckedOnlyWhenBothSidesAreKnown()
    {
        Assert.Empty(FormValidator.Check(Schema, Values(("floor", "12"), ("parking", "false"))));

        var issue = Assert.Single(FormValidator.Check(Schema, Values(("floor", "12"), ("floors", "9"), ("parking", "false"))));
        Assert.Equal("Этаж выше этажности дома", issue.Problem);
    }

    private static Dictionary<string, FieldValue> Values(params (string Name, string Value)[] values) =>
        values.ToDictionary(value => value.Name, value => new FieldValue(value.Value, ValueSource.User));
}
