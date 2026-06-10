using Fsp;
using Lumina;

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

    public Exception? LastException => _filesystem.LastException;

    public GameData? GameData => _filesystem.GameData;

    public PathList PathList => _filesystem.PathList;

    public string GamePath
    {
        get => _filesystem.GamePath;
        set => _filesystem.GamePath = value;
    }

    public void Dispose()
    {
        _host.Unmount();
        _host.Dispose();
        _filesystem.Dispose();
    }
}
