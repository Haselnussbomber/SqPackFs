using Fsp;
using Lumina;

namespace SqPackFs;

public partial class SqPackFileSystem : FileSystemBase, IDisposable
{
    public SqPackFileSystem()
    {
        GamePath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack"; // TODO: auto-detect
    }

    public Exception? LastException { get; private set; }

    public GameData? GameData { get; private set; }
    public PathList PathList { get;} = new PathList();

    public string GamePath
    {
        get;
        set
        {
            field = value;

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

    public void Dispose()
    {
        GameData?.Dispose();
        PathList.Dispose();
    }
}
