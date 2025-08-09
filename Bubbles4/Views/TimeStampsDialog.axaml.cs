using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class TimeStampsDialog : WindowBase
{
    public TimeStampsDialog(TimeStampsDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}