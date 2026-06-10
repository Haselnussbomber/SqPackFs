using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace SqPackFs;

public class MainWindow : Component
{
    public override Element Render()
    {
        var fsService = UseMemo(() => new FsService());

        UseEffect(() =>
        {
            return () => fsService.Dispose();
        }, fsService);

        UseObservable(fsService);
        UseObservable(fsService.PathList);

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
            Heading("SqPackFs")
                .Margin(bottom: 16),

            TextBlock(fsService.LastException?.ToString() ?? string.Empty)
                .Foreground(BrushHelper.Parse("red"))
                .IsVisible(fsService.LastException != null)
                .Margin(bottom: 16),

            // Settings
            Card(
                VStack(
                    SubHeading("Settings")
                        .Margin(bottom: 12),
                    TextBox(fsService.GamePath, (path) => fsService.GamePath = path, string.Empty, "Path to sqpack directory")
                        .AutomationName("GamePath")
                        .IsEnabled(fsService.PathList.Status is not (PathListStatus.Downloading or PathListStatus.Loading))
                )
            )
            .Margin(bottom: 16),

            // Path List
            Card(
                VStack(
                    SubHeading("Path List")
                        .Margin(bottom: 12),
                    TextBlock(fsService.PathList.Status is not PathListStatus.Loading
                        ? $"Number of paths loaded: {fsService.PathList.Count}"
                        : $"Number of paths loaded: {fsService.PathList.Count} / {fsService.PathList.TotalCount} ({fsService.PathList.LoadProgress * 100:F0}%)")
                        .Margin(bottom: 12),

                    VStack(
                        TextBlock(fsService.PathList.Status == PathListStatus.Downloading
                            ? "Downloading path list..." 
                            : "Processing path list...")
                            .Margin(bottom: 8),
                        
                        ProgressIndeterminate()
                            .IsVisible(fsService.PathList.Status == PathListStatus.Downloading),
                            
                        Progress(fsService.PathList.LoadProgress * 100)
                            .IsVisible(fsService.PathList.Status == PathListStatus.Loading)
                    )
                    .IsVisible(fsService.PathList.Status is PathListStatus.Downloading or PathListStatus.Loading)
                    .Margin(bottom: 12),

                    Button("Download", () => fsService.PathList.LoadPathList(true).ConfigureAwait(false))
                        .IsEnabled(fsService.PathList.Status is not (PathListStatus.Loading or PathListStatus.Downloading))
                )
            )
        ).Padding(20);
    }
}
