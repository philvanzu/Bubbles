using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class RenameDialogViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "Rename Dialog";
    [ObservableProperty] private string _contentText = "Rename ?";
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _prefix = string.Empty;
    [ObservableProperty] private string _suffix = string.Empty;
    [ObservableProperty] private bool _showNewName = true;
    partial void OnShowNewNameChanged(bool value)
    {
        if (value) _showPrefixAndSuffix = false;
    }

    [ObservableProperty] private bool _showPrefixAndSuffix = false;
    partial void OnShowPrefixAndSuffixChanged(bool value)
    {
        if(value)_showNewName = false;
    }

    [RelayCommand]
    void OkPressed()
    {
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            if(ShowPrefixAndSuffix)
                window?.Close((Prefix, Suffix)); // Return the result from ShowDialog
            else if(ShowNewName)
                window?.Close(NewName); // Return the result from ShowDialog
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