using Fsp;
using Lumina;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SqPackFs;

public partial class SqPackFileSystem : FileSystemBase, IDisposable, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SqPackFileSystem()
    {
        GamePath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack"; // TODO: auto-detect
    }

    public void Dispose()
    {
        GameData?.Dispose();
        PathList.Dispose();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public Exception? LastException
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public GameData? GameData
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public PathList PathList { get;} = new PathList();

    public string GamePath
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged();

            if (string.IsNullOrWhiteSpace(value))
            {
                LastException = new Exception("Path to sqpack directory not provided.");
                return;
            }

            if (!Directory.Exists(value))
            {
                LastException = new Exception("Path to sqpack directory does not exist.");
                return;
            }

            try
            {
                GameData?.Dispose();
                GameData = null;
                GC.Collect();
                GameData = new GameData(value);
                PathList.SetGameData(GameData);
            }
            catch (Exception ex)
            {
                LastException = ex;
            }
        }
    } = string.Empty;
}
