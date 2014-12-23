using System;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using System.Collections.Generic;
using DevExpress.ExpressApp.Updating;

namespace XpandTestExecutor.Module {
    // For more typical usage scenarios, be sure to check out http://documentation.devexpress.com/#Xaf/clsDevExpressExpressAppModuleBasetopic.
    public sealed partial class XpandTestExecutorModule : ModuleBase {
        public XpandTestExecutorModule() {
            InitializeComponent();
        }
        public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB) {
            ModuleUpdater updater = new DatabaseUpdate.Updater(objectSpace, versionFromDB);
            return new[] { updater };
        }

        public override void CustomizeTypesInfo(ITypesInfo typesInfo){
            base.CustomizeTypesInfo(typesInfo);
            CurrentSequenceOperator.Register();
        }
    }
}
