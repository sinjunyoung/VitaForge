namespace Vita.Core.TitleRules;

internal readonly record struct GenerationResult(int Candidates, int Succeeded)
{
    public static GenerationResult operator +(GenerationResult left, GenerationResult right) => new(left.Candidates + right.Candidates, left.Succeeded + right.Succeeded);
}
