using Vita.Core.Models;

namespace VitaForge.Core.Models.PSVita;

public sealed class PatchCatalogItem
{
    public const string DirectType = "Patch";

    public const string IndividualLinkMode = "Individual";

    public const string SplitLinkMode = "Split";

    public const string InstallerType = "EXE";

    public const string ConsoleInstallType = "Console";

    public const string EmulatorInstallType = "Emulator";

    public const string AllInstallType = "All";

    public string TitleId { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string RequiredUpdateVersion { get; init; } = string.Empty;

    public string User { get; init; } = string.Empty;

    public string ReleaseLink { get; init; } = string.Empty;

    public IReadOnlyList<PatchLink> Links { get; init; } = [];

    public string LinkMode { get; init; } = IndividualLinkMode;

    public string LastModified { get; init; } = string.Empty;

    public string PatchType { get; init; } = DirectType;

    public string InstallType { get; init; } = AllInstallType;

    public bool Enabled { get; init; } = true;

    public bool IsDirect => PatchType.Equals(DirectType, StringComparison.OrdinalIgnoreCase);

    public bool IsSplit => LinkMode.Equals(SplitLinkMode, StringComparison.OrdinalIgnoreCase);

    public bool IsSelectable => Enabled && IsDirect && Links.Count > 0;

    public bool IsConsoleOnly => InstallType.Equals(ConsoleInstallType, StringComparison.OrdinalIgnoreCase);

    public bool IsEmulatorOnly => InstallType.Equals(EmulatorInstallType, StringComparison.OrdinalIgnoreCase);

    public bool Supports(OutputTarget target) => target == OutputTarget.Emu ? !IsConsoleOnly : !IsEmulatorOnly;
}