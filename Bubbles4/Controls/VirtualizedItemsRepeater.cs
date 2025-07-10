using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using Bubbles4.Models;
using Bubbles4.ViewModels;

namespace Bubbles4.Controls;

public class VirtualizedItemsRepeater : ItemsRepeater
{
    public static readonly StyledProperty<ICommand?> ItemPreparedCommandProperty =
        AvaloniaProperty.Register<VirtualizedItemsRepeater, ICommand?>(nameof(ItemPreparedCommand));

    public static readonly StyledProperty<ICommand?> ItemClearingCommandProperty =
        AvaloniaProperty.Register<VirtualizedItemsRepeater, ICommand?>(nameof(ItemClearingCommand));
    
    public static readonly StyledProperty<ISelectableItem?> SelectedItemProperty =
        AvaloniaProperty.Register<VirtualizedItemsRepeater, ISelectableItem?>(nameof(SelectedItem));

    public ISelectableItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private Size? _elementSize = null;
    
    public ICommand? ItemPreparedCommand
    {
        get => GetValue(ItemPreparedCommandProperty);
        set => SetValue(ItemPreparedCommandProperty, value);
    }

    public ICommand? ItemClearingCommand
    {
        get => GetValue(ItemClearingCommandProperty);
        set => SetValue(ItemClearingCommandProperty, value);
    }

    /// <summary>
    /// Raised when an item is realized and bound.
    /// </summary>
    public event EventHandler<object?>? ItemPrepared;

    /// <summary>
    /// Raised when an item is unrealized.
    /// </summary>
    public event EventHandler<object?>? ItemCleared;

    private ISelectItems? _itemsSelector = null;
    public VirtualizedItemsRepeater()
    {
        this.
        ElementPrepared += OnElementPreparedInternal;
        ElementClearing += OnElementClearingInternal;
        DataContextChanged += OnDataContextChanged;
//        PointerWheelChanged += OnPointerWheelChanged;

    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (_elementSize is null)
            return;

        var scrollViewer = FindParentScrollViewer(this);
        if (scrollViewer is null)
            return;

        double scrollDelta = Math.Sign(e.Delta.Y) * _elementSize.Value.Height;

        var newOffsetY = scrollViewer.Offset.Y - scrollDelta;

        // Clamp to valid range
        newOffsetY = Math.Max(0, newOffsetY);
        // Optionally: you could calculate maximum scroll based on estimated total items count and item size

        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, newOffsetY);
        e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_itemsSelector != null)
        {
            _itemsSelector.SortOrderChanged -= OnDcSortOrderChanged;
            _itemsSelector.SelectionChanged -= OnDcSelectionChanged;
            _itemsSelector.ScrollToIndexRequested -= OnDcScrollToIndexRequested;
            _itemsSelector = null;
        }

        if (DataContext is ISelectItems selector)
        {
            _itemsSelector = selector;
            _itemsSelector.SortOrderChanged += OnDcSortOrderChanged;
            _itemsSelector.SelectionChanged += OnDcSelectionChanged;
            _itemsSelector.ScrollToIndexRequested += OnDcScrollToIndexRequested;
        }
        _elementSize = null;
    }

    private void OnDcScrollToIndexRequested(object? sender, int e)
    {
        if(_itemsSelector != null && e != -1)
            ScrollIntoView(e);
    }

    private void OnDcSelectionChanged(object? sender, SelectedItemChangedEventArgs e)
    {
        if (e.NewItem != null && e.Sender == _itemsSelector)
        {
            var idx = _itemsSelector.GetSelectedIndex();
            if(idx != -1) ScrollIntoView(idx);
        }
    }

    private void OnDcSortOrderChanged(object? sender, EventArgs e)
    {
        if (_itemsSelector != null)
        {
            var idx = _itemsSelector.GetSelectedIndex();
            if(idx != -1) ScrollIntoView(idx);
        }
    }

    private void OnElementPreparedInternal(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        
        if (_elementSize == null && e.Element.Width > 0 && e.Element.Height > 0)
            _elementSize = new Size(e.Element.Width, e.Element.Height);
        
        var context = e.Element?.DataContext;
        ItemPreparedCommand?.Execute(context);

        ItemPrepared?.Invoke(this, context);
    }

    private void OnElementClearingInternal(object? sender, ItemsRepeaterElementClearingEventArgs e)
    {
        var context = e.Element?.DataContext;
        ItemClearingCommand?.Execute(context);

        ItemCleared?.Invoke(this, context);
    }
    
    private void ScrollIntoView(int index)
    {
        var scrollViewer = FindParentScrollViewer(this);
        if (_elementSize == null || scrollViewer == null)
            return;
        
        double itemWidth = _elementSize.Value.Width;
        double itemHeight = _elementSize.Value.Height;
        
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

