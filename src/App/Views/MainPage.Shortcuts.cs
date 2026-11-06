using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using static BraviaRemote.Localization;

namespace BraviaRemote;

public partial class MainPage
{
    private const int MaxShortcuts = 6;
    private const int HomeKey = 6;
    private sealed record TvApp(string Name, string Package);

    private static readonly TvApp[] TvApps =
    [
        new("YouTube", "com.google.android.youtube.tv"),
        new("Netflix", "com.netflix.ninja"),
        new("Kyivstar TV", "com.kyivstar.tv.androidtv")
    ];

    private QuickButton[] Shortcuts => CurrentProfile?.Shortcuts ?? [];

    private void RefreshShortcuts()
    {
        QuickButtons.Children.Clear();
        QuickButtons.RowDefinitions.Clear();
        var shortcuts = Shortcuts;
        QuickButtons.IsVisible = shortcuts.Length > 0;
        for (var index = 0; index < shortcuts.Length; index++)
        {
            var shortcut = shortcuts[index];
            if (index % 3 == 0)
                QuickButtons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var button = new Button { Text = shortcut.Name };
            button.Clicked += async (_, _) => await LaunchShortcutAsync(shortcut);
            QuickButtons.Add(button, index % 3, index / 3);
        }
    }

    private async Task LaunchShortcutAsync(QuickButton shortcut)
    {
        if (CurrentProfile is not { } profile || busy)
            return;

        if (shortcut.AppPackage is { } package)
        {
            await RunGoogleTvAsync(async () =>
            {
                var saved = await SecureStorage.Default.GetAsync(PairingKey(profile.Id));
                if (saved?.Split('|', 2) is not [var fingerprint, var certificate])
                {
                    ShowToast(Text("PairTvFirst"), success: false);
                    return null;
                }

                if (!TelevisionProfiles.TryParseAddress(profile.Address, out var address))
                    throw new IOException("Missing TV address.");
                await GoogleTvApps.LaunchAsync(address, new GoogleTvPairing(fingerprint, certificate), package);
                return Text("AppLaunchSent", shortcut.Name);
            });
            return;
        }

        // Retain quick buttons saved before direct app launches were added.
        if (shortcut.Keys is { Length: > 0 } keys)
        {
            await RunAsync(async tv =>
            {
                for (var step = 0; step < keys.Length; step++)
                {
                    await tv.PressAsync(keys[step]);
                    if (step + 1 < keys.Length)
                        await Task.Delay(keys[step] == HomeKey ? 1400 : 350);
                }
                return Text("ShortcutDone", shortcut.Name);
            });
        }
    }

    private void OnOpenShortcuts(object? sender, EventArgs e)
    {
        if (CurrentProfile is null)
            return;

        AppPicker.ItemsSource = TvApps.Select(app => app.Name).ToList();
        RefreshShortcutPicker(-1);
        ShowScreen(Screen.ShortcutEditor);
    }

    private void OnCloseShortcuts(object? sender, EventArgs e) => ShowScreen(Screen.Settings);

    private void OnNewShortcut(object? sender, EventArgs e)
    {
        ShortcutPicker.SelectedIndex = -1;
        LoadShortcutFields();
    }

    private void OnShortcutSelected(object? sender, EventArgs e) => LoadShortcutFields();

    private void OnAppSelected(object? sender, EventArgs e)
    {
        if (AppPicker.SelectedIndex >= 0 && string.IsNullOrWhiteSpace(ShortcutName.Text))
            ShortcutName.Text = TvApps[AppPicker.SelectedIndex].Name;
    }

    private void RefreshShortcutPicker(int index)
    {
        ShortcutPicker.ItemsSource = Shortcuts.Select(shortcut => shortcut.Name).ToList();
        ShortcutPicker.SelectedIndex = index;
        LoadShortcutFields();
    }

