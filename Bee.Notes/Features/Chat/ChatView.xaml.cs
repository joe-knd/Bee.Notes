using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfNotes.Features.Chat;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ChatViewModel oldVm)
            oldVm.ScrollRequested -= ScrollToBottom;
        if (e.NewValue is ChatViewModel newVm)
            newVm.ScrollRequested += ScrollToBottom;
    }

    private void ScrollToBottom() => MessagesScroll.ScrollToEnd();

    private void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
        {
            vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChatViewModel vm && sender is PasswordBox pb)
            vm.RoomPassword = pb.Password;
    }
}
