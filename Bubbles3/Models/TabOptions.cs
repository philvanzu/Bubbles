using Bubbles3.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Bubbles3.Models
{
    public enum PagesSorts
    {
        Natural,
        Alphabetic,
        LastModified,
        Created
    }

    public enum BooksSorts
    {
        NameNatural,
        NameAlphabetic,
        Path,
        Index,
        Created,
        LastModified,
        Custom
    }

    public enum BblListViewMode
    {
        Detail,
        Thumbnail
    }

    public enum BblZoomMode
    {
        Default,
        Fit,
        FitW,
        FitH,
    }

    [Serializable]
    public class TabOptions
    {



        public String name;
        public String Name { get { return name; } set { name = value; } }
        public bool rememberView;

        public bool keepZoom;
        public BblZoomMode zoomMode;
        public bool showScroll;
        public bool mwScroll;
        public bool showPaging;

        public bool animKeyZoom;
        public bool animScroll;
        public bool animRotation;
        public bool animIVP;

        public bool readBackwards;

        public TimeSpan turnPageBlock;

        public bool zoomRectOnRightClick;

        public BblListViewMode libraryViewMode;
        public BblListViewMode bookViewMode;
        public bool exploreSubFolders = true;
        public bool saveIvps = false;
        public bool savePageMarkers = false;

        [NonSerialized]
        public List<String> customBookFields = new List<String>();

        public TabOptions()
        {
            name = "default";
            rememberView = false;
            saveIvps = false;
            savePageMarkers = false;
            keepZoom = false;
            mwScroll = false;
            readBackwards = false;
            zoomMode = BblZoomMode.Fit;
            showPaging = true;
            showScroll = false;
            animIVP = true;
            animKeyZoom = true;
            animRotation = true;
            animScroll = true;
            turnPageBlock = new TimeSpan(0, 0, 0, 0, 500);
            zoomRectOnRightClick = true;
            libraryViewMode = BblListViewMode.Thumbnail;
            bookViewMode = BblListViewMode.Detail; ;
            exploreSubFolders = true;

            customBookFields = null;
        }
        public TabOptions(TabOptions copy)
        {
            name = copy.name;

            rememberView = copy.rememberView;
            saveIvps = copy.saveIvps;
            savePageMarkers = copy.savePageMarkers;
            keepZoom = copy.keepZoom;
            mwScroll = copy.mwScroll;
            readBackwards = copy.readBackwards;
            zoomMode = copy.zoomMode;
            showPaging = copy.showPaging;
            showScroll = copy.showScroll;
            animIVP = copy.animIVP;
            animKeyZoom = copy.animKeyZoom;
            animRotation = copy.animRotation;
            animScroll = copy.animScroll;
            turnPageBlock = copy.turnPageBlock;
            zoomRectOnRightClick = copy.zoomRectOnRightClick;
            libraryViewMode = copy.libraryViewMode;
            bookViewMode = copy.bookViewMode;
            exploreSubFolders = copy.exploreSubFolders;

            customBookFields = null;
        }

        public void Update(TabOptions copy)
        {
            name = copy.name;

            rememberView = copy.rememberView;
            saveIvps = copy.saveIvps;
            savePageMarkers = copy.savePageMarkers;
            keepZoom = copy.keepZoom;
            mwScroll = copy.mwScroll;
            readBackwards = copy.readBackwards;
            zoomMode = copy.zoomMode;
            showPaging = copy.showPaging;
            showScroll = copy.showScroll;
            animIVP = copy.animIVP;
            animKeyZoom = copy.animKeyZoom;
            animRotation = copy.animRotation;
            animScroll = copy.animScroll;
            turnPageBlock = copy.turnPageBlock;
            zoomRectOnRightClick = copy.zoomRectOnRightClick;
            libraryViewMode = copy.libraryViewMode;
            bookViewMode = copy.bookViewMode;
            exploreSubFolders = copy.exploreSubFolders;

            customBookFields = null;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            TabOptions tp = obj as TabOptions;
            if (tp == null) return false;

            if (tp.name == name) return true;

            return false;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static TabOptions PhotoTabOptions
        {
            get
            {
                return new TabOptions()
                {
                    name = "photos",
                    rememberView = true,
                    saveIvps = true,
                    savePageMarkers = false,
                    keepZoom = false,
                    mwScroll = false,
                    readBackwards = false,
                    zoomMode = BblZoomMode.Fit,
                    showPaging = true,
                    showScroll = false,
                    animIVP = true,
                    animKeyZoom = false,
                    animRotation = false,
                    animScroll = false,
                    turnPageBlock = new TimeSpan(0, 0, 0, 0, 500),
                    zoomRectOnRightClick = true,
                    libraryViewMode = BblListViewMode.Thumbnail,
                    bookViewMode = BblListViewMode.Detail,
                    exploreSubFolders = true,
                    customBookFields = null
                };
            }
        }
        public static TabOptions ComicTabOptions
        {
            get
            {
                return new TabOptions()
                {
                    name = "comics",
                    rememberView = false,
                    saveIvps = false,
                    savePageMarkers = false,
                    keepZoom = true,
                    mwScroll = true,
                    readBackwards = true,
                    zoomMode = BblZoomMode.Fit,
                    showPaging = true,
                    showScroll = true,
                    animIVP = false,
                    animKeyZoom = false,
                    animRotation = false,
                    animScroll = false,
                    turnPageBlock = new TimeSpan(0, 0, 0, 0, 500),
                    zoomRectOnRightClick = false,
                    libraryViewMode = BblListViewMode.Thumbnail,
                    bookViewMode = BblListViewMode.Detail,
                    exploreSubFolders = true,
                    customBookFields = null
                };
            }
        }

        public static void WriteToRegistry(ObservableCollection<TabOptions> options)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(stream, options);
                    var data = stream.ToArray();
                    BblRegistryKey.GetKey().SetValue("savedOptions", data, Microsoft.Win32.RegistryValueKind.Binary);
                }
            }
            catch { Console.WriteLine("Failed to write bookmarks to Registry"); }
        }

        public static ObservableCollection<TabOptions> ReadFromRegistry()
        {
            ObservableCollection<TabOptions> options = null;
            try
            {
                byte[] data = BblRegistryKey.GetKey().GetValue("savedOptions") as byte[];
                if (data != null)
                {
                    using (MemoryStream stream = new MemoryStream(data))
                    {
                        BinaryFormatter bin = new BinaryFormatter();
                        options = (ObservableCollection<TabOptions>)bin.Deserialize(stream);
                    }
                }
            }
            catch { Console.WriteLine("Failed to retrieve bookmarks from registry."); }


            return options;
        }
    }


}
