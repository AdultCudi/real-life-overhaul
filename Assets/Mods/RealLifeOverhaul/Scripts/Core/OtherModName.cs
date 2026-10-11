using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whether a loaded assembly's name is that of a mod this one knows (<see cref="OtherMods"/>). Some authors put
    /// the version into the assembly's name ("Immersion-1.2.0-test8"), so the name is matched by how it begins: the
    /// known name itself, or the known name and then something that is not a letter. "Immersion" is not
    /// "ImmersionTools".
    ///
    /// Plain text arithmetic: no game types, nothing allocated. Checked offline (NavCheck).
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
