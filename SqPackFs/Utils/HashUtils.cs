using System.Text;

namespace SqPackFs.Utils;

public static class HashUtils
{
    public static (uint Folder, uint File) GetHash(string path)
    {
        path = path.ToLower();
        var bytes = Encoding.UTF8.GetBytes(path); // `char` is utf16 lol
        var folder = bytes.AsSpan(0, path.LastIndexOf('/'));
        var file = bytes.AsSpan(path.LastIndexOf('/') + 1);

        var folderHash = Lumina.Misc.Crc32.Get(folder);
        var fileHash = Lumina.Misc.Crc32.Get(file);
        return (folderHash, fileHash);
    }

    public static ulong GetFullHash(string path)
    {
        var (folder, file) = GetHash(path);
        return ((ulong)folder << 32) | file;
    }
}
