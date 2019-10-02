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
using XamlFSExplorer;

namespace XamlFileSystemExplorerTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        FileSystemInfoEx selected = null;
        public FileSystemInfoEx SelectedTreeItem
        {
            get => selected;
            set {
                selected = value;
                NotifyPropertyChanged("SelectedTreeItem");
            }
        }

        FileSystemInfoEx selectedListItem = null;
        public FileSystemInfoEx SelectedListItem
        {
            get => selectedListItem;
            set
            {
                selectedListItem = value;
                NotifyPropertyChanged("SelectedListItem");
            }
        }

        ObservableCollection<FileSystemInfoEx> _checkedItems = new ObservableCollection<FileSystemInfoEx>();
        public ObservableCollection<FileSystemInfoEx> CheckedItems
        {
            get => _checkedItems;
            set => _checkedItems = value;
        }

        FSExplorer _theExplorer = null;
        public FSExplorer TheExplorer
        {
            get => _theExplorer;
            set => _theExplorer = value;
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            TheExplorer = new XamlFSExplorer.FSExplorer(new DirectoryInfoEx[] { FSExplorer.MyComputerDirectory, FSExplorer.LibrariesDirectory, new DirectoryInfoEx("D:\\BD") })
            {
                HideAttributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Offline,
                ShowDisabled = true
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            TheExplorer.Dispose();
            base.OnClosing(e);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void SetSelectedButton(object sender, RoutedEventArgs e)
        {
            var selected = CheckedList.SelectedItem as FileSystemInfoEx;
            SelectedTreeItem = selected;
        }
    }
}
