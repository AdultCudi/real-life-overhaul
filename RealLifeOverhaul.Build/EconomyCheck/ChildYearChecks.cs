using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ChildYearRules = RealLifeOverhaul.Family.ChildYearRules;

// A child's year (Scripts/Family/ChildYearRules.cs): the weeks per year of a child's age stopped at 12 though the game allows a year of
// 365 days, which made a child's year too short to match a long game year (a year of 52 weeks is 364 days and divides by 4, as another
// mod's four seasons want). It goes to 52 now, and everything that reads it is held to it at every length from 1 to 52 weeks: the days
// in a year, ageing by the day (a year of N weeks is exactly 7 x N days; sums of a day's share in single precision came a day late at
// every length), the day of the next birthday on the Family page and in the Calendar, how long a pregnancy and an adoption take and how
// far apart their texts fall, the Guide's figure, the docs and What's new.
internal static partial class Program
{
    private static void ChildYear()
    {
        ChildYearEnds();
        ChildYearAgeing();
        ChildYearBirthdays();
        ChildYearSpans();
        ChildYearReaders();
        ChildYearTexts();
    }

    // The option's ends: 1 to 52 weeks of seven days, never past the game's longest year, and every game year that divides by 4 and by 7 can be matched.
    private static void ChildYearEnds()
    {
        bool clamp = ChildYearRules.Weeks(int.MinValue) == 1 && ChildYearRules.Weeks(-3) == 1 && ChildYearRules.Weeks(0) == 1 && ChildYearRules.Weeks(1) == 1
                     && ChildYearRules.Weeks(12) == 12 && ChildYearRules.Weeks(13) == 13 && ChildYearRules.Weeks(52) == 52 && ChildYearRules.Weeks(53) == 52
                     && ChildYearRules.Weeks(400) == 52 && ChildYearRules.Weeks(int.MaxValue) == 52;
        Check(clamp, "child's year: the weeks per year read 1 to 52 and anything outside as the nearest end, not the 12 they stopped at");
        bool days = true, whole = true;
        for (int weeks = 1; weeks <= 52; weeks++)
        {
            int year = ChildYearRules.Days(weeks);
            days &= year == weeks * 7;
            whole &= year % 7 == 0 && year <= ChildYearRules.GameYearMost;
        }
        Check(days && whole && ChildYearRules.Days(0) == 7 && ChildYearRules.Days(99) == 364 && ChildYearRules.Days(12) == 84 && ChildYearRules.Days(4) == 28,
            "child's year: a year of N weeks is 7 x N days at every N from 1 to 52, a whole number of weeks, and never past the game's longest year (" + ChildYearRules.GameYearMost + " days)");
        Check(ChildYearRules.WeeksMost * ChildYearRules.WeekDays <= ChildYearRules.GameYearMost && (ChildYearRules.WeeksMost + 1) * ChildYearRules.WeekDays > ChildYearRules.GameYearMost
              && ChildYearRules.WeeksMost == 52 && ChildYearRules.WeeksLeast == 1,
            "child's year: 52 weeks, 364 days, is the most whole weeks inside the game's longest year of " + ChildYearRules.GameYearMost + " days");
        bool match = true;
        int matched = 0;
        for (int game = 60; game <= ChildYearRules.GameYearMost; game++)
        {
            if (game % 4 == 0 && game % ChildYearRules.WeekDays == 0)
            {
                match &= ChildYearRules.Days(game / ChildYearRules.WeekDays) == game;
                matched++;
            }
        }
        Check(match && matched == 11 && ChildYearRules.Days(52) / 4 == 91,
            "child's year: every game year of 60 to 365 days that divides by 4 (another mod's four seasons) and by 7 can be matched exactly, " + matched + " of them, the longest 364 days as four seasons of 91");
    }

