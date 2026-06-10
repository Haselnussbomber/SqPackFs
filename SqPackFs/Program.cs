using Microsoft.UI.Reactor;
using SqPackFs;

ReactorApp.ShutdownPolicy = ShutdownPolicy.OnLastSurfaceClosed;
ReactorApp.Run<MainWindow>("SqPackFs", width: 900, height: 600);
