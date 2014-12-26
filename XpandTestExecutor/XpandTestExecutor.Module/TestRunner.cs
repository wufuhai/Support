using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp.Utils;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module {
    public class TestRunner {
        public const string EasyTestUsersDir = "EasyTestUsers";
        private static readonly object Locker = new object();

        private static bool ExecutionFinished(IDataLayer dataLayer, Guid executionInfoKey, int testsCount) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey, true);
                return executionInfo.EasyTestExecutionInfos.Count() >= testsCount && executionInfo.ExecutionFinished() == testsCount;
            }
        }

        private static void RunTest(Guid easyTestKey, IDataLayer dataLayer, bool isSystem) {
            Process process = null;
            lock (Locker)
            {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey,true);
                    try {
                        var easyTestExecutionInfo = easyTest.LastEasyTestExecutionInfo;
                        var user = easyTestExecutionInfo.WindowsUser;
                        Tracing.Tracer.LogText(user.Name + " WinPort:" + easyTestExecutionInfo.WinPort + " WebPort:" + easyTestExecutionInfo.WebPort + " -->" + easyTest.FileName);
                        SetupEnviroment(easyTest);
                        var processStartInfo = GetProcessStartInfo(easyTest, user, isSystem);
                        
                        process = new Process {
                            StartInfo = processStartInfo
                        };
                        process.Start();
                        easyTest.SetCurrentExecutionState(EasyTestExecutionInfoState.Running);
                        easyTest.Session.ValidateAndCommitChanges();
                        Thread.Sleep(5000);
                    }
                    catch (Exception e) {
                        LogErrors(easyTest, e);
                    }
                }
            }
            if (process != null)
            {
                process.WaitForExit();
                AfterProcessExecute(dataLayer,easyTestKey);
            }
        }

        private static void LogErrors(EasyTest easyTest, Exception e) {
            lock (Locker) {
                easyTest.SetCurrentExecutionState(EasyTestExecutionInfoState.Failed);
                easyTest.Session.ValidateAndCommitChanges();
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
            Tracing.Tracer.LogError(e);
        }
        private static ProcessStartInfo GetProcessStartInfo(EasyTest easyTest, WindowsUser user, bool isSystem){
            string testExecutor=string.Format("TestExecutor.v{0}.exe",AssemblyInfo.VersionShort);
            var arguments = isSystem ? string.Format("-e " + testExecutor + " -u {0} -p {1} -a {2}", user.Name, user.Password, Path.GetFileName(easyTest.FileName)) : Path.GetFileName(easyTest.FileName);
            string directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
            var exe = isSystem?"ProcessAsUser.exe":testExecutor;
            var processStartInfo = new ProcessStartInfo(Path.Combine(directoryName, exe), arguments){
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directoryName
            };
            return processStartInfo;
        }

        private static void AfterProcessExecute(IDataLayer dataLayer,Guid easyTestKey) {
            lock (Locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer))
                {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey,true);
                    CopyXafLogToPath(Path.GetDirectoryName(easyTest.FileName) + "");
                    var logTests = GetLogTests(easyTest).Tests.Where(test => test != null && test.Name.ToLowerInvariant() == (Path.GetFileNameWithoutExtension(easyTest.FileName) + "").ToLowerInvariant()).ToArray();
                    var state = EasyTestExecutionInfoState.Passed;
                    if (logTests.All(test => test.Result == "Passed")) {
                        Tracing.Tracer.LogText(easyTest.FileName + " passed");
                    }
                    else {
                        Tracing.Tracer.LogText(easyTest.FileName + " not passed=" + string.Join(Environment.NewLine,
                            logTests.SelectMany(test => test.Errors.Select(error => error.Message.Text))));
                        state = EasyTestExecutionInfoState.Failed;
                    }
                    easyTest.SetCurrentExecutionState(state);
                    easyTest.Session.ValidateAndCommitChanges();
                    TestEnviroment.KillWebDev(easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                }
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

        private static void SetupEnviroment(EasyTest easyTest) {
            string configPath = Path.GetDirectoryName(easyTest.FileName) + "";
            string fileName = Path.Combine(configPath, "config.xml");
            TestUpdater.UpdateTestConfig(easyTest, fileName);
            AppConfigUpdater.Update(fileName, configPath, easyTest);
            TestUpdater.UpdateTestFile(easyTest);
        }

        public static void Execute(string fileName, bool isSystem) {
            var fileNames = File.ReadAllLines(fileName).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            var objectSpace = ApplicationHelper.Instance.Application.CreateObjectSpace();
            var easyTests = EasyTest.GetTests(objectSpace,fileNames);
            objectSpace.Session().ValidateAndCommitChanges();
            Execute(easyTests,isSystem);
        }

        public static void Execute(EasyTest[] easyTests, bool isSystem) {
            Tracing.Tracer.LogValue("EasyTests.Count",easyTests.Count());
            if (easyTests.Any()){
                var dataLayer = GetDatalayer();
                var executionInfoKey = GetExecutionInfoKey(dataLayer, isSystem);
                do{
                    var easyTest = GetNextEasyTest(executionInfoKey, easyTests, dataLayer, isSystem);
                    if (easyTest != null){
                        Task.Factory.StartNew(() => RunTest(easyTest.Oid, dataLayer, isSystem));
                    }
                    Thread.Sleep(5000);

                } while (!ExecutionFinished(dataLayer, executionInfoKey, easyTests.Length));
            }
        }

        private static IDataLayer GetDatalayer(){
            var xpObjectSpaceProvider =new XPObjectSpaceProvider(new ConnectionStringDataStoreProvider(ApplicationHelper.Instance.Application.ConnectionString), true);
            return xpObjectSpaceProvider.CreateObjectSpace().Session().DataLayer;
        }

        private static Guid GetExecutionInfoKey(IDataLayer dataLayer, bool isSystem) {
            Guid executionInfoKey;
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = ExecutionInfo.Create(unitOfWork,isSystem);
                unitOfWork.ValidateAndCommitChanges();
                executionInfoKey = executionInfo.Oid;
            }
            return executionInfoKey;
        }

        private static EasyTest GetNextEasyTest(Guid executionInfoKey, EasyTest[] easyTests, IDataLayer dataLayer, bool isSystem) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey);
                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                if (executionInfo.EasyTestExecutingInfos.Count() < executionInfo.WindowsUsers.Count()) {
                    for (int i = 0; i < ((IModelOptionsTestExecutor)CaptionHelper.ApplicationModel.Options).ExecutionRetries; i++) {
                        var easyTest = executionInfo.GetTestsToExecute(i).FirstOrDefault(test => easyTests.Contains(test));
                        if (easyTest != null) {
                            return CreateExecutionInfo(easyTest, executionInfo, isSystem);
                        }
                    }

                }
            }
            return null;
        }

        private static EasyTest CreateExecutionInfo(EasyTest easyTest, ExecutionInfo executionInfo, bool isSystem) {
            var lastWindowsUser = easyTest.LastEasyTestExecutionInfo != null ? easyTest.LastEasyTestExecutionInfo.WindowsUser : null;
            var windowsUser = executionInfo.GetIdleUsers().Except(new[] { lastWindowsUser }).FirstOrDefault() ?? lastWindowsUser;
            if (windowsUser!=null){
                easyTest.CreateExecutionInfo(isSystem, executionInfo);
                var easyTestExecutionInfo = easyTest.LastEasyTestExecutionInfo;
                easyTestExecutionInfo.WindowsUser = windowsUser;
                easyTest.Session.ValidateAndCommitChanges();
                return easyTest;
            }
            return null;
        }

    }
}