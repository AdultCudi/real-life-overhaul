using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;

// The player's age window (Social/RomanceRules): two options, how many years younger and how many older than the player somebody they could fall for
// may be, each 2 to 25 and 8 (the gap the city's own couples keep) unless moved. A player (24 Nov) asked for a slider "in case some people find it too
// broad or too narrow". The guarantees of the single gap hold inside any window, asymmetric ones too: whoever the mod makes for the player is made one
// year inside each edge, never under eighteen, and never leaves the window at a birthday; whoever the player is with is never parted by a narrower one;
// the city's own couples are not the option's business.
internal static partial class Program
{
    private static readonly (int Younger, int Older)[] RomanceWindows =
    {
        (2, 2), (2, 25), (25, 2), (3, 12), (12, 3), (8, 8), (5, 20), (20, 5), (25, 25), (2, 8), (8, 25), (14, 4), (6, 7), (4, 17)
    };

    private static void RomanceWindow()
    {
        int gap = RomanceRules.AgeGap;
        try
        {
            RomanceWindowTables();
            RomanceWindowBirthdays();
            RomanceWindowCasts();
        }
        finally
        {
            RomanceRules.Window(gap, gap);
        }
        RomanceWindowKept();
        RomanceWindowSources();
    }

    // The verdicts and the ages made, window by window.
    private static void RomanceWindowTables()
    {
        const int gap = RomanceRules.AgeGap;
        Check(RomanceRules.Younger == gap && RomanceRules.Older == gap && RomanceRules.WindowLeast == 2 && RomanceRules.WindowMost == 25 && gap == 8,
            "romance window: left alone each side is " + gap + " years (today's gap) and the options run from 2 to 25");
        RomanceRules.Window(0, 0);
        bool low = RomanceRules.Younger == 2 && RomanceRules.Older == 2;
        RomanceRules.Window(100, 1000);
        bool high = RomanceRules.Younger == 25 && RomanceRules.Older == 25;
        RomanceRules.Window(-5, 7);
        bool mixed = RomanceRules.Younger == 2 && RomanceRules.Older == 7;
        RomanceRules.Window(13, 21);
        mixed &= RomanceRules.Younger == 13 && RomanceRules.Older == 21;
        Check(low && high && mixed, "romance window: a value outside 2 to 25 years (a saved one from nowhere) is brought into it for each side alone, so no window rules everybody out");

        // Left alone nothing changes: the verdicts and the ages made are the ones every version gave.
        RomanceRules.Window(gap, gap);
        bool same = true, ages = true;
        foreach (int player in new[] { 0, 18, 19, 25, 26, 40, 70, 92 })
        {
            for (int age = 0; age <= 100; age++)
            {
                same &= RomanceRules.AgeFits(age, player) == (player <= 0 || (age >= 18 && Math.Abs(age - player) <= gap));
                same &= RomanceRules.AgeSteady(age, player) == (player <= 0 || (age >= 18 && Math.Abs(age - player) <= gap - 1));
            }
            for (int roll = -3; roll < 120; roll++)
            {
                int low2 = Math.Max(18, player - gap + 1), high2 = Math.Min(85, player + gap - 1);
                int old = high2 < low2 ? Math.Max(18, Math.Min(85, player)) : low2 + (roll & int.MaxValue) % (high2 - low2 + 1);
                ages &= RomanceRules.AgeNear(player, roll) == old;
            }
        }
        Check(same && ages, "romance window: with both options left at " + gap + " every verdict and every age made are exactly what they were before the options (the rules of the single gap, every player of 0 to 92)");

        bool table = true, steadyTable = true, near = true, steady = true, covered = true, determined = true, adults = true, edges = true;
        foreach ((int y, int o) in RomanceWindows)
        {
            RomanceRules.Window(y, o);
            foreach (int player in new[] { 18, 19, 20, 25, 26, 30, 43, 70, 90 })
            {
                for (int age = 0; age <= 100; age++)
                {
                    bool fits = age >= 18 && age >= player - y && age <= player + o;
                    table &= RomanceRules.AgeFits(age, player) == fits && RomanceRules.AgeFits(age, player, y, o) == fits;
                    steadyTable &= RomanceRules.AgeSteady(age, player) == (age >= 18 && age >= player - y + 1 && age <= player + o - 1);
                }
                int first = Math.Max(18, player - y + 1), last = Math.Min(85, player + o - 1);
                bool room = last >= first;
                var seen = new HashSet<int>();
                for (int roll = -2; roll < 300; roll++)
                {
                    int age = RomanceRules.AgeNear(player, roll);
                    near &= (!room && age >= 18 && age <= 85) || (RomanceRules.AgeFits(age, player) && age >= 18 && age <= 85);
                    steady &= !room || RomanceRules.AgeSteady(age, player);
                    adults &= age >= RomanceRules.AgeLeast;
                    determined &= age == RomanceRules.AgeNear(player, roll) && age == RomanceRules.AgeNear(player, roll, y, o);
                    seen.Add(age);
                }
                covered &= seen.Count == (room ? last - first + 1 : 1);
                if (room)
                {
                    // One year inside each edge: the youngest made is a year inside the younger edge (or eighteen), the oldest a year inside the older.
                    int youngest = int.MaxValue, oldest = int.MinValue;
                    foreach (int a in seen)
                    {
                        youngest = Math.Min(youngest, a);
                        oldest = Math.Max(oldest, a);
                    }
                    edges &= youngest == first && oldest == last;
                    edges &= player - y < 18 || !RomanceRules.AgeSteady(player - y, player);   // on the younger edge itself: made again, never made
                    edges &= player + o > 85 || !RomanceRules.AgeSteady(player + o, player);   // ...and on the older edge
                    edges &= RomanceRules.AgeFits(first, player) && RomanceRules.AgeFits(last, player);
                }
            }
        }
        Check(table, "romance window: somebody fits when they are an adult and at most the younger years under the player's age and the older years over it, edges included, for each of " + RomanceWindows.Length + " windows (asymmetric ones among them)");
        Check(steadyTable && edges, "romance window: somebody stays inside through every birthday when they are an adult and at most one year short of each edge, for each window, each side by its own number of years");
        Check(near && steady && covered && determined, "romance window: an age made to fit a player always fits and is steady, covers the whole window inside its edges and is the same every time, for every window");
        Check(adults, "romance window: nobody is ever made under eighteen, whatever the window (a player of 18 with 25 years younger is still 18 to 42)");
    }

