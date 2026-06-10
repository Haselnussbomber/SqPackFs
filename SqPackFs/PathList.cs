using System.Collections.Frozen;
using System.Diagnostics;
using System.IO.Compression;
using Lumina;
using Lumina.Data;
using Lumina.Misc;

namespace SqPackFs;

public partial class PathList : IDisposable
{
    public static readonly Dictionary<string, byte> RootCategories = new() {
        {"common", 0},
        {"bgcommon", 1},
        {"bg", 2},
        {"cut", 3},
        {"chara", 4},
        {"shader", 5},
        {"ui", 6},
        {"sound", 7},
        {"vfx", 8},
        {"ui_script", 9},
        {"exd", 0xA},
        {"game_script", 0xB},
        {"music", 0xC}
    };

    public static readonly FrozenDictionary<byte, string> ReverseRootCategories = RootCategories.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);
    // Has a subfolder for version, so `cut/ffxiv` and `cut/ex1` and whatnot
    public static readonly string[] HasSubfolder = ["bg", "cut", "music"];

    public static string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqPackFs");
    public static string PathListCachePath => Path.Combine(AppDataPath, "reslogger.csv");

    private readonly HttpClient _httpClient;
    private readonly Dictionary<Dat, Dictionary<ulong, SqFile>> _files = [];
    private readonly Dictionary<string, SqFolder> _folders = [];
    private readonly Dictionary<Dat, Dictionary<uint, string>> _folderNames = [];
    private readonly Lock _processLock = new();

    private SqFolder _rootDirectory = new("root", 0);
    private GameData? _gameData;

    public PathListStatus Status { get; private set; }
    public uint Count { get; private set; }

    public PathList()
    {
        _httpClient = new HttpClient();
    }

    public void Dispose()
    {
        Clear();
    }

    public async Task DownloadPathList()
    {
        Status = PathListStatus.Downloading;

        var url = "https://rl2.perchbird.dev/download/export/PathListWithHashes.gz";

        await using var req = await _httpClient.GetStreamAsync(url);
        await using var gzip = new GZipStream(req, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        if (!Directory.Exists(AppDataPath))
            Directory.CreateDirectory(AppDataPath);

        if (File.Exists(PathListCachePath))
            File.Delete(PathListCachePath);

        var i = 0;
        await using var writer = new StreamWriter(PathListCachePath);
        reader.ReadLine(); // skip header

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
                break;

            line = line.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            writer.WriteLine(line);
            i++;
        }

        Debug.WriteLine($"Downloaded {i} paths to {PathListCachePath}");

        Status = PathListStatus.Downloaded;
    }

    public void SetGameData(GameData gameData)
    {
        _gameData = gameData;
        Status = PathListStatus.Loading;

        Task.Run(() =>
        {
            try
            {
                using (_processLock.EnterScope())
                {
                    Clear();
                    Debug.WriteLine("Processing path list");

                    var stopwatch = Stopwatch.StartNew();

                    LoadPathList();
                    Debug.WriteLine($"Loaded path lists: {stopwatch.Elapsed}");

                    LoadGameFiles();
                    Debug.WriteLine($"Sorting folders: {stopwatch.Elapsed}");

                    Debug.WriteLine($"Finished processing path lists in {stopwatch.Elapsed}");

                    Status = PathListStatus.Loaded;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to process path lists: {e}");
                Status = PathListStatus.Error;
            }
        });
    }

    public void Clear()
    {
        _rootDirectory = new SqFolder("root", 0);
        _files.Clear();
        _folderNames.Clear();
    }

    private void LoadPathList()
    {
        using var reader = new StreamReader(PathListCachePath);

        Count = 0;

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine()!.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var firstComma = line.IndexOf(',');
            var lastComma = line.LastIndexOf(',');
            var indexId = int.Parse(line[..firstComma]);
            var path = line[(lastComma + 1)..];

            var category = new Category((byte)(indexId >> 16), (byte)((indexId >> 8) & 0xFF));
            var dat = new Dat(category, (byte)(indexId & 0xFF));
            var file = new SqFile(path, dat);
            var folder = GetFolder(file.FolderName!, true)!;

            if (!_folderNames.TryGetValue(dat, out var names))
                _folderNames[dat] = names = [];

            names.TryAdd(file.FolderHash, file.FolderName!);

            folder.Files[file.FileHash] = file;

            if (!_files.TryGetValue(file.Dat!, out var files))
                _files[file.Dat!] = files = [];

            files[file.Hash] = file;
            Count++;
        }
    }

    private void LoadGameFiles()
    {
        foreach (var repo in _gameData!.Repositories.Values)
        {
            foreach (var cat in repo.Categories.Values.SelectMany(c => c))
            {
                if (cat.IndexHashTableEntries is null) continue;

                var category = new Category(cat.CategoryId, (byte)cat.Expansion);
                var dats = new Dictionary<byte, Dat>();

                foreach (var (hash, data) in cat.IndexHashTableEntries)
                {
                    if (!dats.TryGetValue(data.DataFileId, out var dat))
                        dats[data.DataFileId] = dat = new Dat(category, data.DataFileId);

                    var file = new SqFile(dat, hash);

                    if (!_files.TryGetValue(file.Dat!, out var files))
                        _files[file.Dat!] = files = [];

                    if (files.ContainsKey(file.Hash))
                        continue;

                    if (!ReverseRootCategories.TryGetValue(cat.CategoryId, out var rootCategory))
                        continue;

                    var folderName =
                        _folderNames.TryGetValue(dat, out var names) && names.TryGetValue(file.FolderHash, out var newName)
                        ? newName
                        : rootCategory + "/" + (HasSubfolder.Contains(rootCategory) ? cat.Expansion == 0 ? "ffxiv/" : "ex" + cat.Expansion + "/" : "") + Utils.PrintFileHash(file.FolderHash);

                    var folder = GetFolder(folderName, true)!;
                    folder.Files[file.FileHash] = file;
                    files[file.Hash] = file;
                }
            }
        }
    }

    public SqFolder? GetFolder(string path, bool mkdir = false)
    {
        if (_folders.TryGetValue(path, out var existing))
            return existing;

        var folders = path.Split('/');
        var current = _rootDirectory;

        while (folders.Length > 0)
        {
            var folder = folders[0];
            folders = folders[1..];

            var folderHash = Crc32.Get(folder.ToLower());
            if (current.Folders.TryGetValue(folderHash, out var next))
            {
                current = next;
                continue;
            }

            if (!mkdir)
                return null;

            next = new SqFolder(folder, folderHash);
            current.Folders[folderHash] = next;
            current = next;
        }

        return _folders[path] = current;
    }

    public IEnumerable<(string, SqFile)> GetAllFiles(SqFolder folder, string path = "")
    {
        foreach (var file in folder.Files)
            yield return (path, file.Value);

        foreach (var subFolder in folder.Folders)
        {
            var newPath = string.IsNullOrEmpty(path) ? subFolder.Value.Name : path + "/" + subFolder.Value.Name;

            foreach (var file in GetAllFiles(subFolder.Value, newPath))
                yield return file;
        }
    }

    public FileResource? GetFile(SqFile file)
        => GetFile<FileResource>(file);

    public T? GetFile<T>(SqFile file) where T : FileResource
    {
        if (_gameData == null)
            return null;

        if (file.Path != null)
            return _gameData.GetFile<T>(file.Path);

        if (file.Dat == null)
            return null;

        foreach (var repo in _gameData.Repositories.Values)
        {
            foreach (var cat in repo.Categories.Values.SelectMany(c => c))
            {
                if (cat.IndexHashTableEntries is null)
                    continue;

                if (cat.CategoryId != file.Dat.Category.Id || cat.Expansion != file.Dat.Category.Expansion)
                    continue;

                return cat.GetFile<T>(file.Hash);
            }
        }

        return null;
    }

    public record Category(byte Id, byte Expansion);
    public record Dat(Category Category, byte Index);

    public record SqFolder(string Name, uint FolderHash)
    {
        public Dictionary<uint, SqFolder> Folders = [];
        public Dictionary<uint, SqFile> Files = [];
    }

    public record SqFile(Dat? Dat, ulong Hash, string? FolderName = null, string? FileName = null)
    {
        public string? Path => FolderName != null && FileName != null ? (FolderName + "/" + FileName) : null;
        public uint FolderHash = (uint)(Hash >> 32);
        public uint FileHash = (uint)Hash;

        public SqFile(string path, Dat? dat = null) : this(dat, Utils.GetFullHash(path))
        {
            FolderName = path[..path.LastIndexOf('/')];
            FileName = path[(path.LastIndexOf('/') + 1)..];
        }
    }
}

public enum PathListStatus
{
    NotLoaded,
    Loading,
    Loaded,
    Downloading,
    Downloaded,
    Error,
}
