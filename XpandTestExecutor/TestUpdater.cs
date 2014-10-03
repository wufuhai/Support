using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using DevExpress.EasyTest.Framework;

namespace XpandTestExecutor{
    public class TestUpdater{
        public static void UpdateTestConfig(EasyTest easyTest, string fileName) {
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
                testDatabase.DBName = testDatabase.DefaultDBName() + suffix;
            }
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
            var xmlSerializer = new XmlSerializer(typeof(Options));
            var stringReader = new StringReader(File.ReadAllText(Path.Combine(Path.GetDirectoryName(fileName)+"","config.xml")));
            var options = (Options)xmlSerializer.Deserialize(stringReader);
            foreach (var testDatabase in options.TestDatabases.Cast<TestDatabase>()){
                var suffix = user.Name != null ? "_" + user.Name : null;
                allText = Regex.Replace(allText, @"(" +testDatabase.DefaultDBName()+ @")(_[^\s]*)?", "$1"+suffix, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);    
            }
            File.WriteAllText(fileName, allText);
        }

        public static void UpdateTestFile(EasyTest easyTest) {
            var fileToUpdate = GetFileToUpdate(easyTest.FileName);
            UpdateTestFileCore(fileToUpdate, easyTest.Users.Last());
        }


        private static void UpdateAppBinAlias(User user, Options options) {
            foreach (var alias in options.Aliases.Cast<TestAlias>().Where(@alias => alias.ContainsAppPath())) {
                alias.Value = alias.UpdateAppPath(user.Name);
            }
        }
    }
}