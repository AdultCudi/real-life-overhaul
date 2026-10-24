using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Seasons;

// The seasons (Scripts/Seasons): the goods' table (every figure in bounds, every year near even, no good twice), the
// season of a day against the calendar's for every year length, the day whose customers a moment belongs to, the snow
// of a day (only in winter, in its hours, over by midnight, about as often as said, the same every time and on every
// machine), how hard it falls and how much lies hour by hour, the rules' cost, the sources' use of the rules, the Guide's
// figures, and the texts in every language.
internal static partial class Program
{
    private static void Seasons()
    {
        SeasonTable();
        SeasonDays();
        SeasonSnow();
        SeasonCover();
        SeasonCost();
        SeasonSources();
        SeasonTexts();
    }

    // ---------------------------------------------------------------- the goods

    private static void SeasonTable()
    {
        bool bounds = true, even = true, once = true, found = true;
        string first = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int g = 0; g < SeasonRules.GoodCount; g++)
        {
            string item = SeasonRules.Goods[g];
            once &= seen.Add(item);
            found &= SeasonRules.IndexOf(item) == g && item.StartsWith("ba:itemname_", StringComparison.Ordinal);
            int sum = 0;
            for (int s = 0; s < SeasonRules.Seasons; s++)
            {
                int d = SeasonRules.DemandOf(g, s);
                sum += d;
                if (d < SeasonRules.DemandLeast || d > SeasonRules.DemandMost)
                {
                    bounds = false;
                    first = first ?? item + " in season " + s + ": " + d;
                }
            }
            int mean = sum / SeasonRules.Seasons;
            if (mean < SeasonRules.YearLeast || mean > SeasonRules.YearMost)
            {
                even = false;
                first = first ?? item + " averages " + mean + " over the year";
            }
        }
        Check(SeasonRules.GoodCount >= 40, "seasons: " + SeasonRules.GoodCount + " goods follow the season (the player's coffee, tea, alcohol, gifts, flowers, ice cream, cold drinks among them)");
        Check(bounds, "seasons: every good's figure is " + SeasonRules.DemandLeast + " to " + SeasonRules.DemandMost + " thousandths in every season" + (first != null ? " (" + first + ")" : string.Empty));
        Check(even, "seasons: every good's four seasons average " + SeasonRules.YearLeast + " to " + SeasonRules.YearMost + ", so a year of them costs or gives next to nothing" + (first != null ? " (" + first + ")" : string.Empty));
        Check(once && found, "seasons: every good is named once, as the game names items, and found again by its name");
        Check(SeasonRules.IndexOf("ba:itemname_burger") == -1 && SeasonRules.IndexOf(null) == -1 && SeasonRules.DemandOf(-1, 0) == 1000 && SeasonRules.DemandOf(0, 4) == 1000,
            "seasons: a good or a season out of the table is the game's own (1000)");
        int coffee = SeasonRules.IndexOf("ba:itemname_cupofcoffee"), cream = SeasonRules.IndexOf("ba:itemname_icecream"), gifts = SeasonRules.IndexOf("ba:itemname_expensivegift");
        int flowers = SeasonRules.IndexOf("ba:itemname_cheapflower"), whisky = SeasonRules.IndexOf("ba:itemname_whisky"), slush = SeasonRules.IndexOf("ba:itemname_slushi");
        Check(SeasonRules.DemandOf(coffee, CalendarRules.Winter) > 1000 && SeasonRules.DemandOf(gifts, CalendarRules.Winter) > 1000 && SeasonRules.DemandOf(flowers, CalendarRules.Winter) > 1000
              && SeasonRules.DemandOf(whisky, CalendarRules.Winter) > 1000 && SeasonRules.DemandOf(cream, CalendarRules.Summer) > 1000 && SeasonRules.DemandOf(slush, CalendarRules.Summer) > 1000
              && SeasonRules.DemandOf(cream, CalendarRules.Winter) < 1000,
            "seasons: winter sells more coffee, alcohol, gifts and flowers, summer more ice cream and cold drinks, winter less ice cream (the player's ask)");
        Check(SeasonNear(SeasonRules.Ratio(0f, coffee, CalendarRules.Winter), 1.15f) && SeasonNear(SeasonRules.Ratio(2f, coffee, CalendarRules.Winter), 2.3f) && SeasonNear(SeasonRules.Ratio(0.5f, coffee, CalendarRules.Spring), 0.5f),
            "seasons: a good's ratio is its own times the season's (the game reads 0 as 1)");
        int top = SeasonRules.Ranked(CalendarRules.Summer, 0, true), low = SeasonRules.Ranked(CalendarRules.Summer, 0, false);
        bool ordered = true;
        for (int s = 0; s < SeasonRules.Seasons; s++)
        {
            for (int rank = 1; rank < 6; rank++)
            {
                int a = SeasonRules.Ranked(s, rank - 1, true), b = SeasonRules.Ranked(s, rank, true);
                ordered &= a >= 0 && (b < 0 || SeasonRules.DemandOf(a, s) >= SeasonRules.DemandOf(b, s)) && a != b;
            }
        }
        Check(top == cream && low >= 0 && SeasonRules.DemandOf(low, CalendarRules.Summer) < 1000 && ordered && SeasonRules.Ranked(CalendarRules.Spring, 999, true) == -1,
            "seasons: the goods that sell most more and most less come first, each once, and nothing past the last");
        Check(SeasonRules.Change(1150) == "+15%" && SeasonRules.Change(700) == "-30%" && SeasonRules.Change(1000) == "0%" && SeasonRules.Change(1355) == "+36%",
            "seasons: a figure is written as a change in whole per cent");
    }

    private static bool SeasonNear(float a, float b)
    {
        return Math.Abs(a - b) < 0.0001f;
    }

    // ---------------------------------------------------------------- the season of a day

    private static void SeasonDays()
    {
        bool same = true, begins = true, starts = true, all = true, draw = true;
        string first = null;
        for (int year = 8; year <= 400; year++)
        {
            var had = new bool[4];
            for (int day = 1; day <= year * 2; day++)
            {
                int season = SeasonRules.SeasonOf(day, year);
                had[season] = true;
                same &= season == CalendarRules.Season(day, year);
                bool turn = day == 1 || season != SeasonRules.SeasonOf(day - 1, year);
                begins &= SeasonRules.BeginsOn(day, year) == turn;
                int start = SeasonRules.SeasonStart(day, year);
                bool ok = start <= day && SeasonRules.SeasonOf(start, year) == season && (start == 1 || SeasonRules.SeasonOf(start - 1, year) != season || day - start >= year);
                if (!ok && first == null)
                {
                    first = year + "-day year, day " + day + " starts on " + start;
                }
                starts &= ok;
            }
            all &= year < 12 || (had[0] && had[1] && had[2] && had[3]);
        }
        for (int hour = 0; hour < 24; hour++)
        {
            draw &= SeasonRules.DrawDay(40, hour) == (hour >= 23 ? 41 : 40);
        }
        Check(same, "seasons: a day's season is the calendar's (CalendarRules.Season), every year length 8 to 400");
        Check(begins && SeasonRules.BeginsOn(1, 60), "seasons: a season begins exactly where the day before was another");
        Check(starts, "seasons: the first day of a day's season is that season's and the day before it another" + (first != null ? " (" + first + ")" : string.Empty));
        Check(all, "seasons: a year of twelve days or more has all four seasons");
        Check(draw, "seasons: from the last hour of a day on, the next day's season counts, as the game draws the day's customers at midnight");
        int year60 = 60;
        Check(SeasonRules.SeasonOf(1, year60) == CalendarRules.Winter && SeasonRules.SeasonOf(60, year60) == CalendarRules.Winter && SeasonRules.SeasonOf(30, year60) == CalendarRules.Summer,
            "seasons: the year begins and ends in winter, summer in its middle, as the weather has it");
    }

    // ---------------------------------------------------------------- the snow

    private static void SeasonSnow()
    {
        int[] salts = { 1, 12345, -7, int.MaxValue - 1, 987654321 };
        int[] years = { 60, 120, 365 };
        bool winter = true, hours = true, again = true;
        int winterDays = 0, snowDays = 0, heavy = 0;
        string first = null;
        foreach (int salt in salts)
        {
            foreach (int year in years)
            {
                for (int day = 1; day <= year * 6; day++)
                {
                    SnowDay snow = SeasonRules.SnowOn(day, year, salt);
                    bool isWinter = SeasonRules.SeasonOf(day, year) == CalendarRules.Winter;
                    winterDays += isWinter ? 1 : 0;
                    if (!snow.Falls)
                    {
                        continue;
                    }
                    snowDays++;
                    heavy += snow.Heavy ? 1 : 0;
                    winter &= isWinter;
                    bool ok = snow.From >= 0 && snow.From <= SeasonRules.LatestStart && snow.Hours >= SeasonRules.LeastHours && snow.Hours <= SeasonRules.MostHours && snow.To <= 24;
                    if (!ok && first == null)
                    {
                        first = "day " + day + " of a " + year + "-day year: " + snow.From + " for " + snow.Hours + " hours";
                    }
                    hours &= ok;
                    SnowDay twice = SeasonRules.SnowOn(day, year, salt);
                    again &= twice.Falls && twice.From == snow.From && twice.Hours == snow.Hours && twice.Heavy == snow.Heavy;
                }
            }
        }
        int share = winterDays > 0 ? snowDays * 1000 / winterDays : 0;
        int heavyShare = snowDays > 0 ? heavy * 1000 / snowDays : 0;
        Check(winter, "seasons: it snows only on winter days");
        Check(hours, "seasons: a fall starts by " + SeasonRules.LatestStart + ":00, lasts " + SeasonRules.LeastHours + " to " + SeasonRules.MostHours + " hours and is over by midnight" + (first != null ? " (" + first + ")" : string.Empty));
        Check(again, "seasons: a day's snow is the same every time it is asked");
        Check(Math.Abs(share - SeasonRules.SnowChance) <= 40 && Math.Abs(heavyShare - SeasonRules.HeavyChance) <= 60,
            "seasons: about " + SeasonRules.SnowChance / 10 + "% of winter days snow (" + share / 10.0 + "% of " + winterDays + "), about " + SeasonRules.HeavyChance / 10 + "% of them heavily (" + heavyShare / 10.0 + "%)");
        bool differ = false;
        for (int day = 1; day <= 60 && !differ; day++)
        {
            differ |= SeasonRules.SnowOn(day, 60, 1).Falls != SeasonRules.SnowOn(day, 60, 2).Falls;
        }
        Check(differ, "seasons: another save's number gives another winter's snow");
        // The figure every machine of a Going Public session must arrive at, from the shared number and the day alone.
        SnowDay known = SeasonRules.SnowOn(SeasonRules.NextSnow(1, 600, 60, 12345), 60, 12345);
        Check(SeasonRules.Roll(12345, 7, SeasonRules.PurposeSnow, 1000) == SeasonRules.Roll(12345, 7, SeasonRules.PurposeSnow, 1000) && known.Falls
              && SeasonRules.NextSnow(1, 600, 60, 12345) == SeasonRules.NextSnow(1, 600, 60, 12345) && SeasonRules.NextSnow(20, 25, 60, 12345) == 0,
            "seasons: the snow comes of the save's number and the day alone, and the next snow day is found (none in summer)");
        SnowDay fall = new SnowDay { Falls = true, From = 10, Hours = 4, Heavy = false };
        SnowDay big = new SnowDay { Falls = true, From = 10, Hours = 4, Heavy = true };
        Check(SeasonRules.Falling(fall, 9.9f) == 0f && SeasonRules.Falling(fall, 14.1f) == 0f && SeasonNear(SeasonRules.Falling(fall, 12f), 0.5f) && SeasonNear(SeasonRules.Falling(big, 12f), 1f)
              && SeasonRules.Falling(big, 10.1f) > 0f && SeasonRules.Falling(big, 10.1f) < 1f && SeasonRules.Falling(new SnowDay(), 12f) == 0f,
            "seasons: snow falls only in its hours, a light fall half as hard as a heavy one, getting going and dying away");
    }

    private static void SeasonCover()
    {
        bool bounds = true, rising = true, lying = true, dry = true;
        string first = null;
        foreach (int salt in new[] { 3, 44, -9001 })
        {
            const int year = 60;
            for (int day = 2; day <= year * 4; day++)
            {
                SnowDay today = SeasonRules.SnowOn(day, year, salt), yesterday = SeasonRules.SnowOn(day - 1, year, salt);
                float last = -1f;
                for (int quarter = 0; quarter <= 96; quarter++)
                {
                    float hour = quarter / 4f, cover = SeasonRules.Cover(day, hour, year, salt);
                    bounds &= cover >= 0f && cover <= 1f;
                    if (!yesterday.Falls && cover > 0f && (!today.Falls || hour <= today.From))
                    {
                        dry = false;
                        first = first ?? "day " + day + " at " + hour + " has snow with none fallen";
                    }
                    if (!yesterday.Falls && last >= 0f && cover < last - 0.0001f)
                    {
                        rising = false;
                        first = first ?? "day " + day + " at " + hour + " has less snow than before with none melting";
                    }
                    last = cover;
                }
                if (yesterday.Falls)
                {
                    float peak = SeasonRules.Laid(yesterday, 24f);
                    float atMidnight = SeasonRules.Cover(day, 0f, year, salt), melted = SeasonRules.Cover(day, SeasonRules.MeltHours, year, salt);
                    bool ok = SeasonNear(atMidnight, Math.Min(1f, peak)) && (today.Falls && today.From < SeasonRules.MeltHours ? melted >= 0f : SeasonNear(melted, 0f));
                    if (!ok && first == null)
                    {
                        first = "day " + day + ": " + atMidnight + " at midnight after a fall laying " + peak + ", " + melted + " once melted";
                    }
                    lying &= ok;
                }
            }
        }
        Check(bounds, "seasons: the snow lying is 0 to 1 at every hour");
        Check(dry, "seasons: no snow lies before any has fallen" + (first != null ? " (" + first + ")" : string.Empty));
        Check(rising, "seasons: snow lies thicker as it falls and stays until midnight" + (first != null ? " (" + first + ")" : string.Empty));
        Check(lying, "seasons: the next morning it lies as it was left at midnight and melts away in " + SeasonRules.MeltHours + " hours" + (first != null ? " (" + first + ")" : string.Empty));
        Check(SeasonRules.Step(0f) == 0 && SeasonRules.Step(SeasonRules.PatchyFrom) == 1 && SeasonRules.Step(SeasonRules.WhiteFrom - 0.01f) == 1 && SeasonRules.Step(SeasonRules.WhiteFrom) == 2
              && SeasonRules.Step(1f) == 2 && SeasonRules.Step(SeasonRules.LightCover) == 1,
            "seasons: the roads are clear, snowy in patches (a light fall's most) or covered (a heavy fall's)");
    }

    private static void SeasonCost()
    {
        float sink = 0f;
        for (int i = 0; i < 2000; i++)
        {
            sink += SeasonRules.Cover(i % 600 + 1, (i % 96) / 4f, 60, 777);
        }
        var watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            sink += SeasonRules.Cover(i % 600 + 1, (i % 96) / 4f, 60, 777) + SeasonRules.Falling(SeasonRules.SnowOn(i % 600 + 1, 60, 777), (i % 96) / 4f);
        }
        long made = GC.GetAllocatedBytesForCurrentThread() - before;
        watch.Stop();
        double each = watch.Elapsed.TotalMilliseconds / 10000.0;
        Check(made == 0 && each < 0.02, "seasons: the snow of a moment (what the director works out once a second) allocates nothing and takes "
                                         + each.ToString("0.0000") + " ms (under 0.02; " + made + " bytes made; " + (sink >= 0f ? "ok" : "?") + ")");
    }

    // ---------------------------------------------------------------- the sources

    private static void SeasonSources()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string director = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "Seasons", "SeasonDirector.cs"));
        string game = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "Seasons", "SeasonGame.cs"));
        string weather = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "Events", "Weather.cs"));
        string calendar = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "Wiring", "Wiring.CalendarSeasons.cs"));
        string guide = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "Seasons", "SeasonGuide.cs"));
        Check(director.Contains("SeasonRules.SnowOn(") && director.Contains("SeasonRules.Cover(") && director.Contains("SeasonRules.Falling(") && !director.Contains("UnityEngine.Random")
              && !director.Contains("Random."), "seasons: the director takes the snow from the rules alone, with no chance of its own");
        Check(director.Contains("SeasonRules.DrawDay(") && game.Contains("SeasonRules.Ratio(") && game.Contains("productSalesRatio = Own[g]") && director.Contains("SeasonGame.Reset()"),
            "seasons: the goods are written for the day whose customers come next, as the rules say, and put back as the game had them");
        Check(weather.Contains("SnowNow()") && Regex.IsMatch(weather, @"case 2:[^\n]*speed \*= 0\.8") && weather.Contains("snow == Snow"),
            "seasons: the weather reads the snow on the roads, and covered roads slow traffic and take grip");
        Check(calendar.Contains("SeasonRules.BeginsOn(") && calendar.Contains("SeasonRules.SnowOn(") && calendar.Contains("SeasonRules.ForecastDays"),
            "seasons: the calendar marks each season's first day and the forecast's snow by the rules");
        var facts = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(guide, "\\.Fact\\(\"([a-z_]+)\""))
        {
            facts.Add(m.Groups[1].Value);
        }
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        var missing = new List<string>();
        if (words.Length > 0 && words[0] != null && words[0].TryGetValue("reallife_guide_seasons", out string page))
        {
            foreach (Match m in Regex.Matches(page, "\\{([a-z_]+)\\}"))
            {
                if (!facts.Contains(m.Groups[1].Value))
                {
                    missing.Add(m.Groups[1].Value);
                }
            }
        }
        else
        {
            missing.Add("(no Guide page)");
        }
        Check(missing.Count == 0 && facts.Count >= 10, "seasons: every figure the Guide page names comes from the code (" + facts.Count + " facts" + (missing.Count > 0 ? "; not: " + string.Join(", ", missing) : string.Empty) + ")");
    }

    private static void SeasonTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("seasons", words, codes))
        {
            return;
        }
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var files = new List<string>(System.IO.Directory.GetFiles(System.IO.Path.Combine(folder, "Seasons"), "*.cs"));
        files.Add(System.IO.Path.Combine(folder, "Wiring", "Wiring.SeasonLinks.cs"));
        files.Add(System.IO.Path.Combine(folder, "Wiring", "Wiring.CalendarSeasons.cs"));
        foreach (string file in files)
        {
            foreach (Match m in Regex.Matches(System.IO.File.ReadAllText(file), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string key in new[] { "reallife_opt_tip_seasons", "reallife_opt_tip_snow_look", "reallife_opt_sec_seasons", "reallife_contract_part_snow_fall", "reallife_contract_part_snow_walkers" })
        {
            keys.Add(key);
        }
        var lacking = new List<string>();
        bool plain = true, short_ = true;
        foreach (string key in keys)
        {
            string missing = LanguagesLacking(words, codes, key);
            if (missing.Length > 0)
            {
                lacking.Add(key + " (" + missing + ")");
            }
            if (words[0].TryGetValue(key, out string text))
            {
                plain &= !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ") && !text.Contains("!");
                short_ &= key == "reallife_guide_seasons" || text.Length <= 240;
            }
        }
        Check(keys.Count >= 30 && lacking.Count == 0, "seasons: each of its " + keys.Count + " texts is in every language with the English's names in curly brackets ("
                                                    + (lacking.Count == 0 ? "all" : string.Join(", ", lacking)) + ")");
        Check(plain && short_, "seasons: its English texts have no dashes or exclamation marks, and a notification is 240 characters at most");
        bool next = true;
        for (int l = 0; l < codes.Length; l++)
        {
            string row = NewsRow(words[l], Lead(codes[l]));
            next &= row != null && FixMarks.TryGetValue(codes[l], out string mark)
                    && (row.IndexOf(mark, StringComparison.Ordinal) < 0 || row.IndexOf(Lead(codes[l]), StringComparison.Ordinal) < row.IndexOf(mark, StringComparison.Ordinal))
                    && row.IndexOf(Lead(codes[l]), StringComparison.Ordinal) >= 0;
        }
        Check(next, "seasons: What's new's next row says it in every language, before the fixes");
    }

    // The first word of the seasons' line in What's new, in each language.
    private static string Lead(string code)
    {
        switch (code)
        {
            case "pt": return "Estações: ";
            case "zh-cn": return "季节：";
            case "de": return "Jahreszeiten: ";
            case "fr": return "Saisons : ";
            case "es": return "Estaciones: ";
            case "ru": return "Времена года: ";
            case "nl": return "Seizoenen: ";
            case "tr": return "Mevsimler: ";
            default: return "Seasons: ";
        }
    }
}
