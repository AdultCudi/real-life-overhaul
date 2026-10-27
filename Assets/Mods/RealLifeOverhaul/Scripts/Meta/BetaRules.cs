namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Reads the beta tag out of a DLL's informational version: "0.61.0-beta.1+3f2a9c" is "beta.1", "0.59.0+3f2a9c" is none.
    /// Pure, and checked offline; Changelog reads the assembly and asks this. COST: once, at the first ask.
    /// </summary>
    public static class BetaRules
    {
        /// <summary>The text between the first '-' and the '+' (or the end), or null when there is none.</summary>
        public static string TagOf(string informational)
        {
            if (string.IsNullOrEmpty(informational))
            {
                return null;
            }
            int dash = informational.IndexOf('-');
            if (dash <= 0)
            {
                return null;
            }
            int plus = informational.IndexOf('+', dash);
            string tag = plus < 0 ? informational.Substring(dash + 1) : informational.Substring(dash + 1, plus - dash - 1);
            return tag.Length > 0 ? tag : null;
        }
    }
}
