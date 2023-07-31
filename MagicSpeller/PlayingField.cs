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

    private ref struct InternalStepBuffer(Span<FieldPositionTuple> fields, int fieldsWriteIndex, Span<SwapInfo> swaps, int swapsWriteIndex)
    {
        public Span<FieldPositionTuple> Fields { get; } = fields;

        public int FieldsWriteIndex { get; set; } = fieldsWriteIndex;

        public Span<SwapInfo> Swaps { get; } = swaps;

        public int SwapsWriteIndex { get; set; } = swapsWriteIndex;

        public SearchResult ToSearchResult() => new(Fields[..FieldsWriteIndex].ToArray(), Swaps[..SwapsWriteIndex].ToArray());
    }

    private SearchResult? FindBestWordStep(WordTrieNode node, int swaps, ref InternalStepBuffer current)
    {
        SearchResult? max = null;
        var (subs, valid) = node;

        if (valid)
            max = current.ToSearchResult();
        if (subs.Count == 0)
            return max;

        current.FieldsWriteIndex++;
        var swapsBefore = current.SwapsWriteIndex;
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            var newPos = current.Fields[current.FieldsWriteIndex - 2].Position + new Size(xOffset, yOffset);
            if (newPos.Y < 0 || newPos.Y >= Rows || newPos.X < 0 || newPos.X >= Cols)
                continue;
            if (!subs.ContainsKey(Fields[newPos.Y, newPos.X].Letter) && swaps == 0)
                continue;
            if (ContainsPosition(current.Fields, current.FieldsWriteIndex - 1, newPos))
                continue;

            foreach (var sub in subs)
            {
                var newSwaps = swaps;
                if (Fields[newPos.Y, newPos.X].Letter != sub.Key)
                {
                    if (swaps == 0)
                        continue;
                    current.Swaps[current.SwapsWriteIndex++] = new(new(newPos.X, newPos.Y), Fields[newPos.Y, newPos.X].Letter, sub.Key);
                    newSwaps--;
                }
                var newField = new FieldPositionTuple(Fields[newPos.Y, newPos.X] with { Letter = sub.Key }, newPos);
                current.Fields[current.FieldsWriteIndex - 1] = newField;

                var result = FindBestWordStep(sub.Value, newSwaps, ref current);
                if (result?.Points > (max?.Points ?? 0))
                    max = result;
                current.SwapsWriteIndex = swapsBefore;
            }
        }
        current.FieldsWriteIndex--;
        return max;
    }

    public SearchResult? FindBestWord(WordTrie words, int maxSwaps = 0)
    {
        return GetStartCombinations()
            .AsParallel()
            .Select(tuple =>
            {
                var (x, y, (key, subs)) = tuple;

                Span<FieldPositionTuple> fields = stackalloc FieldPositionTuple[Rows * Cols];
                fields[0] = new(Fields[y, x] with { Letter = key }, new(x, y));
                Span<SwapInfo> swaps = stackalloc SwapInfo[maxSwaps];
                var newSwapsCount = maxSwaps;

                if (Fields[y, x].Letter != key)
                {
                    if (maxSwaps == 0)
                        return null;

                    swaps[0] = new(new(x, y), Fields[y, x].Letter, key);
                    newSwapsCount--;
                }
                var current = new InternalStepBuffer(fields, 1, swaps, maxSwaps - newSwapsCount);
                return FindBestWordStep(subs, newSwapsCount, ref current);
            })
            .MaxBy(s => s?.Points);

        IEnumerable<(int x, int y, KeyValuePair<char, WordTrieNode> subNode)> GetStartCombinations()
        {
            foreach (var subNode in words.Subs)
                for (var y = 0; y < Rows; y++)
                for (var x = 0; x < Cols; x++)
                    yield return (x, y, subNode);
        }
    }
}
