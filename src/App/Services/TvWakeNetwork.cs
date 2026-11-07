using System.Net;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace UniversalTvRemote;

internal static class TvWakeNetwork
{
    internal static Task<string?> FindMacAsync(IPAddress address)
    {
#if WINDOWS
        return Task.Run(() =>
        {
            var mac = new byte[6];
            uint length = 6;
            var result = SendARP(BitConverter.ToUInt32(address.GetAddressBytes()), 0, mac, ref length);
            return result == 0 && length == 6 && mac.Any(value => value != 0) && (mac[0] & 1) == 0
                ? Convert.ToHexString(mac) : null;
        });
#else
        return Task.FromResult<string?>(null);
#endif
    }

#if WINDOWS
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint SendARP(uint destination, uint source, [Out] byte[] macAddress, ref uint length);
#endif
}