    // Whoever is made keeps fitting through every pair of birthdays, three years of days, window by window; somebody on an edge does not.
    private static void RomanceWindowBirthdays()
    {
        bool drawn = true, leaves = true;
        string fault = string.Empty;
        foreach ((int y, int o) in RomanceWindows)
        {
            RomanceRules.Window(y, o);
            foreach (int player in new[] { 18, 19, 24, 30, 52, 70 })
            {
                var ages = new HashSet<int>();
                for (int roll = 0; roll < 60; roll++)
                {
                    ages.Add(RomanceRules.AgeNear(player, roll));
                }
                foreach (int age in ages)
                {
                    for (int birthday = 1; birthday <= RomanceYear; birthday++)
                    {
                        var p = new SocialPerson { Compatible = true, Age = age, AgeDay = 1, Birthday = birthday };
                        for (int mine = 1; mine <= RomanceYear; mine += 3)
                        {
                            bool ok = RomanceKeepsFitting(p, player, mine, RomanceYear);
                            drawn &= ok;
                            if (!ok && fault.Length == 0)
                            {
                                fault = "window " + y + "/" + o + ", a player of " + player + " (birthday " + mine + "), somebody of " + age + " (birthday " + birthday + ")";
                            }
                        }
                    }
                }
            }
            // Somebody exactly on an edge is out of the window at some birthday of every year (why nobody is made there): the younger edge too.
            bool olderEdge = false, youngerEdge = false;
            for (int birthday = 1; birthday <= RomanceYear; birthday++)
            {
                for (int mine = 1; mine <= RomanceYear; mine += 3)
                {
                    olderEdge |= !RomanceKeepsFitting(new SocialPerson { Compatible = true, Age = 40 + o, AgeDay = 1, Birthday = birthday }, 40, mine, RomanceYear);
                    youngerEdge |= !RomanceKeepsFitting(new SocialPerson { Compatible = true, Age = 40 - y, AgeDay = 1, Birthday = birthday }, 40, mine, RomanceYear);
                }
            }
            leaves &= olderEdge && youngerEdge;
        }
        Check(drawn, "romance window: somebody of an age made to fit a player is inside the window on every day of three years, for every pair of birthdays, in each of " + RomanceWindows.Length + " windows" + (fault.Length > 0 ? "; " + fault : string.Empty));
        Check(leaves, "romance window: somebody exactly on either edge of a window is out of it for part of every year, so nobody is made there, on the younger side as on the older");
    }

