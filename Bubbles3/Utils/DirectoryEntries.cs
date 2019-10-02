using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.IO;
using System.Collections;

namespace Bubbles3.Utils
{
    /// <summary>
    /// ENUMERATE Files and subdirectories in a given FileSystem Directory
    /// </summary>
    public class DirectoryEntries : IEnumerable<WIN32_FIND_DATA>
    {
        private readonly string _currentFolderPath;
        private readonly String _filter;

        public DirectoryEntries(string currentFolderPath, string filter)
        {
            _currentFolderPath = currentFolderPath;
            _filter = filter;
        }

        public IEnumerator<WIN32_FIND_DATA> GetEnumerator()
        {
            return new FastDirectoryEnumerator(_currentFolderPath, _filter);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new FastDirectoryEnumerator(_currentFolderPath, _filter);
        }
    }






    [System.Security.SuppressUnmanagedCodeSecurity]
    public class FastDirectoryEnumerator : IEnumerator<WIN32_FIND_DATA>, IDisposable
    {
        // Wraps a FindFirstFile handle.
        private sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            [DllImport("kernel32.dll")]
            private static extern bool FindClose(IntPtr handle);

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            internal SafeFindHandle() : base(true) { }

            /// <summary>
            /// When overridden in a derived class, executes the code required to free the handle.
            /// </summary>
            /// <returns>
            /// true if the handle is released successfully; otherwise, in the 
            /// event of a catastrophic failure, false. In this case, it 
            /// generates a releaseHandleFailed MDA Managed Debugging Assistant.
            /// </returns>
            protected override bool ReleaseHandle()
            {
                return FindClose(base.handle);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFindHandle FindFirstFile(String fileName, [In, Out]WIN32_FIND_DATA data);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool FindNextFile(SafeFindHandle hndFindFile, [In, Out, MarshalAs(UnmanagedType.LPStruct)]WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFindHandle FindFirstFileEx(String fileName, int infoLevel, [In, Out]WIN32_FIND_DATA data, int searchScope, String notUsedNull, int additionalFlags);

        string _currentFolderPath;
        WIN32_FIND_DATA _findData;
        SafeFindHandle _hndFile;

        int _infoLevel = 1;
        int _additionalFlags = 0;
        int _searchScope = 0;
        string _filter;

        public FastDirectoryEnumerator(string currentFolderPath, string filter)
        {
            _currentFolderPath = currentFolderPath;
            _filter = filter;
            Reset();
        }

        public WIN32_FIND_DATA Current { get { return _findData; } }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            if (_hndFile != null)
            {
                _hndFile.Dispose();
                _hndFile = null;
            }
        }

        public bool MoveNext()
        {
            if (_hndFile != null && !_hndFile.IsInvalid) // e.g. unaccessible C:\System Volume Information or filter like *.txt in a directory with no text files
            {
                return FindNextFile(_hndFile, _findData);

            }
            return false;
        }

        public void Reset()
        {
            _findData = new WIN32_FIND_DATA();
            if (_hndFile != null) _hndFile.Dispose();
            new FileIOPermission(FileIOPermissionAccess.PathDiscovery, _currentFolderPath).Demand();
            String searchPath = System.IO.Path.Combine(_currentFolderPath, _filter);
            _hndFile = FindFirstFileEx(searchPath, _infoLevel, _findData, _searchScope, null, _additionalFlags);
        }
    }


    /// <summary>
    /// Contains information about the file that is found by the FindFirstFile or FindNextFile functions.
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto), BestFitMapping(false)]
    public class WIN32_FIND_DATA
    {
        public FileAttributes dwFileAttributes;
        public uint ftCreationTime_dwLowDateTime;
        public uint ftCreationTime_dwHighDateTime;
        public uint ftLastAccessTime_dwLowDateTime;
        public uint ftLastAccessTime_dwHighDateTime;
        public uint ftLastWriteTime_dwLowDateTime;
        public uint ftLastWriteTime_dwHighDateTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public int dwReserved0;
        public int dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;

        public override String ToString()
        {
            return "File name=" + cFileName;
        }
        public DateTime CreationTime => (ConvertDateTime(ftCreationTime_dwHighDateTime, ftCreationTime_dwLowDateTime)).ToLocalTime();
        public DateTime LastAccessTime => (ConvertDateTime(ftLastAccessTime_dwHighDateTime, ftLastAccessTime_dwLowDateTime)).ToLocalTime();
        public DateTime LastWriteTime => (ConvertDateTime(ftLastWriteTime_dwHighDateTime, ftLastWriteTime_dwLowDateTime)).ToLocalTime();
        public long Length => CombineHighLowInts(nFileSizeHigh, nFileSizeLow);

        protected static long CombineHighLowInts(uint high, uint low)
        {
            return (((long)high) << 0x20) | low;
        }

        protected static DateTime ConvertDateTime(uint high, uint low)
        {
            long fileTime = CombineHighLowInts(high, low);
            return DateTime.FromFileTimeUtc(fileTime);
        }
    }
}
