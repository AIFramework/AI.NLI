using System.Text.Json;

namespace AI.NLI.Tests;

public sealed class FormExtractorTests
{
    private static readonly FormField[] Fields =
    [
        new() { Name = "city" },
        new() { Name = "area", Type = FieldType.Number },
        new() { Name = "floor", Type = FieldType.Integer }
    ];

    [Fact]
    public async Task AcceptsQuoteInOtherCaseAndRejectsQuoteMissingFromText()
    {
        var chat = new FakeChat(_ => FakeChat.Values(("city", "Казань", "в Казань"), ("area", "54", "54 кв.м")));

        var found = await new FormExtractor(chat).ExtractAsync(Fields, new SourceText("Квартира в Казани, 54 квадрата"), ValueSource.User);

        Assert.Equal("Казань", found.Values["city"].Value);
        Assert.DoesNotContain("area", found.Values.Keys);
    }

    [Fact]
    public async Task OnlyUserCanSayDontKnow()
    {
        var chat = new FakeChat(_ => FakeChat.Unknown("floor", "не знаю, какой этаж"));
        var text = new SourceText("Не знаю, какой этаж");

        Assert.Contains("floor", (await new FormExtractor(chat).ExtractAsync(Fields, text, ValueSource.User)).Unknown);
        Assert.Empty((await new FormExtractor(chat).ExtractAsync(Fields, text, ValueSource.Document)).Unknown);
    }

    [Fact]
    public async Task LongTextIsReadByChunksWithReference()
    {
        var text = new string('а', 90) + " этаж 5";
        var chat = new FakeChat(prompt => prompt.Contains("этаж 5") ? FakeChat.Values(("floor", "5", "этаж 5")) : FakeChat.Values());

        var found = await new FormExtractor(chat, new ExtractorOptions { ChunkSize = 40 })
            .ExtractAsync(Fields, new SourceText(text, "договор.pdf"), ValueSource.Document);

        Assert.True(chat.Prompts.Count > 1);
        Assert.StartsWith("договор.pdf, символы ", found.Values["floor"].Reference);
    }

    [Fact]
    public async Task EntailmentCheckDropsValueNotFollowingFromQuote()
    {
        var chat = new FakeChat(FakeChat.Route(
            ("следует ли значение", _ => JsonSerializer.Serialize(new { items = new[] { new { name = "floor", entailed = false } } })),
            ("Ты заполняешь", _ => FakeChat.Values(("city", "Казань", "в Казани"), ("floor", "5", "5 минут до метро")))));

        var found = await new FormExtractor(chat, new ExtractorOptions { VerifyEntailment = true })
            .ExtractAsync(Fields, new SourceText("Квартира в Казани, 5 минут до метро"), ValueSource.User);

        Assert.Equal(["city"], found.Values.Keys);
    }

    [Fact]
    public async Task ApproximateValueGoesToEntailmentCheckMarked()
    {
        // Без пометки проверка видит «area = 80» против «примерно 80» и отбрасывает честное приблизительное значение
        var chat = new FakeChat(FakeChat.Route(("Ты заполняешь", _ => FakeChat.Approximate("area", "80", "Примерно 80 квадратов"))));

        var found = await new FormExtractor(chat).ExtractAsync(Fields, new SourceText("Примерно 80 квадратов"), ValueSource.User);

        Assert.True(found.Values["area"].Approximate);
        Assert.Contains("area () = 80 (приблизительно); цитата: «Примерно 80 квадратов»", chat.Prompts[^1]);
    }

    [Fact]
    public async Task FallsBackWhenModelRejectsJsonSchema()
    {
        var chat = new FakeChat(_ => FakeChat.Values(("city", "Казань", "в Казани"))) { RejectSchema = true };

        var found = await new FormExtractor(chat).ExtractAsync(Fields, new SourceText("Квартира в Казани"), ValueSource.User);

        Assert.Equal("Казань", found.Values["city"].Value);
    }
}
