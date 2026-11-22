namespace Vita.Core.Models;

public sealed record PatchTargetRequest(string TitleId, ContentCategory Category, string? ContentIdSuffix, IReadOnlyList<string> PatchFileNames, IReadOnlyList<string> CandidatePaths);