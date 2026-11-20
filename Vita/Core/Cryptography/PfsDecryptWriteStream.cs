namespace Vita.Core.Cryptography;

internal sealed class PfsDecryptWriteStream : Stream
{
    private const int TargetBufferSize = 1024 * 1024;

    private readonly Stream _inner;
    private readonly PfsGameDataCipher _cipher;
    private readonly byte[] _buffer;
    private int _filled;
    private long _position;

    public PfsDecryptWriteStream(Stream inner, PfsGameDataCipher cipher, int sectorSize)
    {
        _inner = inner;
        _cipher = cipher;

        int sectors = Math.Max(1, TargetBufferSize / sectorSize);

        _buffer = new byte[sectors * sectorSize];
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(ReadOnlySpan<byte> source)
    {
        while (!source.IsEmpty)
        {
            int count = Math.Min(_buffer.Length - _filled, source.Length);

            source[..count].CopyTo(_buffer.AsSpan(_filled));

            _filled += count;
            source = source[count..];

            if (_filled == _buffer.Length)
                FlushBlock();
        }
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public void Finish()
    {
        if (_filled > 0)
            FlushBlock();
    }

    private void FlushBlock()
    {
        _cipher.DecryptRange(_position, _buffer.AsSpan(0, _filled));
        _inner.Write(_buffer, 0, _filled);

        _position += _filled;
        _filled = 0;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}