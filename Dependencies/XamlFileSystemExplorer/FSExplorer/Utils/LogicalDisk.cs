using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Management;
using System.Threading;

namespace XamlFSExplorer
{
    public delegate void LogicalDiskArrayModifiedHandler(LogicalDisk.DriveEventType type, string driveName);

    [Serializable]
    public class LogicalDisk
    {
        [Serializable]
        public enum DriveEventType
        { Created, Deleted, Inserted, Ejected }

        [Serializable]
        public enum DiskType
        {
            Unknown,
            NoRootDirectory,
            Removable,
            Local,
            Network,
            CD,
            RAM
        } //DiskType 



        public string Name;
        public long freespace;
        public long size;
        public DiskType driveType;
        public string volumeName;
        public string networkPath;
        public string volumeSerialNumber;
        public bool IsEnabled;


        //public bool IsEnabled { get => statusInfo == StatusInfo.Enabled; }

        public static Thread MonitoringThread = null;

        public static event LogicalDiskArrayModifiedHandler LogicalDiskArrayModified;

        public LogicalDisk()
        {
            if(MonitoringThread == null)
            {
                MonitoringThread = new Thread(new ParameterizedThreadStart(o =>
                {
                    WqlEventQuery q;
                    ManagementOperationObserver observer = new ManagementOperationObserver();

                    ManagementScope scope = new ManagementScope("root\\CIMV2");
                    scope.Options.EnablePrivileges = true;

                    q = new WqlEventQuery()
                    {
                        EventClassName = "__InstanceOperationEvent",
                        WithinInterval = new TimeSpan(0, 0, 3),
                        Condition = @"TargetInstance ISA 'Win32_LogicalDisk' "
                    };
                    var w = new ManagementEventWatcher(scope, q);

                    w.EventArrived += new EventArrivedEventHandler(W_EventArrived);
                    w.Start();
                }));
                MonitoringThread.Start();
            }
        }

        static void W_EventArrived(object sender, EventArrivedEventArgs e)
        {
            //Get the Event object and display its properties (all)
            foreach (PropertyData pd in e.NewEvent.Properties)
            {
                ManagementBaseObject baseObject = (ManagementBaseObject)e.NewEvent;
                ManagementBaseObject wmiDevice = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string name = (string)wmiDevice["Name"];

                if (baseObject.ClassPath.ClassName.Equals("__InstanceCreationEvent"))
                {
                    DiskCreated(name);
                    LogicalDiskArrayModified(DriveEventType.Created, name);
                }
                else if (baseObject.ClassPath.ClassName.Equals("__InstanceDeletionEvent"))
                {
                    DiskDeleted(name);
                    LogicalDiskArrayModified(DriveEventType.Deleted, name);
                }
                else if (baseObject.ClassPath.ClassName.Equals("__InstanceModificationEvent"))
                {
                    ManagementBaseObject previous = (ManagementBaseObject)e.NewEvent["PreviousInstance"];

                    if (wmiDevice.Properties["VolumeName"].Value != null && Disks[name].IsEnabled == false)
                    {
                        Disks[name].IsEnabled = true;
                        LogicalDiskArrayModified(DriveEventType.Inserted, name);
                    }
                    else if(Disks[name].IsEnabled)
                    {
                        Disks[name].IsEnabled = false;
                        LogicalDiskArrayModified(DriveEventType.Ejected, name);
                    }
                }
            }
        }

        public static Dictionary<string, LogicalDisk> _disks = null;
        public static Dictionary<string, LogicalDisk> Disks
        {
            get
            {
                if(_disks == null)
                {
                    _disks = new Dictionary<string, LogicalDisk>();
                    string query = "Select * from Win32_LogicalDisk";

                    ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                    ManagementObjectCollection results = searcher.Get();

                    foreach (ManagementObject diskObject in results)
                    {


                        LogicalDisk currentDisk = new LogicalDisk()
                        {
                            Name = (string)diskObject["Name"],
                            freespace = Convert.ToInt64(diskObject["FreeSpace"]),
                            size = Convert.ToInt64(diskObject["Size"]),
                            driveType = (DiskType)Convert.ToInt32(diskObject["DriveType"]),
                            volumeName = (string)diskObject["VolumeName"],
                            volumeSerialNumber = (string)diskObject["VolumeSerialNumber"],
                            networkPath = (string)diskObject["ProviderName"], //network path
                            IsEnabled = (string)diskObject["VolumeSerialNumber"] != null
                        };
                        _disks[currentDisk.Name] = currentDisk;
                    }
                }
                return _disks;
            }
        } 

        public void Refresh()
        {
            string query = "Select * from Win32_LogicalDisk Where Name = \"" + Name + "\"";

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection results = searcher.Get();

            foreach (ManagementObject diskObject in results)
            {
                
                Name = (string)diskObject["Name"];
                freespace = Convert.ToInt64(diskObject["FreeSpace"]);
                size = Convert.ToInt64(diskObject["Size"]);
                driveType = (DiskType)Convert.ToInt32(diskObject["DriveType"]);
                volumeName = (string)diskObject["VolumeName"];
                volumeSerialNumber = (string)diskObject["VolumeSerialNumber"];
                networkPath = (string)diskObject["ProviderName"]; //network path
                IsEnabled = (string)diskObject["VolumeSerialNumber"] != null;
            }
        }

        static void DiskCreated(string name)
        {
            string query = "Select * from Win32_LogicalDisk Where Name = \"" + name + "\"";

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection results = searcher.Get();

            foreach (ManagementObject diskObject in results)
            {

                LogicalDisk currentDisk = new LogicalDisk()
                {
                    Name = (string)diskObject["Name"],
                    freespace = Convert.ToInt64(diskObject["FreeSpace"]),
                    size = Convert.ToInt64(diskObject["Size"]),
                    driveType = (DiskType)Convert.ToInt32(diskObject["DriveType"]),
                    volumeName = (string)diskObject["VolumeName"],
                    volumeSerialNumber = (string)diskObject["VolumeSerialNumber"],
                    networkPath = (string)diskObject["ProviderName"], //network path
                    IsEnabled = (string)diskObject["VolumeSerialNumber"] != null
                };
                _disks[currentDisk.Name] = currentDisk;
            }
        }

        static void DiskDeleted(string name)
        {
            if (_disks.ContainsKey(name))
                _disks.Remove(name);
        }

        public static bool operator == (LogicalDisk a, LogicalDisk b)
        {
            if(object.ReferenceEquals(a, null) && object.ReferenceEquals(b, null)) return true;
            if (object.ReferenceEquals(a, null) || object.ReferenceEquals(b, null)) return false;
            return a.volumeSerialNumber == b.volumeSerialNumber;
        }

        public static bool operator != (LogicalDisk a, LogicalDisk b)
        {
            return (!(a == b));
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return volumeSerialNumber.GetHashCode();
        }




    }

}
