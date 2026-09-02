using System.Text.Json;
using System.Text.Json.Serialization;

namespace MirrorsEdgeTweaks.Services
{
    /// <summary>
    /// The shape of upstream's assets-manifest.json: a base URL every asset hangs off, plus
    /// optional per-file overrides for anything hosted somewhere else.
    /// </summary>
    public sealed class AssetManifest
    {
        [JsonPropertyName("version")]
        public int Version { get; init; }

        [JsonPropertyName("assetBase")]
        public string AssetBase { get; init; } = string.Empty;

        [JsonPropertyName("updated")]
        public string? Updated { get; init; }

        [JsonPropertyName("overrides")]
        public Dictionary<string, string>? Overrides { get; init; }

        public static AssetManifest? TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<AssetManifest>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public string? GetValidatedAssetBase() =>
            ValidateHttpsUrl(AssetBase, requireTrailingSlash: true);

        /// <summary>
        /// HTTPS is required, not merely preferred: this file decides where the app downloads
        /// executable game content from, so a manifest that tried to downgrade us to plain
        /// HTTP is rejected outright rather than trusted.
        /// </summary>
        public static string? ValidateHttpsUrl(string? url, bool requireTrailingSlash = false)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            if (!requireTrailingSlash)
                return url;

            return url.EndsWith('/') ? url : url + "/";
        }
    }
}
