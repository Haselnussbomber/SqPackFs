using System.ComponentModel;
using System.Runtime.CompilerServices;
using Fsp;
using Lumina;

namespace SqPackFs;

public partial class FsService : IDisposable, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private readonly FileSystemHost _host;
    private readonly SqPackFileSystem _filesystem;

    public Exception? LastException => _filesystem.LastException;

    public GameData? GameData => _filesystem.GameData;

    public PathList PathList => _filesystem.PathList;

    public string GamePath
    {
        get => _filesystem.GamePath;
        set => _filesystem.GamePath = value;
    }

    public FsService()
    {
        _filesystem = new();
        _filesystem.PropertyChanged += Filesystem_PropertyChanged;
        _host = new FileSystemHost(_filesystem)
        {
            Prefix = @"\ffxiv\sqpack",
            FileSystemName = "SqPackFs",
        };
    }

    private void Filesystem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
    }

    public void Dispose()
    {
        _host.Unmount();
        _host.Dispose();
        _filesystem.Dispose();
    }
}
