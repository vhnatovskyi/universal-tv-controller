using System.Security.Cryptography;

namespace UniversalTvRemote;

internal static class ReleaseInstaller
{
    internal static async Task<string> DownloadAsync(HttpClient client, Uri releaseUrl, Version version,
        string directory, CancellationToken token)
    {
        var name = $"UniversalTvController-Setup-{version.ToString(3)}.exe";
        var checksums = await client.GetStringAsync(new Uri(releaseUrl, "SHA256SUMS.txt"), token);
        var entries = checksums.Split('\n').Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == name).ToArray();
        if (entries.Length != 1 || entries[0][0].Length != 64 || !entries[0][0].All(Uri.IsHexDigit))
            throw new IOException("The release has no valid installer checksum.");

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        // Never expose a partially downloaded executable to the installer launcher.
        var partial = path + ".download";
        try
        {
            using var response = await client.GetAsync(new Uri(releaseUrl, name), HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var destination = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 81920, useAsync: true))
            {
                await source.CopyToAsync(destination, token);
                destination.Position = 0;
                var actual = await SHA256.HashDataAsync(destination, token);
                if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(entries[0][0])))
                    throw new IOException("The update installer checksum does not match.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(partial, path);
            return path;
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }
    }
}
