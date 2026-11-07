using System.Globalization;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using static BraviaRemote.Localization;

namespace BraviaRemote;

public partial class MainPage
{
    private CancellationTokenSource? volumePress;
    private CancellationTokenSource? volumeChange;
    private Button? repeatedVolumeButton;

    internal void CancelVolumeChange()
    {
        volumePress?.Cancel();
        volumeChange?.Cancel();
    }

    private void OnVolumeReleased(object? sender, EventArgs e) => volumePress?.Cancel();

    private void OnVolumeClicked(object? sender, EventArgs e)
    {
        if (ReferenceEquals(repeatedVolumeButton, sender))
        {
            repeatedVolumeButton = null;
            return;
        }
        OnKey(sender, e);
    }

    private async void OnVolumePressed(object? sender, EventArgs e)
    {
        if (busy || sender is not Button { CommandParameter: string code } button)
            return;
        volumePress?.Cancel();
        using var pressed = new CancellationTokenSource();
        volumePress = pressed;
        repeatedVolumeButton = null;
        try
        {
            await Task.Delay(350, pressed.Token);
            if (busy)
                return;
            repeatedVolumeButton = button;
            await RunVolumeChangeAsync((tv, changed, token) =>
                tv.HoldVolumeAsync(code == "30" ? 1 : -1, changed, token), pressed);
        }
        catch (OperationCanceledException) when (pressed.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(volumePress, pressed))
                volumePress = null;
        }
    }

    private async void OnVolumePreset(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string value }
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var target))
            return;
        using var cancellation = new CancellationTokenSource();
        await RunVolumeChangeAsync((tv, changed, token) => tv.SetVolumeAsync(target, changed, token), cancellation);
    }

    private async Task RunVolumeChangeAsync(Func<TvControl, Action<int>, CancellationToken, Task> operation,
        CancellationTokenSource cancellation)
    {
        if (busy || CurrentProfile is not { } profile)
            return;
        if (!TelevisionProfiles.TryParseAddress(profile.Address, out var address))
        {
            ShowToast(Text("CheckProfileAddress"), success: false);
            return;
        }

        busy = true;
        volumeChange = cancellation;
        // Keep the pressed button enabled so its release can stop the repeat loop.
        MenuButton.IsEnabled = CheckButton.IsEnabled = false;
        if (desktopMenu is not null)
            desktopMenu.IsEnabled = false;
        RestartVolumeTracking(resetValue: false);
        try
        {
            var pairing = await TvControl.GetPairingAsync(profile.Id);
            if (pairing is null)
            {
                ShowToast(Text("PairTvFirst"), success: false);
                return;
            }
            await operation(new TvControl(address, pairing), level => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!cancellation.IsCancellationRequested && CurrentProfile?.Id == profile.Id)
                    VolumeValue.Text = level.ToString(CultureInfo.CurrentCulture);
            }), cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is IOException or SocketException or AuthenticationException
            or CryptographicException or FormatException or OperationCanceledException)
        {
            ShowToast(Text("VolumeChangeFailed"), success: false);
        }
        finally
        {
            volumeChange = null;
            busy = false;
            SetRemoteEnabled(true);
            RestartVolumeTracking(resetValue: false);
        }
    }
}
