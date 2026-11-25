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

## Öffentliches Status-Dashboard

`site/` enthält ein öffentlich lesbares Dashboard. Es bezieht Trainingsläufe und Releases über die öffentliche GitHub-API; es speichert keine Zugangsdaten. Das Training selbst lässt sich nur über GitHub Actions nach Anmeldung starten.

Nach dem Push in `main` stellt `.github/workflows/deploy-dashboard.yml` die Website mit GitHub Pages bereit. Falls Pages noch nicht aktiviert ist, öffne **Settings → Pages** und setze **Build and deployment → Source** auf **GitHub Actions**. Danach ist die Website unter `https://viaindustriessupport-byte.github.io/projekt-VIA-AI/` erreichbar.
