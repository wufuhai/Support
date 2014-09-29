using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using DevExpress.EasyTest.Framework;
using Microsoft.Win32;

namespace XpandTestExecutor {
    internal class Program {
        const string EasyTestUsersDir = "EasyTestUsers";
        private static readonly object _locker = new object();

        private static void Main(string[] args) {
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
                Console.WriteLine(process.StandardOutput.ReadToEnd());
                process.WaitForExit();
            }
        }

        private static void SystemAccountExecute(string[] args, Queue<EasyTest> testsQueque) {
            RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(@"Software\Xpand\ProcessAsUser");
            if (registryKey != null) {
                var userNames = (string)registryKey.GetValue("UserName", "");
                if (!string.IsNullOrEmpty(userNames)) {
                    var userQueue = CreateUserQueue(registryKey, userNames);
                    CleanupEnviroment(testsQueque);
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

        private static void CleanupEnviroment(IEnumerable<EasyTest> easyTests) {
            var processes = Process.GetProcesses().Where(process => process.ProcessName.StartsWith("WebDev.WebServer40")).ToArray();
            foreach (var source in processes) {
                source.Kill();
            }

            foreach (var easyTest in easyTests.GroupBy(test => Path.GetDirectoryName(test.FileName))) {
                var path = Path.Combine(easyTest.Key, "config.xml");
                using (var optionsStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var options = Options.LoadOptions(optionsStream, null, null, easyTest.Key);
                    foreach (var alias in options.Aliases.Cast<TestAlias>().Where(ContainsAppPath)) {
                        var appPath = UpdateAppPath(null, alias);
                        var usersDir = Path.Combine(appPath, EasyTestUsersDir);
                        if (Directory.Exists(usersDir))
                            Directory.Delete(usersDir, true);
                    }
                }
            }
        }

        private static void RunTest(string[] args, Queue<EasyTest> testsQueque, EasyTest easyTest) {
            KillWebDev(easyTest.Users.Last().Name);
            try {
                bool processAsUser = ProcessAsUser(args[0], easyTest);
                if (!processAsUser && easyTest.Users.Count < 3)
                    testsQueque.Enqueue(easyTest);
            }
            catch (Exception e) {
                LogErrors(easyTest, e);
            }

        }

        public static void KillProccesses(string user, Func<int, bool> match) {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process");
            foreach (var managementObject in searcher.Get().Cast<ManagementObject>()) {
                KillProcess(user, managementObject, match);
            }
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

        private static void KillWebDev(string name) {
            KillProccesses(name, i => Process.GetProcessById(i).ProcessName.StartsWith("WebDev.WebServer40"));
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
            Console.WriteLine(e);
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
                Console.WriteLine(user.Name + " WinPort:" + easyTest.WinPort + " WebPort:" + easyTest.WebPort + " -->" + easyTest.FileName);
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
                    Console.WriteLine(easyTest.FileName + " passed");
                    return true;
                }
                Console.WriteLine(easyTest.FileName + " not passed=" + string.Join(Environment.NewLine, logTests.SelectMany(test => test.Errors.Select(error => error.Message.Text))));
                return false;
            }
        }

        private static void CopyXafLogToPath(string directoryName) {
            string fileName = Path.Combine(directoryName, "config.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, directoryName);
                foreach (var alias in options.Aliases.Cast<TestAlias>().Where(ContainsAppPath)) {
                    var suffix = IsWinAppPath(alias) ? "_win" : "_web";
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
            UpdateTestConfig(easyTest, fileName);
            if (user.Name != null) {
                using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    Options options = Options.LoadOptions(optionsStream, null, null, configPath);
                    UpdateAppConfigFiles(easyTest, options);
                }
            }
            UpdateTestFile(easyTest);
        }

        private static void UpdateTestConfig(EasyTest easyTest, string fileName) {
            var user = easyTest.Users.Last();
            var xmlSerializer = new XmlSerializer(typeof(Options));
            var options = (Options)xmlSerializer.Deserialize(new StringReader(File.ReadAllText(fileName)));
            UpdatePort(easyTest, options);
            UpdateAppBinAlias(user, options);
            UpdateDataBases(user, options);
            using (var writer = new StreamWriter(fileName))
            using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true }))
                xmlSerializer.Serialize(xmlWriter, options);
        }

