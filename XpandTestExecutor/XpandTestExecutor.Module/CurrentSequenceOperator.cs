using System;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp.SystemModule;

namespace XpandTestExecutor.Module {
    public class CurrentSequenceOperator : ICustomFunctionOperator {
        private static readonly CurrentSequenceOperator _instance = new CurrentSequenceOperator();
        public Type ResultType(params Type[] operands) {
            return typeof(long);
        }

        public static void Register() {
            CustomFunctionOperatorHelper.Register(_instance);
        }

        public object Evaluate(params object[] operands) {
            return CurrentSequence;
        }

        public string Name {
            get { return "CurrentSequence"; }
        }

        public static long CurrentSequence { get; set; }
    }
}