    // The cast, the people met later, and somebody drawn again, under windows that are not the gap.
    private static void RomanceWindowCasts()
    {
        bool fit = true, inner = true, kept = true, fresh = true, rest = true, again = true;
        string fault = string.Empty;
        var people = new SocialPerson[SocialRules.BaseCast];
        var plain = new SocialPerson[SocialRules.BaseCast];
        var nobody = new SocialPerson[1];
        foreach ((int y, int o) in new[] { (2, 25), (25, 2), (3, 12), (12, 3), (2, 2), (20, 5) })
        {
            RomanceRules.Window(y, o);
            foreach (int player in new[] { 18, 24, 31, 45, 63 })
            {
                for (int seed = 1; seed <= 80; seed++)
                {
                    SocialCatalogue.MakeCast(people, seed, 1, RomanceYear, RomanceDistricts, player, RomanceRules.Anyone);
                    RomanceRules.Window(RomanceRules.AgeGap, RomanceRules.AgeGap);
                    SocialCatalogue.MakeCast(plain, seed, 1, RomanceYear, RomanceDistricts);   // (no player's age: nobody is redrawn)
                    RomanceRules.Window(y, o);
                    for (int i = 0; i < people.Length; i++)
                    {
                        SocialPerson p = people[i];
                        rest &= p.Seed == plain[i].Seed && p.Traits == plain[i].Traits && p.Birthday == plain[i].Birthday && p.Compatible == plain[i].Compatible && p.Job == plain[i].Job;
                        if (!p.Compatible)
                        {
                            continue;
                        }
                        bool ok = RomanceRules.Fits(RomanceRules.Anyone, p.Female, p.Age, player) && RomanceRules.AgeSteady(p.Age, player) && p.Age >= 18;
                        fit &= ok;
                        if (!ok && fault.Length == 0)
                        {
                            fault = "window " + y + "/" + o + ", seed " + seed + ", player " + player + ", " + p.Name + " of " + p.Age;
                        }
                        for (int mine = 1; mine <= RomanceYear; mine += 9)
                        {
                            inner &= RomanceKeepsFitting(p, player, mine, RomanceYear);
                        }
                    }
                }
                for (int index = 8; index < 40; index++)
                {
                    SocialPerson p = SocialCatalogue.MakeNew(index, SocialOrigin.Street, -1, 5, 40, RomanceYear, RomanceDistricts, nobody, 0, player, RomanceRules.Anyone);
                    SocialPerson q = SocialCatalogue.MakeNew(index, SocialOrigin.Street, -1, 5, 40, RomanceYear, RomanceDistricts, nobody, 0, player, RomanceRules.Anyone);
                    again &= p.Age == q.Age && p.Name == q.Name;
                    fresh &= !p.Compatible || (RomanceRules.AgeSteady(p.Age, player) && RomanceRules.Fits(RomanceRules.Anyone, p.Female, p.Age, player));
                    for (int mine = 1; p.Compatible && mine <= RomanceYear; mine += 9)
                    {
                        fresh &= RomanceKeepsFitting(p, player, mine, RomanceYear);
                    }
                }
            }
            // Somebody rolled on or beyond an edge is made again inside it; one a year inside is left as rolled (a player of 60, so 18 is never the edge).
            RomanceRules.Window(y, o);
            foreach (int age in new[] { 60 - y - 3, 60 - y, 60 + o, 60 + o + 3 })
            {
                var p = new SocialPerson { Compatible = true, Female = true, Age = age, Seed = 41 };
                kept &= SocialCatalogue.FitToPlayer(p, false, 60, RomanceRules.Anyone) && RomanceRules.AgeSteady(p.Age, 60) && p.Age >= 18;
            }
            foreach (int age in new[] { 60 - y + 1, 60 + o - 1, 60 })
            {
                var p = new SocialPerson { Compatible = true, Female = true, Age = age, Seed = 41 };
                kept &= !SocialCatalogue.FitToPlayer(p, false, 60, RomanceRules.Anyone) && p.Age == age;
            }
        }
        Check(fit && inner, "romance window: everybody of a new cast open to a romance fits the player's window, a year inside each edge and an adult, and keeps fitting at every birthday, under six windows (asymmetric ones: 2 under and 25 over, 25 under and 2 over)" + (fault.Length > 0 ? "; " + fault : string.Empty));
        Check(fresh && again, "romance window: somebody new to be met fits it the same way and is the same person every time");
        Check(kept, "romance window: a person rolled on or past either edge is made again inside it, and one a year inside either edge (or in the middle) is left as rolled");
        Check(rest, "romance window: nothing else of the cast moves with the window: the same seed deals the same people, traits, birthdays and openness whatever the options say");
    }

