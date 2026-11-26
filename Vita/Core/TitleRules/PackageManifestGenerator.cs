using Common.Models;
using Patch.Core;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class PackageManifestGenerator(string titleId, MergeGroup group, PackageManifest manifest, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log)
{
    private readonly Dictionary<string, PatchContainerFile> _patches = BuildPatchMap(group);

    public async Task<GenerationResult> GenerateAsync(string tempDirectory, CancellationToken ct)
    {
        var generated = manifest.Files.Where(e => !e.IsDirect).ToList();

        if (generated.Count == 0)
            return default;

        int succeeded = 0;

        foreach (var entry in generated.Where(e => e.SourceKind != PackageManifestSourceKinds.PackageMember))
        {
            if (await ApplyAsync(entry, () => ResolveLocalSourceAsync(entry, ct), ct))
                succeeded++;
        }

        foreach (var members in generated.Where(e => e.SourceKind == PackageManifestSourceKinds.PackageMember).GroupBy(e => PatchShared.NormalizeZipPath(e.Source?.Archive ?? string.Empty), StringComparer.OrdinalIgnoreCase))
            succeeded += await ApplyArchiveAsync(members.Key, [.. members], tempDirectory, ct);

        log($"{titleId} manifest: {succeeded}/{generated.Count} files generated", succeeded == generated.Count ? LogLevel.Ok : LogLevel.Error);

        return new GenerationResult(generated.Count, succeeded);
    }

    private async Task<int> ApplyArchiveAsync(string archivePath, List<PackageManifestEntry> entries, string tempDirectory, CancellationToken ct)
    {
        DecryptedArchiveFile archive;

        try
        {
            archive = await DecryptedArchiveFile.ExtractAsync(group, archivePath, tempDirectory, onBytes, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log($"{titleId} {archivePath}: {ex.Message}", LogLevel.Error);

            foreach (var entry in entries)
                onBytes(entry.TargetSize);

            return 0;
        }

        using (archive)
        {
            int succeeded = 0;

            foreach (var entry in entries.OrderBy(e => e.Source?.Offset))
            {
                if (await ApplyAsync(entry, () => Task.FromResult(archive.ReadMember(entry.Source ?? throw new InvalidDataException("package member source is missing"))), ct))
                    succeeded++;
            }

            return succeeded;
        }
    }

    private async Task<bool> ApplyAsync(PackageManifestEntry entry, Func<Task<byte[]>> loadSource, CancellationToken ct)
    {
        try
        {
            byte[] source = await loadSource();

            Verify("source", source, entry.SourceSize, entry.SourceSha256);

            byte[] output = await UniversalPatcher.ApplyPatchAsync(source, ReadPatch(entry), null, ct);

            Verify("result", output, entry.TargetSize, entry.TargetSha256);

            await emit(new ExpandedFile(PatchShared.NormalizeZipPath(entry.Destination), output));

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log($"{titleId} {entry.Destination}: {ex.Message}", LogLevel.Error);

            onBytes(entry.TargetSize);

            return false;
        }
    }

    private Task<byte[]> ResolveLocalSourceAsync(PackageManifestEntry entry, CancellationToken ct) => entry.SourceKind switch
    {
        PackageManifestSourceKinds.Empty => Task.FromResult<byte[]>([]),
        PackageManifestSourceKinds.DecryptedEboot => ResolveEbootAsync(entry, ct),
        _ => throw new NotSupportedException($"unsupported source kind: {entry.SourceKind}")
    };

    private Task<byte[]> ResolveEbootAsync(PackageManifestEntry entry, CancellationToken ct)
    {
        string path = PatchShared.NormalizeZipPath(entry.Destination);

        if (!group.Index.TryGetValue(path, out var appEntry))
            throw new FileNotFoundException(path);

        return GroupSourceReader.ReadDecryptedSelfAsync(appEntry, data => Sha256Hex.Matches(data, entry.SourceSha256), entry.SourceSha256, $"{titleId} {path}", log, _ => { }, ct);
    }

    private byte[] ReadPatch(PackageManifestEntry entry)
    {
        if (!_patches.TryGetValue(manifest.PatchPath(entry), out var file))
            throw new FileNotFoundException(manifest.PatchPath(entry));

        byte[] data = group.PatchCtx.Accessor.ReadAllBytes(file.Raw);

        if (!Sha256Hex.Matches(data, entry.PatchSha256))
            throw new InvalidDataException($"damaged patch: {entry.Patch}");

        return data;
    }

    private static void Verify(string role, byte[] data, long expectedSize, string expectedSha256)
    {
        if (data.Length != expectedSize || !Sha256Hex.Matches(data, expectedSha256))
            throw new InvalidDataException($"{role} mismatch (expected {expectedSize:N0} bytes sha256 {expectedSha256}, got {data.Length:N0} bytes sha256 {Sha256Hex.Of(data)})");
    }

    private static Dictionary<string, PatchContainerFile> BuildPatchMap(MergeGroup group)
    {
        var map = new Dictionary<string, PatchContainerFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in group.PatchCtx.Files)
            map.TryAdd(file.Path, file);

        return map;
    }
}
