using Common.Localization;
using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class PatchInstallPlanner
{
    public static PatchInstallPlan? Create(PatchCatalogItem patch, bool includeDlc, Action<string, LogLevel> log)
    {
        var game = NpsCatalogService.Instance.FindGame(patch.TitleId);

        if (game == null)
        {
            log(Loc.S.LogPsvPatchGameNotFound(patch.TitleId, patch.Name), LogLevel.Error);

            return null;
        }

        if (!game.Game.HasKey || string.IsNullOrWhiteSpace(game.Game.Url))
        {
            log(Loc.S.LogNpsKeyMissingSkipped(patch.TitleId, patch.Name), LogLevel.Error);

            return null;
        }

        var update = PickUpdate(game, patch, log);
        var dlcs = includeDlc ? game.Dlcs.Where(d => d.HasKey && !string.IsNullOrWhiteSpace(d.Url)).ToList() : [];

        return new PatchInstallPlan(patch, game.Game, update, dlcs);
    }

    private static NpsCatalogItem? PickUpdate(NpsCatalogGame game, PatchCatalogItem patch, Action<string, LogLevel> log)
    {
        string required = patch.RequiredUpdateVersion;

        if (string.IsNullOrWhiteSpace(required))
            return null;

        var candidates = game.Updates.Where(u => !string.IsNullOrWhiteSpace(u.Url)).ToList();

        if (candidates.Count == 0)
        {
            log(Loc.S.LogPsvPatchUpdateFallback(patch.TitleId, required, "-"), LogLevel.Info);

            return null;
        }

        var exact = candidates.FirstOrDefault(u => NpsCatalogService.CompareVersion(u.Version, required) == 0);

        if (exact != null)
            return exact;

        var versionComparer = Comparer<string>.Create(NpsCatalogService.CompareVersion);

        var fallback = candidates
            .Where(u => NpsCatalogService.CompareVersion(u.Version, required) > 0)
            .OrderBy(u => u.Version, versionComparer)
            .FirstOrDefault() ?? candidates.OrderByDescending(u => u.Version, versionComparer).First();

        log(Loc.S.LogPsvPatchUpdateFallback(patch.TitleId, required, fallback.Version), LogLevel.Info);

        return fallback;
    }
}