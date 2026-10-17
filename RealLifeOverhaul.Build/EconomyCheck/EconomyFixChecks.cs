using System;

// Faults fixed in four Economy folders (Brands, Services, Dealer, Estate), each reproduced in a file of its own:
// BrandFixChecks.cs, ServicesFixChecks.cs, DealerFixChecks.cs and EstateFixChecks.cs. What a rule decides is checked by
// calling it; what only the game's side can do is checked in its source.
internal static partial class Program
{
    private static void EconomyFixes()
    {
        BrandFixes();
        ServicesFixes();
        DealerFixes();
        EstateFixes();
    }

    // A file of Scripts/Economy as text, comments and all.
    private static string EconomySource(string file)
    {
        string path = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Economy", file);
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
    }

    // A member's text from its signature to the next member declared at the same depth; empty if the signature is not there.
    private static string MemberText(string code, string signature)
    {
        int at = code.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int end = code.Length;
        foreach (string next in new[] { "\n        public ", "\n        private ", "\n        internal ", "\n        // ", "\n        /// " })
        {
            int found = code.IndexOf(next, at + signature.Length, StringComparison.Ordinal);
            end = found >= 0 && found < end ? found : end;
        }
        return code.Substring(at, end - at);
    }

    // How often a text stands in another.
    private static int Occurs(string text, string what)
    {
        int count = 0;
        for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }
}
