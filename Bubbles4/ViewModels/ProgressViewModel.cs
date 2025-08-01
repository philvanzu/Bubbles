using System;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.ViewModels;

public partial class ProgressViewModel: ViewModelBase
{
    [ObservableProperty]private string _message = "";
    [ObservableProperty]private double _progressValue=0;
    [ObservableProperty]private bool _isIndeterminate=false;
    [ObservableProperty]private bool _isBusy=false;
    private Progress<(string msg, double progress, bool completed)> _progress;
    public IProgress<(string msg, double progress, bool completed)> Progress => _progress;
    private CancellationTokenSource? _cancellationTokenSource;
    public CancellationTokenSource? CancellationTokenSource 
    { 
        get => _cancellationTokenSource;
        set
        {
            SetProperty(ref _cancellationTokenSource, value);
            OnPropertyChanged(nameof(CanPressCancel));
        } 
    }

    public ProgressViewModel()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            throw new InvalidOperationException("Progress constructor not on UI thread");
        _progress = new (OnProgressUpdated);
    }
    public void OnProgressUpdated((string msg, double value, bool completed)progress)
    {
        
        if (progress.completed)
        {
            Close();
            return;
        }
        if (!IsBusy) IsBusy = true;
        if (Dispatcher.UIThread.CheckAccess())
        {
            IsIndeterminate = Math.Abs(progress.value - (-1.0)) < 0.01;
            Message = progress.msg;
            ProgressValue = IsIndeterminate?0:progress.value;
        }
        else {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsIndeterminate = Math.Abs(progress.value - (-1.0)) < 0.01;
                Message = progress.msg;
                ProgressValue = IsIndeterminate?0:progress.value;
            }, DispatcherPriority.Background);
        }
    }

    [RelayCommand]
    private void CancelPressed()
    {
        if(CancellationTokenSource != null)
            CancellationTokenSource.Cancel();
    }
    public bool CanPressCancel =>CancellationTokenSource!=null;

    public virtual void Close()
    {
        IsIndeterminate = false;
        Message = "";
        ProgressValue = 0;
        IsBusy = false;
        CancellationTokenSource = null;
    }

}