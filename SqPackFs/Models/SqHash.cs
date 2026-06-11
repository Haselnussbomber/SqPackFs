using SqPackFs.Utils;

namespace SqPackFs.Models;

[StructLayout(LayoutKind.Explicit, Size = 0x0C)]
public struct SqHash : IEquatable<SqHash>
{
    [FieldOffset(0x00)] public ulong Combined;
    [FieldOffset(0x00)] public SqFolderHash Folder;
    [FieldOffset(0x04)] public SqFileHash File;
    [FieldOffset(0x08)] public SqFullHash Full;

    public SqHash(string path)
    {
        // TODO: check if this is correct

        if (path.Contains('/'))
        {
            (File, Folder) = HashUtils.GetHash(path);
            Full = Lumina.Misc.Crc32.Get(path);
        }
        else
        {
            var hash = Lumina.Misc.Crc32.Get(path);
            Folder = hash;
            Full = hash;
        }
    }

    public bool Equals(SqHash other) => Combined == other.Combined && Full == other.Full;
    public override bool Equals(object? obj) => obj is SqHash hash && Equals(hash);
    public override int GetHashCode() => HashCode.Combine(Combined, Full);
    public static bool operator ==(SqHash left, SqHash right) => left.Equals(right);
    public static bool operator !=(SqHash left, SqHash right) => !(left == right);
}
