using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class LibraryConfigWindow : Window
{
    
    public LibraryConfigWindow(LibraryConfigViewModel? vm)
    {
        InitializeComponent();
        this.DataContext = vm;

    }
    
}