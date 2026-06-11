
using SqPackFs.Models;

namespace SqPackFs;

public record SqFileNode(string path, SqHash hash)
{
    public SqHash Hash { get; } = hash;
    public string? Path { get; } = path;

    public bool IsDirectory => hash.File != 0;
    public ulong Size { get; set; }
    public Fsp.Interop.FileInfo FileInfo { get; set; }
}
