using System.Net;
using Foundation;

namespace BraviaRemote;

internal static class GoogleTvIosDiscovery
{
    public static async Task<DiscoveredTv[]> FindAsync(CancellationToken token)
    {
        using var browser = new NSNetServiceBrowser();
        var services = new List<NSNetService>();
        var devices = new Dictionary<string, DiscoveredTv>();
        browser.FoundService += (_, args) =>
        {
            var service = args.Service;
            services.Add(service);
            service.AddressResolved += (_, _) =>
            {
                if (service.Port != 6466)
                    return;
                foreach (var address in service.Addresses ?? [])
                {
                    var data = address.ToArray();
                    if (data.Length >= 8 && data[1] == 2)
                    {
                        var ip = new IPAddress(data.AsSpan(4, 4)).ToString();
                        devices[ip] = new DiscoveredTv(service.Name, ip);
                    }
                }
            };
            service.Resolve(4);
        };
        try
        {
            browser.SearchForServices("_androidtvremote2._tcp.", "local.");
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
        finally
        {
            browser.Stop();
            foreach (var service in services)
            {
                service.Stop();
                service.Dispose();
            }
        }
        return devices.Values.OrderBy(device => device.Name).ToArray();
    }
}
