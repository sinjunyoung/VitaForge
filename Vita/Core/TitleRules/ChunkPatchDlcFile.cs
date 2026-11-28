using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed record ChunkPatchDlcFile(string CpkName, string RelativePath, PatchAppEntry Entry);