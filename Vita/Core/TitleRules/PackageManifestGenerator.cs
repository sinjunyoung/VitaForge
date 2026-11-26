using Common.Models;
using Patch.Core;
using System.Security.Cryptography;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class PackageManifestGenerator(string titleId, MergeGroup group, PackageManifest manifest, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log)
{
    private static readonly FselfLayout[] EbootLayouts = [FselfLayout.Standard, FselfLayout.Vita3K];

    private readonly Dictionary<string, PatchContainerFile> _patches = BuildPatchMap(group);

    public async Task<GenerationResult> GenerateAsync(string tempDirectory, CancellationToken ct)
    {
        var generated = manifest.Files.Where(e => !e.IsDirect).ToList();

        if (generated.Count == 0)
            return default;

        int succeeded = 0;

        foreach (var entry in generated.Where(e => e.SourceKind != PackageManifestSourceKinds.PackageMember))
        {
            if (await ApplyAsync(entry, () => ResolveLocalSource(entry), ct))
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
                if (await ApplyAsync(entry, () => archive.ReadMember(entry.Source ?? throw new InvalidDataException("package member source is missing")), ct))
                    succeeded++;
            }

            return succeeded;
        }
    }

    private async Task<bool> ApplyAsync(PackageManifestEntry entry, Func<byte[]> loadSource, CancellationToken ct)
    {
        try
        {
            byte[] source = loadSource();

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

    private byte[] ResolveLocalSource(PackageManifestEntry entry) => entry.SourceKind switch
    {
        PackageManifestSourceKinds.Empty => [],
        PackageManifestSourceKinds.DecryptedEboot => ResolveEboot(entry),
        _ => throw new NotSupportedException($"unsupported source kind: {entry.SourceKind}")
    };

    private byte[] ResolveEboot(PackageManifestEntry entry)
    {
        string path = PatchShared.NormalizeZipPath(entry.Destination);

        if (!group.Index.TryGetValue(path, out var appEntry))
            throw new FileNotFoundException(path);

        var owner = appEntry.Owner;
        byte[] raw = NoNpDrmDecryptor.DecryptEntry(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, appEntry.FileEntry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt);

        if (HashMatches(raw, entry.SourceSha256))
            return raw;

        foreach (var layout in EbootLayouts)
        {
            byte[] decrypted = VitaSelfDecryptor.DecryptToFself(raw, owner.License.Klicensee, layout);

            if (!HashMatches(decrypted, entry.SourceSha256))
                continue;

            log($"{titleId} {path}: decrypted with {layout} layout", LogLevel.Info);

            return decrypted;
        }

        throw new InvalidDataException($"no decrypted eboot layout matches source sha256 {entry.SourceSha256}");
    }

    private byte[] ReadPatch(PackageManifestEntry entry)
    {
        if (!_patches.TryGetValue(manifest.PatchPath(entry), out var file))
            throw new FileNotFoundException(manifest.PatchPath(entry));

        byte[] data = group.PatchCtx.Accessor.ReadAllBytes(file.Raw);

        if (!HashMatches(data, entry.PatchSha256))
            throw new InvalidDataException($"damaged patch: {entry.Patch}");

        return data;
    }

    private static void Verify(string role, byte[] data, long expectedSize, string expectedSha256)
    {
        if (data.Length != expectedSize || !HashMatches(data, expectedSha256))
            throw new InvalidDataException($"{role} mismatch (expected {expectedSize:N0} bytes sha256 {expectedSha256}, got {data.Length:N0} bytes sha256 {Hash(data)})");
    }

    private static Dictionary<string, PatchContainerFile> BuildPatchMap(MergeGroup group)
    {
        var map = new Dictionary<string, PatchContainerFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in group.PatchCtx.Files)
            map.TryAdd(file.Path, file);

        return map;
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private static bool HashMatches(byte[] data, string expectedSha256) => Hash(data).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
}
