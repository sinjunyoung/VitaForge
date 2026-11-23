using Common.Models;
using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal interface ITitleRule
{
    string TitleId { get; }

    IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files) => files;

    byte[] RewriteSource(string relativePath, byte[] sourceBytes, OwnerContext owner) => sourceBytes;

    IEnumerable<ExpandedFile> ExpandOutput(string relativePath, byte[] outputBytes, Action<string, LogLevel> log) => [];

    IEnumerable<ExtraPatchFile> SelectExtraFiles(IReadOnlyList<PatchContainerFile> files) => [];
}