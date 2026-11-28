using Common.Models;
using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed class ChunkPatchDlcGenerator(string titleId, ChunkPatchAssets assets, IReadOnlyList<ChunkPatchDlcFile> files, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log)
{
    public async Task<GenerationResult> GenerateAsync(CancellationToken ct)
    {
        var builder = new ChunkPatchRealHardwareBuilder(titleId, assets, log);
        int succeeded = 0;

        foreach (var file in files)
        {
            if (await ApplyAsync(builder, file, ct))
                succeeded++;
        }

        log($"{titleId} DLC chunk patch: {succeeded}/{files.Count} files generated", succeeded == files.Count ? LogLevel.Ok : LogLevel.Error);

        return new GenerationResult(files.Count, succeeded);
    }

    private async Task<bool> ApplyAsync(ChunkPatchRealHardwareBuilder builder, ChunkPatchDlcFile file, CancellationToken ct)
    {
        try
        {
            byte[] source = await GroupSourceReader.ReadFileAsync(file.Entry, onBytes, ct);
            byte[] output = builder.Build(file.CpkName, source, ct);

            await emit(new ExpandedFile(file.RelativePath, output));

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log($"{titleId} {file.RelativePath}: {ex.Message}", LogLevel.Error);

            onBytes(file.Entry.FileEntry.Size);

            return false;
        }
    }
}