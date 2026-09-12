using System.ComponentModel;
using AI.NLI.Demo.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AI.NLI.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var model = new MainViewModel();
        DataContext = model;

        // Лента всегда показывает последнее: новое сообщение и индикатор работы
        model.Messages.CollectionChanged += (_, _) => ScrollToEnd();
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBusy))
                ScrollToEnd();
        };
        Opened += (_, _) => Composer.Focus();
    }

    private void ScrollToEnd() => Dispatcher.UIThread.Post(() => ChatScroll.ScrollToEnd(), DispatcherPriority.Background);
}
