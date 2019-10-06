using Bubbles3.Models;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bubbles3.ViewModels
{
    public class PromoteViewModel : Screen
    {
        public ObservableCollection<PromotableViewModel> Promotables { get; private set; }
        public PromoteViewModel(List<BblLibraryNode> promotables)
        {

            Promotables = new ObservableCollection<PromotableViewModel>();
            foreach (var n in promotables) Promotables.Add(new PromotableViewModel(n, this));
        }

        public void ProcessPromotables()
        {
            while (Promotables.Count > 0)
            {
                var nvm = Promotables.First();
                Promotables.Remove(nvm);

                nvm.Node.PromoteToBookDirectory();
            }
            TryClose();
        }

        public void Cancel()
        {
            TryClose();
        }

        
    }

    public class PromotableViewModel
    {
        public BblLibraryNode Node { get; set; }
        PromoteViewModel _pvm;
        public PromotableViewModel(BblLibraryNode n, PromoteViewModel pvm)
        {
            _pvm = pvm;
            Node = n;
        }

        public ImageSource Icon
        {
            get
            {
                var bmp = new BitmapImage();
                using (var fs = new FileStream(Node.FirstImage, FileMode.Open, FileAccess.Read))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = fs;
                    bmp.EndInit();
                }
                return bmp;
            }
        }
        public string Path => Node.Path;
        public void Remove()
        {
            _pvm.Promotables.Remove(this);
        }
    }
}
