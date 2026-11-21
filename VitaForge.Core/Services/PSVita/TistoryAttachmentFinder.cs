using HtmlAgilityPack;
using System.IO;
using System.Net.Http;
using System.Xml;

namespace VitaForge.Core.Services.PSVita;

public static class TistoryAttachmentFinder
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private const string TargetDivXPath = "//div[contains(@class, 'tt_article_useless_p_margin') and contains(@class, 'contents_style')]";
    private const int TimeoutSeconds = 30;

    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    private static readonly HashSet<string> AttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".alz", ".egg", ".gz", ".tar",
        ".iso", ".bin", ".cue", ".img", ".chd", ".gdi",
        ".nsp", ".xci", ".nsz", ".xcz", ".3ds", ".cia", ".cci",
        ".gba", ".gbc", ".gb", ".nes", ".sfc", ".smc", ".n64", ".z64", ".pce", ".md", ".sms"
    };

    public static HttpClient Client { get; } = CreateClient();

    public static async Task<string?> FindFirstAsync(string pageUrl, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        using var response = await Client.GetAsync(pageUrl, cts.Token);

        response.EnsureSuccessStatusCode();

        var doc = new HtmlDocument();

        doc.LoadHtml(await response.Content.ReadAsStringAsync(cts.Token));

        var searchNode = doc.DocumentNode.SelectSingleNode(TargetDivXPath) ?? doc.DocumentNode;
        var baseUri = new Uri(pageUrl);
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in searchNode.DescendantsAndSelf())
        {
            foreach (string raw in GetCandidates(node))
            {
                string? url = Resolve(baseUri, raw);

                if (url == null || !IsAttachmentUrl(url) || IsExcluded(url))
                    continue;

                if (seen.Add(url))
                    urls.Add(url);
            }
        }

        return urls.OrderByDescending(GetUrlPriority).FirstOrDefault();
    }

    private static IEnumerable<string> GetCandidates(HtmlNode node)
    {
        if (node.Name == "a")
            yield return HtmlEntity.DeEntitize(node.GetAttributeValue("href", string.Empty)).Trim();

        yield return HtmlEntity.DeEntitize(node.GetAttributeValue("data-url", string.Empty)).Trim();
    }

    private static string? Resolve(Uri baseUri, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "#" || raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute))
            return absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps ? raw : null;

        return Uri.TryCreate(baseUri, raw, out var resolved) ? resolved.AbsoluteUri : null;
    }

    private static bool IsExcluded(string url)
    {
        try
        {
            string ext = Path.GetExtension(new Uri(url).LocalPath);

            return !string.IsNullOrEmpty(ext) && ExcludedExtensions.Contains(ext);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool IsAttachmentUrl(string url)
    {
        string ext = Path.GetExtension(url.Split('?', '#')[0]);

        if (url.Contains("attachment"))
            return true;

        if (url.Contains("kakaocdn.net") && !url.EndsWith(".html") && !url.EndsWith('/'))
            return true;

        if ((url.Contains("daum.net") || url.Contains("daumcdn.net")) &&
            (url.Contains("cfile") || url.Contains("attach") || url.Contains("original") || url.Contains("tistoryfile") || AttachmentExtensions.Contains(ext)))
            return true;

        return AttachmentExtensions.Contains(ext);
    }

    private static int GetUrlPriority(string url)
    {
        if (url.Contains("kakaocdn.net"))
            return 3;

        if (url.Contains("daum.net") || url.Contains("daumcdn.net"))
            return 2;

        if (url.Contains("tistory"))
            return 1;

        return 0;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromMinutes(30) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        return client;
    }
}