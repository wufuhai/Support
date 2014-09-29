using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ProcessAsUser {
    public class ProcessAsUser {
        public enum WTSConnectstateClass {
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

        /// <summary>
        ///     Contains values that indicate the type of session information to retrieve in a call to the
        ///     <see cref="WTSQuerySessionInformation" /> function.
        /// </summary>
        public enum WtsInfoClass {
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

        public const int WTSCurrentServerHandle = 0;

        private readonly Options _options;

        public ProcessAsUser(Options options) {
            _options = options;
        }

        public Options Options {
            get { return _options; }
        }

        public static IntPtr GetUserToken(WTSSessionInfo sessionInfo) {
            IntPtr logonUserToken;
            bool wtsQueryUserToken = WTSQueryUserToken(sessionInfo.SessionID, out logonUserToken);
            if (!wtsQueryUserToken) {
                int lastWin32Error = Marshal.GetLastWin32Error();
                throw new Win32Exception("Error " + lastWin32Error + "querying user token");
            }
            return logonUserToken;
        }

        [DllImport("WTSAPI32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("WTSAPI32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool WTSQueryUserToken(Int32 sessionId, out IntPtr token);

        [DllImport("Wtsapi32.dll")]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WtsInfoClass wtsInfoClass,
            out IntPtr ppBuffer, out int pBytesReturned);


        public static string GetUsernameBySessionId(int sessionId, bool prependDomain) {
            IntPtr buffer;
            int strLen;
            string username = "SYSTEM";
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WtsInfoClass.WTSUserName, out buffer, out strLen) &&
                strLen > 1) {
                username = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                if (prependDomain) {
                    if (
                        WTSQuerySessionInformation(IntPtr.Zero, sessionId, WtsInfoClass.WTSDomainName, out buffer,
                            out strLen) && strLen > 1) {
                        username = Marshal.PtrToStringAnsi(buffer) + "\\" + username;
                        WTSFreeMemory(buffer);
                    }
                }
            }
            return username;
        }

        [DllImport("WTSAPI32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool WTSEnumerateSessions(
            IntPtr hServer,
            [MarshalAs(UnmanagedType.U4)] UInt32 reserved,
            [MarshalAs(UnmanagedType.U4)] UInt32 version,
            ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref UInt32 pSessionInfoCount
            );

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr hHandle);

        public WTSSessionInfo? GetWTSSessionInfo(string userName, uint sessionCount, IntPtr ppSessionInfo) {
            for (int i = 0; i < sessionCount; i++) {
                WTSSessionInfo? wtsSessionInfo = (WTSSessionInfo)Marshal.PtrToStructure(
                    ppSessionInfo + i * Marshal.SizeOf(typeof(WTSSessionInfo)),
                    typeof(WTSSessionInfo));
                if (wtsSessionInfo.Value.State == WTSConnectstateClass.WTSActive &&
                    GetUsernameBySessionId(wtsSessionInfo.Value.SessionID, false).ToLower() == userName.ToLower())
                    return wtsSessionInfo;
            }
            return null;
        }

        public SessionInfo GetSessionInfo(string userName) {
            IntPtr ppSessionInfo = IntPtr.Zero;
            UInt32 sessionCount = 0;
            bool wtsEnumerateSessions = WTSEnumerateSessions((IntPtr)WTSCurrentServerHandle, 0, 1, ref ppSessionInfo,
                ref sessionCount);
            if (wtsEnumerateSessions) {
                WTSSessionInfo? wtsSessionInfo = GetWTSSessionInfo(userName, sessionCount, ppSessionInfo);
                return wtsSessionInfo == null
                    ? new SessionInfo(null, ppSessionInfo)
                    : new SessionInfo(wtsSessionInfo, ppSessionInfo);
            }
            return new SessionInfo(null, ppSessionInfo);
        }

        public bool CreateProcess() {
            string userName = _options.UserName;
            SessionInfo sessionInfo = GetSessionInfo(userName);
            if (sessionInfo.Info != null) {
                IntPtr userToken = GetUserToken(sessionInfo.Info.Value);
                Trace.TraceInformation("UserToken=" + userToken);
                CreateProcess(userToken);
                CloseHandle(userToken);
                WTSFreeMemory(sessionInfo.IntPtr);
                return true;
            }
            WTSFreeMemory(sessionInfo.IntPtr);
            return false;
        }

        public void CreateProcess(IntPtr logonUserToken) {
            CreateProcess(_options.ExePath, logonUserToken, _options.Arguments);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint exitCode);

        private static void CreateProcess(string childProcName, IntPtr logonUserToken, string arguments) {
            string fileName = Path.Combine(Path.GetDirectoryName(childProcName) + "", "ProcessAsUserWrapper.exe");
            string args = Path.GetFileName(childProcName) + " " + arguments;
            var userSpecificProcess = new UserSpecificProcess {
                StartInfo = new ProcessStartInfo(fileName, args) { UseShellExecute = false }
            };
            userSpecificProcess.StartAsUser(logonUserToken);
        }

        public bool SessionExists() {
            SessionInfo sessionInfo = GetSessionInfo(_options.UserName);
            WTSFreeMemory(sessionInfo.IntPtr);
            return sessionInfo.Info != null;
        }

        public struct SessionInfo {
            private readonly IntPtr _intPtr;
            private readonly WTSSessionInfo? _wtsSessionInfo;

            public SessionInfo(WTSSessionInfo? wtsSessionInfo, IntPtr intPtr)
                : this() {
                _wtsSessionInfo = wtsSessionInfo;
                _intPtr = intPtr;
            }

            public IntPtr IntPtr {
                get { return _intPtr; }
            }

            public WTSSessionInfo? Info {
                get { return _wtsSessionInfo; }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WTSSessionInfo {
            public Int32 SessionID;
            public string pWinStationName;
            public WTSConnectstateClass State;
        }
    }
}