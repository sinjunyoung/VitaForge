using System.IO;

namespace VitaForge.Core.Models.PSVita;

public sealed record PatchLink(string Url, string FileName)
{
    public bool HasValidFileName => FileName.Length > 0 && FileName == Path.GetFileName(FileName) && FileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
