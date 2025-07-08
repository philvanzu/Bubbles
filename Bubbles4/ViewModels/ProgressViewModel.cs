using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bubbles4.ViewModels;

public partial class ProgressViewModel: ViewModelBase
{
    [ObservableProperty]private string _message = "";
    [ObservableProperty]private double _progressValue=0;
    [ObservableProperty]private bool _isIndeterminate=false;

    private Progress<(string msg, double progress, bool completed)> _progress;
    public Progress<(string msg, double progress, bool completed)> Progress => _progress;

    public ProgressViewModel()
    {
        _progress = new (OnProgressUpdated);
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

    public virtual void Close()
    {
        IsIndeterminate = false;
        Message = "";
        ProgressValue = 0;
    }

}