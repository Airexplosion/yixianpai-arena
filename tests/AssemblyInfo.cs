// TestHost 是进程级单例（SdkRuntime 静态状态），测试必须串行执行。
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
