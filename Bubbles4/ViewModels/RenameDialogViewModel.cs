using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class RenameDialogViewModel:ViewModelBase
{
    [ObservableProperty] private string _prefix = string.Empty;
    [ObservableProperty] private string _suffix = string.Empty;

    [RelayCommand]
    void OkPressed()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close((Prefix, Suffix)); // Return the result from ShowDialog
        }
    }

    [RelayCommand]
    void CancelPressed()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(null); // Return the result from ShowDialog
        }
    }
}