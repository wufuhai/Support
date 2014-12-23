using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using DevExpress.EasyTest.Framework;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;

namespace XpandTestExecutor.Module.BusinessObjects{
    [DefaultClassOptions]
    public class EasyTestExecutionInfo : BaseObject, ISupportSequenceObject {
        private TimeSpan _end;
        private TimeSpan _start;
        private EasyTest _easyTest;
        private EasyTestExecutionInfoState _state;

        public EasyTestExecutionInfo(Session session)
            : base(session) {
        }
        public int WinPort { get; set; }
        public int WebPort { get; set; }
        [Association("EasyTestExecutionInfo-EasyTestApplications")]
        public XPCollection<EasyTestApplication> EasyTestApplications {
            get {
                return GetCollection<EasyTestApplication>("EasyTestApplications");
            }
        }
        [Association("EasyTestExecutionInfo-EasyTests")]
        public EasyTest EasyTest {
            get { return _easyTest; }
            set { SetPropertyValue("EasyTest", ref _easyTest, value); }
        }
        [Association("EasyTestExecutionInfo-ExecutionInfos")]
        public ExecutionInfo ExecutionInfo { get; set; }

        public double Duration {
            get { return Math.Round(End.Subtract(Start).TotalMinutes,2); }
        }

        [InvisibleInAllViews]
        public TimeSpan End {
            get {
                return _end;
            }
            set {
                SetPropertyValue("End", ref _end, value);
            }
        }
        [InvisibleInAllViews]
        public TimeSpan Start {
            get { return _start; }
            set { SetPropertyValue("Start", ref _start, value); }
        }

        public EasyTestExecutionInfoState State {
            get { return _state; }
            set { SetPropertyValue("State", ref _state, value); }
        }

        [RuleRequiredField]
        public WindowsUser WindowsUser { get; set; }

        protected override void OnSaving() {
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }
        [Browsable(false)]
        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix {
            get { return ((ISupportSequenceObject)EasyTest).Sequence.ToString(CultureInfo.InvariantCulture); }
        }

        public void CreateApplications(string directory){
            using (var optionsStream = new FileStream(Path.Combine(directory, "config.xml"), FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, directory);
                foreach (var application in options.Applications.Cast<TestApplication>()){
                    EasyTestApplications.Add(new XPQuery<EasyTestApplication>(Session).FirstOrDefault(testApplication => testApplication.Name == application.Name) ?? new EasyTestApplication(Session) { Name = application.Name });        
                }
            }
            
        }
    }

    public enum EasyTestExecutionInfoState {
        NotStarted,
        Running,
        Passed,
        Failed
    }

}