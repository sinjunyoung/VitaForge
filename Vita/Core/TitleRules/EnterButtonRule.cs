using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed class EnterButtonRule(string titleId, string prefFolder) : ITitleRule
{
    private const string ImageFolder = "/textures/kr/ui_xbutton/";

    public string TitleId { get; } = titleId;

    public IEnumerable<ExtraPatchFile> SelectExtraFiles(IReadOnlyList<PatchContainerFile> files)
    {
        if (TitleRuleOptions.EnterButton != EnterButton.Cross)
            yield break;

        foreach (var file in files)
        {
            if (file.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && $"/{file.Path}".Contains(ImageFolder, StringComparison.OrdinalIgnoreCase))
                yield return new ExtraPatchFile(file.Raw, $"{prefFolder}/{TitleId}/{Path.GetFileName(file.Path)}");
        }
    }
}