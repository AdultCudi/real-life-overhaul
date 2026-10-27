using RealLifeOverhaul.Meta;

// The beta channel (Scripts/Meta/BetaRules.cs): a beta build carries "0.61.0-beta.1" in the DLL's informational version
// (dotnet build -p:BetaTag=beta.1, steam_beta.py), and the mod reads its beta tag from there. A public build has no tag, so
// the public release is untouched.
internal static partial class Program
{
    private static void Beta()
    {
        Check(BetaRules.TagOf("0.61.0-beta.1+3f2a9c41") == "beta.1" && BetaRules.TagOf("0.61.0-beta.12") == "beta.12" && BetaRules.TagOf("1.0.0-rc.2+abc") == "rc.2",
            "beta: the tag is what stands between the dash and the plus");
        Check(BetaRules.TagOf("0.59.0+3f2a9c41") == null && BetaRules.TagOf("0.59.0") == null && BetaRules.TagOf(null) == null && BetaRules.TagOf("") == null && BetaRules.TagOf("0.59.0-") == null,
            "beta: a public build's version, an empty one and a dash with nothing after it have no tag");
    }
}
