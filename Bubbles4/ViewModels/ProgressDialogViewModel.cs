using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bubbles4.ViewModels;

public partial class ProgressDialogViewModel: ViewModelBase
{
    public bool Relative { get; set; }
    [ObservableProperty]private string _message;
    
    private double _progressValue;
    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            SetProperty(ref _progressValue, value);
            if (Relative) Message = $"Loading...{value}%";
            else
            {
                _count++;
                Message = $"{_count} directories loaded";
            }
        }
    }

    private int _count;
    IDialogService _dialogService;
    private Progress<double> _progress;
    public Progress<double> Progress => _progress;

    public ProgressDialogViewModel(IDialogService dialogService)
    {
        _dialogService=dialogService;
        _progress = new Progress<double>(OnProgressUpdated);
    }
    public void OnProgressUpdated(double progressValue)
    {
        if (Math.Abs(progressValue - (-1.0)) < 1e-6)
        {
            Close();
            return;
        }
        _ = Dispatcher.UIThread.InvokeAsync(() => ProgressValue=progressValue);
    }

    public Task<object?> Show()
    {
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (window != null)
        {
                try
                {
                     return _dialogService.ShowDialogAsync<object>(window, this);
                }
                catch (TaskCanceledException){return (Task<object?>)Task.CompletedTask;}
                catch (Exception ex){Console.WriteLine(ex);return (Task<object?>)Task.CompletedTask;}
                   
        }

        return (Task<object?>)Task.CompletedTask;
    }

    public void Close()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(null); // Return the result from ShowDialog
        }
    }

}