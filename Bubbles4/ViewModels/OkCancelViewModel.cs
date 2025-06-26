using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class OkCancelViewModel: ViewModelBase
{
    private string? _content;
    public string? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    [RelayCommand]
    public void OnOkPressed()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            (window as Window)?.Close(true); // Return the result from ShowDialog
        }
    }
    
    [RelayCommand]
    public void OnCancelPressed()
    {
        // Close the window with a result
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            (window as Window)?.Close(false); // Return the result from ShowDialog
        }

    }

}