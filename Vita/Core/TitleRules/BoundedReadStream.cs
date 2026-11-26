namespace Vita.Core.TitleRules;

internal sealed class BoundedReadStream(Stream inner, long start, long length) : Stream
{
    private long _position;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        long remaining = length - _position;

        if (remaining <= 0)
            return 0;

        if (buffer.Length > remaining)
            buffer = buffer[..(int)remaining];

        inner.Position = start + _position;

        int read = inner.Read(buffer);

        _position += read;

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0)
            throw new IOException("seek position is negative");

        _position = target;

        return _position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
