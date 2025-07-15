using System.Linq;
using Avalonia;
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
    public string OkText { get; set; } = "OK";
    public string CancelText { get; set; } = "Cancel";
    public string Title { get; set; }="Ok Cancel Dialog"; 
    [RelayCommand]
    public void OnOkPressed()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(true); // Return the result from ShowDialog
        }
    }
    
    [RelayCommand]
    public void OnCancelPressed()
    {
        // Close the window with a result
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(false); // Return the result from ShowDialog
        }

    }

}