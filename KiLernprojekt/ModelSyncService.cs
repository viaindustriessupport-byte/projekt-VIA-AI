using System.Net.Http;
using System.Security.Cryptography;
using System.IO;
using System.Text.Json;

namespace KiLernprojekt;

public sealed class ModelSyncService : IDisposable
{
    private readonly HttpClient client = new();

    public sealed class Manifest
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }

    public async Task<string?> SyncAsync(string manifestUrl, string targetFile)
    {
        using HttpResponseMessage manifestResponse = await client.GetAsync(manifestUrl);
        manifestResponse.EnsureSuccessStatusCode();

        string json = await manifestResponse.Content.ReadAsStringAsync();
        Manifest? manifest = JsonSerializer.Deserialize<Manifest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest is null ||
            manifest.DownloadUrl.Length == 0 ||
            manifest.Sha256.Length == 0)
        {
            throw new InvalidOperationException("Das Gewichts-Manifest ist ungültig.");
        }

        string temporaryFile = targetFile + ".download";
        using HttpResponseMessage modelResponse = await client.GetAsync(manifest.DownloadUrl);
        modelResponse.EnsureSuccessStatusCode();

        await using (FileStream stream = File.Create(temporaryFile))
        {
            await modelResponse.Content.CopyToAsync(stream);
        }

        string actualHash;
        await using (FileStream stream = File.OpenRead(temporaryFile))
        {
            byte[] hash = await SHA256.HashDataAsync(stream);
            actualHash = Convert.ToHexString(hash).ToLowerInvariant();
        }

        if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporaryFile);
            throw new InvalidOperationException("Prüfsumme stimmt nicht überein.");
        }

        File.Move(temporaryFile, targetFile, overwrite: true);
        return manifest.Version;
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
