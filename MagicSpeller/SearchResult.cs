using System.Drawing;

namespace MagicSpeller;

public record SearchResult
{
    public SearchResult(List<Field> Fields, List<Point> Path)
    {
        this.Fields = Fields;
        this.Path = Path;
        Points = CalculatePoints();
    }

    private static int GetCharPoints(char c) =>
        c switch
        {
            'A' => 1, 'B' => 4, 'C' => 5, 'D' => 3, 'E' => 1, 'F' => 5, 'G' => 3, 'H' => 4, 'I' => 1, 'J' => 7, 'K' => 6, 'L' => 3, 'M' => 4,
            'N' => 2, 'O' => 1, 'P' => 4, 'Q' => 8, 'R' => 2, 'S' => 2, 'T' => 2, 'U' => 4, 'V' => 5, 'W' => 5, 'X' => 7, 'Y' => 4, 'Z' => 8,
            _ => 0
        };

    private int CalculatePoints()
    {
        var points = 0;
        var wordFactor = 1;
        foreach (var field in Fields)
        {
            var fieldFactor = field.Special switch
            {
                SpecialField.DoubleLetter => 2,
                SpecialField.TripleLetter => 3,
                _ => 1
            };
            wordFactor *= field.Special switch
            {
                SpecialField.DoubleWord => 2,
                SpecialField.TripleWord => 3,
                _ => 1
            };
            points += GetCharPoints(field.Letter) * fieldFactor;
        }
        var lenghtBonus = Fields.Count >= 6 ? 10 : 0;
        return points * wordFactor + lenghtBonus;
    }

    public string Word => string.Concat(Fields.Select(field => field.Letter));

    public int Points { get; }

    public List<Field> Fields { get; }

    public List<Point> Path { get; }

    public override string ToString() => $"{Word} {Points}P {string.Join(", ", Path.Select(point => new Point(point.X + 1, point.Y + 1)))}";
}

public readonly record struct SwapInfo(Point Position, char OldChar, char NewChar);

public record SearchResultWithSwaps(List<Field> Fields, List<Point> Path, List<SwapInfo> Swaps) : SearchResult(Fields, Path)
{
    public override string ToString() => $"{base.ToString()} Swaps: {
        string.Join(", ", Swaps.Select(swap => (new Point(swap.Position.X + 1, swap.Position.Y + 1), swap.OldChar, swap.NewChar)))}";
}
