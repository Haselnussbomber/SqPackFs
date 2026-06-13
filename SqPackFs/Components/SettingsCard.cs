using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using SqPackFs.Services;
using Windows.Storage.Pickers;
using static Microsoft.UI.Reactor.Factories;

namespace SqPackFs.Components;

public class SettingsCard : Component
{
    public override Element Render()
    {
        var gameDataProvider = UseObservable(UseMemo(ServiceLocator.GetService<GameDataProvider>));
        var pathlist = UseObservable(UseMemo(ServiceLocator.GetService<PathList>));

        var selectDirectoryCallback = UseCallback(async () =>
        {
            var folder = await UseFolderPickerAsync(new FolderPickerOptions(PickerLocationId.ComputerFolder)); // TODO: fix warning
            if (folder == null || !Directory.Exists(folder?.Path))
                return;

            ServiceLocator.GetService<GameDataProvider>().SetGamePath(folder.Path);
        });

        return Card(
            VStack(
                SubHeading("Settings")
                    .Margin(bottom: 12),
                TextBox(gameDataProvider.GamePath, gameDataProvider.SetGamePath, string.Empty, "Path to sqpack directory")
                    .AutomationName("GamePath")
                    .IsEnabled(pathlist.Status is not (PathListStatus.Downloading or PathListStatus.Loading)),
                Button("Select sqpack directory", selectDirectoryCallback)
            )
        )
        .Margin(bottom: 16);
    }
}
