using Bubbles3.Controls;
using Bubbles3.ViewModels;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Shell;

namespace Bubbles3.Views
{
    /// <summary>
    /// Interaction logic for ShellView.xaml
    /// </summary>
    public partial class ShellView : Window
    {
        [Serializable]
        public struct ShellState
        {
            public Point position;
            public Size size;
            public WindowState windowState;
            [NonSerialized]
            public ResizeMode resizeMode;
            [NonSerialized]
            public WindowStyle windowStyle;
            [NonSerialized]
            public WindowChrome windowChrome;
        }

        readonly WindowChrome _defaultChrome = new WindowChrome()
        {
            ResizeBorderThickness = new Thickness(5),
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(5),
            GlassFrameThickness = new Thickness(0),
            NonClientFrameEdges = NonClientFrameEdges.None,
        };

        readonly WindowChrome _fullScreenChrome = new WindowChrome()
        {
            ResizeBorderThickness = new Thickness(0),
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(1),
            NonClientFrameEdges = NonClientFrameEdges.None
        };

        ShellState _windowedState = new ShellState();
        ShellState _normalState = new ShellState();

        bool _isFullScreen;
        public bool IsFullScreen {
            get { return (bool)GetValue(IsFullScreenProperty); }
            set { SetValue(IsFullScreenProperty, value); }
        }
        public static DependencyProperty IsFullScreenProperty = DependencyProperty.Register("IsFullScreen", typeof(bool), typeof(ShellView), new FrameworkPropertyMetadata(false));

        public ShellView()
        {
            //double sw = System.Windows.SystemParameters.PrimaryScreenWidth;
            //double sh = System.Windows.SystemParameters.PrimaryScreenHeight;


            InitializeComponent();
            //custom window buttons events
            this.SizeChanged += OnSizeChanged;
            
            this.Window_Close.Click += Window_Close_Clicked;
            this.Window_MaximizeRestoreSwitch.Click += Window_MaximizeRestoreSwitch_Clicked;
            this.Window_Minimize.Click += Window_Minimize_Clicked;
            this.TitleBar.MouseLeftButtonDown += TitleBar_Dragged;
            this.DataContextChanged += OnDataContextChanged;
        }

        

        ShellViewModel _vm = null;

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            SetCorrectBorder();
            InvalidateVisual();
        }
        
        void SetCorrectBorder()
        {
            Border windowBorder = (Border)Template.FindName("WindowBorder", this);
            if (IsFullScreen) windowBorder.BorderThickness = new Thickness(0);
            else if (this.WindowState == WindowState.Maximized) windowBorder.BorderThickness = new Thickness(10);
            else windowBorder.BorderThickness = new Thickness(2);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null) return;

            _vm = this.DataContext as ShellViewModel;

            if (_vm != null)
            {
                var grid = this.ImagePanelFullscreenHost as Grid;
                var host = this.ImagePanel as WindowsFormsHost;
                var imsu = host.Child as BblImageSurface;
                _vm.SetupImagePanel(grid, host, imsu);
                DataContextChanged -= OnDataContextChanged;

                //TODO hack
                //ImagePanelFullscreenHost.IsVisibleChanged += FSPanelVisibilityHack;
            }
        }

        //TODO for some mysterious reason ImagePanelFullscreenHost visibility is changed at startup
        // should stay hidden. this sets it right again. preventing it to happen would be better.
        //UPDATE problem mysteriously disappeared for no reason that i can fathom. keeping this just in case
        //private void FSPanelVisibilityHack(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    ImagePanelFullscreenHost.Visibility = Visibility.Hidden;
        //    ImagePanelFullscreenHost.IsVisibleChanged -= FSPanelVisibilityHack;
        //}
        
        public void OnTabActivated(ref Grid imagePanelWindowedHost)
        {
            if (_vm != null) _vm.OnWindowedAnchorChanged( imagePanelWindowedHost );
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal) SaveShellState(ref _normalState);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            if (this.WindowState == WindowState.Normal) SaveShellState(ref _normalState);
            base.OnLocationChanged(e);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "IsFullScreen" && _isFullScreen != (bool)e.NewValue) ToggleFullscreen();
            else
            {
                try
                {
                    base.OnPropertyChanged(e);
                }
                catch (Exception x) { Console.WriteLine(x.Message); }
            }
        }

        private void TitleBar_Dragged(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) Window_MaximizeRestoreSwitch_Clicked(null, null);
            else DragMove();
        }

        private void Window_Close_Clicked(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MaximizeRestoreSwitch_Clicked(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                this.WindowMaximizeIcon.Visibility = Visibility.Visible;
                this.WindowRestoreIcon.Visibility = Visibility.Collapsed;
                RestoreShellState(ref _normalState);
            }
            else
            {
                SaveShellState(ref _normalState);
                WindowState = WindowState.Maximized;
                this.WindowMaximizeIcon.Visibility = Visibility.Collapsed;
                this.WindowRestoreIcon.Visibility = Visibility.Visible;

            }
        }

        private void Window_Minimize_Clicked(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ToggleFullscreen()
        {
            _isFullScreen = !_isFullScreen;
            if (_isFullScreen)
            {
                SaveShellState(ref _windowedState);
                Top = Left = 0;
                WindowState = WindowState.Normal;


                SetValue(WindowChrome.WindowChromeProperty, _fullScreenChrome);
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowState = WindowState.Maximized;

            }
            else
            {
                RestoreShellState(ref _windowedState);

            }

            SetCorrectBorder();
            InvalidateVisual();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            byte[] data = Utils.BblRegistryKey.GetKey().GetValue("WindowCoords") as byte[];
            if (data != null)
            {
                using (MemoryStream stream = new MemoryStream(data))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    try
                    {
                        ShellState state = new ShellState();
                        state = (ShellState)bin.Deserialize(stream);
                        state.resizeMode = ResizeMode.CanResize;
                        state.windowStyle = WindowStyle.SingleBorderWindow;
                        state.windowChrome = this._defaultChrome;
                        RestoreShellState(ref state);
                    }
                    catch { Console.WriteLine("Failed to restore main window state on Initialisation."); }
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isFullScreen) ToggleFullscreen();

            ShellState state = new ShellState();
            SaveShellState(ref state);

            try
            {

                using (MemoryStream stream = new MemoryStream())
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(stream, state);
                    var data = stream.ToArray();
                    Utils.BblRegistryKey.GetKey().SetValue("WindowCoords", data, RegistryValueKind.Binary);
                }
            }
            catch (IOException)
            {
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void SaveShellState(ref ShellState state)
        {
            state.position.X = Left;
            state.position.Y = Top;
            state.size.Width = Width;
            state.size.Height = Height;
            state.windowState = WindowState;
            state.resizeMode = ResizeMode;
            state.windowStyle = WindowStyle;
            state.windowChrome = GetValue(WindowChrome.WindowChromeProperty) as WindowChrome;
        }

        private void RestoreShellState(ref ShellState state)
        {
            WindowStyle = state.windowStyle;
            ResizeMode = state.resizeMode;
            WindowState = state.windowState;
            Width = state.size.Width;
            Height = state.size.Height;
            Left = state.position.X;
            Top = state.position.Y;
            SetValue(WindowChrome.WindowChromeProperty, state.windowChrome);
        }



        

    }
}
