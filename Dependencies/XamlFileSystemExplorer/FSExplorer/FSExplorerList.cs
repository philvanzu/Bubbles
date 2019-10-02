using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XamlFSExplorer
{
    public class FSExplorerList : Control, INotifyPropertyChanged
    {
        #region DependencyProperty Content

        // ////////////////////////////
        // FSExplorer DependencyProperty
        public static readonly DependencyProperty ExplorerProperty =
            DependencyProperty.Register("Explorer", typeof(FSExplorer), typeof(FSExplorerList),
            new FrameworkPropertyMetadata(null, OnExplorerChanged) { BindsTwoWayByDefault = true });

        private static void OnExplorerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var list = d as FSExplorerList;
            if (list.Explorer != null) list.Explorer.Navigated -= list.OnExplorerNavigated;
            if (e.NewValue is FSExplorer explorer) 
            {
                if(list.ShowParentFolder) explorer.ShowListParentFolder = true;
                explorer.Navigated += list.OnExplorerNavigated;
                list.SetSortTrianglesVisibility(true);
            }
        }



        /// <value> The FileSystemExplorer object  </value>
        public FSExplorer Explorer
        {
            get { return (FSExplorer)GetValue(ExplorerProperty); }
            set { SetValue(ExplorerProperty, value);  }
        }

        // /////////////////////////////////////
        // SelectedItem DependencyProperty
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(FileSystemInfoEx), typeof(FSExplorerList),
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
                NotifyPropertyChanged("SelectedItem");
            }
        }

        // /////////////////////////////////////
        // Show Checkbox DependencyProperty
        public static readonly DependencyProperty ShowCheckboxProperty =
            DependencyProperty.Register("ShowCheckbox", typeof(bool), typeof(FSExplorerList),
            new FrameworkPropertyMetadata(false,
                  FrameworkPropertyMetadataOptions.AffectsRender |
                  FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        /// <value> Array of FileExplorerItems that are currently selected </value>
        public bool ShowCheckbox
        {
            get { return (bool)GetValue(ShowCheckboxProperty); }
            set { SetValue(ShowCheckboxProperty, value); }
        }

        // /////////////////////////////////////
        // CheckedItems DependencyProperty
        public static readonly DependencyProperty CheckedItemsProperty =
            DependencyProperty.Register("CheckedItems", typeof(ObservableCollection<FileSystemInfoEx>), typeof(FSExplorerList),
            new FrameworkPropertyMetadata(null, OnCheckedItemsChanged) { BindsTwoWayByDefault = true });

        private static void OnCheckedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var fsexplorer = d as FSExplorerList;
            var explorer = fsexplorer.Explorer;
            var value = e.NewValue as ObservableCollection<FileSystemInfoEx>;
            var test = fsexplorer.CheckedItems;
            if (explorer != null)
            {
                if (explorer.CheckedItems == null || explorer.CheckedItems.Equals(value) != true)
                    explorer.CheckedItems = (ObservableCollection<FileSystemInfoEx>)e.NewValue;
            }
        }

        /// <value> Currently checked items (FileSystemInfoEx)</value>
        public ObservableCollection<FileSystemInfoEx> CheckedItems
        {
            get { return (ObservableCollection<FileSystemInfoEx>)GetValue(CheckedItemsProperty); }
            set { SetValue(CheckedItemsProperty, value); }
        }


        // /////////////////////////////////////
        // ShowFiles DependencyProperty
        public static readonly DependencyProperty ShowFilesProperty =
            DependencyProperty.Register("ShowFiles", typeof(bool), typeof(FSExplorerList),
            new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        /// <value> Array of FileExplorerItems that are currently selected </value>
        public bool ShowFiles
        {
            get { return (bool)GetValue(ShowFilesProperty); }
            set { SetValue(ShowFilesProperty, value); }
        }

        // /////////////////////////////////////
        // ViewDetails DependencyProperty
        public static readonly DependencyProperty ViewDetailsProperty =
            DependencyProperty.Register("ViewDetails", typeof(bool), typeof(FSExplorerList),
            new FrameworkPropertyMetadata(false));

        /// <value> listview View type </value>
        public bool ViewDetails
        {
            get { return (bool)GetValue(ViewDetailsProperty); }
            set { SetValue(ViewDetailsProperty, value); }
        }

        // /////////////////////////////////////
        // ViewDetails DependencyProperty
        public static readonly DependencyProperty ShowParentFolderProperty =
            DependencyProperty.Register("ShowParentFolder", typeof(bool), typeof(FSExplorerList),
            new FrameworkPropertyMetadata(null, ShowParentFolderChanged) { BindsTwoWayByDefault = false });

        private static object ShowParentFolderChanged(DependencyObject d, object value)
        {
            var list = d as FSExplorerList;
            if(list.Explorer != null) list.Explorer.ShowListParentFolder = (bool)value; 
            return value;
        }

        /// <value> listview View type </value>
        public bool ShowParentFolder
        {
            get { return (bool)GetValue(ShowParentFolderProperty); }
            set { SetValue(ShowParentFolderProperty, value); }
        }

        public double NameColumnWidth
        {
            get => Math.Max(this.Width - 350, 100); 
        }

        #endregion

        

        static FSExplorerList()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FSExplorerList), new FrameworkPropertyMetadata(typeof(FSExplorerList)));
        }


        public Visibility CheckBoxesVisibility { get => (ShowCheckbox == false) ? Visibility.Collapsed : Visibility.Visible; }


        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private void OnExplorerNavigated(FSExplorer sender, FSExplorerEventArgs e)
        {
            SetSortTrianglesVisibility(true);
        }

        public void OnLVPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Explorer.SelectedListItem == null || Explorer.SelectedListItem.Renaming) return;

            if (e.Key == Key.F2 && Explorer.SelectedListItem.CanDoFileOperations) Explorer.SelectedListItem.StartRenaming();
            else if (e.Key == Key.Delete && Explorer.SelectedListItem.CanDoFileOperations) Explorer.SelectedListItem.Delete();
        }


        public void ListColumnHeaderClicked(string columnName)
        {
            if (Explorer.Comparer.SortField != columnName)
            {
                Explorer.Comparer.SortField = columnName;
                Explorer.Comparer.SortDirection = true;
            }
            else Explorer.Comparer.SortDirection = !Explorer.Comparer.SortDirection;

            Explorer.NotifyPropertyChanged("ListItemsCV");
            SetSortTrianglesVisibility();
        }

        public Visibility SortAscNameVis { get; set; }
        public Visibility SortDesNameVis { get; set; }
        public Visibility SortAscModifiedVis { get; set; }
        public Visibility SortDesModifiedVis { get; set; }
        public Visibility SortAscTypeVis { get; set; }
        public Visibility SortDesTypeVis { get; set; }
        public Visibility SortAscSizeVis { get; set; }
        public Visibility SortDesSizeVis { get; set; }

        void SetSortTrianglesVisibility(bool hideall = false)
        {
            SortAscNameVis = Visibility.Collapsed;
            SortDesNameVis = Visibility.Collapsed;
            SortAscModifiedVis = Visibility.Collapsed;
            SortDesModifiedVis = Visibility.Collapsed;
            SortAscTypeVis = Visibility.Collapsed;
            SortDesTypeVis = Visibility.Collapsed;
            SortAscSizeVis = Visibility.Collapsed;
            SortDesSizeVis = Visibility.Collapsed;

            if (!hideall)
            {
                switch (Explorer.Comparer.SortField)
                {
                    case "Name":
                        SortAscNameVis = (Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        SortDesNameVis = (!Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case "Modified":
                        SortAscModifiedVis = (Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        SortDesModifiedVis = (!Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case "Type":
                        SortAscTypeVis = (Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        SortDesTypeVis = (!Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case "Size":
                        SortAscSizeVis = (Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        SortDesSizeVis = (!Explorer.Comparer.SortDirection) ? Visibility.Visible : Visibility.Collapsed;
                        break;
                }
            }
            NotifyPropertyChanged("SortAscNameVis");
            NotifyPropertyChanged("SortDesNameVis");
            NotifyPropertyChanged("SortAscModifiedVis");
            NotifyPropertyChanged("SortDesModifiedVis");
            NotifyPropertyChanged("SortAscTypeVis");
            NotifyPropertyChanged("SortDesTypeVis");
            NotifyPropertyChanged("SortAscSizeVis");
            NotifyPropertyChanged("SortDesSizeVis");
        }

    }
}
