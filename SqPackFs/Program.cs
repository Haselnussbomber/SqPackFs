using Microsoft.UI.Reactor;
using SqPackFs;
using SqPackFs.Services;
using SqPackFs.Utils;
using SqPackFs.Windows;
using static Microsoft.UI.Reactor.Factories;

ServiceLocator.GetService<FileSystemService>();

ReactorApp.Run(ctx =>
{
    // TODO: only works when you open the tray flyout once?
    ReactorApp.ShutdownPolicy = ShutdownPolicy.Explicit;

    var icon = WindowIcon.FromPath("Assets/TrayIcon.ico");

    var tray = ReactorApp.OpenTrayIcon(new TrayIconSpec(
        Key: "main",
        Icon: icon,
        Tooltip: "SqPackFS"));

    var toggleMainWindow = new Throttler(TimeSpan.FromMilliseconds(100), () =>
    {
        if (ReactorApp.FindWindow("main") is { } existing)
        {
            if (existing.IsVisible)
                existing.Hide();
            else
                existing.Activate();
            return;
        }

        ReactorApp.OpenWindow(
            new WindowSpec()
            {
                Key = "main",
                Title = "SqPackFS",
                Icon = icon,
                Width = 900,
                Height = 600,
                PersistenceId = "main",
                PersistPlacement = true,
            },
            () => new MainWindow());
    });

    // TODO: somehow is called twice
    tray.Click += (_, _) => toggleMainWindow.Invoke();

    // TODO: why is this so big?
    tray.RightClick += (_, _) => tray.ShowFlyout(
        VStack(
            Button("Open", () => { toggleMainWindow.Invoke(); tray.HideFlyout(); }),
            Button("Exit", () => ReactorApp.Exit())));

    // TODO: add cli option to not show on startup
    toggleMainWindow.Invoke();
});

ServiceLocator.Dispose();
