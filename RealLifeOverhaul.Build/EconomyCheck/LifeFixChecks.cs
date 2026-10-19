using System;
using System.Collections.Generic;
using System.IO;
using IoPath = System.IO.Path;
using System.Text.RegularExpressions;

// Faults found in Scripts/Life: no life event asked for a home, so a player in a hostel dorm or on the benches heard about
// their building's hot water, their neighbours, their fridge and their landlord. HasHome is now a need, read from Home
// through a bridge, and every event whose English speaks of the player's home asks for it.
internal static partial class Program
{
    // Home words in an event's English. An event whose texts hold one needs HasHome, unless it is listed below.
    private static readonly Regex HomeWords = new Regex(
        @"\b(at home|your place|your building|your apartment|your flat|your kitchen|your door|your front door|your window|landlord|next door|neighbours?\b|the super\b|tenant)",
        RegexOptions.IgnoreCase);

    // Events whose home words are about someone else's home or no home at all, so they fire for anyone.
    private static readonly string[] HomeWordsElsewhere =
    {
        "bz16",   // a poster in the business's window
        "fm08",   // Mum's neighbours
        "fr13",   // a friend's landlord
        "hl04",   // a night with no neighbours to hear, wherever it was spent
        "wk17"    // the people at a street's block party
    };

    private static void LifeFixes()
    {
        string catalogue = ModSource(@"Life\LifeEventCatalogue.cs");
        Check(catalogue.Contains("HasHome = 1 << 30"), "life fix: HasHome uses the next free Need bit");
        string runner = MemberText(ModSource(@"Life\LifeEventRunner.cs"), "public static LifeContext ReadContext()");
        Check(runner.Contains("if (LifeAccounts.HasHome()) flags |= Need.HasHome;"),
            "life fix: the context sets HasHome only when a wired home answers yes");
        string accounts = ModSource(@"Life\LifeEventCatalogue.Accounts.cs");
        Check(accounts.Contains("public static Func<bool> Home;") && accounts.Contains("public static bool HasHome() => Has(Home);"),
            "life fix: Life asks for a home through LifeAccounts, no home while unwired or on a fault");
        string links = ModSource(@"Wiring\LifeLinks.cs");
        string home = MemberText(links, "private static bool HasHome()");
        Check(MemberText(links, "internal static void Connect()").Contains("LifeAccounts.Home = HasHome;")
              && MemberText(links, "internal static void Disconnect()").Contains("LifeAccounts.Home = null;")
              && home.Contains("return HomeTidy.HasHome;") && home.Contains("catch (Exception e)")
              && home.Contains("Fault(\"LifeLinks.HasHome\", e);") && home.Contains("return false;"),
            "life fix: wiring asks Home's residential lookup, defaults to no home on a fault and clears on disconnect");

        string events = MemberText(ModSource(@"Life\LifeEventCatalogue.HomeAndWork.cs"), "private static LifeEvent[] Home()");
        string[] needs =
        {
            "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "None",
            "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "None", "None",
            "HasHome", "HasHome | OnFoot", "None", "HasHome", "Indoors", "Indoors", "HasHome", "HasHome"
        };
        for (int i = 0; i < needs.Length; i++)
        {
            string id = "hm" + (i + 1).ToString("00");
            Match entry = Regex.Match(events, "[EC]\\(\"" + id + "\", ([^,]+),");
            Check(entry.Success && entry.Groups[1].Value == needs[i], "life fix: " + id + " needs " + needs[i]);
        }
        LifeHomeWords();
        Check(FixLineEverywhere("news about your building, neighbours and home", "notícias sobre seu prédio, seus vizinhos e sua casa",
                                "关于你的住宅楼、邻居和住处的消息", "Nachrichten über dein Gebäude, deine Nachbarn und dein Zuhause",
                                "nouvelles de votre immeuble, de vos voisins et de votre logement", "noticias sobre tu edificio, tus vecinos y tu hogar",
                                "новости о вашем доме, соседях и жилье", "nieuws over je gebouw, je buren en je huis",
                                "binanız, komşularınız ve eviniz hakkındaki haberler"),
            "life fix: What's new has the home events' line, with its fix mark, in some row of every language");
    }

    // Every event in the catalogue, by the needs it is declared with, against the English of its keys: one that speaks of the
    // player's home asks for one, wherever in the catalogue it sits.
    private static void LifeHomeWords()
    {
        string folder = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Life"));
        string[] files = Directory.Exists(folder) ? Directory.GetFiles(folder, "LifeEventCatalogue*.cs") : new string[0];
        Dictionary<string, string> en = LoadWords(IoPath.Combine(LocalesFolder(), "en.json"));
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            foreach (Match entry in Regex.Matches(File.ReadAllText(file), "[EC]\\(\"([a-z]{2}[0-9]{2})\", ([^,]+),"))
            {
                declared[entry.Groups[1].Value] = entry.Groups[2].Value;
            }
        }
        Check(en != null && declared.Count >= 200, "life fix: the catalogue's events and their English are read (" + declared.Count + " events)");
        if (en == null)
        {
            return;
        }
        var homeless = new List<string>();
        var speaking = new HashSet<string>(StringComparer.Ordinal);
        int spoken = 0;
        foreach (KeyValuePair<string, string> item in declared)
        {
            string key = "reallife_ev_" + item.Key;
            bool speaks = false;
            foreach (KeyValuePair<string, string> row in en)
            {
                speaks |= (row.Key == key || row.Key.StartsWith(key + "_", StringComparison.Ordinal)) && HomeWords.IsMatch(row.Value);
            }
            bool elsewhere = Array.IndexOf(HomeWordsElsewhere, item.Key) >= 0;
            if (speaks)
            {
                speaking.Add(item.Key);
            }
            spoken += speaks && !elsewhere ? 1 : 0;
            if (speaks && !elsewhere && !Regex.IsMatch(item.Value, @"\bHasHome\b"))
            {
                homeless.Add(item.Key);
            }
        }
        var stale = new List<string>();
        foreach (string id in HomeWordsElsewhere)
        {
            if (!speaking.Contains(id))
            {
                stale.Add(id);
            }
        }
        Check(stale.Count == 0, "life fix: each event listed as speaking of another home is there and its English does"
            + (stale.Count > 0 ? " (not: " + string.Join(", ", stale) + ")" : string.Empty));
        Check(spoken >= 20 && homeless.Count == 0,
            "life fix: every event whose English speaks of the player's home needs HasHome (" + spoken + " do"
            + (homeless.Count > 0 ? "; not: " + string.Join(", ", homeless) : string.Empty) + ")");
    }
}
