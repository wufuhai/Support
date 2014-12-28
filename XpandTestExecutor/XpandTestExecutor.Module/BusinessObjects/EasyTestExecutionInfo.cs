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
    public class EasyTestExecutionInfo : BaseObject, ISupportSequenceObject {
        private string _testsLog;
        private EasyTest _easyTest;
        private TimeSpan _end;
        private TimeSpan _start;
        private EasyTestState _state;
        private string _webLog;
        private int _webPort;
        private Image _webView;
        private string _winLog;
        private int _winPort;
        private Image _winView;

        public EasyTestExecutionInfo(Session session)
            : base(session) {
        }
        [Size(SizeAttribute.Unlimited)]
        public string TestsLog {
            get { return _testsLog; }
            set { SetPropertyValue("TestsLog", ref _testsLog, value); }
        }

        [Size(SizeAttribute.Unlimited)]
        public string WinLog {
            get { return _winLog; }
            set { SetPropertyValue("WinLog", ref _winLog, value); }
        }

        [Size(SizeAttribute.Unlimited)]
        public string WebLog {
            get { return _webLog; }
            set { SetPropertyValue("WebLog", ref _webLog, value); }
        }

        [ValueConverter(typeof (ImageValueConverter))]
        public Image WebView {
            get { return _webView; }
            set { SetPropertyValue("WebView", ref _webView, value); }
        }

        [ValueConverter(typeof (ImageValueConverter))]
        public Image WinView {
            get { return _winView; }
            set { SetPropertyValue("WinView", ref _winView, value); }
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
        public TimeSpan End {
            get { return _end; }
            set { SetPropertyValue("End", ref _end, value); }
        }

        [InvisibleInAllViews]
        public TimeSpan Start {
            get { return _start; }
            set { SetPropertyValue("Start", ref _start, value); }
        }

        public EasyTestState State {
            get { return _state; }
            set { SetPropertyValue("State", ref _state, value); }
        }

        [RuleRequiredField]
        public WindowsUser WindowsUser { get; set; }

        [Browsable(false)]
        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix {
            get { return ((ISupportSequenceObject) EasyTest).Sequence.ToString(CultureInfo.InvariantCulture); }
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
                        new XPQuery<EasyTestApplication>(Session).FirstOrDefault(
                            testApplication => testApplication.Name == application.Name) ??
                        new EasyTestApplication(Session) {Name = application.Name});
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
                Start = DateTime.Now.TimeOfDay;
                if (!ExecutionInfo.EasyTestExecutionInfos.Any(info =>new []{EasyTestState.Failed,EasyTestState.Passed, EasyTestState.Running}.Contains(info.State)))
                    ExecutionInfo.Start=DateTime.Now.TimeOfDay;
            }
            else if (State == EasyTestState.Passed || State == EasyTestState.Failed)
                End = DateTime.Now.TimeOfDay;
            if (State == EasyTestState.Failed) {
                var path = Path.GetDirectoryName(EasyTest.FileName) + "";
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
        Passed,
        Failed
    }
}