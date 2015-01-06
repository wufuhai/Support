using System;
using System.IO;
using System.Linq;
using System.Threading;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Controllers {
    public class TestController : ObjectViewController<ListView, EasyTest> {
        private const string CancelRun = "Cancel Run";
        private readonly SingleChoiceAction _runTestAction;
        private CancellationTokenSource _cancellationTokenSource;
        private const string ItemSelected = "Selected";
        private const string ItemFromFile = "FromFile";
        private const string AsSystem = "AsSystem";
        private const string AsCurrent = "AsCurrent";
        private const string UnlinkUser = "Unlink user";
        public TestController() {
            _runTestAction = new SingleChoiceAction(this, "RunTest", PredefinedCategory.View) { Caption = "Run" };
            _runTestAction.Execute += RunTestActionOnExecute;
            _runTestAction.ItemType = SingleChoiceActionItemType.ItemIsOperation;
            UpdateAction(true);
        }

        private void AddChildChoices(ChoiceActionItem choiceActionItem) {
            choiceActionItem.Items.Add(new ChoiceActionItem(AsSystem, AsSystem));
            choiceActionItem.Items.Add(new ChoiceActionItem(AsCurrent, AsCurrent));
        }

        private void RunTestActionOnExecute(object sender, SingleChoiceActionExecuteEventArgs e) {
            var isSystem = ReferenceEquals(e.SelectedChoiceActionItem.Data, AsSystem);
            if (ReferenceEquals(e.SelectedChoiceActionItem.Data, CancelRun)) {
                _cancellationTokenSource.Cancel();
                UpdateAction(true);
            }
            else if (ReferenceEquals(e.SelectedChoiceActionItem.ParentItem.Data, ItemSelected)) {
                UpdateAction(false);
                _cancellationTokenSource = TestRunner.Execute(e.SelectedObjects.Cast<EasyTest>().ToArray(), isSystem, task => UpdateAction(true));
            }
            else if (ReferenceEquals(e.SelectedChoiceActionItem.ParentItem.Data, UnlinkUser)) {
                var easyTests = e.SelectedObjects.Cast<EasyTest>().ToArray();
                if (ReferenceEquals(e.SelectedChoiceActionItem.Data, ItemFromFile)) {
                    var fileNames = File.ReadAllLines("easytests.txt").Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    easyTests = EasyTest.GetTests(ObjectSpace, fileNames);
                }
                foreach (var info in easyTests.SelectMany(test => test.GetCurrentSequenceInfos())) {
                    info.WindowsUser = WindowsUser.CreateUsers((UnitOfWork)ObjectSpace.Session(), false).First();
                    TestEnviroment.Setup(info);
                }
                ObjectSpace.RollbackSilent();

            }
            else
                TestRunner.Execute(
                    Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "EasyTests.txt"), isSystem);

        }

        private void UpdateAction(bool startConfig) {
            _runTestAction.Items.Clear();
            if (startConfig) {
                _runTestAction.Caption = "Run";
                var choiceActionItem = new ChoiceActionItem(ItemSelected, ItemSelected);
                AddChildChoices(choiceActionItem);
                _runTestAction.Items.Add(choiceActionItem);

                var actionItem = new ChoiceActionItem(ItemFromFile, ItemFromFile);
                AddChildChoices(actionItem);
                _runTestAction.Items.Add(actionItem);

                actionItem = new ChoiceActionItem(UnlinkUser, UnlinkUser);
                actionItem.Items.Add(new ChoiceActionItem(ItemSelected, ItemSelected));
                actionItem.Items.Add(new ChoiceActionItem(ItemFromFile, ItemFromFile));
                _runTestAction.Items.Add(actionItem);
            }
            else {
                _runTestAction.Caption = CancelRun;
                _runTestAction.Items.Add(new ChoiceActionItem(CancelRun, CancelRun));
            }
        }
    }
}