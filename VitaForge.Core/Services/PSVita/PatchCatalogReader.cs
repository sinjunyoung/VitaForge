using System.IO;
using System.Text.Json;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class PatchCatalogReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static List<PatchCatalogItem> Read(string path)
    {
        using var stream = File.OpenRead(path);

        var document = JsonSerializer.Deserialize<CatalogDocument>(stream, Options);
        var items = new List<PatchCatalogItem>();

        if (document?.Patches is null)
            return items;

        foreach (var entry in document.Patches)
        {
            string id = Clean(entry.TitleId).ToUpperInvariant();

            if (id.Length == 0)
                continue;

            items.Add(new PatchCatalogItem
            {
                TitleId = id,
                Region = Clean(entry.Region),
                Name = Clean(entry.Name),
                Version = Clean(entry.Version),
                RequiredUpdateVersion = Clean(entry.RequiredUpdateVersion),
                User = Clean(entry.User),
                ReleaseLink = Clean(entry.ReleaseLink),
                LastModified = Clean(entry.Date),
                PatchType = Or(entry.PatchType, PatchCatalogItem.DirectType),
                InstallType = Or(entry.InstallType, PatchCatalogItem.AllInstallType),
                LinkMode = Or(entry.LinkMode, PatchCatalogItem.IndividualLinkMode),
                Enabled = entry.Enabled ?? true,
                Links = [.. (entry.Links ?? []).Select(ToLink).Where(l => l.Url.Length > 0)]
            });
        }

        return items;
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string Or(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static PatchLink ToLink(CatalogLink link) => new(Clean(link.Url), Clean(link.FileName), link.Index ?? 0);

    private sealed class CatalogDocument
    {
        public List<CatalogEntry>? Patches { get; set; }
    }

    private sealed class CatalogEntry
    {
        public string? TitleId { get; set; }

        public string? Region { get; set; }

        public string? Name { get; set; }

        public string? Version { get; set; }

        public string? RequiredUpdateVersion { get; set; }

        public string? User { get; set; }

        public string? ReleaseLink { get; set; }

        public string? Date { get; set; }

        public string? PatchType { get; set; }

        public string? InstallType { get; set; }

        public string? LinkMode { get; set; }

        public bool? Enabled { get; set; }

        public List<CatalogLink>? Links { get; set; }
    }

    private sealed class CatalogLink
    {
        public string? Url { get; set; }

        public string? FileName { get; set; }

        public int? Index { get; set; }
    }
}
