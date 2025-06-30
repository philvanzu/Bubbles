using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class OkCancelWindow : Window
{
    public OkCancelWindow() : this(null) { }
    public OkCancelWindow(OkCancelViewModel? vm)
    {
        InitializeComponent();
        this.DataContext = vm;
    }
}