using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    public static readonly StyledProperty<int?> ElementWidthProperty =
        AvaloniaProperty.Register<VirtualizedItemsRepeater, int?>(nameof(ElementWidth));
    
    public static readonly StyledProperty<int?> ElementHeightProperty =
        AvaloniaProperty.Register<VirtualizedItemsRepeater, int?>(nameof(ElementHeight));

    public int? ElementWidth
    {
        get => GetValue(ElementWidthProperty);
        set => SetValue(ElementWidthProperty, value);
    }

    public int? ElementHeight
    {
        get => GetValue(ElementHeightProperty);
        set => SetValue(ElementHeightProperty, value);
    }

    public ISelectableItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

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

    private ISelectItems? _itemsSelector;
    public VirtualizedItemsRepeater()
    {
        this.
        ElementPrepared += OnElementPreparedInternal;
        ElementClearing += OnElementClearingInternal;
        DataContextChanged += OnDataContextChanged;
//        PointerWheelChanged += OnPointerWheelChanged;

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
        
        var context = e.Element.DataContext;
        
        //__item0__issue__hack
        if (context is BookViewModel book && book.IsFirstBook)
        {
            int row  = ViewportRow();
            if (row > 3) return; //actual hack
            Console.WriteLine($"first book prepared : Viewport at row {row}");
        }

        if (context is PageViewModel page && page.IsFirstPage)
        {
            int row  = ViewportRow();
            if (row > 2) return;//actual hack
            Console.WriteLine($"first page prepared : Viewport at row {row}");
        }    

        ItemPreparedCommand?.Execute(context);

        ItemPrepared?.Invoke(this, context);
    }

    private void OnElementClearingInternal(object? sender, ItemsRepeaterElementClearingEventArgs e)
    {
        var context = e.Element.DataContext;
        
        //__item0__issue__hack
        if (context is BookViewModel book && book.IsFirstBook)
        {
            int row  = ViewportRow();
            if (row < 4) return;//actual hack
            Console.WriteLine($"first book clearing : Viewport at row {row}");
        }
        if (context is PageViewModel page && page.IsFirstPage)
        {
            int row  = ViewportRow();
            if (row < 3) return;//actual hack
            Console.WriteLine($"first page clearing : Viewport at row {row}");
        }    
        
        ItemClearingCommand?.Execute(context);
        ItemCleared?.Invoke(this, context);
    }

    private int ViewportRow()
    {
        var scrollViewer = FindParentScrollViewer(this);
        if (ElementHeight == null || ElementWidth == null || scrollViewer == null)
            return -1;
        
        double itemHeight = (double)ElementHeight;
        return (int)( scrollViewer.Offset.Y / itemHeight);
    }
    private void ScrollIntoView(int index)
    {
        var scrollViewer = FindParentScrollViewer(this);
        if (ElementHeight == null || ElementWidth == null || scrollViewer == null)
            return;
        
        double itemWidth = (double)ElementWidth;
        double itemHeight = (double)ElementHeight;
        
        var viewport = scrollViewer.Viewport;
        int columns = Math.Max(1, (int)(viewport.Width / itemWidth));
        //int rows = Math.Max(1, (int)(viewport.Height / itemHeight));
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

