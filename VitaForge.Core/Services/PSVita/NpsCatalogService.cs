using Common.Localization;
using VitaForge.Core.Models.PSVita;
using System.IO;

namespace VitaForge.Core.Services.PSVita;

public sealed class NpsCatalogService
{
    private const string GamesFileName = "PSV_GAMES.tsv";
    private const string UpdatesFileName = "PSV_UPDATES.tsv";
    private const string DlcsFileName = "PSV_DLCS.tsv";
    private const string KoreanNamesFileName = "PSV_NAMES_KO.tsv";

    private static readonly Lazy<NpsCatalogService> _instance = new(() => new NpsCatalogService());

    public static NpsCatalogService Instance => _instance.Value;

    private List<NpsCatalogGame> _games = [];
    private Dictionary<string, NpsCatalogGame> _byTitleId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Regions { get; private set; } = [];

    public bool IsLoaded { get; private set; }

    private NpsCatalogService() { }

    private readonly Lock _loadLock = new();
    private Task? _loadTask;

    public Task LoadAsync()
    {
        if (IsLoaded)
            return Task.CompletedTask;

        lock (_loadLock)
        {
            if (_loadTask is { IsFaulted: false })
                return _loadTask;

            return _loadTask = Task.Run(Load);
        }
    }

    public NpsCatalogGame? FindGame(string? titleId) => !string.IsNullOrEmpty(titleId) && _byTitleId.TryGetValue(titleId, out var game) ? game : null;

    public List<NpsCatalogGame> Search(string? titleId, string? region, string? name)
    {
        IEnumerable<NpsCatalogGame> query = _games;

        if (!string.IsNullOrWhiteSpace(titleId))
        {
            string keyword = titleId.Trim();

            query = query.Where(g => g.Game.TitleId.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(g => string.Equals(g.Game.Region, region, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(name))
        {
            string keyword = name.Trim();
            string compactKeyword = Compact(keyword);

            query = query.Where(g => MatchesName(g.Game, keyword, compactKeyword));
        }

        return [.. query];
    }

    private void Load()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string gamesPath = Path.Combine(baseDir, GamesFileName);
        string updatesPath = Path.Combine(baseDir, UpdatesFileName);
        string dlcsPath = Path.Combine(baseDir, DlcsFileName);

        if (!File.Exists(gamesPath))
            throw new FileNotFoundException(Loc.S.ErrNpsTsvNotFound(GamesFileName), gamesPath);

        var games = LoadGames(gamesPath);

        if (File.Exists(updatesPath))
            AttachUpdates(updatesPath, games);

        if (File.Exists(dlcsPath))
            AttachDlcs(dlcsPath, games);

        string koreanNamesPath = Path.Combine(baseDir, KoreanNamesFileName);

        if (File.Exists(koreanNamesPath))
            AttachKoreanNames(koreanNamesPath, games);

        _games = [.. games.Values.OrderBy(g => g.Game.Name, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Game.TitleId, StringComparer.OrdinalIgnoreCase)];
        _byTitleId = games;
        Regions = [.. _games.Select(g => g.Game.Region).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r, StringComparer.OrdinalIgnoreCase)];
        IsLoaded = true;
    }

    private static Dictionary<string, NpsCatalogGame> LoadGames(string path)
    {
        var games = new Dictionary<string, NpsCatalogGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in NpsTsvReader.Read(path, "Title ID", "PKG direct link"))
        {
            var item = CreateItem(row, NpsEntryKind.Game);

            if (item == null)
                continue;

            if (games.TryGetValue(item.TitleId, out var existing))
            {
                if (!existing.Game.HasKey && item.HasKey)
                    games[item.TitleId] = new NpsCatalogGame(item);

                continue;
            }

            games[item.TitleId] = new NpsCatalogGame(item);
        }

        return games;
    }

