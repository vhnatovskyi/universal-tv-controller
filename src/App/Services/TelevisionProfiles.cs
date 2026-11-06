using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BraviaRemote;

internal sealed record QuickButton(string Name, int[]? Keys = null, string? AppPackage = null);

internal sealed record TelevisionProfile(Guid Id, string Name, string Address, QuickButton[]? Shortcuts = null);

internal sealed record TelevisionProfiles(List<TelevisionProfile> Items, Guid? DefaultId)
{
    private const string ProfilesKey = "television-profiles";
    private const string LegacyAddressKey = "television-address";

    public static TelevisionProfiles Load(IPreferences preferences)
    {
        if (preferences.ContainsKey(ProfilesKey))
        {
            try
            {
                var saved = JsonSerializer.Deserialize(
                    preferences.Get(ProfilesKey, ""), ProfileJsonContext.Default.TelevisionProfiles);
                if (saved is not null)
                    return saved;
            }
            catch (JsonException)
            {
                // Fall back to the previous address if the saved profile data cannot be read.
            }
        }

        if (!TryParseAddress(preferences.Get(LegacyAddressKey, ""), out var address))
            return new([], null);

        var profile = new TelevisionProfile(Guid.NewGuid(), "BRAVIA 3", address.ToString());
        var migrated = new TelevisionProfiles([profile], profile.Id);
        migrated.Save(preferences);
        preferences.Remove(LegacyAddressKey);
        return migrated;
    }

    public void Save(IPreferences preferences) =>
        preferences.Set(ProfilesKey, JsonSerializer.Serialize(this, ProfileJsonContext.Default.TelevisionProfiles));

    public static bool TryParseAddress(string? text, out IPAddress address)
    {
        if (IPAddress.TryParse(text?.Trim(), out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
        {
            address = parsed;
            return true;
        }

        address = IPAddress.None;
        return false;
    }
}

[JsonSerializable(typeof(TelevisionProfiles))]
internal sealed partial class ProfileJsonContext : JsonSerializerContext;
