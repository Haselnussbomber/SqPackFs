using Fsp;
using Fsp.Interop;
using Lumina.Misc;
using SqPackFs.Models;
using SqPackFs.Utils;
using FileInfo = Fsp.Interop.FileInfo;

namespace SqPackFs.Services;

[RegisterSingleton, AutoConstruct]
public partial class SqPackFileSystem : FileSystemBase
{
    private readonly ILogger<SqPackFileSystem> _logger;
    private readonly PathList _pathList;
    private readonly GameDataProvider _gameDataProvider;

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "\\")
            return string.Empty;

        return path.ToLowerInvariant().TrimStart('\\').Replace('\\', '/');
    }

    public override int GetVolumeInfo(out VolumeInfo volumeInfo)
    {
        volumeInfo = default;
        volumeInfo.TotalSize = _pathList.TotalSize;
        volumeInfo.FreeSize = 0; // readonly
        volumeInfo.SetVolumeLabel("SqPackFS");
        // if (_logger.IsEnabled(LogLevel.Trace))
        //     _logger.LogTrace("[GetVolumeInfo] STATUS_SUCCESS with TotalSize = {t}", volumeInfo.TotalSize);
        return STATUS_SUCCESS;
    }

    public override int GetSecurityByName(
        string fileName,
        out uint fileAttributes,
        ref byte[] securityDescriptor)
    {
        fileAttributes = 0;

        var path = NormalizePath(fileName);

        // find by path
        if (_pathList.TryGetNodeByPath(path, out var node))
        {
            fileAttributes = node.FileInfo.FileAttributes;
            return STATUS_SUCCESS;
        }

        // find by hash
        var fullHash = Crc32.Get(path);
        var (folderHash, fileHash) = HashUtils.GetHash(path);
        var hash = new SqHash()
        {
            Full = fullHash,
            File = fileHash,
            Folder = folderHash
        };

        if (_pathList.TryGetNodeByHash(hash, out node))
        {
            fileAttributes = node.FileInfo.FileAttributes;
            return STATUS_SUCCESS;
        }

        _logger.LogTrace("[GetSecurityByName] Not found: {path}", path);
        return STATUS_OBJECT_NAME_NOT_FOUND;
    }

    public override bool ReadDirectoryEntry(
        object fileNode,
        object fileDesc,
        string pattern,
        string marker,
        ref object context,
        out string fileName,
        out FileInfo fileInfo)
    {
        if (context is not IEnumerator<SqNode> enumerator)
        {
            var node = (SqNode)fileDesc;
            var entries = GetDirectoryEntries(node);

            if (!string.IsNullOrEmpty(marker))
                entries = [.. entries.Where(e => string.Compare(e.Name, marker, StringComparison.OrdinalIgnoreCase) > 0)];

            enumerator = entries.GetEnumerator();
            context = enumerator;
        }

        if (enumerator.MoveNext())
        {
            fileName = enumerator.Current.Name;
            fileInfo = enumerator.Current.FileInfo;
            return true;
        }

        fileName = null!;
        fileInfo = default;
        return false;
    }

    public override int GetFileInfo(
        object fileNode,
        object fileDesc,
        out FileInfo fileInfo)
    {
        var node = (SqNode)fileDesc;
        fileInfo = node.FileInfo;
        return STATUS_SUCCESS;
    }

    public override int Open(
        string fileName,
        uint createOptions,
        uint grantedAccess,
        out object fileNode,
        out object fileDesc,
        out FileInfo fileInfo,
        out string normalizedName)
    {
        var path = NormalizePath(fileName);

        if (!_pathList.TryGetNodeByPath(path, out var node))
        {
            fileNode = null!;
            fileDesc = null!;
            fileInfo = default;
            normalizedName = null!;

            // if (_logger.IsEnabled(LogLevel.Trace))
            //     _logger.LogTrace("[Open] STATUS_OBJECT_NAME_NOT_FOUND for {path}", path);

            return STATUS_OBJECT_NAME_NOT_FOUND;
        }

        fileNode = node;
        fileDesc = node;
        fileInfo = node.FileInfo;
        normalizedName = fileName;

        // if (_logger.IsEnabled(LogLevel.Trace))
        //     _logger.LogTrace("[Open] STATUS_SUCCESS for {path}", path);

        return STATUS_SUCCESS;
    }

    public override int Read(
        object fileNode,
        object fileDesc,
        nint buffer,
        ulong offset,
        uint length,
        out uint bytesTransferred)
    {
        var node = (SqNode)fileDesc;

        if (node.IsDirectory)
        {
            bytesTransferred = 0;
            return STATUS_INVALID_DEVICE_REQUEST;
        }

        node.FileResource ??= _gameDataProvider.GameData?.GetFile(node.Path);

        if (node.FileResource == null)
        {
            bytesTransferred = 0;
            return STATUS_INVALID_DEVICE_REQUEST;
        }

        if (offset >= (ulong)node.FileResource.Data.Length)
        {
            bytesTransferred = 0;
            return STATUS_END_OF_FILE;
        }

        var bytesToCopy = Math.Min(length, (uint)(node.FileResource.Data.Length - (int)offset));
        Marshal.Copy(node.FileResource.Data, (int)offset, buffer, (int)bytesToCopy);
        bytesTransferred = bytesToCopy;

        // if (_logger.IsEnabled(LogLevel.Trace))
        //     _logger.LogTrace("[Read] STATUS_SUCCESS for {path}, bytesTransferred = {bytesTransferred}", node.Path, bytesTransferred);

        return STATUS_SUCCESS;
    }

    public override void Close(
        object fileNode,
        object fileDesc)
    {
        // TODO: unset FileResource after some time
    }

    public override int Create(
        string fileName,
        uint createOptions,
        uint grantedAccess,
        uint fileAttributes,
        byte[] securityDescriptor,
        ulong allocationSize,
        out object fileNode,
        out object fileDesc,
        out FileInfo fileInfo,
        out string normalizedName)
    {
        fileNode = null!;
        fileDesc = null!;
        fileInfo = default;
        normalizedName = null!;
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    public override int Write(
        object fileNode,
        object fileDesc,
        nint buffer,
        ulong offset,
        uint length,
        bool writeToEndOfFile,
        bool constrainedIo,
        out uint bytesTransferred,
        out FileInfo fileInfo)
    {
        bytesTransferred = 0;
        fileInfo = default;
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    public override int Overwrite(
        object fileNode,
        object fileDesc,
        uint fileAttributes,
        bool replaceFileAttributes,
        ulong allocationSize,
        out FileInfo fileInfo)
    {
        fileInfo = default;
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    public override int SetBasicInfo(
        object fileNode,
        object fileDesc,
        uint fileAttributes,
        ulong creationTime,
        ulong lastAccessTime,
        ulong lastWriteTime,
        ulong changeTime,
        out FileInfo fileInfo)
    {
        fileInfo = default;
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    public override int SetFileSize(
        object fileNode,
        object fileDesc,
        ulong newSize,
        bool setAllocationSize,
        out FileInfo fileInfo)
    {
        fileInfo = default;
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    public override int Rename(
        object fileNode,
        object fileDesc,
        string fileName,
        string newFileName,
        bool replaceIfExists)
    {
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    public override int SetSecurity(
        object fileNode,
        object fileDesc,
        System.Security.AccessControl.AccessControlSections sections,
        byte[] securityDescriptor)
    {
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    public override int SetDelete(
        object fileNode,
        object fileDesc,
        string fileName,
        bool deleteFile)
    {
        return STATUS_MEDIA_WRITE_PROTECTED;
    }

    private List<SqNode> GetDirectoryEntries(SqNode currentNode)
    {
        var entries = new List<SqNode>();

        foreach (var node in _pathList.GetNodesInFolder(currentNode.Hash.Folder).Except([currentNode]))
        {
            if (!node.HasMetdata)
            {
                if (_gameDataProvider.GameData?.GetFileMetadata(node.Path) is { } fileMetadata)
                {
                    node.FileInfo = node.FileInfo with
                    {
                        FileSize = fileMetadata.RawFileSize,
                        AllocationSize = fileMetadata.Size,
                    };
                }

                node.HasMetdata = true;
            }

            if (node.HasMetdata)
                entries.Add(node);
        }

        // TODO: return . and .. folder entries too

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return entries;
    }
}
