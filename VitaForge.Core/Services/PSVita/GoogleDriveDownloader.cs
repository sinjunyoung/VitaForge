using Common.Localization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace VitaForge.Core.Services.PSVita;

public static partial class GoogleDriveDownloader
{
    [GeneratedRegex(@"<form[^>]*id=""download-form""[^>]*action=""(?<action>[^""]+)""", RegexOptions.Singleline)]
    private static partial Regex FormRegex();

    [GeneratedRegex(@"<input[^>]*type=""hidden""[^>]*name=""(?<name>[^""]+)""[^>]*value=""(?<value>[^""]*)""", RegexOptions.Singleline)]
    private static partial Regex HiddenInputRegex();

    public static async Task DownloadAsync(string fileId, string destinationPath, Action<long> onBytes, Action<long>? onTotal, CancellationToken ct)
    {
        string url = $"https://drive.usercontent.google.com/download?id={Uri.EscapeDataString(fileId)}&export=download&confirm=t";

        for (int attempt = 0; attempt < 3; attempt++)
        {
            using var response = await StreamDownloader.GetAsync(GoogleDriveHttp.Client, url, ct);

            if (!response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType != "text/html")
                throw new HttpRequestException(Loc.S.ErrPsvPatchDriveBlocked);

            if (response.Content.Headers.ContentType?.MediaType != "text/html")
            {
                if (response.Content.Headers.ContentLength is { } total)
                    onTotal?.Invoke(total);

                await using var source = await response.Content.ReadAsStreamAsync(ct);

                await StreamDownloader.CopyAsync(source, destinationPath, null, onBytes, null, ct);

                return;
            }

            string html = await response.Content.ReadAsStringAsync(ct);

            url = BuildConfirmUrl(html) ?? throw new HttpRequestException(Loc.S.ErrPsvPatchDriveBlocked);
        }

        throw new HttpRequestException(Loc.S.ErrPsvPatchDriveBlocked);
    }

    private static string? BuildConfirmUrl(string html)
    {
        var form = FormRegex().Match(html);

        if (!form.Success)
            return null;

        var query = new StringBuilder();

        foreach (Match input in HiddenInputRegex().Matches(html))
        {
            if (query.Length > 0)
                query.Append('&');

            query.Append(Uri.EscapeDataString(WebUtility.HtmlDecode(input.Groups["name"].Value)));
            query.Append('=');
            query.Append(Uri.EscapeDataString(WebUtility.HtmlDecode(input.Groups["value"].Value)));
        }

        string action = WebUtility.HtmlDecode(form.Groups["action"].Value);

        return query.Length == 0 ? action : $"{action}{(action.Contains('?') ? '&' : '?')}{query}";
    }
}