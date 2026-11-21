using System.Windows;
using StudentScoreTool.Services;
using StudentScoreTool.Themes;

namespace StudentScoreTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var settings = AppSettings.Load();
        ThemeManager.Apply(settings.Theme);
        base.OnStartup(e);
    }
}
