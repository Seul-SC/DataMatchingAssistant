#if VELOPACK_BRIDGE
using Velopack;
#endif

namespace StudentScoreTool;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
#if VELOPACK_BRIDGE
        // 只有给旧版 Velopack 用户的过渡包会编译这段：Velopack 在更新、卸载时会用特殊参数
        // 启动主程序，必须在 WPF 启动前处理这些钩子并尽快退出。正式的 Inno 版本不包含 Velopack。
        VelopackApp.Build().Run();
#endif

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }
}
