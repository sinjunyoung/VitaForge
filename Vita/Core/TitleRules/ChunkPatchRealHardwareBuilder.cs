using Common.Models;
using Vita.Core.Cpk;
using Vita.Core.Ftx;

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

        return CpkAppender.AppendWithoutEtoc(archive, replacements, ct);
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