using System.Globalization;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;

namespace UniversalTvRemote;

public partial class MainPage
{
    protected override void OnAppearing()
    {
        base.OnAppearing();
        SetVolumeTracking(true);
        CheckForRelease();
    }

    protected override void OnDisappearing()
    {
        SetVolumeTracking(false);
        base.OnDisappearing();
    }

    internal void SetVolumeTracking(bool active)
    {
        if (!active)
            CancelVolumeChange();
        if (volumeTrackingActive == active)
            return;
        volumeTrackingActive = active;
        RestartVolumeTracking(resetValue: true);
    }

    private void RestartVolumeTracking(bool resetValue)
    {
        var previous = volumePolling;
        volumePolling = null;
        previous?.Cancel();
        VolumeChip.IsVisible = RemoteButtons.IsVisible;
        if (resetValue)
        {
            VolumeValue.Text = "—";
            ActiveAppChip.IsVisible = false;
            UpdatePowerState(null);
        }

        if (!volumeTrackingActive || busy || !RemoteButtons.IsVisible || CurrentProfile is not { } profile)
            return;

        var polling = new CancellationTokenSource();
        volumePolling = polling;
        _ = TrackTvAsync(profile, polling);
    }

    private async Task TrackTvAsync(TelevisionProfile profile, CancellationTokenSource polling)
    {
        var token = polling.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var pairing = await TvControl.GetPairingAsync(profile.Id);
                    if (pairing is null || !TelevisionProfiles.TryParseAddress(profile.Address, out var address))
                        return;
                    await GoogleTvApps.WatchAppAsync(address, pairing,
                        package => MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (!token.IsCancellationRequested)
                                ShowActiveApp(package);
                        }),
                        volume => MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (!token.IsCancellationRequested)
                                VolumeValue.Text = volume.ToString(CultureInfo.CurrentCulture);
                        }),
                        power => MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (!token.IsCancellationRequested)
                                UpdatePowerState(power);
                        }), token);
                }
                catch (Exception error) when (error is IOException or SocketException or AuthenticationException
                    or CryptographicException or FormatException or InvalidOperationException or OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                        return;
                    VolumeValue.Text = "—";
                    ActiveAppChip.IsVisible = false;
                    UpdatePowerState(null);
                }
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(volumePolling, polling))
                volumePolling = null;
            polling.Dispose();
        }
    }

    private void ShowActiveApp(string package)
    {
        var (icon, name) = package switch
        {
            "com.google.android.youtube.tv" => ("active_youtube.png", "YouTube"),
            "com.netflix.ninja" => ("active_netflix.png", "Netflix"),
            "com.kyivstar.tv.androidtv" => ("active_kyivstar.png", "Kyivstar TV"),
            _ => ("active_app.png", package)
        };
        ActiveAppIcon.Source = icon;
        SemanticProperties.SetDescription(ActiveAppChip, name);
        ToolTipProperties.SetText(ActiveAppChip, name);
        ActiveAppChip.IsVisible = true;
    }
}
