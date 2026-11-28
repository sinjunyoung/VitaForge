using Vita.Core.Models;

namespace Vita.Core.TitleRules;

public static class TitleRuleOptions
{
    public static EnterButton EnterButton { get; set; } = EnterButton.Circle;

    public static bool SupportsEnterButton(string titleId) => TitleRuleRegistry.SupportsEnterButton(titleId);
}