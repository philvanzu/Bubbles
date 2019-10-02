using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bubbles3.Utils
{
    public static class BblRegistryKey
    {
        public static readonly string registryKeyString = "SOFTWARE\\philvanzu\\Bubbles3";
        public static RegistryKey GetKey()
        {
            RegistryKey registryKey = null;
            try
            {
                registryKey = Registry.CurrentUser.OpenSubKey(registryKeyString, true);
                if (registryKey == null) registryKey = Registry.CurrentUser.CreateSubKey(registryKeyString);
            }
            catch
            { Console.WriteLine("Open Registry key failed in ShellView window ctor."); }

            return registryKey;
        }
    }
}
