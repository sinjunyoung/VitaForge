namespace Vita.Core.TitleRules;

internal sealed record EmbeddedPayloadVariant(string Resource, string SourceSha256, string TargetSha256, long TargetSize);
