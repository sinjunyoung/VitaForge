using Common.Localization;
using Common.Models;
using Patch.Core.Enums;
using Patch.Core.Models;
using System.Diagnostics.CodeAnalysis;

namespace Patch.Core.Services;

public sealed class PatchPlan
{
    private readonly Dictionary<string, PatchFileRef> _overwrites;
    private readonly Dictionary<string, PatchFileRef> _unmatchedOverwrites;
    private readonly Dictionary<string, List<PlannedPatch>> _patches;
    private readonly List<UnmatchedPatch> _unmatchedPatches = [];
    private readonly List<ExplicitPatchMatch> _explicitMatches = [];

    private PatchPlan(StringComparer comparer)
    {
        _overwrites = new Dictionary<string, PatchFileRef>(comparer);
        _unmatchedOverwrites = new Dictionary<string, PatchFileRef>(comparer);
        _patches = new Dictionary<string, List<PlannedPatch>>(comparer);
    }

    public int OverwriteCount => _overwrites.Count;

    public int PatchedCount => _patches.Values.Count(list => list.Any(p => p.Strict));

    public int LooseCandidateCount => _patches.Values.Count(list => list.All(p => !p.Strict));

    public bool HasAnyMatch => OverwriteCount > 0 || _patches.Count > 0;

    public IReadOnlyDictionary<string, PatchFileRef> Overwrites => _overwrites;

    public IReadOnlyDictionary<string, PatchFileRef> UnmatchedOverwrites => _unmatchedOverwrites;

    public IReadOnlyList<UnmatchedPatch> UnmatchedPatches => _unmatchedPatches;

    public IReadOnlyList<ExplicitPatchMatch> ExplicitMatches => _explicitMatches;

    public IEnumerable<string> PatchedPaths => _patches.Keys;

    public bool TryGetOverwrite(string path, [NotNullWhen(true)] out PatchFileRef? file) => _overwrites.TryGetValue(path, out file);

    public bool IsPatched(string path) => _patches.ContainsKey(path);

    public IReadOnlyList<PlannedPatch> GetPatches(string path) => _patches.TryGetValue(path, out var list) ? list : [];

