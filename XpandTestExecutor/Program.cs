using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.EasyTest.Framework;
using Microsoft.Win32;

namespace XpandTestExecutor {
    internal class Program {
        public const string EasyTestUsersDir = "EasyTestUsers";
        private static readonly object _locker = new object();

        private static void Main(string[] args) {
            Trace.UseGlobalLock = true;
            Trace.Listeners.Add(new TextWriterTraceListener("XpandTestExecutor.log"));
            Trace.Listeners.Add(new ConsoleTraceListener());
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            try{
                var windowsIdentity = WindowsIdentity.GetCurrent();
                Debug.Assert(windowsIdentity != null, "windowsIdentity != null");
                var isSystem = windowsIdentity.IsSystem;
                var testsQueque = CreateTestsQueque();
                if (isSystem) {
                    SystemAccountExecute(args, testsQueque);
                }
                else {
                    NormalAccountExecute(args, testsQueque);
                }
            }
            catch (Exception e){
                Trace.TraceError(e.ToString());
                throw;
            }
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs) {
            Trace.TraceError(unhandledExceptionEventArgs.ExceptionObject.ToString());
        }

        private static void NormalAccountExecute(string[] args, Queue<EasyTest> testsQueque) {
            while (testsQueque.Count > 0) {
                var easyTest = testsQueque.Dequeue();
                easyTest.Users.Add(new User());
                var workingDirectory = Path.GetDirectoryName(easyTest.FileName) + "";
                SetupEnviroment(workingDirectory, easyTest);
                var process = new Process {
                    StartInfo =
                        new ProcessStartInfo(Path.Combine(workingDirectory, "TestExecutor.v" + args[0] + ".exe"), easyTest.FileName) {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WorkingDirectory = workingDirectory
                        }
                };
                process.Start();
                Trace.TraceInformation(process.StandardOutput.ReadToEnd());
                process.WaitForExit();
            }
        }

        private static void SystemAccountExecute(string[] args, Queue<EasyTest> testsQueque) {
            RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(@"Software\Xpand\ProcessAsUser");
            if (registryKey != null) {
                var userNames = (string)registryKey.GetValue("UserName", "");
                if (!string.IsNullOrEmpty(userNames)) {
                    var userQueue = CreateUserQueue(registryKey, userNames);
                    TestEnviroment.CLeanup(testsQueque);
                    int usersCount = userQueue.Count;
                    do {
                        if (testsQueque.Count > 0) {
                            var user = GetUser(userQueue);
                            var easyTest = testsQueque.Dequeue();
                            easyTest.Users.Add(user);
                            Task startNew = Task.Factory.StartNew(() => RunTest(args, testsQueque, easyTest));
                            startNew.ContinueWith(task => userQueue.Enqueue(user));
                        }

                    } while (userQueue.Count != usersCount);
                }
                else {
                    Environment.Exit(1);
                }
            }
            else
                Environment.Exit(2);
        }

        private static void RunTest(string[] args, Queue<EasyTest> testsQueque, EasyTest easyTest) {
            TestEnviroment.KillWebDev(easyTest.Users.Last().Name);
            try {
                bool processAsUser = ProcessAsUser(args[0], easyTest);
                if (!processAsUser && easyTest.Users.Count < 3)
                    testsQueque.Enqueue(easyTest);
            }
            catch (Exception e) {
                LogErrors(easyTest, e);
            }

        }

        private static void LogErrors(EasyTest easyTest, Exception e) {
            lock (_locker) {
                var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                string fileName = Path.Combine(directoryName, "config.xml");
                using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    Options options = Options.LoadOptions(optionsStream, null, null, directoryName);
                    var logTests = new LogTests();
                    foreach (var application in options.Applications.Cast<TestApplication>()) {
                        var logTest = new LogTest { ApplicationName = application.Name, Result = "Failed" };
                        var logError = new LogError { Message = { Text = e.ToString() } };
                        logTest.Errors.Add(logError);
                        logTests.Tests.Add(logTest);
                    }
                    logTests.Save(Path.Combine(directoryName, "TestsLog.xml"));
                }
            }
            Trace.TraceError(e.ToString());
        }

