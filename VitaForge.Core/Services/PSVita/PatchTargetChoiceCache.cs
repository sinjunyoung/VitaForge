using System.IO;
using Vita.Core.Models;

namespace VitaForge.Core.Services.PSVita;

public sealed class PatchTargetChoiceCache
{
    private readonly Dictionary<string, IReadOnlyList<string?>> _choices = new(StringComparer.OrdinalIgnoreCase);
    private string _fingerprint = string.Empty;

    public void Clear()
    {
        _choices.Clear();
        _fingerprint = string.Empty;
    }

    public void Reset(string fingerprint)
    {
        if (string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal))
            return;

        _choices.Clear();
        _fingerprint = fingerprint;
    }

    public bool TryGet(PatchTargetRequest request, out IReadOnlyList<string?> selections)
    {
        if (_choices.TryGetValue(KeyOf(request), out var cached) && cached.All(s => s == null || request.CandidatePaths.Contains(s, StringComparer.OrdinalIgnoreCase)))
        {
            selections = cached;

            return true;
        }

        selections = [];

        return false;
    }

    public void Set(PatchTargetRequest request, IReadOnlyList<string?> selections) => _choices[KeyOf(request)] = selections;

    public static string BuildFingerprint(IEnumerable<string?> paths)
    {
        var parts = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetFullPath(p!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(Describe);

        return string.Join("\n", parts);
    }

    private static string KeyOf(PatchTargetRequest request) => $"{request.TitleId}|{request.Category}|{request.ContentIdSuffix}|{string.Join("|", request.PatchFileNames)}";

    private static string Describe(string path)
    {
        if (File.Exists(path))
        {
            var info = new FileInfo(path);

            return $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }

        if (!Directory.Exists(path))
            return $"{path}|missing";

        long count = 0;
        long totalLength = 0;
        long latestTicks = 0;

        foreach (var info in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            count++;
            totalLength += info.Length;
            latestTicks = Math.Max(latestTicks, info.LastWriteTimeUtc.Ticks);
        }

        return $"{path}|dir|{count}|{totalLength}|{latestTicks}";
    }
}