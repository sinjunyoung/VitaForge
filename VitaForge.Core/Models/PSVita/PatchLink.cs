using System.IO;

namespace VitaForge.Core.Models.PSVita;

public sealed record PatchLink(string Url, string FileName, int Index)
{
    public bool HasSelector => FileName.Length > 0 || Index > 0;

    public string LocalFileName => FileName.Length > 0 ? FileName : InferFileName(Url);

    public bool HasValidFileName => LocalFileName.Length > 0 && LocalFileName == Path.GetFileName(LocalFileName) && LocalFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string InferFileName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return string.Empty;

        string name = Uri.UnescapeDataString(Path.GetFileName(uri.LocalPath));

        return name.Contains('.') ? name : string.Empty;
    }
}
