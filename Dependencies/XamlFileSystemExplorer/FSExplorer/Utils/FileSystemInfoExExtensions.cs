using System;
using System.IO;
using System.Runtime.InteropServices;

namespace System.IO
{
    public static class FileSystemInfoExExtensions
    {
        public static string MimeType(this FileSystemInfoEx info)
        {
            if (info is DirectoryInfoEx) return "Folder";
            else if (info is FileInfoEx finfo)
            {
                if (info.IsSymLink()) return "Shortcut";
                var ext = finfo.Extension;
                Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
                if (regKey != null && regKey.GetValue("Content Type") != null)
                    return regKey.GetValue("Content Type").ToString();

                return info.Extension;
            }
            return string.Empty;
        }

        public static string TypeName(this FileSystemInfoEx info)
        {
            if (info == null || info.Exists == false) return string.Empty;

            SHFILEINFO shFileInfo = new SHFILEINFO();
            uint dwFileAttributes = FILE_ATTRIBUTE_NORMAL;
            uint uFlags = (uint)(SHGFI_PIDL | SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES);
            info.RequestPIDL(pidl => { if (pidl != null) SHGetFileInfo(pidl.Ptr, dwFileAttributes, ref shFileInfo, (uint)Marshal.SizeOf(shFileInfo), uFlags); return; });

            return shFileInfo.szTypeName;
        }

        public static bool IsSystemFolder(this FileSystemInfoEx info)
        {
            if (info is DirectoryInfoEx dir)
            {
                return dir.Attributes.HasFlag(FileAttributes.System);
            }
            return false;
        }
        public static bool IsSymLink(this FileSystemInfoEx info)
        {
            if (info.IsFolder) return false;
            var path = info.FullName;
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(directory);
            Shell32.FolderItem folderItem = folder.ParseName(file);

            if (folderItem != null)
            {
                return folderItem.IsLink;
            }

            return false;
        }

        public static FileSystemInfoEx ResolveSymLink(this FileSystemInfoEx info)
        {
            var path = info.FullName;
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(directory);
            Shell32.FolderItem folderItem = folder.ParseName(file);

            Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;

            FileSystemInfoEx ret = null;
            if (Path.GetDirectoryName(link.Path) == link.Path) ret = new DirectoryInfoEx(link.Path);
            else ret = new FileInfoEx(link.Path);
            return ret;
        }
        
        //native methods, structs & classes
        [StructLayout(LayoutKind.Sequential)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };


        // The CharSet must match the CharSet of the corresponding PInvoke signature
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        //public enum FileAttributes
        //{
        //    FILE_ATTRIBUTE_ARCHIVE = 0x20,
        //    FILE_ATTRIBUTE_COMPRESSED = 0x800,
        //    FILE_ATTRIBUTE_DEVICE = 0x40,
        //    FILE_ATTRIBUTE_DIRECTORY = 0x10,
        //    FILE_ATTRIBUTE_ENCRYPTED = 0x4000,
        //    FILE_ATTRIBUTE_HIDDEN = 0x2,
        //    FILE_ATTRIBUTE_INTEGRITY_STREAM = 0x8000,
        //    FILE_ATTRIBUTE_NORMAL = 0x80,
        //    FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x2000,
        //    FILE_ATTRIBUTE_NO_SCRUB_DATA = 0x20000,
        //    FILE_ATTRIBUTE_OFFLINE = 0x1000,
        //    FILE_ATTRIBUTE_READONLY = 0x1,
        //    FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 0x400000,
        //    FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x40000,
        //    FILE_ATTRIBUTE_REPARSE_POINT = 0x400,
        //    FILE_ATTRIBUTE_SPARSE_FILE = 0x200,
        //    FILE_ATTRIBUTE_SYSTEM = 0x4,
        //    FILE_ATTRIBUTE_TEMPORARY = 0x100,
        //    FILE_ATTRIBUTE_VIRTUAL = 0x10000
        //}

        const uint SHGFI_ADDOVERLAYS = 0x000000020;
        const uint SHGFI_ATTR_SPECIFIED = 0x000020000;
        const uint SHGFI_ATTRIBUTES = 0x000000800;
        const uint SHGFI_DISPLAYNAME = 0x000000200;
        const uint SHGFI_EXETYPE = 0x000002000;
        const uint SHGFI_ICON = 0x000000100;
        const uint SHGFI_ICONLOCATION = 0x000001000;
        const uint SHGFI_LARGEICON = 0x000000000;
        const uint SHGFI_LINKOVERLAY = 0x000008000;
        const uint SHGFI_OPENICON = 0x000000002;
        const uint SHGFI_OVERLAYINDEX = 0x000000040;
        const uint SHGFI_PIDL = 0x000000008;
        const uint SHGFI_SELECTED = 0x000010000;
        const uint SHGFI_SHELLICONSIZE = 0x000000004;
        const uint SHGFI_SMALLICON = 0x000000001;
        const uint SHGFI_SYSICONINDEX = 0x000004000;
        const uint SHGFI_TYPENAME = 0x000000400;
        const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;

        [DllImport("shell32.dll")]
        static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("shell32.dll")]
        static extern IntPtr SHGetFileInfo(IntPtr pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern uint GetFileAttributes(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

    }

}

