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

public record Field(char Letter, SpecialField Special);

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
            var newPos = current.Path.Last() + new Size(xOffset, yOffset);
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

        SearchResultWithSwaps? max = null;
        var semaphore = new SemaphoreSlim(1);
        for (var y = 0; y < _rows; y++)
        for (var x = 0; x < _cols; x++)
            //for (var letter = 'A'; letter <= 'Z'; letter++)
            Parallel.For<SearchResultWithSwaps?>(0,
                27,
                () => null,
                (i, _, localMax) =>
                {
                    var letter = (char)('A' + i);
                    if (letter == Fields[y, x].Letter)
                        return localMax;
                    var newField = (Field[,])Fields.Clone();
                    newField[y, x] = newField[y, x] with { Letter = letter };
                    var newPlayingField = new PlayingField(newField);
                    var result = newPlayingField.FindBestWordLoopApproach(words, swaps - 1);
                    if (result?.Points > (localMax?.Points ?? 0))
                        localMax = result with { Swaps = new(result.Swaps) { new(new(x, y), Fields[y, x].Letter, letter) } };
                    return localMax;
                },
                localMax =>
                {
                    semaphore.Wait();
                    if (localMax?.Points > (max?.Points ?? 0))
                        max = localMax;
                    semaphore.Release();
                });
        return max;
    }

    private SearchResultWithSwaps? FindWordStep(string word, int swaps, SearchResultWithSwaps current)
    {
        if (word == current.Word)
            return current;

        SearchResultWithSwaps? max = null;
        if (current.Fields.Count >= word.Length)
            return max;

        var seachFor = word[current.Fields.Count];
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            var newPos = current.Path.Last() + new Size(xOffset, yOffset);
            if (newPos.Y < 0 || newPos.Y >= _rows || newPos.X < 0 || newPos.X >= _cols)
                continue;
            if (current.Path.Contains(newPos))
                continue;
            if (Fields[newPos.Y, newPos.X].Letter != seachFor && swaps == 0)
                continue;

            var newSwaps = swaps;
            var newField = Fields[newPos.Y, newPos.X] with { Letter = seachFor };
            var newSwapList = new List<SwapInfo>(current.Swaps);
            if (Fields[newPos.Y, newPos.X].Letter != seachFor)
            {
                newSwapList.Add(new(new(newPos.X, newPos.Y), Fields[newPos.Y, newPos.X].Letter, seachFor));
                newSwaps--;
            }

            var next = new SearchResultWithSwaps(new(current.Fields) { newField }, new(current.Path) { new(newPos.X, newPos.Y) }, new(newSwapList));
            var result = FindWordStep(word, newSwaps, next);
            if (result?.Points > (max?.Points ?? 0))
                max = result;
        }
        return max;
    }

    private SearchResultWithSwaps? FindBestWordListApproach(IEnumerable<string> words, int swaps)
    {
        SearchResultWithSwaps? max = null;
        var semaphore = new SemaphoreSlim(1);
        //foreach (var word in words)
        Parallel.ForEach<string, SearchResultWithSwaps?>(words,
            () => null,
            (word, _, localMax) =>
            {
                for (var y = 0; y < _rows; y++)
                for (var x = 0; x < _cols; x++)
                {
                    var newSwaps = swaps;
                    var newField = Fields[y, x] with { Letter = word[0] };
                    var newSwapList = new List<SwapInfo>();
                    if (Fields[y, x].Letter != word[0])
                    {
                        if (swaps == 0)
                            continue;
                        newSwapList.Add(new(new(x, y), Fields[y, x].Letter, word[0]));
                        newSwaps--;
                    }
                    var current = new SearchResultWithSwaps(new() { newField }, new() { new(x, y) }, new(newSwapList));
                    var result = FindWordStep(word, newSwaps, current);
                    if (result?.Points > (localMax?.Points ?? 0))
                        localMax = result;
                }
                return localMax;
            },
            localMax =>
            {
                semaphore.Wait();
                if (localMax?.Points > (max?.Points ?? 0))
                    max = localMax;
                semaphore.Release();
            });
        return max;
    }

    public SearchResultWithSwaps? FindBestWordWithSwaps(WordList words, int swaps) => swaps <= 1
        ? FindBestWordLoopApproach(words.WordTree, swaps)
        : FindBestWordListApproach(words.Words, swaps);
}
