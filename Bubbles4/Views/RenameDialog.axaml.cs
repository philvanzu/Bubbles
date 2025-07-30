using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class RenameDialog : Window
{
    public RenameDialog( RenameDialogViewModel vm )
    {
        InitializeComponent();
        DataContext = vm;
    }
}