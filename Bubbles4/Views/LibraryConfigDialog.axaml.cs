using Avalonia.Controls;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class LibraryConfigDialog : WindowBase
{
    public LibraryConfigDialog() : this(null) { }
    public LibraryConfigDialog(LibraryConfigViewModel? vm)
    {
        InitializeComponent();
        this.DataContext = vm;

    }
    
}