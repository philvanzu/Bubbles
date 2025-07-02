using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class PreferencesEditorView : Window
{
    public PreferencesEditorView() : this(null) { }
    public PreferencesEditorView(PreferencesEditorViewModel? vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}