using System;
using System.Collections.Generic;
using System.Windows.Input;
using Caliburn.Micro;
using Bubbles3.Models;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using Bubbles3.Controls;
using Bubbles3.Utils;

namespace Bubbles3.ViewModels
{
    public class ShellViewModel:Conductor<IScreen>.Collection.OneActive
    {
        ObservableCollection<TabOptions> _savedOptions;

        public static ShellViewModel Instance { get; set; }
        public ShellViewModel()
        {
            if (Instance != null) throw new InvalidOperationException("Trying to instantiate a new ShellView window");
            else Instance = this;

            DisplayName = "Bubbles 3";


        }



        public TabViewModel ActiveTab { get { return ActiveItem as TabViewModel; } }

        bool _isFullScreen = false;
        public bool IsFullScreen
        {
            get { return _isFullScreen; }
            set
            {
                _isFullScreen = value;
                NotifyOfPropertyChange(()=>IsFullScreen);
            }
        }


        public void CreateTab()
        {
            ActivateItem( new TabViewModel( this, new BblTabState(), _savedOptions) );
        }
        public void CycleTab(bool next = true)
        {
            if (Items.Count < 2) return;
            int idx = Items.IndexOf(ActiveItem);

            idx = (next) ? idx + 1 : idx - 1;
            if (idx < 0) idx = Items.Count - 1;
            else if (idx >= Items.Count) idx = 0;

            ActivateItem(Items[idx]);
        }
        public bool CanCloseTab
        {
            get { return Items.Count > 1; }
        }

        public override void ActivateItem(IScreen item)
        {
            if (ActiveItem != null) DeactivateItem(ActiveItem, false);

            base.ActivateItem(item);

            if (ActiveItem == null) return;

            if (_isFullScreen)
            {
                ImageSurface.P = ActiveTab.FullscreenOptions;
            }
            else
            {
                ImageSurface.P = ActiveTab.WindowedOptions;
            }
        }

        protected override void OnActivationProcessed(IScreen item, bool success)
        {
            base.OnActivationProcessed(item, success);
            NotifyOfPropertyChange(() => ActiveItem);
        }

        public override void DeactivateItem(IScreen item, bool close)
        {
            base.DeactivateItem(item, close);
            if (close) NotifyOfPropertyChange(() => CanCloseTab);
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            List<BblTabState> tabStates = BblTabState.ReadFromRegistry();

            _savedOptions = TabOptions.ReadFromRegistry();
            if (_savedOptions == null || _savedOptions.Count == 0)
            {
                _savedOptions = new ObservableCollection<TabOptions>();
                _savedOptions.Add(new TabOptions());
                _savedOptions.Add(TabOptions.PhotoTabOptions);
                _savedOptions.Add(TabOptions.ComicTabOptions);
            }


            foreach (var tabState in tabStates)
            {
                TabViewModel tab = new TabViewModel(this, tabState, _savedOptions);
                Items.Add(tab);
                if (tabState.isActive) ActivateItem(tab);
            }

        }
        protected override void OnActivate()
        {
            base.OnActivate();
        }
        protected override void OnDeactivate(bool close)
        {
            if(close)
            { 
                List<BblTabState> tabStates = new List<BblTabState>();

                foreach (var i in Items)
                {
                    var tab = i as TabViewModel;
                    tab.TabState.isActive = i.IsActive;
                    if(tab.SelectedFolder != null) tab.TabState.navigated = tab.SelectedFolder.FullName;
                    foreach (var fsbm in tab.Bookmarks)
                    {
                        var bm = (BblBookmark)fsbm.UserData;
                        if (!bm.destroyOnClose) tab.TabState.savedBookmarks.Add(bm);
                    }
                    tabStates.Add(tab.TabState);

                    
                }
                
                BblTabState.WriteToRegistry(tabStates);
                TabOptions.WriteToRegistry(_savedOptions);

                if (ImageSurface != null && !ImageSurface.IsDisposed)
                {
                    ImageSurface.nextPageRequested -= OnNextPageRequested;
                    ImageSurface.prevPageRequested -= OnPrevPageRequested;
                    ImageSurface.fullscreenToggleRequested -= OnFullScreenToggleRequested;
                    ImageSurface.Dispose();
                }

            }
            base.OnDeactivate(close);
        }

