using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Bubbles3.Controls;
using Bubbles3.Models;
using Bubbles3.Utils;

namespace Bubbles3.Views
{
    /// <summary>
    /// Interaction logic for TabView.xaml
    /// </summary>
    public partial class TabView : UserControl
    {

        private readonly static BitmapImage leftpanel_hide = new BitmapImage(new Uri(@"/icons/leftpanel_hide.png", UriKind.Relative));
        private readonly static BitmapImage leftpanel_show = new BitmapImage(new Uri(@"/icons/leftpanel_show.png", UriKind.Relative));
        private readonly static BitmapImage bookview_hide = new BitmapImage(new Uri(@"/icons/bookview_hide.png", UriKind.Relative));
        private readonly static BitmapImage bookview_show = new BitmapImage(new Uri(@"/icons/bookview_show.png", UriKind.Relative));

        private readonly static GridLength _gridLengthZero = new GridLength(0, GridUnitType.Pixel);
        private readonly static GridLength _gridLengthSplitters = new GridLength(5, GridUnitType.Pixel);

        private bool _pageVisible = true;
        private bool _bookVisible = true;
        private bool _explorerVisible = true;

        private GridLength _nav_width = new GridLength(0.66, GridUnitType.Star);
        private GridLength _page_width = new GridLength(0.33, GridUnitType.Star);

        private GridLength _explorer_width = new GridLength(0.33, GridUnitType.Star);
        private GridLength _library_width = new GridLength(0.34, GridUnitType.Star);

        private GridLength _library_height = new GridLength(0.5, GridUnitType.Star);
        private GridLength _book_height = new GridLength(0.5, GridUnitType.Star);




        public TabUIState TabState
        {
            get { return (TabUIState) GetValue(TabStateProperty); }
            set { SetValue(TabStateProperty, value); }
        }
        public static DependencyProperty TabStateProperty = DependencyProperty.Register("TabState", typeof(TabUIState), typeof(TabView), new FrameworkPropertyMetadata(null));

        public bool PageVisible
        {
            get { return (bool)GetValue(PageVisibleProperty); }
            set { SetValue(PageVisibleProperty, value); }
        }
        public static DependencyProperty PageVisibleProperty = DependencyProperty.Register("PageVisible", typeof(bool), typeof(TabView), new FrameworkPropertyMetadata(true));

        public bool BookVisible
        {
            get { return (bool)GetValue(BookVisibleProperty); }
            set { SetValue(BookVisibleProperty, value); }
        }
        public static DependencyProperty BookVisibleProperty = DependencyProperty.Register("BookVisible", typeof(bool), typeof(TabView), new FrameworkPropertyMetadata(true));

        public bool ExplorerVisible
        {
            get { return (bool)GetValue(ExplorerVisibleProperty); }
            set { SetValue(ExplorerVisibleProperty, value); }
        }
        public static DependencyProperty ExplorerVisibleProperty = DependencyProperty.Register("ExplorerVisible", typeof(bool), typeof(TabView), new FrameworkPropertyMetadata(true));

        


        public TabView()
        {
            InitializeComponent();

            Style = this.FindResource("TabViewStyle") as Style;

            if (TabState == null) TabState = new TabUIState();

            this.Page_showhide.MouseLeftButtonUp += ShowHidePagePane;
            this.Explorer_showhide.MouseLeftButtonUp += ShowHideExplorerPane;
            this.Bookview_showhide.MouseLeftButtonUp += ShowHideBookviewPane;

            this.CloseExplorerPanel.Click += CloseExplorerPanel_Clicked;
            

            this.ExplorerGridSplitter.LayoutUpdated += ExplorerGridSplitter_LayoutUpdated;
            this.CentralGridSplitter.LayoutUpdated += CentralGridSplitter_LayoutUpdated;
            this.PageGridSplitter.LayoutUpdated += PageGridSplitter_LayoutUpdated;
            this.PageGridSplitter.DragDelta += PageGridSplitter_DragDelta;
            this.PageGridSplitter.DragCompleted += PageGridSplitter_DragCompleted;
            this.LayoutUpdated += OnLayoutUpdated;
        }



        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            if (oldParent == null)
            {
                var shellview = Application.Current.MainWindow as ShellView;
                shellview.OnTabActivated(ref this.ImagePanelWindowedHost);
            }
        }


