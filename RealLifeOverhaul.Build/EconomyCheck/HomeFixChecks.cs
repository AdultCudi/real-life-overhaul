using System;
using System.Collections.Generic;
using System.IO;
using IoPath = System.IO.Path;
using RealLifeOverhaul;

// Faults found in Scripts/Home: the roommate's talk test read the player's character without testing it, inside the tick
// whose fault switches roommates off; and a door to social life (RoommateBridges.Friend) was filled but never read.
internal static partial class Program
{
    private static void HomeFixes()
    {
        string flat = ModSource(@"Home\Roommates\RoommateFlat.cs");
        string reach = MemberText(flat, "private bool Reachable(Vector3 player)");
        int tested = reach.IndexOf("controller.Character != null", StringComparison.Ordinal);
        int read = reach.IndexOf("controller.Character.transform", StringComparison.Ordinal);
        Check(tested > 0 && read > tested, "home fix: the roommate's talk test tests the player's character before it reads where they face");

        string bridges = ModSource(@"Home\Roommates\RoommateSettings.cs");
        string links = ModSource(@"Wiring\RoommateLinks.cs");
        Check(bridges.Contains("public static Func<List<SocialPerson>, int> Friends;") && !bridges.Contains("Func<int, SocialPerson> Friend;")
              && !bridges.Contains(" Friend = null;") && !links.Contains("RoommateBridges.Friend = "),
            "home fix: RoommateBridges has no Friend door, filled by the wiring and read by nobody");
        string reader = null;
        foreach (string path in Directory.GetFiles(IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")), "*.cs", SearchOption.AllDirectories))
        {
            if (File.ReadAllText(path).Contains("RoommateBridges.Friend("))
            {
                reader = IoPath.GetFileName(path);
            }
        }
        Check(reader == null, "home fix: nothing calls a RoommateBridges.Friend door" + (reader != null ? " (" + reader + ")" : string.Empty));
        Check(FixLineEverywhere("roommate no longer goes quiet", "colega de casa não fica mais calado", "室友不会再因此", "Mitbewohner verstummt nicht mehr",
                                "colocataire ne se tait plus", "compañero de piso ya no se queda callado", "сосед по квартире больше не замолкает",
                                "een huisgenoot wordt niet meer stil", "ev arkadaşı artık oturumun geri kalanında sessiz kalmıyor"),
            "home fix: What's new has the roommate's line, with its fix mark, in some row of every language");
    }

    // A fix's line, by a piece of it in each language (in the order of ModLanguages.All: en, pt, zh-cn, de, fr, es, ru, nl, tr),
    // in some What's new row of each with the language's fix mark: the next row until a release numbers it into its own.
    private static bool FixLineEverywhere(params string[] pieces)
    {
        string[] order = ModLanguages.All;
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        bool all = codes.Length == order.Length && pieces.Length == order.Length;
        for (int l = 0; all && l < codes.Length; l++)
        {
            int at = Array.IndexOf(order, codes[l]);
            bool found = false;
            foreach (KeyValuePair<string, string> row in langs[l])
            {
                int piece = row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal)
                    ? row.Value.IndexOf(pieces[at], StringComparison.Ordinal) : -1;
                found |= piece > 0 && row.Value.LastIndexOf(FixMarks[codes[l]], piece, StringComparison.Ordinal) >= 0;
            }
            all &= at >= 0 && found;
        }
        return all;
    }
}
