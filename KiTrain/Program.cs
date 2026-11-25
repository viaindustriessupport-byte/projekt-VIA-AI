using KiLernprojekt;

string[] labels =
[
    "begruessung",
    "identitaet",
    "faehigkeiten",
    "hilfe",
    "status",
    "programmieren",
    "bildanalyse",
    "bildgenerierung",
    "danken",
    "verabschiedung",
];

IntentNetwork network = new(labels);
string outputDirectory = GetKiLernprojektDirectory();
Directory.CreateDirectory(outputDirectory);
string weightsPath = Path.Combine(outputDirectory, "eigene-gewichte.json");

if (File.Exists(weightsPath))
{
    Console.WriteLine(network.Load(weightsPath)
        ? "Vorherigen Modell-Checkpoint geladen."
        : "Checkpoint nutzt ein älteres Format; das neue Netzwerk startet mit frischen Gewichten.");
}

Dictionary<string, string[]> trainingData = new()
{
    ["begruessung"] =
    [
        "hallo",
        "hi",
        "guten morgen",
        "guten tag",
        "guten abend",
        "hey, wie geht es",
        "moin",
        "schön dich zu sehen",
    ],
    ["identitaet"] =
    [
        "wer bist du",
        "wie heißt du",
        "wie ist dein name",
        "was für eine ki bist du",
        "wer hat dich programmiert",
        "bist du ein computerprogramm",
        "was ist deine geschichte",
        "wie soll ich dich nennen",
    ],
    ["faehigkeiten"] =
    [
        "was kannst du",
        "welche aufgaben kannst du übernehmen",
        "was kannst du schon lernen",
        "welche funktionen hast du",
        "wobei kannst du mir helfen",
        "was kann dieses modell",
        "kannst du mir etwas erklären",
        "welche themen kennst du",
    ],
    ["hilfe"] =
    [
        "hilfe",
        "ich brauche hilfe",
        "wie benutze ich das dashboard",
        "gibt es eine anleitung",
        "erkläre mir die bedienung",
        "wie teste ich das modell",
        "wo finde ich die befehle",
        "ich komme nicht weiter",
    ],
    ["status"] =
    [
        "wie geht es dir",
        "geht es dir gut",
        "wie läuft es",
        "was ist dein aktueller status",
        "läuft das training gerade",
        "wann trainierst du wieder",
        "ist das modell online",
        "wann war der letzte trainingslauf",
    ],
    ["programmieren"] =
    [
        "schreibe ein csharp programm",
        "erstelle eine javascript funktion",
        "hilf mir beim debuggen von code",
        "wie programmiere ich eine schleife",
        "analysiere diesen quellcode",
        "erkläre diese programmierfehlermeldung",
        "baue eine webseite mit html",
        "implementiere eine klasse",
    ],
    ["bildanalyse"] =
    [
        "analysiere dieses bild",
        "was ist auf diesem foto zu sehen",
        "erkenne die objekte im bild",
        "beschreibe dieses bild",
        "was zeigt das foto",
        "lies den text auf diesem bild",
        "vergleiche diese beiden bilder",
        "erkennst du personen auf dem foto",
    ],
    ["bildgenerierung"] =
    [
        "erstelle ein bild",
        "generiere ein bild von einem wald",
        "zeichne eine futuristische stadt",
        "male ein bild mit einem hund",
        "erzeuge eine illustration",
        "ich möchte ein neues bild erstellen",
        "generiere ein logo für mein projekt",
        "entwirf ein digitales kunstwerk",
    ],
    ["danken"] =
    [
        "danke",
        "vielen dank",
        "danke für deine hilfe",
        "das war hilfreich",
        "super, danke dir",
        "ich weiß das zu schätzen",
        "besten dank",
        "du hast mir geholfen",
    ],
    ["verabschiedung"] =
    [
        "tschüss",
        "auf wiedersehen",
        "bis später",
        "mach es gut",
        "ich muss jetzt gehen",
        "bis bald",
        "gute nacht",
        "wir sprechen uns später",
    ],
};

foreach (KeyValuePair<string, string[]> category in trainingData)
{
    foreach (string example in category.Value)
    {
        network.Train(example, category.Key);
    }
}

network.Save(weightsPath);
Console.WriteLine($"Training abgeschlossen: {trainingData.Count} Absichten, {trainingData.Values.Sum(examples => examples.Length)} Beispielsätze.");

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
