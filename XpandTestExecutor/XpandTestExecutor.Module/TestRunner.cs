using System;
using System.Collections.Generic;
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
using Xpand.Utils.Threading;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module {
    public class TestRunner {
        public const string EasyTestUsersDir = "EasyTestUsers";
        private static readonly object Locker = new object();

        private static bool ExecutionFinished(IDataLayer dataLayer, Guid executionInfoKey, int testsCount) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey, true);
                return executionInfo.FinishedEasyTestExecutionInfos() == testsCount;
            }
        }

        private static void RunTest(Guid easyTestKey, IDataLayer dataLayer, bool isSystem) {
            Process process = null;
            lock (Locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    try {
                        var lastEasyTestExecutionInfo = easyTest.LastEasyTestExecutionInfo;
                        var user = lastEasyTestExecutionInfo.WindowsUser;
                        SetupEnviroment(easyTest);
                        var processStartInfo = GetProcessStartInfo(easyTest, user, isSystem);

                        process = new Process {
                            StartInfo = processStartInfo
                        };
                        process.Start();
                        lastEasyTestExecutionInfo = unitOfWork.GetObjectByKey<EasyTestExecutionInfo>(lastEasyTestExecutionInfo.Oid, true);
                        lastEasyTestExecutionInfo.Update(EasyTestState.Running);
                        unitOfWork.ValidateAndCommitChanges();

                        Thread.Sleep(5000);
                    }
                    catch (Exception e) {
                        LogErrors(easyTest, e);
                    }
                }
            }
            if (process != null) {
                process.WaitForExit();
                AfterProcessExecute(dataLayer, easyTestKey);
            }
        }

        private static void LogErrors(EasyTest easyTest, Exception e) {
            lock (Locker) {
                easyTest.LastEasyTestExecutionInfo.Update(EasyTestState.Failed);
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
        private static ProcessStartInfo GetProcessStartInfo(EasyTest easyTest, WindowsUser user, bool isSystem) {
            string testExecutor = string.Format("TestExecutor.v{0}.exe", AssemblyInfo.VersionShort);
            var arguments = isSystem ? string.Format("-e " + testExecutor + " -u {0} -p {1} -a {2}", user.Name, user.Password, Path.GetFileName(easyTest.FileName)) : Path.GetFileName(easyTest.FileName);
            string directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
            var exe = isSystem ? "ProcessAsUser.exe" : testExecutor;
            var processStartInfo = new ProcessStartInfo(Path.Combine(directoryName, exe), arguments) {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directoryName
            };
            return processStartInfo;
        }

        private static void AfterProcessExecute(IDataLayer dataLayer, Guid easyTestKey) {
            lock (Locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                    CopyXafLogs(directoryName);
                    var logTests = easyTest.GetFailedLogTests();
                    var state = EasyTestState.Passed;
                    if (logTests.All(test => test.Result == "Passed")) {
                        Tracing.Tracer.LogText(easyTest.FileName + " passed");
                    }
                    else {
                        Tracing.Tracer.LogText(easyTest.FileName + " not passed=" + string.Join(Environment.NewLine,
                            logTests.SelectMany(test => test.Errors.Select(error => error.Message.Text))));
                        state = EasyTestState.Failed;
                    }
                    easyTest.LastEasyTestExecutionInfo.Update(state);
                    easyTest.Session.ValidateAndCommitChanges();
                }
            }
        }

        private static void CopyXafLogs(string directoryName) {
            string fileName = Path.Combine(directoryName, "config.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, directoryName);
                foreach (var alias in options.Aliases.Cast<TestAlias>().Where(@alias => alias.ContainsAppPath())) {
                    var suffix = alias.IsWinAppPath() ? "_win" : "_web";
                    var sourceFileName = Path.Combine(alias.Value, "eXpressAppFramework.log");
                    if (File.Exists(sourceFileName)) {
                        File.Copy(sourceFileName, Path.Combine(directoryName, "eXpressAppFramework" + suffix + ".log"), true);
                    }
                }
            }
        }

        private static void SetupEnviroment(EasyTest easyTest) {
            TestEnviroment.Setup(easyTest.LastEasyTestExecutionInfo);
        }

        public static void Execute(string fileName, bool isSystem) {
            var fileNames = File.ReadAllLines(fileName).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            var objectSpace = ApplicationHelper.Instance.Application.CreateObjectSpace();
            var easyTests = EasyTest.GetTests(objectSpace, fileNames);
            objectSpace.Session().ValidateAndCommitChanges();
            Execute(easyTests, isSystem, null);
        }

        public static CancellationTokenSource Execute(EasyTest[] easyTests, bool isSystem, Action<Task> finished) {
            Tracing.Tracer.LogValue("EasyTests.Count", easyTests.Count());
            if (easyTests.Any()) {
                var dataLayer = GetDatalayer();
                var tokenSource = new CancellationTokenSource();
                Task.Factory.StartNew(() => ExecuteCore(easyTests, isSystem, dataLayer, tokenSource.Token), tokenSource.Token).ContinueWith(task => finished, tokenSource.Token);
                return tokenSource;
            }
            return null;
        }

        private static void ExecuteCore(EasyTest[] easyTests, bool isSystem, IDataLayer dataLayer, CancellationToken token) {
            var executionInfoKey = CreateExecutionInfoKey(dataLayer, isSystem, easyTests);
            do {
                if (token.IsCancellationRequested)
                    return;
                var easyTest = GetNextEasyTest(executionInfoKey, easyTests, dataLayer, isSystem);
                if (easyTest != null) {
                    Task.Factory.StartNew(() => RunTest(easyTest.Oid, dataLayer, isSystem), token).TimeoutAfter(1000 * 60 * 60);
                }
                Thread.Sleep(10000);
            } while (!ExecutionFinished(dataLayer, executionInfoKey, easyTests.Length));
        }

        private static IDataLayer GetDatalayer() {
            var xpObjectSpaceProvider = new XPObjectSpaceProvider(new ConnectionStringDataStoreProvider(ApplicationHelper.Instance.Application.ConnectionString), true);
            return xpObjectSpaceProvider.CreateObjectSpace().Session().DataLayer;
        }

        private static Guid CreateExecutionInfoKey(IDataLayer dataLayer, bool isSystem, EasyTest[] easyTests) {
            Guid executionInfoKey;
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = ExecutionInfo.Create(unitOfWork, isSystem);
                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                foreach (var easyTest in easyTests) {
                    easyTest.CreateExecutionInfo(isSystem, executionInfo);
                }
                unitOfWork.ValidateAndCommitChanges();
                CurrentSequenceOperator.CurrentSequence = executionInfo.Sequence;
                executionInfoKey = executionInfo.Oid;
            }
            return executionInfoKey;
        }

        private static EasyTest GetNextEasyTest(Guid executionInfoKey, EasyTest[] easyTests, IDataLayer dataLayer, bool isSystem) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey);
                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();

                var runningInfosCount = executionInfo.EasyTestRunningInfos.Count();
                if (runningInfosCount < executionInfo.WindowsUsers.Count()) {
                    var neverRunTest = GetEasyTest(easyTests, executionInfo, 0);
                    if (neverRunTest != null) {
                        var windowsUser = executionInfo.GetNextUser(neverRunTest);
                        if (windowsUser != null) {
                            neverRunTest.LastEasyTestExecutionInfo.WindowsUser = windowsUser;
                            neverRunTest.Session.ValidateAndCommitChanges();
                            return neverRunTest;
                        }
                        return null;
                    }
                    for (int i = 0; i < ((IModelOptionsTestExecutor)CaptionHelper.ApplicationModel.Options).ExecutionRetries; i++) {
                        var easyTest = GetEasyTest(easyTests, executionInfo, i + 1);
                        if (easyTest != null) {
                            var windowsUser = executionInfo.GetNextUser(easyTest);
                            if (windowsUser != null) {
                                easyTest.CreateExecutionInfo(isSystem, executionInfo, windowsUser);
                                easyTest.Session.ValidateAndCommitChanges();
                                return easyTest;
                            }
                            return null;
                        }
                    }
                }
            }
            return null;
        }

        private static EasyTest GetEasyTest(IEnumerable<EasyTest> easyTests, ExecutionInfo executionInfo, int i) {
            return executionInfo.GetTestsToExecute(i).FirstOrDefault(easyTests.Contains);
        }


    }
}