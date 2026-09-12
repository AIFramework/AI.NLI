using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI.NLI.Demo.ViewModels;

/// <summary>Окно диалога: лента, форма с происхождением значений и журнал.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private static readonly string[] Examples =
    [
        "Хочу оценить двушку в Таганроге, 54 квадрата",
        "Евроремонт, 72 м², 3 комнаты, 7 этаж из 9, есть балкон",
        "Около 80 метров, этаж выше пятого, дом 1970 года"
    ];

    private static readonly string[] FollowUps =
    [
        "Почему такая оценка?",
        "Что сильнее всего влияет на цену?",
        "А если бы площадь была на 10 метров больше?"
    ];

    private FormState _state = new();
    private FormDialog? _dialog;
    private FormSchema _schema = FormSchema.FromYaml(TaganrogRegression.SchemaYaml);
    private QuestionCard? _openCard;

    [ObservableProperty]
    private string _message = Examples[0];

    [ObservableProperty]
    private string _model = OpenRouter.DefaultModel;

    [ObservableProperty]
    private string _schemaYaml = TaganrogRegression.SchemaYaml;

    [ObservableProperty]
    private bool _verify = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Опишите квартиру своими словами";

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private double _progress;

    public MainViewModel()
    {
        Messages.Add(Welcome());
        Refresh();
    }

    public ObservableCollection<object> Messages { get; } = [];

    public ObservableCollection<FieldRow> Fields { get; } = [];

    public ObservableCollection<JournalRow> Journal { get; } = [];

    public bool IsIdle => !IsBusy;

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task SendAsync()
    {
        var text = Message.Trim();
        if (text.Length == 0)
            return;

        Message = "";
        await SayAsync(text);
    }

    [RelayCommand]
    private void Reset()
    {
        try
        {
            _schema = FormSchema.FromYaml(SchemaYaml);
        }
        catch (Exception ex)
        {
            Status = $"Описание не разобрано: {ex.Message}";
            return;
        }

        (_state, _dialog, _openCard) = (new FormState(), null, null);
        Messages.Clear();
        Messages.Add(Welcome());
        Refresh();
        Status = "Новый диалог";
    }

    private async Task SayAsync(string text)
    {
        if (IsBusy)
            return;
        Messages.Add(new UserBubble(text));
        await TurnAsync(dialog => dialog.ReplyAsync(_state, text));
    }

    private async Task ChooseAsync(string field, string choice)
    {
        if (IsBusy)
            return;
        Messages.Add(new UserBubble(Present.Choice(choice)));
        await TurnAsync(dialog => dialog.ChooseAsync(_state, field, choice));
    }

    private async Task TurnAsync(Func<FormDialog, Task<DialogTurn>> step)
    {
        if (_openCard is not null)
            _openCard.IsOpen = false;
        IsBusy = true;
        Status = "Разбираю ответ...";
        try
        {
            _dialog ??= CreateDialog();
            Show(await step(_dialog));
        }
        catch (Exception ex)
        {
            Messages.Add(new SystemBubble(ex.Message, true));
            Status = "Не получилось, попробуйте еще раз";
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    private FormDialog CreateDialog()
    {
        var chat = OpenRouter.CreateClient(Model)
            ?? throw new InvalidOperationException("Ключ OpenRouter не найден: задайте OPENROUTER_API_KEY или положите key.txt в корень репозитория");
        var options = new DialogOptions { Extractor = new ExtractorOptions { VerifyEntailment = Verify } };
        return new FormDialog(chat, [TaganrogRegression.System(_schema)], options: options);
    }

    private void Show(DialogTurn turn)
    {
        switch (turn)
        {
            case { Kind: TurnKind.Questions }:
                _openCard = new QuestionCard(turn.Questions.Select(Item).ToList());
                Messages.Add(_openCard);
                Status = "Ответьте своими словами или выберите вариант";
                break;
            case { Output: not null }:
                var isResult = turn.Kind == TurnKind.Result;
                Messages.Add(new ResultCard(isResult ? "ОЦЕНКА" : "ЕСЛИ БЫ", Metrics(turn.Output), turn.Text, Notes(turn.Check),
                    isResult ? FollowUps.Select(Ask).ToList() : []));
                Status = isResult ? "Можно спросить об оценке или «а если бы…»" : "Это прикидка: форма не менялась";
                break;
            case { Kind: TurnKind.Incomplete }:
                Messages.Add(new SystemBubble("Без этого оценку не посчитать:\n" +
                    string.Join('\n', turn.Issues.Select(issue => $"• {FieldTitle(issue.Field)}: {issue.Problem}")), true));
                Status = "Не хватает данных";
                break;
            default:
                Messages.Add(new SystemBubble(turn.Text, false));
                Status = "Готово";
                break;
        }
    }

    private QuestionItem Item(Question question) => new(question.Text, Present.Reason(question.Reason),
        question.Choices.Select(choice => new Chip(Present.Choice(choice), new AsyncRelayCommand(() => ChooseAsync(question.Field, choice)))).ToList());

    private Chip Ask(string text) => new(text, new AsyncRelayCommand(() => SayAsync(text)));

    private WelcomeCard Welcome() => new(Examples.Select(Ask).ToList());

    private List<Metric> Metrics(IReadOnlyDictionary<string, object?> output) => output
        .Select((pair, index) => new Metric(_schema.Outputs.Named(pair.Key) is { } field ? Present.Title(field) : pair.Key, Present.Number(pair.Value), index == 0))
        .ToList();

    private List<Note> Notes(AssumptionCheck? check) => check is null ? [] : check.Robust
        .Select(field => new Note($"✓ {Assumed(field)}: на оценку почти не влияет", false))
        .Concat(check.Sensitive.Select(field => new Note($"! {Assumed(field)}: влияет на оценку, а уточнить не удалось", true)))
        .ToList();

    private string Assumed(FormField field) =>
        $"{Present.Title(field)} = {Present.Value(field, _state.Values[field.Name])} ({_state.Values[field.Name].SourceLabel})";

    private string FieldTitle(string name) => _schema.Fields.Named(name) is { } field ? Present.Title(field) : name;

    private void Refresh()
    {
        var active = _schema.Fields.Active(_state.Values).ToList();
        Fields.Clear();
        foreach (var field in active)
            Fields.Add(Present.Row(field, _state));

        var required = active.Where(field => field.Required).ToList();
        var filled = required.Count(field => _state.Values.ContainsKey(field.Name));
        Progress = required.Count == 0 ? 100 : 100.0 * filled / required.Count;
        ProgressText = $"Заполнено {filled} из {required.Count} обязательных полей";

        Journal.Clear();
        foreach (var entry in Enumerable.Reverse(_state.Journal))
            Journal.Add(new JournalRow(entry.At.ToLocalTime().ToString("HH:mm:ss"), $"{FieldTitle(entry.Field)}: {entry.Action}",
                entry.Value is null ? "" : $"{entry.Value.Value} · {entry.Value.SourceLabel}"));
    }
}
