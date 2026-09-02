using System.IO;

namespace MirrorsEdgeTweaks.Helpers
{
    /// <summary>Where this app keeps its own files on disk.</summary>
    public static class AppPaths
    {
        /// <summary>
        /// ~/.config/MirrorsEdgeTweaks (or $XDG_CONFIG_HOME).
        ///
        /// DoNotVerify is load-bearing on Linux: without it GetFolderPath returns an empty
        /// string when ~/.config doesn't exist yet, and everything below would silently become
        /// a relative path written into the working directory.
        /// </summary>
        public static string ConfigDirectory { get; } = ResolveConfigDirectory();

        private static string ResolveConfigDirectory()
        {
            string root = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);

            // Only reachable with no HOME and no XDG_CONFIG_HOME, but an absolute path here
            // beats a stray directory next to the binary.
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Path.GetTempPath(), ".config");

            return Path.Combine(root, "MirrorsEdgeTweaks");
        }
    }
}
