namespace AI.NLI.Tests;

public sealed class FormSchemaTests
{
    [Fact]
    public void ReadsFieldsOutputsRulesAndConditions()
    {
        var schema = FormSchema.FromYaml("""
            name: estimate
            description: Оценка квартиры
            fields:
              - name: area
                type: number
                unit: м²
                min: 10
              - name: parking
                type: boolean
                missing: skip
              - name: parking_type
                type: choice
                choices: [подземный, наземный]
                when: { field: parking, is: [true] }
            outputs:
              - name: price
                type: number
            rules:
              - check: area <= 1000
                message: Слишком большая площадь
            """);

        Assert.Equal("estimate", schema.Name);
        Assert.Equal(FieldType.Number, schema.Fields[0].Type);
        Assert.Equal(10, schema.Fields[0].Min);
        Assert.Equal(MissingPolicy.Skip, schema.Fields[1].Missing);
        Assert.Equal("parking", schema.Fields[2].When!.Field);
        Assert.Equal(["true"], schema.Fields[2].When!.Is);
        Assert.Equal("price", Assert.Single(schema.Outputs).Name);
        Assert.Equal("Слишком большая площадь", Assert.Single(schema.Rules).Message);
    }

    [Theory]
    [InlineData("fields: [{ name: area }, { name: area }]")]
    [InlineData("fields: [{ name: a, when: { field: b, is: [x] } }]")]
    [InlineData("fields: [{ name: rows, type: table }]")]
    [InlineData("fields: [{ name: a }]\nrules: [{ check: a is big }]")]
    public void RejectsContradictorySchema(string yaml) =>
        Assert.Throws<InvalidDataException>(() => FormSchema.FromYaml(yaml));

    [Fact]
    public void ImportsJsonSchema()
    {
        var schema = FormSchema.FromJsonSchema("""
            {
              "title": "estimate", "type": "object", "required": ["city"],
              "properties": {
                "city": { "type": "string", "description": "Город" },
                "built": { "type": "string", "format": "date" },
                "area": { "type": "number", "minimum": 10, "default": 50 },
                "condition": { "type": "string", "enum": ["без ремонта", "евроремонт"] },
                "outlets": { "type": "array", "items": { "type": "string", "enum": ["сайт", "блог"] } },
                "rooms": { "type": "array", "items": { "type": "object", "required": ["area"], "properties": { "area": { "type": "number" } } } }
              }
            }
            """);

        var fields = schema.Fields.ToDictionary(field => field.Name);
        Assert.Equal("estimate", schema.Name);
        Assert.True(fields["city"].Required);
        Assert.Equal("Город", fields["city"].Description);
        Assert.False(fields["area"].Required);
        Assert.Equal(FieldType.Date, fields["built"].Type);
        Assert.Equal((10.0, "50"), (fields["area"].Min!.Value, fields["area"].Default));
        Assert.Equal(FieldType.Choice, fields["condition"].Type);
        Assert.True(fields["outlets"].Multi);
        Assert.Equal(["сайт", "блог"], fields["outlets"].Choices);
        Assert.Equal(FieldType.Table, fields["rooms"].Type);
        Assert.True(Assert.Single(fields["rooms"].Fields).Required);
    }
}
