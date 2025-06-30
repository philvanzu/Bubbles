using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private IInputElement? _previousFocusedElement;

    public MainView()
    {
        InitializeComponent();
        
        // Find your controls by name, assuming you have x:Name on them
        _fullscreenOverlay = this.FindControl<Panel>("FullscreenOverlay");
        _imgViewerContainer = this.FindControl<ContentControl>("ImageViewerContainer");
        if(_imgViewerContainer!= null) _imgViewerContainer.Focusable = true;
        var fastImageViewer = this.FindControl<FastImageViewer>("ImageViewer");

        // Remember original parent and index to restore later
        _originalParent = (Panel)_imgViewerContainer?.Parent!;
        _originalIndex = _originalParent.Children.IndexOf(_imgViewerContainer!);

        // Subscribe to DataContext changes to watch IsFullscreen property
        this.DataContextChanged += MainView_DataContextChanged;
        _imgViewerContainer!.DoubleTapped += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            { 
                vm.ToggleFullscreenCommand.Execute(null);
            }
        };
        _imgViewerContainer.PointerWheelChanged += (s, e) =>
        {
            if (DataContext is MainViewModel vm &&
                vm.Config != null &&
                ( vm.Config.ScrollAction == LibraryConfig.ScrollActions.TurnPage ||
                  vm.IsFullscreen == false) )
            {
                if (Math.Abs(e.Delta.Y - (-1.0)) < 0.01f) _ = vm.Next();
                
                else if (Math.Abs(e.Delta.Y - 1.0) < 0.01f) _ = vm.Previous(); 
            }
            else fastImageViewer!.OnMouseWheel(s, e);
        };
        _imgViewerContainer.PointerPressed += (s, e) =>
        {
            if (fastImageViewer != null)
                fastImageViewer.OnPointerPressed(s, e);
        };
        _imgViewerContainer.PointerReleased += (s, e) =>
        {
            if (fastImageViewer != null)
                fastImageViewer.OnPointerReleased(s, e);
        };
        _imgViewerContainer.PointerMoved += (s, e) =>
        {
            if (fastImageViewer != null)
            {
                fastImageViewer.OnPointerMoved(s, e);
            }
        };
        
        _imgViewerContainer.KeyUp += (_, e) =>
        {
            if (fastImageViewer != null)
            {
                switch (e.Key)
                {
                    case Key.H:
                        e.Handled = true;
                        fastImageViewer.FitHeight();
                        break;
                    case  Key.W:
                        e.Handled = true;
                        fastImageViewer.FitWidth();
                        break;
                    case Key.B:
                        e.Handled = true;
                        fastImageViewer.Fit();
                        break;
                    case Key.F:
                        e.Handled = true;
                        fastImageViewer.FitStock();
                        break;
                    case Key.Down :
                        e.Handled = true;
                        fastImageViewer.OnDownArrowPressed();
                        break;
                    case Key.Up :
                        e.Handled = true;
                        fastImageViewer.OnUpArrowPressed();
                        break;
                    case Key.Add:
                        e.Handled = true;
                        fastImageViewer.Zoom(1);
                        break;
                    case Key.Subtract:
                        e.Handled = true;
                        fastImageViewer.Zoom(-1);
                        break;
                }
            }
        };
        /*
         relocation of
           <KeyBinding Gesture="PageDown" Command="{Binding NextBookCommand}" />
           <KeyBinding Gesture="PageUp" Command="{Binding PreviousBookCommand}" />
           <KeyBinding Gesture="Escape" Command="{Binding ExitFullScreenCommand}" />
           <KeyBinding Gesture="Space" Command="{Binding NextCommand}" />
           <KeyBinding Gesture="Alt+Space" Command="{Binding PreviousCommand}" />
           <KeyBinding Gesture="Right" Command="{Binding NextCommand}" />
           <KeyBinding Gesture="Back" Command="{Binding PreviousCommand}" />
           <KeyBinding Gesture="Left" Command="{Binding PreviousCommand}" />
         */
        KeyUp += ((sender, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                switch (e.Key)
                {
                    case Key.PageDown :
                        e.Handled = true;
                        vm.NextBookCommand.Execute(null);
                        break;
                    case Key.PageUp :
                        e.Handled = true;
                        vm.PreviousBookCommand.Execute(null);
                        break;
                    case Key.Home:
                        e.Handled = true;
                        vm.FirstPageCommand.Execute(null);
                        break;
                    case Key.End:
                        vm.LastPageCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Space:
                    case Key.Right:
                        if(e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                            vm.PreviousCommand.Execute(null);
                        else vm.NextCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Left:
                    case Key.Back:
                        vm.PreviousCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        if(vm.IsFullscreen)
                            vm.ToggleFullscreenCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
        });

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
        if(_fullscreenOverlay==null || _imgViewerContainer == null || _originalParent==null) return;
        if (fullscreen)
        {
            _previousFocusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            // Move ImageViewer to fullscreen overlay
            _originalParent.Children.Remove(_imgViewerContainer);
            _fullscreenOverlay.Children.Add(_imgViewerContainer);
            _fullscreenOverlay.IsVisible = true;
            //save reference to the control who currently has focus
            _imgViewerContainer.Focus();
        }
        else
        {
            // Move ImageViewer back to original parent at original position
            _fullscreenOverlay.Children.Remove(_imgViewerContainer);
            _originalParent.Children.Insert(_originalIndex, _imgViewerContainer);
            _fullscreenOverlay.IsVisible = false;
            //restore focus to saved control
            _previousFocusedElement?.Focus();
        }
    }
    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchButton.Command?.Execute(SearchButton.CommandParameter);
        }
    }
}