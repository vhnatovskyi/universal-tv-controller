using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BraviaRemote;

internal sealed record QuickButton(string Name, string AppPackage);

internal sealed record TelevisionProfile(Guid Id, string Name, string Address, QuickButton[]? Shortcuts = null);

internal sealed record TelevisionProfiles(List<TelevisionProfile> Items, Guid? DefaultId)
{
    private const string ProfilesKey = "google-tv-profiles-v2";
    private const string LegacyAddressKey = "television-address";

    public static TelevisionProfiles Load(IPreferences preferences)
    {
        if (preferences.ContainsKey("television-profiles"))
        {
            try
            {
                using var legacy = JsonDocument.Parse(preferences.Get("television-profiles", ""));
                if (legacy.RootElement.TryGetProperty("Items", out var items) && items.ValueKind == JsonValueKind.Array)
                    foreach (var item in items.EnumerateArray())
                        if (item.TryGetProperty("Id", out var id) && id.TryGetGuid(out var profileId))
                            SecureStorage.Default.Remove($"google-tv-pair-{profileId:N}");
            }
            catch (JsonException)
            {
            }
            preferences.Remove("television-profiles");
        }
        preferences.Remove(LegacyAddressKey);
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
                // An unreadable profile list opens the setup screen.
            }
        }

        return new([], null);
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
