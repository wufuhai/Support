using System.Text.RegularExpressions;
using DevExpress.EasyTest.Framework;

namespace XpandTestExecutor {
    public static class Extensions {
        public static string DefaultDBName(this TestDatabase testDatabase)
        {
            return Regex.Replace(testDatabase.DBName, "([^_]*)(.*)", "$1", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public static bool IsWinAppPath(this TestAlias alias) {
            return alias.Name.ToLowerInvariant().StartsWith("win");
        }

        public static string UpdateAppPath(this TestAlias alias, string userName) {
            string containerDir = userName == null ? null : Program.EasyTestUsersDir + @"\";
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
