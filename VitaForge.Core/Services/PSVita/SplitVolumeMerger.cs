using Common.Localization;
using System.IO;
using System.Text.RegularExpressions;

namespace VitaForge.Core.Services.PSVita;

public static class SplitVolumeMerger
{
    private const int BufferSize = 1024 * 1024;
    private const int SuffixLength = 4;

    private static readonly Regex NumberedVolumeRegex = new(@"\.(\d{3})$", RegexOptions.Compiled);

    public static bool IsNumbered(IReadOnlyList<string> volumePaths)
        => volumePaths.Count > 0 && volumePaths.All(p => NumberedVolumeRegex.IsMatch(p));

    public static async Task<string> MergeAsync(IReadOnlyList<string> volumePaths, CancellationToken ct)
    {
        var ordered = volumePaths.OrderBy(GetNumber).ToList();
        string mergedPath = ordered[0][..^SuffixLength];

        for (int i = 0; i < ordered.Count; i++)
        {
            if (GetNumber(ordered[i]) != i + 1)
                throw new InvalidOperationException(Loc.S.ErrPsvPatchSplitVolumeMissing($"{Path.GetFileName(mergedPath)}.{i + 1:D3}"));
        }

        string tempPath = mergedPath + ".merging";

        try
        {
            await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true))
            {
                foreach (string volumePath in ordered)
                {
                    await using (var input = new FileStream(volumePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true))
                        await input.CopyToAsync(output, BufferSize, ct);

                    File.Delete(volumePath);
                }
            }

            File.Move(tempPath, mergedPath, true);
        }
        catch
        {
            DeleteFileQuietly(tempPath);

            throw;
        }

        return mergedPath;
    }

    private static int GetNumber(string path) => int.Parse(NumberedVolumeRegex.Match(path).Groups[1].Value);

    private static void DeleteFileQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch { }
    }
}