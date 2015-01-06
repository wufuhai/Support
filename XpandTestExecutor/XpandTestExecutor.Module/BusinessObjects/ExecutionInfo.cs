using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DevExpress.ExpressApp.Utils;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;

namespace XpandTestExecutor.Module.BusinessObjects {
    [DefaultProperty("Sequence")]
    [FriendlyKeyProperty("Sequence")]
    public class ExecutionInfo : BaseObject, ISupportSequenceObject {
        private DateTime _creationDate;

        public ExecutionInfo(Session session)
            : base(session) {
        }

        public int Duration {
            //            get{return EasyTestExecutionInfos.Duration();}
            get { return EasyTestExecutionInfos.Duration(); }
        }
        [Association("ExecutionInfos-Users")]
        public XPCollection<WindowsUser> WindowsUsers {
            get { return GetCollection<WindowsUser>("WindowsUsers"); }
        }

        [Association("EasyTestExecutionInfo-ExecutionInfos")]
        public XPCollection<EasyTestExecutionInfo> EasyTestExecutionInfos {
            get { return GetCollection<EasyTestExecutionInfo>("EasyTestExecutionInfos"); }
        }

        public XPCollection<EasyTestExecutionInfo> EasyTestRunningInfos {
            get {
                return new XPCollection<EasyTestExecutionInfo>(Session, EasyTestExecutionInfos.Where(info => info.State == EasyTestState.Running));
            }
        }

        [VisibleInListView(false)]
        public DateTime CreationDate {
            get { return _creationDate; }
            set { SetPropertyValue("CreationDate", ref _creationDate, value); }
        }

        [InvisibleInAllViews]
        public XPCollection<EasyTestExecutionInfo> PassedEasyTestExecutionInfos {
            get {
                IEnumerable<EasyTest> passedEasyTests =
                    EasyTestExecutionInfos.GroupBy(info => info.EasyTest)
                        .Where(infos => infos.Any(info => info.State == EasyTestState.Passed))
                        .Select(infos => infos.Key);
                return new XPCollection<EasyTestExecutionInfo>(Session,
                    EasyTestExecutionInfos.Where(info => passedEasyTests.Contains(info.EasyTest)));
            }
        }


        [InvisibleInAllViews]
        public bool Failed {
            get { return PassedEasyTestExecutionInfos.Count != EasyTestExecutionInfos.Count; }
        }

        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix {
            get { return ""; }
        }

        protected override void OnSaving() {
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }

        public static ExecutionInfo Create(UnitOfWork unitOfWork, bool isSystem) {
            IEnumerable<WindowsUser> windowsUsers = WindowsUser.CreateUsers(unitOfWork, isSystem);
            var executionInfo = new ExecutionInfo(unitOfWork);
            executionInfo.WindowsUsers.AddRange(windowsUsers);
            return executionInfo;
        }

        public override void AfterConstruction() {
            base.AfterConstruction();
            CreationDate = DateTime.Now;
        }

        public EasyTest[] GetTestsToExecute(int retries) {
            if (retries == 0) {
                var neverRunEasyTests = GetNeverRunEasyTests().Select(test => new { Test = test, Duration = test.LastPassedDuration() });
                return neverRunEasyTests.Select(arg => arg.Test).ToArray();
            }
            return EasyTestExecutionInfos.GroupBy(executionInfo => executionInfo.EasyTest)
                    .Where(infos => infos.All(info => info.State == EasyTestState.Failed) && infos.Count() == retries)
                    .Select(infos => new { Test = infos.Key, Count = infos.Count() })
                    .OrderBy(arg => arg.Count)
                    .Select(arg => arg.Test)
                    .ToArray();
        }

        private IEnumerable<EasyTest> GetNeverRunEasyTests() {
            return EasyTestExecutionInfos.GroupBy(info => info.EasyTest)
                .Where(infos => infos.Count() == 1 && infos.First().State == EasyTestState.NotStarted)
                .SelectMany(infos => infos).Select(info => info.EasyTest).Distinct().OrderByDescending(test => test.LastPassedDuration()).ToArray();
        }

        public IEnumerable<WindowsUser> GetUsedUsers(EasyTest easytest) {
            return EasyTestExecutionInfos.Where(info => ReferenceEquals(info.EasyTest, easytest) && info.State == EasyTestState.Passed || info.State == EasyTestState.Failed).Select(info => info.WindowsUser).Distinct();
        }

        public IEnumerable<WindowsUser> GetIdleUsers() {
            var users = EasyTestRunningInfos.Select(info => info.WindowsUser).Distinct();
            return WindowsUsers.Except(users);
        }

        public int FinishedEasyTestExecutionInfos() {
            return EasyTestExecutionInfos.Where(info => info.State > EasyTestState.Running).GroupBy(info => info.EasyTest)
                    .Count(infos => infos.Count() == ((IModelOptionsTestExecutor)CaptionHelper.ApplicationModel.Options).ExecutionRetries + 1);
        }

        public WindowsUser GetNextUser(EasyTest easyTest) {
            var lastWindowsUser = easyTest.LastEasyTestExecutionInfo != null ? easyTest.LastEasyTestExecutionInfo.WindowsUser : null;
            var windowsUsers = GetIdleUsers().ToArray();
            return windowsUsers.Except(GetUsedUsers(easyTest).Concat(new[] { lastWindowsUser })).FirstOrDefault() ?? windowsUsers.Except(new[] { lastWindowsUser }).FirstOrDefault() ?? lastWindowsUser;
        }



    }
}