using Fsp;

namespace SqPackFs;

[RegisterSingleton, AutoConstruct]
public partial class FileSystemService : IDisposable
{
    private readonly ILogger<FileSystemService> _logger;
    private readonly SqPackFileSystem _filesystem;

    private FileSystemHost _host;

    [AutoPostConstruct]
    private void Initialize()
    {
        _host = new FileSystemHost(_filesystem)
        {
            Prefix = @"\ffxiv\sqpack",
            FileSystemName = "SqPackFs",
        };
        
        Task.Run(() =>
        {
            _logger.LogInformation("Mounting X:");

            if (_host.Mount("X:", null, false, 0) != FileSystemBase.STATUS_SUCCESS)
            {
                throw new Exception("Unable to mount to X:");
            }
        });
    }

    public void Dispose()
    {
        _logger.LogInformation("Unmounting X:");
        _host.Unmount();
        _host.Dispose();
    }
}
