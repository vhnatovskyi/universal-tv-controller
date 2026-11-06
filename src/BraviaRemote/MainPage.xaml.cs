using System.Globalization;
using System.Net.Sockets;

namespace BraviaRemote;

public partial class MainPage : ContentPage
{
    private TelevisionProfiles profiles;
    private Guid? editingId;
    private bool busy;
    private bool volumeTrackingActive;
    private CancellationTokenSource? volumePolling;

    private TelevisionProfile? CurrentProfile =>
        ProfilePicker.SelectedIndex is var index && index >= 0 && index < profiles.Items.Count
            ? profiles.Items[index]
            : null;

    public MainPage()
    {
        profiles = TelevisionProfiles.Load(Preferences.Default);
        InitializeComponent();
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
        ProfileActions.IsVisible = RemoteButtons.IsVisible = profile is not null;
        DefaultLabel.IsVisible = isDefault;
        DefaultLabel.Text = isDefault ? "Профіль за замовчуванням" : "";
        DefaultButton.IsEnabled = !isDefault;
        StatusLabel.Text = profile is null ? "Додай профіль телевізора, щоб почати." : "";
        RefreshShortcuts();
        RestartVolumeTracking(resetValue: true);
    }

    private void OnAddProfile(object? sender, EventArgs e) => OpenEditor(null);

    private void OnEditProfile(object? sender, EventArgs e) => OpenEditor(CurrentProfile);

    private void OpenEditor(TelevisionProfile? profile)
    {
        editingId = profile?.Id;
        EditorTitle.Text = profile is null ? "Новий профіль" : "Редагування профілю";
        ProfileNameEntry.Text = profile?.Name;
        AddressEntry.Text = profile?.Address;
        EditorError.IsVisible = false;
        ProfileControls.IsVisible = RemoteButtons.IsVisible = StatusLabel.IsVisible = false;
        ProfileEditor.IsVisible = true;
        RestartVolumeTracking(resetValue: true);
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

    private void OnCancelProfile(object? sender, EventArgs e)
    {
        CloseEditor();
        RefreshRemote();
    }

    private void CloseEditor()
    {
        ProfileEditor.IsVisible = false;
        ProfileNameEntry.Text = AddressEntry.Text = null;
        ProfileControls.IsVisible = StatusLabel.IsVisible = true;
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
        await RunAsync(async tv => $"З’єднання є. Гучність: {await tv.GetVolumeAsync()}.");

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

    private async Task RunAsync(Func<BraviaClient, Task<string>> operation)
    {
        if (busy || CurrentProfile is not { } profile)
            return;

        if (!TelevisionProfiles.TryParseAddress(profile.Address, out var address))
        {
            StatusLabel.Text = "Перевір IP-адресу в налаштуваннях профілю.";
            return;
        }

        busy = true;
        RestartVolumeTracking(resetValue: false);
        ProfileControls.IsEnabled = RemoteButtons.IsEnabled = false;
        StatusLabel.Text = "Очікуємо відповідь телевізора…";

        try
        {
            StatusLabel.Text = await operation(new BraviaClient(address));
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Телевізор не відповів. Перевір мережу та керування IP.";
        }
        catch (SocketException)
        {
            StatusLabel.Text = "Не вдалося під’єднатися. Перевір мережу та просте управління IP.";
        }
        catch (EndOfStreamException)
        {
            StatusLabel.Text = "Телевізор закрив з’єднання до підтвердження команди.";
        }
        catch (IOException error)
        {
            StatusLabel.Text = error.Message;
        }
        finally
        {
            busy = false;
            ProfileControls.IsEnabled = RemoteButtons.IsEnabled = true;
            RestartVolumeTracking(resetValue: false);
        }
    }
}
