using System;
using System.Linq;
using System.Text.RegularExpressions;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module {

    public static class Extensions {
        public static void ValidateAndCommitChanges(this Session session) {
            var unitOfWork = ((UnitOfWork)session);
            var objectSpace = new XPObjectSpace(XafTypesInfo.Instance, XpoTypesInfoHelper.GetXpoTypeInfoSource(), () => unitOfWork);
            var result = Validator.RuleSet.ValidateAllTargets(objectSpace, session.GetObjectsToSave(), ContextIdentifier.Save);
            if (result.ValidationOutcome == ValidationOutcome.Error)
                throw new Exception(result.GetFormattedErrorMessage());
            unitOfWork.CommitChanges();
        }

        public static XPCollection<EasyTestExecutionInfo> Failed(this XPCollection<EasyTestExecutionInfo> collection) {
            var failedEasyTests = collection.GroupBy(info => info.EasyTest).Where(infos
                    => infos.All(info => info.State == EasyTestState.Failed)).Select(infos => infos.Key);
            return new XPCollection<EasyTestExecutionInfo>(collection.Session,
                collection.Where(info => failedEasyTests.Contains(info.EasyTest)));
        }
        public static double Duration(this XPCollection<EasyTestExecutionInfo> collection) {
            return collection.Aggregate(0d, (current, easyTestExecutionInfo) => current + easyTestExecutionInfo.Duration);
        }
        public static string DefaultDBName(this TestDatabase testDatabase) {
            return Regex.Replace(testDatabase.DBName, "([^_]*)(.*)", "$1", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public static bool IsWinAppPath(this TestAlias alias) {
            return alias.Name.ToLowerInvariant().StartsWith("win");
        }

        public static string UpdateAppPath(this TestAlias alias, string userName) {
            string containerDir = userName == null ? null : TestRunner.EasyTestUsersDir + @"\";
            return IsWinAppPath(alias)
                ? Regex.Replace(alias.Value, @"(.*\\Bin\\)(.*)", @"$1EasyTest\" + containerDir + userName,
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline)
                : Regex.Replace(alias.Value, @"(.*\.web)(.*)", @"$1\" + containerDir + userName,
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public static bool ContainsAppPath(this TestAlias alias) {
            return alias.Name.ToLowerInvariant().EndsWith("bin");
        }
    }
}
