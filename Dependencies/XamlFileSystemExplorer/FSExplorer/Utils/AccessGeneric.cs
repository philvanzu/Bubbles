using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace XamlFSExplorer.Utils
{
    class AccessGeneric
    {
        void Access()
        {
            var _dic = new ResourceDictionary();
            _dic.Source = new Uri("pack://application:,,,/XamlFSExplorer;component/Themes/Generic.xaml");
        }
    }
}
