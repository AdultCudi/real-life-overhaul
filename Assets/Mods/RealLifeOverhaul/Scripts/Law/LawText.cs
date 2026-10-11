using Localizor;

namespace RealLifeOverhaul.Law
{
    /// <summary>Texts for the law's own screens and toasts: the locale line with the key names filled in.</summary>
    internal static class LawText
    {
        /// <summary>The line for a key, with {key}, {key1}..{key3} filled in (Jobs/JobKeys, Core/Keys).</summary>
        public static string Tr(this string key)
        {
            return JobKeys.Fill(key.GetLocalization());
        }
    }
}
