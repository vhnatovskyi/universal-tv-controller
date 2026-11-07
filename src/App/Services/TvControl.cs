using System.Net;

namespace BraviaRemote;

internal sealed class TvControl(IPAddress address, GoogleTvPairing pairing, string? macAddress = null)
{
    public Task<int> GetVolumeAsync() => GoogleTvApps.ControlAsync(address, pairing);

    public async Task PressAsync(int key)
    {
        await GoogleTvApps.ControlAsync(address, pairing, sonyKey: key);
    }

    public async Task SetPowerAsync(bool on)
    {
        if (on && macAddress is not null)
        {
            await GoogleTvApps.WakeAsync(address, pairing, macAddress);
            return;
        }
        await GoogleTvApps.ControlAsync(address, pairing, power: on);
    }

    public Task TogglePowerAsync(bool? observedOn = null) =>
        macAddress is not null ? SetPowerAsync(observedOn != true) : GoogleTvApps.ControlAsync(address, pairing, sonyKey: 0);

    public Task SetVolumeAsync(int volume, Action<int> changed, CancellationToken token) =>
        GoogleTvApps.SetVolumeAsync(address, pairing, volume, changed, token);

    public Task HoldVolumeAsync(int direction, Action<int> changed, CancellationToken token) =>
        GoogleTvApps.HoldVolumeAsync(address, pairing, direction, changed, token);

    public static async Task<GoogleTvPairing?> GetPairingAsync(Guid profile)
    {
        var saved = await SecureStorage.Default.GetAsync($"google-tv-pair-{profile:N}");
        return saved?.Split('|', 2) is [var fingerprint, var certificate] ? new(fingerprint, certificate) : null;
    }
}
