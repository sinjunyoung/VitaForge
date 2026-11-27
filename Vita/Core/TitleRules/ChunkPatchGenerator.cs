using Common.Models;
using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed class ChunkPatchGenerator(string titleId, MergeGroup group, OutputTarget target, ChunkPatchAssets? assets, IReadOnlyList<ChunkPatchDefinition> definitions, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log)
{
    public async Task<GenerationResult> GenerateAsync(CancellationToken ct)
    {
        int succeeded = 0;

        foreach (var definition in definitions)
        {
            if (await ApplyAsync(definition, ct))
                succeeded++;
        }

        log($"{titleId} chunk patch: {succeeded}/{definitions.Count} files generated", succeeded == definitions.Count ? LogLevel.Ok : LogLevel.Error);

        return new GenerationResult(definitions.Count, succeeded);
    }

    private async Task<bool> ApplyAsync(ChunkPatchDefinition definition, CancellationToken ct)
    {
        try
        {
            byte[] output = await BuildAsync(definition, ct);

            await emit(new ExpandedFile(definition.SourcePath, output));

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log($"{titleId} {definition.SourceName}: {ex.Message}", LogLevel.Error);

            onBytes(definition.TargetSize);

            return false;
        }
    }

    private async Task<byte[]> BuildAsync(ChunkPatchDefinition definition, CancellationToken ct)
    {
        if (!group.Index.TryGetValue(definition.SourcePath, out var appEntry))
            throw new FileNotFoundException(definition.SourcePath);

        long sourceSize = appEntry.FileEntry.Size;

        if (sourceSize != definition.SourceSize)
            throw new InvalidDataException($"source size mismatch (expected {definition.SourceSize:N0} bytes, got {sourceSize:N0} bytes)");

        byte[] blob = ReadBlob(definition);
        byte[] buffer = await GroupSourceReader.ReadFileWithCapacityAsync(appEntry, Math.Max(sourceSize, definition.TargetSize), onBytes, ct);

        if (!Sha256Hex.Matches(buffer.AsSpan(0, (int)sourceSize), definition.SourceSha256))
            throw new InvalidDataException($"source sha256 {Sha256Hex.Of(buffer.AsSpan(0, (int)sourceSize))} does not match the supported original ({definition.SourceSha256})");

        ApplyChunks(definition, buffer, blob, ct);

        if (buffer.Length != definition.TargetSize)
            Array.Resize(ref buffer, (int)definition.TargetSize);

        if (!Sha256Hex.Matches(buffer, definition.TargetSha256))
            throw new InvalidDataException($"result mismatch (expected {definition.TargetSize:N0} bytes sha256 {definition.TargetSha256}, got {buffer.Length:N0} bytes sha256 {Sha256Hex.Of(buffer)})");

        if (target != OutputTarget.Retail)
            return buffer;

        return new ChunkPatchRealHardwareBuilder(titleId, assets ?? throw new InvalidOperationException("real hardware assets are not loaded"), log).Build(Path.GetFileNameWithoutExtension(definition.SourceName), buffer, ct);
    }

    private byte[] ReadBlob(ChunkPatchDefinition definition)
    {
        var file = group.PatchCtx.Files.FirstOrDefault(f => f.Path.Equals(definition.BlobPath, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException(definition.BlobPath);
        byte[] data = group.PatchCtx.Accessor.ReadAllBytes(file.Raw);

        if (data.Length != definition.BlobSize || !Sha256Hex.Matches(data, definition.BlobSha256))
            throw new InvalidDataException($"damaged patch: {definition.BlobFile}");

        return data;
    }

    private static void ApplyChunks(ChunkPatchDefinition definition, byte[] buffer, byte[] blob, CancellationToken ct)
    {
        foreach (var chunk in definition.Chunks)
        {
            ct.ThrowIfCancellationRequested();

            if (chunk.Offset < 0 || chunk.Length < 0 || chunk.BlobOffset < 0 || chunk.Offset + chunk.Length > buffer.Length || chunk.BlobOffset + chunk.Length > blob.Length)
                throw new InvalidDataException($"invalid patch chunk (offset {chunk.Offset:N0}, length {chunk.Length:N0})");

            blob.AsSpan((int)chunk.BlobOffset, chunk.Length).CopyTo(buffer.AsSpan((int)chunk.Offset));
        }
    }
}