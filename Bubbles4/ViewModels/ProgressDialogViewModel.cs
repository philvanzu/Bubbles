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

// constructor must run on the ui thread for the progress object to be valid
public partial class ProgressDialogViewModel: ViewModelBase
{
    private readonly TaskCompletionSource _shownTcs = new TaskCompletionSource();
    public Task DialogShown => _shownTcs.Task;
    
    [ObservableProperty]private string _message = "";
    [ObservableProperty]private double _progressValue=0;
    [ObservableProperty]private bool _isIndeterminate=false;

    IDialogService _dialogService;
    private Progress<(string msg, double progress, bool completed)> _progress;
    public Progress<(string msg, double progress, bool completed)> Progress => _progress;
    

    public ProgressDialogViewModel(IDialogService dialogService)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw(new InvalidOperationException("ProgressDialogViewModel constructor Must be invoked on the UI thread."));
        }
        _dialogService=dialogService;
        _progress = new Progress<(string msg, double progress, bool completed)>(OnProgressUpdated);
    }
    public void OnProgressUpdated((string msg, double value, bool completed)progress)
    {
        if (progress.completed)
        {
            Close();
            return;
        }
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsIndeterminate = Math.Abs(progress.value - (-1.0)) < 0.01;
            Message = progress.msg;
            ProgressValue = IsIndeterminate?0:progress.value;
        });
    }

    public async Task Show()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw(new InvalidOperationException("ProgressDialogViewModel.Show Must be invoked on the UI thread."));
        }
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (window != null)
        {
            try
            {
                await _dialogService.ShowDialogAsync<object>(window, this);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        
    }

    public void Close()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(null); // Return the result from ShowDialog
        }
    }

    public void NotifyDialogShown()
    {
        _shownTcs.TrySetResult();
    }
}