        private static void UpdateDataBases(User user, Options options) {
            foreach (var testDatabase in options.TestDatabases.Cast<TestDatabase>()) {
                var suffix = user.Name != null ? "_" + user.Name : null;
                testDatabase.DBName = Regex.Replace(testDatabase.DBName, "([^_]*)(.*)", "$1", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline) + suffix;
            }
        }

        private static void UpdateAppBinAlias(User user, Options options) {
            foreach (var alias in options.Aliases.Cast<TestAlias>().Where(ContainsAppPath)) {
                alias.Value = UpdateAppPath(user.Name, alias);
            }
        }

        private static string UpdateAppPath(string userName, TestAlias alias) {
            string containerDir = userName == null ? null : EasyTestUsersDir + @"\";
            return IsWinAppPath(alias)
                ? Regex.Replace(alias.Value, @"(.*\\Bin\\)(.*)", @"$1EasyTest\" + containerDir + userName,
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline)
                : Regex.Replace(alias.Value, @"(.*\.web)(.*)", @"$1\" + containerDir + userName,
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        private static bool IsWinAppPath(TestAlias alias) {
            return alias.Name.ToLowerInvariant().StartsWith("win");
        }

        private static bool ContainsAppPath(TestAlias alias) {
            return alias.Name.ToLowerInvariant().EndsWith("bin");
        }

        private static void UpdatePort(EasyTest easyTest, Options options) {
            foreach (var application in options.Applications.Cast<TestApplication>()) {
                var additionalAttribute =
                    application.AdditionalAttributes.FirstOrDefault(
                        attribute => attribute.LocalName.ToLowerInvariant() == "communicationport");
                if (additionalAttribute != null)
                    additionalAttribute.Value = easyTest.WinPort.ToString(CultureInfo.InvariantCulture);
                else {
                    additionalAttribute =
                        application.AdditionalAttributes.First(attribute => attribute.LocalName.ToLowerInvariant() == "url");
                    additionalAttribute.Value = "http://localhost:" + easyTest.WebPort;
                }
            }
        }

        private static void UpdateTestFile(EasyTest easyTest) {
            var fileToUpdate = GetFileToUpdate(easyTest.FileName);
            UpdateTestFileCore(fileToUpdate, easyTest.Users.Last());
        }

        private static string GetFileToUpdate(string easyTestFileName) {
            using (var scriptStream = File.OpenRead(easyTestFileName)) {
                using (var streamReader = new StreamReader(scriptStream, System.Text.Encoding.UTF8)) {
                    while (streamReader.Peek() > -1) {
                        string currentLine = streamReader.ReadLine() + "";
                        currentLine = currentLine.TrimEnd();
                        if (currentLine.StartsWith("#IncludeFile")) {
                            int spaceIndex = currentLine.IndexOf(" ", StringComparison.Ordinal);
                            string includedfileName = currentLine.Remove(0, spaceIndex + 1).Trim();
                            if (!File.Exists(includedfileName)) {
                                string fullFileName = Path.Combine(Path.GetDirectoryName(easyTestFileName) + "", includedfileName);
                                includedfileName = fullFileName;
                            }
                            return includedfileName;
                        }
                        if (currentLine.StartsWith("#DropDB")) {
                            return easyTestFileName;
                        }
                    }
                }
            }

            throw new NotImplementedException(easyTestFileName);
        }

        private static void UpdateTestFileCore(string fileName, User user) {
            var allText = File.ReadAllText(fileName);
            var suffix = user.Name != null ? "_" + user.Name : null;
            allText = Regex.Replace(allText, @"(#DropDB [^_\s]*)([^\s]*)", "$1" + suffix, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            File.WriteAllText(fileName, allText);
        }

        private static void UpdateAppConfigFiles(EasyTest easyTest, Options options) {
            var user = easyTest.Users.Last();
            foreach (var alias in options.Aliases.Cast<TestAlias>().Where(ContainsAppPath)) {
                var sourcePath = Path.GetFullPath(UpdateAppPath(null, alias));
                if (Directory.Exists(sourcePath)) {
                    var destPath = Path.GetFullPath(UpdateAppPath(user.Name, alias));
                    DirectoryCopy(sourcePath, destPath, true, sourcePath + @"\" + EasyTestUsersDir);
                    UpdateAppConfig(easyTest, options, alias, user);
                }
            }
        }

        private static void UpdateAppConfig(EasyTest easyTest, Options options, TestAlias alias, User user) {
            var keyValuePair = LoadAppConfig(alias, options.Applications);
            if (File.Exists(keyValuePair.Value)) {
                var document = keyValuePair.Key;
                UpdatePort(easyTest.WinPort, document);
                UpdateConnectionStrings(user, options, document);
                document.Save(keyValuePair.Value);
            }
        }

        private static void UpdateConnectionStrings(User user, Options options, XDocument document) {
            foreach (TestDatabase testDatabase in options.TestDatabases) {
                var indexOf = testDatabase.DBName.IndexOf("_", StringComparison.Ordinal);
                var database = indexOf > -1 ? testDatabase.DBName.Substring(0, indexOf) : testDatabase.DBName;
                var connectionStrings = document.Descendants("connectionStrings").SelectMany(element => element.Descendants())
                    .Where(element => element.Attribute("connectionString").Value.Contains(database)).Select(element
                        => element.Attribute("connectionString"));
                foreach (var connectionString in connectionStrings) {
                    connectionString.Value = Regex.Replace(connectionString.Value,
                        @"(.*)(" + database + @"[^;""\s]*)(.*)", "$1" + database + "_" + user.Name + "$3",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);

                }
            }
        }

        private static void UpdatePort(int port, XDocument document) {
            var element = GetAppSettingsElement(document);
            element.SetAttributeValue("value", port);
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, string containerDir) {
            var dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists) {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }
            if (!Directory.Exists(destDirName)) {
                Directory.CreateDirectory(destDirName);
            }
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }
            if (copySubDirs) {
                var dirs = dir.GetDirectories().Where(info => Path.GetDirectoryName(info.FullName + @"\") != Path.GetDirectoryName(containerDir + @"\"));
                foreach (DirectoryInfo subdir in dirs) {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, true, containerDir);
                }
            }
        }

        private static XElement GetAppSettingsElement(XDocument document) {
            Debug.Assert(document.Root != null, "config.Root != null");
            XElement appSettings = document.Root.Element("appSettings");
            Debug.Assert(appSettings != null, "appSettings != null");
            var element = appSettings.Descendants().FirstOrDefault(node => node.Attribute("key").Value == "EasyTestCommunicationPort");
            if (element == null) {
                element = new XElement("add");
                element.SetAttributeValue("key", "EasyTestCommunicationPort");
                appSettings.Add(element);
            }
            return element;
        }

        private static KeyValuePair<XDocument, string> LoadAppConfig(TestAlias testAlias, TestApplicationList applications) {
            var path = Path.GetFullPath(testAlias.Value);
            var configName = "web.config";
            if (IsWinAppPath(testAlias)) {
                var fileName = Path.GetFileName(applications.Cast<TestApplication>().SelectMany(application => application.AdditionalAttributes).First(attribute => attribute.LocalName.ToLowerInvariant() == "filename").Value);
                configName = fileName + ".config";
            }
            var configPath = Path.Combine(path, configName);
            if (File.Exists(configPath)) {
                using (var streamReader = new StreamReader(configPath)) {
                    return new KeyValuePair<XDocument, string>(XDocument.Load(streamReader), configPath);
                }
            }
            return new KeyValuePair<XDocument, string>();
        }

    }

    public class User {

        public string Name { get; set; }
        public string Password { get; set; }
    }

    public class EasyTest {
        private readonly int _winPort;
        private readonly int _webPort;
        private readonly List<User> _users = new List<User>();

        public EasyTest(int winPort, int webPort) {
            _winPort = winPort;
            _webPort = webPort;
        }

        public int WinPort {
            get { return _winPort; }
        }

        public List<User> Users {
            get { return _users; }
        }

        public int WebPort {
            get { return _webPort; }
        }

        public string FileName { get; set; }

        public string Index { get; set; }


    }

    public class EasyTestRunner {
        public User User { get; set; }

    }

}