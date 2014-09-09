using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ProcessAsUser{
    internal class ProcessAsUser{
        [DllImport("WTSAPI32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint exitCode);

        private static void CreateProcess(string childProcName, IntPtr logonUserToken, string arguments){
            var userSpecificProcess = new UserSpecificProcess{
                StartInfo = new ProcessStartInfo(childProcName, arguments) { UseShellExecute = false}
            };
            userSpecificProcess.StartAsUser(logonUserToken);
        }

        public static void Launch(string userName, string password, string processPath, string arguments){
            var windowsIdentity = WindowsIdentity.GetCurrent();
            Debug.Assert(windowsIdentity != null, "windowsIdentity != null");
            string name = windowsIdentity.Name;
            if (name.StartsWith(@"NT AUTHORITY")) {
                LaunchAsUser(userName,password,processPath,arguments);    
            }
            else{
                var process = new Process {StartInfo = new ProcessStartInfo(processPath, arguments){UseShellExecute = false,CreateNoWindow = true,RedirectStandardOutput = true}};
                process.Start();
                Trace.TraceInformation(process.StandardOutput.ReadToEnd());
                process.WaitForExit();
            }
        }

        private static void LaunchAsUser(string userName, string password, string processPath, string arguments){
            RDCClient.SessionInfo sessionInfo = RDCClient.GetSessionInfo(userName, password);
            if (sessionInfo.Info != null){
                IntPtr userToken = RDCClient.GetUserToken(sessionInfo.Info.Value);
                Trace.TraceInformation("UserToken=" + userToken);
                CreateProcess(processPath, userToken, arguments);
                CloseHandle(userToken);
            }
            else{
                Environment.Exit(200);
            }
            WTSFreeMemory(sessionInfo.IntPtr);
        }


        #region P/Invoke CreateProcessAsUser

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr hHandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct ProcessInformation{
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecurityAttributes{
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        /// <summary>
        ///     Struct, Enum and P/Invoke Declarations for CreateProcessAsUser.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct Startupinfo{
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        #endregion
    }
}