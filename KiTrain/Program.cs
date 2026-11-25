using KiLernprojekt;

string[] labels =
[
    "begruessung",
    "identitaet",
    "faehigkeiten",
    "hilfe",
    "status",
];

IntentNetwork network = new(labels);
string outputDirectory = GetKiLernprojektDirectory();
Directory.CreateDirectory(outputDirectory);
string weightsPath = Path.Combine(outputDirectory, "eigene-gewichte.json");

if (File.Exists(weightsPath))
{
    network.Load(weightsPath);
    Console.WriteLine("Vorherigen Modell-Checkpoint geladen.");
}

Dictionary<string, string[]> trainingData = new()
{
    ["begruessung"] = ["hallo", "hi", "guten tag", "hey"],
    ["identitaet"] = ["wie heißt du", "wer bist du", "wie ist dein name"],
    ["faehigkeiten"] = ["was kannst du", "csharp programmieren", "code schreiben"],
    ["hilfe"] = ["hilfe", "befehle", "anleitung", "benutzen"],
    ["status"] = ["wie geht es dir", "geht es dir gut", "wie läuft es"],
};

foreach (KeyValuePair<string, string[]> category in trainingData)
{
    foreach (string example in category.Value)
    {
        network.Train(example, category.Key);
    }
}

foreach (string sourceFile in Directory.EnumerateFiles(
             GetKiLernprojektDirectory(),
             "*.cs",
             SearchOption.AllDirectories))
{
    if (sourceFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        sourceFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    string source = File.ReadAllText(sourceFile);
    foreach (string line in source.Split('\n'))
    {
        string trimmed = line.Trim();
        if (trimmed.Contains("class ", StringComparison.Ordinal))
        {
            network.Train(trimmed, "faehigkeiten", 5);
        }
        if (trimmed.Contains("Console.", StringComparison.Ordinal))
        {
            network.Train(trimmed, "faehigkeiten", 5);
        }
    }
}

network.Save(weightsPath);
Console.WriteLine("Eigenes Modelltraining abgeschlossen.");

static string GetKiLernprojektDirectory()
{
    string? current = Directory.GetCurrentDirectory();
    while (!string.IsNullOrEmpty(current))
    {
        string candidate = Path.Combine(current, "KiLernprojekt");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        string? parent = Directory.GetParent(current)?.FullName;
        if (parent is null)
        {
            break;
        }
        current = parent;
    }

    throw new DirectoryNotFoundException("KiLernprojekt-Verzeichnis wurde nicht gefunden.");
}
