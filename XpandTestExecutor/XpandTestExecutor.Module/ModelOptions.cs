using System.ComponentModel;

namespace XpandTestExecutor.Module {
    public interface IModelOptionsTestExecutor
    {
        [Category("TestExecutor")]
        [DefaultValue(2)]
        int ExecutionRetries { get; set; }     
    }
}