    // Ageing by the day: exactly N days older after N days at every length, a birthday on the day a whole number of years of days has passed, whatever the size of the
    // steps between, a year in one go the same as its days one by one. The plain sum Children.Grow made before is the control: it is a day late somewhere at every length.
    private static void ChildYearAgeing()
    {
        bool exact = true, rises = true, ticks = true, year = true, plain = false, steady = true;
        string bad = null;
        int plainLate = 0;
        for (int weeks = ChildYearRules.WeeksLeast; weeks <= ChildYearRules.WeeksMost; weeks++)
        {
            int length = ChildYearRules.Days(weeks);
            float one = 1 / (float)length;   // Children.Daily's elapsed / (float)DaysPerYear, for one day
            float age = 0f, sum = 0f, before;
            int whole = 0;
            bool late = false;
            for (int day = 1; day <= length * 18; day++)
            {
                before = age;
                age = ChildYearRules.Aged(age, one, length);
                sum += one;
                bool here = ChildYearRules.DayOfLife(age, length) == day && (int)Math.Floor(age) == day / length && age >= before;
                exact &= here;
                late |= (int)Math.Floor(sum) < day / length;
                if ((int)Math.Floor(age) > whole)
                {
                    whole = (int)Math.Floor(age);
                    rises &= day == whole * length;
                }
                if (!here && bad == null)
                {
                    bad = weeks + " weeks a year, day " + day + ": age " + age;
                }
            }
            rises &= whole == 18;
            plainLate += late ? 1 : 0;
            foreach (int tick in new[] { 2, 3, 7, 14 })
            {
                float a = 0f, b = 0f;
                for (int day = tick; day <= length * 6; day += tick)
                {
                    a = ChildYearRules.Aged(a, tick / (float)length, length);
                    for (int k = 0; k < tick; k++)
                    {
                        b = ChildYearRules.Aged(b, one, length);
                    }
                    ticks &= a == b;
                }
            }
            foreach (float start in new[] { 0f, 0.3f, 6f, 7.25f, 11.99f })
            {
                float grown = ChildYearRules.Aged(start, 1f, length);
                year &= ChildYearRules.DayOfLife(grown, length) == ChildYearRules.DayOfLife(start, length) + length;
                float held = ChildYearRules.Aged(start, 0f, length);
                steady &= Math.Abs(held - start) <= 0.5f / length + 0.00001f && ChildYearRules.Aged(held, 0f, length) == held;
            }
        }
        plain = plainLate > 0;
        Check(exact && rises, "child's year: at every length from 1 to 52 weeks a child is exactly N days older after N days and turns a year older on each multiple of the year's days, to eighteen (" + (bad ?? "every day") + ")");
        Check(ticks, "child's year: a day's tick, two, three, a week's or a fortnight's (the most the family's round hands over) age a child to the same day at every length");
        Check(year && steady, "child's year: a year in one go (the test button) is exactly the year's days, and an age between two days (an adopted child's) is taken to the nearest day and stays there");
        Check(plain, "child's year: the control sees what this guards against: the plain single-precision sum of a day's share, as ageing added it before, is a day late at " + plainLate + " of 52 lengths");
        Check(ChildYearRules.Aged(2.9999998f, 0f, 28) == 3f && ChildYearRules.Aged(1f, 1f, 0) == 2f && ChildYearRules.Aged(-1f, 0f, 28) == 0f && ChildYearRules.DayOfLife(0.3f, 28) == 8,
            "child's year: an age one old save kept a hair under a whole year reads as that year, and a year of no days or an age under nought cannot divide by zero or go back");
    }

