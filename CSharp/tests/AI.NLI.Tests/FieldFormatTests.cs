namespace AI.NLI.Tests;

public sealed class FieldFormatTests
{
    [Theory]
    [InlineData(FieldType.Number, "54,5", "54.5")]
    [InlineData(FieldType.Integer, "1 500", "1500")]
    [InlineData(FieldType.Boolean, "Да", "true")]
    [InlineData(FieldType.Boolean, "нет", "false")]
    [InlineData(FieldType.Date, "12.09.2026", "2026-09-12")]
    public void NormalizesToCanonicalFormat(FieldType type, string raw, string expected) =>
        Assert.Equal(expected, FieldFormat.Normalize(new FormField { Name = "x", Type = type }, raw));

    [Theory]
    [InlineData(FieldType.Number, "54.5", true)]
    [InlineData(FieldType.Number, "пятьдесят", false)]
    [InlineData(FieldType.Number, "5", false)]
    [InlineData(FieldType.Integer, "2", true)]
    [InlineData(FieldType.Integer, "2.5", false)]
    [InlineData(FieldType.Boolean, "true", true)]
    [InlineData(FieldType.Boolean, "может быть", false)]
    [InlineData(FieldType.Date, "2026-09-12", true)]
    [InlineData(FieldType.Date, "сентябрь", false)]
    public void ChecksTypeAndRange(FieldType type, string value, bool valid)
    {
        var field = new FormField { Name = "x", Type = type, Min = type == FieldType.Number ? 10 : null };

        Assert.Equal(valid, FieldFormat.Parse(field, value).Problem is null);
    }

    [Fact]
    public void ParsesTypedValues()
    {
        Assert.Equal(2L, FieldFormat.Parse(new FormField { Name = "x", Type = FieldType.Integer }, "2").Value);
        Assert.Equal(new DateOnly(2026, 9, 12), FieldFormat.Parse(new FormField { Name = "x", Type = FieldType.Date }, "2026-09-12").Value);
    }

    [Fact]
    public void MultiChoiceAcceptsOnlyListedItems()
    {
        var field = new FormField { Name = "outlets", Type = FieldType.Choice, Choices = ["сайт", "блог", "рассылка"], Multi = true };

        Assert.Equal(new object[] { "сайт", "блог" }, (List<object?>)FieldFormat.Parse(field, "Сайт; блог").Value!);
        Assert.NotNull(FieldFormat.Parse(field, "сайт; газета").Problem);
    }

    [Fact]
    public void TableRowsAreCheckedByColumns()
    {
        var field = new FormField
        {
            Name = "rooms",
            Type = FieldType.Table,
            Fields = [new FormField { Name = "name" }, new FormField { Name = "area", Type = FieldType.Number }]
        };

        var rows = (List<Dictionary<string, object?>>)FieldFormat.Parse(field, """[{"name":"кухня","area":"9,5"},{"name":"спальня","area":14}]""").Value!;
        Assert.Equal(9.5, rows[0]["area"]);
        Assert.Equal(14.0, rows[1]["area"]);
        Assert.Equal("area: не заполнено", FieldFormat.Parse(field, """[{"name":"кухня"}]""").Problem);
        Assert.NotNull(FieldFormat.Parse(field, "кухня 9 метров").Problem);
    }

    [Fact]
    public void ApproximateNumbersMatchWithinTenPercent()
    {
        var field = new FormField { Name = "area", Type = FieldType.Number };
        var approximate = new FieldValue("50", ValueSource.User) { Approximate = true };

        Assert.True(FieldFormat.Same(field, approximate, new FieldValue("54", ValueSource.Document)));
        Assert.False(FieldFormat.Same(field, new FieldValue("50", ValueSource.User), new FieldValue("54", ValueSource.Document)));
    }
}
