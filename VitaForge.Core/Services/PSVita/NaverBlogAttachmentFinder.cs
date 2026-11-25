using HtmlAgilityPack;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class NaverBlogAttachmentFinder
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private const string BlogReferrer = "https://blog.naver.com/";
    private const int TimeoutSeconds = 30;

    private static readonly Regex DownloadUrlRegex = new(@"https?://download\.blog\.naver\.com/open/[^\s""'<>\\]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static HttpClient Client { get; } = CreateClient();

    public static async Task<List<NaverBlogAttachment>> FindAllAsync(string pageUrl, CancellationToken ct)
    {
        var post = ParsePost(pageUrl);

        if (post == null)
            return [];

        string[] candidates =
        [
            $"https://blog.naver.com/PostView.naver?blogId={post.Value.BlogId}&logNo={post.Value.LogNo}&redirect=Dlog&widgetTypeCall=true&directAccess=false",
            $"https://m.blog.naver.com/PostView.naver?blogId={post.Value.BlogId}&logNo={post.Value.LogNo}"
        ];

        foreach (string candidate in candidates)
        {
            var attachments = Extract(await GetHtmlAsync(candidate, ct));

            if (attachments.Count > 0)
                return attachments;
        }

        return [];
    }

    private static async Task<string> GetHtmlAsync(string url, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        using var response = await Client.GetAsync(url, cts.Token);

        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cts.Token) : string.Empty;
    }

    private static List<NaverBlogAttachment> Extract(string html)
    {
        string text = HtmlEntity.DeEntitize(html).Replace("\\/", "/").Replace("\\u0026", "&").Replace("\\u002F", "/");
        var attachments = new List<NaverBlogAttachment>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in DownloadUrlRegex.Matches(text))
        {
            string name = GetUrlFileName(match.Value);

            if (name.Length > 0 && seen.Add(name))
                attachments.Add(new NaverBlogAttachment(match.Value, name));
        }

        return attachments;
    }

    private static (string BlogId, string LogNo)? ParsePost(string pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
            return null;

        var query = HttpUtility.ParseQueryString(uri.Query);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        string blogId = query["blogId"] ?? (segments.Length > 0 && !segments[0].EndsWith(".naver", StringComparison.OrdinalIgnoreCase) ? segments[0] : string.Empty);
        string logNo = query["logNo"] ?? (segments.Length > 1 ? segments[1] : string.Empty);

        if (blogId.Length == 0 || logNo.Length == 0 || !logNo.All(char.IsDigit))
            return null;

        return (blogId, logNo);
    }

    private static string GetUrlFileName(string url)
    {
        try
        {
            string name = Uri.UnescapeDataString(Path.GetFileName(new Uri(url).LocalPath));

            return name.Contains('.') ? name : string.Empty;
        }
        catch (UriFormatException)
        {
            return string.Empty;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true }) { Timeout = TimeSpan.FromMinutes(30) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Referrer = new Uri(BlogReferrer);

        return client;
    }
}