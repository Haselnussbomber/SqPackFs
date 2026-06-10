using System.Text;

namespace SqPackFs;

public static class Utils
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

    public static ulong GetFullHash(uint folder, uint file)
    {
        return ((ulong)folder << 32) | file;
    }

    public static (uint Folder, uint File) GetHash(ulong fullHash)
    {
        return ((uint)(fullHash >> 32), (uint)fullHash);
    }

    public static string PrintFileHash(uint hash)
    {
        return "~" + hash.ToString("X8");
    }

    public static string PrintFileHash(ulong hash)
    {
        var (folder, file) = GetHash(hash);
        return $"{PrintFileHash(folder)}/{PrintFileHash(file)}";
    }
}
