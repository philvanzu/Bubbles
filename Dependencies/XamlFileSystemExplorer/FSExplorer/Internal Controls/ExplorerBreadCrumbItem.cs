using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace XamlFSExplorer
{
    internal class ExplorerBreadCrumbItem : ContentControl , INotifyPropertyChanged
    {
        //The FSExplorerItem
        public static readonly DependencyProperty DirectoryProperty =
            DependencyProperty.Register("Directory", typeof(FSExplorerItem), typeof(ExplorerBreadCrumbItem),
                new FrameworkPropertyMetadata(null, OnDirectoryChanged) { BindsTwoWayByDefault = false });

        private static void OnDirectoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e )
        {
            var item = d as ExplorerBreadCrumbItem;
            item.NotifyPropertyChanged("Directory");
            item.NotifyPropertyChanged("BrowseButtonText");
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

        public string BrowseButtonText { get => Directory?.Info?.Label; }

        public ObservableCollection<FSExplorerItem> _children = new ObservableCollection<FSExplorerItem>();
        public ObservableCollection<FSExplorerItem> Children
        {
            get
            {
                _children.Clear();
                foreach (var d in Directory.Directories)
                    _children.Add(d);

                return _children;
            }
        }
        public Visibility ChildrenButtonVisibility { get => (Directory.Explorer.CurrentFolderItem == Directory)? Visibility.Collapsed : Visibility.Visible; }

        public BrowseButtonClickedCommand BrowseButtonClickedCmd { get => new BrowseButtonClickedCommand(Directory); }

        public static Size EvaluateSize(FSExplorerItem fsei)
        {
            var formattedText = new FormattedText(
                fsei.Info.Label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal),
                SystemFonts.MessageFontSize,
                Brushes.Black);

            return new Size(formattedText.Width + 20, formattedText.Height + 8);
        }

    }
}
