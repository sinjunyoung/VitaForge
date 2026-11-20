namespace Vita.Core.Services;

internal static class SourceStreamCopier
{
    private const int ChunkSize = 1024 * 1024;

    public static void Copy(Stream source, Stream destination, Action<long>? onProgress)
    {
        var buffer = new byte[ChunkSize];
        int read;

        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, read);
            onProgress?.Invoke(read);
        }
    }

    public static void CopyScaled(ISourceAccessor accessor, string relativePath, Stream destination, long expectedSize, Action<long>? onProgress)
    {
        var scaler = new ProgressScaler(accessor.GetFileSize(relativePath), expectedSize, onProgress);

        accessor.CopyTo(relativePath, destination, scaler.Report);
        scaler.Complete();
    }
}