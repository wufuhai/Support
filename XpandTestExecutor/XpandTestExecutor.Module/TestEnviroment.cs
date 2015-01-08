using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using DevExpress.EasyTest.Framework;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module {
    public class TestEnviroment {
        [StructLayout(LayoutKind.Sequential)]
        internal struct WTS_SESSION_INFO {
            public Int32 SessionID;
            [MarshalAs(UnmanagedType.LPStr)]
            public String pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }

        internal enum WTS_CONNECTSTATE_CLASS {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        internal enum WTS_INFO_CLASS {
            WTSInitialProgram,
            WTSApplicationName,
            WTSWorkingDirectory,
            WTSOEMId,
            WTSSessionId,
            WTSUserName,
            WTSWinStationName,
            WTSDomainName,
            WTSConnectState,
            WTSClientBuildNumber,
            WTSClientName,
            WTSClientDirectory,
            WTSClientProductId,
            WTSClientHardwareId,
            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType,
            WTSIdleTime,
            WTSLogonTime,
            WTSIncomingBytes,
            WTSOutgoingBytes,
            WTSIncomingFrames,
            WTSOutgoingFrames,
            WTSClientInfo,
            WTSSessionInfo
        }
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSLogoffSession(IntPtr hServer, int sessionId, bool bWait);

        [DllImport("Wtsapi32.dll")]
        private static extern bool WTSQuerySessionInformation(
            IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] String pServerName);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSCloseServer(IntPtr hServer);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern Int32 WTSEnumerateSessions(IntPtr hServer, [MarshalAs(UnmanagedType.U4)] Int32 reserved,
            [MarshalAs(UnmanagedType.U4)] Int32 version, ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref Int32 pCount);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        internal static List<int> GetSessionIDs(IntPtr server) {
            var sessionIds = new List<int>();
            IntPtr buffer = IntPtr.Zero;
            int count = 0;
            int retval = WTSEnumerateSessions(server, 0, 1, ref buffer, ref count);
            int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
            Int64 current = (int)buffer;

            if (retval != 0) {
                for (int i = 0; i < count; i++) {
                    var si = (WTS_SESSION_INFO)Marshal.PtrToStructure((IntPtr)current, typeof(WTS_SESSION_INFO));
                    current += dataSize;
                    sessionIds.Add(si.SessionID);
                }
                WTSFreeMemory(buffer);
            }
            return sessionIds;
        }

        public static bool LogOffUser(string userName){
            IntPtr server=IntPtr.Zero;
            try{
                server = WTSOpenServer(Environment.MachineName);
                return LogOffUser(userName, server);
            }
            finally{
                WTSCloseServer(server);
            }
        }

        private static bool LogOffUser(string userName, IntPtr server) {
            userName = userName.Trim().ToUpper();
            List<int> sessions = GetSessionIDs(server);
            Dictionary<string, int> userSessionDictionary = GetUserSessionDictionary(server, sessions);
            if (userSessionDictionary.ContainsKey(userName))
                return WTSLogoffSession(server, userSessionDictionary[userName], true);
            return false;
        }

        private static Dictionary<string, int> GetUserSessionDictionary(IntPtr server, IEnumerable<int> sessions) {
            var userSession = new Dictionary<string, int>();

            foreach (int sessionId in sessions) {
                string uName = GetUserName(sessionId, server);
                if (!string.IsNullOrWhiteSpace(uName))
                    userSession.Add(uName, sessionId);
            }
            return userSession;
        }

        internal static string GetUserName(int sessionId, IntPtr server) {
            IntPtr buffer = IntPtr.Zero;
            string userName = string.Empty;
            try {
                uint count;
                WTSQuerySessionInformation(server, sessionId, WTS_INFO_CLASS.WTSUserName, out buffer, out count);
                var ptrToStringAnsi = Marshal.PtrToStringAnsi(buffer);
                if (ptrToStringAnsi != null)
                    userName = ptrToStringAnsi.ToUpper().Trim();
            }
            finally {
                WTSFreeMemory(buffer);
            }
            return userName;
        }

        public static void LogOffAllUsers(string[] users){
            IntPtr server = IntPtr.Zero;
            try{
                server = WTSOpenServer(Environment.MachineName);
                foreach (var user in users){
                    LogOffUser(user, server);
                }
            }
            finally{
                WTSCloseServer(server);
            }
        }

        public static void KillWebDev(string name) {
            KillProccesses(name, i => Process.GetProcessById(i).ProcessName.StartsWith("WebDev.WebServer40"));
        }

        [DebuggerStepThrough]
        private static void KillProcess(string user, ManagementObject managementObject, Func<int, bool> match) {
            var objects = new object[2];
            try {
                managementObject.InvokeMethod("GetOwner", objects);
                if (user == (string)objects[0]) {
                    var pid = int.Parse(managementObject["ProcessId"].ToString());
                    if (match(pid))
                        KillProcessAndChildren(pid);
                }
            }
            catch (ManagementException) {
            }
        }

        private static void KillProcessAndChildren(int pid) {
            if (pid != Process.GetCurrentProcess().Id) {
                var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
                foreach (var managementObject in searcher.Get().Cast<ManagementObject>()) {
                    KillProcessAndChildren(Convert.ToInt32(managementObject["ProcessID"]));
                }
                try {
                    var proc = Process.GetProcessById(pid);
                    proc.Kill();
                }
                catch (ArgumentException) {
                }
            }
        }

        private static void KillProccesses(string user, Func<int, bool> match) {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process");
            foreach (var managementObject in searcher.Get().Cast<ManagementObject>()) {
                KillProcess(user, managementObject, match);
            }
        }

        public static void Cleanup(EasyTest[] easyTests) {
            var processes = Process.GetProcesses().Where(process => process.ProcessName.StartsWith("WebDev.WebServer40")).ToArray();
            foreach (var source in processes) {
                source.Kill();
            }

            foreach (var easyTest in easyTests.GroupBy(test => Path.GetDirectoryName(test.FileName))) {
                var path = Path.Combine(easyTest.Key, "config.xml");
                try {
                    using (var optionsStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        var options = Options.LoadOptions(optionsStream, null, null, easyTest.Key);
                        foreach (var alias in options.Aliases.Cast<TestAlias>().Where(@alias => alias.ContainsAppPath())) {
                            var appPath = alias.UpdateAppPath(null);
                            var usersDir = Path.Combine(appPath, TestRunner.EasyTestUsersDir);
                            if (Directory.Exists(usersDir))
                                Directory.Delete(usersDir, true);
                        }
                    }
                }
                catch (Exception e) {
                    throw new Exception(easyTest.Key, e);
                }
            }

        }

        public static void Setup(EasyTestExecutionInfo info) {
            string configPath = Path.GetDirectoryName(info.EasyTest.FileName) + "";
            string fileName = Path.Combine(configPath, "config.xml");
            TestUpdater.UpdateTestConfig(info, fileName);
            AppConfigUpdater.Update(fileName, configPath, info);
            TestUpdater.UpdateTestFile(info);
        }
    }
}