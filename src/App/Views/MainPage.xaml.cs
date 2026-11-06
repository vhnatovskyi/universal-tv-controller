using System.Globalization;
using System.Net.Sockets;
using static BraviaRemote.Localization;

namespace BraviaRemote;

public partial class MainPage : ContentPage
{
    private TelevisionProfiles profiles;
    private Guid? editingId;
    private Screen screen;
    private Screen editorReturnScreen;
    private bool busy;
    private Button? desktopMenu;
    private int toastVersion;
    private bool volumeTrackingActive;
    private CancellationTokenSource? volumePolling;

    private enum Screen { Remote, Settings, ProfileEditor, ShortcutEditor, Updates, About }

    private TelevisionProfile? CurrentProfile =>
        ProfilePicker.SelectedIndex is var index && index >= 0 && index < profiles.Items.Count
            ? profiles.Items[index]
            : null;

    public MainPage()
    {
        profiles = TelevisionProfiles.Load(Preferences.Default);
        InitializeComponent();
        Resources["RemoteButtonHeight"] = DeviceInfo.Platform == DevicePlatform.WinUI ? 36d : 44d;
        VersionLabel.Text = $"BRAVIA Remote v{DisplayVersion}";


        RefreshLanguageChoices();
        RefreshProfiles(profiles.DefaultId);
    }

    private void RefreshProfiles(Guid? selectedId)
    {
        ProfilePicker.ItemsSource = profiles.Items.Select(profile => profile.Name).ToList();
        ProfilePicker.SelectedIndex = profiles.Items.FindIndex(profile => profile.Id == selectedId);
        if (ProfilePicker.SelectedIndex < 0 && profiles.Items.Count > 0)
            ProfilePicker.SelectedIndex = 0;
        RefreshRemote();
    }

    private void OnProfileChanged(object? sender, EventArgs e) => RefreshRemote();

    private void OnRemoteViewportChanged(object? sender, EventArgs e) => ResizeRemoteButtons();

    private void ResizeRemoteButtons()
    {
        if (ScreenScroll.Height <= 0)
            return;

        var shortcutRows = (Shortcuts.Length + ShortcutColumns - 1) / ShortcutColumns;
        var minimum = DeviceInfo.Platform == DevicePlatform.WinUI ? 36d : 44d;
        // The remote has six control rows, 24px of outer padding, and 44px of borders and gaps.
        var available = ScreenScroll.Height - 68 - shortcutRows * 8;
        var height = Math.Clamp(Math.Floor(available / (6 + shortcutRows)), minimum, 58);
        if (!Resources.TryGetValue("RemoteButtonHeight", out var previous) || !Equals(previous, height))
            Resources["RemoteButtonHeight"] = height;
    }

    private void RefreshRemote()
    {
        var profile = CurrentProfile;
        var isDefault = profile is not null && profile.Id == profiles.DefaultId;
        ProfileControls.IsVisible = screen == Screen.Settings;
        ProfileEditor.IsVisible = screen == Screen.ProfileEditor;
        ShortcutEditor.IsVisible = screen == Screen.ShortcutEditor;
        UpdatesPanel.IsVisible = screen == Screen.Updates;
        AboutPanel.IsVisible = screen == Screen.About;
        Onboarding.IsVisible = screen == Screen.Remote && profile is null;
        RemoteButtons.IsVisible = screen == Screen.Remote && profile is not null;
        CheckButton.IsVisible = RemoteButtons.IsVisible;
        ProfileActions.IsVisible = ShortcutSettingsButton.IsVisible = ProfilePairingControls.IsVisible = profile is not null;

        DefaultLabel.IsVisible = isDefault;
        DefaultLabel.Text = isDefault ? Text("DefaultProfile") : "";
        DefaultButton.IsEnabled = !isDefault;

        ToolbarTitle.Text = screen switch
        {
            Screen.Settings => Text("Settings"),
            Screen.ProfileEditor => Text(editingId is null ? "NewProfile" : "EditProfile"),
            Screen.ShortcutEditor => Text("Shortcuts"),
            Screen.Updates => Text("Updates"),
            Screen.About => Text("About"),
            _ => profile?.Name ?? Text("Remote")
        };
        toastVersion++;
        Toast.IsVisible = false;
        AboutVersionLabel.Text = $"v{DisplayVersion} · .NET MAUI";
        RefreshUpdateText();
        RefreshShortcuts();
        RestartVolumeTracking(resetValue: true);
    }

