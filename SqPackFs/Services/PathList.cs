using System.Diagnostics;
using System.IO.Compression;
using Lumina.Misc;
using SqPackFs.Models;
using SqPackFs.Utils;

namespace SqPackFs.Services;

[RegisterSingleton, AutoConstruct]
public partial class PathList : IDisposable
{
    public static string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqPackFs");
    public static string PathListCachePath => Path.Combine(AppDataPath, "PathListWithHashes.gz");

    private readonly ILogger<PathList> _logger;

    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<SqHash, string> _files = [];
    private readonly Lock _processLock = new();

    [Notify(Setter.Private)] private PathListStatus _status;
    [Notify(Setter.Private)] private int _count;
    [Notify(Setter.Private)] private int _totalCount;
    [Notify(Setter.Private)] private double _loadProgress;

    private ILookup<SqFolderHash, string>? _folderFilePathIndex;

    [AutoPostConstruct]
    private void Initialize()
    {
        Task.Run(() => LoadPathList(!File.Exists(PathListCachePath))).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Clear();
    }

    public async Task LoadPathList(bool download = false)
    {
        try
        {
            if (download)
            {
                await DownloadPathList();
            }

            using var lockScope = _processLock.EnterScope();

            Clear();

            _logger.LogInformation("Processing path list");

            var stopwatch = Stopwatch.StartNew();

            Status = PathListStatus.Loading;
            LoadCachedPathList();
            Status = PathListStatus.Loaded;

            _logger.LogInformation("Loaded path lists in {elapsed}", stopwatch.Elapsed);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process path lists");
            Status = PathListStatus.Error;
        }
    }

    public IEnumerable<string> GetFilesInFolder(string path)
    {
        SqFolderHash folderHash = Crc32.Get(path.ToLower());
        return _folderFilePathIndex?[folderHash] ?? [];
    }

    private void Clear()
    {
        _files.Clear();
        _folderFilePathIndex = null;
        Count = 0;
        LoadProgress = 0;
        Status = PathListStatus.NotLoaded;
    }

    private async Task DownloadPathList()
    {
        Status = PathListStatus.Downloading;

        var url = "https://rl2.perchbird.dev/download/export/PathListWithHashes.gz";

        await using var req = await _httpClient.GetStreamAsync(url);
        using var reader = new StreamReader(req);

        if (!Directory.Exists(AppDataPath))
            Directory.CreateDirectory(AppDataPath);

        if (File.Exists(PathListCachePath))
            File.Delete(PathListCachePath);

        await using var writer = new StreamWriter(PathListCachePath);
        await reader.BaseStream.CopyToAsync(writer.BaseStream);

        _logger.LogInformation("Downloaded paths to {path}", PathListCachePath);

        Status = PathListStatus.Downloaded;
    }

    private void LoadCachedPathList()
    {
        using var stream = File.OpenRead(PathListCachePath);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new Utf8CsvReader(gzip);

        var totalBytes = stream.Length;
        var linesRead = 0;

        Count = 0;
        TotalCount = FileUtils.CountLines(PathListCachePath);

        _files.EnsureCapacity(_totalCount);

        reader.ReadNextRow(); // ship header

        while (reader.ReadNextRow())
        {
            var row = reader.GetRowReader();

            if (!row.Skip()) // indexid
                continue;

            if (!row.TryRead(out uint folderhash))
                continue;

            if (!row.TryRead(out uint filehash))
                continue;

            if (!row.TryRead(out uint fullhash))
                continue;

            if (!row.TryRead(out var path))
                continue;

            var hash = new SqHash()
            {
                Full = fullhash,
                Folder = folderhash,
                File = filehash,
            };

            _files[hash] = path;

            if (++linesRead % 10000 == 0 && totalBytes > 0)
            {
                Count = linesRead;
                LoadProgress = (double)stream.Position / totalBytes;
            }
        }

        _folderFilePathIndex = _files.ToLookup(kvp => kvp.Key.Folder, kvp => kvp.Value);

        Count = linesRead;

        LoadProgress = 1.0;
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