    // The people the player is with, and the city's couples, are not touched by a narrower window.
    private static void RomanceWindowKept()
    {
        int gap = RomanceRules.AgeGap;
        const int day = 600;
        try
        {
            RomanceRules.Window(2, 2);
            var dating = new SocialPerson { Compatible = true, Female = true, Age = 60, AgeDay = day, Birthday = 3, Stage = SocialStage.Dating, StageDay = 10, Dates = 6, Affection = 85, Trust = 75, Familiarity = 85, Met = true };
            var partner = new SocialPerson { Compatible = true, Female = false, Age = 19, AgeDay = day, Birthday = 3, Stage = SocialStage.Partner, StageDay = 10, Affection = 90, Trust = 90, Familiarity = 90, Met = true };
            bool outside = !SocialRules.Romantic(dating, RomanceRules.Anyone, 30, day, RomanceYear) && !SocialRules.Romantic(partner, RomanceRules.Anyone, 45, day, RomanceYear);
            bool kept = !SocialRules.BrokenUp(dating) && !SocialRules.BrokenUp(partner) && SocialRules.Earned(dating, day) == SocialStage.Dating && SocialRules.Earned(partner, day) == SocialStage.Partner
                        && SocialRules.WouldCommit(dating, day) && dating.Together && partner.Together;
            Check(outside && kept, "romance window: somebody the player is dating, or with, who the narrower window would now rule out is never parted from them (not broken up, same stage, the next step still theirs): only affection and trust end it");
        }
        finally
        {
            RomanceRules.Window(gap, gap);
        }
        string person = ModSource(@"Social\PersonDialog.cs");
        int official = person.IndexOf("_actions.Add(\"official\");", StringComparison.Ordinal);
        string beforeOfficial = official > 200 ? person.Substring(official - 200, 200) : string.Empty;
        Check(person.Contains("if (LoveHooks.MayAskOut(_p))") && ModSource(@"Social\Love\LoveHooks.cs").Contains("return p != null && !p.Together && p.Stage >= SocialStage.Friend && CanDate && SocialLife.Romantic(p);") && beforeOfficial.Contains("_p.Stage == SocialStage.Dating")
              && !beforeOfficial.Contains("Romantic"),
            "romance window: only Ask them out reads the window, and only for somebody the player is not with; making it official asks the stage and the figures alone");

        string life = ModSource(@"Population\LifeRules.cs");
        Check(life.Contains("public const int AgeGapMost = 8;") && !life.Contains("RomanceRules.Window") && !life.Contains("RomanceYounger") && !life.Contains("RomanceOlder") && !life.Contains("Settings.")
              && RomanceRules.AgeGap == LifeRules.AgeGapMost,
            "romance window: the city's own couples (LifeRules) keep their gap of " + LifeRules.AgeGapMost + " years, which the options never touch");
    }

    // Where the options are read, the texts, and where they stand.
    private static void RomanceWindowSources()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var readers = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = System.IO.File.ReadAllText(file);
            if (code.Contains("RomanceYounger") || code.Contains("RomanceOlder"))
            {
                readers.Add(file.Substring(root.Length + 1));
            }
        }
        readers.Sort(StringComparer.Ordinal);
        Check(string.Join(",", readers) == @"Core\RealLifeOverhaulMod.cs,Core\Settings.cs,Meta\FeatureList.Life.cs,Social\SocialLife.cs",
            "romance window: the options are read in one place, SocialLife.ApplyWindow (beside the wish, which every judge of somebody's age asks), after Settings and the options page (" + string.Join(", ", readers) + ")");
        string life = ModSource(@"Social\SocialLife.cs");
        string interest = MemberText(life, "public static int Interest");
        Check(interest.Contains("ApplyWindow();") && life.Contains("RomanceRules.Window(Settings.RomanceYounger, Settings.RomanceOlder);"),
            "romance window: asking who the player is interested in hands the rules the window too, so a value moved by a preset or a reset is never stale");

        string rules = ModSource(@"Social\RomanceRules.cs"), settings = ModSource(@"Core\Settings.cs");
        Check(rules.Contains("public const int AgeGap = RealLifeOverhaul.Population.LifeRules.AgeGapMost;") && rules.Contains("public static int Younger { get; private set; } = AgeGap;")
              && rules.Contains("public static int Older { get; private set; } = AgeGap;") && settings.Contains("RomanceRules.Window(RomanceYounger, RomanceOlder);"),
            "romance window: the window starts at the city's gap and only Window() moves it");

        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        string Of(string key) => en != null && en.TryGetValue(key, out string text) ? text : string.Empty;
        Check(Of("reallife_romance_younger") == "Partners younger than you, at most" && Of("reallife_romance_older") == "Partners older than you, at most"
              && Of("reallife_opt_tip_romance_younger").Contains("2 to 25") && Of("reallife_opt_tip_romance_older").Contains("2 to 25") && Of("reallife_opt_tip_romance_younger").Contains("already with"),
            "romance window: the two sliders are called what the players asked for and their tips say the range and that the people the player is with stay");
        string guide = Of("reallife_guide_social");
        Check(guide.Contains("{social_age_gap}") && guide.Contains("{social_age_least}") && guide.Contains("{social_age_most}") && guide.Contains("'Partners younger than you, at most'")
              && guide.Contains("never parted from you by a narrower window"),
            "romance window: the Guide says how the window is set, its range and default, and that nobody the player is with is parted by it");
    }
}
