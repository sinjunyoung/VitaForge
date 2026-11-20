namespace Vita.Core.Models;

public sealed record PatchTargetRequest(string TitleId, ContentCategory Category, string? ContentIdSuffix, string PatchFileName, IReadOnlyList<string> CandidatePaths);
