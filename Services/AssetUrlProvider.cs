using MirrorsEdgeTweaks.Helpers;
using System.IO;
using System.Net.Http;
using System.Text;

namespace MirrorsEdgeTweaks.Services
{
    public interface IAssetUrlProvider
    {
        /// <summary>Resolves a bare asset file name to a download URL.</summary>
        string For(string fileName);

        string AssetBase { get; }

        Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Decides where runtime assets (TdGame packages, Faith models, language files, OpenAL,
    /// the console, the tweaks scripts) are downloaded from.
    ///
    /// Resolution order is remote manifest, then the last manifest we cached, then the
    /// compiled-in default. That ordering is the point: when upstream relocated these files
    /// out of the repository's Downloads/ folder, every hard-coded URL in the app began
    /// returning 404 and only a new build could fix it. Reading the location at runtime means
    /// the next relocation costs users nothing, and the cache means it still works offline
    /// once they've been online at least once.
    /// </summary>
    public class AssetUrlProvider : IAssetUrlProvider
    {
        private static readonly HttpClient ManifestClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Shared instance. This app builds its services by hand rather than through a
        /// container, and the manifest is process-wide state, so the call sites share one.
        /// </summary>
        public static readonly AssetUrlProvider Shared = new();

        private readonly string _cacheFilePath;
        private readonly string _bootstrapUrl;
        private readonly object _loadLock = new();
        private string _assetBase = DownloadUrls.DefaultAssetBase;
        private Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);
        private Task? _loadTask;

        public AssetUrlProvider()
            : this(
                Path.Combine(AppPaths.ConfigDirectory, "asset-manifest.json"),
                DownloadUrls.ManifestBootstrapUrl)
        {
        }

        internal AssetUrlProvider(string cacheFilePath, string bootstrapUrl)
        {
            _cacheFilePath = cacheFilePath;
            _bootstrapUrl = bootstrapUrl;
        }

        public string AssetBase => _assetBase;

        public string For(string fileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

            if (_overrides.TryGetValue(fileName, out string? overrideUrl))
                return overrideUrl;

            return _assetBase + fileName;
        }

        /// <summary>
        /// Loads the manifest once per process. Safe to await from every download path;
        /// callers after the first get the same task back.
        /// </summary>
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            lock (_loadLock)
            {
                // A failed or cancelled attempt shouldn't poison every later download.
                if (_loadTask is { IsCompleted: true, IsFaulted: true }
                    or { IsCompleted: true, IsCanceled: true })
                {
                    _loadTask = null;
                }

                _loadTask ??= LoadInternalAsync(cancellationToken);
                return _loadTask;
            }
        }

        internal void ApplyManifestForTesting(string json, bool persistCache = false)
        {
            if (!TryApplyManifest(json, persistCache))
                throw new InvalidOperationException("Invalid manifest JSON.");
        }

        private async Task LoadInternalAsync(CancellationToken cancellationToken)
        {
            try
            {
                string? fetchedJson = await TryFetchManifestAsync(cancellationToken);
                if (fetchedJson != null && TryApplyManifest(fetchedJson, persistCache: true))
                    return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Network or parse trouble falls through to the cache, then to the default.
            }

            if (TryLoadCachedManifest())
                return;

            ApplyDefaults();
        }

        private async Task<string?> TryFetchManifestAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var response = await ManifestClient.GetAsync(_bootstrapUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private bool TryLoadCachedManifest()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return false;

                return TryApplyManifest(
                    File.ReadAllText(_cacheFilePath, Encoding.UTF8), persistCache: false);
            }
            catch
            {
                return false;
            }
        }

        private bool TryApplyManifest(string json, bool persistCache)
        {
            var manifest = AssetManifest.TryParse(json);
            string? assetBase = manifest?.GetValidatedAssetBase();
            if (assetBase == null)
                return false;

            _assetBase = assetBase;
            _overrides = manifest!.Overrides != null
                ? manifest.Overrides
                    .Select(kvp => (kvp.Key, Url: AssetManifest.ValidateHttpsUrl(kvp.Value)))
                    .Where(entry => entry.Url != null)
                    .ToDictionary(entry => entry.Key, entry => entry.Url!, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (persistCache)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
                    File.WriteAllText(_cacheFilePath, json, Encoding.UTF8);
                }
                catch
                {
                    // An unwritable cache mustn't stop us using the manifest we just fetched.
                }
            }

            return true;
        }

        private void ApplyDefaults()
        {
            _assetBase = DownloadUrls.DefaultAssetBase;
            _overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
