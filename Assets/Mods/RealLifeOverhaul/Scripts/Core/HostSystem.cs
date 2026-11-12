using System;
using System.Runtime.InteropServices;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Which system the game runs on, asked without the engine so that worker threads and the offline checks can ask too.
    /// The game is sold for Windows and macOS (under Proton it is Windows to itself). What only Windows has, kernel32 and
    /// crypt32 through P/Invoke and llama.cpp's .exe, is reached only behind <see cref="Windows"/>, and each such place
    /// has another way or says plainly that it stands aside: StandardCheck fails a call that is not behind it (RLO-PLT-001).
    /// Cost: a field read.
    /// </summary>
    public static class HostSystem
    {
        /// <summary>True on Windows; false on macOS and Linux.</summary>
        public static readonly bool Windows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>The system's plain name for a log line: "Windows", "macOS", "Linux", or "this system" where it cannot be told.</summary>
        public static readonly string Name = Named();

        private static string Named()
        {
            if (Windows)
            {
                return "Windows";
            }
            try
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "this system";
            }
            catch (Exception)
            {
                return "this system";   // a runtime that cannot say: only the name is lost
            }
        }
    }
}
