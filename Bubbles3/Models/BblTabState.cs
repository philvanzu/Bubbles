using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bubbles3.ViewModels;
using Bubbles3.Views;
using Bubbles3.Utils;
using Microsoft.Win32;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;

namespace Bubbles3.Models
{
    [System.Serializable]
    public class BblTabState
    {
        public string displayName = "New Tab";
        public bool isActive;
        public string navigated;
        public bool saveToDB;
        public string windowedOptions;
        public string fullscreenOptions;
        public string booksSort= "Creation Time";
        public bool booksSortDirection=false;
        public string pagesSort="Natural";
        public bool pagesSortDirection=true;
        public BblBookmark currentBookmark;
        public List<BblBookmark> savedBookmarks = new List<BblBookmark>();
        public bool showIvp;

        public TabUIState uiState = new TabUIState()
        {
            pageVisible = true,
            explorerVisible = true,
            bookViewVisible = true,
            page = new SerializableGridLength(new System.Windows.GridLength(0.33, System.Windows.GridUnitType.Star)),
            explorer = new SerializableGridLength(new System.Windows.GridLength(0.33, System.Windows.GridUnitType.Star)),
            bookview = new SerializableGridLength(new System.Windows.GridLength(0.5, System.Windows.GridUnitType.Star)),
            doLoad = true
        };

        public static void WriteToRegistry(List<BblTabState> tabStates)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(stream, tabStates);
                    var data = stream.ToArray();
                    BblRegistryKey.GetKey().SetValue("TabStates", data, RegistryValueKind.Binary);
                }
            }
            catch { Console.WriteLine("Failed to write tabStates to Registry"); }
        }
        public static List<BblTabState> ReadFromRegistry()
        {
            List<BblTabState> tabStates = new List<BblTabState>();
            
            try
            {
                byte[] data = BblRegistryKey.GetKey().GetValue("TabStates") as byte[];
                if (data != null)
                {
                    using (MemoryStream stream = new MemoryStream(data))
                    {
                        BinaryFormatter bin = new BinaryFormatter();

                        tabStates = (List<BblTabState>)bin.Deserialize(stream);
                    }
                }
            }
            catch { Console.WriteLine("Failed to retrieve tabStates from registry."); }
            
            
            return tabStates;
        }
    }

    [System.Serializable]
    public struct BblBookmark
    {
        public string libraryPath;
        public string bookPath;
        public string pageFilename;
        public override string ToString()
        {
            return Path.GetFileNameWithoutExtension(bookPath) + "::" + Path.GetFileNameWithoutExtension(pageFilename);
        }

        public bool destroyOnClose;
    }

    [System.Serializable]
    public class TabUIState
    {
        public bool pageVisible = true;
        public bool explorerVisible = true;
        public bool bookViewVisible = true;
        public SerializableGridLength page = new SerializableGridLength(new GridLength(0.33, GridUnitType.Star));
        public SerializableGridLength explorer = new SerializableGridLength(new GridLength(0.33, GridUnitType.Star));
        public SerializableGridLength bookview = new SerializableGridLength(new GridLength(0.5, GridUnitType.Star));
        public bool doLoad = false;
    }
}
