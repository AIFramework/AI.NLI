using AI.NLI.Samples;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AI.NLI.Demo;

public partial class MainWindow : Window
{
    private const string SampleMessage = "Хочу оценить двушку в Казани, 54 квадрата.";

    private FormState _state = new();
    private FormDialog? _dialog;

    public MainWindow()
    {
        InitializeComponent();
        SchemaInput.Text = Apartment.SchemaYaml;
        MessageInput.Text = SampleMessage;
        ModelInput.Text = OpenRouter.DefaultModel;
    }

    private async void OnSend(object? sender, RoutedEventArgs e)
    {
        var message = MessageInput.Text ?? "";
        if (string.IsNullOrWhiteSpace(message))
            return;

        Log("Вы", message);
        MessageInput.Text = "";
        await TurnAsync(dialog => dialog.ReplyAsync(_state, message));
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        (_state, _dialog) = (new FormState(), null);
        LogText.Text = "";
        ChoicesPanel.Children.Clear();
        ShowState();
        StatusText.Text = "Новый диалог";
    }

    private async Task TurnAsync(Func<FormDialog, Task<DialogTurn>> step)
    {
        SendButton.IsEnabled = ChoicesPanel.IsEnabled = false;
        StatusText.Text = "Работаю...";
        try
        {
            _dialog ??= CreateDialog();
            var turn = await step(_dialog);
            Log("Система", turn.Text);
            ShowChoices(turn.Questions);
            StatusText.Text = turn.Kind switch
            {
                TurnKind.Questions => "Ответьте текстом или кнопкой",
                TurnKind.Incomplete => "Спрашивать больше нечего, но форма не готова",
                TurnKind.Result => "Вывод получен: можно спросить о нем или «а если бы…»",
                _ => "Ответ дан, форма не менялась"
            };
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            SendButton.IsEnabled = ChoicesPanel.IsEnabled = true;
            ShowState();
        }
    }

    private FormDialog CreateDialog()
    {
        var chat = OpenRouter.CreateClient(ModelInput.Text)
            ?? throw new InvalidOperationException("Ключ не найден: задайте OPENROUTER_API_KEY или положите key.txt в корень репозитория");
        var system = new ExpertSystem(FormSchema.FromYaml(SchemaInput.Text ?? ""), Apartment.EstimateAsync);
        var options = new DialogOptions { Extractor = new ExtractorOptions { VerifyEntailment = VerifyBox.IsChecked == true } };
        return new FormDialog(chat, [system], options: options);
    }

    private void ShowChoices(IReadOnlyList<Question> questions)
    {
        ChoicesPanel.Children.Clear();
        foreach (var question in questions)
            foreach (var choice in question.Choices)
            {
                var button = new Button { Content = $"{question.Field}: {choice}", Margin = new Thickness(0, 0, 6, 6) };
                button.Click += async (_, _) =>
                {
                    Log("Вы", $"{question.Field}: {choice}");
                    await TurnAsync(dialog => dialog.ChooseAsync(_state, question.Field, choice));
                };
                ChoicesPanel.Children.Add(button);
            }
    }

    private void ShowState()
    {
        var values = _state.Values.Select(pair =>
            $"{pair.Key} = {pair.Value.Value}{(pair.Value.Approximate ? " (примерно)" : "")}  [{pair.Value.SourceLabel}]" +
            (pair.Value.Evidence is null ? "" : $"  «{pair.Value.Evidence}»") +
            (pair.Value.Reference is null ? "" : $"  {pair.Value.Reference}"));
        var unknown = _state.Unknown.Select(name => $"{name}: не знаю");
        var conflicts = _state.Conflicts.Select(conflict => $"! {conflict.Field}: {conflict.Current.Value} или {conflict.Offered.Value}");
        StateText.Text = string.Join('\n', values.Concat(unknown).Concat(conflicts));
        JournalText.Text = string.Join('\n', _state.Journal.Select(entry =>
            $"{entry.At.ToLocalTime():HH:mm:ss} {entry.Field}: {entry.Action}{(entry.Value is null ? "" : $" {entry.Value.Value} [{entry.Value.SourceLabel}]")}"));
    }

    private void Log(string who, string text)
    {
        LogText.Text += $"{who}: {text}\n\n";
        LogText.CaretIndex = LogText.Text.Length;
    }
}
