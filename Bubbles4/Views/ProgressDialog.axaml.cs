using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class ProgressDialog : Window
{
    public ProgressDialog() : this(null) { }
    public ProgressDialog(ProgressDialogViewModel? vm)
    {
        InitializeComponent();
        DataContext = vm;
        this.Opened += ((sender, args) =>
        {
            if (DataContext is ProgressDialogViewModel vm)
                vm.NotifyDialogShown();
        });
    }
}