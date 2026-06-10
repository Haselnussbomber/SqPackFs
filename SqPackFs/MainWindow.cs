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

            TextBlock(fsService.LastException?.ToString() ?? string.Empty) // TODO: doesn't update. don't know how to make it to do so.
                .Foreground(BrushHelper.Parse("red"))
                .IsVisible(fsService.LastException != null),

            // TODO: maybe add boxes around these.. Settings group, Path List group

            TextBox(fsService.GamePath, (path) => fsService.GamePath = path, string.Empty, "Path to sqpack directory")
                .AutomationName("GamePath"),

            SubHeading("Path List"),
            TextBlock($"Number of paths loaded: {fsService.PathList.Count}"), // TODO: doesn't update. don't know how to make it to do so.
            Button("Download", () => fsService.PathList.DownloadPathList().ConfigureAwait(false))
                .IsEnabled(fsService.PathList.Status is not (PathListStatus.Loading or PathListStatus.Downloading)) // TODO: show progress bar (immediate for download, since size is unknown, then count for processing lines)
        ).Padding(20);
    }
}
