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
    public int Rows { get; } = Fields.GetLength(0);

    public int Cols { get; } = Fields.GetLength(1);

    private static bool ContainsPosition(Span<FieldPositionTuple> array, int lenght, Point position)
    {
        for (var index = lenght - 1; index >= 0; index--)
            if (array[index].Position == position)
                return true;
        return false;
    }

    private SearchResult? FindBestWordStep(WordTree words, SearchResult current)
    {
        var max = words.Exists(current.Word) ? current : null;
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            var newPos = current.Fields[^1].Position + new Size(xOffset, yOffset);
            if (newPos.Y < 0 || newPos.Y >= Rows || newPos.X < 0 || newPos.X >= Cols)
                continue;
            if (ContainsPosition((FieldPositionTuple[])current.Fields, current.Fields.Count, newPos))
                continue;

            var newField = new FieldPositionTuple(Fields[newPos.Y, newPos.X], newPos);
            var searchResult = new SearchResult(current.Fields.Append(newField).ToArray());
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
        for (var y = 0; y < Rows; y++)
        for (var x = 0; x < Cols; x++)
        {
            var newField = new FieldPositionTuple(Fields[y, x], new(x, y));
            var result = FindBestWordStep(words, new(new[] { newField }));
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
            return result is null ? null : new(result.Fields, Array.Empty<SwapInfo>());
        }

        return GetStartCombinations()
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
                return result with { Swaps = result.Swaps.Append(new(new(x, y), Fields[y, x].Letter, letter)).ToArray() };
            })
            .MaxBy(s => s?.Points);

        IEnumerable<(int x, int y, char letter)> GetStartCombinations()
        {
            for (var y = 0; y < Rows; y++)
            for (var x = 0; x < Cols; x++)
            for (var letter = 'A'; letter <= 'Z'; letter++)
                yield return (x, y, letter);
        }
    }

    private ref struct InternalStepBuffer(Span<FieldPositionTuple> fields, int fieldsWriteIndex, Span<SwapInfo> swaps, int swapsWriteIndex)
    {
        public Span<FieldPositionTuple> Fields { get; } = fields;

        public int FieldsWriteIndex { get; set; } = fieldsWriteIndex;

        public Span<SwapInfo> Swaps { get; } = swaps;

        public int SwapsWriteIndex { get; set; } = swapsWriteIndex;

        public SearchResultWithSwaps ToSearchResult() => new(Fields[..FieldsWriteIndex].ToArray(), Swaps[..SwapsWriteIndex].ToArray());
    }

    private SearchResultWithSwaps? FindWordStep(string word, int swaps, ref InternalStepBuffer current)
    {
        if (current.FieldsWriteIndex == word.Length)
        {
            var result = current.ToSearchResult();
            if (word == result.Word)
                return result;
        }
        if (current.FieldsWriteIndex >= word.Length)
            return null;

        SearchResultWithSwaps? max = null;
        var seachFor = word[current.FieldsWriteIndex];
        current.FieldsWriteIndex++;
        var swapsBefore = current.SwapsWriteIndex;
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            var newPos = current.Fields[current.FieldsWriteIndex - 2].Position + new Size(xOffset, yOffset);
            if (newPos.Y < 0 || newPos.Y >= Rows || newPos.X < 0 || newPos.X >= Cols)
                continue;
            if (Fields[newPos.Y, newPos.X].Letter != seachFor && swaps == 0)
                continue;
            if (ContainsPosition(current.Fields, current.FieldsWriteIndex - 1, newPos))
                continue;

            var newSwaps = swaps;
            var newField = new FieldPositionTuple(Fields[newPos.Y, newPos.X] with { Letter = seachFor }, newPos);
            if (Fields[newPos.Y, newPos.X].Letter != seachFor)
            {
                current.Swaps[current.SwapsWriteIndex++] = new(new(newPos.X, newPos.Y), Fields[newPos.Y, newPos.X].Letter, seachFor);
                newSwaps--;
            }

            current.Fields[current.FieldsWriteIndex - 1] = newField;
            var result = FindWordStep(word, newSwaps, ref current);
            if (result?.Points > (max?.Points ?? 0))
                max = result;
            current.SwapsWriteIndex = swapsBefore;
        }
        current.FieldsWriteIndex--;
        return max;
    }

    private SearchResultWithSwaps? FindBestWordListApproach(IEnumerable<string> words, int maxSwaps)
    {
        return GetStartCombinations()
            .AsParallel()
            .Select(tuple =>
            {
                var (x, y, word) = tuple;

                Span<FieldPositionTuple> fields = stackalloc FieldPositionTuple[word.Length];
                fields[0] = new(Fields[y, x] with { Letter = word[0] }, new(x, y));
                Span<SwapInfo> swaps = stackalloc SwapInfo[maxSwaps];
                var newSwapsCount = maxSwaps;

                if (Fields[y, x].Letter != word[0])
                {
                    if (maxSwaps == 0)
                        return null;

                    swaps[0] = new(new(x, y), Fields[y, x].Letter, word[0]);
                    newSwapsCount--;
                }
                var current = new InternalStepBuffer(fields, 1, swaps, maxSwaps - newSwapsCount);
                return FindWordStep(word, newSwapsCount, ref current);
            })
            .MaxBy(s => s?.Points);

        IEnumerable<(int x, int y, string word)> GetStartCombinations()
        {
            foreach (var word in words)
                for (var y = 0; y < Rows; y++)
                for (var x = 0; x < Cols; x++)
                    yield return (x, y, word);
        }
    }

    public SearchResultWithSwaps? FindBestWordWithSwaps(WordList words, int swaps) => swaps <= 1
        ? FindBestWordLoopApproach(words.WordTree, swaps)
        : FindBestWordListApproach(words.Words, swaps);
}
