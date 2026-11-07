using static UniversalTvRemote.Localization;

namespace UniversalTvRemote;

public partial class MainPage
{
    private CancellationTokenSource? voiceInput;
    internal void CancelVoiceInput() => voiceInput?.Cancel();
    private void OnCancelVoice(object? sender, EventArgs e) => voiceInput?.Cancel();

    private async void OnVoiceInput(object? sender, EventArgs e)
    {
        if (busy || CurrentProfile is not { } profile)
            return;
        busy = true;
        SetRemoteEnabled(false);
        RestartVolumeTracking(resetValue: false);
        using var listening = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        voiceInput = listening;
        VoicePanel.IsVisible = VoiceSpinner.IsRunning = true;
        VoiceStatus.Text = Text("VoiceListening");
        try
        {
            var transcript = await VoiceInput.RecognizeAsync(IsUkrainian ? "uk" : "en", listening.Token);
            VoicePanel.IsVisible = VoiceSpinner.IsRunning = false;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                ShowToast(Text("VoiceNoSpeech"), success: false);
                return;
            }
            var confirmed = await DisplayPromptAsync(Text("VoiceInput"), Text("VoiceConfirm"),
                Text("SendText"), Text("Cancel"), initialValue: transcript, maxLength: 1000);
            if (string.IsNullOrWhiteSpace(confirmed))
                return;
            var pairing = await TvControl.GetPairingAsync(profile.Id);
            if (pairing is null || !TelevisionProfiles.TryParseAddress(profile.Address, out var address))
                throw new IOException("Missing paired TV.");
            await GoogleTvApps.TypeTextAsync(address, pairing, confirmed);
            ShowToast(Text("VoiceTextSent"), success: true);
        }
        catch (TvTextFieldRequiredException)
        {
            ShowToast(Text("VoiceOpenField"), success: false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException
            or System.Runtime.InteropServices.COMException or System.Security.Authentication.AuthenticationException)
        {
            ShowToast(Text("VoiceUnavailable"), success: false);
        }
        finally
        {
            voiceInput = null;
            VoicePanel.IsVisible = VoiceSpinner.IsRunning = false;
            busy = false;
            SetRemoteEnabled(true);
            RestartVolumeTracking(resetValue: false);
        }
    }
}