        Grid _imagePanelFullscreenHost = null;
        Grid _imagePanelWindowedHost = null;
        WindowsFormsHost _imagePanelHost = null;
        BblImageSurface ImageSurface { get; set; }
        public void SetupImagePanel(Grid fullscreenAnchor, WindowsFormsHost surfaceHost, BblImageSurface surface )
        {
            ImageSurface = surface;
            ImageSurface.nextPageRequested += OnNextPageRequested;
            ImageSurface.prevPageRequested += OnPrevPageRequested;
            ImageSurface.fullscreenToggleRequested += OnFullScreenToggleRequested;

            _imagePanelFullscreenHost = fullscreenAnchor;
            _imagePanelHost = surfaceHost;

            _imagePanelFullscreenHost.Children.Remove(_imagePanelHost);
            if(_imagePanelWindowedHost != null) _imagePanelWindowedHost.Children.Add(_imagePanelHost);
        }

        public void ShowPage(PageViewModel page)
        {
            if (page != null && ImageSurface != null)
            {
                ImageSurface.LoadPage(page);
                ActiveTab.PagePath = page.Model.Path;
                
            }
        }

        private void OnFullScreenToggleRequested(object sender, EventArgs e)
        {
            ToggleFullscreen();
        }

        private void OnPrevPageRequested(object sender, EventArgs e)
        {
            ActiveTab.OnRequestPreviousPage();
        }

        private void OnNextPageRequested(object sender, EventArgs e)
        {
            ActiveTab.OnRequestNextPage();
        }

        public void OnWindowedAnchorChanged(Grid windowedAnchor)
        {
            if (!_isFullScreen)
            {
                if(_imagePanelWindowedHost != null) _imagePanelWindowedHost.Children.Remove(_imagePanelHost);
                if(_imagePanelHost != null) windowedAnchor.Children.Add(_imagePanelHost);
            }
            _imagePanelWindowedHost = windowedAnchor;
        }

