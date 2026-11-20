using System.Text.RegularExpressions;

namespace VitaForge.Core.Services.PSVita;

public static partial class GoogleDriveLink
{
    [GeneratedRegex(@"drive\.google\.com/(?:drive/(?:u/\d+/)?(?:mobile/)?folders/|folderview\?id=)(?<id>[\w-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FolderRegex();

    [GeneratedRegex(@"(?:drive\.google\.com/file/d/|drive\.google\.com/(?:open|uc)\?(?:[^#\s]*&)?id=|docs\.google\.com/uc\?(?:[^#\s]*&)?id=)(?<id>[\w-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileRegex();

    public static bool TryGetFolderId(string url, out string id) => TryGet(FolderRegex(), url, out id);

    public static bool TryGetFileId(string url, out string id) => TryGet(FileRegex(), url, out id);

    private static bool TryGet(Regex regex, string url, out string id)
    {
        var match = regex.Match(url ?? string.Empty);

        id = match.Success ? match.Groups["id"].Value : string.Empty;

        return match.Success;
    }
}