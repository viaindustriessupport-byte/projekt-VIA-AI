# Eigenes KI-Labor

Dieses Repository enthält eine kleine, selbst geschriebene C#-Absichtsklassifikation und einen Trainer. Es ist **kein** großes Sprachmodell und erzeugt keine beliebigen Chat- oder Coding-Antworten.

## Lokaler Start

```powershell
dotnet run --project KiLernprojekt\KiLernprojekt.csproj
```

Die App lädt `KiLernprojekt\eigene-gewichte.json`. Die NVIDIA-GPU wird erkannt; das aktuelle Training selbst läuft im C#-Trainer auf der CPU.

## Cloud-Training und Updates

`.github/workflows/ai-training.yml` startet alle sechs Stunden (00:00, 06:00, 12:00 und 18:00 UTC), bei Änderungen an Trainer- oder C#-Quelldateien sowie manuell über GitHub Actions. Jede begrenzte Runde lädt den letzten Checkpoint, trainiert mit den eingebauten Beispielen und dem C#-Quelltext weiter, erstellt eine SHA-256-Prüfsumme und veröffentlicht die aktualisierten Gewichte als GitHub Release. Ein einzelner GitHub-Lauf kann nicht unbegrenzt laufen; der Fortschritt wird zwischen den Runden im Release gespeichert.

Beim App-Start liest die Anwendung `KiLernprojekt\sync-config.json`, lädt das Manifest des letzten Releases und übernimmt Gewichte nur, wenn die Prüfsumme stimmt. Der Sync nutzt den öffentlichen GitHub-Release-Endpunkt; Zugangsdaten sind nicht in der App gespeichert.

GitHub-Runner sind zeit- und ressourcenbegrenzt. Wiederholtes Training mit denselben Beispielen macht das aktuelle kleine Modell nicht zu einem Coding-, Sprach- oder Bildmodell. Dafür braucht es in späteren Schritten neue Architekturen, geeignete Datensätze, Tests und deutlich mehr Rechenleistung.

## Öffentliches Status-Dashboard und Cloudflare

`site/` enthält ein öffentlich lesbares Dashboard. Es bezieht Trainingsläufe und Releases über die öffentliche GitHub-API, aktualisiert die Anzeige alle fünf Minuten und speichert keine Zugangsdaten. Das Training läuft täglich sowie nach Änderungen an Trainer- oder C#-Quelldateien. Manuelles Starten ist über den Button **Training verwalten** möglich und erfordert eine GitHub-Anmeldung.

Cloudflare Pages ist mit diesem GitHub-Repository verbunden und veröffentlicht Commits auf `main` automatisch. Das Projekt verwendet keinen Build-Befehl und veröffentlicht den Ordner `site/`; dafür sind keine API-Tokens oder GitHub-Secrets erforderlich.

Die Dashboard-Adresse ist `https://via-ai-training-dashboard.pages.dev`. Die Trainingsläufe selbst laufen weiterhin auf GitHub Actions (CPU); Cloudflare hostet die Anzeige und führt kein Modelltraining aus.
