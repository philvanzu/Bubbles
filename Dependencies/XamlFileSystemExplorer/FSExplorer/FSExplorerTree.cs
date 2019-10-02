using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace XamlFSExplorer
{

    public class FSExplorerTree : Control, INotifyPropertyChanged
    {
        #region DependencyProperty Content

        // ////////////////////////////
        // FSExplorer DependencyProperty
        public static readonly DependencyProperty ExplorerProperty =
            DependencyProperty.Register("Explorer", typeof(FSExplorer), typeof(FSExplorerTree),
            new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.AffectsRender |
                  FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        /// <value> The FileSystemExplorer object  </value>
        public FSExplorer Explorer
        {
            get { return (FSExplorer)GetValue(ExplorerProperty); }
            set { SetValue(ExplorerProperty, value); }
        }

        // /////////////////////////////////////
        // SelectedItem DependencyProperty
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(FileSystemInfoEx), typeof(FSExplorerTree),
            new FrameworkPropertyMetadata(null, OnSelectedItemChanged) { BindsTwoWayByDefault = true });

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tree = d as FSExplorerTree;
            if (e.NewValue is FileSystemInfoEx value)
            {
                if (tree.Explorer?.SelectedTreeItem?.Info != value)
                {
                    tree.Explorer.Selected = value;
                }
            }
        }

        public FileSystemInfoEx SelectedItem
        {
            get => (FileSystemInfoEx)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value); 
        }

        // /////////////////////////////////////
        // Show Checkbox DependencyProperty
        public static readonly DependencyProperty ShowCheckboxProperty =
            DependencyProperty.Register("ShowCheckbox", typeof(bool), typeof(FSExplorerTree),
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
            DependencyProperty.Register("CheckedItems", typeof(ObservableCollection<FileSystemInfoEx>), typeof(FSExplorerTree),
            new FrameworkPropertyMetadata(null, OnCheckedItemsChanged) { BindsTwoWayByDefault = true });

        private static void OnCheckedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var fsexplorer = d as FSExplorerTree;
            var explorer = fsexplorer.Explorer;
            var value = e.NewValue as ObservableCollection<FileSystemInfoEx>;
            var test = fsexplorer.CheckedItems;
            if (explorer != null )
            { 
                if ( explorer.CheckedItems == null || explorer.CheckedItems.Equals(value) != true)
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
            DependencyProperty.Register("ShowFiles", typeof(bool), typeof(FSExplorerTree),
            new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        /// <value> Array of FileExplorerItems that are currently selected </value>
        public bool ShowFiles
        {
            get { return (bool)GetValue(ShowFilesProperty); }
            set { SetValue(ShowFilesProperty, value); }
        }

        #endregion

        public Visibility CheckBoxesVisibility { get => (ShowCheckbox == false) ? Visibility.Collapsed : Visibility.Visible; }

        static FSExplorerTree()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FSExplorerTree), new FrameworkPropertyMetadata(typeof(FSExplorerTree)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            var fse = (FSExplorer)GetValue(ExplorerProperty);
            //ItemsSource = fse.RootItems;


        }

        

        

    }


}
