namespace Vita.Core.TitleRules;

internal sealed record EmbeddedPayloadRecipe(string ResourceMarker, string? AssemblyName, IReadOnlyList<EmbeddedPayloadEntry> Entries);
