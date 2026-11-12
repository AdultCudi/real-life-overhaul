using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// "Naming your own products in the tech industry" (a player's ask, Nov 2026). Until then a product's name was rolled from one pair of word
// lists whatever it was (a horror game could be called Quillledger), nothing kept two products apart, a concept's name was saved and never
// asked for, a rival company's product had no name but its maker's, and the rival studios' games came from 256 pairings that repeated.
// Held here: the made-up names (every pairing of every style: none empty, none over the length, none a famous real name, no two alike, each
// of its own style's words, the rivals' and the player's never the same), ten thousand rival products and ten thousand suggestions without a
// clash, the rival studios' games over thirty years, the rules a typed name is cleaned by and that the save reads it back as typed, an older
// save's products and rival games named once and kept, the names made from a game's (a sequel, an add-on, an update), that a name moves no
// score and costs nothing to change, that nothing new is saved for the rivals or sent, the screens, the sources of every place that shows a
// product, and the texts. --titles runs these alone.
internal static partial class Program
{
    private static void Titles()
    {
        TitleLists();
        TitleSpace();
        TitleOldNames();
        TitleTenThousand();
        TitleFits();
        TitleRivalGames();
        TitleTyped();
        TitleOldSaves();
        TitleFromGame();
        TitleNoEffect();
        TitleRivalProducts();
        TitleScreens();
        TitleSources();
        TitleTexts();
    }

    private static bool TitleLetters(string word) => word.Length > 0 && word.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

    // The style whose two lists make a name, or -1 for a name no style makes.
    private static int TitleStyleOfName(string name)
    {
        for (int style = 0; style < TitleStyle.Count; style++)
        {
            foreach (string first in TitleCatalogue.First[style])
            {
                foreach (string second in TitleCatalogue.Second[style])
                {
                    if (name == (TitleCatalogue.Joined[style] ? first + second : first + " " + second))
                    {
                        return style;
                    }
                }
            }
        }
        return -1;
    }

    // ---------------------------------------------------------------- the word lists