    private void LoadShortcutFields()
    {
        var shortcut = Shortcuts.ElementAtOrDefault(ShortcutPicker.SelectedIndex);
        ShortcutName.Text = shortcut?.Name;
        AppPicker.SelectedIndex = Array.FindIndex(TvApps, app => app.Package == shortcut?.AppPackage);
        RemoveShortcutButton.IsEnabled = shortcut is not null;
        ShortcutError.IsVisible = false;
    }

    private void OnSaveShortcut(object? sender, EventArgs e)
    {
        if (AppPicker.SelectedIndex < 0)
        {
            ShowShortcutError(Text("SelectAnApp"));
            return;
        }

        var app = TvApps[AppPicker.SelectedIndex];
        var name = ShortcutName.Text?.Trim();
        var shortcuts = Shortcuts.ToList();
        var index = ShortcutPicker.SelectedIndex;
        if (index < 0 && shortcuts.Count >= MaxShortcuts)
        {
            ShowShortcutError(Text("ShortcutLimit", MaxShortcuts));
            return;
        }

        var shortcut = new QuickButton(string.IsNullOrEmpty(name) ? app.Name : name, AppPackage: app.Package);
        if (index < 0)
        {
            index = shortcuts.Count;
            shortcuts.Add(shortcut);
        }
        else
        {
            shortcuts[index] = shortcut;
        }
        SaveShortcuts(shortcuts, index);
    }

    private void OnRemoveShortcut(object? sender, EventArgs e)
    {
        var index = ShortcutPicker.SelectedIndex;
        if (index < 0)
            return;

        var shortcuts = Shortcuts.ToList();
        shortcuts.RemoveAt(index);
        SaveShortcuts(shortcuts, -1);
    }

    private void SaveShortcuts(List<QuickButton> shortcuts, int selectedIndex)
    {
        var index = ProfilePicker.SelectedIndex;
        profiles.Items[index] = profiles.Items[index] with { Shortcuts = shortcuts.ToArray() };
        profiles.Save(Preferences.Default);
        RefreshShortcutPicker(selectedIndex);
    }

    private void ShowShortcutError(string message)
    {
        ShortcutError.Text = message;
        ShortcutError.IsVisible = true;
    }

    private static string PairingKey(Guid id) => $"google-tv-pair-{id:N}";

    private async void OnPairTv(object? sender, EventArgs e)
    {
        if (CurrentProfile is not { } profile || !TelevisionProfiles.TryParseAddress(profile.Address, out var address))
            return;

        await RunGoogleTvAsync(async () =>
        {
            var pairing = await GoogleTvApps.PairAsync(address, incorrect =>
                DisplayPromptAsync(Text("PairTv"), Text(incorrect ? "IncorrectTvCode" : "EnterTvCode"),
                    Text("Confirm"), Text("Cancel"), maxLength: 6));
            if (pairing is null)
                return null;
            await SecureStorage.Default.SetAsync(PairingKey(profile.Id), $"{pairing.Fingerprint}|{pairing.Certificate}");
            return Text("TvPaired");
        });
    }

    private async Task RunGoogleTvAsync(Func<Task<string?>> operation)
    {
        if (busy)
            return;

        busy = true;
        SetRemoteEnabled(false);
        ShortcutEditor.IsEnabled = ProfileControls.IsEnabled = false;
        RestartVolumeTracking(resetValue: false);
        try
        {
            if (await operation() is { } message)
                ShowToast(message, success: true);
        }
        catch (Exception error) when (error is IOException or SocketException or AuthenticationException
            or CryptographicException or FormatException or InvalidOperationException or OperationCanceledException)
        {
            ShowToast(Text("GoogleTvFailed"), success: false);
        }
        finally
        {
            busy = false;
            ShortcutEditor.IsEnabled = ProfileControls.IsEnabled = true;
            SetRemoteEnabled(true);
            RestartVolumeTracking(resetValue: false);
        }
    }
}
