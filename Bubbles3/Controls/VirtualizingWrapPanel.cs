using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Bubbles3.ViewModels;
namespace Bubbles3.Controls
{

    public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {

        private ScrollViewer _owner;
        private const bool _canHScroll = false;
        private bool _canVScroll = false;
        private Size _extent = new Size(0, 0);
        private Size _viewport = new Size(0, 0);
        private Point _offset;
        UIElementCollection _children;
        ItemsControl _itemsControl;
        IItemContainerGenerator _generator;
        private int _firstVisibleIndex;
        private int _lastVisibleIndex;

        Dictionary<UIElement, Rect> _realizedChildLayout = new Dictionary<UIElement, Rect>();


        public VirtualizingWrapPanel()
        {

        }

        ~VirtualizingWrapPanel()
        {

        }

        #region Properties
        private Size ChildSlotSize
        {
            get { return new Size(ItemWidth, ItemHeight); }
        }

        #endregion

        #region Dependency Properties

        [TypeConverter(typeof(LengthConverter))]
        public double ItemHeight
        {
            get { return (double)base.GetValue(ItemHeightProperty); }
            set { base.SetValue(ItemHeightProperty, value);  }
        }

        [TypeConverter(typeof(LengthConverter))]
        public double ItemWidth
        {
            get { return (double)base.GetValue(ItemWidthProperty); }
            set { base.SetValue(ItemWidthProperty, value); }
        }

        public int ScrollToIndex
        {
            get { return (int)GetValue(ScrollToIndexProperty); }
            set
            {
                SetValue(ScrollToIndexProperty, value);
                BringIndexIntoView(value);
            }
        }
        
        public static DependencyProperty ItemHeightProperty = DependencyProperty.Register("ItemHeight", typeof(double), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(double.PositiveInfinity));
        public static DependencyProperty ItemWidthProperty = DependencyProperty.Register("ItemWidth", typeof(double), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(double.PositiveInfinity));
        public static DependencyProperty ScrollToIndexProperty = DependencyProperty.Register("ScrollToIndex", typeof(int), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(-1));


        private int LineCapacity => Math.Max((_viewport.Width != 0) ? (int)(_viewport.Width / ItemWidth) : 0, 1); 
        private int RowCapacity => Math.Max((_viewport.Height != 0)?(int)(_viewport.Height / ItemHeight) : 0 ,1);
        private int LinesCount => (ItemsCount > 0) ? (ItemsCount / LineCapacity)+1 : 0; 
        private int ItemsCount => _itemsControl.Items.Count; 
        public int FirstVisibleLine => (int)(_offset.Y / ItemHeight); 
        public int FirstVisibleItemVPos => (int)((FirstVisibleLine * ItemHeight) - _offset.Y); 
        public int FirstVisibleIndex => (FirstVisibleLine * LineCapacity); 
        public int LastVisibleLine => FirstVisibleLine + (int)Math.Ceiling(((_viewport.Height - (FirstVisibleItemVPos + ItemHeight)) / ItemHeight));
        public int LastVisibleIndex => ((LastVisibleLine + 1) * LineCapacity) - 1; 
        public double ItemSpacing => (_viewport.Width - (LineCapacity * ItemWidth)) / (LineCapacity*2); 
        #endregion



        #region VirtualizingPanel overrides
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {

            base.OnPropertyChanged(e);

            if (e.Property.Name == "ItemWidth")
            {
                int first = _firstVisibleIndex;
                
                if(_itemsControl != null)
                { 
                    ListView lv = (ListView)_itemsControl;
                    object selected = lv.SelectedItem;
                    lv.SelectedItem = null;
                    Size extent = _extent;
                    _extent = new Size(_extent.Width, _extent.Height + (2 * _viewport.Height));
                    SetVerticalOffset(_extent.Height);
                    MeasureOverride(_viewport);
                    _extent = extent;
                    lv.SelectedItem = selected;
                    BringIndexIntoView(first);
                    
                }
                else SetVerticalOffset(0);
                //UpdateLayout();
            }
            else if (e.Property.Name =="ScrollToIndex")
            {
                BringIndexIntoView((int)e.NewValue);
            }
            //else if (e.Property.Name == "DataContext")
            //{
            //    var bvm = e.NewValue as BookViewModel;
            //    if (bvm != null)
            //        bvm.Library.OnBookContainerDataContextChanged(bvm);
            //}
        }
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            _itemsControl = ItemsControl.GetItemsOwner(this);
            _children = InternalChildren;
            _generator = ItemContainerGenerator;
            this.SizeChanged += new SizeChangedEventHandler(this.Resizing);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_itemsControl == null || _itemsControl.Items.Count == 0)
                return availableSize;

            if (availableSize != _viewport)
            {
                _viewport = availableSize;
                if (_owner != null)
                    _owner.InvalidateScrollInfo();
            }

