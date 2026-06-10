using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace SqPackFs;

public class MainWindow : Component
{
    public override Element Render()
    {
        var fsService = UseContext(Contexts.FsService);

        var window = UseWindow();

        var icon = UseMemo(() => WindowIcon.FromPath("Assets/TrayIcon.ico"));
        var tray = UseTrayIcon(new TrayIconSpec(
            Icon: icon,
            Tooltip: "SqPackFs",
            Key: WindowKey.Of("main-tray")));

        UseEffect(() =>
        {
            if (tray is null) return () => { };
            static void onClick(object? s, EventArgs e)
                => ReactorApp.PrimaryWindow?.Activate();
            tray.Click += onClick;
            return () => tray.Click -= onClick;
        }, tray ?? (object)"no-tray");

        return VStack(
            Heading("SqPackFs"),

            TextBlock(fsService.LastException?.ToString() ?? string.Empty)
                .Foreground(BrushHelper.Parse("red"))
                .IsVisible(fsService.LastException != null),

            TextBox(fsService.GamePath, (path) => fsService.GamePath = path, string.Empty, "Path to sqpack directory")
                .AutomationName("GamePath")
        ).Padding(20);
    }
}
