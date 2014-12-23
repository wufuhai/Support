using System;
using System.IO;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Controllers{
    public class TestController : ObjectViewController<ListView, EasyTest> {
        private const string ItemSelected = "Selected";
        private const string ItemFromFile = "FromFile";
        private const string AsSystem = "AsSystem";
        private const string AsCurrent = "AsCurrent";
        public TestController() {
            var singleChoiceAction = new SingleChoiceAction(this, "RunTest", PredefinedCategory.View) { Caption = "Run" };
            singleChoiceAction.Execute += SingleChoiceActionOnExecute;
            singleChoiceAction.ItemType = SingleChoiceActionItemType.ItemIsOperation;
            var choiceActionItem = new ChoiceActionItem(ItemSelected, ItemSelected);
            AddChildChoices(choiceActionItem);
            singleChoiceAction.Items.Add(choiceActionItem);
            var actionItem = new ChoiceActionItem(ItemFromFile, ItemFromFile);
            AddChildChoices(actionItem);
            singleChoiceAction.Items.Add(actionItem);
        }

        private void AddChildChoices(ChoiceActionItem choiceActionItem){
            choiceActionItem.Items.Add(new ChoiceActionItem(AsSystem, AsSystem));
            choiceActionItem.Items.Add(new ChoiceActionItem(AsCurrent, AsCurrent));
        }

        private void SingleChoiceActionOnExecute(object sender, SingleChoiceActionExecuteEventArgs singleChoiceActionExecuteEventArgs) {
            var isSystem = ReferenceEquals(singleChoiceActionExecuteEventArgs.SelectedChoiceActionItem.Data, AsSystem);
            if (ReferenceEquals(singleChoiceActionExecuteEventArgs.SelectedChoiceActionItem.ParentItem.Data, ItemSelected)){
                TestRunner.Execute(singleChoiceActionExecuteEventArgs.SelectedObjects.Cast<EasyTest>().ToArray(), isSystem);
            }
            else {
                TestRunner.Execute(Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "EasyTests.txt"),isSystem);
            }
        }

    }
}