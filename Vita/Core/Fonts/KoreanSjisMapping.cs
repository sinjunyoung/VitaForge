using System.Text.Json;

namespace Vita.Core.Fonts;

internal sealed class KoreanSjisMapping
{
    private KoreanSjisMapping(IReadOnlyList<(string Character, int First, int Second)> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<(string Character, int First, int Second)> Entries { get; }

    public static KoreanSjisMapping Parse(byte[] json)
    {
        using var document = JsonDocument.Parse(json);

        var entries = new List<(string, int, int)>();

        foreach (var property in document.RootElement.GetProperty("korean_to_sjis").EnumerateObject())
            entries.Add((property.Name, property.Value[0].GetInt32(), property.Value[1].GetInt32()));

        return new KoreanSjisMapping(entries);
    }

    public HashSet<int> OccupiedLocals(int pageBase)
    {
        var result = new HashSet<int>();

        foreach (var (_, first, second) in Entries)
        {
            int local = SjisCells.ToCell(first, second) - pageBase;

            if (local >= 0 && local < 1024)
                result.Add(local);
        }

        return result;
    }
}