using VitaForge.Core.Models.PSVita;
using System.IO;

namespace VitaForge.Core.Services.PSVita
{
    public class NPSTsvParser
    {
        private static readonly Lazy<NPSTsvParser> _instance = new(() => new NPSTsvParser());

        public static NPSTsvParser Instance => _instance.Value;

        private readonly Dictionary<string, NPSGroup> _cachedData = new(StringComparer.OrdinalIgnoreCase);

        private NPSTsvParser() { }

        public void LoadData(string gamesTsvPath, string dlcsTsvPath)
        {
            _cachedData.Clear();

            if (File.Exists(gamesTsvPath))
                ParseTsvFile(gamesTsvPath, isDlc: false, _cachedData);

            if (File.Exists(dlcsTsvPath))
                ParseTsvFile(dlcsTsvPath, isDlc: true, _cachedData);
        }

        public bool TryGetGroup(string titleId, out NPSGroup group) => _cachedData.TryGetValue(titleId, out group!);

        private static string NormalizeZRif(string zRif)
        {
            if (zRif.Equals("MISSING", StringComparison.OrdinalIgnoreCase) || zRif.Equals("NOT REQUIRED", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return zRif;
        }

        private static void ParseTsvFile(string filePath, bool isDlc, Dictionary<string, NPSGroup> dictionary)
        {
            foreach (var row in NpsTsvReader.Read(filePath, "Title ID", "zRIF", "Content ID"))
            {
                string titleId = row["Title ID"];

                if (string.IsNullOrEmpty(titleId))
                    continue;

                string rawContentId = row["Content ID"];
                string contentId = rawContentId;
                int lastHyphenIdx = rawContentId.LastIndexOf('-');

                if (lastHyphenIdx >= 0 && lastHyphenIdx < rawContentId.Length - 1)
                    contentId = rawContentId[(lastHyphenIdx + 1)..];

                var item = new NPSItem
                {
                    TitleId = titleId,
                    ZRif = NormalizeZRif(row["zRIF"]),
                    ContentId = contentId,
                    IsDlc = isDlc
                };

                if (!dictionary.TryGetValue(titleId, out var group))
                {
                    group = new NPSGroup { TitleId = titleId };
                    dictionary[titleId] = group;
                }

                if (!isDlc)
                {
                    if (group.App == null || (string.IsNullOrEmpty(group.App.ZRif) && !string.IsNullOrEmpty(item.ZRif)))
                        group.App = item;
                }
                else
                {
                    int existing = group.Dlcs.FindIndex(d => string.Equals(d.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase));

                    if (existing < 0)
                        group.Dlcs.Add(item);
                    else if (string.IsNullOrEmpty(group.Dlcs[existing].ZRif) && !string.IsNullOrEmpty(item.ZRif))
                        group.Dlcs[existing] = item;
                }
            }
        }
    }
}
