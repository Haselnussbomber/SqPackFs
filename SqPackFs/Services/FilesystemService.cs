using Fsp;

namespace SqPackFs.Services;

[RegisterSingleton, AutoConstruct]
public partial class FileSystemService : IDisposable
{
    private readonly ILogger<FileSystemService> _logger;
    private readonly SqPackFileSystem _filesystem;

    private FileSystemHost _host;
    private Task? _task;

    [Notify(Setter.Private)] private bool _isMounted;
    [Notify(Setter.Private)] private string[] _driveLetters = GetDriveLetters();

    [Notify] private string _driveLetter; // TODO: make persistent

    [AutoPostConstruct]
    private void Initialize()
    {
        _host = new FileSystemHost(_filesystem)
        {
            Prefix = @"\ffxiv\sqpack",
            FileSystemName = "SqPackFs",
        };

        DriveLetter = _driveLetters.Last();
    }

    public void Mount()
    {
        if (_task != null)
            Unmount();

        _task = Task.Run(() =>
        {
            _logger.LogInformation("Mounting {driveLetter}", _driveLetter);

            IsMounted = true;

            if (_host.Mount(_driveLetter, null, false, 0) != FileSystemBase.STATUS_SUCCESS)
            {
                IsMounted = false;
                throw new Exception("Unable to mount to " + _driveLetter);
            }

            _task = null;
        });
    }

    public void Unmount()
    {
        _logger.LogInformation("Unmounting {driveLetter}", _driveLetter);
        _host.Unmount();
        IsMounted = false;
    }

    public void Dispose()
    {
        Unmount();
        _host.Dispose();
    }

    private static string[] GetDriveLetters()
    {
        return Enumerable.Range('A', 26).Select(c => $"{(char)c}:")
            .Except(DriveInfo.GetDrives().Select(d => $"{char.ToUpper(d.Name[0])}:"))
            .ToArray();
    }
}
