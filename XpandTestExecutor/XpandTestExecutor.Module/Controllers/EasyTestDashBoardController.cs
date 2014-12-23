using System.Linq;
using DevExpress.ExpressApp;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.Controllers.Dashboard;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Controllers {
    public class EasyTestDashBoardController : ViewController<DashboardView> {
        protected override void OnActivated() {
            base.OnActivated();
            Frame.GetController<DashboardInteractionController>().ListViewFiltering += OnListViewFiltering;
        }

        protected override void OnDeactivated() {
            base.OnDeactivated();
            Frame.GetController<DashboardInteractionController>().ListViewFiltering -= OnListViewFiltering;
        }

        private void OnListViewFiltering(object sender, ListViewFilteringArgs listViewFilteringArgs) {
            listViewFilteringArgs.Handled = true;
            var executionInfo = listViewFilteringArgs.DataSourceListView.SelectedObjects.Cast<ExecutionInfo>().FirstOrDefault();
            if (executionInfo != null) {
                CurrentSequenceOperator.CurrentSequence = executionInfo.Sequence;
                var listView = ((ListView)listViewFilteringArgs.DashboardViewItem.Frame.View);

                var expression = ObjectSpace.TransformExpression<EasyTest>(test => test.EasyTestExecutionInfos.Any(info => info.ExecutionInfo.Sequence == CurrentSequenceOperator.CurrentSequence));
                listView.CollectionSource.Criteria["currentsequence"] = expression;

                //                listView.EditFrame.GetController<CurrentSequenceController>().CurrentSequence = sequence;
                //                var listPropertyEditor = ((DetailView) listView.EditFrame.View).GetItems<ListPropertyEditor>().FirstOrDefault(editor => editor.ListView!=null&&editor.ListView.ObjectTypeInfo.Type==typeof(EasyTestExecutionInfo));
                //
                //                if (listPropertyEditor != null)
                //                {
                //                    var criteria = listPropertyEditor.View.ObjectSpace.TransformExpression<EasyTestExecutionInfo>(info => info.ExecutionInfo.Sequence==sequence);
                //                    var filterAction = listPropertyEditor.Frame.GetController<FilterController>().SetFilterAction;
                //                    filterAction.Items.FindItemByID("Current").Data=criteria.ToString();
                //                    filterAction.DoExecute();
                //                }
            }
        }
    }
}
