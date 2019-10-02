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
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XamlFSExplorer
{
    /// <summary>
    /// </summary>
    public class FSExplorerNavbar : Control, INotifyPropertyChanged
    {
        private FSExplorerItem _currentItem;
        public FSExplorerItem CurrentFolderItem { get => Explorer?.CurrentFolderItem; }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            AddressBarVisibility = Visibility.Hidden;
            BreadcrumbBarVisibility = Visibility.Visible;
        }


        // ////////////////////////////
        // FSExplorer DependencyProperty
        public static readonly DependencyProperty ExplorerProperty =
            DependencyProperty.Register("Explorer", typeof(FSExplorer), typeof(FSExplorerNavbar),
            new FrameworkPropertyMetadata(null, OnExplorerChanged) { BindsTwoWayByDefault = true });

        private static void OnExplorerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var navbar = d as FSExplorerNavbar;
            if (navbar.Explorer != null) navbar.Explorer.Navigated -= navbar.OnExplorerNavigated;
            if (e.NewValue is FSExplorer explorer) explorer.Navigated += navbar.OnExplorerNavigated;
            var test = navbar.GetValue(ExplorerProperty);
            navbar.CurrentPathChanged();
        }

        /// <value> The FileSystemExplorer object  </value>
        public FSExplorer Explorer
        {
            get { return (FSExplorer)GetValue(ExplorerProperty); }
            set { SetValue(ExplorerProperty, value); }
        }


        // /////////////////////////////////////
        // SelectedItem DependencyProperty
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(FileSystemInfoEx), typeof(FSExplorerNavbar),
            new FrameworkPropertyMetadata(null, OnSelectedItemChanged) { BindsTwoWayByDefault = true });

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var fsexplorer = d as FSExplorerList;
            if (e.NewValue is FileSystemInfoEx value)
            {

            }
        }

        public FileSystemInfoEx SelectedItem
        {
            get => (FileSystemInfoEx)GetValue(SelectedItemProperty);
            set
            {
                SetValue(SelectedItemProperty, value);
                CurrentPathChanged();
            }
        }

        // properties
        public double NavButtonsWidth { get => 78; }
        public double AddressBarWidth { get => Math.Max(ActualWidth - NavButtonsWidth, 0); }
        double _availableWidth;
        protected override Size MeasureOverride(Size constraint)
        {
            _availableWidth = constraint.Width;
            return base.MeasureOverride(constraint);
        }
        string _currentAddress;
        public string CurrentAddress
        {
            get => _currentAddress;
            set
            {
                _currentAddress = value;
            }
        }



        public BitmapSource CurrentIcon
        {
            get => Explorer?.CurrentFolderItem?.Icon;
        }

        public ObservableCollection<FSExplorerItem> CurrentPathHidden { get; set; }
        public ObservableCollection<FSExplorerItem> CurrentPathVisible { get; set; }
        public Visibility HiddenPathVisibility { get; set; }
        static FSExplorerNavbar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FSExplorerNavbar), new FrameworkPropertyMetadata(typeof(FSExplorerNavbar)));
        }


        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnExplorerNavigated(FSExplorer sender, FSExplorerEventArgs e)
        {
            CurrentPathChanged();
        }

        public double MaxContentWidth { get => AddressBarWidth - 20; }
        void CurrentPathChanged()
        {
            if (Explorer == null || Explorer.CurrentFolder == null) return;
            var path = Explorer.CurrentFolderItem;
            double visibleW = 0;
            

            var visible = new ObservableCollection<FSExplorerItem>();
            var hidden = new ObservableCollection<FSExplorerItem>();


            do
            {
                visibleW += ExplorerBreadCrumbItem.EvaluateSize(path).Width;
                if(visibleW <= MaxContentWidth || visible.Count == 0) visible.Insert(0, path);
                else hidden.Add(path);

                path = path.ParentDirectory;

            } while (path != null);
            HiddenPathVisibility = (hidden.Count > 0) ? Visibility.Visible : Visibility.Collapsed;

            if (!_backforwardNavButtonPressed && _currentItem != null)
            {
                _navbackstack.Push(_currentItem);
                _navfwdstack.Clear();
            }
            _backforwardNavButtonPressed = false;

            CurrentPathVisible = visible;
            CurrentPathHidden = hidden;

            _currentAddress = Explorer.CurrentFolderItem.Info.FullName;
            if (_currentAddress.StartsWith("::")) CurrentAddress = Explorer.CurrentFolderItem.Info.Label;

            NotifyPropertyChanged("CurrentPathVisible");
            NotifyPropertyChanged("CurrentPathHidden");
            NotifyPropertyChanged("HiddenPathVisibility");
            NotifyPropertyChanged("CurrentAddress");
            NotifyPropertyChanged("CurrentFolderItem");
            NotifyPropertyChanged("CurrentIcon");
            NotifyPropertyChanged("CanNavBack");
            NavBackCmd.RaiseCanExecuteChanged();
            NotifyPropertyChanged("CanNavFwd");
            NavFwdCmd.RaiseCanExecuteChanged();
            NotifyPropertyChanged("CanNavParent");
            NavParentCmd.RaiseCanExecuteChanged();

            _currentItem = Explorer.CurrentFolderItem;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            NotifyPropertyChanged("NavButtonsWidth");
            NotifyPropertyChanged("AddressBarWidth");
            CurrentPathChanged();
        }

        internal void OnAddressBarClicked(object sender, MouseButtonEventArgs e)
        {
            AddressBarVisibility = Visibility.Visible;
            BreadcrumbBarVisibility = Visibility.Hidden;
            NotifyPropertyChanged("AddressBarVisibility");
            NotifyPropertyChanged("BreadcrumbBarVisibility");
        }

        internal void AddressBarLostFocus(object sender, EventArgs e)
        {
            string address = (sender as TextBox).Text;
            if(Explorer.CurrentFolderItem.Info.FullName.ToLowerInvariant() != address.ToLowerInvariant())
            {
                var dir = new DirectoryInfoEx(address);
                if(dir.Exists)
                {
                    Explorer.NavigateTo(dir);
                    return;
                }
                else
                {
                    _currentAddress = Explorer.CurrentFolderItem.Info.FullName;
                    if (_currentAddress.StartsWith("::")) CurrentAddress = Explorer.CurrentFolderItem.Info.Label;
                }
            }

            AddressBarVisibility = Visibility.Hidden;
            BreadcrumbBarVisibility = Visibility.Visible;
            NotifyPropertyChanged("AddressBarVisibility");
            NotifyPropertyChanged("BreadcrumbBarVisibility");
            NotifyPropertyChanged("CurrentAddress");
        }

        public Visibility AddressBarVisibility { get; set; }
        public Visibility BreadcrumbBarVisibility { get; set; }

        bool _backforwardNavButtonPressed = false;
        private Stack<FSExplorerItem> _navbackstack = new Stack<FSExplorerItem>();
        private Stack<FSExplorerItem> _navfwdstack = new Stack<FSExplorerItem>();

        public bool CanNavBack { get => _navbackstack.Count > 0; }
        public bool CanNavFwd { get => _navfwdstack.Count > 0; }
        public bool CanNavParent { get => (Explorer == null || Explorer.CurrentFolderItem == null || Explorer.CurrentFolderItem.ParentDirectory == null)? false : true; }

        NavBackCommand _navBackCmd;
        public NavBackCommand NavBackCmd
        {
            get
            {
                if (_navBackCmd == null) _navBackCmd = new NavBackCommand(this);
                return _navBackCmd;
            }
        }
        public void NavBack()
        {
            if (_navbackstack.Count <= 0) return;
            _backforwardNavButtonPressed = true;
            _navfwdstack.Push(Explorer.CurrentFolderItem);
            Explorer.NavigateTo(_navbackstack.Pop().Info);
        }

        NavFwdCommand _navFwdCmd;
        public NavFwdCommand NavFwdCmd
        {
            get
            {
                if (_navFwdCmd == null) _navFwdCmd = new NavFwdCommand(this);
                return _navFwdCmd;
            }
        }
        public void NavFwd()
        {
            if (_navfwdstack.Count <= 0) return;
            _backforwardNavButtonPressed = true;
            _navbackstack.Push(Explorer.CurrentFolderItem);
            Explorer.NavigateTo(_navfwdstack.Pop().Info);
        }

        NavParentCommand _navParentCmd;
        public NavParentCommand NavParentCmd {
            get
            {
                if (_navParentCmd == null) _navParentCmd = new NavParentCommand(this);
                return _navParentCmd;
            }
        }
        public void NavParent()
        {
            Explorer.NavigateTo(Explorer.CurrentFolderItem.ParentDirectory.Info);
        }

    }


    public class NavBackCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        internal FSExplorerNavbar item;
        internal NavBackCommand(FSExplorerNavbar item) { this.item = item; }

        public bool CanExecute(object parameter) { return item.CanNavBack; }
        public void Execute(object parameter) { item.NavBack(); }
        public void RaiseCanExecuteChanged() { CanExecuteChanged?.Invoke(this, new EventArgs()); }
    }

    public class NavFwdCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        internal FSExplorerNavbar item;
        internal NavFwdCommand(FSExplorerNavbar item) { this.item = item; }

        public bool CanExecute(object parameter) { return item.CanNavFwd; }
        public void Execute(object parameter) { item.NavFwd(); }
        public void RaiseCanExecuteChanged() { CanExecuteChanged?.Invoke(this, new EventArgs()); }
    }

    public class NavParentCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        internal FSExplorerNavbar item;
        internal NavParentCommand(FSExplorerNavbar item) { this.item = item; }

        public bool CanExecute(object parameter) { return item.CanNavParent; }
        public void Execute(object parameter) { item.NavParent(); }
        public void RaiseCanExecuteChanged() { CanExecuteChanged?.Invoke(this, new EventArgs()); }
    }
}
