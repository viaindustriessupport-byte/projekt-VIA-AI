# Eigenes KI-Labor

Dieses Repository enthält eine kleine, selbst geschriebene C#-Absichtsklassifikation und einen Trainer. Es ist **kein** großes Sprachmodell und erzeugt keine beliebigen Chat- oder Coding-Antworten.

## Lokaler Start

```powershell
dotnet run --project KiLernprojekt\KiLernprojekt.csproj
```

Die App lädt `KiLernprojekt\eigene-gewichte.json`. Die NVIDIA-GPU wird erkannt; das aktuelle Training selbst läuft im C#-Trainer auf der CPU.

## Cloud-Training und Updates

`.github/workflows/ai-training.yml` startet täglich um 03:00 UTC oder manuell über GitHub Actions. Der Workflow trainiert das kleine eigene Modell mit den eingebauten Beispielen und dem C#-Quelltext, erstellt eine SHA-256-Prüfsumme und veröffentlicht die Gewichte als GitHub Release.

Beim App-Start liest die Anwendung `KiLernprojekt\sync-config.json`, lädt das Manifest des letzten Releases und übernimmt Gewichte nur, wenn die Prüfsumme stimmt. Der Sync nutzt den öffentlichen GitHub-Release-Endpunkt; Zugangsdaten sind nicht in der App gespeichert.

Kostenlose GitHub-Runner sind zeit- und ressourcenbegrenzt. Das tägliche Training ist kein kontinuierliches Lernen und macht das Modell nicht zu einem allgemeinen Sprachmodell.

## Öffentliches Status-Dashboard und Cloudflare

`site/` enthält ein öffentlich lesbares Dashboard. Es bezieht Trainingsläufe und Releases über die öffentliche GitHub-API, aktualisiert die Anzeige alle fünf Minuten und speichert keine Zugangsdaten. Das Training läuft täglich sowie nach Änderungen an Trainer- oder C#-Quelldateien. Manuelles Starten ist über den Button **Training verwalten** möglich und erfordert eine GitHub-Anmeldung.

Die Bereitstellung nach Cloudflare Pages läuft über `.github/workflows/deploy-dashboard.yml`. Für die erstmalige Einrichtung:

1. Erstelle in Cloudflare unter **Workers & Pages** ein Pages-Projekt als **Direct Upload** mit dem Projektnamen `via-ai-training-dashboard`.
2. Erstelle einen Cloudflare API-Token mit der Berechtigung **Account → Cloudflare Pages → Edit**.
3. Lege im GitHub-Repository unter **Settings → Secrets and variables → Actions** zwei Repository-Secrets an: `CLOUDFLARE_API_TOKEN` (der Token) und `CLOUDFLARE_ACCOUNT_ID` (die Account-ID aus dem Cloudflare-Dashboard).
4. Push auf `main` oder starte **Deploy public status dashboard to Cloudflare** unter GitHub Actions.

Die Dashboard-Adresse ist `https://via-ai-training-dashboard.pages.dev`. Die Trainingsläufe selbst laufen weiterhin auf GitHub Actions (CPU); Cloudflare hostet die Anzeige und führt kein Modelltraining aus.
