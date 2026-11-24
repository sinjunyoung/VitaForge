using Common.Localization;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public static class PatchFileSelector
{
    public static T Select<T>(IReadOnlyList<T> items, Func<T, IEnumerable<string>> namesOf, PatchLink patchLink)
    {
        if (patchLink.FileName.Length > 0)
        {
            foreach (var item in items)
            {
                if (namesOf(item).Any(n => n.Equals(patchLink.FileName, StringComparison.OrdinalIgnoreCase)))
                    return item;
            }

            throw new InvalidOperationException(Loc.S.ErrPsvPatchFileNotFound(patchLink.FileName));
        }

        if (patchLink.Index < 1 || patchLink.Index > items.Count)
            throw new InvalidOperationException(Loc.S.ErrPsvPatchIndexOutOfRange(patchLink.Index, items.Count));

        return items[patchLink.Index - 1];
    }
}
