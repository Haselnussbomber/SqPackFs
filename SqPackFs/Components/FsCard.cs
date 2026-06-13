using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using SqPackFs.Services;
using static Microsoft.UI.Reactor.Factories;

namespace SqPackFs.Components;

public class FsCard : Component
{
    public override Element Render()
    {
        var gameDataProvider = UseObservable(UseMemo(ServiceLocator.GetService<GameDataProvider>));
        var pathlist = UseObservable(UseMemo(ServiceLocator.GetService<PathList>));
        var fsService = UseObservable(UseMemo(ServiceLocator.GetService<FileSystemService>));

        var mountFs = UseCallback(fsService.Mount);
        var unmountFs = UseCallback(fsService.Unmount);

        return Card(
            VStack(
                SubHeading("Filesystem")
                    .Margin(bottom: 12),
                HStack(
                    ComboBox(fsService.DriveLetters, fsService.DriveLetters.Length - 1, (index) => { fsService.DriveLetter = fsService.DriveLetters[index]; })
                        .IsEnabled(!fsService.IsMounted),
                    Button("Mount", mountFs)
                        .IsVisible(!fsService.IsMounted)
                        .IsEnabled(gameDataProvider.GameData != null && pathlist.Status is PathListStatus.Loaded),
                    Button("Unmount", unmountFs)
                        .IsVisible(fsService.IsMounted)
                        .IsEnabled(gameDataProvider.GameData != null && pathlist.Status is PathListStatus.Loaded)
                )
            )
        )
        .Margin(bottom: 16);
    }
}
