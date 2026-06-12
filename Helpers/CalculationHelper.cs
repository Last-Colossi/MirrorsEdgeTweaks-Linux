using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MirrorsEdgeTweaks.Models;
using UELib;
using UELib.Core;
using static UELib.Core.UStruct.UByteCodeDecompiler;

namespace MirrorsEdgeTweaks.Helpers
{
    public static class ByteArrayHelper
    {
        public static byte[] StringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("The hex string cannot have an odd number of digits.", nameof(hex));

            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static float ReadFloatFromBytes(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length) return 0f;
            return BitConverter.ToSingle(bytes, offset);
        }

        public static void WriteFloatToBytes(byte[] bytes, int offset, float value)
        {
            if (offset < 0 || offset + 4 > bytes.Length) return;
            var valueBytes = BitConverter.GetBytes(value);
            Array.Copy(valueBytes, 0, bytes, offset, 4);
        }
    }

    public static class GameVersionHelper
    {
        public static GameVersion GetGameVersion(string gameDirectoryPath)
        {
            if (string.IsNullOrEmpty(gameDirectoryPath))
            {
                return new GameVersion { DisplayText = "Game Version: N/A", IsValid = false };
            }

            string exePath = System.IO.Path.Combine(gameDirectoryPath, "Binaries", "MirrorsEdge.exe");

            if (!System.IO.File.Exists(exePath))
            {
                return new GameVersion { DisplayText = "Game Version: MirrorsEdge.exe not found.", IsValid = false };
            }

            try
            {
                string? version = PeVersionHelper.GetFileVersion(exePath);
                if (!string.IsNullOrEmpty(version))
                {
                    bool isDlcVersion = version.StartsWith("1.1.0.0", StringComparison.OrdinalIgnoreCase);
                    string versionLabel = isDlcVersion ? $"{version} (DLC)" : version;

                    return new GameVersion
                    {
                        Version = version,
                        DisplayText = $"Game Version: {versionLabel}",
                        IsValid = true
                    };
                }
                else
                {
                    return new GameVersion { DisplayText = "Game Version: Not found in executable details.", IsValid = false };
                }
            }
            catch (Exception)
            {
                return new GameVersion { DisplayText = "Game Version: Error reading version.", IsValid = false };
            }
        }

        public static string? GetDownloadUrl(string gameVersionInfo, string selectedFix)
        {
            string baseUrl;

            if (gameVersionInfo.Contains("1.0.0.0") || gameVersionInfo.Contains("1.0.1.0"))
            {
                baseUrl = "https://github.com/softsoundd/MirrorsEdgeTweaks/raw/refs/heads/main/Downloads/Base_";
            }
            else if (gameVersionInfo.Contains("1.1.0.0"))
            {
                baseUrl = "https://github.com/softsoundd/MirrorsEdgeTweaks/raw/refs/heads/main/Downloads/DLC_";
            }
            else
            {
                return null;
            }

            string fileName = selectedFix switch
            {
                "Original" => "TdGame.zip",
                "TdGame Fix (by Keku)" => "TdGameFix.zip",
                "Time Trials Timer Fix (by Nulaft)" => "TimerFix.zip",
                "TdGame Fix + Time Trials Timer Fix" => "TdGameFix+TimerFix.zip",
                _ => string.Empty
            };

            return string.IsNullOrEmpty(fileName) ? null : baseUrl + fileName;
        }
    }

    public static class TdGameVersionDetector
    {
        public static string DetectTdGameVersion(string packagePath)
        {
            if (string.IsNullOrEmpty(packagePath) || !System.IO.File.Exists(packagePath))
            {
                return "Unknown";
            }

            try
            {
                using var package = UELib.UnrealLoader.LoadPackage(packagePath, System.IO.FileAccess.Read);
                package?.InitializePackage();

                if (package == null)
                {
                    return "Unknown";
                }

                string packageGuid = package.Summary.Guid.ToString();
                bool hasTimeAttackClock = CheckForTimeAttackClock(package);

                // Game version 1.0.0.0 - 1.0.1.0
                if (packageGuid.Equals("FF6DA87146E12E95C932FBA8D99244B0", StringComparison.OrdinalIgnoreCase))
                {
                    return hasTimeAttackClock ? "Time Trials Timer Fix (by Nulaft)" : "Original";
                }
                if (packageGuid.Equals("9C05ED954D0470138B83FB859AFBD97E", StringComparison.OrdinalIgnoreCase))
                {
                    return "TdGame Fix (by Keku)";
                }
                if (packageGuid.Equals("0F92289646781F18A15FDBAF61817238", StringComparison.OrdinalIgnoreCase))
                {
                    return "TdGame Fix + Time Trials Timer Fix";
                }

                // Game version 1.1.0.0
                if (packageGuid.Equals("2090A0F24C332BA658BC549AB764BCFF", StringComparison.OrdinalIgnoreCase))
                {
                    return hasTimeAttackClock ? "Time Trials Timer Fix (by Nulaft)" : "Original";
                }
                if (packageGuid.Equals("5B0067B8477495CD827B74B01FCE3862", StringComparison.OrdinalIgnoreCase))
                {
                    return "TdGame Fix (by Keku)";
                }
                if (packageGuid.Equals("18F2F3064301C448B3192E8E0D51E987", StringComparison.OrdinalIgnoreCase))
                {
                    return "TdGame Fix + Time Trials Timer Fix";
                }

                return "Unknown";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        // Original and Time Trials Timer Fix TdGame versions have the same GUID but different usage of the TimeAttackClock variable
        private static bool CheckForTimeAttackClock(UnrealPackage package)
        {
            try
            {
                var tdSPTimeTrialGameClass = package.FindObject<UClass>("TdSPTimeTrialGame");
                if (tdSPTimeTrialGameClass != null)
                {
                    var functions = package.Objects
                        .Where(o => IsOwnedBy(o, tdSPTimeTrialGameClass) && (o is UFunction || o is UState))
                        .Cast<UStruct>()
                        .ToList();

                    foreach (var func in functions)
                    {
                        try
                        {
                            if (func.ByteCodeManager != null)
                            {
                                func.ByteCodeManager.Deserialize();
                                if (func.ByteCodeManager.DeserializedTokens != null)
                                {
                                    foreach (var token in func.ByteCodeManager.DeserializedTokens)
                                    {
                                        if (token is InstanceVariableToken instToken)
                                        {
                                            if (instToken.Object != null && string.Equals(instToken.Object.Name, "TimeAttackClock", StringComparison.OrdinalIgnoreCase))
                                                return true;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsOwnedBy(UObject obj, UObject owner)
        {
            var current = obj.Outer;
            while (current != null)
            {
                if (current == owner) return true;
                current = current.Outer;
            }
            return false;
        }
    }

    public static class ConfigFileHelper
    {
        public static string? ReadIniValue(string filePath, string section, string key)
        {
            if (!System.IO.File.Exists(filePath)) return null;

            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);
                string sectionHeader = $"[{section}]";
                bool inSection = false;

                foreach (var line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (trimmedLine.Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = true;
                        continue;
                    }

                    if (inSection)
                    {
                        if (trimmedLine.StartsWith("["))
                        {
                            break;
                        }

                        if (trimmedLine.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                        {
                            return trimmedLine.Substring(key.Length + 1).Trim();
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static void ModifyIniFile(string filePath, string section, string key, string value)
        {
            var fileInfo = new System.IO.FileInfo(filePath);

            try
            {
                if (fileInfo.IsReadOnly)
                    fileInfo.IsReadOnly = false;

                var lines = System.IO.File.ReadAllLines(filePath).ToList();
                string sectionHeader = $"[{section}]";
                int sectionIndex = -1;
                int keyIndex = -1;

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        sectionIndex = i;
                        for (int j = i + 1; j < lines.Count; j++)
                        {
                            if (lines[j].Trim().StartsWith("[")) break;

                            if (lines[j].Trim().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                            {
                                keyIndex = j;
                                break;
                            }
                        }
                        break;
                    }
                }

                string newEntry = $"{key}={value}";
                if (keyIndex != -1)
                {
                    lines[keyIndex] = newEntry;
                }
                else if (sectionIndex != -1)
                {
                    lines.Insert(sectionIndex + 1, newEntry);
                }
                else
                {
                    if (lines.Any() && !string.IsNullOrWhiteSpace(lines.Last()))
                    {
                        lines.Add(string.Empty);
                    }
                    lines.Add(sectionHeader);
                    lines.Add(newEntry);
                }

                System.IO.File.WriteAllLines(filePath, lines);
            }
            finally
            {
                fileInfo.IsReadOnly = true;
            }
        }
    }

    public static class ResolutionHelper
    {
        public class Resolution
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public string DisplayText => $"{Width} x {Height}";
        }

        /// <summary>
        /// Display-mode enumeration is a Windows API concept; on Linux (Wayland/X11)
        /// there is no portable equivalent, so offer the screens' current resolutions
        /// plus the common mode list. The game accepts arbitrary resolutions via its
        /// ini regardless of what the desktop reports.
        /// </summary>
        public static List<Resolution> GetAvailableResolutions(IEnumerable<(int Width, int Height)>? screenResolutions = null)
        {
            var resolutions = new List<Resolution>();
            var resolutionSet = new HashSet<string>();

            void Add(int width, int height)
            {
                string key = $"{width}x{height}";
                if (!resolutionSet.Contains(key) &&
                    width >= 800 && height >= 600 &&
                    width <= 7680 && height <= 4320)
                {
                    resolutions.Add(new Resolution { Width = width, Height = height });
                    resolutionSet.Add(key);
                }
            }

            if (screenResolutions != null)
            {
                foreach (var (width, height) in screenResolutions)
                {
                    Add(width, height);
                }
            }

            foreach (var (width, height) in CommonModes)
            {
                Add(width, height);
            }

            return resolutions.OrderBy(r => r.Width).ThenBy(r => r.Height).ToList();
        }

        private static readonly (int, int)[] CommonModes =
        {
            (800, 600), (1024, 768), (1152, 864), (1280, 720), (1280, 800),
            (1280, 960), (1280, 1024), (1360, 768), (1366, 768), (1440, 900),
            (1600, 900), (1600, 1200), (1680, 1050), (1920, 1080), (1920, 1200),
            (2560, 1080), (2560, 1440), (2560, 1600), (3440, 1440), (3840, 1600),
            (3840, 2160), (5120, 1440), (5120, 2880), (7680, 4320)
        };
    }
}
