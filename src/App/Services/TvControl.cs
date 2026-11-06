using System.Net;

namespace BraviaRemote;

internal sealed class TvControl(IPAddress address, GoogleTvPairing pairing)
{
    public Task<int> GetVolumeAsync() => GoogleTvApps.ControlAsync(address, pairing);

    public async Task PressAsync(int key)
    {
        await GoogleTvApps.ControlAsync(address, pairing, sonyKey: key);
    }

    public async Task SetPowerAsync(bool on)
    {
        await GoogleTvApps.ControlAsync(address, pairing, power: on);
    }

    public Task TogglePowerAsync() => GoogleTvApps.ControlAsync(address, pairing, sonyKey: 0);

    public static async Task<GoogleTvPairing?> GetPairingAsync(Guid profile)
    {
        var saved = await SecureStorage.Default.GetAsync($"google-tv-pair-{profile:N}");
        return saved?.Split('|', 2) is [var fingerprint, var certificate] ? new(fingerprint, certificate) : null;
    }
}
