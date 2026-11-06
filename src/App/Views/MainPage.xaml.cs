using System.Globalization;
using System.Net.Sockets;

namespace BraviaRemote;

public partial class MainPage : ContentPage
{
    private TelevisionProfiles profiles;
    private Guid? editingId;
    private Screen screen;
    private Screen editorReturnScreen;
    private bool busy;
    private int toastVersion;
    private bool volumeTrackingActive;
    private CancellationTokenSource? volumePolling;

    private enum Screen { Remote, Settings, ProfileEditor, ShortcutEditor }

    private TelevisionProfile? CurrentProfile =>
        ProfilePicker.SelectedIndex is var index && index >= 0 && index < profiles.Items.Count
            ? profiles.Items[index]
            : null;

    public MainPage()
    {
        profiles = TelevisionProfiles.Load(Preferences.Default);
        InitializeComponent();
        VersionLabel.Text = $"BRAVIA Remote v{DisplayVersion}";
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

    private void RefreshRemote()
    {
        var profile = CurrentProfile;
        var isDefault = profile is not null && profile.Id == profiles.DefaultId;
        ProfileControls.IsVisible = screen == Screen.Settings;
        ProfileEditor.IsVisible = screen == Screen.ProfileEditor;
        ShortcutEditor.IsVisible = screen == Screen.ShortcutEditor;
        Onboarding.IsVisible = screen == Screen.Remote && profile is null;
        RemoteButtons.IsVisible = screen == Screen.Remote && profile is not null;
        ProfileActions.IsVisible = ShortcutSettingsButton.IsVisible = profile is not null;
        DefaultLabel.IsVisible = isDefault;
        DefaultLabel.Text = isDefault ? "Профіль за замовчуванням" : "";
        DefaultButton.IsEnabled = !isDefault;
        ToolbarTitle.Text = screen switch
        {
            Screen.Settings => "Налаштування",
            Screen.ProfileEditor => editingId is null ? "Новий профіль" : "Редагування профілю",
            Screen.ShortcutEditor => "Швидкі кнопки",
            _ => profile?.Name ?? "Пульт"
        };
        toastVersion++;
        Toast.IsVisible = false;
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
        var onRemote = screen == Screen.Remote;
        RemoteMenuButton.BackgroundColor = Color.FromArgb(onRemote ? "#304664" : "#18243A");
        SettingsMenuButton.BackgroundColor = Color.FromArgb(onRemote ? "#18243A" : "#304664");
        MenuOverlay.IsVisible = !MenuOverlay.IsVisible;
    }

    private void OnCloseMenu(object? sender, EventArgs e) => MenuOverlay.IsVisible = false;

    private void OnShowRemote(object? sender, EventArgs e) => ShowScreen(Screen.Remote);

    private void OnShowSettings(object? sender, EventArgs e) => ShowScreen(Screen.Settings);

    private void OnAddProfile(object? sender, EventArgs e) => OpenEditor(null);

    private void OnEditProfile(object? sender, EventArgs e) => OpenEditor(CurrentProfile);

    private void OpenEditor(TelevisionProfile? profile)
    {
        editorReturnScreen = screen;
        editingId = profile?.Id;
        EditorTitle.Text = profile is null ? "Новий профіль" : "Редагування профілю";
        ProfileNameEntry.Text = profile?.Name;
        AddressEntry.Text = profile?.Address;
        EditorError.IsVisible = false;
        ShowScreen(Screen.ProfileEditor);
    }

    private void OnSaveProfile(object? sender, EventArgs e)
    {
        var name = ProfileNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ShowEditorError("Введи назву профілю.");
            return;
        }

        if (!TelevisionProfiles.TryParseAddress(AddressEntry.Text, out var address))
        {
            ShowEditorError("Введи IPv4-адресу телевізора, наприклад 192.168.1.50.");
            return;
        }

        var index = profiles.Items.FindIndex(profile => profile.Id == editingId);
        var profile = index < 0
            ? new TelevisionProfile(Guid.NewGuid(), name, address.ToString())
            : profiles.Items[index] with { Name = name, Address = address.ToString() };
        if (index < 0)
            profiles.Items.Add(profile);
        else
            profiles.Items[index] = profile;
        profiles = profiles with { DefaultId = profiles.DefaultId ?? profile.Id };
        profiles.Save(Preferences.Default);
        CloseEditor();
        RefreshProfiles(profile.Id);
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
        await RunAsync(async tv => $"З’єднання встановлено. Гучність: {await tv.GetVolumeAsync()}.", showSuccess: true);

    private async void OnKey(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string code } button
            && int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var key))
        {
            await RunAsync(async tv =>
            {
                await tv.PressAsync(key);
                return $"Телевізор прийняв команду «{button.Text}».";
            });
        }
    }

    private async void OnPower(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string state } button)
        {
            await RunAsync(async tv =>
            {
                await tv.SetPowerAsync(state == "on");
                return $"Телевізор прийняв команду «{button.Text}».";
            });
        }
    }

    private async Task RunAsync(Func<BraviaClient, Task<string>> operation, bool showSuccess = false)
    {
        if (busy || CurrentProfile is not { } profile)
            return;

        if (!TelevisionProfiles.TryParseAddress(profile.Address, out var address))
        {
            ShowToast("Перевір IP-адресу в налаштуваннях профілю.", success: false);
            return;
        }

        busy = true;
        RestartVolumeTracking(resetValue: false);
        MenuButton.IsEnabled = RemoteButtons.IsEnabled = false;
        try
        {
            var message = await operation(new BraviaClient(address));
            if (showSuccess)
                ShowToast(message, success: true);
        }
        catch (OperationCanceledException)
        {
            ShowToast("Телевізор не відповів. Перевір мережу та керування IP.", success: false);
        }
        catch (SocketException)
        {
            ShowToast("Не вдалося під’єднатися. Перевір мережу та просте управління IP.", success: false);
        }
        catch (EndOfStreamException)
        {
            ShowToast("Телевізор закрив з’єднання до підтвердження команди.", success: false);
        }
        catch (IOException error)
        {
            ShowToast(error.Message, success: false);
        }
        finally
        {
            busy = false;
            MenuButton.IsEnabled = RemoteButtons.IsEnabled = true;
            RestartVolumeTracking(resetValue: false);
        }
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
