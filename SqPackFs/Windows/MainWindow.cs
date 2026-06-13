using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using SqPackFs.Components;
using SqPackFs.Services;
using static Microsoft.UI.Reactor.Factories;

namespace SqPackFs.Windows;

public class MainWindow : Component
{
    public override Element Render()
    {
        var gameDataProvider = UseObservable(UseMemo(ServiceLocator.GetService<GameDataProvider>));

        return VStack(
            Heading("SqPackFS")
                .Margin(bottom: 16),

            TextBlock(gameDataProvider.LastException?.ToString() ?? string.Empty)
                .Foreground(BrushHelper.Parse("red"))
                .IsVisible(gameDataProvider.LastException != null)
                .Margin(bottom: 16),

            Component<SettingsCard>(),
            Component<PathListCard>(),
            Component<FsCard>()
        ).Padding(20);
    }
}
