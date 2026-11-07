using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using static BraviaRemote.Localization;

namespace BraviaRemote;

public partial class MainPage
{
    private const string Repository = "vhnatovskyi/universal-tv-controller";
    private const string RepositoryUrl = "https://github.com/" + Repository;
    private static readonly HttpClient GitHub = new() { Timeout = TimeSpan.FromSeconds(6) };
    private sealed record PublishedRelease(Version Version, DateTimeOffset? Published, string Notes)
    {
        public string Tag => $"v{Version.ToString(3)}";
    }

    private bool releaseChecked;
    private bool releaseListLoaded;
    private string releaseStatus = "UpdatesIdle";
    private Version? updateVersion;
    private PublishedRelease[] releases = [];
    private CancellationTokenSource? updateDownload;

    internal static string DisplayVersion => typeof(MainPage).Assembly.GetName().Version!.ToString(3);

    private async void CheckForRelease()
    {
        if (releaseChecked)
            return;
        releaseChecked = true;
        try
        {
            var latest = await FetchReleasesAsync(latestOnly: true);
            if (!releaseListLoaded)
                SetUpdateVersion(latest.FirstOrDefault()?.Version);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or XmlException or OperationCanceledException)
        {
            // The remote can start without internet access.
        }
    }

    private async Task LoadReleaseListAsync()
    {
        if (releaseStatus == "UpdatesChecking" || updateDownload is not null)
            return;

        releaseStatus = "UpdatesChecking";
        RefreshUpdateText();
        try
        {
            releases = await FetchReleasesAsync(latestOnly: false);
            releaseListLoaded = true;
            releaseStatus = releases.Length == 0 ? "UpdatesEmpty" : "";
            RefreshReleasePicker();
            SetUpdateVersion(releases.FirstOrDefault()?.Version);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or XmlException or OperationCanceledException)
        {
            releaseStatus = "UpdatesUnavailable";
        }
        RefreshUpdateText();
    }

    private static async Task<PublishedRelease[]> FetchReleasesAsync(bool latestOnly)
    {
        var endpoint = latestOnly ? "/latest" : "?per_page=20";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases{endpoint}");
        request.Headers.UserAgent.ParseAdd($"UniversalTvController/{DisplayVersion}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await GitHub.SendAsync(request);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            return await FetchReleaseFeedAsync();
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (latestOnly)
            return ReadRelease(payload.RootElement) is { } latest ? [latest] : [];
        if (payload.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Invalid releases list.");
        return SortReleases(payload.RootElement.EnumerateArray().Select(ReadRelease));
    }

    private static async Task<PublishedRelease[]> FetchReleaseFeedAsync()
    {
        var xml = await GitHub.GetStringAsync(RepositoryUrl + "/releases.atom");
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = 1_000_000
        });
        XNamespace atom = "http://www.w3.org/2005/Atom";
        return SortReleases(XDocument.Load(reader).Descendants(atom + "entry").Select(entry =>
        {
            var id = entry.Element(atom + "id")?.Value;
            var tag = id?[(id.LastIndexOf('/') + 1)..];
            // GitHub also includes bare tags in this feed. Published releases use the title set by our workflow.
            var title = entry.Element(atom + "title")?.Value;
            if (!TryReadVersion(tag, out var version)
                || (title != $"Universal TV Controller {tag}" && title != $"BRAVIA Remote {tag}"))
                return null;
            var date = DateTimeOffset.TryParse(entry.Element(atom + "updated")?.Value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var published) ? published : (DateTimeOffset?)null;
            var html = entry.Element(atom + "content")?.Value ?? "";
            var notes = Regex.Replace(html, "<h3[^>]*>(.*?)</h3>", "### $1\n", RegexOptions.Singleline);
            notes = Regex.Replace(notes, "<li[^>]*>", "- ", RegexOptions.IgnoreCase);
            notes = Regex.Replace(notes, "</(?:li|p|h2)>", "\n", RegexOptions.IgnoreCase);
            notes = WebUtility.HtmlDecode(Regex.Replace(notes, "<[^>]+>", ""));
            return new PublishedRelease(version, date, notes);
        }));
    }

    private static PublishedRelease[] SortReleases(IEnumerable<PublishedRelease?> entries) =>
        entries.OfType<PublishedRelease>().OrderByDescending(release => release.Version).ToArray();

    private static PublishedRelease? ReadRelease(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object
            || data.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True
            || data.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True
            || !data.TryGetProperty("tag_name", out var tagProperty)
            || tagProperty.ValueKind != JsonValueKind.String
            || !TryReadVersion(tagProperty.GetString(), out var version))
            return null;

