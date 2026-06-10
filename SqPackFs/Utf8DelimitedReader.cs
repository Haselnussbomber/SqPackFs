using System.Buffers.Text;

namespace SqPackFs;

using System.Buffers;
using System.IO;
using System.Text;

public partial class Utf8CsvReader(Stream stream, int initialBufferSize = 32 * 1024) : Utf8DelimitedReader(stream, (byte)',', initialBufferSize);
public partial class Utf8TsvReader(Stream stream, int initialBufferSize = 32 * 1024) : Utf8DelimitedReader(stream, (byte)'\t', initialBufferSize);

public partial class Utf8DelimitedReader(Stream stream, byte columnSeparator, int initialBufferSize = 32 * 1024) : IDisposable
{
    private byte[]? _buffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
    private int _bufferOffset = 0;
    private int _bufferCount = 0;
    private bool _isEof = false;

    private ReadOnlyMemory<byte> _currentLine;

    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }

    /// <summary>
    /// Gets a reader for the current row.
    /// </summary>
    public Utf8RowReader GetRowReader()
    {
        return new Utf8RowReader(_currentLine.Span, columnSeparator);
    }

    private bool _isInsideQuote = false;

    public void Reset()
    {
        stream.Seek(0, SeekOrigin.Begin);
        _bufferOffset = 0;
        _bufferCount = 0;
        _isEof = false;
    }

    public bool ReadNextRow()
    {
        if (_buffer == null)
            return false;

        var startOffset = _bufferOffset;

        while (true)
        {
            var searchSpan = _buffer.AsSpan(_bufferOffset, _bufferCount - _bufferOffset);

            for (var i = 0; i < searchSpan.Length; i++)
            {
                var b = searchSpan[i];

                if (b == (byte)'\"')
                {
                    _isInsideQuote = !_isInsideQuote;
                }
                else if (b == (byte)'\n' && !_isInsideQuote)
                {
                    var absoluteNewlineIdx = _bufferOffset + i;
                    var length = absoluteNewlineIdx - startOffset;

                    if (length > 0 && _buffer[startOffset + length - 1] == (byte)'\r')
                        length--;

                    _currentLine = _buffer.AsMemory(startOffset, length);
                    _bufferOffset = absoluteNewlineIdx + 1;
                    return true;
                }
            }

            if (_isEof)
            {
                if (startOffset < _bufferCount)
                {
                    var length = _bufferCount - startOffset;
                    if (length > 0 && _buffer[startOffset + length - 1] == (byte)'\r')
                        length--;

                    _currentLine = _buffer.AsMemory(startOffset, length);
                    _bufferOffset = _bufferCount;
                    return true;
                }
                return false;
            }

            var remaining = _bufferCount - startOffset;
            if (remaining >= _buffer.Length)
            {
                // double buffer size if it doesn't fit
                var newSize = _buffer.Length * 2;
                var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

                Array.Copy(_buffer, startOffset, newBuffer, 0, remaining);

                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = newBuffer;
            }
            else if (remaining > 0)
            {
                Array.Copy(_buffer, startOffset, _buffer, 0, remaining);
            }

            _bufferOffset = remaining;
            startOffset = 0;

            var read = stream.Read(_buffer.AsSpan(remaining));
            if (read == 0)
            {
                _isEof = true;
                _bufferCount = remaining;
            }
            else
            {
                _bufferCount = remaining + read;
            }
        }
    }
}

public ref struct Utf8RowReader(ReadOnlySpan<byte> line, byte columnSeparator = (byte)',')
{
    private ReadOnlySpan<byte> _remaining = line;

    public bool TryReadRaw(out ReadOnlySpan<byte> value)
    {
        if (_remaining.IsEmpty)
        {
            value = default;
            return false;
        }

        if (_remaining[0] == (byte)'\"')
        {
            var searchOffset = 1;

            while (true)
            {
                var nextQuote = _remaining[searchOffset..].IndexOf((byte)'\"');
                if (nextQuote == -1)
                {
                    value = _remaining[1..];
                    _remaining = default;
                    return true;
                }

                var actualQuoteIdx = searchOffset + nextQuote;
                if (actualQuoteIdx + 1 < _remaining.Length && _remaining[actualQuoteIdx + 1] == (byte)'\"')
                {
                    searchOffset = actualQuoteIdx + 2;
                    continue;
                }

                value = _remaining[1..actualQuoteIdx];

                var remainingAfterQuote = _remaining[(actualQuoteIdx + 1)..];
                var nextSep = remainingAfterQuote.IndexOf(columnSeparator);
                _remaining = nextSep >= 0 ? remainingAfterQuote[(nextSep + 1)..] : default;
                return true;
            }
        }

        var separatorIdx = _remaining.IndexOf(columnSeparator);
        if (separatorIdx >= 0)
        {
            value = _remaining[..separatorIdx];
            _remaining = _remaining[(separatorIdx + 1)..];
            return true;
        }

        value = _remaining;
        _remaining = default;
        return true;
    }

    public bool TryRead(out string value)
    {
        if (!TryReadRaw(out var column))
        {
            value = string.Empty;
            return false;
        }

        value = Encoding.UTF8.GetString(column);

        if (column.IndexOf((byte)'\"') != -1)
            value = value.Replace("\"\"", "\"");

        return true;
    }

    public bool TryRead(out bool value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out sbyte value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out byte value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out short value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out ushort value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out int value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out uint value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out long value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out ulong value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out float value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out double value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out decimal value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out DateTime value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out DateTimeOffset value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out TimeSpan value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool TryRead(out Guid value, char standardFormat = default)
    {
        value = default;
        return TryReadRaw(out var column) && Utf8Parser.TryParse(column, out value, out _, standardFormat);
    }

    public bool Skip()
    {
        return TryReadRaw(out _);
    }
}