    // The day of the next birthday, from 1 to a year, the same figure for the Family page and the Calendar, and the age it brings.
    private static void ChildYearBirthdays()
    {
        bool next = true, turning = true, bounds = true, onDay = true;
        string bad = null;
        for (int weeks = ChildYearRules.WeeksLeast; weeks <= ChildYearRules.WeeksMost; weeks++)
        {
            int length = ChildYearRules.Days(weeks);
            float one = 1 / (float)length;
            float age = 0f;
            for (int day = 0; day < length * 18; day++)
            {
                int expected = (day / length + 1) * length - day;   // from the child's day of life alone: the next whole multiple of the year's days
                int told = ChildYearRules.BirthdayIn(age, length);
                int turns = ChildYearRules.Turning(age, length);
                next &= told == expected;
                turning &= turns == day / length + 1;
                bounds &= told >= 1 && told <= length;
                onDay &= day % length != 0 || told == length;
                if (told != expected && bad == null)
                {
                    bad = weeks + " weeks a year, day " + day + ": " + told + " against " + expected;
                }
                age = ChildYearRules.Aged(age, one, length);
            }
        }
        Check(next && bounds && onDay, "child's year: the days to a child's next birthday are from 1 to a year, a full year on the birthday itself, and the day their whole years next rise, at every length (" + (bad ?? "every day of eighteen years") + ")");
        Check(turning, "child's year: the age the next birthday brings is the whole years lived plus one, at every length");
        Check(ChildYearRules.BirthdayIn(0.3f, 28) == 20 && ChildYearRules.BirthdayIn(2f, 28) == 28 && ChildYearRules.Turning(2f, 28) == 3 && ChildYearRules.BirthdayIn(0f, 0) == 1 && ChildYearRules.Turning(0f, 0) == 1,
            "child's year: an adopted child's first birthday is counted from their age to the nearest day, and a year of no days counts as one");
    }

    // How long a pregnancy and an adoption take, and how far apart the texts that fill them fall (Children.Timeline): the same handful at any length of year, not one every
    // four days through a pregnancy of 273.
    private static void ChildYearSpans()
    {
        bool least = true, inside = true, same = true, growing = true, paced = true;
        int fewest = 99, most = 0, adoptFewest = 99, adoptMost = 0;
        int lastPregnancy = 0, lastAdoption = 0;
        for (int weeks = ChildYearRules.WeeksLeast; weeks <= ChildYearRules.WeeksMost; weeks++)
        {
            int length = ChildYearRules.Days(weeks);
            int pregnancy = ChildYearRules.Span(0.75f, length), adoption = ChildYearRules.Span(0.6f, length);
            least &= pregnancy >= ChildYearRules.SpanLeast && adoption >= ChildYearRules.SpanLeast;
            inside &= pregnancy <= length && adoption <= length;
            int gapPregnancy = ChildYearRules.TextGap(ChildYearRules.PregnancyTextDays, length), gapAdoption = ChildYearRules.TextGap(ChildYearRules.AdoptionTextDays, length);
            growing &= gapPregnancy >= lastPregnancy && gapAdoption >= lastAdoption;
            lastPregnancy = gapPregnancy;
            lastAdoption = gapAdoption;
            paced &= length > ChildYearRules.PacedWeeks * ChildYearRules.WeekDays
                ? gapPregnancy >= ChildYearRules.PregnancyTextDays && gapAdoption >= ChildYearRules.AdoptionTextDays
                : gapPregnancy == ChildYearRules.PregnancyTextDays && gapAdoption == ChildYearRules.AdoptionTextDays;
            int texts = 0, waiting = 0, last = 0;
            for (int day = 1; day <= pregnancy; day++)
            {
                if (day - last >= gapPregnancy && day < pregnancy)
                {
                    last = day;
                    texts++;
                }
            }
            last = 0;
            int step = 1, visit = Math.Max(2, adoption / 3);
            for (int day = 1; day <= adoption; day++)
            {
                step = day >= visit && step == 1 ? 2 : step;
                step = step == 2 && day >= adoption - 1 ? 3 : step;
                if (step < 3 && day - last >= gapAdoption)
                {
                    last = day;
                    waiting++;
                }
            }
            if (length >= ChildYearRules.PacedWeeks * ChildYearRules.WeekDays)
            {
                fewest = Math.Min(fewest, texts);
                most = Math.Max(most, texts);
                adoptFewest = Math.Min(adoptFewest, waiting);
                adoptMost = Math.Max(adoptMost, waiting);
            }
            same &= length > ChildYearRules.PacedWeeks * ChildYearRules.WeekDays || texts == CountEvery(pregnancy, ChildYearRules.PregnancyTextDays);
        }
        Check(least && inside && ChildYearRules.Span(0.75f, 28) == 21 && ChildYearRules.Span(0.75f, 7) == 6 && ChildYearRules.Span(0.75f, 364) == 273
              && ChildYearRules.Span(0.6f, 28) == 17 && ChildYearRules.Span(0.6f, 364) == 218 && ChildYearRules.Span(0.1f, 7) == 6,
            "child's year: a pregnancy takes three quarters of the child's year and an adoption three fifths, six days at the least and inside one year, at every length (21 and 17 days at 4 weeks, 273 and 218 at 52)");
        Check(growing && paced && ChildYearRules.TextGap(4, 28) == 4 && ChildYearRules.TextGap(4, 364) == 52 && ChildYearRules.TextGap(5, 364) == 65 && ChildYearRules.TextGap(4, 7) == 4 && ChildYearRules.TextGap(4, 0) == 4,
            "child's year: the days between the texts of a pregnancy and an adoption are the paced ones up to 4 weeks and grow with the year after, never shorter (52 and 65 days at 52 weeks)");
        Check(fewest >= 4 && most <= 6 && adoptFewest >= 2 && adoptMost <= 4 && same,
            "child's year: from 4 to 52 weeks a pregnancy has " + fewest + " to " + most + " texts and an adoption's wait " + adoptFewest + " to " + adoptMost + " (one every 4 days would have made 68 in a pregnancy of 273), and a short year is paced as before");
    }

