using Fsp;
using Lumina;

namespace SqPackFs;

public partial class SqPackFileSystem : FileSystemBase, IDisposable
{
    private GameData? _lumina;

    public SqPackFileSystem()
    {
        GamePath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack"; // TODO: auto-detect
    }

    public Exception? LastException { get; private set; }

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
                _lumina?.Dispose();
                _lumina = null;
                GC.Collect();
                _lumina = new GameData(value);
            }
            catch (Exception ex)
            {
                LastException = ex;
            }
        }
    } = string.Empty;

    public void Dispose()
    {
        _lumina?.Dispose();
    }
}
