using Common.Localization;
using Common.Services;
using Vita.Core.TitleRules;

namespace Vita.Core.Services;

internal sealed class ExpansionProgress
{
    private readonly ProgressReporter? _stage;

    public ExpansionProgress(ProgressReporter reporter, string titleId, string relativePath, byte[] outputBytes)
    {
        long total = TitleRuleRegistry.EstimateExpandedSize(titleId, relativePath, outputBytes);

        if (total <= 0)
        {
            Reporter = reporter;

            return;
        }

        _stage = reporter.CreateStage(Loc.S.StatusUnpackingPatchedArchive, total);

        Reporter = _stage;

        _stage.ForceReport();
    }

    public ProgressReporter Reporter { get; }

    public long SizeOf(ExpandedFile file) => _stage is null ? 0 : file.Data.Length;

    public void Complete() => _stage?.ForceReport();
}
