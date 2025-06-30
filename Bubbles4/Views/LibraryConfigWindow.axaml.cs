using Avalonia.Controls;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class LibraryConfigWindow : Window
{
    public LibraryConfigWindow() : this(null) { }
    public LibraryConfigWindow(LibraryConfigViewModel? vm)
    {
        InitializeComponent();
        this.DataContext = vm;

    }
    
}