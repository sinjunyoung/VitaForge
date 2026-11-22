using Common.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class L7caUnpackRule(string titleId, string archiveRelativePath, string prefix) : ITitleRule
{
    private readonly string _archivePath = PatchShared.NormalizeZipPath(archiveRelativePath);

    public string TitleId { get; } = titleId;

    public IEnumerable<ExpandedFile> ExpandOutput(string relativePath, byte[] outputBytes, Action<string, LogLevel> log)
    {
        if (!PatchShared.NormalizeZipPath(relativePath).Equals(_archivePath, StringComparison.OrdinalIgnoreCase))
            yield break;

        var failures = new List<string>();
        int count = 0;

        foreach (var (path, data) in L7caUnpacker.Unpack(outputBytes, prefix, failures))
        {
            count++;

            yield return new ExpandedFile(path, data);
        }

        log($"{TitleId} {relativePath}: {count} files unpacked, {failures.Count} failed", failures.Count == 0 ? LogLevel.Ok : LogLevel.Error);

        foreach (var failure in failures.Take(20))
            log($"  {failure}", LogLevel.Error);
    }
}