    public static PatchPlan Build(PatchFileIndex index, IEnumerable<string> targetPaths, StringComparer? comparer = null, IReadOnlyDictionary<string, string>? manualTargets = null, PatchMatchOptions? options = null)
    {
        comparer ??= StringComparer.Ordinal;

        var plan = new PatchPlan(comparer);
        var targets = targetPaths.ToList();
        var targetSet = new HashSet<string>(targets, comparer);
        var binaryEntries = new List<(PatchFileEntry Entry, string Key, string? StrippedName, string? StrippedKey)>();

        foreach (var entry in index.Entries)
        {
            string relDir = entry.RelativeDir.Replace(Path.DirectorySeparatorChar, '/');
            string key = relDir.Length == 0 ? entry.BaseName : $"{relDir}/{entry.BaseName}";

            if (entry.Kind == PatchFileKind.Overwrite)
            {
                if (targetSet.Contains(key))
                    plan._overwrites[key] = entry.File;
                else
                    plan._unmatchedOverwrites[key] = entry.File;

                continue;
            }

            string stripped = options?.StripName(entry.BaseName) ?? entry.BaseName;
            bool hasStripped = !stripped.Equals(entry.BaseName, StringComparison.Ordinal);

            binaryEntries.Add((entry, key, hasStripped ? stripped : null, hasStripped ? (relDir.Length == 0 ? stripped : $"{relDir}/{stripped}") : null));
        }

        var exactKeys = new HashSet<string>(comparer);
        var handled = new HashSet<PatchFileEntry>();
        var explicitHandled = new HashSet<PatchFileEntry>();

        if (options is { TargetRules.Count: > 0 })
        {
            foreach (var (entry, _, _, _) in binaryEntries)
            {
                foreach (var rule in options.TargetRules.Where(r => r.MatchesPatch(entry)))
                {
                    var matched = targets.Where(t => !plan._overwrites.ContainsKey(t) && rule.MatchesTarget(t)).ToList();

                    if (matched.Count == 0)
                        continue;

                    foreach (string target in matched)
                    {
                        if (!plan._patches.TryGetValue(target, out var list))
                            plan._patches[target] = list = [];

                        list.Add(new PlannedPatch(entry.File, true));
                        exactKeys.Add(target);
                        plan._explicitMatches.Add(new ExplicitPatchMatch(rule.Patch, target));
                    }

                    handled.Add(entry);
                    explicitHandled.Add(entry);
                }
            }
        }

        foreach (var (entry, key, _, _) in binaryEntries)
        {
            if (handled.Contains(entry) || !targetSet.Contains(key) || plan._overwrites.ContainsKey(key) || !exactKeys.Add(key))
                continue;

            plan._patches[key] = [new PlannedPatch(entry.File, true)];
            handled.Add(entry);
        }

        foreach (var (entry, _, _, _) in binaryEntries)
        {
            if (entry.BaseName.Length == 0 || explicitHandled.Contains(entry))
                continue;

            var candidates = targets.Where(t => !plan._overwrites.ContainsKey(t) && !exactKeys.Contains(t) && NameMatches(FileNameOf(t), entry.BaseName)).ToList();

            if (candidates.Count == 0 || !PatchMatchPolicy.AllowsLooseNameMatch(DetectFormat(entry.File)))
                continue;

            foreach (string candidate in candidates)
            {
                if (!plan._patches.TryGetValue(candidate, out var list))
                    plan._patches[candidate] = list = [];

                list.Add(new PlannedPatch(entry.File, false));
            }

            handled.Add(entry);
        }

        foreach (var (entry, _, _, strippedKey) in binaryEntries)
        {
            if (strippedKey is null || handled.Contains(entry) || !targetSet.Contains(strippedKey) || plan._overwrites.ContainsKey(strippedKey) || !exactKeys.Add(strippedKey))
                continue;

            plan._patches[strippedKey] = [new PlannedPatch(entry.File, true)];
            handled.Add(entry);
        }

        foreach (var (entry, _, strippedName, _) in binaryEntries)
        {
            if (strippedName is null || handled.Contains(entry))
                continue;

            var candidates = targets.Where(t => !plan._overwrites.ContainsKey(t) && !exactKeys.Contains(t) && NameMatches(FileNameOf(t), strippedName)).ToList();

            if (candidates.Count == 0 || !PatchMatchPolicy.AllowsLooseNameMatch(DetectFormat(entry.File)))
                continue;

            foreach (string candidate in candidates)
            {
                if (!plan._patches.TryGetValue(candidate, out var list))
                    plan._patches[candidate] = list = [];

                list.Add(new PlannedPatch(entry.File, false));
            }

            handled.Add(entry);
        }

        foreach (var (entry, key, _, _) in binaryEntries)
        {
            if (entry.BaseName.Length == 0 || entry.RelativeDir == PatchFileIndex.LooseOnlyDir || handled.Contains(entry) || targetSet.Contains(key))
                continue;

            if (manualTargets != null && manualTargets.TryGetValue(key, out string? manualTarget) && targetSet.Contains(manualTarget) && !plan._overwrites.ContainsKey(manualTarget))
            {
                if (!plan._patches.TryGetValue(manualTarget, out var manualList))
                    plan._patches[manualTarget] = manualList = [];

                manualList.Add(new PlannedPatch(entry.File, true));
                continue;
            }

            plan._unmatchedPatches.Add(new UnmatchedPatch(entry, key));
        }

        return plan;
    }

    public void LogSummary(Action<string, LogLevel>? log, string? label = null)
    {
        if (log is null)
            return;

        string suffix = string.IsNullOrEmpty(label) ? string.Empty : $" ({label})";

        if (!HasAnyMatch)
        {
            log(Loc.S.LogPatchTargetMissing + suffix, LogLevel.Error);
            return;
        }

        if (OverwriteCount > 0)
            log(Loc.S.LogPlanReplaceDone(OverwriteCount, suffix), LogLevel.Ok);

        if (PatchedCount > 0)
            log(Loc.S.LogPlanPatchDone(PatchedCount, suffix), LogLevel.Ok);

        if (LooseCandidateCount > 0)
            log(Loc.S.LogPlanLooseCandidates(LooseCandidateCount, suffix), LogLevel.Info);
    }

    public static bool MatchesLoosely(PatchFileEntry entry, IEnumerable<string> targetPaths, PatchMatchOptions? options = null)
    {
        string stripped = options?.StripName(entry.BaseName) ?? entry.BaseName;

        bool nameHit = targetPaths.Any(t => NameMatches(FileNameOf(t), entry.BaseName) || NameMatches(FileNameOf(t), stripped));

        return nameHit && PatchMatchPolicy.AllowsLooseNameMatch(DetectFormat(entry.File));
    }

    private static PatchFormat DetectFormat(PatchFileRef file)
    {
        byte[] header = new byte[8];
        using var stream = file.OpenRead();
        int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        return UniversalPatcher.DetectFormat(header.AsSpan(0, read));
    }

    private static bool NameMatches(string targetFileName, string patchBaseName) => targetFileName.Contains(patchBaseName, StringComparison.OrdinalIgnoreCase) || patchBaseName.Contains(targetFileName, StringComparison.OrdinalIgnoreCase);

    private static string FileNameOf(string path)
    {
        int idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}