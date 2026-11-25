namespace KiLernprojekt;

public sealed class MarkovGenerator
{
    private readonly Dictionary<string, List<string>> transitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random random = new();

    public void Train(string text)
    {
        string[] words = text
            .Split(new[] { ' ', '\n', '\r', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length - 1; i++)
        {
            string current = words[i].ToLowerInvariant();
            string next = words[i + 1].ToLowerInvariant();
            if (!transitions.TryGetValue(current, out List<string>? choices))
            {
                choices = [];
                transitions[current] = choices;
            }
            choices.Add(next);
        }
    }

    public string Generate(string start, int wordCount = 12)
    {
        string current = start.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.ToLowerInvariant() ?? "";

        List<string> result = [];
        for (int i = 0; i < wordCount; i++)
        {
            if (!transitions.TryGetValue(current, out List<string>? choices))
            {
                break;
            }

            string next = choices[random.Next(choices.Count)];
            result.Add(next);
            current = next;
        }

        return result.Count == 0
            ? "Ich habe zu diesem Thema noch keine Sprachabfolge gelernt."
            : string.Join(" ", result);
    }
}
