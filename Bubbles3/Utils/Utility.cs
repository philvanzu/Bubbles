using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Bubbles3.Utils
{
    public static class Utility
    {

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static void InvokeIfRequired(this Dispatcher dispatcher, DispatcherPriority priority, Action action)
        {
            if (Thread.CurrentThread != dispatcher.Thread)
            {
                dispatcher.Invoke(priority, action);
            }
            else action();
        }

        public static void BeginInvokeIfRequired(this Dispatcher dispatcher, DispatcherPriority priority, Action action)
        {
            if (Thread.CurrentThread != dispatcher.Thread)
            {
                dispatcher.BeginInvoke(priority, action);
            }
            else action();
        }
        /// <summary>
        /// Checks the stream header if it matches with
        /// any of the supported image file types.
        /// </summary>
        /// <param name="stream">An open stream pointing to an image file.</param>
        /// <returns>true if the stream is an image file (BMP, TIFF, PNG, GIF, JPEG, WMF, EMF, ICO, CUR);
        /// false otherwise.</returns>
        internal static bool IsImage(Stream stream)
        {
            // Sniff some bytes from the start of the stream
            // and check against magic numbers of supported 
            // image file formats
            byte[] header = new byte[8];
            stream.Seek(0, SeekOrigin.Begin);
            if (stream.Read(header, 0, header.Length) != header.Length)
                return false;

            // BMP
            string bmpHeader = Encoding.ASCII.GetString(header, 0, 2);
            if (bmpHeader == "BM") // BM - Windows bitmap
                return true;
            else if (bmpHeader == "BA") // BA - Bitmap array
                return true;
            else if (bmpHeader == "CI") // CI - Color Icon
                return true;
            else if (bmpHeader == "CP") // CP - Color Pointer
                return true;
            else if (bmpHeader == "IC") // IC - Icon
                return true;
            else if (bmpHeader == "PT") // PI - Pointer
                return true;

            // TIFF
            string tiffHeader = Encoding.ASCII.GetString(header, 0, 4);
            if (tiffHeader == "MM\x00\x2a") // Big-endian
                return true;
            else if (tiffHeader == "II\x2a\x00") // Little-endian
                return true;

            // PNG
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return true;

            // GIF
            string gifHeader = Encoding.ASCII.GetString(header, 0, 4);
            if (gifHeader == "GIF8")
                return true;

            // JPEG
            if (header[0] == 0xFF && header[1] == 0xD8)
                return true;

            // WMF
            if (header[0] == 0xD7 && header[1] == 0xCD && header[2] == 0xC6 && header[3] == 0x9A)
                return true;

            // EMF
            if (header[0] == 0x01 && header[1] == 0x00 && header[2] == 0x00 && header[3] == 0x00)
                return true;

            // Windows Icons
            if (header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x01 && header[3] == 0x00) // ICO
                return true;
            else if (header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x02 && header[3] == 0x00) // CUR
                return true;

            return false;
        }
    }


    internal static class SafeNativeMethods
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string psz1, string psz2);
    }

    public sealed class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string a, string b)
        {
            return SafeNativeMethods.StrCmpLogicalW(a, b);
        }
    }
    

}
