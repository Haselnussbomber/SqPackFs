using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lumina;
using Lumina.Data;
using Lumina.Misc;

namespace SqPackFs;

public partial class PathList : IDisposable, INotifyPropertyChanged
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
    private readonly Dictionary<SqDat, Dictionary<SqHash, SqFile>> _files = [];
    private readonly Dictionary<string, SqFolder> _folders = [];
    private readonly Dictionary<SqDat, Dictionary<uint, string>> _folderNames = [];
    private readonly Lock _processLock = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private SqFolder _rootDirectory = new("root", 0);
    private GameData? _gameData;

    public PathListStatus Status
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

    public uint Count
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

    public uint TotalCount
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

    public double LoadProgress
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

    public PathList()
    {
        _httpClient = new HttpClient();
    }

    public void Dispose()
    {
        Clear();
    }

    public void SetGameData(GameData gameData)
    {
        _gameData = gameData;
        Task.Run(() => LoadPathList(!File.Exists(PathListCachePath))).ConfigureAwait(false);
    }

    public async Task LoadPathList(bool download = false)
    {
        if (download)
        {
            await DownloadPathList();
        }

        try
        {
            using (_processLock.EnterScope())
            {
                Clear();
                Debug.WriteLine("Processing path list");

                var stopwatch = Stopwatch.StartNew();

                Status = PathListStatus.Loading;

                LoadCachedPathList();
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

        foreach (var repo in _gameData.Repositories.Values)
        {
            foreach (var cat in repo.Categories.Values.SelectMany(c => c))
            {
                if (cat.IndexHashTableEntries is null)
                    continue;

                if (cat.CategoryId != file.Dat.Category.Id || cat.Expansion != file.Dat.Category.Expansion)
                    continue;

                return cat.GetFile<T>(file.Hash.Full);
            }
        }

        return null;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void Clear()
    {
        _rootDirectory = new SqFolder("root", 0);
        _files.Clear();
        _folderNames.Clear();
        Count = 0;
        LoadProgress = 0;
    }

    private async Task DownloadPathList()
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

    private void LoadCachedPathList()
    {
        using var stream = File.OpenRead(PathListCachePath);
        using var reader = new Utf8CsvReader(stream);

        Count = 0;
        var totalBytes = stream.Length;
        var linesRead = 0;
        var totalLines = 0u;

        while (reader.ReadNextRow())
            totalLines++;

        TotalCount = totalLines;

        reader.Reset();

        while (reader.ReadNextRow())
        {
            var row = reader.GetRowReader();
            // indexid,folderhash,filehash,fullhash,path

            if (!row.TryRead(out uint indexId))
                continue;

            if (!row.Skip())
                continue;

            if (!row.Skip())
                continue;

            if (!row.TryRead(out ulong fullhash))
                continue;

            if (!row.TryRead(out var path))
                continue;

            var category = new SqCategory((byte)(indexId >> 16), (byte)((indexId >> 8) & 0xFF));
            var dat = new SqDat(category, (byte)(indexId & 0xFF));
            var file = new SqFile(dat, fullhash, string.Intern(path));
            var folder = GetFolder(file.FolderName!, true)!;

            if (!_folderNames.TryGetValue(dat, out var names))
                _folderNames[dat] = names = [];

            names.TryAdd(file.Hash.Folder, file.FolderName!);

            folder.Files[file.Hash.File] = file;

            if (!_files.TryGetValue(file.Dat!, out var files))
                _files[file.Dat!] = files = [];

            files[file.Hash.Full] = file;
            Count++;

            linesRead++;
            if (linesRead % 10000 == 0 && totalBytes > 0)
            {
                LoadProgress = (double)stream.Position / totalBytes;
            }
        }

        GC.Collect();
        LoadProgress = 1.0;
    }

    private void LoadGameFiles()
    {
        foreach (var repo in _gameData!.Repositories.Values)
        {
            foreach (var cat in repo.Categories.Values.SelectMany(c => c))
            {
                if (cat.IndexHashTableEntries is null) continue;

                var category = new SqCategory(cat.CategoryId, (byte)cat.Expansion);
                var dats = new Dictionary<byte, SqDat>();

                foreach (var (hash, data) in cat.IndexHashTableEntries)
                {
                    if (!dats.TryGetValue(data.DataFileId, out var dat))
                        dats[data.DataFileId] = dat = new SqDat(category, data.DataFileId);

                    var file = new SqFile(dat, hash);

                    if (!_files.TryGetValue(file.Dat!, out var files))
                        _files[file.Dat!] = files = [];

                    if (files.ContainsKey(file.Hash.Full))
                        continue;

                    if (!ReverseRootCategories.TryGetValue(cat.CategoryId, out var rootCategory))
                        continue;

                    var folderName =
                        _folderNames.TryGetValue(dat, out var names) && names.TryGetValue(file.Hash.Folder, out var newName)
                        ? newName
                        : rootCategory + "/" + (HasSubfolder.Contains(rootCategory) ? cat.Expansion == 0 ? "ffxiv/" : "ex" + cat.Expansion + "/" : "") + Utils.PrintFileHash(file.Hash.Folder);

                    var folder = GetFolder(folderName, true)!;
                    folder.Files[file.Hash.File] = file;
                    files[file.Hash.Full] = file;
                }
            }
        }
    }
}

public record struct SqCategory(byte Id, byte Expansion);
public record struct SqDat(SqCategory Category, byte Index);

public record SqFolder(string Name, uint FolderHash)
{
    public Dictionary<uint, SqFolder> Folders = [];
    public Dictionary<uint, SqFile> Files = [];
}

[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct SqHash : IEquatable<SqHash>, IComparable<SqHash>
{
    [FieldOffset(0x00)] public ulong Full;
    [FieldOffset(0x00)] public uint Folder;
    [FieldOffset(0x04)] public uint File;

    public bool Equals(SqHash other)
        => Full == other.Full;

    public override bool Equals(object? obj)
    {
        if (obj is ulong ulongHash)
            return Equals((SqHash)ulongHash);

        if (obj is SqHash fullHash)
            return Equals(fullHash);

        return false;
    }

    public int CompareTo(SqHash other)
        => Full.CompareTo(other.Full);

    public override int GetHashCode()
        => Full.GetHashCode();

    public static bool operator ==(SqHash left, SqHash right)
        => left.Equals(right);

    public static bool operator !=(SqHash left, SqHash right)
        => !(left == right);

    public static bool operator <(SqHash left, SqHash right)
        => left.CompareTo(right) < 0;

    public static bool operator <=(SqHash left, SqHash right)
        => left.CompareTo(right) <= 0;

    public static bool operator >(SqHash left, SqHash right)
        => left.CompareTo(right) > 0;

    public static bool operator >=(SqHash left, SqHash right)
        => left.CompareTo(right) >= 0;

    public static implicit operator SqHash(ulong value)
        => new() { Full = value };
}

public record SqFile
{
    public SqDat Dat { get; }
    public SqHash Hash { get; }
    public string? FolderName { get; }
    public string? FileName { get; }
    public string? Path { get; }

    public SqFile(SqDat dat, SqHash hash)
    {
        Dat = dat;
        Hash = hash;
    }

    public SqFile(SqDat dat, SqHash hash, string path) : this(dat, hash)
    {
        Path = path;
        FolderName = string.Intern(path[..path.LastIndexOf('/')]);
        FileName = string.Intern(path[(path.LastIndexOf('/') + 1)..]);
    }

    public SqFile(SqDat dat, string path) : this(dat, Utils.GetFullHash(path))
    {
        Path = path;
        FolderName = string.Intern(path[..path.LastIndexOf('/')]);
        FileName = string.Intern(path[(path.LastIndexOf('/') + 1)..]);
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
