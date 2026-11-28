using Common.Models;
using Vita.Core.Cpk;
using Vita.Core.Fonts;
using Vita.Core.Ftx;
using Vita.Core.Typography;

namespace Vita.Core.TitleRules;

internal sealed class ChunkPatchRealHardwareBuilder(string titleId, ChunkPatchAssets assets, Action<string, LogLevel> log)
{
    public byte[] Build(string cpkName, byte[] cpk, CancellationToken ct)
    {
        if (!assets.HasTextures)
            throw new InvalidDataException("real hardware textures are missing from the patch package (use the full patcher zip)");

        var archive = CpkArchive.Open(cpk, cpk.Length);
        var baker = new FtxBaker(assets.LoadImage);
        var replacements = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        int total = 0;
        int baked = 0;

        foreach (var (ftxPath, items) in GroupItems(cpkName))
        {
            ct.ThrowIfCancellationRequested();

            total += items.Count;

            var entry = archive.Find(ftxPath);

            if (entry is null)
            {
                log($"{titleId} {cpkName}: {ftxPath} not found", LogLevel.Warning);

                continue;
            }

            byte[] ftx = archive.Extract(entry);
            var result = baker.Bake(ftx, items);

            foreach (string skipped in result.Skipped)
                log($"{titleId} {cpkName} {ftxPath}: {skipped}", LogLevel.Warning);

            if (result.Baked == 0)
                continue;

            replacements[ftxPath] = ftx;
            baked += result.Baked;
        }

        log($"{titleId} {cpkName}: {baked}/{total} Korean textures baked", baked == total ? LogLevel.Ok : LogLevel.Warning);

        if (cpkName.Equals("NinPri", StringComparison.OrdinalIgnoreCase))
            BakeFonts(archive, replacements, ct);

        return CpkAppender.AppendWithoutEtoc(archive, replacements, ct);
    }

    private void BakeFonts(CpkArchive archive, Dictionary<string, byte[]> replacements, CancellationToken ct)
    {
        if (!FontBakeResourceLoader.TryLoad(assets, GlyphRasterizerProvider.Current, out var resources, out string? error))
        {
            log($"{titleId}: {error}", LogLevel.Warning);

            return;
        }

        var baker = new FontFtxBaker(resources!);
        int pages = 0;

        foreach (string path in KnownFontPages.FontFtxPaths)
        {
            ct.ThrowIfCancellationRequested();

            var entry = archive.Find(path);

            if (entry is null)
            {
                log($"{titleId}: {path} not found", LogLevel.Warning);

                continue;
            }

            try
            {
                byte[] ftx = replacements.TryGetValue(path, out byte[]? existing) ? existing : archive.Extract(entry);
                int baked = baker.Bake(ftx, message => log($"{titleId} {path}: {message}", LogLevel.Info));

                if (baked == 0)
                    continue;

                replacements[path] = ftx;
                pages += baked;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log($"{titleId} {path}: font bake failed: {ex}", LogLevel.Error);
            }
        }

        log($"{titleId}: {pages} Korean font pages baked", pages > 0 ? LogLevel.Ok : LogLevel.Warning);
    }

    private IEnumerable<(string FtxPath, List<FtxBakeItem> Items)> GroupItems(string cpkName)
    {
        var groups = new Dictionary<string, List<FtxBakeItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hash, info) in assets.Manifest)
        {
            foreach (var location in info.Locations.Where(l => l.Cpk.Equals(cpkName, StringComparison.OrdinalIgnoreCase)))
            {
                if (!groups.TryGetValue(location.FtxPath, out var items))
                {
                    items = [];
                    groups[location.FtxPath] = items;
                }

                items.Add(new FtxBakeItem(hash, location.BlockOrdinal, location.Width, location.Height));
            }
        }

        return groups.Select(g => (g.Key, g.Value));
    }
}