using PatchedContent = Patch.Core.Models.PatchedContent;

namespace Vita.Core.Services;

internal sealed class StreamedPatchResult(PatchedContent content, string? tempPath) : IDisposable
{
    public PatchedContent Content { get; } = content;

    public void Dispose()
    {
        if (tempPath == null)
            return;

        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch { }
    }
}
