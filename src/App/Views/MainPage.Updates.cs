using System.Text.Json;

namespace BraviaRemote;

public partial class MainPage
{
    private const string Repository = "vhnatovskyi/tv-controller";
    private static readonly HttpClient GitHub = new() { Timeout = TimeSpan.FromSeconds(6) };
    private bool releaseChecked;
    private Uri? updatePage;

    private async void CheckForRelease()
    {
        if (releaseChecked)
            return;
        releaseChecked = true;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd($"BraviaRemote/{AppInfo.Current.VersionString}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await GitHub.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return;

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (payload.RootElement.ValueKind != JsonValueKind.Object
                || !payload.RootElement.TryGetProperty("tag_name", out var tagProperty)
                || tagProperty.ValueKind != JsonValueKind.String
                || tagProperty.GetString() is not { Length: > 1 } tag
                || tag[0] != 'v'
                || !Version.TryParse(tag[1..], out var release)
                || release.Build < 0 || release.Revision >= 0
                || tag != $"v{release.ToString(3)}"
                || !Version.TryParse(AppInfo.Current.VersionString, out var current)
                || release <= new Version(current.Major, current.Minor, Math.Max(current.Build, 0)))
                return;

            updatePage = new Uri($"https://github.com/{Repository}/releases/tag/{tag}");
            UpdateLabel.Text = $"Доступна версія {release.ToString(3)}";
            UpdateBanner.IsVisible = true;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            // The remote remains usable without GitHub or internet access.
        }
    }

    private async void OnOpenRelease(object? sender, EventArgs e)
    {
        if (updatePage is { } page)
            await Launcher.Default.OpenAsync(page);
    }
}
