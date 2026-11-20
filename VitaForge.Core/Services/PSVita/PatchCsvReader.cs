using VitaForge.Core.Models.PSVita;
using System.IO;
using System.Text;

namespace VitaForge.Core.Services.PSVita;

public static class PatchCsvReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    static PatchCsvReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static List<PatchCatalogItem> Read(string path)
    {
        var rows = Parse(Decode(File.ReadAllBytes(path)));
        var items = new List<PatchCatalogItem>();

        if (rows.Count == 0)
            return items;

        var header = rows[0].Select(h => h.Trim()).ToList();
        int titleId = IndexOf(header, "Title ID");
        int region = IndexOf(header, "Region");
        int name = IndexOf(header, "Name");
        int version = IndexOf(header, "Version");
        int requiredUpdate = IndexOf(header, "Required Update VERSION", "Required FW VERSION");
        int user = IndexOf(header, "User");
        int releaseLink = IndexOf(header, "release link");
        int directLink = IndexOf(header, "direct link");
        int lastModified = IndexOf(header, "Last Modification Date");

        foreach (var row in rows.Skip(1))
        {
            string id = Get(row, titleId).ToUpperInvariant();
            string link = Get(row, directLink);

            if (id.Length == 0 || link.Length == 0)
                continue;

            items.Add(new PatchCatalogItem
            {
                TitleId = id,
                Region = Get(row, region),
                Name = Get(row, name),
                Version = Get(row, version),
                RequiredUpdateVersion = Get(row, requiredUpdate),
                User = Get(row, user),
                ReleaseLink = Get(row, releaseLink),
                DirectLink = link,
                LastModified = Get(row, lastModified)
            });
        }

        return items;
    }

    private static int IndexOf(List<string> header, params string[] names)
    {
        foreach (string name in names)
        {
            int index = header.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
                return index;
        }

        return -1;
    }

    private static string Get(List<string> row, int index) => index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(949).GetString(bytes);
        }
    }

    private static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c != '"')
                    field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                    quoted = false;

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    AddRow(rows, row);
                    row = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            AddRow(rows, row);
        }

        return rows;
    }

    private static void AddRow(List<List<string>> rows, List<string> row)
    {
        if (row.Any(f => !string.IsNullOrWhiteSpace(f)))
            rows.Add(row);
    }
}