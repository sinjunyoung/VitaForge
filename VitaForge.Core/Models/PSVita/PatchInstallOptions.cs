using Vita.Core.Models;

namespace VitaForge.Core.Models.PSVita;

public sealed record PatchInstallOptions(OutputTarget Target, string InstallRoot, bool IncludeDlc);