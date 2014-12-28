using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;

namespace XpandTestExecutor.Module.BusinessObjects{
    [DefaultClassOptions]
    [DefaultProperty("Name")]
    public class EasyTest : BaseObject, ISupportSequenceObject{
        private string _application;
        private EasyTestExecutionInfo _lastEasyTestExecutionInfo;

        public EasyTest(Session session)
            : base(session) {
        }

        public LogTest[] GetFailedLogTests() {
            var directoryName = Path.GetDirectoryName(FileName) + "";
            var fileName = Path.Combine(directoryName, "testslog.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return LogTests.LoadTestsResults(optionsStream).Tests.Where(test 
                    => test != null && test.Name.ToLowerInvariant() == (Path.GetFileNameWithoutExtension(FileName) + "").ToLowerInvariant()).Where(test 
                        =>test.Result!="Passed" ).ToArray();
            }
        }
        public double Duration {
            get { return CurrentSequenceInfos.Duration(); }
        }

        [InvisibleInAllViews]
        public XPCollection<EasyTestExecutionInfo> FailedEasyTestExecutionInfos {
            get { return CurrentSequenceInfos.Failed(); }
        }

        [InvisibleInAllViews]
        public XPCollection<EasyTestExecutionInfo> CurrentSequenceInfos {
            get {
                return new XPCollection<EasyTestExecutionInfo>(Session,
                    EasyTestExecutionInfos.Where(
                        info => info.ExecutionInfo.Sequence == CurrentSequenceOperator.CurrentSequence));
            }
        }

        [InvisibleInAllViews]
        public bool Failed {
            get { return FailedEasyTestExecutionInfos.Select(info => info.EasyTest).Distinct().Contains(this); }
        }

        [Association("EasyTestExecutionInfo-EasyTests")]
        public XPCollection<EasyTestExecutionInfo> EasyTestExecutionInfos {
            get { return GetCollection<EasyTestExecutionInfo>("EasyTestExecutionInfos"); }
        }

        [Size(SizeAttribute.Unlimited)]
        [RuleUniqueValue]
        [ModelDefault("RowCount", "1")]
        public string FileName { get; set; }

        [VisibleInDetailView(false)]
        [RuleRequiredField]
        public string Application{
            get { return _application; }
            set { SetPropertyValue("Application", ref _application, value); }
        }

        [VisibleInDetailView(false)]
        public string Name{
            get { return Path.GetFileNameWithoutExtension(FileName); }
        }

        [InvisibleInAllViews]
        public EasyTestExecutionInfo LastEasyTestExecutionInfo{
            get { return _lastEasyTestExecutionInfo ?? GetLastInfo(); }
        }

        [Browsable(false)]
        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix{
            get { return null; }
        }

        public void CreateExecutionInfo(bool useCustomPort, ExecutionInfo executionInfo){
            _lastEasyTestExecutionInfo = new EasyTestExecutionInfo(Session){
                ExecutionInfo = executionInfo,
                EasyTest = this,
                WinPort = 4100,
                WebPort = 4030
            };
            _lastEasyTestExecutionInfo.CreateApplications(Path.GetDirectoryName(FileName));
            if (useCustomPort){
                IQueryable<EasyTestExecutionInfo> executionInfos =
                    new XPQuery<EasyTestExecutionInfo>(Session, true).Where(
                        info => info.ExecutionInfo.Oid == executionInfo.Oid);
                int winPort = executionInfos.Max(info => info.WinPort);
                int webPort = executionInfos.Max(info => info.WebPort);
                _lastEasyTestExecutionInfo.WinPort = winPort + 1;
                _lastEasyTestExecutionInfo.WebPort = webPort + 1;
            }
            EasyTestExecutionInfos.Add(_lastEasyTestExecutionInfo);
            

        }

        private EasyTestExecutionInfo GetLastInfo(){
            if (EasyTestExecutionInfos.Any()){
                long max = EasyTestExecutionInfos.Max(info => info.Sequence);
                return EasyTestExecutionInfos.First(info => info.Sequence == max);
            }
            return null;
        }

        protected override void OnSaving(){
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }

        public static EasyTest[] GetTests(IObjectSpace objectSpace, string[] fileNames){
            var easyTests = new EasyTest[fileNames.Length];
            for (int index = 0; index < fileNames.Length; index++){
                var fileName = fileNames[index];
                string name = fileName;
                var easyTest = objectSpace.FindObject<EasyTest>(test => test.FileName == name) ??objectSpace.CreateObject<EasyTest>();
                easyTest.FileName = fileName;
                var configPath = Path.Combine(Path.GetDirectoryName(fileName) + "", "config.xml");
                using (var optionsStream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read)){
                    var options = Options.LoadOptions(optionsStream, null, null, Path.GetDirectoryName(configPath));
                    easyTest.Application =options.Applications.Cast<TestApplication>().Select(application => application.Name.Replace(".Win", "").Replace(".Web", "")).First();
                }
                easyTests[index] = easyTest;
            }
            return easyTests;
        }
    }
}