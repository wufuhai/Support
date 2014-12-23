using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace XpandTestExecutor.Module.BusinessObjects{
    [DefaultClassOptions]
    public class EasyTestApplication : BaseObject {
        public EasyTestApplication(Session session)
            : base(session) {
        }
        [RuleUniqueValue]
        [RuleRequiredField]
        public string Name { get; set; }
        [Association("EasyTestExecutionInfo-EasyTestApplications")]
        public XPCollection<EasyTestExecutionInfo> EasyTestExecutionInfos {
            get {
                return GetCollection<EasyTestExecutionInfo>("EasyTestExecutionInfos");
            }
        }
    }
}