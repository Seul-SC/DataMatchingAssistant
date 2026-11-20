using Velopack;

namespace StudentScoreTool;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack 会在安装、更新、卸载时用特殊参数启动主程序，必须在 WPF 启动之前处理这些钩子。
        VelopackApp.Build().Run();

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }
}