    private void ShowScreen(Screen next)
    {

        screen = next;
        MenuOverlay.IsVisible = false;
        RefreshRemote();
        _ = ScreenScroll.ScrollToAsync(0, 0, animated: false);
    }

    private void OnToggleMenu(object? sender, EventArgs e)
    {
        RemoteMenuButton.BackgroundColor = MenuColor(screen == Screen.Remote);
        SettingsMenuButton.BackgroundColor = MenuColor(screen is Screen.Settings or Screen.ProfileEditor or Screen.ShortcutEditor);
        UpdatesMenuButton.BackgroundColor = MenuColor(screen == Screen.Updates);
        AboutMenuButton.BackgroundColor = MenuColor(screen == Screen.About);
        MenuOverlay.IsVisible = !MenuOverlay.IsVisible;
    }

    private static Color MenuColor(bool selected) => Color.FromArgb(selected ? "#243347" : "#141E2B");

    private void OnCloseMenu(object? sender, EventArgs e) => MenuOverlay.IsVisible = false;

    private void OnShowRemote(object? sender, EventArgs e) => ShowScreen(Screen.Remote);

    private void OnShowSettings(object? sender, EventArgs e) => ShowScreen(Screen.Settings);

    private async void OnShowUpdates(object? sender, EventArgs e)
    {
        ShowScreen(Screen.Updates);
        if (!releaseListLoaded)
            await LoadReleaseListAsync();
    }

    private void OnShowAbout(object? sender, EventArgs e) => ShowScreen(Screen.About);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (LanguagePicker.SelectedIndex < 0 || Application.Current is not { } app)
            return;

        var mode = LanguagePicker.SelectedIndex switch { 1 => "en", 2 => "uk", _ => "system" };
        if (mode == Mode)
            return;

