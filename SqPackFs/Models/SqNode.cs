using Lumina.Data;
using FileInfo = Fsp.Interop.FileInfo;

namespace SqPackFs.Models;

public record SqNode
{
    private static readonly ulong VolumeCreationTime = (ulong)DateTimeOffset.UtcNow.ToFileTime();

    public SqNode(string path, SqHash hash)
    {
        Hash = hash;
        IsDirectory = hash.File == 0;
        Name = System.IO.Path.GetFileName(path);
        Path = path;
        FileInfo = new FileInfo
        {
            CreationTime = VolumeCreationTime,
            LastAccessTime = VolumeCreationTime,
            LastWriteTime = VolumeCreationTime,
            ChangeTime = VolumeCreationTime,
            FileAttributes = (uint)((IsDirectory ? FileAttributes.Directory : FileAttributes.Normal) | FileAttributes.ReadOnly),
        };
    }

    public SqHash Hash { get; }
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsDirectory { get; }
    public FileInfo FileInfo { get; set; }
    public bool HasMetdata { get; set; }
    public FileResource? FileResource { get; set; }
}
