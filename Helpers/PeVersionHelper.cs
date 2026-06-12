using System;
using System.IO;

namespace MirrorsEdgeTweaks.Helpers
{
    /// <summary>
    /// Reads the FileVersion from a Windows PE executable. .NET's FileVersionInfo only
    /// understands Win32 version resources on Windows itself, so on Linux we locate the
    /// VS_FIXEDFILEINFO structure (signature 0xFEEF04BD) in the file and decode the
    /// version fields directly.
    /// </summary>
    public static class PeVersionHelper
    {
        private const uint FixedFileInfoSignature = 0xFEEF04BD;

        public static string? GetFileVersion(string exePath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(exePath);
                int index = FindSignature(data);
                if (index < 0 || index + 16 > data.Length)
                {
                    return null;
                }

                // VS_FIXEDFILEINFO: dwSignature, dwStrucVersion, dwFileVersionMS, dwFileVersionLS
                uint fileVersionMs = BitConverter.ToUInt32(data, index + 8);
                uint fileVersionLs = BitConverter.ToUInt32(data, index + 12);

                return string.Format("{0}.{1}.{2}.{3}",
                    (fileVersionMs >> 16) & 0xFFFF,
                    fileVersionMs & 0xFFFF,
                    (fileVersionLs >> 16) & 0xFFFF,
                    fileVersionLs & 0xFFFF);
            }
            catch
            {
                return null;
            }
        }

        private static int FindSignature(byte[] data)
        {
            byte[] sig = BitConverter.GetBytes(FixedFileInfoSignature);
            for (int i = 0; i <= data.Length - 4; i++)
            {
                if (data[i] == sig[0] && data[i + 1] == sig[1] &&
                    data[i + 2] == sig[2] && data[i + 3] == sig[3])
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
