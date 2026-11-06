namespace BraviaRemote;

public partial class MainPage
{
    private DiscoveredTv[] discoveredDevices = [];
    private DiscoveredTv? selectedDevice;
    private CancellationTokenSource? discovery;

    private async void OnFindDevices(object? sender, EventArgs e) => await FindDevicesAsync();

    private void OnDeviceSelected(object? sender, EventArgs e)
    {
        selectedDevice = discoveredDevices.ElementAtOrDefault(DevicePicker.SelectedIndex);
        SaveProfileButton.IsEnabled = selectedDevice is not null;
        if (selectedDevice is not null && string.IsNullOrWhiteSpace(ProfileNameEntry.Text))
            ProfileNameEntry.Text = selectedDevice.Name;
    }

    private void RefreshDevicePicker()
    {
        var address = selectedDevice?.Address;
        DevicePicker.ItemsSource = discoveredDevices.Select(device => device.Name).ToList();
        DevicePicker.SelectedIndex = Array.FindIndex(discoveredDevices, device => device.Address == address);
        if (DevicePicker.SelectedIndex < 0 && discoveredDevices.Length == 1)
            DevicePicker.SelectedIndex = 0;
        OnDeviceSelected(null, EventArgs.Empty);
    }

    private async Task FindDevicesAsync()
    {
        if (discovery is not null)
            return;
        using var searching = new CancellationTokenSource();
        discovery = searching;
        FindDevicesButton.IsEnabled = false;
        DiscoveryStatus.Text = Localization.Text("SearchingTvs");
        try
        {
#if IOS
            var found = await GoogleTvIosDiscovery.FindAsync(searching.Token);
#else
            var found = await GoogleTvDiscovery.FindAsync(searching.Token);
#endif
            if (searching.IsCancellationRequested)
                return;
            discoveredDevices = selectedDevice is not null
                ? found.Append(selectedDevice).DistinctBy(device => device.Address).ToArray()
                : found;
            RefreshDevicePicker();
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
            discovery = null;
            FindDevicesButton.IsEnabled = true;
        }
    }
}
