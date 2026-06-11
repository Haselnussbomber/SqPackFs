using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using SqPackFs.Services;
using static Microsoft.UI.Reactor.Factories;

namespace SqPackFs.Components;

public class SettingsCard : Component
{
    public override Element Render()
    {
        var gameDataProvider = UseObservable(UseMemo(ServiceLocator.GetService<GameDataProvider>));
        var pathlist = UseObservable(UseMemo(ServiceLocator.GetService<PathList>));

        return Card(
            VStack(
                SubHeading("Settings")
                    .Margin(bottom: 12),
                TextBox(gameDataProvider.GamePath, gameDataProvider.SetGamePath, string.Empty, "Path to sqpack directory")
                    .AutomationName("GamePath")
                    .IsEnabled(pathlist.Status is not (PathListStatus.Downloading or PathListStatus.Loading))
            )
        )
        .Margin(bottom: 16);
    }
}