    // The texts of a pregnancy at the old fixed gap.
    private static int CountEvery(int length, int gap)
    {
        int count = 0;
        for (int day = gap; day < length; day += gap)
        {
            count++;
        }
        return count;
    }

    // Everything that reads the year goes through the one rule: the slider, the days, the ageing, the pregnancy, the adoption, the texts, the page, the Calendar, the Guide.
    private static void ChildYearReaders()
    {
        string rules = ModSource(@"Family\ChildYearRules.cs"), children = ModSource(@"Family\Children.cs"), growing = ModSource(@"Family\Children.Growing.cs");
        string arrival = ModSource(@"Family\Children.Arrival.cs"), page = ModSource(@"Family\FamilyPage.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string list = ModSource(@"Meta\FeatureList.Life.cs"), settings = ModSource(@"Core\Settings.cs"), month = ModSource(@"Calendar\MonthViewRules.cs");
        Check(rules.Contains("public const int WeeksLeast = 1;") && rules.Contains("public const int WeeksMost = 52;") && rules.Contains("public const int PacedWeeks = 4;")
              && mod.Contains("AddSlider(Settings.FamilyYearWeeksId, \"reallife_family_year_weeks\", Family.ChildYearRules.WeeksLeast, Family.ChildYearRules.WeeksMost, Settings.FamilyYearWeeks,")
              && settings.Contains("FamilyYearWeeks = 4;") && ChildYearRules.PacedWeeks == 4 && ChildYearRules.Weeks(4) == 4,
            "child's year: the options page's slider takes its ends from the rule, 1 and 52, and the shipped value is the paced year of 4 weeks");
        Check(children.Contains("public static int DaysPerYear => ChildYearRules.Days(Settings.FamilyYearWeeks);") && !children.Contains("Mathf.Clamp(Settings.FamilyYearWeeks")
              && !Regex.IsMatch(children, @"FamilyYearWeeks,\s*1,\s*12"),
            "child's year: the days in a child's year come of the rule, with no clamp of 12 weeks left beside it");
        var readers = new List<string>();
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        bool aged = true;
        if (System.IO.Directory.Exists(scripts))
        {
            foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string code = System.IO.File.ReadAllText(file);
                if (Regex.IsMatch(code, @"\bFamilyYearWeeks\b"))
                {
                    readers.Add(file.Substring(scripts.Length + 1).Replace('/', '\\'));
                }
                if (file.Contains(@"\Family\") && Regex.IsMatch(code, @"\bAge\s*\+="))
                {
                    aged = false;
                }
            }
        }
        readers.Sort(StringComparer.Ordinal);
        Check(string.Join(", ", readers) == @"Core\RealLifeOverhaulMod.cs, Core\Settings.cs, Family\Children.cs, Meta\FeatureList.Life.cs",
            "child's year: the setting is read in four places only, the page's slider, the saved value, the rule's days and the Guide's figure (" + string.Join(", ", readers) + ")");
        Check(growing.Contains("c.Age = ChildYearRules.Aged(c.Age, years, yearDays);") && growing.Contains("int yearDays = DaysPerYear;") && aged,
            "child's year: children are aged only by the rule, in whole days of the year, and no other file adds to a child's age");
        Check(children.Contains("ChildYearRules.Span(FamilyBalance.PregnancyYears, DaysPerYear)") && arrival.Contains("ChildYearRules.Span(FamilyBalance.AdoptionYears, DaysPerYear)")
              && !Regex.IsMatch(children + arrival, @"RoundToInt\(\s*FamilyBalance\.(Pregnancy|Adoption)Years"),
            "child's year: a pregnancy's and an adoption's length come of the rule");
        Check(children.Contains("day - s.LastFlavourDay >= ChildYearRules.TextGap(ChildYearRules.PregnancyTextDays, DaysPerYear) && day < s.PDueDay")
              && children.Contains("day - s.LastFlavourDay >= ChildYearRules.TextGap(ChildYearRules.AdoptionTextDays, DaysPerYear)")
              && !children.Contains("LastFlavourDay >= 4") && !children.Contains("LastFlavourDay >= 5"),
            "child's year: the texts of a pregnancy and an adoption's wait are spaced by the rule, not a fixed four and five days");
        Check(page.Contains("int yearDays = Children.DaysPerYear;") && page.Contains("ChildYearRules.BirthdayIn(c.Age, yearDays)") && page.Contains("ChildYearRules.Turning(c.Age, yearDays)")
              && !page.Contains("Mathf.CeilToInt((Mathf.Floor(c.Age)") && !month.Contains("ChildBirthdayIn"),
            "child's year: the Family page names a birthday by the same rule as the Calendar and the ageing, and the Calendar's own single-precision count is gone");
        Check(list.Contains(".Fact(\"family_year_weeks\", () => Family.ChildYearRules.Weeks(Settings.FamilyYearWeeks).ToString(CultureInfo.InvariantCulture))"),
            "child's year: the Guide says the weeks the rule uses, not the raw setting");
        string docs = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\family.md"));
        string doc = System.IO.File.Exists(docs) ? System.IO.File.ReadAllText(docs) : string.Empty;
        Check(doc.Contains("1 to 52 weeks") && !doc.Contains("1 to 12 weeks") && doc.Contains("ChildYearRules") && doc.Contains("364 days"),
            "child's year: the Family page of docs/features says 1 to 52 weeks, the rule's part in it and what a year of 52 weeks is");
    }

    // The words: the option's sentence names the days, and What's new says the longer year and the fixed birthdays.
    private static void ChildYearTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        string tip = en != null && en.TryGetValue("reallife_opt_tip_family_year_weeks", out string said) ? said : string.Empty;
        Check(tip.Contains("52") && tip.Contains("364") && tip.Contains("7 days") && tip.EndsWith("."),
            "child's year: the option's sentence says the most, 52 weeks, and that a week is 7 days (" + tip + ")");
        string row = null;
        if (en != null)
        {
            foreach (KeyValuePair<string, string> pair in en)
            {
                if (pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !pair.Key.EndsWith("_title", StringComparison.Ordinal)
                    && pair.Value.Contains("A child's year in Family life can now last up to 52 weeks"))
                {
                    row = pair.Key;
                    break;
                }
            }
        }
        string text = row != null ? en[row] : string.Empty;
        Check(text.Contains("up to 52 weeks, a year of 364 days, instead of 12 weeks.") && text.Contains("Fixed: a child's birthday could come a day late, the Family page and the Calendar could name different days for it, and the Calendar went on listing birthdays after the child had grown up."),
            "child's year: What's new says the longer year and the birthday fixes (in the row " + (row ?? "none") + ")");
    }
}
