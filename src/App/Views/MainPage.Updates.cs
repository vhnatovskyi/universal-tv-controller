using System.Text.Json;

namespace BraviaRemote;

public partial class MainPage
{
    private const string Repository = "vhnatovskyi/tv-controller";
    private const string RepositoryUrl = "https://github.com/" + Repository;
    private static readonly HttpClient GitHub = new() { Timeout = TimeSpan.FromSeconds(6) };
    private bool releaseChecked;
    private string releaseStatus = "UpdatesIdle";
    private Version? updateVersion;

    internal static string DisplayVersion => typeof(MainPage).Assembly.GetName().Version!.ToString(3);

    private void CheckForRelease()
    {
        if (!releaseChecked)
            _ = LoadReleaseAsync();
    }

    private async Task LoadReleaseAsync()
    {
        if (releaseStatus == "UpdatesChecking")
            return;
        releaseChecked = true;
        releaseStatus = "UpdatesChecking";
        RefreshUpdateText();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd($"BraviaRemote/{DisplayVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await GitHub.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (payload.RootElement.ValueKind != JsonValueKind.Object
                || !payload.RootElement.TryGetProperty("tag_name", out var tagProperty)
                || tagProperty.ValueKind != JsonValueKind.String
                || tagProperty.GetString() is not { Length: > 1 } tag
                || tag[0] != 'v'
                || !Version.TryParse(tag[1..], out var release)
                || release.Build < 0 || release.Revision >= 0
                || tag != $"v{release.ToString(3)}")
                throw new JsonException("Invalid stable release version.");

            updateVersion = release > Version.Parse(DisplayVersion) ? release : null;
            releaseStatus = updateVersion is null ? "UpdatesCurrent" : "UpdateAvailable";
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            releaseStatus = "UpdatesUnavailable";
        }

        RefreshUpdateText();
    }

    private void RefreshUpdateText()
    {
        InstalledVersionLabel.Text = Localization.Text("InstalledVersion", DisplayVersion);
        CheckUpdatesButton.IsEnabled = releaseStatus != "UpdatesChecking";
        var latest = updateVersion?.ToString(3);
        UpdateBanner.IsVisible = latest is not null;
        if (latest is not null)
            UpdateLabel.Text = Localization.Text("UpdateAvailable", latest);
        ReleaseStatusLabel.Text = releaseStatus == "UpdateAvailable" && latest is not null
            ? Localization.Text("UpdateAvailable", latest)
            : Localization.Text(releaseStatus);
    }

    private async void OnCheckUpdates(object? sender, EventArgs e) => await LoadReleaseAsync();

    private async void OnOpenRelease(object? sender, EventArgs e)
    {
        if (updateVersion is { } release)
            await Launcher.Default.OpenAsync(new Uri($"{RepositoryUrl}/releases/tag/v{release.ToString(3)}"));
    }

    private async void OnOpenReleases(object? sender, EventArgs e) =>
        await Launcher.Default.OpenAsync(new Uri(RepositoryUrl + "/releases"));

    private async void OnOpenRepository(object? sender, EventArgs e) =>
        await Launcher.Default.OpenAsync(new Uri(RepositoryUrl));

    private async void OnOpenDeveloper(object? sender, EventArgs e) =>
        await Launcher.Default.OpenAsync(new Uri("https://github.com/vhnatovskyi"));
}
