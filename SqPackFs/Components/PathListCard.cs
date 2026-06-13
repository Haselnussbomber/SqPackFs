using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using SqPackFs.Services;
using static Microsoft.UI.Reactor.Factories;

namespace SqPackFs.Components;

public class PathListCard : Component
{
    public override Element Render()
    {
        var pathlist = UseObservable(UseMemo(ServiceLocator.GetService<PathList>));

        return Card(
            VStack(
                SubHeading("Path List")
                    .Margin(bottom: 12),

                pathlist.Status switch
                {
                    PathListStatus.Loaded => TextBlock($"Number of paths loaded: {pathlist.Count}"),
                    PathListStatus.Loading => VStack(
                        (pathlist.TotalCount switch
                        {
                            0 => TextBlock("Processing path list..."),
                            _ => TextBlock($"Processing path list... {pathlist.Count} / {pathlist.TotalCount} ({pathlist.LoadProgress * 100:F0}%)")
                        })
                            .Margin(bottom: 12),
                        Progress(pathlist.LoadProgress * 100)
                            .IsVisible(pathlist.Status == PathListStatus.Loading)
                            .Margin(bottom: 12)
                    ),
                    PathListStatus.Downloading => VStack(
                        TextBlock("Downloading path list...")
                            .Margin(bottom: 12),
                        ProgressIndeterminate()
                            .IsVisible(pathlist.Status == PathListStatus.Downloading)
                            .Margin(bottom: 12)
                    ),
                    _ => TextBlock(pathlist.Status.ToString())
                },

                HStack(
                    Button("Download", () => Task.Run(() => pathlist.LoadPathList(true)).ConfigureAwait(false)),

                    Button("Load", () => Task.Run(() => pathlist.LoadPathList(false)).ConfigureAwait(false))
                        .IsVisible(pathlist.Status is not (PathListStatus.Loaded or PathListStatus.Loading or PathListStatus.Downloading) && pathlist.IsCached),

                    Button("Reload", () => Task.Run(() => pathlist.LoadPathList(false)).ConfigureAwait(false))
                        .IsVisible(pathlist.Status is PathListStatus.Loaded && pathlist.IsCached)
                )
                    .IsVisible(pathlist.Status is not (PathListStatus.Loading or PathListStatus.Downloading))
                    .Margin(top: 12)
            )
        );
    }
}
