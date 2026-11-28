using System.Text.Json;

namespace Vita.Core.Fonts;

internal static class AsciiOverflowMap
{
    private const int FirstCell = 960;
    private const int LastCell = 1024;

    private static readonly HashSet<int> HalfWidthCodes = [0x20, 0x2E];

    public static HashSet<int> ReadUsedCodes(byte[] messagesJson)
    {
        var used = new HashSet<int>();

        using var document = JsonDocument.Parse(messagesJson);

        foreach (var section in document.RootElement.EnumerateObject())
        {
            if (section.Value.ValueKind != JsonValueKind.Object || !section.Value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var message in messages.EnumerateArray())
            {
                if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("ko", out var text) || text.ValueKind != JsonValueKind.String)
                    continue;

                foreach (char c in text.GetString()!)
                {
                    if (c >= 0x21 && c <= 0x7E)
                        used.Add(c);
                }
            }
        }

        return used;
    }

    public static IReadOnlyList<(char Character, int Local)> BuildCells(KoreanSjisMapping mapping, IReadOnlySet<int> usedCodes)
    {
        var occupied = mapping.OccupiedLocals(SjisCells.PageZeroBase);
        var free = Enumerable.Range(FirstCell, LastCell - FirstCell).Where(c => !occupied.Contains(c)).ToList();
        var codes = Enumerable.Range(0x21, 0x7E - 0x21 + 1).Where(c => !HalfWidthCodes.Contains(c)).ToList();
        var ordered = codes.Where(usedCodes.Contains).Concat(codes.Where(c => !usedCodes.Contains(c)));
        var result = new List<(char, int)>();
        int next = 0;

        foreach (int code in ordered)
        {
            if (next >= free.Count)
                continue;

            result.Add(((char)code, free[next]));
            next++;
        }

        return result;
    }
}