        private void PageGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            var grid = this.ImagePanelWindowedHost as Grid;
            if (ImagePanelWindowedHost.Children.Count > 0)
            {
                var host = grid.Children[0] as WindowsFormsHost;
                var imsu = host.Child as BblImageSurface;
                imsu.OnResizePreviewCompleted(e);
            }
        }

        private void PageGridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var grid = this.ImagePanelWindowedHost as Grid;
            if (ImagePanelWindowedHost.Children.Count > 0)
            {
                var host = grid.Children[0] as WindowsFormsHost;
                var imsu = host.Child as BblImageSurface;
                imsu.OnResizePreview(e);
            }
        }

        private void OnLayoutUpdated(object sender, EventArgs e)
        {
            this.Page_showhide.Source = (_pageVisible )? leftpanel_show : leftpanel_hide;
            this.Explorer_showhide.Source = (_explorerVisible) ?  leftpanel_hide : leftpanel_show;
            this.Bookview_showhide.Source = (_bookVisible)? bookview_hide : bookview_show;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if(e.Property.Name == "PageVisible" && (bool)e.NewValue != _pageVisible)
            {
                ShowHidePagePane(null, null);
            }
            else if (e.Property.Name == "ExplorerVisible" && (bool)e.NewValue != _explorerVisible)
            {
                ShowHideExplorerPane(null, null);
            }
            else if (e.Property.Name == "BookVisible" && (bool)e.NewValue != _bookVisible)
            {
                ShowHideBookviewPane(null, null);
            }
            else if(e.Property.Name == "TabState")
            {
                TabUIState state = e.NewValue as TabUIState;
                if(state.doLoad == true)
                { 
                    _page_width = state.page.ToGridLength;
                    this.Navpane_Width.Width = _nav_width = new GridLength(1d - _page_width.Value, GridUnitType.Star);

                    _explorer_width = state.explorer.ToGridLength;
                    this.LibraryPane_Width.Width = _library_width = new GridLength(1d - _explorer_width.Value, GridUnitType.Star);

                    _book_height = state.bookview.ToGridLength;
                    this.LibraryPane_Height.Height = _library_height = new GridLength(1d - _book_height.Value, GridUnitType.Star);

                    if (state.pageVisible == true)
                    {
                        PageVisible = _pageVisible = true;
                        this.PagePane_Width.Width = _page_width;
                        this.PagePane_Splitter_Width.Width = _gridLengthSplitters;
                        this.Page_showhide.Source = leftpanel_show;
                    }
                    else
                    {
                        PageVisible = _pageVisible = false;
                        this.PagePane_Width.Width = _gridLengthZero;
                        this.PagePane_Splitter_Width.Width = _gridLengthZero;
                        this.Page_showhide.Source = leftpanel_hide;
                    }

                    if (state.explorerVisible == true)
                    {
                        ExplorerVisible = _explorerVisible = true;
                        this.ExplorerPane_Width.Width = _explorer_width;
                        this.ExplorerPane_Splitter_Width.Width = _gridLengthSplitters;
                        this.Explorer_showhide.Source = leftpanel_hide;
                    }
                    else
                    {
                        ExplorerVisible = _explorerVisible = false;
                        this.ExplorerPane_Width.Width = _gridLengthZero;
                        this.ExplorerPane_Splitter_Width.Width = _gridLengthZero;
                        this.Explorer_showhide.Source = leftpanel_show;
                    }

                    this.LibraryPane_Height.Height = new GridLength(1d - _book_height.Value, GridUnitType.Star);
                    if (state.bookViewVisible == true)
                    {
                        BookVisible = _bookVisible = true;
                        this.BookPane_Height.Height = _book_height;
                        this.BookPane_Splitter_Height.Height = _gridLengthSplitters;
                        this.Bookview_showhide.Source = bookview_hide;
                    }
                    else
                    {
                        BookVisible = _bookVisible = false;
                        this.BookPane_Height.Height = _gridLengthZero;
                        this.BookPane_Splitter_Height.Height = _gridLengthZero;
                        this.Bookview_showhide.Source = bookview_show;
                    }
                }
            }
        }

        private void ShowHidePagePane(object sender, MouseButtonEventArgs e)
        {
            _pageVisible = !_pageVisible;
            if (_pageVisible)
            {
                Page_showhide.Source = leftpanel_hide;
                PagePane_Width.Width = _page_width;
                PagePane_Splitter_Width.Width = _gridLengthSplitters;
            }
            else
            {
                Page_showhide.Source = leftpanel_show;
                PagePane_Width.Width = _gridLengthZero;
                PagePane_Splitter_Width.Width = _gridLengthZero;
            }
            TabUIState state = TabState;
            state.pageVisible = _pageVisible;
            state.doLoad = false;
            TabState = state;
        }
        private void PageGridSplitter_LayoutUpdated(object sender, EventArgs e)
        {
            _nav_width = Navpane_Width.Width;
            if (_pageVisible)
            {
                _page_width = PagePane_Width.Width;
                double w = _nav_width.Value + _page_width.Value;
                double rel = _page_width.Value / w;
                GridLength gl = new GridLength(rel, GridUnitType.Star);
                TabUIState state = TabState;
                state.page = new SerializableGridLength(gl);
                state.doLoad = false;
                TabState = state;
            }
        }


        private void ShowHideExplorerPane(object sender, MouseButtonEventArgs e)
        {
            _explorerVisible = !_explorerVisible;
            if (!_explorerVisible)
            {
                Explorer_showhide.Source = leftpanel_show;
                this.ExplorerPane_Width.Width = _gridLengthZero;
                this.ExplorerPane_Splitter_Width.Width = _gridLengthZero;

            }
            else
            {
                Explorer_showhide.Source = leftpanel_hide;
                this.ExplorerPane_Width.Width = _explorer_width;
                this.ExplorerPane_Splitter_Width.Width = _gridLengthSplitters;
            }

            TabUIState state = TabState;
            state.explorerVisible = _explorerVisible;
            state.doLoad = false;
            TabState = state;
            if (ExplorerVisible != _explorerVisible) ExplorerVisible = _explorerVisible;
        }

        private void CloseExplorerPanel_Clicked(object sender, RoutedEventArgs e)
        {
            if(_explorerVisible) ShowHideExplorerPane(null, null);
        }

        private void ExplorerGridSplitter_LayoutUpdated(object sender, EventArgs e)
        {
            _library_width = LibraryPane_Width.Width;
            if (_explorerVisible)
            {
                _explorer_width = this.ExplorerPane_Width.Width;
                double w = _library_width.Value + _explorer_width.Value;
                double rel = _explorer_width.Value / w;
                GridLength gl = new GridLength(rel, GridUnitType.Star);
                TabUIState state = TabState;
                state.explorer = new SerializableGridLength(gl);
                state.doLoad = false;
                TabState = state;
            }

        }

        private void ShowHideBookviewPane(object sender, MouseButtonEventArgs e)
        {
            _bookVisible = !_bookVisible;
            if (!_bookVisible)
            {
                Bookview_showhide.Source = bookview_show;
                this.BookPane_Height.Height = _gridLengthZero;
                this.BookPane_Splitter_Height.Height = _gridLengthZero;
            }
            else
            {
                Bookview_showhide.Source = bookview_show;
                this.BookPane_Height.Height = _book_height;
                this.BookPane_Splitter_Height.Height = _gridLengthSplitters;
            }
            TabUIState state = TabState;
            state.bookViewVisible = _bookVisible;
            state.doLoad = false;
            TabState = state;
            if (BookVisible != _bookVisible) BookVisible = _bookVisible;
        }

        private void CentralGridSplitter_LayoutUpdated(object sender, EventArgs e)
        {
            
            _library_height = LibraryPane_Height.Height;
            if (_bookVisible)
            {
                _book_height = BookPane_Height.Height;
                double h = _library_height.Value + _book_height.Value;
                double rel = _book_height.Value / h;
                GridLength gl = new GridLength(rel, GridUnitType.Star);
                TabUIState state = TabState;
                state.bookview = new SerializableGridLength(gl);
                state.doLoad = false;
                TabState = state;
            }
        }

    }
}
