using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using DevExpress.EasyTest.Framework;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;

namespace XpandTestExecutor.Module.BusinessObjects {
    [DefaultClassOptions]
    [DefaultProperty("Sequence")]
    public class EasyTestExecutionInfo : BaseObject, ISupportSequenceObject {
        private WindowsUser _windowsUser;
        private EasyTest _easyTest;
        private DateTime _end;
        private DateTime _start;
        private EasyTestState _state;
        private int _webPort;
        private int _winPort;

        public EasyTestExecutionInfo(Session session)
            : base(session) {
        }
        [Size(SizeAttribute.Unlimited), Delayed]
        public string TestsLog {
            get { return GetDelayedPropertyValue<string>("TestsLog"); }
            set { SetDelayedPropertyValue("TestsLog", value); }
        }

        [Size(SizeAttribute.Unlimited), Delayed]
        public string WinLog {
            get { return GetDelayedPropertyValue<string>("WinLog"); }
            set { SetDelayedPropertyValue("WinLog", value); }
        }

        [Size(SizeAttribute.Unlimited), Delayed]
        public string WebLog {
            get { return GetDelayedPropertyValue<string>("WebLog"); }
            set { SetDelayedPropertyValue("WebLog", value); }
        }

        [ValueConverter(typeof(ImageValueConverter))]
        [Delayed]
        public Image WebView {
            get { return GetDelayedPropertyValue<Image>("WebView"); }
            set { SetDelayedPropertyValue("WebView", value); }
        }

        [ValueConverter(typeof(ImageValueConverter))]
        [Delayed]
        public Image WinView {
            get { return GetDelayedPropertyValue<Image>("WinView"); }
            set { SetDelayedPropertyValue("WinView", value); }
        }

        public int WinPort {
            get { return _winPort; }
            set { SetPropertyValue("WinPort", ref _winPort, value); }
        }

        public int WebPort {
            get { return _webPort; }
            set { SetPropertyValue("WebPort", ref _webPort, value); }
        }

        [Association("EasyTestExecutionInfo-EasyTestApplications")]
        public XPCollection<EasyTestApplication> EasyTestApplications {
            get { return GetCollection<EasyTestApplication>("EasyTestApplications"); }
        }

        [Association("EasyTestExecutionInfo-EasyTests")]
        public EasyTest EasyTest {
            get { return _easyTest; }
            set { SetPropertyValue("EasyTest", ref _easyTest, value); }
        }

        [Association("EasyTestExecutionInfo-ExecutionInfos")]
        public ExecutionInfo ExecutionInfo { get; set; }

        public int Duration {
            get { return (int)End.Subtract(Start).TotalMinutes; }
        }
        [InvisibleInAllViews]
        public DateTime End {
            get { return _end; }
            set { SetPropertyValue("End", ref _end, value); }
        }

        [InvisibleInAllViews]
        public DateTime Start {
            get { return _start; }
            set { SetPropertyValue("Start", ref _start, value); }
        }

        public EasyTestState State {
            get { return _state; }
            set { SetPropertyValue("State", ref _state, value); }
        }

        [RuleRequiredField(TargetCriteria = "State='Running'")]
        public WindowsUser WindowsUser {
            get { return _windowsUser; }
            set { SetPropertyValue("WindowsUser", ref _windowsUser, value); }
        }

        [InvisibleInAllViews]
        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix {
            get { return ((ISupportSequenceObject)EasyTest).Sequence.ToString(CultureInfo.InvariantCulture); }
        }

        public void SetView(bool win, Image view) {
            if (win)
                WinView = view;
            else {
                WebView = view;
            }
        }

        protected override void OnSaving() {
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }

        public void CreateApplications(string directory) {
            using (
                var optionsStream = new FileStream(Path.Combine(directory, "config.xml"), FileMode.Open, FileAccess.Read,
                    FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, directory);
                foreach (TestApplication application in options.Applications.Cast<TestApplication>()) {
                    EasyTestApplications.Add(
                        new XPQuery<EasyTestApplication>(Session,true).FirstOrDefault(
                            testApplication => testApplication.Name == application.Name) ??
                        new EasyTestApplication(Session) { Name = application.Name });
                }
            }
        }

        public void SetLog(bool isWin, string text) {
            if (isWin)
                WinLog = text;
            else {
                WebLog = text;
            }
        }

        public void Update(EasyTestState easyTestState) {
            State = easyTestState;
            if (State == EasyTestState.Running) {
                Start = DateTime.Now;
            }
            else if (State == EasyTestState.Passed || State == EasyTestState.Failed)
                End = DateTime.Now;

            var path = Path.GetDirectoryName(EasyTest.FileName) + "";
            if (State == EasyTestState.Failed) {
                var logTests = EasyTest.GetFailedLogTests();

                foreach (var platform in new[] { "Win", "Web" }) {
                    var logTest = logTests.FirstOrDefault(test => test.ApplicationName.Contains("." + platform));
                    if (logTest != null) {
                        var fileName = Directory.GetFiles(path, EasyTest.Name + "_*." + platform + "_View.jpeg").FirstOrDefault();
                        if (fileName != null)
                            SetView(platform == "Win", Image.FromFile(fileName));
                        fileName = Directory.GetFiles(path, "eXpressAppFramework_" + platform + ".log").FirstOrDefault();
                        if (fileName != null)
                            SetLog(platform == "Win", File.ReadAllText(fileName));
                        TestsLog = File.ReadAllText(Path.Combine(path, "TestsLog.xml"));
                    }
                }
            }

        }
    }

    public enum EasyTestState {
        NotStarted,
        Running,
        Failed,
        Passed
    }
}