using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AI.NLI.Demo.ViewModels;

/// <summary>Кнопка-подсказка: готовый ответ, пример запроса, следующий вопрос.</summary>
public sealed record Chip(string Label, ICommand Command);

/// <summary>Реплика пользователя.</summary>
public sealed record UserBubble(string Text);

/// <summary>Сообщение системы; предупреждение выделяется.</summary>
public sealed record SystemBubble(string Text, bool IsWarning);

/// <summary>Приветствие с примерами запросов.</summary>
public sealed record WelcomeCard(IReadOnlyList<Chip> Examples);

/// <summary>Вопрос с готовыми ответами.</summary>
public sealed record QuestionItem(string Text, string Reason, IReadOnlyList<Chip> Choices)
{
    public bool HasReason => Reason.Length > 0;
}

/// <summary>Вопросы одного хода; после ответа карточка закрывается, и ее кнопки гаснут.</summary>
public sealed partial class QuestionCard(IReadOnlyList<QuestionItem> items) : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen = true;

    public IReadOnlyList<QuestionItem> Items { get; } = items;
}

/// <summary>Показатель вывода; главный крупнее.</summary>
public sealed record Metric(string Label, string Value, bool IsMain);

/// <summary>Оговорка к выводу.</summary>
public sealed record Note(string Text, bool IsWarning);

/// <summary>Вывод экспертной системы: показатели, объяснение, оговорки и следующие шаги.</summary>
public sealed record ResultCard(string Title, IReadOnlyList<Metric> Metrics, string Text, IReadOnlyList<Note> Notes, IReadOnlyList<Chip> Actions)
{
    public bool HasNotes => Notes.Count > 0;

    public bool HasActions => Actions.Count > 0;
}

/// <summary>Цветная метка источника или состояния поля.</summary>
public sealed record Badge(string Text, IBrush Background, IBrush Foreground);

/// <summary>Строка формы.</summary>
public sealed record FieldRow(string Title, string Value, Badge Badge, string? Evidence, string? Note)
{
    public bool HasEvidence => Evidence is not null;

    public bool HasNote => Note is not null;
}

/// <summary>Строка журнала.</summary>
public sealed record JournalRow(string Time, string Text, string Detail)
{
    public bool HasDetail => Detail.Length > 0;
}