            Size childSize = ChildSlotSize;
            Size extent = new Size(availableSize.Width, LinesCount * ItemHeight);

            if (extent != _extent)
            {
                _extent = extent;
                if (_owner != null)
                    _owner.InvalidateScrollInfo();
            }

            _realizedChildLayout.Clear();

            Size realizedFrameSize = availableSize;

            
            //TODO : Try generating one line below firstvisible and one line above lastvisible?
            _firstVisibleIndex = FirstVisibleIndex;
            SetValue(ScrollToIndexProperty, _firstVisibleIndex);

            _lastVisibleIndex = LastVisibleIndex;
            int visibleItemsCount = (_lastVisibleIndex - _firstVisibleIndex)+1;

            GeneratorPosition startPos = _generator.GeneratorPositionFromIndex(_firstVisibleIndex);

            int childIndex = (startPos.Offset == 0) ? startPos.Index : startPos.Index + 1;
            int current = _firstVisibleIndex;
            using (_generator.StartAt(startPos, GeneratorDirection.Forward, true))
            {
                bool stop = false;
                double currentX = 0;
                double currentY = FirstVisibleItemVPos;

                while (current < ItemsCount && current <= _lastVisibleIndex)
                {
                    bool newlyRealized;

                    // Get or create the child                    
                    UIElement child = _generator.GenerateNext(out newlyRealized) as UIElement;
                    if (child == null) break;
                    child.IsEnabled = true;

                    if (newlyRealized)
                    {
                        // Figure out if we need to insert the child at the end or somewhere in the middle
                        if (childIndex >= _children.Count)
                        {
                            base.AddInternalChild(child);
                        }
                        else
                        {
                            base.InsertInternalChild(childIndex, child);
                        }
                        _generator.PrepareItemContainer(child);
                        child.Measure(ChildSlotSize);
                    }
                    else
                    {
                        // The child has already been created, let's be sure it's in the right spot
                        //Debug.Assert(child == _children[childIndex], "Wrong child was generated");
                    }
                    childSize = child.DesiredSize;
                    currentX += ItemSpacing;
                    Rect childRect = new Rect(new Point(currentX, currentY), childSize);

                    if (childRect.Right > realizedFrameSize.Width) //wrap to a new line
                    {
                        currentY = currentY + ItemHeight;
                        currentX = ItemSpacing;
                        childRect.X = currentX;
                        childRect.Y = currentY;
                    }

                    if (currentY > realizedFrameSize.Height)
                        stop = true;
                    currentX = childRect.Right + ItemSpacing;

                    _realizedChildLayout.Add(child, childRect);

                    if (current < ItemsCount)
                    { 
                        var lvi = child as FrameworkElement;
                        var vitem = lvi.DataContext as IVirtualizableItem;
                        if (vitem != null) vitem.IsRealized = true;
                    }

                    if (stop)
                        break;

                    current++;
                    childIndex++;
                }
            }
            CleanUpItems(_firstVisibleIndex, current-1 );

