using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Bubbles4.Controls;
using Bubbles4.Models;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class MainView : UserControl
{
    private Panel? _fullscreenOverlay;
    private ContentControl? _imgViewerContainer;
    private Panel? _originalParent;
    private int _originalIndex;

    public MainView()
    {
        InitializeComponent();
        
        // Find your controls by name, assuming you have x:Name on them
        _fullscreenOverlay = this.FindControl<Panel>("FullscreenOverlay");
        _imgViewerContainer = this.FindControl<ContentControl>("ImageViewerContainer");
        var fastImageViewer = this.FindControl<FastImageViewer>("ImageViewer");

        // Remember original parent and index to restore later
        _originalParent = (Panel)_imgViewerContainer?.Parent!;
        _originalIndex = _originalParent.Children.IndexOf(_imgViewerContainer!);

        // Subscribe to DataContext changes to watch IsFullscreen property
        this.DataContextChanged += MainView_DataContextChanged;
        _imgViewerContainer!.DoubleTapped += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            { 
                vm.ToggleFullscreenCommand.Execute(null);
            }
        };
        _imgViewerContainer.PointerWheelChanged += (s, e) =>
        {
            if (DataContext is MainViewModel vm && vm.Config.ScrollAction == LibraryConfig.ScrollActions.TurnPage)
            {
                if (Math.Abs(e.Delta.Y - (-1.0)) < 0.01f) vm.Next();
                
                else if (Math.Abs(e.Delta.Y - 1.0) < 0.01f) vm.Previous();
            }
            else fastImageViewer!.OnScroll(s, e);
        };
        
        
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (DataContext is MainViewModel vm) vm.OnClose();
    }

    private void MainView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
        {
            Dispatcher.UIThread.Post(() =>
            {
                var vm = DataContext as MainViewModel;
                if (vm != null)
                    ToggleFullscreen(vm.IsFullscreen);
            });
        }
    }

    private void ToggleFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            // Move ImageViewer to fullscreen overlay
            _originalParent.Children.Remove(_imgViewerContainer);
            _fullscreenOverlay.Children.Add(_imgViewerContainer);
            _fullscreenOverlay.IsVisible = true;
        }
        else
        {
            // Move ImageViewer back to original parent at original position
            _fullscreenOverlay.Children.Remove(_imgViewerContainer);
            _originalParent.Children.Insert(_originalIndex, _imgViewerContainer);
            _fullscreenOverlay.IsVisible = false;
        }
    }
  
}