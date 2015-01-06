using System;
using System.Collections.Generic;
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
                    => infos.All(info => info.State < EasyTestState.Passed)).Select(infos => infos.Key);
            var failedInfos = collection.Where(info => failedEasyTests.Contains(info.EasyTest));
            return new XPCollection<EasyTestExecutionInfo>(collection.Session, failedInfos);
        }

        public static int Duration(this IEnumerable<EasyTestExecutionInfo> collection) {
            var easyTestExecutionInfos = collection as EasyTestExecutionInfo[] ?? collection.ToArray();
            if (easyTestExecutionInfos.Any()) {
                var min = easyTestExecutionInfos.Min(info => info.Start);
                if (min != DateTime.MinValue) {
                    var max = easyTestExecutionInfos.Max(info => info.End);
                    return (int)max.Subtract(min).TotalMinutes;
                }
            }
            return 0;
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
