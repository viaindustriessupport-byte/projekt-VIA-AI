using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace KiLernprojekt;

public partial class MainWindow : Window
{
    private readonly NotifyIcon trayIcon;
    private readonly GpuBackend gpu;
    private readonly ModelSyncService syncService = new();
    private readonly IntentNetwork intentNetwork;
    private bool kiPausiert;
    private readonly Dictionary<string, string> wissensbank = new()
    {
        ["hallo"] = "Hallo! Schon gut dich kennenzulernen.",
        ["wie heißt du"] = "Ich heiße C#-Bot. Ich laufe vollständig lokal.",
        ["wer bist du"] = "Ich bin dein selbst programmiertes lokales neuronales KI-Projekt.",
        ["hilfe"] = "Schreibe eine Frage oder lerne neue Antworten mit Lernen.",
    };

    private readonly string wissensDatei =
        System.IO.Path.Combine(AppContext.BaseDirectory, "wissen.json");
    private string codeDatei = "";

    public MainWindow()
    {
        InitializeComponent();

        intentNetwork = new IntentNetwork(
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
        ]);
        bool modelLoaded = intentNetwork.Load(System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "eigene-gewichte.json"));

        gpu = new GpuBackend();
        StatusText.Text = gpu.IsCuda
            ? $"GPU aktiv: {gpu.DeviceName}"
            : $"GPU: {gpu.DeviceName}";

        LadeWissen();
        Loaded += async (_, _) => await SyncGewichteBeimStartAsync();
        ChatHinzu("KI", modelLoaded
            ? "Lokale Demo bereit. Das mehrschichtige Absichtsmodell ist geladen."
            : "Lokale Demo bereit. Das gespeicherte Modell ist nicht kompatibel und muss neu trainiert werden.");

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = false,
            Text = "Meine eigene KI",
        };
        trayIcon.DoubleClick += (_, _) => TrayOeffnen();

        ContextMenuStrip menu = new();
        menu.Items.Add("Öffnen", null, (_, _) => TrayOeffnen());
        menu.Items.Add("Beenden", null, (_, _) => VollstaendigBeenden());
        trayIcon.ContextMenuStrip = menu;
    }

    private void LadeWissen()
    {
        if (!System.IO.File.Exists(wissensDatei))
        {
            return;
        }

        Dictionary<string, string>? gespeichert =
            JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(wissensDatei));
        if (gespeichert is null)
        {
            return;
        }

        foreach (KeyValuePair<string, string> eintrag in gespeichert)
        {
            wissensbank[eintrag.Key] = eintrag.Value;
        }
    }

    private async Task SyncGewichteBeimStartAsync()
    {
        string configDatei =
            System.IO.Path.Combine(AppContext.BaseDirectory, "sync-config.json");
        if (!System.IO.File.Exists(configDatei))
        {
            CloudStatusText.Text = "Cloud: nicht konfiguriert";
            ChatHinzu("KI", "Keine Cloud-Konfiguration gefunden. Lokales Wissen wird verwendet.");
            return;
        }

        try
        {
            using JsonDocument config =
                JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(configDatei));
            string manifestUrl =
                config.RootElement.GetProperty("manifestUrl").GetString() ?? "";

            if (manifestUrl.Length == 0)
            {
                ChatHinzu("KI", "sync-config.json enthält keine gültige manifestUrl.");
                return;
            }

            StatusText.Text = "Suche Gewichts-Update...";
            string gewichteDatei =
                System.IO.Path.Combine(AppContext.BaseDirectory, "eigene-gewichte.json");
            string? version = await syncService.SyncAsync(manifestUrl, gewichteDatei);
            bool modelLoaded = intentNetwork.Load(gewichteDatei);

            StatusText.Text = gpu.IsCuda
                ? $"GPU aktiv: {gpu.DeviceName}"
                : $"GPU: {gpu.DeviceName}";
            ChatHinzu("KI", !modelLoaded
                ? "Das Cloud-Modell nutzt ein älteres Format. Bitte warte auf einen neuen Trainingslauf."
                : string.IsNullOrEmpty(version)
                    ? "Gewichte wurden aktualisiert."
                    : $"Gewichte aktualisiert: Version {version}");
        }
        catch (Exception exception)
        {
            StatusText.Text = "Lokale KI aktiv";
            ChatHinzu("KI", $"Cloud-Update fehlgeschlagen: {exception.Message}");
        }
    }

    private void Senden_Click(object sender, RoutedEventArgs e)
    {
        SendeEingabe();
    }

    private void ChatTab_Click(object sender, RoutedEventArgs e)
    {
        ChatView.Visibility = Visibility.Visible;
        CodeView.Visibility = Visibility.Collapsed;
    }

    private void CodeTab_Click(object sender, RoutedEventArgs e)
    {
        ChatView.Visibility = Visibility.Collapsed;
        CodeView.Visibility = Visibility.Visible;
    }

    private void VerlaufLoeschen_Click(object sender, RoutedEventArgs e)
    {
        ChatText.Clear();
    }

    private void CodeOeffnen_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Filter = "C#-Dateien (*.cs)|*.cs|Alle Dateien (*.*)|*.*",
            InitialDirectory = @"E:\ao",
        };

        if (dialog.ShowDialog() == true)
        {
            codeDatei = dialog.FileName;
            CodeText.Text = File.ReadAllText(codeDatei);
            OutputText.Text = $"Geöffnet: {codeDatei}";
        }
    }

    private void CodeSpeichern_Click(object sender, RoutedEventArgs e)
    {
        if (codeDatei.Length == 0)
        {
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                Filter = "C#-Dateien (*.cs)|*.cs",
                InitialDirectory = @"E:\ao",
                FileName = "Program.cs",
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }
            codeDatei = dialog.FileName;
        }

        File.WriteAllText(codeDatei, CodeText.Text);
        OutputText.Text = $"Gespeichert: {codeDatei}";
    }

    private async void CodeAusfuehren_Click(object sender, RoutedEventArgs e)
    {
        if (kiPausiert)
        {
            OutputText.Text = "Pausiert: KI und lokale Ausführung sind deaktiviert.";
            return;
        }

        if (codeDatei.Length == 0)
        {
            OutputText.Text = "Öffne oder speichere zuerst eine C#-Datei.";
            return;
        }

        OutputText.Text = "Starte...";
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(codeDatei) ?? @"E:\ao",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        string? projektVerzeichnis = Path.GetDirectoryName(codeDatei);
        string projektDatei = Directory
            .EnumerateFiles(projektVerzeichnis ?? @"E:\ao", "*.csproj", SearchOption.AllDirectories)
            .FirstOrDefault() ?? "";

        if (projektDatei.Length == 0)
        {
            OutputText.Text = "Keine .csproj-Datei in diesem Projektordner gefunden.";
            return;
        }

        startInfo.Arguments = $"run --project \"{projektDatei}\"";

        try
        {
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet konnte nicht gestartet werden.");
            string standardOut = await process.StandardOutput.ReadToEndAsync();
            string standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            OutputText.Text =
                (standardOut + standardError).Trim().Length == 0
                    ? $"Beendet mit Code {process.ExitCode}"
                    : standardOut + standardError;
        }
        catch (Exception exception)
        {
            OutputText.Text = $"Ausführung fehlgeschlagen: {exception.Message}";
        }
    }

    private void EingabeText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendeEingabe();
        }
    }

    private void SendeEingabe()
    {
        if (kiPausiert)
        {
            ChatHinzu("KI", "Die KI ist pausiert. Klicke auf 'KI fortsetzen'.");
            return;
        }

        string eingabe = EingabeText.Text.Trim();
        if (eingabe.Length == 0)
        {
            return;
        }

        EingabeText.Clear();
        ChatHinzu("Du", eingabe);
        ChatHinzu("KI", Verarbeite(eingabe));
    }

    private string Verarbeite(string eingabe)
    {
        if (eingabe.Equals("tschüss", StringComparison.OrdinalIgnoreCase))
        {
            return "Bis bald!";
        }

        string text = eingabe.ToLowerInvariant();
        foreach (KeyValuePair<string, string> eintrag in wissensbank)
        {
            if (text.Contains(eintrag.Key))
            {
                return eintrag.Value;
            }
        }

        string intent = intentNetwork.Predict(eingabe);
        if (intent == "begruessung")
        {
            return "Hallo! Welche Aufgabe möchtest du bearbeiten?";
        }
        if (intent == "identitaet")
        {
            return "Ich bin eine eigene lokale C#-KI im Aufbau.";
        }
        if (intent == "faehigkeiten")
        {
            return "Ich kann an C#-Programmierung und Projektdateien arbeiten.";
        }
        if (intent == "hilfe")
        {
            return "Nutze Chat für Fragen oder Code für das Coding-Interface.";
        }
        if (intent == "programmieren")
        {
            return "Ich erkenne eine Programmieraufgabe, kann aber noch keinen geprüften Code generieren.";
        }
        if (intent == "bildanalyse")
        {
            return "Ich erkenne den Wunsch nach Bildanalyse, kann aktuell aber keine Bilder verarbeiten.";
        }
        if (intent == "bildgenerierung")
        {
            return "Ich erkenne den Wunsch nach einem Bild, kann aktuell aber keine Bilder erzeugen.";
        }
        if (intent == "danken")
        {
            return "Gern geschehen!";
        }
        if (intent == "verabschiedung")
        {
            return "Bis bald!";
        }

        return "Das habe ich noch nicht gelernt. Nutze die Schaltfläche Lernen.";
    }

    private void Lernen_Click(object sender, RoutedEventArgs e)
    {
        string eingabe = EingabeText.Text.Trim();
        if (eingabe.Length == 0)
        {
            ChatHinzu("KI", "Beispiel: wetter = Heute scheint die Sonne.");
            return;
        }

        EingabeText.Clear();
        string[] teile = eingabe.Split('=', 2);
        if (teile.Length != 2)
        {
            ChatHinzu("KI", "Verwendung: Schlüsselwort = Antwort");
            return;
        }

        string thema = teile[0].Trim().ToLowerInvariant();
        string antwort = teile[1].Trim();
        if (thema.Length == 0 || antwort.Length == 0)
        {
            ChatHinzu("KI", "Thema und Antwort dürfen nicht leer sein.");
            return;
        }

        wissensbank[thema] = antwort;
        System.IO.File.WriteAllText(
            wissensDatei,
            JsonSerializer.Serialize(wissensbank));
        ChatHinzu("KI", $"Gelernt: '{thema}' = '{antwort}'");
    }

    private void ChatHinzu(string absender, string text)
    {
        ChatText.AppendText($"{absender}: {text}{Environment.NewLine}{Environment.NewLine}");
        ChatText.ScrollToEnd();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        trayIcon.Visible = true;
    }

    private void TrayOeffnen()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        trayIcon.Visible = false;
    }

    private void Beenden_Click(object sender, RoutedEventArgs e)
    {
        VollstaendigBeenden();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        kiPausiert = !kiPausiert;
        PauseButton.Content = kiPausiert ? "KI fortsetzen" : "KI pausieren";
        StatusText.Text = kiPausiert
            ? "KI pausiert – keine KI-Arbeit"
            : gpu.IsCuda
                ? $"GPU aktiv: {gpu.DeviceName}"
                : $"GPU: {gpu.DeviceName}";
    }

    private void VollstaendigBeenden()
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        syncService.Dispose();
        gpu.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