    private static void TitleLists()
    {
        bool sized = TitleCatalogue.First.Length == TitleStyle.Count && TitleCatalogue.Second.Length == TitleStyle.Count && TitleCatalogue.Joined.Length == TitleStyle.Count
                     && TitleCatalogue.Words % 2 == 0;
        string odd = "none";
        var seconds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int s = 0; sized && s < TitleStyle.Count; s++)
        {
            if (TitleCatalogue.First[s].Length != TitleCatalogue.Words || TitleCatalogue.Second[s].Length != TitleCatalogue.Words)
            {
                sized = false;
                odd = "style " + s + " has " + TitleCatalogue.First[s].Length + " and " + TitleCatalogue.Second[s].Length + " words";
            }
        }
        Check(sized, "titles: every style has its two lists of " + TitleCatalogue.Words + " words (" + odd + ")");
        if (!sized)
        {
            return;
        }
        string shared = "none", bad = "none", doubled = "none";
        for (int s = 0; s < TitleStyle.Count; s++)
        {
            foreach (string[] list in new[] { TitleCatalogue.First[s], TitleCatalogue.Second[s] })
            {
                if (list.Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Length)
                {
                    doubled = "style " + s;
                }
                foreach (string word in list)
                {
                    if (!TitleLetters(word) || TitleRules.Denied(word) || TitleCatalogue.Denied.Contains(TitleRules.Key(word)))
                    {
                        bad = word;
                    }
                }
            }
            foreach (string word in TitleCatalogue.Second[s])
            {
                if (seconds.TryGetValue(word, out int other) && other != s)
                {
                    shared = word + " in styles " + other + " and " + s;
                }
                seconds[word] = s;
            }
        }
        Check(shared == "none", "titles: no second word is shared by two styles, so two styles never make the same name (" + shared + ")");
        Check(bad == "none" && doubled == "none", "titles: every word is plain letters, none a famous real name, none twice in its list (" + bad + ", " + doubled + ")");
        Check(TitleCatalogue.Denied.Length >= 100 && TitleCatalogue.Denied.All(d => d == TitleRules.Key(d)) && TitleCatalogue.Denied.Distinct().Count() == TitleCatalogue.Denied.Length,
            "titles: the list of real names is in the form names are compared in, each once (" + TitleCatalogue.Denied.Length + ")");
        Check(TitleRules.Denied("Minecraft") && TitleRules.Denied("Call of Duty") && TitleRules.Denied("Iron Excel") && TitleRules.Denied("PHOTOSHOP")
              && !TitleRules.Denied("Hollow Manor") && !TitleRules.Denied("Brightdesk") && !TitleRules.Denied(string.Empty),
            "titles: a real name is known whole, in any case, with spaces or as one word of a name; an invented one is not");
        Check(TitleCatalogue.AddOns.Length == 16 && TitleCatalogue.AddOns.Distinct().Count() == 16 && TitleCatalogue.AddOns.All(a => !TitleRules.Denied(a) && TitleRules.Clean(a) == a),
            "titles: the add-ons' sixteen names are each their own, none a famous real one");
    }

    // ---------------------------------------------------------------- every name there is

    private static void TitleSpace()
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        int made = 0, longest = 0;
        string clash = "none", wrong = "none";
        for (int style = 0; style < TitleStyle.Count; style++)
        {
            int space = TitleRules.Space(style);
            for (int side = TitleRules.Rivals; side <= TitleRules.Player; side++)
            {
                for (int i = 0; i < space; i++)
                {
                    string name = TitleRules.Name(style, side, i);
                    made++;
                    longest = Math.Max(longest, name.Length);
                    if (string.IsNullOrEmpty(name) || name.Length > TitleCatalogue.Most || TitleRules.Denied(name) || TitleRules.Clean(name) != name)
                    {
                        wrong = name + " (style " + style + ")";
                    }
                    string key = TitleRules.Key(name);
                    if (seen.TryGetValue(key, out string held))
                    {
                        clash = name + " and " + held;
                    }
                    seen[key] = name + " (style " + style + ", side " + side + ")";
                }
            }
        }
        Check(made == TitleStyle.Count * TitleCatalogue.Words * TitleCatalogue.Words && wrong == "none",
            "titles: all " + made + " pairings make a name that is not empty, at most " + TitleCatalogue.Most + " characters (longest " + longest
            + "), no famous real name, and unchanged by the typed names' own rules (" + wrong + ")");
        Check(clash == "none" && seen.Count == made, "titles: no two pairings of any style or side make the same name, compared as names are (" + clash + ")");
        Check(TitleRules.Name(TitleStyle.SaasBusiness, TitleRules.Rivals, 0) == "Brightsheet" && TitleRules.Name(TitleStyle.SaasBusiness, TitleRules.Player, 0) == "Brightaudit"
              && TitleRules.Name(TitleStyle.Games + (int)Genre.Horror, TitleRules.Rivals, 0) == "Hollow Manor"
              && TitleRules.Name(TitleStyle.SaasBusiness, TitleRules.Rivals, -1) == TitleRules.Name(TitleStyle.SaasBusiness, TitleRules.Rivals, TitleRules.Space(1) - 1)
              && TitleRules.Name(99, TitleRules.Player, 5) == TitleRules.Name(TitleStyle.Count - 1, TitleRules.Player, 5),
            "titles: a tool's two words are written as one and a game's as two, the rivals take the even pairings and the player the odd, and a place or style out of range is held in");
    }

    // The words products and rival games were named with before names could be typed (FirmsCatalogue.NameFirst and NameSecond, StudioCatalogue.TitleFirst
    // and TitleSecond, as they stood): an older save holds names made of them.
    private static readonly string[] TitleOldFirst =
    {
        "Bright", "Quill", "Harbour", "Lantern", "Copper", "Tide", "Ember", "Cobalt", "Juniper", "Meadow", "Summit", "Willow", "Ridge", "Arbor", "Marble", "Orbit", "Pebble", "Kestrel"
    };

    private static readonly string[] TitleOldSecond =
    {
        "desk", "ledger", "flow", "board", "stack", "notes", "pass", "track", "cast", "loop", "post", "mint", "wave", "base", "kit", "path", "bloom", "forge"
    };

    private static readonly string[] TitleOldGameFirst =
    {
        "Ember", "Iron", "Hollow", "Star", "Crimson", "Silent", "Wild", "Copper", "Frost", "Sunken", "Neon", "Paper", "Thunder", "Velvet", "Lantern", "Granite"
    };

    private static readonly string[] TitleOldGameSecond =
    {
        "Crown", "Frontier", "Tides", "Circuit", "Garden", "Legion", "Harbour", "Echoes", "Kingdom", "Rally", "Heist", "Orchard", "Signal", "Vault", "Pilgrim", "League"
    };

    private static void TitleOldNames()
    {
        var products = new HashSet<string>(StringComparer.Ordinal);
        var games = new HashSet<string>(StringComparer.Ordinal);
        foreach (string first in TitleOldFirst)
        {
            foreach (string second in TitleOldSecond)
            {
                products.Add(TitleRules.Key(first + second));
            }
        }
        foreach (string first in TitleOldGameFirst)
        {
            foreach (string second in TitleOldGameSecond)
            {
                games.Add(TitleRules.Key(first + " " + second));
            }
        }
        string product = "none", game = "none";
        for (int style = 0; style < TitleStyle.Count; style++)
        {
            for (int i = 0; i < TitleRules.Space(style); i++)
            {
                string rival = TitleRules.Name(style, TitleRules.Rivals, i), mine = TitleRules.Name(style, TitleRules.Player, i);
                product = products.Contains(TitleRules.Key(rival)) ? rival : products.Contains(TitleRules.Key(mine)) ? mine : product;
                game = games.Contains(TitleRules.Key(mine)) ? mine : game;
            }
        }
        Check(products.Count == 324 && product == "none",
            "titles old names: no made-up name, a rival's or a suggestion, is one of the 324 an older save's products were named with, so no rival is ever called as one of them (" + product + ")");
        Check(games.Count == 256 && game == "none",
            "titles old names: no suggestion is one of the 256 names an older save's rival games bear; a new rival game passes over a name on the list (" + game + ")");
    }

    // ---------------------------------------------------------------- ten thousand without a clash

    private static void TitleTenThousand()
    {
        // rival products: an id and a market are all a name is made from, so ten thousand ids of one market, and of the markets in turn, are ten thousand names
        var one = new HashSet<string>(StringComparer.Ordinal);
        var turn = new HashSet<string>(StringComparer.Ordinal);
        bool same = true, filled = true;
        for (int id = 0; id < 10000; id++)
        {
            string a = TitleRules.Rival(id, (int)SectorMarket.BusinessSoftware), b = TitleRules.Rival(id, id % (int)SectorMarket.Count);
            filled &= a.Length > 0 && b.Length > 0 && !TitleRules.Denied(a) && !TitleRules.Denied(b);
            same &= a == TitleRules.Rival(id, (int)SectorMarket.BusinessSoftware);
            one.Add(TitleRules.Key(a));
            turn.Add(TitleRules.Key(b));
        }
        Check(one.Count == 10000 && turn.Count == 10000 && filled && same,
            "titles: ten thousand rival products of one market, and of the markets in turn, have ten thousand names, none empty or real, the same whenever asked (" + one.Count
            + ", " + turn.Count + ")");
        Check(TitleRules.Rival(1, 1) != TitleRules.Rival(2, 1) && TitleRules.Rival(1, 1) == TitleRules.Rival(1, 1) && TitleRules.Rival(1 + TitleRules.Space(1), 1).EndsWith(" 2")
              && TitleRules.Rival(-5, 1) == TitleRules.Rival(0, 1) && TitleRules.Rival(3, 99) == TitleRules.Rival(3, (int)SectorMarket.EnterpriseAi),
            "titles: a rival's name is numbered once its style has gone round, and an id or a market out of range is held in");

        // the player's suggestions: ten thousand products in a row, each given the offered name, of every style in turn; never one the rivals have
        var taken = new HashSet<string>(StringComparer.Ordinal);
        bool free = true, apart = true;
        for (int id = 1; id <= 10000; id++)
        {
            string name = TitleRules.Suggest(77, id, id % TitleStyle.Count, 0, n => taken.Contains(TitleRules.Key(n)));
            free &= name.Length > 0 && !TitleRules.Denied(name) && taken.Add(TitleRules.Key(name));
            apart &= !one.Contains(TitleRules.Key(name)) && !turn.Contains(TitleRules.Key(name));
        }
        Check(free && taken.Count == 10000, "titles: ten thousand products named one after another by the suggestion have ten thousand names, none empty or real (" + taken.Count + ")");
        Check(apart, "titles: none of those ten thousand suggestions is one of the rivals' twenty thousand names");
        Check(TitleRules.Suggest(5, 9, 1, 0, null) == TitleRules.Suggest(5, 9, 1, 0, null) && TitleRules.Suggest(5, 9, 1, 0, null) != TitleRules.Suggest(5, 9, 1, 1, null)
              && TitleRules.Suggest(5, 9, 1, 1, null) != TitleRules.Suggest(5, 9, 1, 2, null),
            "titles: a suggestion is the same for the same save, product and turn, and each turn offers another");
        var seeds = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            seeds.Add(TitleRules.Suggest(seed, 3, TitleStyle.App, 0, null));
        }
        Check(seeds.Count >= 20, "titles: saves of different seeds are offered different names for the same product (" + seeds.Count + " of 40)");
    }

    // ---------------------------------------------------------------- a name fits its kind and genre

    private static void TitleFits()
    {
        bool fits = true;
        string why = "none";
        for (int style = 0; style < TitleStyle.Count; style++)
        {
            for (int id = 1; id <= 60; id++)
            {
                foreach (string name in new[] { TitleRules.Suggest(31, id, style, 0, null), TitleRules.Name(style, TitleRules.Rivals, id) })
                {
                    if (TitleStyleOfName(name) != style)
                    {
                        fits = false;
                        why = name + " in style " + style;
                    }
                }
            }
        }
        Check(fits, "titles: every name is two words of its own style's lists, so a spreadsheet tool never sounds like a horror game (" + why + ")");
        Check(TitleRules.StyleOf(ProductKind.Saas, (int)Segment.Consumer, 0) == TitleStyle.SaasConsumer && TitleRules.StyleOf(ProductKind.Saas, (int)Segment.Business, 0) == TitleStyle.SaasBusiness
              && TitleRules.StyleOf(ProductKind.Saas, (int)Segment.Enterprise, 0) == TitleStyle.SaasEnterprise && TitleRules.StyleOf(ProductKind.App, 2, 0) == TitleStyle.App
              && TitleRules.StyleOf(ProductKind.Licence, 0, 0) == TitleStyle.Licence && TitleRules.StyleOf(ProductKind.Model, 1, 0) == TitleStyle.Model
              && TitleRules.StyleOf(ProductKind.Game, 0, (int)Genre.Horror) == TitleStyle.Games + (int)Genre.Horror && TitleRules.StyleOfGenre(99) == TitleStyle.Count - 1
              && TitleRules.StyleOfGenre(-1) == TitleStyle.Games,
            "titles: every kind of product, each kind of buyer of a subscription service and every genre has its style");
        bool markets = true;
        for (int m = 0; m < (int)SectorMarket.Count; m++)
        {
            int style = TitleRules.StyleOfMarket(m);
            markets &= style >= 0 && style < TitleStyle.Games;
        }
        Check(markets && TitleRules.StyleOfMarket((int)SectorMarket.Apps) == TitleStyle.App && TitleRules.StyleOfMarket((int)SectorMarket.Licences) == TitleStyle.Licence
              && TitleRules.StyleOfMarket((int)SectorMarket.ConsumerAi) == TitleStyle.Model && TitleRules.StyleOfMarket((int)SectorMarket.BusinessSoftware) == TitleStyle.SaasBusiness,
            "titles: every market's rivals are named as the player's products of that market are, and none as a game");
        var f = new FirmsFile { Seed = 9 };
        bool kinds = true;
        for (int k = 0; k < (int)ProductKind.Count; k++)
        {
            for (int s = 0; s < (int)Segment.Count; s++)
            {
                Product p = FirmsRules.NewProduct(f, "Tool Street~1", (ProductKind)k, (Segment)s, 1, null);
                int style = TitleStyleOfName(p.Name);
                kinds &= p.Name.Length > 0 && !FirmsRules.Named(f, p.Name, p.Id)
                         && (k == (int)ProductKind.Game ? style >= TitleStyle.Games : style == TitleRules.StyleOf((ProductKind)k, p.Segment, 0));
                f.Products.Add(p);
            }
        }
        Check(kinds && f.Products.Select(p => TitleRules.Key(p.Name)).Distinct().Count() == f.Products.Count,
            "titles: a product of every kind and buyer started with no name typed is given one of its own kind that no product of the file has");
        Product typed = FirmsRules.NewProduct(f, "Tool Street~1", ProductKind.Saas, Segment.Business, 1, "  My   Sheet;tool ");
        Check(typed.Name == "My Sheet tool", "titles: a product started with a typed name bears it, cleaned (" + typed.Name + ")");
    }

    // ---------------------------------------------------------------- the rival studios' games

    private static void TitleRivalGames()
    {
        StudioFile f = RivalRun(21, 365 * 30), again = RivalRun(21, 365 * 30), other = RivalRun(22, 365 * 30);
        bool apart = f.Rivals.Select(r => TitleRules.Key(r.Name)).Distinct().Count() == f.Rivals.Count;
        bool genre = f.Rivals.All(r => TitleCatalogue.First[TitleStyle.Games + r.Genre].Any(w => r.Name.StartsWith(w + " ", StringComparison.Ordinal)) && !TitleRules.Denied(r.Name));
        Check(f.Rivals.Count == StudioCatalogue.RivalGamesKept && apart && genre,
            "titles: after thirty years the rival games on the list all have their own names, each of its genre's words (" + f.Rivals.Count + " kept)");
        Check(f.Rivals.Select(r => r.Name).SequenceEqual(again.Rivals.Select(r => r.Name)) && !f.Rivals.Select(r => r.Name).SequenceEqual(other.Rivals.Select(r => r.Name)),
            "titles: the rival games' names are the same for the same seed and differ with another");
        // every release of a long run is named apart from the games on the list that day
        var run = new StudioFile { Seed = 5 };
        var released = new List<RivalGame>();
        bool clear = true;
        int releases = 0;
        for (int day = 1; day <= 365 * 40; day++)
        {
            StudioRules.RivalDay(run, day, released);
            releases += released.Count;
            if (released.Count > 0)
            {
                clear &= run.Rivals.Select(r => TitleRules.Key(r.Name)).Distinct().Count() == run.Rivals.Count && run.Rivals.All(r => r.Name.Length > 0);
            }
        }
        Check(clear && releases > 100, "titles: over forty years no rival release shares a name with a game on the list that day (" + releases + " releases)");
        // a taken name is passed over, and a style with none left numbers its first
        string first = TitleRules.RivalGame(8, 3, (int)Genre.Puzzle, null);
        string next = TitleRules.RivalGame(8, 3, (int)Genre.Puzzle, n => n == first);
        var all = new HashSet<string>();
        for (int i = 0; i < TitleRules.Space(TitleStyle.Games + (int)Genre.Puzzle); i++)
        {
            all.Add(TitleRules.Name(TitleStyle.Games + (int)Genre.Puzzle, TitleRules.Rivals, i));
        }
        string numbered = TitleRules.RivalGame(8, 3, (int)Genre.Puzzle, n => all.Contains(n));
        Check(next != first && all.Contains(next) && numbered == first + " 2", "titles: a rival game passes over a name in use, and with every name in use takes its own with a number (" + numbered + ")");

        // an older save: 256 pairings with nothing to keep two apart
        var old = new StudioFile { Seed = 12, NextRival = 9 };
        string[] names = { "Ember Crown", "Iron Tides", "Ember Crown", string.Empty, "ember  crown", "Star Vault", "Iron Tides", "Wild Garden" };
        for (int i = 0; i < names.Length; i++)
        {
            old.Rivals.Add(new RivalGame { Id = i + 1, Studio = i % 7, Name = names[i], Genre = i % (int)Genre.Count, Score = 60, Launched = 10 * i, Units = 1000 });
        }
        int named = StudioRules.NameRivals(old);
        bool kept = old.Rivals[0].Name == "Ember Crown" && old.Rivals[1].Name == "Iron Tides" && old.Rivals[5].Name == "Star Vault" && old.Rivals[7].Name == "Wild Garden";
        bool fresh = new[] { 2, 3, 4, 6 }.All(i => TitleCatalogue.First[TitleStyle.Games + old.Rivals[i].Genre].Any(w => old.Rivals[i].Name.StartsWith(w + " ", StringComparison.Ordinal)));
        Check(named == 4 && kept && fresh && old.Rivals.Select(r => TitleRules.Key(r.Name)).Distinct().Count() == old.Rivals.Count,
            "titles: an older save's rival games that share a name, or have none, are named apart by their genre, the first bearer keeping its own (" + named + " named)");
        string[] given = old.Rivals.Select(r => r.Name).ToArray();
        StudioFile back = StudioStore.Read(StudioStore.Write(old), out _);
        Check(StudioRules.NameRivals(old) == 0 && StudioRules.NameRivals(back) == 0 && back.Rivals.Select(r => r.Name).SequenceEqual(given),
            "titles: those names are given once: asked again, or after the save is written and read, nothing changes");
        var twin = new StudioFile { Seed = 12, NextRival = 9 };
        for (int i = 0; i < names.Length; i++)
        {
            twin.Rivals.Add(new RivalGame { Id = i + 1, Studio = i % 7, Name = names[i], Genre = i % (int)Genre.Count, Score = 60, Launched = 10 * i, Units = 1000 });
        }
        StudioRules.NameRivals(twin);
        Check(twin.Rivals.Select(r => r.Name).SequenceEqual(given), "titles: the same older save is given the same names every time it is loaded");
    }

    // ---------------------------------------------------------------- typed names

    private static void TitleTyped()
    {
        string forty = new string('a', 40);
        string pair = char.ConvertFromUtf32(0x1F3AE);   // a pair of surrogates, as an emoji is typed
        Check(TitleRules.Clean("  Bright   Desk \t") == "Bright Desk" && TitleRules.Clean(null) == string.Empty && TitleRules.Clean("   ") == string.Empty,
            "titles typed: a name is trimmed, its runs of spaces are one, and a box of spaces is empty");
        Check(TitleRules.Clean("<b>Big</b> <color=red>Name</color>") == "bBig/b color=redName/color" && TitleRules.Clean("<>") == string.Empty
              && TitleRules.Clean("Go {firm} {0}") == "Go firm 0" && !TitleRules.Clean("<size=200>x</size>").Contains("<"),
            "titles typed: the signs that open a tag or a text's own name are taken out, so no name is read as rich text or filled in");
        Check(TitleRules.Clean("Line\nBreak\r\nHere") == "Line Break Here" && TitleRules.Clean("Tab\tand" + (char)7 + "bell") == "Tab and bell"
              && TitleRules.Clean("a" + (char)0x2028 + "b" + (char)0x2029 + "c") == "a b c" && TitleRules.Clean("zero" + (char)0x200B + "width" + (char)0x202E + "turn") == "zerowidthturn",
            "titles typed: line breaks and control characters become a space, and the marks that hide or turn a line round are taken out");
        Check(TitleRules.Clean("One~Two;Three|Four") == "One Two Three Four", "titles typed: the save's own separators become a space, so what is typed is what is read back");
        Check(TitleRules.Clean(forty) == forty && TitleRules.Clean(forty + "b").Length == 40 && TitleRules.Clean(new string('a', 39) + pair + "z") == new string('a', 39)
              && TitleRules.Clean(new string('a', 38) + pair + "z") == new string('a', 38) + pair && TitleRules.Clean(new string('a', 39) + " b") == new string('a', 39),
            "titles typed: a name is at most " + TitleCatalogue.Most + " characters, a pair never halved and no space left at the end");
        string[] tongues = { "Ёжик в тумане", "魔法の城", "城市传奇 2", "별빛 농장", "Świat Gier", "Çılgın Koşu", "Crème Brûlée", "São João", "Größe Fünf", "مزرعة سعيدة", "Niño 3", "A", "7", "?!" };
        Check(tongues.All(t => TitleRules.Clean(t) == t), "titles typed: any language's letters, a single character, numbers and signs are kept as typed");
        Check(TitleRules.Clean(((char)0xD83C).ToString()) == string.Empty && TitleRules.Clean("ab" + (char)0xDFAE + "c") == "abc" && TitleRules.Clean(pair) == pair
              && TitleRules.Clean(((char)0x200D).ToString() + (char)0x200C) == string.Empty && TitleRules.Clean("می" + (char)0x200C + "خواهم") == "می" + (char)0x200C + "خواهم",
            "titles typed: half a pair is dropped, a whole one kept, the joiners of Persian and Indic words kept inside a name and never a name by themselves");
        Check(TitleRules.Same("Brightdesk", "bright desk") && TitleRules.Same("Ember Crown!", "EMBER-CROWN") && !TitleRules.Same("Ember Crown", "Ember Crown 2")
              && TitleRules.Same("城市传奇", "城市 传奇") && !TitleRules.Same("?!", "!?") && TitleRules.Key(string.Empty) == string.Empty,
            "titles typed: two names are one when their letters and digits are, in any case and spacing; names of signs alone are compared as written");
        var held = new HashSet<string> { TitleRules.Key("Brightdesk") };
        Func<string, bool> taken = n => held.Contains(TitleRules.Key(n));
        Check(TitleRules.Check(TitleRules.Clean("  "), taken) == TitleRules.Empty && TitleRules.Check(TitleRules.Clean("bright DESK"), taken) == TitleRules.Taken
              && TitleRules.Check(TitleRules.Clean("Brightdesk 2"), taken) == TitleRules.Ok && TitleRules.Check("Anything", null) == TitleRules.Ok,
            "titles typed: an empty name and one another product bears are refused, any other is kept");
        // what is typed is what the save reads back, for every kind of name a player may type
        var f = new FirmsFile { Seed = 3 };
        string[] typed = tongues.Concat(new[] { forty, "My Sheet tool", "Name: with, signs (and) more.", pair + " Arcade " + pair, "Rock & Roll 100%", "50/50 \\ \"quoted\" 'name'" }).ToArray();
        foreach (string t in typed)
        {
            f.Products.Add(FirmsRules.NewProduct(f, "Tool Street~1", ProductKind.App, Segment.Consumer, 0, t));
        }
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(f), out int version);
        Check(version == FirmsStore.Version && back.Products.Count == typed.Length && back.Products.Select(p => p.Name).SequenceEqual(typed)
              && FirmsStore.Write(back) == FirmsStore.Write(f),
            "titles typed: " + typed.Length + " typed names of every kind are written to the save and read back exactly as typed");
        f.Products[0].Name = "<b>Hand</b> edited;name~here|now\n";
        FirmsFile mended = FirmsStore.Read(FirmsStore.Write(f), out _);
        Check(mended.Products[0].Name == "bHand/b edited name here now", "titles typed: a name written into a save by hand is cleaned when it is read (" + mended.Products[0].Name + ")");
        var draft = new StudioFile { Seed = 4 };
        draft.Drafts.Add(new GameDraft { Place = StudioPlace, Genre = 8, Name = "城市传奇 2" });
        Check(StudioStore.Read(StudioStore.Write(draft), out _).Drafts[0].Name == "城市传奇 2", "titles typed: a concept's typed name is kept in the studio's line");
    }

    // ---------------------------------------------------------------- older saves

    private static FirmsFile TitleOldFile()
    {
        var f = new FirmsFile { Seed = 41, NextId = 9 };
        string[] names = { "Quillledger", string.Empty, "Quillledger", "Brightdesk", "bright desk", "   ", "Tideflow", "Quill Ledger" };
        ProductKind[] kinds = { ProductKind.Game, ProductKind.Saas, ProductKind.Saas, ProductKind.App, ProductKind.Licence, ProductKind.Model, ProductKind.Game, ProductKind.Saas };
        for (int i = 0; i < names.Length; i++)
        {
            f.Products.Add(new Product { Id = i + 1, Place = "Tool Street~1", Name = names[i], Kind = (int)kinds[i], Segment = i % 3, Stage = i % 3 });
        }
        return f;
    }

    private static void TitleOldSaves()
    {
        FirmsFile f = TitleOldFile();
        int named = FirmsRules.NameAll(f, null, null);
        bool kept = f.Products[0].Name == "Quillledger" && f.Products[3].Name == "Brightdesk" && f.Products[6].Name == "Tideflow";
        bool apart = f.Products.All(p => p.Name.Length > 0) && f.Products.Select(p => TitleRules.Key(p.Name)).Distinct().Count() == f.Products.Count;
        bool fits = new[] { 1, 2, 4, 5, 7 }.All(i => TitleStyleOfName(f.Products[i].Name) == TitleRules.StyleOf(f.Products[i].K, f.Products[i].Segment, 0));
        Check(named == 5 && kept && apart && fits,
            "titles old saves: the products with no name, and each second bearer of one, are named by their kind; the first bearer and every name that was its own are kept ("
            + named + " named: " + string.Join(", ", f.Products.Select(p => p.Name)) + ")");
        string[] given = f.Products.Select(p => p.Name).ToArray();
        Check(FirmsRules.NameAll(f, null, null) == 0 && f.Products.Select(p => p.Name).SequenceEqual(given), "titles old saves: asked again, nothing is named and nothing changes");
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(f), out _);
        Check(FirmsRules.NameAll(back, null, null) == 0 && back.Products.Select(p => p.Name).SequenceEqual(given), "titles old saves: the names are in the save's line and are kept when it is read");
        FirmsFile twin = TitleOldFile();
        FirmsRules.NameAll(twin, null, null);
        Check(twin.Products.Select(p => p.Name).SequenceEqual(given), "titles old saves: the same save is given the same names on every machine and every load");
        FirmsFile blocked = TitleOldFile();
        string wanted = given[1];
        FirmsRules.NameAll(blocked, null, n => TitleRules.Same(n, wanted));
        Check(!TitleRules.Same(blocked.Products[1].Name, wanted) && blocked.Products[1].Name.Length > 0, "titles old saves: a name in use elsewhere in the city is passed over");
        // an older line, with fewer fields and no name at all, still loads and is named
        FirmsFile thin = FirmsStore.Read("v=1~h:6|1|5|4~p:1|Tool Street%7E1;2|Tool Street%7E1||2", out int version);
        int thinNamed = FirmsRules.NameAll(thin, null, null);
        Check(version == 1 && thin.Products.Count == 2 && thinNamed == 2 && thin.Products.All(p => p.Name.Length > 0) && thin.Products[0].Name != thin.Products[1].Name,
            "titles old saves: a line with no names at all loads, and its products are named (" + string.Join(", ", thin.Products.Select(p => p.Name)) + ")");
        Check(FirmsStore.Version == 1 && SectorStore.Version >= 1 && StudioShare.Version == 1 && SectorShare.Version == 1,
            "titles old saves: the firms' line and the two shared parts keep their versions: a name is a field they already had, and nothing is added to what is sent");
    }

    // ---------------------------------------------------------------- names made from a game's name

    private static void TitleFromGame()
    {
        var held = new HashSet<string> { TitleRules.Key("Ember Crown 2") };
        Check(TitleRules.Sequel("Ember Crown", null) == "Ember Crown 2" && TitleRules.Sequel("Ember Crown 2", null) == "Ember Crown 3" && TitleRules.Sequel("Ember Crown 9", null) == "Ember Crown 10"
              && TitleRules.Sequel("Ember Crown", n => held.Contains(TitleRules.Key(n))) == "Ember Crown 3" && TitleRules.Sequel("Area 51", null) == "Area 52"
              && TitleRules.Sequel("1", null) == "1 2" && TitleRules.Sequel(string.Empty, null) == string.Empty && TitleRules.Sequel("城市传奇", null) == "城市传奇 2",
            "titles from a game: a sequel is its game's name with the next number, the next free one where that is taken, and nothing for a game with no name");
        string longName = new string('n', 40);
        Check(TitleRules.Sequel(longName, null) == new string('n', 38) + " 2" && TitleRules.Sequel(longName, null).Length == TitleCatalogue.Most,
            "titles from a game: a sequel of the longest name is shortened to fit");
        var addons = new HashSet<string>();
        for (int n = 1; n <= TitleCatalogue.AddOns.Length; n++)
        {
            addons.Add(TitleRules.AddOn("Ember Crown", 14, n));
        }
        Check(addons.Count == TitleCatalogue.AddOns.Length && addons.All(a => a.StartsWith("Ember Crown: ", StringComparison.Ordinal))
              && TitleRules.AddOn("Ember Crown", 14, 17).EndsWith(" 2") && TitleRules.AddOn("Ember Crown", 14, 1) == TitleRules.AddOn("Ember Crown", 14, 0)
              && TitleRules.AddOn(string.Empty, 14, 1) == string.Empty,
            "titles from a game: a game's sixteen add-ons each have their own name after the game's, a seventeenth is numbered, and the same add-on is always called the same");
        Check(TitleRules.Update("Ember Crown", 1) == "Ember Crown 1.1" && TitleRules.Update("Ember Crown", 12) == "Ember Crown 1.12" && TitleRules.Update(null, 3) == string.Empty,
            "titles from a game: a free update is the game's name with its version");
        var g = new GameDev { Id = 14, Updates = 2, AddOns = 1 };
        Check(StudioViews.ProjectTitle(g, "Ember Crown", StudioRules.ProjectUpdate, 1) == "Ember Crown 1.3" && StudioViews.ProjectTitle(g, "Ember Crown", StudioRules.ProjectUpdate, 0) == "Ember Crown 1.2"
              && StudioViews.ProjectTitle(g, "Ember Crown", StudioRules.ProjectAddOn, 1) == TitleRules.AddOn("Ember Crown", 14, 2)
              && StudioViews.ProjectTitle(g, "Ember Crown", StudioRules.ProjectPort, 1) == string.Empty && StudioViews.ProjectTitle(g, "Ember Crown", StudioRules.ProjectLocalise, 1) == string.Empty,
            "titles from a game: the update or add-on being built is the next one, the one just released the last, and a port or localisation has no name of its own");
        Check(TitleRules.By("Brightdesk", "Codeharbour Software") == "Brightdesk (Codeharbour Software)" && TitleRules.By(string.Empty, "Maker") == "Maker" && TitleRules.By("Title", null) == "Title",
            "titles from a game: a product named with its maker is written the product first");
    }

    // ---------------------------------------------------------------- a name moves nothing

    private static void TitleNoEffect()
    {
        MethodInfo[] scoring = typeof(StudioRules).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "Review" || m.Name == "CriticsScore" || m.Name == "Quality" || m.Name == "PartQuality" || m.Name == "Backlash" || m.Name == "Expect").ToArray();
        bool blind = scoring.Select(m => m.Name).Distinct().Count() == 6
                     && scoring.All(m => m.GetParameters().All(p => p.ParameterType != typeof(string) && p.ParameterType != typeof(Product)));
        Check(blind && typeof(GameDev).GetFields().All(x => x.FieldType != typeof(string) || x.Name == "Place") && typeof(RivalGame).GetField("Name") != null,
            "titles no effect: the critics' score and a game's quality are worked out from the game's record, which holds no name: what a game is called cannot move them");
        string rules = ModSource(@"Tech\Studio\StudioRules.cs") + ModSource(@"Tech\Studio\StudioRules.Reviews.cs") + ModSource(@"Tech\Scale\ScaleRules.cs");
        string sales = ModSource(@"Tech\Studio\StudioRules.Sales.cs");
        string reads = System.Text.RegularExpressions.Regex.Replace(rules, @"^\s*//.*$", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);
        int named = System.Text.RegularExpressions.Regex.Matches(reads, @"\.Name\b").Count, selling = System.Text.RegularExpressions.Regex.Matches(sales, @"\.Name\b").Count;
        Check(rules.Length > 0 && named == 0 && selling == 1 && ModSource(@"Tech\Studio\StudioRules.cs").Contains("what the game is called counts for nothing"),
            "titles no effect: the rules of a game's making, its reviews and its sales read no name at all (" + named + " reads), but for the one below (" + selling + ")");
        Check(System.Text.RegularExpressions.Regex.Matches(sales, @"GenreOfName\(").Count == 2 && sales.Contains("Genre = GenreOfName(p.Name, p.Id, seed)"),
            "titles no effect: the one rule that reads a name is the genre an older save's game is taken in with, once, a choice a concept is free to make");
        string titles = ModSource(@"Tech\TechDirector.Titles.cs");
        Check(titles.Contains("if (p == null || p.Stage >= 2)") && titles.Contains("TitleRules.Check(name, n => taken.Contains(TitleRules.Key(n)))") && !titles.Contains("FirmsGame.")
              && !titles.Contains("Hype") && !titles.Contains("Reputation") && !titles.Contains("modData") && !titles.Contains("System.IO") && !titles.Contains("Path."),
            "titles no effect: a rename is free until the product is withdrawn, checked as a typed name is, books no money, moves no hype or reputation, and is no key or file name");
    }

    // ---------------------------------------------------------------- the rivals' products

    private static void TitleRivalProducts()
    {
        var f = new SectorFile { Seed = 3, FirstDay = 1 };
        f.Rivals.Add(new RivalProduct { Id = 4, Company = 0, Market = (int)SectorMarket.BusinessSoftware, Name = "Codeharbour Software", Features = 5, Customers = 900 });
        f.Rivals.Add(new RivalProduct { Id = 5, Startup = 12, Market = (int)SectorMarket.ConsumerAi, Name = "Fernhollow Labs", Features = 3, Customers = 40 });
        f.NextRival = 6;
        RivalProduct a = f.Rivals[0], b = f.Rivals[1];
        Check(a.Title == TitleRules.Rival(4, (int)SectorMarket.BusinessSoftware) && b.Title == TitleRules.Rival(5, (int)SectorMarket.ConsumerAi) && a.Title != b.Title
              && a.Called == a.Title + " (Codeharbour Software)" && a.Name == "Codeharbour Software",
            "titles rivals: a rival's product has a name of its market's style beside its maker's (" + a.Called + ", " + b.Called + ")");
        string line = SectorStore.Write(f), shared = SectorShare.Write(f);
        SectorFile back = SectorStore.Read(line, out _);
        Check(!line.Contains(a.Title) && !line.Contains(b.Title) && !shared.Contains(a.Title) && !shared.Contains(b.Title) && back.Rivals[0].Title == a.Title && back.Rivals[1].Title == b.Title,
            "titles rivals: the name is made from the rival's id and market, so nothing of it is saved or sent, and a host and a guest read the same");
        a.Id = 44;
        Check(a.Title == TitleRules.Rival(44, (int)SectorMarket.BusinessSoftware), "titles rivals: a record read into the same object is named for what it now is");
        var words = new PlainWords();
        string page = ModSource(@"Tech\Sector\SectorViews.Page.cs"), views = ModSource(@"Tech\Sector\SectorViews.cs"), day = ModSource(@"Tech\TechDirector.Sector.cs");
        Check(page.Contains("table.Rows.Add(new[] { rp.Called, fmt.Text(MarketKey(rp.Market))") && views.Contains("\"leader\", lead != null ? lead.Called : string.Empty")
              && views.Contains("ViewTone.Plain, \"name\", best.Called"),
            "titles rivals sources: the rivals' table, a market's leader and a product's rivals name the product with its maker");
        Check(day.Contains("Data(\"rival\", r.Called, \"market\", market, \"features\"") && day.Contains("Story(\"reallife_tech_mkt_news_ship\", \"name\", r.Called")
              && day.Contains("Data(\"rival\", r.Called, \"market\", market), TabProducts") && day.Contains("Story(\"reallife_tech_mkt_news_cut\", \"name\", r.Called")
              && day.Contains("Story(\"reallife_tech_mkt_news_outage\", \"name\", r.Called")
              && ModSource(@"Tech\TechDirector.SectorDesk.cs").Contains("Story(\"reallife_tech_mkt_news_benchmark\", \"product\", subject, \"rival\", beaten.Called)"),
            "titles rivals sources: the notices and the paper's lines of a rival's feature, price cut, outage and lost benchmark name its product");
        Check(ModSource(@"Tech\TechDirector.Rivalry.cs").Contains("Name = r.Name, Title = r.Title,") && ModSource(@"Ui\Tech\TechLive.Rivals.cs").Contains("card.Who(TitleRules.By(r.Title, r.Name),")
              && ModSource(@"Tech\Firms\TechApp.Rivals.cs").Contains("_director.RivalTitles(c)") && ModSource(@"Ui\Tech\TechLive.Contracts.cs").Contains("_d.RivalTitles(c)"),
            "titles rivals sources: a niche's rivals and a competitor's page, on both faces, name what the rival sells");
        Check(words.Text("x") == "x", "titles rivals: the plain words stand in for the game's");
    }

    // ---------------------------------------------------------------- the screens

    private static void TitleScreens()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var words = new PlainWords();
        StudioFile f = RichStudio();
        string[] trades = { "reallife_studio_name_suggested", "reallife_studio_name_typed", "reallife_studio_name_sequel" };
        bool all = true;
        string why = "none";
        for (int from = 0; from < trades.Length; from++)
        {
            var place = new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", PointsADay = 80, People = 10, Title = "Hollow Manor", TitleFrom = from, GameName = id => "Game " + id };
            StudioScreen v = StudioViews.Concept(f, new GameDraft { Place = StudioPlace, Genre = 8, Sequel = from == StudioViews.TitleSequel ? 52 : 0 }, place, 500, words);
            ViewChoice name = v.Sections[0].Choices.FirstOrDefault(c => c.Act == StudioAct.Name);
            bool fine = ScreenFine(v, en, out why) && name != null && name.Options.Count == 1 && name.Options[0] == "Hollow Manor" && name.Trade != null && name.Trade.Key == trades[from]
                        && name.Go != null && name.Go.Act == StudioAct.Name && name.Go.Key == "reallife_studio_button_name" && v.Others.Any(a => a.Act == StudioAct.Suggest)
                        && v.Sections[0].Choices[0].Act == StudioAct.Genre && v.Primary != null && v.Primary.Act == StudioAct.Pitch;
            all &= fine;
            ViewChoice sequel = v.Sections[0].Choices.FirstOrDefault(c => c.Act == StudioAct.Sequel);
            all &= sequel == null || sequel.Options.Skip(1).All(o => o.StartsWith("Game 5", StringComparison.Ordinal));
        }
        Check(all, "titles screens: the concept shows what the game will be called and how the name came, with the buttons to type one and to be offered another (" + why + ")");
        StudioScreen bare = StudioViews.Concept(f, new GameDraft { Place = StudioPlace }, new StudioPlace { Place = StudioPlace, Name = "Kestrel Works" }, 500, words);
        Check(bare.Sections[0].Choices.All(c => c.Act != StudioAct.Name) && bare.Others.All(a => a.Act != StudioAct.Suggest), "titles screens: a screen built with no name asks for none");
        var studio = new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", PointsADay = 80, People = 10 };
        GameDev live = f.Games[2];
        live.Phase = (int)GamePhase.Live;
        live.Project = StudioRules.ProjectAddOn;
        live.ProjectWork = 100;
        StudioScreen game = StudioViews.Game(f, live, "Copper Crown", studio, 1000, 100, 700, 365, 0, 0, words);
        ViewSection after = game.Sections.FirstOrDefault(s => s.TitleKey == "reallife_studio_sec_after");
        ViewLine called = after != null ? after.Lines.FirstOrDefault(l => l.Key == "reallife_studio_after_named") : null;
        Check(ScreenFine(game, en, out why) && game.Others.Any(a => a.Act == StudioAct.Name && a.Key == "reallife_tech_button_rename") && called != null
              && called.Pairs[1] == TitleRules.AddOn("Copper Crown", live.Id, live.AddOns + 1),
            "titles screens: a game's page has the button that renames it, and names the add-on being built after the game (" + why + ")");
        live.Phase = (int)GamePhase.Ended;
        StudioScreen ended = StudioViews.Game(f, live, "Copper Crown", studio, 1000, 100, 700, 365, 0, 0, words);
        Check(ended.Others.All(a => a.Act != StudioAct.Name), "titles screens: a withdrawn game has no button to rename it");
        Check(StudioAct.Name > StudioAct.Greenlight && StudioAct.Suggest > StudioAct.Greenlight && !StudioPages.Direct(StudioAct.Name) && StudioPages.Direct(StudioAct.Suggest)
              && !StudioPages.Opens(StudioAct.Name) && !StudioPages.Opens(StudioAct.Suggest),
            "titles screens: a typed name is asked for by the page that shows it, never run as a button of a firm's page; another suggestion is the director's own to give");
    }

    // ---------------------------------------------------------------- every place that shows a product

    private static void TitleSources()
    {
        string old = ModSource(@"Tech\Firms\TechApp.Pages.cs"), kit = ModSource(@"Ui\Tech\TechLive.Products.cs"), firm = ModSource(@"Ui\Tech\TechLive.FirmPage.cs");
        string gamePage = ModSource(@"Ui\Tech\TechLive.Game.cs"), studioOld = ModSource(@"Tech\Studio\TechApp.Studio.cs");
        Check(old.Contains("row.Set(0, p.Name);") && old.Contains("box.Title(p.Name);") && old.Contains("box.Subtitle(TechText.ProductName(p.K));")
              && old.Contains("row.Set(2, TechText.ProductName(p.K)"),
            "titles sources: the older pages' products table and a product's page show the name first and the kind second");
        Check(kit.Contains("row.Cells[0] = p.Name;") && kit.Contains("row.Sub = TechText.ProductName(p.K)") && kit.Contains("Name = p.Name, Icon = TechGlyphs.Of(TechGlyphs.Product, p.Kind), Role"),
            "titles sources: the kit's products table and a product's page show the name first and the kind under it");
        Check(firm.Contains("Title = best.Name") && firm.Contains("p.Name, g != null ? TechText.T(StudioViews.GenreKey(g.Genre)) : TechText.ProductName(p.K)")
              && firm.Contains("row.Cells[0] = p != null ? p.Name : \"-\";"),
            "titles sources: a firm's page names the product being made, its products out and its award winners");
        Check(gamePage.Contains("Title = p.Name,") && gamePage.Contains("Label = p.Name, Values = mine") && gamePage.Contains("Label = rival.Name + \", \" + StudioCatalogue.RivalNames")
              && gamePage.Contains("Name = p.Name, Icon"),
            "titles sources: a game's page, its plan and its sales chart name the game, and the rival's line the rival game");
        Check(ModSource(@"Tech\Sector\SectorViews.Page.cs").Contains("p.Name, fmt.Text(Mod + \"_tech_product_\"") && ModSource(@"Tech\Studio\StudioViews.Page.cs").Contains("rg.Name, StudioCatalogue.RivalNames")
              && ModSource(@"Tech\Studio\StudioViews.cs").Contains("\"studio\", StudioCatalogue.RivalNames[r.Studio], \"game\", r.Name"),
            "titles sources: the firms' own pages name their products with the kind beside, and the rival studios' games by name");
        // the typed name: asked on both faces for a new product, a product's page, a concept and a game, and given to the director alone
        Check(old.Contains("_director.NewProduct(place, kind, (Segment)segment, scope, typed, turn)") && old.Contains("AskName(now, false, text => _director.RenameProduct(id, text))")
              && old.Contains("_director.SuggestTitle(kind, _newSegment, turn)") && old.Contains("_director.TypedTitle(text, out string name) == TitleRules.Ok")
              && old.Contains("GameUi.TextBox.Ask(\"reallife_tech_name_placeholder\", \"common_confirm\"")
              && old.Replace("\r\n", "\n").Contains("allowEmpty,\n                TitleCatalogue.Most);"),
            "titles sources: the older pages ask for a new product's name and a rename in the game's text box, at most the longest name");
        Check(kit.Contains("_d.NewProduct(place, kind, (Segment)segment, scope, typed, turn)") && kit.Contains("NameRun(p.Name, false, text => _d.RenameProduct(pid, text))")
              && kit.Contains("_d.SuggestTitle(kind, _newSegment, turn)") && kit.Contains("_d.TypedTitle(text, out string name) == TitleRules.Ok")
              && kit.Contains("GameUi.TextBox.Ask(\"reallife_tech_name_placeholder\", \"common_confirm\"") && kit.Contains("allowEmpty, TitleCatalogue.Most);"),
            "titles sources: the kit's pages ask for a new product's name and a rename in the same box, at most the longest name");
        Check(studioOld.Contains("case StudioAct.Name:") && studioOld.Contains("AskName(_director.TitleOf(id), false, text => _director.RenameProduct(id, text))")
              && studioOld.Contains("AskName(_director.DraftTyped(place), true, text => _director.NameDraft(place, text))") && studioOld.Contains("box.Row(c.LabelKey, c.Options[0]);")
              && firm.Contains("NameRun(_d.TitleOf(id), false, text => _d.RenameProduct(id, text)) : NameRun(_d.DraftTyped(place), true, text => _d.NameDraft(place, text))")
              && gamePage.Contains("a.Act == StudioAct.Name && GameUi.TextBox.CanType") && kit.Contains("v.Others[i].Act == StudioAct.Suggest"),
            "titles sources: a concept's name and a game's rename are asked for on both faces, an empty box handing a concept's name back to the studio");
        // sorting by name: the game's own table by its text column, the kit's from the head of its first column
        string screen = ModSource(@"Ui\Tech\TechScreen.cs");
        Check(ModSource(@"Tech\Firms\TechColumns.cs").Contains(".Column(\"reallife_tech_col_product\", 300f, TableSort.Text, flexible: true)")
              && kit.Contains("string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)") && kit.Contains("table.Columns[0].Sortable = true;")
              && kit.Contains("List<Product> list = ProductsInOrder(null);") && firm.Contains("List<Product> list = ProductsInOrder(place);") && firm.Contains("NameSort(_m.Table);")
              && screen.Contains("case TechFace.TableBase + UiDataTable.Head:") && screen.Contains("!_m.Table.Columns[arg].Sortable"),
            "titles sources: the products' tables sort by name on both faces, the kit's from its head, the same on every machine whatever its language");
        // the director: the name given, the notices and the paper
        string desk = ModSource(@"Tech\TechDirector.StudioDesk.cs"), studioDay = ModSource(@"Tech\TechDirector.Studio.cs"), firmsApp = ModSource(@"Tech\TechDirector.FirmsApp.cs");
        string firmsDay = ModSource(@"Tech\TechDirector.Firms.cs");
        Check(desk.Contains("string name = DraftTitle(d, out _);") && desk.Contains("case StudioAct.Suggest: SuggestDraft(place); break;") && desk.Contains("s.GameName = NameOf;")
              && desk.Contains("place.Title = DraftTitle(d, out int from);") && firmsApp.Contains("name = SuggestTitle(kind, (int)segment, turn);")
              && firmsApp.Contains("FirmsRules.NewProduct(_firmsFile, place, kind, segment, scope, name)") && firmsDay.Contains("NameOldProducts();"),
            "titles sources: the pitch gives the game the name its concept showed, a product started the name its page showed, and an older save is named when it loads");
        Check(studioDay.Contains("StudioViews.ProjectTitle(g, p.Name, project, 0)") && studioDay.Contains("called.Length > 0 ? NamedNotes[project]")
              && studioDay.Contains("!fresh && !SharedCity.Follows(StudioShare.Part) ? StudioRules.NameRivals(_studio) : 0")
              && System.Text.RegularExpressions.Regex.Matches(studioDay + desk, "\"game\", p\\.Name").Count >= 12,
            "titles sources: the studio's notices and the paper name the game, a released update or add-on its own name, and a guest never renames the host's rival games");
        Check(System.Text.RegularExpressions.Regex.Matches(firmsDay + firmsApp + ModSource(@"Tech\TechDirector.Sector.cs"), "\"product\", p\\.Name|p\\.Name, r\\.").Count >= 9,
            "titles sources: the firms' notices and the paper's lines of a launch, an outage, a breach and a lead name the product");
        // the audit of typed answers, and one place alone that opens the box for these names
        Check(SweepAudit.Rows.Any(r => r.Kind == AuditWindow.TextInput && r.Area == "Tech/Firms/" && r.Runner == SweepAudit.Text)
              && !studioOld.Contains("TextBox.Ask(") && !ModSource(@"Tech\TechDirector.Titles.cs").Contains("TextBox.Ask("),
            "titles sources: the typed name has its row in the audit of windows, and the Tech app's one method opens the box");
    }

    // ---------------------------------------------------------------- the texts

    private static void TitleTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_tech_label_name", "reallife_tech_name_placeholder", "reallife_tech_name_suggested", "reallife_tech_name_typed", "reallife_tech_button_name",
            "reallife_tech_button_suggest", "reallife_tech_button_rename", "reallife_tech_name_taken", "reallife_tech_name_empty", "reallife_tech_name_closed", "reallife_tech_rival_titles",
            "reallife_studio_label_name", "reallife_studio_name_typed", "reallife_studio_name_suggested", "reallife_studio_name_sequel", "reallife_studio_button_name",
            "reallife_studio_button_suggest", "reallife_studio_after_named", "reallife_studio_note_named_1", "reallife_studio_note_named_2"
        };
        string missing = keys.FirstOrDefault(k => !en.TryGetValue(k, out string v) || string.IsNullOrEmpty(v)) ?? "none";
        Check(missing == "none", "titles texts: every text of the names is in English (" + missing + ")");
        if (missing != "none")
        {
            return;
        }
        string[] notices = { "reallife_tech_name_taken", "reallife_tech_name_empty", "reallife_tech_name_closed", "reallife_studio_note_named_1", "reallife_studio_note_named_2" };
        Check(notices.All(k => en[k].Length + 2 * TitleCatalogue.Most <= 240), "titles texts: each notice, with two of the longest names in it, is within a notice's 240 characters");
        Check(en["reallife_tech_name_taken"].Contains("{name}") && en["reallife_studio_note_named_1"].Contains("{name}") && en["reallife_studio_note_named_1"].Contains("{game}")
              && en["reallife_studio_note_named_1"].Contains("{copies}") && en["reallife_studio_note_named_2"].Contains("{name}") && en["reallife_studio_note_named_2"].Contains("{game}")
              && en["reallife_studio_note_named_2"].Contains("{copies}") && en["reallife_studio_after_named"].Contains("{name}") && en["reallife_tech_rival_titles"].Contains("{names}"),
            "titles texts: each text names what its line fills in");
        Check(keys.All(k => !en[k].Contains("!") && !en[k].Contains(((char)0x2014).ToString()) && !en[k].Contains(((char)0x2013).ToString()) && !en[k].Contains(" - ")), "titles texts: no dashes and no exclamation marks");
        // the row that holds the line: the next row while it is unreleased, the version's own row afterwards
        string next = en.Where(p => p.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && p.Value.Contains("Names for tech products and games:"))
            .Select(p => p.Value).FirstOrDefault() ?? string.Empty;
        int at = next.IndexOf("Names for tech products and games:", StringComparison.Ordinal), fix = next.IndexOf("Fixed: ", StringComparison.Ordinal);
        Check(at > 0 && fix > at && next.IndexOf("Names for tech products and games:", at + 1, StringComparison.Ordinal) < 0,
            "titles texts: What's new says it once, among the features, before the first fix");
        Check(en["reallife_guide_tech_sector"].Contains("type a name or keep the one suggested") && en["reallife_guide_tech_sector"].Contains("rename it from its page at any time until it is withdrawn")
              && en["reallife_guide_studio"].Contains("keep the one the studio suggests for its genre") && en["reallife_guide_studio"].Contains("carries its name with the next number")
              && en["reallife_guide_studio"].Contains("each named after the game") && en["reallife_guide_firms"].Contains("each product under a name of its own"),
            "titles texts: the Guide's pages on the tech firms and the game studio say how products and games are named");
    }
}
