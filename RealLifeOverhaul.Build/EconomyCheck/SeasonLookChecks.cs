using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Seasons;

// How the seasons look (Scripts/Seasons/SeasonLookRules and the sources that use it): the day of a season and its length,
// the leaves' colour through every year length (the game's own all summer, never brighter than the game's, autumn warm
// and darker in green, a few writes a day at most), the season's own rain (never in winter, in its hours, over by
// midnight, about as often as said, the same every time), the lightning's gaps, the falling leaves, the sky's overcast
// and warmth, the cost, and the sources: each part behind its own option, standing down for Alcware Seasons, the game's
// own rain and sky used and put back, no chance of the mod's own, and nothing of a person's but clothes ever changed.
internal static partial class Program
{
    private static void SeasonLooks()
    {
        SeasonLookDays();
        SeasonLookFoliage();
        SeasonLookRain();
        SeasonLookRest();
        SeasonLookCost();
        SeasonLookSources();
        SeasonBarIndependence();   // the season under the date and Alcware Seasons (LoadPathChecks.cs)
    }

    private static void SeasonLookDays()
    {
        bool lengths = true, counted = true, shown = true;
        foreach (int year in new[] { 8, 12, 28, 60, 120, 365, 400 })
        {
            int total = 0;
            for (int day = 1; day <= year; day++)
            {
                int s = SeasonRules.SeasonOf(day, year);
                int at = SeasonLookRules.SeasonDay(day, year), length = SeasonLookRules.SeasonLength(day, year);
                counted &= at >= 1 && at <= length && (day == 1 || SeasonRules.SeasonOf(day - 1, year) != s ? at == 1 || day == 1 : at == SeasonLookRules.SeasonDay(day - 1, year) + 1);
                lengths &= length >= 1 && length <= year;
                total += day == 1 || SeasonRules.SeasonOf(day - 1, year) != s ? 1 : 0;
            }
            for (int s = 0; s < 4; s++)
            {
                int d = SeasonLookRules.ShowDay(s, year, 0.5f);
                shown &= d >= 1 && d <= year && SeasonRules.SeasonOf(d, year) == s;
            }
            lengths &= total >= 4 && total <= 5;   // winter is split by the year's turn
        }
        Check(counted, "season looks: the day of the season counts from 1 on each season's first day and up by one a day, for years of 8 to 400 days");
        Check(lengths, "season looks: a season's length is between one day and the year, and a year has its four seasons (winter split by the year's turn)");
        Check(shown, "season looks: a test's day for each season is in that season, for every year length");
    }

    private static void SeasonLookFoliage()
    {
        bool summerPlain = true, never = true, autumn = true, steps = true, spring = true;
        int changes = 0;
        foreach (int year in new[] { 8, 28, 60, 120, 365 })
        {
            Tint last = SeasonLookRules.Foliage(1, 0f, year);
            for (int day = 1; day <= year; day++)
            {
                for (int q = 0; q < 24; q++)
                {
                    Tint t = SeasonLookRules.Foliage(day, q, year);
                    never &= t.R <= 1f && t.G <= 1f && t.B <= 1f && t.R >= 0.5f && t.G >= 0.4f && t.B >= 0.2f;
                    steps &= Math.Abs(t.R * SeasonLookRules.Steps - Math.Round(t.R * SeasonLookRules.Steps)) < 0.001;
                    if (year == 60 && !t.Same(last))
                    {
                        changes++;
                    }
                    last = t;
                }
                int season = SeasonRules.SeasonOf(day, year);
                Tint noon = SeasonLookRules.Foliage(day, 12f, year);
                float part = SeasonLookRules.YearPart(day, 12f, year);
                if (season == CalendarRules.Summer)
                {
                    summerPlain &= SeasonLookRules.Foliage(day, 0f, year).Plain;   // its last afternoon may begin to turn
                }
                if (season == CalendarRules.Autumn && part > 0.78f && part < 0.86f)
                {
                    autumn &= noon.G < 0.8f && noon.R > noon.G && noon.G > noon.B;
                }
                if (season == CalendarRules.Spring && part > 0.27f && part < 0.33f)
                {
                    spring &= noon.G >= noon.R && noon.G >= noon.B;
                }
            }
        }
        Check(summerPlain, "season looks: the trees keep the game's own colour all summer, so nothing is written on them then");
        Check(never, "season looks: the leaves are only ever darkened from the game's own colour, never brightened past it or near black");
        Check(autumn, "season looks: mid autumn's leaves are warm (red over green over blue, green well down)");
        Check(spring, "season looks: spring's leaves are green");
        Check(steps && changes > 0 && changes <= 120, "season looks: the leaves' colour moves in twentieths, " + changes + " changes in a 60-day year (at most 120: a few a game day)");
    }

