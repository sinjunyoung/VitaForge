using Vita.Core.Models;

namespace VitaForge.Core.Models.PSVita;

public sealed class PatchCatalogItem
{
    public const string DirectType = "Patch";

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

    public string DirectLink { get; init; } = string.Empty;

    public string LastModified { get; init; } = string.Empty;

    public string PatchType { get; init; } = DirectType;

    public string InstallType { get; init; } = AllInstallType;

    public bool Enabled { get; init; } = true;

    public bool IsDirect => PatchType.Equals(DirectType, StringComparison.OrdinalIgnoreCase);

    public bool IsSelectable => Enabled && IsDirect && !string.IsNullOrWhiteSpace(DirectLink);

    public bool IsConsoleOnly => InstallType.Equals(ConsoleInstallType, StringComparison.OrdinalIgnoreCase);

    public bool IsEmulatorOnly => InstallType.Equals(EmulatorInstallType, StringComparison.OrdinalIgnoreCase);

    public bool Supports(OutputTarget target) => target == OutputTarget.Emu ? !IsConsoleOnly : !IsEmulatorOnly;
}