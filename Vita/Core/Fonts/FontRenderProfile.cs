namespace Vita.Core.Fonts;

internal sealed record FontRenderProfile(string FontKey, byte[] FontData, double KoreanPoints, double AsciiPoints, double Stroke);