    private static void SeasonLookRain()
    {
        bool winterDry = true, hours = true, same = true, storms = true;
        int spring = 0, springRain = 0, autumn = 0, autumnRain = 0, summer = 0, summerStorm = 0;
        foreach (int salt in new[] { 1, 777, -40213, 99991, 123456789 })
        {
            foreach (int year in new[] { 60, 120, 365 })
            {
                for (int day = 1; day <= year * 2; day++)
                {
                    RainDay rain = SeasonLookRules.RainOn(day, year, salt);
                    RainDay again = SeasonLookRules.RainOn(day, year, salt);
                    same &= rain.Falls == again.Falls && rain.From == again.From && rain.Hours == again.Hours && rain.Storm == again.Storm;
                    int season = SeasonRules.SeasonOf(day, year);
                    if (season == CalendarRules.Winter)
                    {
                        winterDry &= !rain.Falls;
                        continue;
                    }
                    if (rain.Falls)
                    {
                        hours &= rain.From >= SeasonLookRules.RainFirstHour && rain.From <= SeasonLookRules.RainLastHour && rain.Hours >= 1 && rain.To <= 24;
                        storms &= rain.Storm == (season == CalendarRules.Summer);
                    }
                    spring += season == CalendarRules.Spring ? 1 : 0;
                    springRain += season == CalendarRules.Spring && rain.Falls ? 1 : 0;
                    autumn += season == CalendarRules.Autumn ? 1 : 0;
                    autumnRain += season == CalendarRules.Autumn && rain.Falls ? 1 : 0;
                    summer += season == CalendarRules.Summer ? 1 : 0;
                    summerStorm += season == CalendarRules.Summer && rain.Falls ? 1 : 0;
                }
            }
        }
        Check(winterDry, "season looks: no rain of the seasons' in winter, which snows");
        Check(hours && storms, "season looks: the seasons' rain starts in its hours, is over by midnight, and only summer's is a storm");
        Check(same, "season looks: a day's rain is the same every time it is asked for (the same on every machine of a session)");
        Check(SeasonNear(springRain, spring, SeasonLookRules.ShowerChance) && SeasonNear(autumnRain, autumn, SeasonLookRules.AutumnRainChance) && SeasonNear(summerStorm, summer, SeasonLookRules.StormChance),
            "season looks: showers on " + SeasonPer(springRain, spring) + " of spring days, rain on " + SeasonPer(autumnRain, autumn) + " of autumn's, storms on " + SeasonPer(summerStorm, summer)
            + " of summer's, near the rules' " + SeasonLookRules.ShowerChance + ", " + SeasonLookRules.AutumnRainChance + " and " + SeasonLookRules.StormChance + " thousandths");
        bool gaps = true;
        for (int i = 0; i < 500; i++)
        {
            int g = SeasonLookRules.FlashGap(30 + i % 7, 777, i);
            gaps &= g >= SeasonLookRules.FlashLeast && g <= SeasonLookRules.FlashMost && g == SeasonLookRules.FlashGap(30 + i % 7, 777, i);
        }
        Check(gaps, "season looks: lightning comes " + SeasonLookRules.FlashLeast + " to " + SeasonLookRules.FlashMost + " seconds apart, the same gaps every time");
    }

    private static bool SeasonNear(int hits, int days, int thousandths)
    {
        return days > 0 && Math.Abs(hits * 1000.0 / days - thousandths) < 60;
    }

    private static string SeasonPer(int hits, int days)
    {
        return days > 0 ? (hits * 1000 / days) + "/1000" : "-";
    }

    private static void SeasonLookRest()
    {
        bool leaves = true, autumnLeaves = false;
        for (int day = 1; day <= 120; day++)
        {
            float l = SeasonLookRules.Leaves(day, 12f, 120);
            bool inAutumn = SeasonRules.SeasonOf(day, 120) == CalendarRules.Autumn;
            leaves &= inAutumn ? l > 0f && l <= 1f : l == 0f;
            autumnLeaves |= inAutumn && l >= 0.9f;
        }
        Check(leaves && autumnLeaves, "season looks: leaves fall only in autumn, most in its middle");
        Check(SeasonLookRules.Overcast(0f, 0f) == 0f && SeasonLookRules.Overcast(1f, 0f) == 1f && SeasonLookRules.Overcast(0f, 1f) > 0f && SeasonLookRules.Overcast(0f, 1f) < 0.5f,
            "season looks: the sky is the game's own without snow, overcast while it snows hard, a little grey while it lies");
        Check(SeasonLookRules.Warmth(CalendarRules.Summer, false) == 1f && SeasonLookRules.Warmth(CalendarRules.Summer, true) == 0f && SeasonLookRules.Warmth(CalendarRules.Winter, false) == 0f
              && SeasonLookRules.Warmth(CalendarRules.Spring, false) == 0f, "season looks: the sun is warmer only in summer, and not in a storm");
    }

