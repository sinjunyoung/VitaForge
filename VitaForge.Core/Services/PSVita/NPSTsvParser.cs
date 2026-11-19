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
                    ZRif = row["zRIF"],
                    ContentId = contentId,
                    IsDlc = isDlc
                };

                if (!dictionary.TryGetValue(titleId, out var group))
                {
                    group = new NPSGroup { TitleId = titleId };
                    dictionary[titleId] = group;
                }

                if (!isDlc)
                    group.App = item;
                else
                    group.Dlcs.Add(item);
            }
        }
    }
}
