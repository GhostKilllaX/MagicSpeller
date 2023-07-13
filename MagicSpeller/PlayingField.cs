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

public readonly record struct Field(char Letter, SpecialField Special);

public record PlayingField(Field[,] Fields)
{
    private readonly int _rows = Fields.GetLength(0);
    private readonly int _cols = Fields.GetLength(1);

    private SearchResult? FindBestWordStep(WordTree words, SearchResult current)
    {
        var max = words.Exists(current.Word) ? current : null;
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            var newPos = current.Path[^1] + new Size(xOffset, yOffset);
            if (newPos.Y < 0 || newPos.Y >= _rows || newPos.X < 0 || newPos.X >= _cols)
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
        for (var y = 0; y < _rows; y++)
        for (var x = 0; x < _cols; x++)
        {
            var newField = Fields[y, x];
            var result = FindBestWordStep(words, new(new() { newField }, new() { new(x, y) }));
            if (result?.Points > (max?.Points ?? 0))
                max = result;
        }
        return max;
    }

    private SearchResultWithSwaps? FindBestWordLoopApproach(WordTree words, int swaps)
    {
        if (swaps == 0)
        {
            var result = FindBestWord(words);
            return result is null ? null : new(result.Fields, result.Path, new());
        }

        return GetCombinations()
            .AsParallel()
            .Select(tuple =>
            {
                var (x, y, letter) = tuple;
                var newField = (Field[,])Fields.Clone();
                newField[y, x] = newField[y, x] with { Letter = letter };
                var newPlayingField = new PlayingField(newField);
                var result = newPlayingField.FindBestWordLoopApproach(words, swaps - 1);
                if (result is null)
                    return null;
                if (letter == Fields[y, x].Letter)
                    return result;
                return result with { Swaps = new(result.Swaps) { new(new(x, y), Fields[y, x].Letter, letter) } };
            })
            .MaxBy(s => s?.Points);

        IEnumerable<(int x, int y, char letter)> GetCombinations()
        {
            for (var y = 0; y < _rows; y++)
            for (var x = 0; x < _cols; x++)
            for (var letter = 'A'; letter <= 'Z'; letter++)
                yield return (x, y, letter);
        }
    }

    private SearchResultWithSwaps? FindWordStep(string word, int swaps, SearchResultWithSwaps current)
    {
        if (word.Length == current.Fields.Count && word == current.Word)
            return current;

        SearchResultWithSwaps? max = null;
        if (current.Fields.Count >= word.Length)
            return max;

        var seachFor = word[current.Fields.Count];
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            var newPos = current.Path[^1] + new Size(xOffset, yOffset);
            if (newPos.Y < 0 || newPos.Y >= _rows || newPos.X < 0 || newPos.X >= _cols)
                continue;
            if (current.Path.Contains(newPos))
                continue;
            if (Fields[newPos.Y, newPos.X].Letter != seachFor && swaps == 0)
                continue;

            var newSwaps = swaps;
            var newField = Fields[newPos.Y, newPos.X] with { Letter = seachFor };
            var newSwapList = current.Swaps;
            if (Fields[newPos.Y, newPos.X].Letter != seachFor)
            {
                newSwapList = new(current.Swaps) { new(new(newPos.X, newPos.Y), Fields[newPos.Y, newPos.X].Letter, seachFor) };
                newSwaps--;
            }

            var next = new SearchResultWithSwaps(new(current.Fields) { newField }, new(current.Path) { new(newPos.X, newPos.Y) }, newSwapList);
            var result = FindWordStep(word, newSwaps, next);
            if (result?.Points > (max?.Points ?? 0))
                max = result;
        }
        return max;
    }

    private SearchResultWithSwaps? FindBestWordListApproach(IEnumerable<string> words, int swaps)
    {
        return GetCombinations()
            .AsParallel()
            .Select(tuple =>
            {
                var (x, y, word) = tuple;
                var newSwaps = swaps;
                var newField = Fields[y, x] with { Letter = word[0] };
                var newSwapList = new List<SwapInfo>();
                if (Fields[y, x].Letter != word[0])
                {
                    if (swaps == 0)
                        return null;
                    newSwapList.Add(new(new(x, y), Fields[y, x].Letter, word[0]));
                    newSwaps--;
                }
                var current = new SearchResultWithSwaps(new() { newField }, new() { new(x, y) }, newSwapList);
                return FindWordStep(word, newSwaps, current);
            })
            .MaxBy(s => s?.Points);

        IEnumerable<(int x, int y, string word)> GetCombinations()
        {
            foreach (var word in words)
                for (var y = 0; y < _rows; y++)
                for (var x = 0; x < _cols; x++)
                    yield return (x, y, word);
        }
    }

    public SearchResultWithSwaps? FindBestWordWithSwaps(WordList words, int swaps) => swaps <= 1
        ? FindBestWordLoopApproach(words.WordTree, swaps)
        : FindBestWordListApproach(words.Words, swaps);
}
