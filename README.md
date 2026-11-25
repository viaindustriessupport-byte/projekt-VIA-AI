# Eigenes KI-Labor

Dieses Repository enthält eine kleine, selbst geschriebene C#-Absichtsklassifikation und einen Trainer. Das Netz verarbeitet 256 Zeichenmerkmale über zwei verborgene Schichten mit 32 und 16 Neuronen und ordnet Texte zehn Absichten zu. Es ist **kein** großes Sprachmodell und erzeugt weder Chat- noch Coding-Antworten oder Bilder.

## Lokaler Start

```powershell
dotnet run --project KiLernprojekt\KiLernprojekt.csproj
```

Die App lädt `KiLernprojekt\eigene-gewichte.json`. Die NVIDIA-GPU wird erkannt; das aktuelle Training selbst läuft im C#-Trainer auf der CPU.

## Cloud-Training und Updates

`.github/workflows/ai-training.yml` startet alle sechs Stunden (00:00, 06:00, 12:00 und 18:00 UTC), bei Änderungen an Trainer- oder C#-Quelldateien sowie manuell über GitHub Actions. Jede begrenzte Runde lädt den letzten passenden Checkpoint, trainiert mit Beispielsätzen für zehn Absichten weiter, erstellt eine SHA-256-Prüfsumme und veröffentlicht die aktualisierten Gewichte als GitHub Release. Ein einzelner GitHub-Lauf kann nicht unbegrenzt laufen; der Fortschritt wird zwischen den Runden im Release gespeichert. Beim Wechsel von der alten linearen Architektur startet der Trainer einmal mit frischen Gewichten.

Beim App-Start liest die Anwendung `KiLernprojekt\sync-config.json`, lädt das Manifest des letzten Releases und übernimmt Gewichte nur, wenn die Prüfsumme stimmt. Der Sync nutzt den öffentlichen GitHub-Release-Endpunkt; Zugangsdaten sind nicht in der App gespeichert.

GitHub-Runner sind zeit- und ressourcenbegrenzt. Programmier-, Bildanalyse- und Bildwunsch-Absichten erkennen nur die Art der Textanfrage; sie fügen dem Modell keine Codegenerierung, Bildverarbeitung oder Bildgenerierung hinzu. Dafür wären separate Modelle, passende Datensätze, Tests und deutlich mehr Rechenleistung nötig.

## Öffentliches Status-Dashboard und Cloudflare

`site/` enthält ein öffentlich lesbares Dashboard. Es bezieht Trainingsläufe und Releases über die öffentliche GitHub-API, aktualisiert die Anzeige alle fünf Minuten und speichert keine Zugangsdaten. `functions/api/model.js` lädt Modellgewichte serverseitig über Cloudflare Pages Functions, damit Browser-CORS den Download nicht blockiert. Das Training läuft alle sechs Stunden sowie nach Änderungen an Trainer- oder C#-Quelldateien. Manuelles Starten ist über den Button **Training starten** möglich und erfordert eine GitHub-Anmeldung.

Cloudflare Pages ist mit diesem GitHub-Repository verbunden und veröffentlicht Commits auf `main` automatisch. Das Projekt verwendet keinen Build-Befehl und veröffentlicht den Ordner `site/`; dafür sind keine API-Tokens oder GitHub-Secrets erforderlich.

Die Dashboard-Adresse ist `https://via-ai-training-dashboard.pages.dev`. Die Trainingsläufe selbst laufen weiterhin auf GitHub Actions (CPU); Cloudflare hostet die Anzeige und führt kein Modelltraining aus.
