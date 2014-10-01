using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DevExpress.EasyTest.Framework;

namespace XpandTestExecutor{
    public class AppConfigUpdater {
        
        public static void Update(string fileName, string configPath, EasyTest easyTest) {
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, configPath);
                UpdateAppConfigFiles(easyTest, options);
            }
        }
        private static void UpdateAppConfigFiles(EasyTest easyTest, Options options) {
            var user = easyTest.Users.Last();
            foreach (var alias in options.Aliases.Cast<TestAlias>().Where(@alias => alias.ContainsAppPath())) {
                var sourcePath = Path.GetFullPath(alias.UpdateAppPath(null));
                if (Directory.Exists(sourcePath)) {
                    var destPath = Path.GetFullPath(alias.UpdateAppPath(user.Name));
                    DirectoryCopy(sourcePath, destPath, true, sourcePath + @"\" + Program.EasyTestUsersDir);
                    UpdateAppConfig(easyTest, options, alias, user);
                }
            }
        }

        private static void UpdateConnectionStrings(User user, Options options, XDocument document) {
            foreach (TestDatabase testDatabase in options.TestDatabases) {
                var indexOf = testDatabase.DBName.IndexOf("_", StringComparison.Ordinal);
                var database = indexOf > -1 ? testDatabase.DBName.Substring(0, indexOf) : testDatabase.DBName;
                var connectionStrings = document.Descendants("connectionStrings").SelectMany(element => element.Descendants())
                    .Where(element => element.Attribute("connectionString").Value.ToLowerInvariant().Contains(database.ToLowerInvariant())).Select(element
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
            if (testAlias.IsWinAppPath()) {
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

        private static void UpdateAppConfig(EasyTest easyTest, Options options, TestAlias alias, User user) {
            var keyValuePair = LoadAppConfig(alias, options.Applications);
            if (File.Exists(keyValuePair.Value)) {
                var document = keyValuePair.Key;
                UpdatePort(easyTest.WinPort, document);
                UpdateConnectionStrings(user, options, document);
                document.Save(keyValuePair.Value);
            }
        }

    }
}