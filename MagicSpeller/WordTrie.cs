namespace MagicSpeller;

public record WordTrieNode(Dictionary<char, WordTrieNode> Subs, bool Valid)
{
    public WordTrieNode? this[char c] => Subs.GetValueOrDefault(c);
}

public record WordTrie
{
    public Dictionary<char, WordTrieNode> Subs { get; } = new();

    public WordTrie(string path)
    {
        foreach (var word in File.ReadAllLines(path))
            Insert(word);
    }

    private void Insert(string word)
    {
        if (string.IsNullOrEmpty(word))
            return;

        var node = Subs.GetValueOrDefault(word[0]);
        if (node is null)
        {
            Subs.Add(word[0], new(new(), false));
            node = Subs[word[0]];
        }

        if (word.Length == 1)
            return;

        foreach (var c in word[1..^1])
        {
            var nextNode = node[c];
            if (nextNode is null)
            {
                node.Subs.Add(c, new(new(), false));
                nextNode = node[c]!;
            }
            node = nextNode;
        }

        var lastNode = node[word[^1]];
        if (lastNode is not null)
            node.Subs.Remove(word[^1]);
        node.Subs.Add(word[^1], new(lastNode?.Subs ?? new(), true));
    }
}
