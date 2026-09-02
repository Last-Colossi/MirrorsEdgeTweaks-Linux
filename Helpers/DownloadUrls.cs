namespace MirrorsEdgeTweaks.Helpers
{
    /// <summary>
    /// Baked-in defaults used when the remote manifest can't be fetched. Where the runtime
    /// assets actually live is resolved at startup by
    /// <see cref="Services.AssetUrlProvider"/>; these are only the fallback.
    ///
    /// Upstream used to serve these files from a Downloads/ folder in the repository. That
    /// folder was removed and the files moved to the runtime-assets release, which is what
    /// broke every download in 4.4.2. The manifest exists so the next move doesn't.
    /// </summary>
    public static class DownloadUrls
    {
        public const string DefaultAssetBase =
            "https://github.com/softsoundd/MirrorsEdgeTweaks/releases/download/runtime-assets/";

        public const string ManifestBootstrapUrl =
            "https://raw.githubusercontent.com/softsoundd/MirrorsEdgeTweaks/main/assets-manifest.json";
    }
}
