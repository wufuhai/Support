using System.Diagnostics;
using System.IO;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Controllers {

    public class BrowseEasyTestFolderController : ObjectViewController<ListView, EasyTest> {
        private readonly ParametrizedAction _parametrizedAction;

        public BrowseEasyTestFolderController() {
            var simpleAction = new SimpleAction(this, "BrowseEasyTestFolder", PredefinedCategory.View) { Caption = "Browse", SelectionDependencyType = SelectionDependencyType.RequireSingleObject };
            simpleAction.Execute += SimpleActionOnExecute;
            _parametrizedAction = new ParametrizedAction(this, "ExecutionRetries", PredefinedCategory.View, typeof(int));
            _parametrizedAction.Execute += ParametrizedActionOnExecute;
        }

        protected override void OnActivated() {
            base.OnActivated();
            _parametrizedAction.Value = ((IModelOptionsTestExecutor)Application.Model.Options).ExecutionRetries;
        }

        private void ParametrizedActionOnExecute(object sender, ParametrizedActionExecuteEventArgs parametrizedActionExecuteEventArgs) {
            ((IModelOptionsTestExecutor)Application.Model.Options).ExecutionRetries = (int)parametrizedActionExecuteEventArgs.ParameterCurrentValue;
        }

        private void SimpleActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            var easyTest = View.SelectedObjects.Cast<EasyTest>().First();
            Process.Start(Path.GetDirectoryName(easyTest.FileName) + "");
        }
    }
}
