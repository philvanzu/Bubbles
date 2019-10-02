using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace XamlFSExplorer
{
    internal class ExplorerAddressTextBox : TextBox, INotifyPropertyChanged
    {
        string _savedText;
        string _query;
        bool _browsingOptions;

        ObservableCollection<String> _options = new ObservableCollection<string>();
        ICollectionView _cv;
        DirectoryInfoEx _currentTextFolder;
        DirectoryInfoEx CurrentTextFolder
        {
            get => _currentTextFolder;
            set
            {
                _options.Clear();
                if (value.FullName != "::Root")
                {
                    foreach (var d in value?.GetDirectories())
                        _options.Add((d.FullName.StartsWith("::")) ? d.Label : d.FullName);
                }
                _currentTextFolder = value;
            }
        }

        ListBox _optionsLB = new ListBox();
        Popup _optionsPopup = new Popup();
        
        //The FSExplorerItem
        public static readonly DependencyProperty DirectoryProperty =
            DependencyProperty.Register("Directory", typeof(FSExplorerItem), typeof(ExplorerAddressTextBox),
                new FrameworkPropertyMetadata(null, OnDirectoryChanged) { BindsTwoWayByDefault = false });

        private static void OnDirectoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { (d as ExplorerAddressTextBox).OnDirectoryChanged(e); }

        void OnDirectoryChanged(DependencyPropertyChangedEventArgs e)
        {
            CurrentTextFolder = (e.NewValue as FSExplorerItem).Info as DirectoryInfoEx;
            Text = (CurrentTextFolder.FullName.StartsWith("::"))?CurrentTextFolder.Label : CurrentTextFolder.FullName;
            NotifyPropertyChanged("Directory");
            NotifyPropertyChanged("Text");
        }

        public FSExplorerItem Directory
        {
            get => (FSExplorerItem)GetValue(DirectoryProperty);
            set => SetValue(DirectoryProperty, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            _optionsPopup.PlacementTarget = this;
            _optionsPopup.Placement = PlacementMode.Bottom;

            Window w = Window.GetWindow(this);
            if (null != w)
            {
                w.LocationChanged += delegate (object sender2, EventArgs args)
                {
                    var offset = _optionsPopup.HorizontalOffset;
                    _optionsPopup.HorizontalOffset = offset + 1;
                    _optionsPopup.HorizontalOffset = offset;
                };

                w.SizeChanged += delegate (object sender3, SizeChangedEventArgs e2)
                {
                    var offset = _optionsPopup.HorizontalOffset;
                    _optionsPopup.HorizontalOffset = offset + 1;
                    _optionsPopup.HorizontalOffset = offset;
                };

                w.Closing += delegate (object sender, CancelEventArgs e3)
                {
                    if (_optionsPopup.IsOpen) _optionsPopup.IsOpen = false;
                };
            }

            _cv = CollectionViewSource.GetDefaultView(_options);
            _cv.Filter = (o) =>  
            {
                return (_query == null || _query == string.Empty || (o as string).ToLowerInvariant().StartsWith(_query));
            };

            _optionsLB.ItemsSource = _cv;
            _optionsLB.Focusable = false;
            _optionsLB.SelectionChanged += _optionsLB_SelectionChanged;
            _optionsPopup.Child = _optionsLB;
        }

        

        private void _optionsLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            _savedText = Text;
            CaretIndex = Text.Length;
            SelectAll();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            
            base.OnLostKeyboardFocus(e);
            _browsingOptions = true;
            Text = _savedText;

            _optionsPopup.IsOpen = false;
            if (DataContext is FSExplorerNavbar nb)
            {
                nb.AddressBarLostFocus(this, e);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            
            base.OnPreviewKeyDown(e);
            if(e.Key == Key.Enter)
            {
                _savedText = Text;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (_optionsPopup.IsOpen && (e.Key == Key.Down || e.Key == Key.Tab))
            {
                _optionsLB.SelectedIndex = Math.Min(_optionsLB.Items.Count-1, _optionsLB.SelectedIndex + 1);
                var s = _optionsLB.SelectedItem as string;
                if (s != null && s != string.Empty)
                {
                    _browsingOptions = true;
                    Text = s;
                    CaretIndex = Text.Length;
                }
                e.Handled = true;
            }
            else if (_optionsPopup.IsOpen && e.Key == Key.Up)
            {
                _optionsLB.SelectedIndex = Math.Max(0, _optionsLB.SelectedIndex - 1);
                var s = _optionsLB.SelectedItem as string;
                if (s != null && s != string.Empty)
                {
                    _browsingOptions = true;
                    Text = s;
                    CaretIndex = Text.Length;
                }
                e.Handled = true;
            }
            
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            if (!HasEffectiveKeyboardFocus) return;

            if(_browsingOptions)
            {
                _browsingOptions = false;
                return;
            }
            
            //autocomplete path
            _query = Text.ToLowerInvariant();
            string dirstr = null;
            try { dirstr = Path.GetDirectoryName(_query)?.ToLowerInvariant(); }
            catch { }
            if (dirstr != null && dirstr != string.Empty && dirstr != _currentTextFolder.FullName)
            { 
                try { CurrentTextFolder = new DirectoryInfoEx(dirstr); }
                catch { }
            }
            _optionsLB.SelectedIndex = -1;
            _cv.Refresh();
            _optionsPopup.IsOpen = (_optionsLB.Items.Count > 0) ? true : false;
        }
    }
}
