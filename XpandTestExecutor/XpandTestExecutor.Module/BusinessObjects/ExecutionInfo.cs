using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DevExpress.Data.Filtering;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;

namespace XpandTestExecutor.Module.BusinessObjects{
    [DefaultProperty("Sequence")]
    [FriendlyKeyProperty("Sequence")]
    public class ExecutionInfo : BaseObject, ISupportSequenceObject{
        private DateTime _creationDate;
        private int _executionRetries;

        public ExecutionInfo(Session session)
            : base(session){
        }

        public double Duration{
            get { return EasyTestExecutionInfos.Duration(); }
        }

        [VisibleInListView(false)]
        public int ExecutionRetries{
            get { return _executionRetries; }
            set { SetPropertyValue("ExecutionRetries", ref _executionRetries, value); }
        }

        [Association("ExecutionInfos-Users")]
        public XPCollection<WindowsUser> WindowsUsers{
            get { return GetCollection<WindowsUser>("WindowsUsers"); }
        }

        [Association("EasyTestExecutionInfo-ExecutionInfos")]
        public XPCollection<EasyTestExecutionInfo> EasyTestExecutionInfos{
            get { return GetCollection<EasyTestExecutionInfo>("EasyTestExecutionInfos"); }
        }

        public XPCollection<EasyTestExecutionInfo> EasyTestExecutingInfos{
            get{
                CriteriaOperator expression = new XPQuery<EasyTestExecutionInfo>(Session).TransformExpression(info
                    =>
                    info.ExecutionInfo.Oid == Oid &&
                    (info.State != EasyTestExecutionInfoState.Failed && info.State != EasyTestExecutionInfoState.Passed));
                return new XPCollection<EasyTestExecutionInfo>(Session, expression);
            }
        }

        [VisibleInListView(false)]
        public DateTime CreationDate{
            get { return _creationDate; }
            set { SetPropertyValue("CreationDate", ref _creationDate, value); }
        }

        [InvisibleInAllViews]
        public XPCollection<EasyTestExecutionInfo> PassedEasyTestExecutionInfos{
            get{
                IEnumerable<EasyTest> passedEasyTests =
                    EasyTestExecutionInfos.GroupBy(info => info.EasyTest)
                        .Where(infos => infos.Any(info => info.State == EasyTestExecutionInfoState.Passed))
                        .Select(infos => infos.Key);
                return new XPCollection<EasyTestExecutionInfo>(Session,
                    EasyTestExecutionInfos.Where(info => passedEasyTests.Contains(info.EasyTest)));
            }
        }


        [InvisibleInAllViews]
        public bool Failed{
            get { return PassedEasyTestExecutionInfos.Count != EasyTestExecutionInfos.Count; }
        }

        [InvisibleInAllViews]
        public XPCollection<EasyTestExecutionInfo> FailedEasyTestExecutionInfos{
            get { return EasyTestExecutionInfos.Failed(); }
        }

        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix{
            get { return ""; }
        }

        protected override void OnSaving(){
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }

        public static ExecutionInfo Create(UnitOfWork unitOfWork, bool isSystem){
            IEnumerable<WindowsUser> windowsUsers = WindowsUser.CreateUsers(unitOfWork,isSystem);
            var executionInfo = new ExecutionInfo(unitOfWork);
            executionInfo.WindowsUsers.AddRange(windowsUsers);
            return executionInfo;
        }

        public override void AfterConstruction(){
            base.AfterConstruction();
            ExecutionRetries = 2;
            CreationDate = DateTime.Now;
        }

        public int ExecutionFinished(){
            int passedEasyTests = PassedEasyTestExecutionInfos.Select(info => info.EasyTest).Distinct().Count();
            int failed =
                FailedEasyTestExecutionInfos.GroupBy(info => info.EasyTest)
                    .Count(infos => infos.Count() == ExecutionRetries);
            return failed + passedEasyTests;
        }


        public IQueryable<EasyTest> GetTestsToExecute(int retries){
            if (retries == 0){
                var easyTests = new XPQuery<EasyTest>(Session);
                return easyTests.Where(test => test.EasyTestExecutionInfos.All(info => info.ExecutionInfo.Oid != Oid));
            }

            IEnumerable<EasyTest> failedEasyTests =
                FailedEasyTestExecutionInfos.GroupBy(info => info.EasyTest)
                    .Where(infos => infos.Count() == retries)
                    .Select(infos => infos.Key)
                    .Distinct();
            IEnumerable<EasyTest> executingTests = EasyTestExecutingInfos.Select(info => info.EasyTest).Distinct();
            return
                new XPQuery<EasyTest>(Session).Where(
                    test => failedEasyTests.Contains(test) && !executingTests.Contains(test));
        }

        public IEnumerable<WindowsUser> GetIdleUsers(){
            IEnumerable<WindowsUser> users =
                EasyTestExecutionInfos.Where(
                    info =>
                        (info.State == EasyTestExecutionInfoState.Running ||
                         info.State == EasyTestExecutionInfoState.NotStarted))
                    .Select(info => info.WindowsUser)
                    .Distinct();
            return WindowsUsers.Except(users);
        }
    }
}