using Common.Models;
using Patch.Core;
using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed class EmbeddedPayloadGenerator(string titleId, MergeGroup group, EmbeddedPayloadRecipe recipe, PatchContainerFile executable, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log)
{
    public async Task<GenerationResult> GenerateAsync(string tempDirectory, CancellationToken ct)
    {
        var entries = recipe.Entries.Where(e => !e.IsOptional || group.Index.ContainsKey(e.RelativePath)).ToList();

        if (entries.Count == 0)
            return default;

        ExecutableResources resources;

        try
        {
            resources = await ExecutableResources.OpenAsync(group.PatchCtx.Accessor, executable.Raw, recipe.AssemblyName, recipe.ResourceMarker, tempDirectory, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log($"{titleId} {executable.Path}: {ex.Message}", LogLevel.Error);

            foreach (var entry in entries)
                onBytes(entry.EstimateSize(group));

            return new GenerationResult(entries.Count, 0);
        }

        using (resources)
        {
            int succeeded = 0;

            foreach (var entry in entries)
            {
                if (await ApplyAsync(entry, resources, ct))
                    succeeded++;
            }

            log($"{titleId} embedded payload: {succeeded}/{entries.Count} files generated", succeeded == entries.Count ? LogLevel.Ok : LogLevel.Error);

            return new GenerationResult(entries.Count, succeeded);
        }
    }

    private async Task<bool> ApplyAsync(EmbeddedPayloadEntry entry, ExecutableResources resources, CancellationToken ct)
    {
        try
        {
            byte[] output = await BuildAsync(entry, resources, ct);

            await emit(new ExpandedFile(entry.RelativePath, output));

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log($"{titleId} {entry.RelativePath}: {ex.Message}", LogLevel.Error);

            onBytes(entry.Variants[0].TargetSize);

            return false;
        }
    }

    private async Task<byte[]> BuildAsync(EmbeddedPayloadEntry entry, ExecutableResources resources, CancellationToken ct)
    {
        if (!group.Index.TryGetValue(entry.RelativePath, out var appEntry))
            throw new FileNotFoundException(entry.RelativePath);

        byte[] source = entry.IsSelf
            ? await GroupSourceReader.ReadDecryptedSelfAsync(appEntry, data => FindVariant(entry, data) is not null, DescribeSources(entry), $"{titleId} {entry.RelativePath}", log, onBytes, ct)
            : await GroupSourceReader.ReadFileAsync(appEntry, onBytes, ct);

        var variant = FindVariant(entry, source) ?? throw new InvalidDataException($"source sha256 {Sha256Hex.Of(source)} does not match any supported original ({DescribeSources(entry)})");
        byte[] output = await UniversalPatcher.ApplyPatchAsync(source, resources.Read(variant.Resource), null, ct);

        if (output.Length != variant.TargetSize || !Sha256Hex.Matches(output, variant.TargetSha256))
            throw new InvalidDataException($"result mismatch (expected {variant.TargetSize:N0} bytes sha256 {variant.TargetSha256}, got {output.Length:N0} bytes sha256 {Sha256Hex.Of(output)})");

        return output;
    }

    private static EmbeddedPayloadVariant? FindVariant(EmbeddedPayloadEntry entry, byte[] source)
    {
        string hash = Sha256Hex.Of(source);

        return entry.Variants.FirstOrDefault(v => v.SourceSha256.Equals(hash, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeSources(EmbeddedPayloadEntry entry) => string.Join(", ", entry.Variants.Select(v => v.SourceSha256));
}
