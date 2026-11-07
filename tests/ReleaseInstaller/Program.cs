using System.Net;
using System.Security.Cryptography;
using BraviaRemote;

var version = new Version(1, 2, 3);
const string name = "UniversalTvController-Setup-1.2.3.exe";
byte[] content = [1, 2, 3, 4];
var hash = Convert.ToHexString(SHA256.HashData(content));
var url = new Uri("https://github.com/example/repo/releases/download/v1.2.3/");

await CheckAsync($"{hash.ToLowerInvariant()}  {name}\r\n", content, succeeds: true);
await CheckAsync($"{hash} *{name}\n", content, succeeds: true);
await CheckAsync($"{hash}  {name}", [9], succeeds: false);
await CheckAsync($"{hash}  another.exe", content, succeeds: false);
await CheckAsync($"invalid  {name}", content, succeeds: false);
await CheckAsync($"{new string('z', 64)}  {name}", content, succeeds: false);
await CheckAsync($"{hash}  {name}\n{hash}  {name}", content, succeeds: false);
await CheckAsync($"{hash}  {name}", content, succeeds: false, status: HttpStatusCode.NotFound);
await CheckAsync($"{hash}  {name}", content, succeeds: false, cancel: true);
Console.WriteLine("Verified downloads, checksum mismatch/missing/malformed/duplicate, HTTP failure, cancellation, and partial-file cleanup passed.");

async Task CheckAsync(string checksums, byte[] installer, bool succeeds,
    HttpStatusCode status = HttpStatusCode.OK, bool cancel = false)
{
    var directory = Path.Combine(Path.GetTempPath(), $"UniversalTvController-update-test-{Guid.NewGuid():N}");
    using var cancellation = new CancellationTokenSource();
    using var client = new HttpClient(new FakeReleaseHandler(request =>
    {
        if (request.RequestUri == new Uri(url, "SHA256SUMS.txt"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(checksums) };
        if (request.RequestUri != new Uri(url, name))
            throw new Exception("Unexpected download URL");
        if (cancel)
        {
            cancellation.Cancel();
            cancellation.Token.ThrowIfCancellationRequested();
        }
        return new HttpResponseMessage(status) { Content = new ByteArrayContent(installer) };
    }));
    try
    {
        try
        {
            var path = await ReleaseInstaller.DownloadAsync(client, url, version, directory, cancellation.Token);
            if (!succeeds || path != Path.Combine(directory, name) || !File.ReadAllBytes(path).SequenceEqual(content))
                throw new Exception("Unexpected installer result");
        }
        catch (Exception error) when (!succeeds && error is IOException or HttpRequestException or OperationCanceledException)
        {
            if (Directory.Exists(directory) && Directory.EnumerateFiles(directory).Any())
                throw new Exception("A failed download left an executable or partial file behind");
            return;
        }
        if (!succeeds)
            throw new Exception("Invalid update was accepted");
        if (File.Exists(Path.Combine(directory, name + ".download")))
            throw new Exception("Partial file was left behind");
    }
    finally
    {
        // Only remove the two exact files created by this test; no recursive deletion.
        if (Directory.Exists(directory))
        {
            File.Delete(Path.Combine(directory, name));
            File.Delete(Path.Combine(directory, name + ".download"));
            Directory.Delete(directory);
        }
    }
}

sealed class FakeReleaseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(respond(request));
}
