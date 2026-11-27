using System.Text.Json;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal static class ChunkPatchReader
{
    private const string DefinitionSuffix = ".patch.json";

    private static readonly string[] SupportedFormats = ["muramasa-kor-binary-patch-v1"];

    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static IReadOnlyList<ChunkPatchDefinition> Read(IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
        => [.. files
            .Where(f => f.Path.EndsWith(DefinitionSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .Select(f => TryParse(f, accessor))
            .OfType<ChunkPatchDefinition>()
            .DistinctBy(d => d.SourcePath, StringComparer.OrdinalIgnoreCase)];

    private static ChunkPatchDefinition? TryParse(PatchContainerFile file, ISourceAccessor accessor)
    {
        ChunkPatchDefinition? definition;

        try
        {
            using var stream = new MemoryStream(accessor.ReadAllBytes(file.Raw), writable: false);

            definition = JsonSerializer.Deserialize<ChunkPatchDefinition>(stream, Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            return null;
        }

        if (definition is null || !SupportedFormats.Contains(definition.Format, StringComparer.Ordinal) || string.IsNullOrEmpty(definition.SourceName))
            return null;

        int slash = file.Path.LastIndexOf('/');

        definition.Root = slash < 0 ? string.Empty : file.Path[..(slash + 1)];

        return definition;
    }
}