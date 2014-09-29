using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;

namespace ProcessAsUser {
    public static class PowerUtilities {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int ExitWindowsEx(ExitWindows uFlags, ShutdownReason dwReason);

        public static bool ExitWindows(ExitWindows exitWindows, ShutdownReason reason, bool ajustToken) {
            if (ajustToken && !TokenAdjuster.EnablePrivilege("SeShutdownPrivilege", true)) {
                return false;
            }
            return ExitWindowsEx(exitWindows, reason) != 0;
        }

        public static void KillProccesses(string user) {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process");
            foreach (var managementObject in searcher.Get().Cast<ManagementObject>()) {
                KillProcess(user, managementObject);
            }
            foreach (var process in Process.GetProcesses()){
                var mObject = new ManagementObjectSearcher("SELECT * FROM Win32_Process Where ProcessID='" + process.Id + "'").Get().Cast<ManagementObject>().FirstOrDefault();
                if (mObject != null) KillProcess(user, mObject);
            }
        }

        private static void KillProcess(string user, ManagementObject managementObject){
            var objects = new object[2];
            try{
                managementObject.InvokeMethod("GetOwner", objects);
                if (user == (string) objects[0]){
                    var pid = int.Parse(managementObject["ProcessId"].ToString());
                    KillProcessAndChildren(pid);
                }
            }
            catch (Exception e){
                Console.WriteLine(e);
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

    }

    [Flags]
    public enum ExitWindows : uint {
        // ONE of the following:
        LogOff = 0x00,
        ShutDown = 0x01,
        Reboot = 0x02,
        PowerOff = 0x08,
        RestartApps = 0x40,
        // plus AT MOST ONE of the following two:
        Force = 0x04,
        ForceIfHung = 0x10,
    }

    [Flags]
    public enum ShutdownReason : uint {
        None = 0,

        MajorApplication = 0x00040000,
        MajorHardware = 0x00010000,
        MajorLegacyApi = 0x00070000,
        MajorOperatingSystem = 0x00020000,
        MajorOther = 0x00000000,
        MajorPower = 0x00060000,
        MajorSoftware = 0x00030000,
        MajorSystem = 0x00050000,

        MinorBlueScreen = 0x0000000F,
        MinorCordUnplugged = 0x0000000b,
        MinorDisk = 0x00000007,
        MinorEnvironment = 0x0000000c,
        MinorHardwareDriver = 0x0000000d,
        MinorHotfix = 0x00000011,
        MinorHung = 0x00000005,
        MinorInstallation = 0x00000002,
        MinorMaintenance = 0x00000001,
        MinorMMC = 0x00000019,
        MinorNetworkConnectivity = 0x00000014,
        MinorNetworkCard = 0x00000009,
        MinorOther = 0x00000000,
        MinorOtherDriver = 0x0000000e,
        MinorPowerSupply = 0x0000000a,
        MinorProcessor = 0x00000008,
        MinorReconfig = 0x00000004,
        MinorSecurity = 0x00000013,
        MinorSecurityFix = 0x00000012,
        MinorSecurityFixUninstall = 0x00000018,
        MinorServicePack = 0x00000010,
        MinorServicePackUninstall = 0x00000016,
        MinorTermSrv = 0x00000020,
        MinorUnstable = 0x00000006,
        MinorUpgrade = 0x00000003,
        MinorWMI = 0x00000015,

        FlagUserDefined = 0x40000000,
        FlagPlanned = 0x80000000
    }

    public sealed class TokenAdjuster {
        // PInvoke stuff required to set/enable security privileges
        private const int SE_PRIVILEGE_ENABLED = 0x00000002;
        private const int TOKEN_ADJUST_PRIVILEGES = 0X00000020;
        private const int TOKEN_QUERY = 0X00000008;
        internal const int TOKEN_ALL_ACCESS = 0X001f01ff;
        internal const int PROCESS_QUERY_INFORMATION = 0X00000400;

        [DllImport("advapi32", SetLastError = true), SuppressUnmanagedCodeSecurity]
        private static extern int OpenProcessToken(
            IntPtr processHandle, // handle to process
            int desiredAccess, // desired access to process
            ref IntPtr tokenHandle // handle to open access token
            );

        [DllImport("kernel32", SetLastError = true),
         SuppressUnmanagedCodeSecurity]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int AdjustTokenPrivileges(
            IntPtr tokenHandle,
            int disableAllPrivileges,
            IntPtr newState,
            int bufferLength,
            IntPtr previousState,
            ref int returnLength);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool LookupPrivilegeValue(
            string lpSystemName,
            string lpName,
            ref LUID lpLuid);

        public static bool EnablePrivilege(string lpszPrivilege, bool bEnablePrivilege) {
            bool retval = false;
            int ltkpOld = 0;
            IntPtr hToken = IntPtr.Zero;
            var tkp = new TOKEN_PRIVILEGES{Privileges = new int[3]};
            var tLuid = new LUID();
            tkp.PrivilegeCount = 1;
            if (bEnablePrivilege)
                tkp.Privileges[2] = SE_PRIVILEGE_ENABLED;
            else
                tkp.Privileges[2] = 0;
            if (LookupPrivilegeValue(null, lpszPrivilege, ref tLuid)) {
                Process proc = Process.GetCurrentProcess();
                if (proc.Handle != IntPtr.Zero) {
                    if (OpenProcessToken(proc.Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
                        ref hToken) != 0) {
                        tkp.PrivilegeCount = 1;
                        tkp.Privileges[2] = SE_PRIVILEGE_ENABLED;
                        tkp.Privileges[1] = tLuid.HighPart;
                        tkp.Privileges[0] = tLuid.LowPart;
                        const int bufLength = 256;
                        IntPtr tu = Marshal.AllocHGlobal(bufLength);
                        Marshal.StructureToPtr(tkp, tu, true);
                        if (AdjustTokenPrivileges(hToken, 0, tu, bufLength, IntPtr.Zero, ref ltkpOld) != 0) {
                            // successful AdjustTokenPrivileges doesn't mean privilege could be changed
                            if (Marshal.GetLastWin32Error() == 0) {
                                retval = true; // Token changed
                            }
                        }
                        Marshal.FreeHGlobal(tu);
                    }
                }
            }
            if (hToken != IntPtr.Zero) {
                CloseHandle(hToken);
            }
            return retval;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID {
            internal int LowPart;
            internal int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_PRIVILEGES {
            internal int PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            internal int[] Privileges;
        }
    }
}
