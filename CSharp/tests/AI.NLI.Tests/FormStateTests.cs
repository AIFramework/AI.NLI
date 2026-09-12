using System.Text.Json;

namespace AI.NLI.Tests;

public sealed class FormStateTests
{
    private static readonly FormField Area = new() { Name = "area", Type = FieldType.Number };

    [Fact]
    public void UserCorrectionReplacesValueAndIsJournaled()
    {
        var state = new FormState();
        state.Apply(Area, new FieldValue("54", ValueSource.User, "54 метра"));
        state.Apply(Area, new FieldValue("45", ValueSource.User, "не 54, а 45"));

        Assert.Equal("45", state.Values["area"].Value);
        Assert.Equal(["задано", "исправлено"], state.Journal.Select(entry => entry.Action));
    }

    [Fact]
    public void DocumentReplacesAssumptionButConflictsWithUser()
    {
        var state = new FormState();
        state.Apply(Area, new FieldValue("50", ValueSource.Default));
        state.Apply(Area, new FieldValue("54", ValueSource.Document, "площадь 54"));
        Assert.Equal(ValueSource.Document, state.Values["area"].Source);

        var user = new FormState();
        user.Apply(Area, new FieldValue("45", ValueSource.User, "45 метров"));
        user.Apply(Area, new FieldValue("54", ValueSource.Document, "площадь 54"));
        Assert.Equal("45", user.Values["area"].Value);
        Assert.Equal("54", Assert.Single(user.Conflicts).Offered.Value);

        user.Apply(Area, new FieldValue("54", ValueSource.User, "54"));
        Assert.Empty(user.Conflicts);
    }

    [Fact]
    public void AnswerAfterDontKnowClearsIt()
    {
        var state = new FormState();
        state.MarkUnknown("area");
        state.Apply(Area, new FieldValue("54", ValueSource.User, "54"));

        Assert.Empty(state.Unknown);
    }

    [Fact]
    public void SurvivesJsonRoundTrip()
    {
        var state = new FormState { System = "estimate" };
        state.Apply(Area, new FieldValue("50", ValueSource.User, "около 50") { Approximate = true });
        state.MarkUnknown("floor");
        state.Pending = [Question.For(Area, Question.NoData)];

        var restored = JsonSerializer.Deserialize<FormState>(JsonSerializer.Serialize(state))!;

        Assert.Equal(state.Values["area"], restored.Values["area"]);
        Assert.Contains("floor", restored.Unknown);
        Assert.Equal("area", Assert.Single(restored.Pending).Field);
        Assert.Equal(2, restored.Journal.Count);
    }
}
