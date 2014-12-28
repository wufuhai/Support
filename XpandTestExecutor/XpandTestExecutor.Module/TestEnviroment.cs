using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using DevExpress.EasyTest.Framework;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module {
    public class TestEnviroment {
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