        private static Queue<EasyTest> CreateTestsQueque() {
            string[] tests = File.ReadAllLines("easytests.txt");
            var testsQueque = new Queue<EasyTest>();
            CheckConfigSharing(tests);
            for (int index = 0; index < tests.Length; index++) {
                string test = tests[index];
                testsQueque.Enqueue(new EasyTest(4200 + index, 4030 + index) { FileName = test });
            }
            return testsQueque;
        }

        private static void CheckConfigSharing(IEnumerable<string> tests) {
            IGrouping<string, string>[] sharedConfigs =
                tests.GroupBy(Path.GetDirectoryName).Where(grouping => grouping.Count() > 1).ToArray();
            if (sharedConfigs.Length > 0) {
                string @join = String.Join(Environment.NewLine, sharedConfigs.Select(grouping => grouping.Key));
                throw new NotImplementedException(
                    "Please put ets files with configs in dedicated directories. Config sharing is not supported. See " +
                    Environment.NewLine + @join);
            }
        }

        private static User GetUser(Queue<User> userQueue) {
            Task task = Task.Factory.StartNew(() => {
                while (userQueue.Count == 0) {
                    Thread.Sleep(1000);
                }
            });
            Task.WaitAll(task);
            User user = userQueue.Dequeue();
            return user;
        }

        private static Queue<User> CreateUserQueue(RegistryKey registryKey, string userNames) {
            string[] passwords = ((string)registryKey.GetValue("Password")).Split(';');
            var userQueue = new Queue<User>();
            for (int i = 0; i < userNames.Split(';').Length; i++) {
                string userName = userNames.Split(';')[i];
                var user = new User { Name = userName, Password = passwords[i] };
                userQueue.Enqueue(user);
            }
            return userQueue;
        }

        private static bool ProcessAsUser(string version, EasyTest easyTest) {
            Process process;
            string directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
            lock (_locker) {
                var user = easyTest.Users.Last();
                Trace.TraceInformation(user.Name + " WinPort:" + easyTest.WinPort + " WebPort:" + easyTest.WebPort + " -->" + easyTest.FileName);
                SetupEnviroment(directoryName, easyTest);
                var arguments = string.Format("-e TestExecutor.v{0}.exe -u {1} -p {2} -a {3}", version, user.Name,
                    user.Password, Path.GetFileName(easyTest.FileName));
                process = new Process {
                    StartInfo = new ProcessStartInfo(Path.Combine(directoryName, "ProcessAsUser.exe"), arguments) {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WorkingDirectory = directoryName
                    }
                };
                process.Start();
                Thread.Sleep(5000);
            }
            process.WaitForExit();
            lock (_locker) {
                CopyXafLogToPath(directoryName);

                var logTests = GetLogTests(easyTest).Tests.Where(test => test.Name.ToLowerInvariant() == (Path.GetFileNameWithoutExtension(easyTest.FileName) + "").ToLowerInvariant()).ToArray();
                if (logTests.All(test => test.Result == "Passed")) {
                    Trace.TraceInformation(easyTest.FileName + " passed");
                    return true;
                }
                Trace.TraceInformation(easyTest.FileName + " not passed=" + string.Join(Environment.NewLine, logTests.SelectMany(test => test.Errors.Select(error => error.Message.Text))));
                return false;
            }
        }

        private static void CopyXafLogToPath(string directoryName) {
            string fileName = Path.Combine(directoryName, "config.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, directoryName);
                foreach (var alias in options.Aliases.Cast<TestAlias>().Where(@alias => alias.ContainsAppPath())) {
                    var suffix = alias.IsWinAppPath() ? "_win" : "_web";
                    var sourceFileName = Path.Combine(alias.Value, "eXpressAppFramework.log");
                    if (File.Exists(sourceFileName))
                        File.Copy(sourceFileName, Path.Combine(directoryName, "eXpressAppFramework" + suffix + ".log"), true);
                }
            }
        }

        private static LogTests GetLogTests(EasyTest easyTest) {
            var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
            var fileName = Path.Combine(directoryName, "testslog.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return LogTests.LoadTestsResults(optionsStream);
            }
        }

        private static void SetupEnviroment(string configPath, EasyTest easyTest) {
            var user = easyTest.Users.Last();
            string fileName = Path.Combine(configPath, "config.xml");
            TestUpdater.UpdateTestConfig(easyTest, fileName);
            if (user.Name != null){
                AppConfigUpdater.Update(fileName,configPath,easyTest);
            }
            TestUpdater.UpdateTestFile(easyTest);
        }
    }
}