        var published = data.TryGetProperty("published_at", out var date)
            && date.ValueKind == JsonValueKind.String && date.TryGetDateTimeOffset(out var timestamp)
            ? timestamp : (DateTimeOffset?)null;
        var notes = data.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
            ? body.GetString() ?? "" : "";
        return new PublishedRelease(version, published, notes);
    }

    private static bool TryReadVersion(string? tag, out Version version)
    {
        if (tag is { Length: > 1 } && tag[0] == 'v' && Version.TryParse(tag[1..], out var parsed)
            && parsed.Build >= 0 && parsed.Revision < 0 && tag == $"v{parsed.ToString(3)}")
        {
            version = parsed;
            return true;
        }
        version = new Version();
        return false;
    }

    private void SetUpdateVersion(Version? release)
    {
        updateVersion = release is not null && release > Version.Parse(DisplayVersion) ? release : null;
        RefreshUpdateText();
    }

    private void RefreshUpdateText()
    {
        InstalledVersionLabel.Text = Text("InstalledVersion", DisplayVersion);
        var downloading = updateDownload is not null;
        CheckUpdatesButton.IsEnabled = releaseStatus != "UpdatesChecking" && !downloading;
        InstallUpdateButton.IsEnabled = updateVersion is not null && CheckUpdatesButton.IsEnabled;
        InstallUpdateButton.Text = Text(OperatingSystem.IsWindows() ? "InstallLatestUpdate" : "OpenLatestRelease");
        UpdateInstallHelpLabel.IsVisible = OperatingSystem.IsWindows();
        UpdateDownloadSpinner.IsVisible = UpdateDownloadSpinner.IsRunning = downloading;
        CancelUpdateButton.IsVisible = downloading;
        LatestVersionLabel.Text = updateVersion is { } available ? Text("UpdateAvailable", available.ToString(3))
            : releaseListLoaded && releaseStatus.Length == 0 ? Text("UpdatesCurrent") : "";
        ReleaseStatusLabel.IsVisible = releaseStatus.Length > 0;
        ReleaseStatusLabel.Text = releaseStatus.Length > 0 ? Text(releaseStatus) : "";
        var latest = updateVersion?.ToString(3);
        UpdateBanner.IsVisible = screen == Screen.Remote && latest is not null;
        if (latest is not null)
            UpdateLabel.Text = Text("UpdateAvailable", latest);
    }

    private void RefreshReleasePicker()
    {
        var selected = ReleasePicker.SelectedIndex;
        ReleasePicker.ItemsSource = releases.Select(release => release.Published is { } date
            ? $"{release.Tag} · {Date(date)}" : release.Tag).ToList();
        if (releases.Length > 0)
            ReleasePicker.SelectedIndex = Math.Clamp(selected, 0, releases.Length - 1);
        OnReleaseSelected(null, EventArgs.Empty);
    }

    private void OnReleaseSelected(object? sender, EventArgs e)
    {
        var release = releases.ElementAtOrDefault(ReleasePicker.SelectedIndex);
        SelectedReleaseButton.IsEnabled = release is not null;
        ReleaseNotesLabel.Text = release is null ? "" : LocalizedNotes(release.Notes);
    }

    private static string LocalizedNotes(string notes)
    {
        const string English = "### English";
        const string Ukrainian = "### Українська";
        var en = notes.IndexOf(English, StringComparison.Ordinal);
        var uk = notes.IndexOf(Ukrainian, StringComparison.Ordinal);
        if (en < 0 || uk <= en)
            return notes.Trim();
        return IsUkrainian ? notes[(uk + Ukrainian.Length)..].Trim() : notes[(en + English.Length)..uk].Trim();
    }

    private async void OnCheckUpdates(object? sender, EventArgs e) => await LoadReleaseListAsync();

    private void OnCancelUpdate(object? sender, EventArgs e) => updateDownload?.Cancel();

    private async void OnInstallUpdate(object? sender, EventArgs e)
    {
        if (updateVersion is not { } version || updateDownload is not null || releaseStatus == "UpdatesChecking")
            return;
#if WINDOWS
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        updateDownload = cancellation;
        UpdateInstallStatusLabel.IsVisible = true;
        UpdateInstallStatusLabel.Text = Text("UpdateDownloading");
        RefreshUpdateText();
        try
        {
            using var downloadClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            var directory = Path.Combine(FileSystem.CacheDirectory, "updates", Guid.NewGuid().ToString("N"));
            var releaseUrl = new Uri($"{RepositoryUrl}/releases/download/v{version.ToString(3)}/");
            var installer = await ReleaseInstaller.DownloadAsync(downloadClient, releaseUrl, version, directory, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installer)
            {
                UseShellExecute = true,
                ArgumentList = { $"/DIR={AppContext.BaseDirectory}" }
            });
            if (process is null)
                throw new IOException("Could not start the update installer.");
            process.Dispose();
            Application.Current?.Quit();
        }
        catch (OperationCanceledException)
        {
            UpdateInstallStatusLabel.Text = Text("UpdateCancelled");
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException)
        {
            UpdateInstallStatusLabel.Text = Text("UpdateInstallFailed");
        }
        finally
        {
            updateDownload = null;
            RefreshUpdateText();
        }
#else
        await OpenTagAsync($"v{version.ToString(3)}");
#endif
    }

    private async void OnOpenRelease(object? sender, EventArgs e)
    {
        if (updateVersion is { } release)
            await OpenTagAsync($"v{release.ToString(3)}");
    }

    private async void OnOpenSelectedRelease(object? sender, EventArgs e)
    {
        if (releases.ElementAtOrDefault(ReleasePicker.SelectedIndex) is { } release)
            await OpenTagAsync(release.Tag);
    }

    private static Task<bool> OpenTagAsync(string tag) =>
        Launcher.Default.OpenAsync(new Uri($"{RepositoryUrl}/releases/tag/{tag}"));

    private async void OnOpenRepository(object? sender, EventArgs e) =>
        await Launcher.Default.OpenAsync(new Uri(RepositoryUrl));

    private async void OnOpenLinkedIn(object? sender, EventArgs e) =>
        await Launcher.Default.OpenAsync(new Uri("https://www.linkedin.com/in/vladyslav-hnatovskyi/"));

    private async void OnOpenEmail(object? sender, EventArgs e) =>
        await Launcher.Default.OpenAsync(new Uri("mailto:vhnatovskyi@hotmail.com"));
}
