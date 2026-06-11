namespace SqPackFs.Utils;

public static class FileUtils
{
    public static int CountLines(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file was not found.", filePath);

        Span<byte> buffer = stackalloc byte[64 * 1024];
        var lineCount = 0;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: buffer.Length);

        int bytesRead;
        while ((bytesRead = stream.Read(buffer)) > 0)
            lineCount += buffer[..bytesRead].Count((byte)'\n');

        return lineCount;
    }
}
