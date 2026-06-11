using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using ResLogger2.Common;
using SqPackFs.Models;
using SqPackFs.Utils;

namespace SqPackFs.Services;

[RegisterSingleton, AutoConstruct]
public partial class PathList : IDisposable
{
    public static string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqPackFs");
    public static string PathListCachePath => Path.Combine(AppDataPath, "CurrentPathListWithHashes.gz");

    private readonly ILogger<PathList> _logger;
    private readonly GameDataProvider _gameDataProvider;

    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, SqNode> _paths = [];
    private readonly Dictionary<SqHash, SqNode> _nodes = [];
    private readonly Dictionary<SqFolderHash, HashSet<SqNode>> _folderContents = [];
    private readonly Lock _processLock = new();

    [Notify(Setter.Private)] private PathListStatus _status;
    [Notify(Setter.Private)] private int _count;
    [Notify(Setter.Private)] private int _totalCount;
    [Notify(Setter.Private)] private double _loadProgress;
    [Notify(Setter.Private)] private ulong _totalSize;

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
        ServiceLocator.GetService<FileSystemService>().Unmount();

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

            ServiceLocator.GetService<FileSystemService>().Mount();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process path lists");
            Status = PathListStatus.Error;
        }
    }

    public IEnumerable<SqNode> GetNodesInFolder(SqFolderHash folderHash)
    {
        _processLock.Enter();

        try
        {
            if (!_folderContents.TryGetValue(folderHash, out var nodes))
                return [];

            return [.. nodes];
        }
        finally
        {
            _processLock.Exit();
        }
    }

    public bool TryGetNodeByPath(string path, [NotNullWhen(returnValue: true)] out SqNode? node)
    {
        return _paths.TryGetValue(path, out node);
    }

    public bool TryGetNodeByHash(SqHash hash, [NotNullWhen(returnValue: true)] out SqNode? node)
    {
        return _nodes.TryGetValue(hash, out node);
    }

    public bool TryGetNodeByFolderHash(SqFolderHash hash, [NotNullWhen(returnValue: true)] out SqNode? node)
    {
        foreach (var folderNode in GetNodesInFolder(hash))
        {
            if (folderNode.IsDirectory)
            {
                node = folderNode;
                return true;
            }
        }

        node = null;
        return false;
    }

    private void Clear()
    {
        _nodes.Clear();
        _paths.Clear();
        _folderContents.Clear();
        Count = 0;
        LoadProgress = 0;
        Status = PathListStatus.NotLoaded;
    }

    private async Task DownloadPathList()
    {
        Status = PathListStatus.Downloading;

        if (!Directory.Exists(AppDataPath))
            Directory.CreateDirectory(AppDataPath);

        if (File.Exists(PathListCachePath))
            File.Delete(PathListCachePath);

        await using var req = await _httpClient.GetStreamAsync("https://rl2.perchbird.dev/download/export/PathListWithHashes.gz");
        using var reader = new StreamReader(req);

        await using var writer = new StreamWriter(PathListCachePath);
        await reader.BaseStream.CopyToAsync(writer.BaseStream);

        _logger.LogInformation("Downloaded paths to {path}", PathListCachePath);

        Status = PathListStatus.Downloaded;
    }

    private void LoadCachedPathList()
    {
        if (_gameDataProvider.GameData == null)
            throw new NullReferenceException("GameData is not set");

        using var stream = File.OpenRead(PathListCachePath);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new Utf8CsvReader(gzip);

        var totalBytes = stream.Length;
        var linesRead = 0;

        Count = 0;
        TotalCount = FileUtils.CountLines(PathListCachePath);

        _nodes.EnsureCapacity(_totalCount);
        _paths.EnsureCapacity(_totalCount);
        // add root node
        var rootHash = new SqHash("", true);
        var rootNode = new SqNode("", rootHash);
        _nodes.TryAdd(rootHash, rootNode);
        _paths.TryAdd("", rootNode);
        _folderContents[rootHash.Folder] = []; // Initialize root directory

        reader.ReadNextRow(); // skip header

        while (reader.ReadNextRow())
        {
            var row = reader.GetRowReader();

            // skip indexid
            if (!row.Skip() || !row.TryRead(out uint folderhash) || !row.TryRead(out uint filehash) || !row.TryRead(out uint fullhash) || !row.TryRead(out var path))
            {
                TotalCount--;
                continue;
            }

            if (!_gameDataProvider.GameData.FileExists(path))
            {
                TotalCount--;
                continue;
            }

            // add file node
            var hash = new SqHash()
            {
                Full = fullhash,
                Folder = folderhash,
                File = filehash,
            };

            var node = new SqNode(path, hash);

            _nodes[hash] = node;
            _paths[path] = node;

            // 1. Link this file directly to its parent folder
            if (!_folderContents.TryGetValue(folderhash, out var children))
            {
                children = [];
                _folderContents[folderhash] = children;
            }
            children.Add(node);

            // 2. Walk up the path to ensure all parent folders exist AND are linked
            var pathSpan = path.AsSpan();
            if (pathSpan.EndsWith("/"))
                pathSpan = pathSpan[..^1];

            while (true)
            {
                var lastSlash = pathSpan.LastIndexOf('/');
                if (lastSlash <= 0)
                    break;

                pathSpan = pathSpan[..lastSlash];
                var parentPath = pathSpan.ToString();
                var parentHash = new SqHash(parentPath, true);

                // If this folder already exists, its ancestors are already linked. We can stop.
                if (_nodes.ContainsKey(parentHash))
                    break;

                // Create the missing folder node
                var parentNode = new SqNode(parentPath, parentHash);
                _nodes[parentHash] = parentNode;
                _paths[parentPath] = parentNode;

                // 3. Link this NEW folder to ITS parent (the grandparent)
                var grandParentSlash = pathSpan.LastIndexOf('/');
                uint grandParentHash = grandParentSlash <= 0
                    ? rootHash.Folder
                    : new SqHash(pathSpan[..grandParentSlash], true).Folder;

                if (!_folderContents.TryGetValue(grandParentHash, out var gpChildren))
                {
                    gpChildren = [];
                    _folderContents[grandParentHash] = gpChildren;
                }
                gpChildren.Add(parentNode);
            }

            if (++linesRead % 10000 == 0 && totalBytes > 0)
            {
                Count = linesRead;
                LoadProgress = (double)stream.Position / totalBytes;
            }
        }

        Count = linesRead;
        LoadProgress = 1.0;

        // add files without path
        foreach (var index in PatchIndexHolder.LoadAllIndexData(_gameDataProvider.GamePath))
        {
            foreach (var indexEntry in index.CombinedIndexEntries.Values)
            {
                var hash = new SqHash
                {
                    File = indexEntry.FileHash,
                    Folder = indexEntry.FolderHash,
                    Full = indexEntry.FullHash
                };

                _nodes.TryAdd(hash, new SqNode($"~{indexEntry.FullHash:X8}", hash));

                // TODO: can't access files without path just yet...
                // _paths[path] = node;
            }
        }

        GC.Collect();
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