        //more code specific to fullscreen switching in Shellview.cs codebehind
        public void ToggleFullscreen()
        {
            if (!IsFullScreen) //going fullscreen
            {
                IsFullScreen = true; 
                _imagePanelWindowedHost.Children.Remove(_imagePanelHost);
                _imagePanelFullscreenHost.Children.Add(_imagePanelHost);
                ImageSurface.P = ActiveTab.FullscreenOptions;
                _imagePanelFullscreenHost.Visibility = System.Windows.Visibility.Visible;
            }
            else if (IsFullScreen) //going windowed
            {
                IsFullScreen = false; 
                _imagePanelFullscreenHost.Visibility = System.Windows.Visibility.Hidden;
                ImageSurface.P = ActiveTab.WindowedOptions;
                _imagePanelFullscreenHost.Children.Remove(_imagePanelHost);
                _imagePanelWindowedHost.Children.Add(_imagePanelHost);
                
                if(ActiveTab?.Library != null) ActiveTab.Library.ScrollToSelected();
            }
        }
        public ICommand CreateTabCommand => new DelegateCommand(new Action<object>((t) => {
            CreateTab();
        }));
        public ICommand ShowOptionsCommand => new DelegateCommand(new Action<object>((t) => {
            if (ActiveTab != null) ((TabViewModel)ActiveTab).ManageSettings();
        }));
        public ICommand FullscreenCommand => new DelegateCommand(new Action<object>((t) => 
        {
            ToggleFullscreen();
        }));
        public ICommand EscapeCommand => new DelegateCommand(new Action<object>((t) => 
        {

        }));
        public ICommand ResetViewCommand => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.ResetView();
        }));
        public ICommand FitBestCommand => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.Fit();
        }));
        public ICommand FitHCommand => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.FitH();
        }));
        public ICommand FitWCommand => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.FitW();
        }));
        public ICommand Rot0Command => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.RotationAngle = 0;
        }));
        public ICommand Rot90Command => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.RotationAngle = 90;
        }));
        public ICommand Rot180Command => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.RotationAngle = 180;
        }));
        public ICommand Rot270Command => new DelegateCommand(new Action<object>((t) => 
        {
            ImageSurface.RotationAngle = 270;
        }));
        public ICommand ZoopInCommand => new DelegateCommand(new Action<object>((t) =>
        {
                ImageSurface.Zoom(15f, true, true);
        }));
        public ICommand ZoopOutCommand => new DelegateCommand(new Action<object>((t) =>
        {
                ImageSurface.Zoom(-15f, true, true);
        }));
        public ICommand NextPageCommand => new DelegateCommand(new Action<object>((t) => 
        {
            if (ActiveTab != null) ActiveTab.OnRequestNextPage();
        }));
        public ICommand PreviousPageCommand => new DelegateCommand(new Action<object>((t) => 
        {
            if (ActiveTab != null) ActiveTab.OnRequestPreviousPage();
        }));
        public ICommand LastPageCommand => new DelegateCommand(new Action<object>((t) =>
        {
            if (ActiveTab != null) ActiveTab.OnRequestLastPage();
        }));
        public ICommand FirstPageCommand => new DelegateCommand(new Action<object>((t) =>
        {
            if (ActiveTab != null) ActiveTab.OnRequestFirstPage();
        }));
        public ICommand NextBookCommand => new DelegateCommand(new Action<object>((t) => 
        {
            if (ActiveTab != null) ActiveTab.OnRequestNextBook();
        }));
        public ICommand PreviousBookCommand => new DelegateCommand(new Action<object>((t) => 
        {
            if (ActiveTab != null) ActiveTab.OnRequestPrevBook();
        }));
        public ICommand NextTabCommand => new DelegateCommand(new Action<object>((t) => 
        {
            CycleTab(true);
        }));
        public ICommand PreviousTabCommand => new DelegateCommand(new Action<object>((t) => 
        {
            CycleTab(false);
        }));
        public ICommand DeleteCommand => new DelegateCommand(new Action<object>((t) => {
            if(IsFullScreen)
            {
                ActiveTab?.Library?.SelectedBook?.SelectedPage?.DeleteFile();
            }

        }));
        public ICommand AddBookDirectoriesCommand => new DelegateCommand(new Action<object>((t) => {
            if(ActiveTab != null) ActiveTab.ProcessPromotables();
          }));
        public ICommand PredictIvpCommand => new DelegateCommand(new Action<object>((t) => {
            //if(ActiveTab?.Library?.SelectedBook != null)
            //{
            //    List<string> imagePaths = new List<string>();
            //    foreach ( var p in ActiveTab.Library.SelectedBook.Pages)
            //    {
            //        if (System.IO.File.Exists(p.Path)) imagePaths.Add(p.Path);
            //    }

            //    IvpPredictor predictor = new IvpPredictor(null);
            //    List<DeepVPngData> predictions = predictions = predictor.Predict(imagePaths);
            //    predictor.Dispose();

            //    if (predictions != null && predictions.Count > 0)
            //    {
            //        foreach ( var p in ActiveTab.Library.SelectedBook.Pages)
            //        {
            //            var ivp = p.Model.Ivp;
            //            DeepVPngData pred = predictions.Where(x => x.path == p.Path).FirstOrDefault();
            //            //Console.WriteLine("-------------------");
            //            //Console.WriteLine("Top : " + (ivp.t - pred.ivpTop).ToString());
            //            //Console.WriteLine("Bottom : " + (ivp.b - pred.ivpBottom).ToString());
            //            //Console.WriteLine("Left : " + (ivp.l - pred.ivpLeft).ToString());
            //            //Console.WriteLine("Right : " + (ivp.r - pred.ivpRight).ToString());
            //            //Console.WriteLine("-------------------");
            //            ivp.t = (float) pred.ivpTop;
            //            ivp.b = (float)pred.ivpBottom;
            //            ivp.l = (float)pred.ivpLeft;
            //            ivp.r = (float)pred.ivpRight;

            //            p.Model.Ivp = ivp;
            //        }
            //    }
                
            //}
        }));

        public void OnKeyUp(System.Windows.Input.KeyEventArgs e)
        {
        }
        public void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (ImageSurface == null || !ImageSurface.Focused) return;
            switch (e.Key)
            {
                case Key.Escape:
                    if (ImageSurface.IsDrawingZoomRect)
                    {
                        ImageSurface.IsDrawingZoomRect = false;
                        ImageSurface.ZoomRect(false);
                        ImageSurface.Invalidate();
                        e.Handled = true;
                    }
                    else if (IsFullScreen)
                    {
                        ToggleFullscreen();
                        ImageSurface.Invalidate();
                        e.Handled = true;
                    }
                    break;
                case Key.Left:
                    if(ImageSurface.Focused)
                    { 
                        ImageSurface.OnLeft();
                        e.Handled = true;
                    }
                    break;
                case Key.Right:
                    if (ImageSurface.Focused)
                    {
                        ImageSurface.OnRight();
                        e.Handled = true;
                    }
                    break;
                case Key.Up:
                    if (ImageSurface.Focused)
                    {
                        ImageSurface.OnUp();
                        e.Handled = true;
                    }
                    break;
                case Key.Down:
                    if (ImageSurface.Focused)
                    {
                        ImageSurface.OnDown();
                        e.Handled = true;
                    }
                    break;
                //case Key.Add:
                //    if(ImageSurface.Focused)
                //    {
                //        ImageSurface.Zoom(15f, true, true);
                //    }
                //    break;
                //case Key.Subtract:
                //    if (ImageSurface.Focused)
                //    {
                //        ImageSurface.Zoom(-15f, true, true);
                //    }
                //    break;
                default:
                    break;
            }
        }
    }//class
}//namespace
