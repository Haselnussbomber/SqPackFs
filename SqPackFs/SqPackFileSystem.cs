using Fsp;
using Fsp.Interop;
using SqPackFs.Services;
using FileInfo = Fsp.Interop.FileInfo;

namespace SqPackFs;

[RegisterSingleton, AutoConstruct]
public partial class SqPackFileSystem : FileSystemBase
{
    private readonly ILogger<SqPackFileSystem> _logger;
    private readonly PathList _pathList;
    private readonly Dictionary<string, SqFileNode> _nodes = [];

    public override int ExceptionHandler(Exception ex)
    {
        _logger.LogError(ex, "Unexpected exception");
        return base.ExceptionHandler(ex);
    }

    public override int Init(object argHost)
    {
        var host = (FileSystemHost)argHost;

        // TODO: figure out what to set here (read only?)

        return STATUS_SUCCESS;
    }

    public override int GetVolumeInfo(out VolumeInfo volumeInfo)
    {
        volumeInfo = default;
        volumeInfo.TotalSize = 1024 * 1024 * 1024; // 1GB fake size
        volumeInfo.FreeSize = 0;
        volumeInfo.SetVolumeLabel("FFXIV SqPack");
        return STATUS_SUCCESS;
    }

    public override int GetFileInfo(object fileNode, object fileDesc, out FileInfo fileInfo)
    {
        fileInfo = default;

        if (fileNode is not SqFileNode node)
            return STATUS_INVALID_HANDLE;

        fileInfo.FileAttributes = (uint)(node.IsDirectory ? FileAttributes.Directory : FileAttributes.ReadOnly);
        fileInfo.FileSize = node.Size;

        return STATUS_SUCCESS;
    }
}
