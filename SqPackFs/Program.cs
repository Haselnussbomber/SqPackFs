using Microsoft.UI.Reactor;
using SqPackFs;
using SqPackFs.Windows;

ServiceLocator.GetService<FileSystemService>();

ReactorApp.ShutdownPolicy = ShutdownPolicy.OnLastSurfaceClosed;
ReactorApp.Run<MainWindow>("SqPackFs", width: 900, height: 600);

ServiceLocator.Dispose();