    private static void SeasonLookCost()
    {
        float sink = 0f;
        for (int i = 0; i < 2000; i++)
        {
            sink += SeasonLookRules.Foliage(i % 600 + 1, 6f, 60).G + SeasonLookRules.RainOn(i % 600 + 1, 60, 777).Hours + SeasonLookRules.Leaves(i % 600 + 1, 12f, 60);
        }
        var watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            Tint t = SeasonLookRules.Foliage(i % 600 + 1, (i % 96) / 4f, 60);
            RainDay r = SeasonLookRules.RainOn(i % 600 + 1, 60, 777);
            sink += t.R + r.Hours + SeasonLookRules.Leaves(i % 600 + 1, 12f, 60) + SeasonLookRules.Overcast(0.5f, 0.5f);
        }
        watch.Stop();
        long made = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = watch.Elapsed.TotalMilliseconds / 10000.0;
        Check(made == 0 && each < 0.05, "season looks: a second's look (leaves, rain, falling leaves, sky) allocates nothing and takes " + each.ToString("0.0000") + " ms (under 0.05; "
                                        + made + " bytes; " + (sink >= 0f ? "ok" : "?") + ")");
    }

    private static void SeasonLookSources()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string look = SeasonRead(folder, "Seasons", "SeasonDirector.Look.cs");
        string director = SeasonRead(folder, "Seasons", "SeasonDirector.cs");
        string sky = SeasonRead(folder, "Seasons", "SeasonGame.Sky.cs");
        string trees = SeasonRead(folder, "Seasons", "SeasonGame.Trees.cs");
        string coats = SeasonRead(folder, "Seasons", "SeasonGame.Coats.cs");
        string bar = SeasonRead(folder, "Seasons", "SeasonBar.cs");
        string others = SeasonRead(folder, "Core", "OtherMods.cs");
        string guide = SeasonRead(folder, "Seasons", "SeasonGuide.cs");
        string settings = SeasonRead(folder, "Core", "Settings.cs");
        Check(look.Contains("Settings.SeasonTrees") && look.Contains("Settings.SeasonRain") && look.Contains("Settings.SeasonSky") && look.Contains("Settings.SeasonLeaves")
              && look.Contains("Settings.SeasonBar") && look.Contains("Settings.SeasonCoats"), "season looks: every part follows its own option");
        Check(settings.Contains("public static bool SeasonCoats = false;") && settings.Contains("public static bool SeasonBesideAlcware = false;"),
            "season looks: dressing people warmer is off by default, and these seasons stand down for Alcware Seasons by default");
        Check(others.Contains("new OtherMod(\"AlcwareSeasons\", \"AlcwareSeasons.SeasonsMod\"") && director.Contains("|| StandDown()") && look.Contains("OtherMods.AlcwareSeasons.Loaded")
              && look.Contains("reallife_season_alcware"), "season looks: Alcware Seasons is known by its assembly and type, and the seasons stand down for it with one notice");
        Check(look.Contains("SeasonLookRules.RainOn(") && look.Contains("SeasonLookRules.Foliage(") && look.Contains("SeasonLookRules.FlashGap(") && !look.Contains("Random."),
            "season looks: the rain, the leaves and the lightning come of the rules alone, with no chance of the mod's own");
        Check(look.Contains("!GoingPublic.IsGuest") && sky.Contains("RainHelper.Command_ToggleRain(") && sky.Contains("GameContract.RainEnd") && sky.Contains("GameContract.RainFade"),
            "season looks: the season's rain is the game's own, started with its command and ended by its own fade, never on a Going Public guest (the host's rain is brought)");
        Check(sky.Contains("Object.Instantiate(now)") && sky.Contains("fair.SetValue(time, _own)") && sky.Contains("Blend(_made, _own, rain"),
            "season looks: the sky is a copy of the game's fair-weather settings blended from the game's own each time, and the game's own put back");
        Check(trees.Contains("TreeController.AllTrees") && trees.Contains("LeafShader") && trees.Contains("SetColor(MainColorId, LeavesOwn[i])") && !trees.Contains("SceneFinds."),
            "season looks: the trees are the game's own list (no search), only its wind shader's leaf materials are tinted, and each is put back");
        Check(coats.Contains("UpdateElements(Change)") && coats.Contains("AppearanceElementType.Torso") && coats.Contains("AppearanceElementType.Legs")
              && !coats.Contains("AppearanceElementType.Head") && !coats.Contains("AppearanceElementType.Hair") && !coats.Contains("RandomizeClothes") && !coats.Contains("SetBody")
              && coats.Contains("ScenePrewarm.Again(DoorWhat, _atDoor ??= AtDoor)") && coats.Contains("DressSome(DoorMost)") && coats.Contains("PutBack(DoorMost)"),
            "season looks: dressing for the cold changes only tops and trousers, among the game's own, behind the black screen at a door, never a body, face or hair");
        Check(bar.Contains("\"Date/Label\"") && bar.Contains("UiClone.StripGameScripts") && bar.Contains("ignoreLayout = true"),
            "season looks: the season under the date is a copy of the game's own date label, outside the date's layout");
        Check(guide.Contains("reallife_season_test_spring") && guide.Contains("reallife_season_test_winter") && guide.Contains("Settings.SeasonBesideAlcwareId"),
            "season looks: each season's look has a test button, and every option belongs to the seasons");
    }

    private static string SeasonRead(string folder, string a, string b)
    {
        return System.IO.File.ReadAllText(System.IO.Path.Combine(folder, a, b));
    }
}