            return availableSize;
        }
        public void CleanUpItems(int minDesiredGenerated, int maxDesiredGenerated)
        {
            for (int i = _children.Count - 1; i >= 0; i--)
            {

                GeneratorPosition childGeneratorPos = new GeneratorPosition(i, 0);
                int itemIndex = _generator.IndexFromGeneratorPosition(childGeneratorPos);
                if (itemIndex < minDesiredGenerated || itemIndex > maxDesiredGenerated)
                {
                    var child = _children[i] as ListBoxItem;
                    child.IsEnabled = false;

                    if (child != null && child.IsSelected)
                    {
                        var layoutInfo = new Rect(0, 0, 0, 0);
                        child.Arrange(layoutInfo);
                    }
                    else
                    {
                        var vitem = child.DataContext as IVirtualizableItem;
                        if (vitem != null) vitem.IsRealized = false;
                        _generator.Remove(childGeneratorPos, 1);
                        RemoveInternalChildRange(i, 1);

                    }
                }

            }
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_children != null)
            {
                foreach (UIElement child in _children)
                {
                    if (child.IsEnabled)
                    {
                        var layoutInfo = _realizedChildLayout[child];
                        child.Arrange(layoutInfo);
                    }
                }
            }
            return finalSize;
        }

        int _scrollToRequest = -1;
        protected override void BringIndexIntoView(int index)
        {
            if (index != 0 && _viewport.Width == 0 && _viewport.Height == 0)
            {
                _scrollToRequest = index;
                return;
            }
            double indexOffset = -1;
            if (index < FirstVisibleIndex)indexOffset = (index / LineCapacity) * ItemHeight;
            else if(index > LastVisibleIndex) indexOffset = ((index / LineCapacity) * ItemHeight) - ((RowCapacity) * ItemHeight);

            if(indexOffset != -1) SetVerticalOffset(indexOffset);
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            base.OnItemsChanged(sender, args);

            _offset.X = 0;
            _offset.Y = 0;

            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;
                case NotifyCollectionChangedAction.Move:
                    RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
                    break;
            }
        }

        #endregion


        #region EventHandlers
        public void Resizing(object sender, EventArgs e)
        {
            var args = e as SizeChangedEventArgs;
            if (args.WidthChanged)
            {
                int lineCapacity = LineCapacity;
                int previousLineCapacity = (int)(args.PreviousSize.Width / ItemWidth);
                if (previousLineCapacity != lineCapacity)
                {
                    int previousFirstItem;
                    if (_scrollToRequest != -1)
                    {
                        previousFirstItem = _scrollToRequest;
                        _scrollToRequest = -1;
                    }
                    else previousFirstItem = ((int)(_offset.Y / ItemHeight) <= 0) ? 0 : ((int)(_offset.Y / ItemHeight) * previousLineCapacity);
                    BringIndexIntoView(previousFirstItem);
                }
            }
            if (_viewport.Width != 0)
            {
                MeasureOverride(_viewport);
            }
        }
        #endregion

        #region IScrollInfo Implementation
        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            var gen = (ItemContainerGenerator)_generator.GetItemContainerGeneratorForPanel(this);
            var element = (UIElement)visual;
            int itemIndex = gen.IndexFromContainer(element);
            while (itemIndex == -1)
            {
                element = (UIElement)VisualTreeHelper.GetParent(element);
                itemIndex = gen.IndexFromContainer(element);
            }

            Rect elementRect = new Rect();
            if (element.IsEnabled)
                elementRect = _realizedChildLayout[element];

            if (elementRect.Bottom > ViewportHeight)
            {
                double translation = elementRect.Bottom - ViewportHeight;
                _offset.Y += translation;
            }
            else if (elementRect.Top < 0)
            {
                double translation = elementRect.Top;
                _offset.Y += translation;
            }
            InvalidateMeasure();
            return elementRect;
        }

        public void LineDown()
        {
            SetVerticalOffset(VerticalOffset + ItemHeight);
        }

        public void LineUp()
        {
            SetVerticalOffset(VerticalOffset - ItemHeight);
        }

        public void MouseWheelDown()
        {
            SetVerticalOffset(VerticalOffset + ItemHeight);
        }

        public void MouseWheelUp()
        {
            SetVerticalOffset(VerticalOffset - ItemHeight);
        }

        public void PageDown()
        {
            int fullyVisibleLines = (int)(_viewport.Height / ItemHeight);
            SetVerticalOffset(VerticalOffset + (fullyVisibleLines * ItemHeight));
        }

        public void PageUp()
        {
            int fullyVisibleLines = (int)(_viewport.Height / ItemHeight);
            SetVerticalOffset(VerticalOffset - (fullyVisibleLines * ItemHeight));
        }

        public void SetVerticalOffset(double offset)
        {
            if (offset < 0 || _viewport.Height >= _extent.Height)
            {
                offset = 0;
            }
            else
            {
                if (offset + _viewport.Height >= _extent.Height)
                {
                    offset = _extent.Height - _viewport.Height;
                }
            }

            _offset.Y = offset;

            if (_owner != null)
                _owner.InvalidateScrollInfo();

            InvalidateMeasure();
        }

        public ScrollViewer ScrollOwner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public bool CanHorizontallyScroll
        {
            get { return false; }
            set { if (value == true) throw (new ArgumentException("VirtualizingWrapPanel does not support Horizontal scrolling")); }
        }

        public bool CanVerticallyScroll
        {
            get { return _canVScroll; }
            set { _canVScroll = value; }
        }

        public double ExtentHeight
        {
            get { return _extent.Height; }
        }

        public double ExtentWidth
        {
            get { return _extent.Width; }
        }

        public double HorizontalOffset
        {
            get { return _offset.X; }
        }

        public double VerticalOffset
        {
            get { return _offset.Y; }
        }

        public double ViewportHeight
        {
            get { return _viewport.Height; }
        }

        public double ViewportWidth
        {
            get { return _viewport.Width; }
        }

        public void LineLeft() { throw new NotImplementedException(); }
        public void LineRight() { throw new NotImplementedException(); }
        public void MouseWheelLeft() { throw new NotImplementedException(); }
        public void MouseWheelRight() { throw new NotImplementedException(); }
        public void PageLeft() { throw new NotImplementedException(); }
        public void PageRight() { throw new NotImplementedException(); }
        public void SetHorizontalOffset(double offset) { throw new NotImplementedException(); }
        #endregion

        #region methods


        #endregion
    }

    public interface IVirtualizableItem
    {
        bool IsRealized { get; set; }
    }
}