    private static void AttachUpdates(string path, Dictionary<string, NpsCatalogGame> games)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in NpsTsvReader.Read(path, "Title ID", "PKG direct link", "Update Version"))
        {
            var item = CreateItem(row, NpsEntryKind.Update);

            if (item == null || !games.TryGetValue(item.TitleId, out var game))
                continue;

            if (!seen.Add($"{item.TitleId}/{(string.IsNullOrEmpty(item.Version) ? item.Url : item.Version)}"))
                continue;

            game.Updates.Add(item);
        }

        foreach (var game in games.Values)
            game.Updates.Sort((a, b) => CompareVersion(a.Version, b.Version));
    }

    private static void AttachDlcs(string path, Dictionary<string, NpsCatalogGame> games)
    {
        var picked = new Dictionary<string, NpsCatalogItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in NpsTsvReader.Read(path, "Title ID", "PKG direct link", "zRIF", "Content ID"))
        {
            var item = CreateItem(row, NpsEntryKind.Dlc);

            if (item == null || !games.ContainsKey(item.TitleId))
                continue;

            string key = $"{item.TitleId}/{(string.IsNullOrEmpty(item.ContentId) ? item.Url : item.ContentId)}";

            if (picked.TryGetValue(key, out var existing) && (existing.HasKey || !item.HasKey))
                continue;

            picked[key] = item;
        }

        foreach (var item in picked.Values.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.ContentId, StringComparer.OrdinalIgnoreCase))
            games[item.TitleId].Dlcs.Add(item);
    }

    private static void AttachKoreanNames(string path, Dictionary<string, NpsCatalogGame> games)
    {
        foreach (var row in NpsTsvReader.Read(path, "Title ID", "Korean Name"))
        {
            if (!games.TryGetValue(row["Title ID"], out var game))
                continue;

            game.Game.KoreanNames = [.. row["Korean Name"].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }
    }

    private static bool MatchesName(NpsCatalogItem item, string keyword, string compactKeyword)
    {
        if (Contains(item.Name, keyword, compactKeyword) || Contains(item.OriginalName, keyword, compactKeyword))
            return true;

        foreach (string koreanName in item.KoreanNames)
        {
            if (Contains(koreanName, keyword, compactKeyword))
                return true;
        }

        return false;
    }

    private static bool Contains(string source, string keyword, string compactKeyword)
    {
        if (string.IsNullOrEmpty(source))
            return false;

        if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;

        return compactKeyword.Length > 0 && Compact(source).Contains(compactKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string Compact(string value) => string.Concat(value.Where(char.IsLetterOrDigit));

    private static NpsCatalogItem? CreateItem(NpsTsvRow row, NpsEntryKind kind)
    {
        string titleId = row["Title ID"];
        string url = row["PKG direct link"];

        if (string.IsNullOrEmpty(titleId) || !IsDownloadableUrl(url))
            return null;

        string zRif = row["zRIF"];
        bool hasKey = !string.IsNullOrEmpty(zRif) && !zRif.Equals("MISSING", StringComparison.OrdinalIgnoreCase) && !zRif.Equals("NOT REQUIRED", StringComparison.OrdinalIgnoreCase);

        long.TryParse(row["File Size"], out long size);

        return new NpsCatalogItem
        {
            Kind = kind,
            TitleId = titleId,
            Region = row["Region"],
            Name = row["Name"],
            OriginalName = row["Original Name"],
            Url = url,
            ZRif = hasKey ? zRif : null,
            ContentId = row["Content ID"],
            Version = kind == NpsEntryKind.Update ? row["Update Version"] : row["App Version"],
            Size = size,
            Sha256 = row["SHA256"]
        };
    }

    private static bool IsDownloadableUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static int CompareVersion(string? a, string? b)
    {
        if (Version.TryParse(Normalize(a), out var va) && Version.TryParse(Normalize(b), out var vb))
            return va.CompareTo(vb);

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? version) => string.IsNullOrWhiteSpace(version) ? "0.0" : version.Contains('.') ? version.Trim() : version.Trim() + ".0";
}
