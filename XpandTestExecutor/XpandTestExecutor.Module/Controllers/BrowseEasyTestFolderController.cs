using System.Diagnostics;
using System.IO;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Controllers {
    public class BrowseEasyTestFolderController:ObjectViewController<ListView,EasyTest> {
        public BrowseEasyTestFolderController() {
            var simpleAction = new SimpleAction(this,"BrowseEasyTestFolder",PredefinedCategory.View){Caption = "Browse",SelectionDependencyType = SelectionDependencyType.RequireSingleObject};
            simpleAction.Execute+=SimpleActionOnExecute;
        }

        private void SimpleActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            var easyTest = View.SelectedObjects.Cast<EasyTest>().First();
            Process.Start(Path.GetDirectoryName(easyTest.FileName) + "");
        }
    }
}
