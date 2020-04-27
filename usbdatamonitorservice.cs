using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Tools;

namespace UsbDataMonitor
{
    public partial class usbdatamonitorservice : ServiceBase
    {
        public usbdatamonitorservice()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {

                ManagementEventWatcher eventWatcher2 = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2"));
                eventWatcher2.EventArrived += new EventArrivedEventHandler(Tools.Tools.UsbInsertedEventHandler);
                eventWatcher2.Start();

                ManagementEventWatcher eventWatcher3 = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3"));
                eventWatcher3.EventArrived += new EventArrivedEventHandler(Tools.Tools.UsbRemovedEventHandler);
                eventWatcher3.Start();
            }
            catch (IOException e)
            {
                Tools.Tools.writeWindowsLog(e.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }
            catch (Exception e)
            {
                Tools.Tools.writeWindowsLog(e.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }

            Tools.Tools.writeWindowsLog("Service USB Data Monitor Started", EventLogEntryType.Information, (int)LogCode.ServiceStarted);
            Tools.Tools.InitializeFilter();
        }

        protected override void OnStop()
        {
            // Dispose Dictionnary 
            try
            {
                foreach (String key in Tools.Tools.watcherDict.Keys)
                {
                    if (Tools.Tools.watcherDict[key] != null)
                    {
                        Tools.Tools.watcherDict[key].Dispose();
                    }

                }
            }
            catch (Exception e)
            {
                Tools.Tools.writeWindowsLog(e.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }

            Tools.Tools.writeWindowsLog("Service USB Data Monitor Stopped", EventLogEntryType.Warning, (int)LogCode.ServiceStopped);
        }
    }
}

