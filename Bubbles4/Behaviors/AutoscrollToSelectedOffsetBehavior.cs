using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Bubbles4.ViewModels;
using Bubbles4.Controls;
    
namespace Bubbles4.Behaviors;

public class AutoscrollToSelectedOffsetBehavior : Behavior<VirtualizedItemsRepeater>
{
    public static readonly StyledProperty<int> HeightProperty =
        AvaloniaProperty.Register<AutoscrollToSelectedOffsetBehavior, int>(nameof(Height));
    public int Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }
    
    public static readonly StyledProperty<int> WidthProperty =
        AvaloniaProperty.Register<AutoscrollToSelectedOffsetBehavior, int>(nameof(Width));
    public int Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }
    
    private ViewModelBase? _viewModel;
    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            // Observe DataContext changes explicitly
            AssociatedObject.GetObservable(Control.DataContextProperty)
                .Subscribe(dc => TryHookIntoViewModel(dc));

            // Call once immediately in case DataContext is already set
            TryHookIntoViewModel(AssociatedObject.DataContext);
        }
    }
    private void TryHookIntoViewModel(object? dc)
    {
        if (_viewModel is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            
        _viewModel = dc as ViewModelBase;

        if (_viewModel is INotifyPropertyChanged newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.SelectedItem))
        {
            if (_viewModel is LibraryViewModel libvm)
            {
                int idx = libvm.GetBookIndex(libvm.SelectedItem) ;
                if(idx != -1)
                    ScrollIntoView(idx);
            }
            
        }
        else if (e.PropertyName == nameof(BookViewModel.SelectedPage))
        {
            if (_viewModel is BookViewModel bookvm)
            {
                int idx = bookvm.GetPageIndex(bookvm.SelectedPage) ;
                if(idx != -1)
                    ScrollIntoView(idx);
            }
            
        }
    }


    private void ScrollIntoView(int index)
    {
        double itemWidth = Width;
        double itemHeight = Height;

        var repeater = AssociatedObject;
        var scrollViewer = FindParentScrollViewer(repeater);
        if (repeater == null || scrollViewer == null)
            return;

        var viewport = scrollViewer.Viewport;
        int columns = Math.Max(1, (int)(viewport.Width / itemWidth));
        int rows = Math.Max(1, (int)(viewport.Height / itemHeight));
        int row = index / columns;

        double itemTop = row * itemHeight;
        double itemBottom = itemTop + itemHeight;

        double offsetY = scrollViewer.Offset.Y;
        double viewportHeight = viewport.Height;

        if (itemBottom > offsetY + viewportHeight)
        {
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, itemBottom - viewportHeight);
        }
        else if (itemTop < offsetY)
        {
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, itemTop);
        }
    }

    private ScrollViewer? FindParentScrollViewer(Control? element)
    {
        while (element != null && element is not ScrollViewer)
        {
            element = element.Parent as Control;
        }
        return element as ScrollViewer;
    }
}