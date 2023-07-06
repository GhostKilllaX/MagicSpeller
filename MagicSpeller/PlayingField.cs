using System.Drawing;

namespace MagicSpeller;

public enum SpecialField
{
    None,
    DoubleLetter,
    TripleLetter,
    DoubleWord,
    TripleWord
}

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

    public string Word => string.Join("", Fields.Select(field => field.Letter));

    public int Points { get; }

    public List<Field> Fields { get; }

    public List<Point> Path { get; }

    public override string ToString() => $"{Word} {Points}P {string.Join(", ", Path.Select(point => new Point(point.X + 1, point.Y + 1)))}";
}

public record SearchResultWithSwaps
    (List<Field> Fields, List<Point> Path, List<(Point Position, char OldChar, char NewChar)> Swaps) : SearchResult(Fields, Path)
{
    public override string ToString() => $"{base.ToString()} Swaps: {
        string.Join(", ", Swaps.Select(swap => (new Point(swap.Position.X + 1, swap.Position.Y + 1), swap.OldChar, swap.NewChar)))}";
}

public record Field(char Letter, SpecialField Special);

public record PlayingField(Field[,] Fields)
{
    private SearchResult? FindBestWordStep(WordTree words, SearchResult current)
    {
        var max = words.Exists(current.Word) ? current : null;
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            var newPos = current.Path.Last() + new Size(xOffset, yOffset);
            if (newPos.Y < 0 || newPos.Y >= 5 || newPos.X < 0 || newPos.X >= 5)
                continue;
            if (current.Path.Contains(newPos))
                continue;

            var newField = Fields[newPos.Y, newPos.X];
            var searchResult = new SearchResult(new(current.Fields) { newField }, new(current.Path) { newPos });
            if (!words.CheckBeginning(searchResult.Word))
                continue;
            var recursiveSearchResult = FindBestWordStep(words, searchResult);
            if (recursiveSearchResult?.Points > (max?.Points ?? 0))
                max = recursiveSearchResult;
        }
        return max;
    }

    public SearchResult? FindBestWord(WordTree words)
    {
        SearchResult? max = null;
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            var newField = Fields[y, x];
            var result = FindBestWordStep(words, new(new() { newField }, new() { new(x, y) }));
            if (result?.Points > (max?.Points ?? 0))
                max = result;
        }
        return max;
    }

    public SearchResultWithSwaps? FindBestWordWithSwaps(WordTree words, int swaps)
    {
        if (swaps == 0)
        {
            var result = FindBestWord(words);
            return result is null ? null : new(result.Fields, result.Path, new());
        }

        SearchResultWithSwaps? max = null;
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
            //for (var letter = 'A'; letter <= 'Z'; letter++)
            Parallel.For(0,
                27,
                i =>
                {
                    var letter = (char)('A' + i);
                    if (letter == Fields[y, x].Letter)
                        return;
                    var newField = (Field[,])Fields.Clone();
                    newField[y, x] = newField[y, x] with { Letter = letter };
                    var newPlayingField = new PlayingField(newField);
                    var result = newPlayingField.FindBestWordWithSwaps(words, swaps - 1);
                    if (result?.Points > (max?.Points ?? 0))
                        max = result with { Swaps = new List<(Point, char, char)>(result.Swaps) { (new(x, y), Fields[y, x].Letter, letter) } };
                });
        return max;
    }
}
