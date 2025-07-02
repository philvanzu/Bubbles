using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class ProgressDialogView : Window
{
    public ProgressDialogView() : this(null) { }
    public ProgressDialogView(ProgressDialogViewModel? vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}