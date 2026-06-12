using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MirrorsEdgeTweaks.Platform
{
    /// <summary>
    /// Linux/Steam-specific environment discovery for the Steam release of Mirror's Edge
    /// running under Proton. Finds the Steam root (native or Flatpak), the game install
    /// directory, and the Proton prefix that stands in for the Windows "My Documents"
    /// folder and HKLM registry.
    /// </summary>
    public static class SteamEnvironment
    {
        public const int MirrorsEdgeAppId = 17410;

        private static string? _cachedDocumentsPath;
        private static string? _cachedGameDirectory;
        private static string? _cachedPrefixPath;

        private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        /// <summary>
        /// Candidate Steam roots, in preference order. Bazzite ships Flatpak Steam by
        /// default but users frequently layer or install native Steam too.
        /// </summary>
        public static IEnumerable<string> SteamRootCandidates()
        {
            yield return Path.Combine(Home, ".local", "share", "Steam");
            yield return Path.Combine(Home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
            yield return Path.Combine(Home, ".steam", "steam");
            yield return Path.Combine(Home, ".steam", "root");
        }

        public static string? FindSteamRoot()
        {
            foreach (var candidate in SteamRootCandidates())
            {
                if (File.Exists(Path.Combine(candidate, "steamapps", "libraryfolders.vdf")))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// All Steam library roots (each containing a steamapps folder), parsed from
        /// libraryfolders.vdf plus the Steam root itself.
        /// </summary>
        public static List<string> FindLibraryRoots()
        {
            var roots = new List<string>();
            var steamRoot = FindSteamRoot();
            if (steamRoot == null)
            {
                return roots;
            }

            roots.Add(steamRoot);

            string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            try
            {
                string vdf = File.ReadAllText(vdfPath);
                foreach (Match match in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\""))
                {
                    string path = match.Groups[1].Value.Replace("\\\\", "\\").Replace('\\', '/');
                    if (Directory.Exists(path) && !roots.Contains(path))
                    {
                        roots.Add(path);
                    }
                }
            }
            catch
            {
                // Fall back to just the Steam root.
            }

            return roots;
        }

        /// <summary>
        /// Locates the Mirror's Edge install directory (the folder containing Binaries
        /// and TdGame) across all Steam libraries.
        /// </summary>
        public static string? FindGameDirectory()
        {
            if (_cachedGameDirectory != null && Directory.Exists(_cachedGameDirectory))
            {
                return _cachedGameDirectory;
            }

            foreach (var library in FindLibraryRoots())
            {
                string manifest = Path.Combine(library, "steamapps", $"appmanifest_{MirrorsEdgeAppId}.acf");
                string installDir = Path.Combine(library, "steamapps", "common", "mirrors edge");

                if (File.Exists(manifest))
                {
                    try
                    {
                        var match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                        if (match.Success)
                        {
                            installDir = Path.Combine(library, "steamapps", "common", match.Groups[1].Value);
                        }
                    }
                    catch
                    {
                        // Use default installdir.
                    }
                }

                if (File.Exists(Path.Combine(installDir, "Binaries", "MirrorsEdge.exe")))
                {
                    _cachedGameDirectory = installDir;
                    return installDir;
                }
            }

            return null;
        }

        /// <summary>
        /// The Proton (Wine) prefix for the game, i.e. steamapps/compatdata/17410/pfx.
        /// This exists once the game has been launched at least once through Proton.
        /// </summary>
        public static string? FindProtonPrefix()
        {
            if (_cachedPrefixPath != null && Directory.Exists(_cachedPrefixPath))
            {
                return _cachedPrefixPath;
            }

            foreach (var library in FindLibraryRoots())
            {
                string pfx = Path.Combine(library, "steamapps", "compatdata", MirrorsEdgeAppId.ToString(), "pfx");
                if (Directory.Exists(pfx))
                {
                    _cachedPrefixPath = pfx;
                    return pfx;
                }
            }

            return null;
        }

        /// <summary>
        /// The Windows "My Documents" folder as the game sees it under Proton. The game
        /// writes its TdGame/Config inis below this. Returns null until the prefix exists
        /// (i.e. before the game's first launch).
        /// </summary>
        public static string? FindDocumentsPath()
        {
            if (_cachedDocumentsPath != null && Directory.Exists(_cachedDocumentsPath))
            {
                return _cachedDocumentsPath;
            }

            var pfx = FindProtonPrefix();
            if (pfx == null)
            {
                return null;
            }

            string users = Path.Combine(pfx, "drive_c", "users", "steamuser");
            foreach (var name in new[] { "Documents", "My Documents" })
            {
                string docs = Path.Combine(users, name);
                if (Directory.Exists(docs))
                {
                    _cachedDocumentsPath = docs;
                    return docs;
                }
            }

            return null;
        }

        /// <summary>
        /// Drop-in replacement for Environment.GetFolderPath(MyDocuments) on the original
        /// Windows build. Falls back to the real XDG Documents folder so status labels can
        /// still render something sensible when the prefix is missing.
        /// </summary>
        public static string DocumentsPath =>
            FindDocumentsPath() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public static bool IsFlatpakSteam()
        {
            var root = FindSteamRoot();
            return root != null && root.Contains("/.var/app/com.valvesoftware.Steam/");
        }

        /// <summary>
        /// Launches the game through Steam (so Proton, the overlay, and Steam DRM all
        /// work). Arguments are passed through to the game via -applaunch.
        /// </summary>
        public static void LaunchGame(string arguments)
        {
            var errors = new List<string>();

            // Inside a Flatpak sandbox we cannot spawn steam/flatpak directly; the
            // steam:// URL goes through the desktop's URI portal instead.
            if (Environment.GetEnvironmentVariable("FLATPAK_ID") != null)
            {
                string sandboxUrl = string.IsNullOrWhiteSpace(arguments)
                    ? $"steam://rungameid/{MirrorsEdgeAppId}"
                    : $"steam://run/{MirrorsEdgeAppId}//{Uri.EscapeDataString(arguments)}/";
                if (TryStart("xdg-open", sandboxUrl, errors))
                {
                    return;
                }
                throw new InvalidOperationException($"Could not open {sandboxUrl}. {string.Join(" | ", errors)}");
            }

            if (IsFlatpakSteam() &&
                TryStart("flatpak", BuildArgs("run com.valvesoftware.Steam -applaunch", arguments), errors))
            {
                return;
            }

            if (TryStart("steam", BuildArgs("-applaunch", arguments), errors))
            {
                return;
            }

            // Steam URL fallback; argument passing uses the steam browser protocol form.
            string url = string.IsNullOrWhiteSpace(arguments)
                ? $"steam://rungameid/{MirrorsEdgeAppId}"
                : $"steam://run/{MirrorsEdgeAppId}//{Uri.EscapeDataString(arguments)}/";
            if (TryStart("xdg-open", url, errors))
            {
                return;
            }

            throw new InvalidOperationException($"All launch strategies failed. {string.Join(" | ", errors)}");
        }

        private static string BuildArgs(string prefix, string gameArguments)
        {
            string args = $"{prefix} {MirrorsEdgeAppId}";
            if (!string.IsNullOrWhiteSpace(gameArguments))
            {
                args += " " + gameArguments;
            }
            return args;
        }

        private static bool TryStart(string fileName, string arguments, List<string> errors)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false
                });
                if (process == null)
                {
                    errors.Add($"{fileName}: Process.Start returned null.");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errors.Add($"{fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads a string value from the prefix's HKLM hive (system.reg). Key path is
        /// given Windows-style, e.g. SOFTWARE\WOW6432Node\EA Games\Mirror's Edge.
        /// </summary>
        public static string? ReadRegistryValue(string keyPath, string valueName)
        {
            var regFile = SystemRegPath();
            if (regFile == null || !File.Exists(regFile))
            {
                return null;
            }

            var lines = File.ReadAllLines(regFile);
            string sectionPrefix = "[" + EscapeRegKey(NormalizeKeyPath(keyPath)) + "]";
            bool inSection = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("["))
                {
                    inSection = line.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (inSection)
                {
                    var match = Regex.Match(line, "^\"" + Regex.Escape(valueName) + "\"=\"(.*)\"\\s*$", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return UnescapeRegString(match.Groups[1].Value);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Writes a string value into the prefix's HKLM hive (system.reg), creating the
        /// key if needed. The game must not be running. This mirrors what the Windows
        /// build does via Microsoft.Win32.Registry with admin rights.
        /// </summary>
        public static void UpdateRegistryValue(string keyPath, string valueName, string newValue)
        {
            var regFile = SystemRegPath();
            if (regFile == null)
            {
                throw new InvalidOperationException(
                    "Proton prefix not found. Launch the game once through Steam first.");
            }

            string normalizedKey = NormalizeKeyPath(keyPath);
            string sectionHeader = "[" + EscapeRegKey(normalizedKey) + "]";
            string valueLine = "\"" + valueName + "\"=\"" + EscapeRegString(newValue) + "\"";

            var lines = File.ReadAllLines(regFile).ToList();
            int sectionStart = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("[") &&
                    lines[i].StartsWith(sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    sectionStart = i;
                    break;
                }
            }

            if (sectionStart == -1)
            {
                // Append a new key at the end of the hive.
                long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (lines.Count > 0 && lines[^1].Trim().Length != 0)
                {
                    lines.Add(string.Empty);
                }
                lines.Add($"{sectionHeader} {epoch}");
                lines.Add(valueLine);
                lines.Add(string.Empty);
            }
            else
            {
                int i = sectionStart + 1;
                bool replaced = false;
                while (i < lines.Count && !lines[i].StartsWith("["))
                {
                    if (Regex.IsMatch(lines[i], "^\"" + Regex.Escape(valueName) + "\"=", RegexOptions.IgnoreCase))
                    {
                        lines[i] = valueLine;
                        replaced = true;
                        break;
                    }
                    i++;
                }
                if (!replaced)
                {
                    lines.Insert(i, valueLine);
                }
            }

            File.WriteAllLines(regFile, lines, new UTF8Encoding(false));
        }

        private static string? SystemRegPath()
        {
            var pfx = FindProtonPrefix();
            return pfx == null ? null : Path.Combine(pfx, "system.reg");
        }

        private static string NormalizeKeyPath(string keyPath)
        {
            // system.reg stores the HKLM tree with "Software" (and Wine's canonical
            // "Wow6432Node") casing; match how Wine writes it.
            string normalized = keyPath.TrimStart('\\');
            if (normalized.StartsWith("SOFTWARE", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "Software" + normalized.Substring("SOFTWARE".Length);
            }
            normalized = Regex.Replace(normalized, "WOW6432Node", "Wow6432Node", RegexOptions.IgnoreCase);
            return normalized;
        }

        private static string EscapeRegKey(string key) => key.Replace("\\", "\\\\");

        private static string EscapeRegString(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string UnescapeRegString(string value) =>
            value.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
