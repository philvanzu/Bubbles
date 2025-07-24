using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Bubbles4.Services;
using Bubbles4.ViewModels;

namespace Bubbles4.Views;

public partial class MainView : UserControl
{
    private Panel _fullscreenOverlay;
    private Panel? _originalParent;
    private int _originalIndex;
    private IInputElement? _previousFocusedElement;
    
    
    
    public MainView()
    {
        InitializeComponent();

        InputManager.ImageViewer = ImageViewer;
        // Find your controls by name, assuming you have x:Name on them
        _fullscreenOverlay = FullscreenOverlay;
        if(ImageViewerContainer!= null) ImageViewerContainer.Focusable = true;

        // Remember original parent and index to restore later
        _originalParent = (Panel)ImageViewerContainer?.Parent!;
        _originalIndex = _originalParent.Children.IndexOf(ImageViewerContainer!);

        // Subscribe to DataContext changes to watch IsFullscreen property
        this.DataContextChanged += MainView_DataContextChanged;
        



        

        //ImageViewerContainer.DoubleTapped += ImgViewerDoubleTapped;
        if (ImageViewerContainer != null)
        {
            //ImageViewerContainer.KeyUp += InputManager.Instance.ImageControlKeyUp;
            ImageViewerContainer.PointerWheelChanged += ImageViewer.OnMouseWheel;
            ImageViewerContainer.PointerPressed += ImageViewer.OnPointerPressed;
            ImageViewerContainer.PointerReleased += ImageViewer.OnPointerReleased;
            ImageViewerContainer.PointerMoved += ImageViewer.OnPointerMoved;
            ImageViewerContainer.Pinched += ImageViewer.OnPinched;
        }

        
    }




    

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var window = this.GetVisualRoot() as Window;
        if (window != null)
        {
            window.AddHandler(InputElement.KeyUpEvent, OnGlobalKeyUp,  RoutingStrategies.Bubble, true);
        }
    }
    private void OnGlobalKeyUp(object? sender, KeyEventArgs e)
    {
        if (SearchBox.IsFocused 
            || GotoPageNumericUpDown.IsFocused
            || DataContext is not MainViewModel vm )
            return;
        InputManager.Instance.GlobalKeyUp(sender, e);

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
        if( _originalParent==null) return;
        if (fullscreen)
        {
            
            _previousFocusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            // Move ImageViewer to fullscreen overlay
            _originalParent.Children.Remove(ImageViewerContainer);
            _fullscreenOverlay.Children.Add(ImageViewerContainer);
            _fullscreenOverlay.IsVisible = true;
            //save reference to the control who currently has focus
            ImageViewerContainer.Focus();
        }
        else
        {
            // Move ImageViewer back to original parent at original position
            _fullscreenOverlay.Children.Remove(ImageViewerContainer);
            _originalParent.Children.Insert(_originalIndex, ImageViewerContainer);
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
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ImageViewer.Focus();
            e.Handled = true;
        }
    }

    private void GotoPage_KeyDown(object? sender, KeyEventArgs e){
        if (e.Key == Key.Enter && DataContext is MainViewModel vm)
        {
            vm.GotoPageCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ImageViewer.Focus();
            e.Handled = true;
        }

    }

}