using Common.Localization;
using System.IO;

namespace VitaForge.Core.Services.PSVita;

public sealed class NpsTsvRow(Dictionary<string, int> index, string[] parts)
{
    public string this[string column] => index.TryGetValue(column, out int i) && i < parts.Length ? parts[i].Trim() : string.Empty;
}

public static class NpsTsvReader
{
    public static IEnumerable<NpsTsvRow> Read(string filePath, params string[] requiredColumns)
    {
        using var reader = new StreamReader(filePath);
        string? headerLine = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(headerLine))
            yield break;

        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] headers = headerLine.Split('\t');

        for (int i = 0; i < headers.Length; i++)
            index.TryAdd(headers[i].Trim(), i);

        foreach (string column in requiredColumns)
        {
            if (!index.ContainsKey(column))
                throw new Exception(Loc.S.ErrRequiredHeaderNotFound(filePath));
        }

        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            yield return new NpsTsvRow(index, line.Split('\t'));
        }
    }
}
