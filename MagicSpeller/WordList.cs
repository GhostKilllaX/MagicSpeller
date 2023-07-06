namespace MagicSpeller;

public record WordNode(char Char, Dictionary<char, WordNode> Subs, bool Valid)
{
    public WordNode? this[char c] => Subs.GetValueOrDefault(c);
}

public record WordTree(Dictionary<char, WordNode> Subs)
{
    private WordNode? GetLastNode(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var node = Subs.GetValueOrDefault(text[0]);
        foreach (var c in text[1..])
            if (node is null)
                break;
            else
                node = node[c];
        return node;
    }

    public bool CheckBeginning(string beginning) => GetLastNode(beginning) is not null;

    public bool Exists(string word) => GetLastNode(word)?.Valid ?? false;

    public void Insert(string word)
    {
        if (string.IsNullOrEmpty(word))
            return;

        var node = Subs.GetValueOrDefault(word[0]);
        if (node is null)
        {
            Subs.Add(word[0], new(word[0], new(), false));
            node = Subs[word[0]];
        }

        if (word.Length == 1)
            return;

        foreach (var c in word[1..^1])
        {
            var nextNode = node[c];
            if (nextNode is null)
            {
                node.Subs.Add(c, new(c, new(), false));
                nextNode = node[c]!;
            }
            node = nextNode;
        }

        var lastNode = node[word[^1]];
        if (lastNode is not null)
            node.Subs.Remove(word[^1]);
        node.Subs.Add(word[^1], new(word[^1], lastNode?.Subs ?? new(), true));
    }
}

public static class WordList
{
    public static readonly WordTree WordTree = new(new());

    static WordList()
    {
        var words = File.ReadAllLines("./words.txt");
        foreach (var word in words)
            WordTree.Insert(word);
    }
}
