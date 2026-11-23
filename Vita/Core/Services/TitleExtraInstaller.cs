using Common.Localization;
using Common.Models;
using Vita.Core.Models;
using Vita.Core.TitleRules;

namespace Vita.Core.Services;

public static class TitleExtraInstaller
{
    public static int InstallToEmulatorRoot(string patchPath, string titleId, string emulatorRoot, WrittenFileTracker tracker, Action<string, LogLevel> log, CancellationToken ct)
    {
        using var accessor = SourceAccessorFactory.Open(patchPath);

        var extras = Collect(accessor, titleId);

        if (extras.Count == 0)
            return 0;

        using var sink = new FolderDecryptSink(emulatorRoot, tracker);

        foreach (var extra in extras)
        {
            ct.ThrowIfCancellationRequested();

            using var stream = sink.Open(extra.TargetPath);

            accessor.CopyTo(extra.Raw, stream);
        }

        log(Loc.S.LogPsvPatchExtraFilesInstalled(titleId, extras.Count, emulatorRoot), LogLevel.Ok);

        return extras.Count;
    }

    public static bool HasExtraFiles(string patchPath, string titleId)
    {
        using var accessor = SourceAccessorFactory.Open(patchPath);

        return Collect(accessor, titleId).Count > 0;
    }

    private static List<ExtraPatchFile> Collect(ISourceAccessor accessor, string titleId)
    {
        var files = accessor.EnumerateAllFiles().Select(raw => new PatchContainerFile(PatchShared.NormalizeZipPath(raw), raw)).ToList();

        return [.. TitleRuleRegistry.SelectExtraFiles(titleId, files).Where(e => !e.TargetPath.Split('/').Contains(".."))];
    }
}