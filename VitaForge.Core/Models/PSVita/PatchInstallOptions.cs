using Vita.Core.Models;

namespace VitaForge.Core.Models.PSVita;

public sealed record PatchInstallOptions(OutputTarget Target, PatchOutputMode Mode, string TargetPath, bool IncludeDlc);