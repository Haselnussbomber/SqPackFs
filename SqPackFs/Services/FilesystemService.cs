using Fsp;

namespace SqPackFs;

[RegisterSingleton, AutoConstruct]
public partial class FileSystemService : IDisposable
{
    private readonly ILogger<FileSystemService> _logger;
    private readonly SqPackFileSystem _filesystem;

    private FileSystemHost _host;
    private Task? _task;

    [AutoPostConstruct]
    private void Initialize()
    {
        _host = new FileSystemHost(_filesystem)
        {
            Prefix = @"\ffxiv\sqpack",
            FileSystemName = "SqPackFs",
        };
    }

    public void Mount()
    {
        if (_task != null)
            Unmount();

        _task = Task.Run(() =>
        {
            _logger.LogInformation("Mounting X:");

            if (_host.Mount("X:", null, false, 0) != FileSystemBase.STATUS_SUCCESS)
            {
                throw new Exception("Unable to mount to X:");
            }

            _task = null;
        });
    }

    public void Unmount()
    {
        _logger.LogInformation("Unmounting X:");
        _host.Unmount();
    }

    public void Dispose()
    {
        Unmount();
        _host.Dispose();
    }
}
