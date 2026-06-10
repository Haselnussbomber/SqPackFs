using Fsp;

namespace SqPackFs;

public partial class FsService : IDisposable
{
    private readonly FileSystemHost _host;
    private readonly SqPackFileSystem _filesystem;

    public FsService()
    {
        _filesystem = new();
        _host = new FileSystemHost(_filesystem)
        {
            Prefix = @"\ffxiv\sqpack",
            FileSystemName = "SqPackFs",
        };
    }

    public string GamePath
    {
        get => _filesystem.GamePath;
        set => _filesystem.GamePath = value;
    }

    public Exception? LastException => _filesystem.LastException;

    public void Dispose()
    {
        _host.Unmount();
        _host.Dispose();
        _filesystem.Dispose();
    }
}
