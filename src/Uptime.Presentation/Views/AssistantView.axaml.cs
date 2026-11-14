using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class AssistantView : UserControl
{
    private AssistantViewModel? _subscribedVm;

    public AssistantView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.Messages.CollectionChanged -= OnMessagesChanged;
            _subscribedVm = null;
        }

        if (DataContext is AssistantViewModel vm)
        {
            vm.Messages.CollectionChanged += OnMessagesChanged;
            _subscribedVm = vm;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ChatScroll?.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (DataContext is AssistantViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                e.Handled = true;
                vm.SendMessageCommand.Execute(null);
            }
        }
    }
}
