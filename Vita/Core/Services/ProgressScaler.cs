namespace Vita.Core.Services;

internal sealed class ProgressScaler(long sourceSize, long expectedSize, Action<long>? onProgress)
{
    private long _sourceDone;
    private long _reported;

    public void Report(long bytes)
    {
        _sourceDone += bytes;

        long target = sourceSize > 0 ? Math.Min(expectedSize, (long)((double)_sourceDone / sourceSize * expectedSize)) : expectedSize;

        if (target <= _reported)
            return;

        onProgress?.Invoke(target - _reported);

        _reported = target;
    }

    public void Complete()
    {
        if (_reported < expectedSize)
            onProgress?.Invoke(expectedSize - _reported);

        _reported = expectedSize;
    }
}