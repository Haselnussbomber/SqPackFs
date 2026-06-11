using Lumina;

namespace SqPackFs.Services;

[RegisterSingleton, AutoConstruct]
public partial class GameDataProvider
{
    [Notify(Setter.Private)] private Exception? _lastException;
    [Notify(Setter.Private)] private GameData? _gameData;
    [Notify(Setter.Private)] private string _gamePath;

    [AutoPostConstruct]
    private void Initialize()
    {
        GamePath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack"; // TODO: auto-detect
    }

    public void Dispose()
    {
        GameData?.Dispose();
    }

    public void SetGamePath(string path)
    {
        if (_gamePath == path)
            return;

        if (string.IsNullOrWhiteSpace(path))
        {
            LastException = new Exception("Path to sqpack directory not provided.");
            return;
        }

        if (!Directory.Exists(path))
        {
            LastException = new Exception("Path to sqpack directory does not exist.");
            return;
        }

        try
        {
            GameData?.Dispose();
            GameData = null;
            GC.Collect();
            GameData = new GameData(path);
        }
        catch (Exception ex)
        {
            LastException = ex;
        }
    }
}
