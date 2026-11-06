using System.Globalization;
using System.Net.Sockets;

namespace BraviaRemote;

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
            VolumeValue.Text = "—";

        if (!volumeTrackingActive || busy || !RemoteButtons.IsVisible
            || CurrentProfile is not { } profile
            || !TelevisionProfiles.TryParseAddress(profile.Address, out var address))
            return;

        var polling = new CancellationTokenSource();
        volumePolling = polling;
        _ = TrackVolumeAsync(new BraviaClient(address), polling);
    }

    private async Task TrackVolumeAsync(BraviaClient tv, CancellationTokenSource polling)
    {
        var token = polling.Token;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        try
        {
            do
            {
                string value;
                try
                {
                    value = (await tv.GetVolumeAsync(token)).ToString(CultureInfo.CurrentCulture);
                }
                catch (Exception error) when (error is IOException or SocketException or OperationCanceledException)
                {
                    value = "—";
                }

                if (token.IsCancellationRequested)
                    return;
                VolumeValue.Text = value;
            }
            while (await timer.WaitForNextTickAsync(token));
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
}
