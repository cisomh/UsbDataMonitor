using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace Tools
{

    enum LogCode : int
    {
        UsbInserted = 7000,
        UsbRemoved = 7001,
        FileWatcher = 7002,
        FileCopied = 7003,
        ServiceStopped = 7005,
        FilterAdd = 7006,
        FilterReplace = 7006,
        FilterList = 7007,
        ServiceStarted = 7098,
        Error = 7999
    }

    public class Tools
    {
        static string[] extensions = new string[] { ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx", ".xlm", ".ppt", ".pptx", ".pdf" };
        public static Dictionary<string, FileSystemWatcher> watcherDict = new Dictionary<string, FileSystemWatcher>();
        public static StringCollection filterList = new StringCollection();
        public static string fl = "";

        public static void writeWindowsLog(string log, EventLogEntryType eType, int eventId)
        {
            string source = "UsbDataMonitor";

            EventLog systemEventLog = new EventLog("Application");

            try
            {
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, "Application");
                }
                systemEventLog.Source = source;
                systemEventLog.WriteEntry(log, eType, eventId);
            }
            catch (System.Security.SecurityException se)
            {
                systemEventLog.WriteEntry(se.Message, EventLogEntryType.Error);
            }
        }

        public static void InitializeFilter()
        {
            string regKeyRoot = @"HKEY_LOCAL_MACHINE\SOFTWARE\UsbMonitor";
            string regKeyAction = "Action";
            string regKeyFilter = "Filter";

            string Action = "";
            string Filter = "";

            filterList.AddRange(extensions);

            try
            {
                if (Registry.GetValue(regKeyRoot, regKeyAction, "") != null)
                {
                    Action = Registry.GetValue(regKeyRoot, regKeyAction, "").ToString().ToLower();
                    //Console.WriteLine("Action = {0}", Action);
                }
                if (Registry.GetValue(regKeyRoot, regKeyAction, "") != null)
                {
                    Filter = Registry.GetValue(regKeyRoot, regKeyFilter, "").ToString().ToLower();
                    //Console.WriteLine("Filter = {0}", Filter);
                }

                if (Action == "add")
                {
                    buildFilter(Filter, false);
                    splitFilter();
                    writeWindowsLog("Add method selected", EventLogEntryType.Information, (int)LogCode.FilterAdd);
                    writeWindowsLog("New filter selection : " + fl, EventLogEntryType.Information, (int)LogCode.FilterList);
                }
                else if (Action == "replace")
                {
                    buildFilter(Filter, true);
                    if (filterList.Count == 0)
                    {
                        filterList.AddRange(extensions);
                    }
                    splitFilter();
                    writeWindowsLog("Replace method selected", EventLogEntryType.Information, (int)LogCode.FilterAdd);
                    writeWindowsLog("New filter selection : " + fl, EventLogEntryType.Information, (int)LogCode.FilterList);
                }
                else
                {
                    Action = "add";
                    buildFilter(Filter, false);
                    splitFilter();
                    writeWindowsLog("Add method selected", EventLogEntryType.Information, (int)LogCode.FilterAdd);
                    writeWindowsLog("New filter selection : " + fl, EventLogEntryType.Information, (int)LogCode.FilterList);
                }

                //foreach (string name in filterList)
                //{
                //    Console.WriteLine(name);
                //}
            }
            catch (System.Security.SecurityException se)
            { writeWindowsLog(se.Message, EventLogEntryType.Error, (int)LogCode.Error); }
            catch (IOException ioe)
            { writeWindowsLog(ioe.Message, EventLogEntryType.Error, (int)LogCode.Error); }
            catch (ArgumentException ae)
            { writeWindowsLog(ae.Message, EventLogEntryType.Error, (int)LogCode.Error); }

            //Console.WriteLine("Action = {0}", Action);
        }

        public static void OnChanged(object source, FileSystemEventArgs e)
        {


            // get the file's extension 
            string ext = (Path.GetExtension(e.FullPath) ?? string.Empty).ToLower();

            if (filterList.Contains(ext))
            {
                // Do your magic here
                string log = "File " + e.FullPath + " has been copied on USB Mass Storage by " + System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                writeWindowsLog(log, EventLogEntryType.Warning, (int)LogCode.FileCopied);
            }
            //Console.WriteLine("{0}, with path {1} has been {2} by {3}", e.Name, e.FullPath, e.ChangeType, System.Security.Principal.WindowsIdentity.GetCurrent().Name);
        }

        public static void UsbInsertedEventHandler(object sender, EventArrivedEventArgs e)
        {
            string driveName = e.NewEvent.Properties["DriveName"].Value.ToString();
            string log = "USB Mass Storage device inserted - Drive : " + driveName;

            writeWindowsLog(log, EventLogEntryType.Information, (int)LogCode.UsbInserted);
            //Console.WriteLine("usb {0} inserted", driveName);

            FileSystemWatcher watcher = new FileSystemWatcher();

            try
            {
                if (watcherDict.ContainsKey(driveName))
                {
                    watcherDict[driveName] = watcher;
                }
                else
                {
                    watcherDict.Add(driveName, watcher);
                }

                UsbFileCopyDetect(driveName, watcher);
                //Console.WriteLine("File watcher set on {0}", driveName);
            }
            catch (ArgumentNullException ex)
            {
                writeWindowsLog(ex.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }
        }

        public static void UsbRemovedEventHandler(object sender, EventArrivedEventArgs e)
        {
            string driveName = e.NewEvent.Properties["DriveName"].Value.ToString();
            string log = "USB Mass Storage device removed - Drive : " + driveName;

            try
            {
                if (watcherDict.ContainsKey(driveName))
                {
                    if (watcherDict[driveName] != null)
                    {
                        watcherDict[driveName].Dispose();
                        watcherDict[driveName] = null;
                    }
                    writeWindowsLog(log, EventLogEntryType.Information, (int)LogCode.UsbRemoved);
                    //Console.WriteLine("usb {0} and watcher removed", driveName);

                    //foreach (String key in watcherDict.Keys)
                    //{
                    //    Console.WriteLine("{0} {1}", key, watcherDict[key]);

                    //}
                }
            }
            catch (ArgumentNullException ex)
            {
                writeWindowsLog(ex.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }
            catch (Exception ex)
            {
                writeWindowsLog(ex.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }
        }

        public static void UsbFileCopyDetect(string dir, FileSystemWatcher watcher)
        {
            // Create a new FileSystemWatcher and set its properties.
            string log = "Filewatcher set on drive : " + dir;

            try
            {
                watcher.Path = dir;
                watcher.IncludeSubdirectories = true;
                watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Security | NotifyFilters.Size;
                watcher.Filter = "*.*";
                watcher.Created += new FileSystemEventHandler(OnChanged);
                watcher.EnableRaisingEvents = true;


                writeWindowsLog(log, EventLogEntryType.Information, (int)LogCode.FileWatcher);
            }
            catch (ArgumentException e)
            {
                writeWindowsLog(e.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }
        }

        public static void buildFilter(string Filter, bool isReplace)
        {
            string[] FilterList = Filter.Split(',');

            if (isReplace)
            {
                filterList.Clear();
            }

            foreach (string filter in FilterList)
            {
                string tmp = "." + filter;
                if (filter.Length == 3 && !filterList.Contains(tmp))
                {
                    filterList.Add(tmp);
                }
            }
        }

        public static void freeWatcher()
        {
            try
            {
                foreach (String key in watcherDict.Keys)
                {
                    if (watcherDict[key] != null)
                    {
                        watcherDict[key].Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                writeWindowsLog(e.Message, EventLogEntryType.Error, (int)LogCode.Error);
            }
        }

        public static void splitFilter()
        {
            foreach (string filter in filterList)
            {
                fl += filter + ",";
            }

           //Console.WriteLine("fl : {0}", fl); ;
        }
    }
}