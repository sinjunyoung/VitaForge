using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static partial class GoogleDriveFolderLister
{
    private const string FolderMime = "application/vnd.google-apps.folder";

    [GeneratedRegex(@"window\['_DRIVE_ivd'\]\s*=\s*'(.*?)';", RegexOptions.Singleline)]
    private static partial Regex IvdRegex();

    [GeneratedRegex(@"<a href=""https://drive\.google\.com/(?<kind>file/d|drive/folders)/(?<id>[\w-]+)[^""]*""[^>]*>.*?<div class=""flip-entry-title"">(?<name>[^<]*)</div>", RegexOptions.Singleline)]
    private static partial Regex EmbeddedEntryRegex();

    public static async Task<List<GoogleDriveFile>> ListAsync(string folderId, CancellationToken ct)
    {
        var files = await TryAsync(() => ListFromFolderPageAsync(folderId, ct), ct);

        if (files.Count == 0)
            files = await TryAsync(() => ListFromEmbeddedViewAsync(folderId, ct), ct);

        return [.. files.Where(f => f.MimeType != FolderMime)];
    }

    private static async Task<List<GoogleDriveFile>> TryAsync(Func<Task<List<GoogleDriveFile>>> action, CancellationToken ct)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or ArgumentException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            return [];
        }
    }

    private static async Task<List<GoogleDriveFile>> ListFromFolderPageAsync(string folderId, CancellationToken ct)
    {
        string html = await GoogleDriveHttp.GetStringAsync($"https://drive.google.com/drive/folders/{folderId}?hl=en", ct);
        var match = IvdRegex().Match(html);

        if (!match.Success)
            return [];

        using var doc = JsonDocument.Parse(Regex.Unescape(match.Groups[1].Value));

        var root = doc.RootElement;
        var files = new List<GoogleDriveFile>();

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 || root[0].ValueKind != JsonValueKind.Array)
            return files;

        foreach (var child in root[0].EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Array || child.GetArrayLength() < 4)
                continue;

            string id = Text(child[0]);

            if (id.Length == 0)
                continue;

            files.Add(new GoogleDriveFile(id, Text(child[2]), Text(child[3])));
        }

        return files;
    }

    private static async Task<List<GoogleDriveFile>> ListFromEmbeddedViewAsync(string folderId, CancellationToken ct)
    {
        string html = await GoogleDriveHttp.GetStringAsync($"https://drive.google.com/embeddedfolderview?id={folderId}", ct);
        var files = new List<GoogleDriveFile>();

        foreach (Match match in EmbeddedEntryRegex().Matches(html))
        {
            string mime = match.Groups["kind"].Value == "file/d" ? "application/octet-stream" : FolderMime;

            files.Add(new GoogleDriveFile(match.Groups["id"].Value, WebUtility.HtmlDecode(match.Groups["name"].Value), mime));
        }

        return files;
    }

    private static string Text(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : string.Empty;
}