        SetLanguage(app.Resources, mode);
        RefreshLanguageChoices();
        RefreshReleasePicker();
        RefreshRemote();
    }

    private void RefreshLanguageChoices()
    {
        LanguagePicker.ItemsSource = new[] { Text("SystemLanguage"), "English", "Українська" };
        LanguagePicker.SelectedIndex = Mode switch { "en" => 1, "uk" => 2, _ => 0 };
    }

    private void OnAddProfile(object? sender, EventArgs e) => OpenEditor(null);

    private void OnEditProfile(object? sender, EventArgs e) => OpenEditor(CurrentProfile);

    private void OpenEditor(TelevisionProfile? profile)
    {
        editorReturnScreen = screen;
        editingId = profile?.Id;
        ProfileNameEntry.Text = profile?.Name;
        AddressEntry.Text = profile?.Address;

        EditorError.IsVisible = false;
        ShowScreen(Screen.ProfileEditor);
    }

    private async void OnSaveProfile(object? sender, EventArgs e)
    {
        var name = ProfileNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ShowEditorError(Text("MissingProfileName"));
            return;
        }
        if (!TelevisionProfiles.TryParseAddress(AddressEntry.Text, out var address))
        {
            ShowEditorError(Text("InvalidAddress"));
            return;
        }
        var index = profiles.Items.FindIndex(profile => profile.Id == editingId);
        var profile = index < 0
            ? new TelevisionProfile(Guid.NewGuid(), name, address.ToString())
            : profiles.Items[index] with { Name = name, Address = address.ToString() };

        await RunGoogleTvAsync(async () =>
        {
            var pairing = await GoogleTvApps.PairAsync(address, incorrect =>
                DisplayPromptAsync(Text("PairTv"), Text(incorrect ? "IncorrectTvCode" : "EnterTvCode"),
                    Text("Confirm"), Text("Cancel"), maxLength: 6));
            if (pairing is null)
                return null;
            await SecureStorage.Default.SetAsync(PairingKey(profile.Id), $"{pairing.Fingerprint}|{pairing.Certificate}");
            if (index < 0)
                profiles.Items.Add(profile);
            else
                profiles.Items[index] = profile;
            profiles = profiles with { DefaultId = profiles.DefaultId ?? profile.Id };
            profiles.Save(Preferences.Default);
            CloseEditor();
            RefreshProfiles(profile.Id);
            return Text("TvPaired");
        });
    }
    private void ShowEditorError(string message)
    {
        EditorError.Text = message;
        EditorError.IsVisible = true;
    }

    private void OnCancelProfile(object? sender, EventArgs e) => CloseEditor();

    private void CloseEditor()
    {

        ProfileNameEntry.Text = AddressEntry.Text = null;
        ShowScreen(editorReturnScreen);
    }

    private void OnSetDefault(object? sender, EventArgs e)
    {
        if (CurrentProfile is not { } profile)
            return;
        profiles = profiles with { DefaultId = profile.Id };
        profiles.Save(Preferences.Default);
        RefreshRemote();
    }

    private async void OnCheck(object? sender, EventArgs e) =>
        await RunAsync(async tv => Text("Connected", await tv.GetVolumeAsync()));

    private async void OnKey(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string code }
            && int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var key))
        {

            await RunAsync(async tv =>
            {
                await tv.PressAsync(key);
                return null;
            });
        }
    }

    private async void OnPower(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string state })
        {

            await RunAsync(async tv =>
            {
                await tv.SetPowerAsync(state == "on");
                return null;
            });
        }
    }

    private async Task RunAsync(Func<TvControl, Task<string?>> operation)
    {
        if (busy || CurrentProfile is not { } profile)
            return;

        if (!TelevisionProfiles.TryParseAddress(profile.Address, out var address))
        {
            ShowToast(Text("CheckProfileAddress"), success: false);
            return;
        }

        busy = true;
        RestartVolumeTracking(resetValue: false);
        SetRemoteEnabled(false);
        try
        {
            var pairing = await TvControl.GetPairingAsync(profile.Id);
            if (pairing is null)
            {
                ShowToast(Text("PairTvFirst"), success: false);
                return;
            }
            var message = await operation(new TvControl(address, pairing));
            if (message is not null)
                ShowToast(message, success: true);
        }
        catch (OperationCanceledException)
        {
            ShowToast(Text("TvTimeout"), success: false);
        }
        catch (SocketException)
        {
            ShowToast(Text("ConnectionFailed"), success: false);
        }
        catch (EndOfStreamException)
        {
            ShowToast(Text("ConnectionClosed"), success: false);
        }
        catch (IOException)
        {
            ShowToast(Text("ConnectionFailed"), success: false);
        }
        catch (System.Security.Authentication.AuthenticationException)
        {
            ShowToast(Text("PairTvFirst"), success: false);
        }
        finally
        {
            busy = false;
            SetRemoteEnabled(true);
            RestartVolumeTracking(resetValue: false);
        }
    }

    private void SetRemoteEnabled(bool enabled)
    {
        MenuButton.IsEnabled = RemoteButtons.IsEnabled = CheckButton.IsEnabled = enabled;
        if (desktopMenu is not null)
            desktopMenu.IsEnabled = enabled;
    }

    private async void ShowToast(string message, bool success)
    {
        var version = ++toastVersion;
        Toast.BackgroundColor = Color.FromArgb(success ? "#195139" : "#742E38");
        Toast.Stroke = new SolidColorBrush(Color.FromArgb(success ? "#56A886" : "#EA8C96"));
        ToastIcon.Text = success ? "✓" : "!";
        ToastText.Text = message;
        Toast.IsVisible = true;
        SemanticScreenReader.Default.Announce(message);

        await Task.Delay(TimeSpan.FromSeconds(4));
        if (version == toastVersion)
            Toast.IsVisible = false;
    }
}
