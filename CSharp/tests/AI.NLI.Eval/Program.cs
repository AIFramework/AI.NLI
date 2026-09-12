using System.Text;
using AI.NLI;
using AI.NLI.Demo;
using AI.NLI.Eval;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// Замер качества заполнения на живой модели. Модель: переменная NLI_MODEL или модель по умолчанию.
// Выход 0: пороги пройдены, 1: нет, 2: нет ключа.
const int MaxSteps = 10;
const double MinPrecision = 0.9;
const double MinRecall = 0.8;
const double MaxFabricated = 0.05;

Console.OutputEncoding = Encoding.UTF8;
var chat = OpenRouter.CreateClient(Environment.GetEnvironmentVariable("NLI_MODEL"));
if (chat is null)
{
    Console.Error.WriteLine("Ключ не найден: задайте OPENROUTER_API_KEY или положите key.txt в корень репозитория");
    return 2;
}

var system = TaganrogRegression.System(FormSchema.FromYaml(TaganrogRegression.SchemaYaml));
var cases = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
    .Deserialize<EvalCase[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cases.yaml")));
var scores = new List<EvalScore>();
foreach (var eval in cases)
{
    var score = await RunAsync(eval);
    scores.Add(score);
    Console.WriteLine($"{score.Case,-28} верно {score.Correct}/{score.Expected}, неверно {score.Wrong}, выдумано {score.Fabricated}, " +
                      $"вопросов {score.Questions}{(score.AssumptionMattered ? ", допущение влияло на вывод" : "")}");
    foreach (var miss in score.Misses)
        Console.WriteLine($"    {miss}");
}

var filled = scores.Sum(score => score.Correct + score.Wrong + score.Fabricated);
var precision = filled == 0 ? 0 : (double)scores.Sum(score => score.Correct) / filled;
var recall = (double)scores.Sum(score => score.Correct) / scores.Sum(score => score.Expected);
var fabricated = filled == 0 ? 0 : (double)scores.Sum(score => score.Fabricated) / filled;
Console.WriteLine();
Console.WriteLine($"Точность {precision:P0} (порог {MinPrecision:P0}), полнота {recall:P0} (порог {MinRecall:P0}), " +
                  $"выдумано {fabricated:P0} (порог {MaxFabricated:P0})");
Console.WriteLine($"Вопросов на диалог {scores.Average(score => score.Questions):F1}, " +
                  $"вывод зависел от допущения в {scores.Count(score => score.AssumptionMattered)} из {scores.Count}");

return precision >= MinPrecision && recall >= MinRecall && fabricated <= MaxFabricated ? 0 : 1;

async Task<EvalScore> RunAsync(EvalCase eval)
{
    var state = new FormState();
    var dialog = new FormDialog(chat, [system]);
    var (questions, mattered) = (0, false);

    var turn = await dialog.ReplyAsync(state, eval.Turns[0]);
    for (var step = 1; turn.Kind == TurnKind.Questions && step < MaxSteps; step++)
    {
        questions += turn.Questions.Count;
        mattered |= turn.Questions.Any(question => question.Reason == Question.AffectsResult);
        turn = step < eval.Turns.Length
            ? await dialog.ReplyAsync(state, eval.Turns[step])
            : await dialog.ChooseAsync(state, turn.Questions[0].Field, Question.DontKnow);
    }

    return EvalScore.Of(eval, system.Schema, state, questions, mattered);
}
