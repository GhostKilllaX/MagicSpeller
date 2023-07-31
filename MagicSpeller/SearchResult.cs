using System.Drawing;

namespace MagicSpeller;

public readonly record struct FieldPositionTuple(Field Field, Point Position);

public readonly record struct SwapInfo(Point Position, char OldChar, char NewChar);

public record SearchResult
{
    public SearchResult(IReadOnlyList<FieldPositionTuple> Fields, IReadOnlyList<SwapInfo> Swaps)
    {
        this.Fields = Fields;
        this.Swaps = Swaps;
        Points = CalculatePoints();
    }

    public string Word => string.Concat(Fields.Select(tuple => tuple.Field.Letter));

    public int Points { get; }

    public IReadOnlyList<FieldPositionTuple> Fields { get; }

    public IReadOnlyList<SwapInfo> Swaps { get; }

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
        foreach (var (field, _) in Fields)
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

    public override string ToString() => $"{Word} {Points}P {
        string.Join(", ", Fields.Select(field => new Point(field.Position.X + 1, field.Position.Y + 1)))} Swaps: {
            string.Join(", ", Swaps.Select(swap => (new Point(swap.Position.X + 1, swap.Position.Y + 1), swap.OldChar, swap.NewChar)))}";
}
