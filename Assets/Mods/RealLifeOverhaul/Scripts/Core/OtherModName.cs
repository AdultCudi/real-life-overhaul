using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whether a loaded assembly's name is that of a known mod. Some carry their version in it ("Immersion-1.2.0-test8"),
    /// so the name matches by its start, followed by the end or a non-letter: "Immersion" is not "ImmersionTools".
    /// </summary>
    public static class OtherModName
    {
        public static bool Is(string assemblyName, string prefix)
        {
            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(prefix) || !assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return assemblyName.Length == prefix.Length || !char.IsLetter(assemblyName[prefix.Length]);
        }
    }
}
