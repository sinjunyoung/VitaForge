using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal static class ChunkPatchDlcFinder
{
    private static readonly string[] BaseCpkNames = ["NinPri", "NinPriPatch"];

    public static IReadOnlyList<ChunkPatchDlcFile> Find(MergeGroup group, ChunkPatchAssets assets)
    {
        if (!assets.HasTextures)
            return [];

        var result = new List<ChunkPatchDlcFile>();
        var cpkNames = assets.Manifest.Values
            .SelectMany(i => i.Locations)
            .Select(l => l.Cpk)
            .Where(name => !BaseCpkNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string cpkName in cpkNames)
        {
            string fileName = $"NinPri{cpkName}.cpk";

            foreach (var (relativePath, entry) in group.Index)
            {
                if (Path.GetFileName(relativePath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    result.Add(new ChunkPatchDlcFile(cpkName, relativePath, entry));
            }
        }

        return result;
    }
}