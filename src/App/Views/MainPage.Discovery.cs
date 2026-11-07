namespace UniversalTvRemote;

public partial class MainPage
{
    private DiscoveredTv[] discoveredDevices = [];
    private DiscoveredTv? selectedDevice;
    private CancellationTokenSource? discovery;

    private void OnChooseDevice(object? sender, EventArgs e) => OpenDiscovery();

    private void OpenDiscovery()
    {
        ShowScreen(Screen.Discovery);
        _ = FindDevicesAsync();
    }

    private void OnCancelDiscovery(object? sender, EventArgs e)
    {
        discovery?.Cancel();
        ShowScreen(selectedDevice is null ? editorReturnScreen : Screen.ProfileEditor);
    }

    private async void OnFindDevices(object? sender, EventArgs e) => await FindDevicesAsync();

    private void RefreshSelectedDevice()
    {
        SelectedDeviceButton.Text = selectedDevice?.Name ?? Localization.Text("SelectTv");
        SaveProfileButton.IsEnabled = selectedDevice is not null;
    }

    private void RefreshDeviceCards()
    {
        DeviceCards.Children.Clear();
        foreach (var device in discoveredDevices)
        {
            var button = new Button
            {
                Text = device.Name,
                ImageSource = "control_input.png",
                ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 14),
                HeightRequest = 68,
                CornerRadius = 12,
                BorderColor = Color.FromArgb("#3B526C"),
                BackgroundColor = Color.FromArgb("#1B293A")
            };
            button.SetDynamicResource(SemanticProperties.DescriptionProperty, "Loc.SelectTv");
            button.Clicked += (_, _) =>
            {
                selectedDevice = device;
                if (string.IsNullOrWhiteSpace(ProfileNameEntry.Text))
                    ProfileNameEntry.Text = device.Name;
                RefreshSelectedDevice();
                ShowScreen(Screen.ProfileEditor);
            };
            DeviceCards.Add(button);
        }
    }

    private async Task FindDevicesAsync()
    {
        discovery?.Cancel();
        using var searching = new CancellationTokenSource();
        discovery = searching;
        FindDevicesButton.IsEnabled = false;
        DiscoverySpinner.IsRunning = DiscoverySpinner.IsVisible = true;
        DiscoveryStatus.Text = Localization.Text("SearchingTvs");
        DeviceCards.Children.Clear();
        try
        {
#if IOS
            var found = await GoogleTvIosDiscovery.FindAsync(searching.Token);
#else
            var found = await GoogleTvDiscovery.FindAsync(searching.Token);
#endif
            if (searching.IsCancellationRequested)
                return;
            discoveredDevices = found;
            RefreshDeviceCards();
            DiscoveryStatus.Text = Localization.Text(found.Length > 0 ? "TvsFound" : "NoTvsFound", found.Length);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is System.Net.Sockets.SocketException or System.Net.NetworkInformation.NetworkInformationException)
        {
            DiscoveryStatus.Text = Localization.Text("NoTvsFound", 0);
        }
        finally
        {
            if (ReferenceEquals(discovery, searching))
            {
                discovery = null;
                FindDevicesButton.IsEnabled = true;
                DiscoverySpinner.IsRunning = DiscoverySpinner.IsVisible = false;
            }
        }
    }
}
