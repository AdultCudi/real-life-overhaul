using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Flare;

// Three gyms run as a brand, store rating over 80, and brand recognition that did not seem to increase; and an influencer
// hired for a campaign who posted nothing. Reproduced with the mod's own rules:
//   recognition  three gyms in one district for twenty days, by BrandRules.Day: it does grow, about half a point a day,
//                but the Brands app showed whole points (two mornings at 0) and a city-wide figure that stays near 0 for
//                a brand in one district; the app now shows tenths, says on how many mornings it has moved
//                (BrandRules.Missed) and its "tomorrow" is the step's own sum (BrandRules.GainOn)
//   influencers  a hired influencer who stops posting (the cast's month) made none of the posts paid for
//                (FlareCast.Day skipped every inactive one); the Business tab counted only the player's own posts, so a
//                deal's posts never showed there. Now the paid posts are made to the deal's end and counted on the deal
//                (FlareHire.Posted, LastPost, saved), and the tab and the deal's page say so (FlareCast.PostsAbout, HireStage)
// A save at day 40: one brand of three shops, one each in Garment District, Midtown and Murray Hill, and recognition
// that had not gone up. Each district gained from its one shop alone and the city-wide figure headed
// for the districts' sum divided by eight at 5% a day: under 2 points city-wide by day 40. Now each district also gains
// from the brand's shops in other districts (BrandRules.Pull, diminishing) and the city-wide figure heads for their mean
// from three districts on, at 10% a day (BrandRules.CityTarget). A brand in one district grows there as it did.
// The same save had one frame of 35.8 ms in BrandsDirector's second: its first after the load read the save's brands and
// the shops and put the app on the phone in one frame; now the app goes on at the second after, each part timed.
internal static partial class Program
{
    private const string GymType = "ba:businesstype_gym";
    private const string GymDistrict = "ba:neighborhood_midtown";

    private static void BrandRec()
    {
        BrandRecGyms();
        BrandRecTomorrow();
        BrandRecMissed();
        FlareHireQuit();
        FlareHireCounted();
        FlareHireSaved();
        BrandRecWords();
        BrandPaceSpread();
        BrandPacePacked();
        BrandPaceWide();
        BrandPaceCost();
        BrandOfferSpread();
        BrandOfferPacked();
        BrandOfferNever();
        BrandOfferWords();
    }

    // The player's brand: so many shops in each district, all with the same standards and customers.
    private static BrandsState Spread(int[] shopsIn, int customers, int standards, out Brand brand)
    {
        string[] districts = { "ba:neighborhood_garmentdistrict", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill", "ba:neighborhood_hellskitchen", "ba:neighborhood_eastvillage", "ba:neighborhood_chinatown", "ba:neighborhood_harlem", "ba:neighborhood_bronx" };
        var s = new BrandsState { Seed = 1015, FirstDay = 1, LastDay = 1 };
        brand = BrandRules.Create(s, "Loja Boa", GymType, "Shop~0", districts[0], 1);
        int n = 0;
        for (int d = 0; d < shopsIn.Length; d++)
        {
            for (int k = 0; k < shopsIn[d]; k++)
            {
                if (n > 0)
                {
                    BrandRules.Join(s, brand, "Shop~" + n, GymType, districts[d], 1);
                }
                n++;
            }
        }
        foreach (Member m in s.Members)
        {
            m.Standards = standards;
            m.Customers = customers;
        }
        brand.Standards = BrandRules.Standards(s, brand);
        return s;
    }

    // What the old rule gave for the same brand: no pull, the city-wide figure at the districts' sum divided by eight, 5% a day.
    private static int OldRule(int[] shopsIn, int customers, int standards, int days, out int city)
    {
        var rec = new int[shopsIn.Length];
        city = 0;
        for (int day = 0; day < days; day++)
        {
            long sum = 0;
            for (int d = 0; d < rec.Length; d++)
            {
                rec[d] = BrandRules.Step(rec[d], BrandRules.Gain(shopsIn[d], customers * shopsIn[d], standards, 0), shopsIn[d] > 0);
                sum += rec[d];
            }
            int target = (int)Math.Min(BrandCatalogue.RecMost, sum / BrandCatalogue.CityDistricts);
            city = BrandCatalogue.Clamp(city + BrandCatalogue.Bp(target - city, 500), 0, BrandCatalogue.RecMost);
        }
        int best = 0;
        for (int d = 0; d < rec.Length; d++)
        {
            best = Math.Max(best, rec[d]);
        }
        return best;
    }

    private static void BrandPaceSpread()
    {
        int[] spread = { 1, 1, 1 };
        Brand b;
        BrandsState s = Spread(spread, 100, 75, out b);
        int weekly = int.MaxValue, cityWeekly = int.MaxValue, lowest = int.MaxValue, highest = 0;
        var weekStart = new int[b.Districts.Count];
        int cityStart = 0;
        for (int day = 1; day <= 40; day++)
        {
            BrandRules.Day(s, b);
            if (day % 7 == 0)
            {
                for (int d = 0; d < b.Districts.Count; d++)
                {
                    weekly = Math.Min(weekly, b.Districts[d].Rec - weekStart[d]);
                    weekStart[d] = b.Districts[d].Rec;
                }
                if (day > 7)
                {
                    cityWeekly = Math.Min(cityWeekly, b.City - cityStart);   // (the city-wide figure follows its districts: from its second week)
                }
                cityStart = b.City;
            }
        }
        foreach (DistrictFile d in b.Districts)
        {
            lowest = Math.Min(lowest, d.Rec);
            highest = Math.Max(highest, d.Rec);
        }
        int oldCity, oldDistrict = OldRule(spread, 100, 75, 40, out oldCity);
        Check(b.Districts.Count == 3 && weekly >= 100 && lowest >= 800 && highest <= 1300,
            "brand pace: three shops in three districts (standards 75, 100 customers) rise at least " + weekly + " hundredths every week in each district, to " + lowest + " by day 40 (8 to 13 points; " + oldDistrict + " by the old rule)");
        Check(cityWeekly >= 90 && b.City >= 800 && b.City <= 1300,
            "brand pace: and city-wide by at least " + cityWeekly + " hundredths a week from the second, to " + b.City + " by day 40 (8 to 13 points; " + oldCity + " by the old rule)");
        int quotedCity, quoted = BrandRules.AfterSpread(3, 100, 75, 40, out quotedCity);
        Check(quoted == lowest && quotedCity == b.City, "brand pace: the Guide's spread example (" + quoted + ", " + quotedCity + " city-wide) is what the day's steps give");
        Check(BrandRules.Pull(0) == 0 && BrandRules.Pull(-3) == 0 && BrandRules.Pull(1) == 7 && BrandRules.Pull(2) == 12 && BrandRules.Pull(4) == 16 && BrandRules.Pull(60) == 16,
            "brand pace: the pull of shops elsewhere is 7 for the first, two less for each after, 16 at most");
    }

    // Three shops in one district: in the district as it was; city-wide a little quicker (10% a day) and still at most an eighth of it.
    private static void BrandPacePacked()
    {
        int[] packed = { 3 };
        Brand b;
        BrandsState s = Spread(packed, 100, 75, out b);
        for (int day = 1; day <= 40; day++)
        {
            BrandRules.Day(s, b);
        }
        DistrictFile d = b.Districts[0];
        int oldCity, oldDistrict = OldRule(packed, 100, 75, 40, out oldCity);
        Check(d.Rec == oldDistrict && d.Rec == BrandRules.After(3, 300, 75, 40), "brand pace: three shops in one district grow there exactly as before (" + d.Rec + " hundredths by day 40)");
        Check(b.City * BrandCatalogue.CityDistricts <= d.Rec && b.City <= oldCity * 3 / 2 && b.City >= oldCity,
            "brand pace: city-wide it stays under an eighth of its district, " + b.City + " by day 40 against " + oldCity + " by the old rule (a little quicker, not much)");
    }

    // Twenty shops in seven districts with perfect standards and full shops for years: diminishing, no runaway.
    private static void BrandPaceWide()
    {
        int[] wide = { 3, 3, 3, 3, 3, 3, 2 };
        Brand b;
        BrandsState s = Spread(wide, 400, 100, out b);
        for (int day = 1; day <= 2000; day++)
        {
            BrandRules.Day(s, b);
        }
        int best = BrandRules.Best(b);
        int oldCity, oldBest = OldRule(wide, 400, 100, 2000, out oldCity);
        Check(b.Districts.Count == 7 && best <= 7000 && best - oldBest <= 800 && b.City <= best,
            "brand pace: twenty shops in seven districts, perfect for 2,000 days, settle at " + best + " in a district (" + oldBest + " by the old rule) and " + b.City + " city-wide (" + oldCity + " by the old rule): no runaway");
        Check(BrandRules.CityTarget(80000, 16, 1) == 10000 && BrandRules.CityTarget(0, 0, 0) == 0 && BrandRules.CityTarget(9000, 10, 9) == 1125 && BrandRules.CityTarget(3000, 3, 3) == 1000
              && BrandRules.CityTarget(2000, 2, 2) == 500 && BrandRules.CityTarget(800, 1, 1) == 100,
            "brand pace: the city-wide target never passes 100, is the sum divided by eight in one district or more than eight, a quarter of the sum in two, the mean from three");
    }

    // The 35.8 ms second: the start and the app's install never in one frame, each timed; the day's rules for the player's brand cost next to nothing.
    private static void BrandPaceCost()
    {
        string director = System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Economy\Brands\BrandsDirector.cs"));
        int slow = director.IndexOf("private void Slow()", StringComparison.Ordinal);
        int start = slow >= 0 ? director.IndexOf("StartUp();", slow, StringComparison.Ordinal) : -1;
        int back = start >= 0 ? director.IndexOf("return;", start, StringComparison.Ordinal) : -1;
        int install = start >= 0 ? director.IndexOf("Install();", start, StringComparison.Ordinal) : -1;
        Check(start >= 0 && back > start && install > back && director.Contains("HotSpots.Time(HotStart)") && director.Contains("HotSpots.Time(HotInstall)"),
            "brand pace: the Brands start (the state and the shops) and the app's install are in seconds of their own, each timed");
        Brand b;
        BrandsState s = Spread(new[] { 1, 1, 1 }, 100, 75, out b);
        BrandPaceDays(s, b, 50);   // (warmed)
        double ms = BrandPaceDays(s, b, 1000);
        Check(ms / 1000 < 0.05, "brand pace: a day of the player's brand's rules takes " + (ms / 1000 * 1000).ToString("0.0") + " microseconds");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double BrandPaceDays(BrandsState s, Brand b, int days)
    {
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < days; i++)
        {
            BrandRules.Day(s, b);
        }
        return watch.Elapsed.TotalMilliseconds;
    }

    // A save at day 60: one brand, three member shops in three districts, standards over 60, and no franchise on offer: the offer wanted
    // 40 points in one district (or 25 city-wide) and a spread brand's districts and city-wide figure were near 12 by day 40. Now the mean over
    // the districts where it has a member shop counts too (BrandCatalogue.OfferMeanRec, from two districts).
    private static int FirstOfferDay(int[] shopsIn, int customers, int standards, int days, out BrandsState state, out Brand brand)
    {
        state = Spread(shopsIn, customers, standards, out brand);
        int good;
        for (int day = 1; day <= days; day++)
        {
            BrandRules.Day(state, brand);
            if (BrandRules.CanOffer(state, brand, out good))
            {
                return day;
            }
        }
        return -1;
    }

    private static void BrandOfferSpread()
    {
        foreach (int standards in new[] { 75, 100 })
        {
            Brand b;
            BrandsState s;
            int day = FirstOfferDay(new[] { 1, 1, 1 }, 100, standards, 80, out s, out b);
            Check(day >= 20 && day <= 35, "brand offer: three shops in three districts with standards of " + standards + " and 100 customers a day each can be offered from day " + day + " (not before 20, by 35)");
            Check(b.City < BrandCatalogue.OfferCityRec && BrandRules.Best(b) < BrandCatalogue.OfferDistrictRec && BrandRules.SpreadRec(s, b) >= BrandCatalogue.OfferMeanRec,
                "brand offer: and it is the mean over its districts that opens it (" + b.City + " city-wide of " + BrandCatalogue.OfferCityRec + ", best " + BrandRules.Best(b) + " of " + BrandCatalogue.OfferDistrictRec + ")");
        }
        Brand old;
        BrandsState state = Spread(new[] { 1, 1, 1 }, 100, 75, out old);
        for (int day = 1; day <= 60; day++)
        {
            BrandRules.Day(state, old);
        }
        Check(old.City < BrandCatalogue.OfferCityRec && BrandRules.Best(old) < BrandCatalogue.OfferDistrictRec,
            "brand offer: by the old rule (25 city-wide or 40 in a district) the player's brand was still not ready at day 60 (" + old.City + " city-wide, best " + BrandRules.Best(old) + ")");
        int districts;
        Check(BrandRules.MeanRec(state, old, out districts) == BrandRules.SpreadRec(state, old) && districts == 3, "brand offer: the mean counts the three districts where it has a member shop");
        state.Members.RemoveAt(state.Members.Count - 1);
        BrandRules.MeanRec(state, old, out districts);
        Check(districts == 2 && BrandRules.SpreadRec(state, old) > 0, "brand offer: a district with no member shop left is not in the mean");
    }

    // Three shops packed into one district: no mean, the district's own 40 (or 25 city-wide) as before.
    private static void BrandOfferPacked()
    {
        Brand b;
        BrandsState s;
        int day = FirstOfferDay(new[] { 3 }, 100, 75, 1000, out s, out b);
        Check(day > 40 && BrandRules.SpreadRec(s, b) == 0 && (BrandRules.Best(b) >= BrandCatalogue.OfferDistrictRec || b.City >= BrandCatalogue.OfferCityRec),
            "brand offer: three shops in one district are ready on day " + day + ", when the district reaches " + BrandRules.Best(b) + " of " + BrandCatalogue.OfferDistrictRec + " (no mean for one district)");
        b.Districts[0].Rec = BrandCatalogue.OfferDistrictRec - 1;
        b.City = 0;
        int good;
        bool below = BrandRules.CanOffer(s, b, out good);
        b.Districts[0].Rec = BrandCatalogue.OfferDistrictRec;
        Check(!below && BrandRules.CanOffer(s, b, out good) && good == 3, "brand offer: 40 in one district opens it and 39.99 does not");
        b.Districts[0].Rec = BrandCatalogue.OfferMeanRec + 500;
        Check(!BrandRules.CanOffer(s, b, out good), "brand offer: a packed brand at the mean's figure alone is not ready");
    }

    // Under three member shops, or under standards of 60, never: whatever the recognition.
    private static void BrandOfferNever()
    {
        Brand b;
        BrandsState s = Spread(new[] { 1, 1 }, 400, 100, out b);
        foreach (DistrictFile d in b.Districts)
        {
            d.Rec = BrandCatalogue.RecMost;
        }
        b.City = BrandCatalogue.RecMost;
        int good;
        Check(!BrandRules.CanOffer(s, b, out good) && good == 2, "brand offer: two member shops are never enough, at full recognition everywhere");
        BrandRules.Join(s, b, "Shop~9", GymType, "ba:neighborhood_chinatown", 1);
        s.Members[s.Members.Count - 1].Standards = 100;
        Check(BrandRules.CanOffer(s, b, out good) && good == 3, "brand offer: the third shop opens it");
        s.Members[0].Standards = BrandCatalogue.OfferStandards - 1;
        Check(!BrandRules.CanOffer(s, b, out good) && good == 2, "brand offer: a shop under standards of 60 does not count");
        int never = 0;
        Brand low;
        BrandsState sl = Spread(new[] { 1, 1, 1 }, 400, 59, out low);
        for (int day = 1; day <= 400; day++)
        {
            BrandRules.Day(sl, low);
            never += BrandRules.CanOffer(sl, low, out good) ? 1 : 0;
        }
        Check(never == 0, "brand offer: three shops with standards of 59 are never ready in 400 days (" + never + ")");
    }

    private static void BrandOfferWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("brand offer", langs, codes))
        {
            return;
        }
        var missing = new List<string>();
        foreach (string key in new[] { "reallife_brand_set_cannot_offer", "reallife_brand_note_cannot_offer", "reallife_guide_economy_brands" })
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "brand offer: the three texts that say when a brand can be offered are in every language with the same {names} (" + string.Join(", ", missing) + ")");
        bool mean = true, plain = true;
        for (int l = 0; l < langs.Length; l++)
        {
            bool set = langs[l].TryGetValue("reallife_brand_set_cannot_offer", out string a) && a.Contains("{mean}") && a.Contains("{average}");
            bool note = langs[l].TryGetValue("reallife_brand_note_cannot_offer", out string n) && n.Contains("{mean}");
            bool guide = langs[l].TryGetValue("reallife_guide_economy_brands", out string g) && g.Contains("{br_offer_mean}") && g.Contains("{br_offer_mean_districts}");
            mean &= set && note && guide;
            foreach (string key in new[] { "reallife_brand_set_cannot_offer", "reallife_brand_note_cannot_offer" })
            {
                plain &= !langs[l][key].Contains("!") && !langs[l][key].Contains("—") && !langs[l][key].Contains("–");
            }
        }
        Check(mean, "brand offer: the page, the notice and the Guide all give the average over districts, in every language");
        Check(plain && langs[0]["reallife_brand_note_cannot_offer"].Length <= 240, "brand offer: the notice keeps to 240 characters, with no dash and no exclamation mark");
        string brands = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Economy\Brands");
        string guideCode = System.IO.File.ReadAllText(System.IO.Path.Combine(brands, "BrandsGuide.cs"));
        string appCode = System.IO.File.ReadAllText(System.IO.Path.Combine(brands, "BrandsApp.Brands.cs"));
        string actions = System.IO.File.ReadAllText(System.IO.Path.Combine(brands, "BrandsDirector.Actions.cs"));
        Check(guideCode.Contains("\"br_offer_mean\", () => Whole(BrandCatalogue.OfferMeanRec / 100)") && guideCode.Contains("\"br_offer_mean_districts\"")
              && appCode.Contains("BrandCatalogue.OfferMeanRec") && appCode.Contains("BrandRules.SpreadRec") && actions.Contains("BrandCatalogue.OfferMeanRec"),
            "brand offer: the Guide's facts, the brand page and the notice are filled from BrandCatalogue.OfferMeanRec, not from a number of their own");
        bool line = true;
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            // any reallife_changes_ row: the line is in the next row until a release numbers it
            bool found = false;
            foreach (var row in langs[l])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal)
                         && row.Value.Length > 20 && (l != 0 || row.Value.Contains("Fixed: a brand with its shops spread over several districts"));
            }
            line &= found;
        }
        Check(line, "brand offer: What's new has the fix's line in English, Portuguese and Chinese, in some reallife_changes_ row");
    }

    // Three gyms of a brand in one district, each with standards of 85 (the player's store rating over 80).
    private static BrandsState Gyms(int customers, out Brand brand)
    {
        var s = new BrandsState { Seed = 1015, FirstDay = 1, LastDay = 1 };
        brand = BrandRules.Create(s, "Iron Works", GymType, "Gym~1", GymDistrict, 1);
        BrandRules.Join(s, brand, "Gym~2", GymType, GymDistrict, 1);
        BrandRules.Join(s, brand, "Gym~3", GymType, GymDistrict, 1);
        foreach (Member m in s.Members)
        {
            m.Standards = 85;
            m.Customers = customers;
            m.District = GymDistrict;
        }
        brand.Standards = BrandRules.Standards(s, brand);   // (as the morning's read of the shops leaves it)
        return s;
    }

    private static void BrandRecGyms()
    {
        foreach (int customers in new[] { 0, 120 })   // (a gym's visits counted as customers or not: it grows either way)
        {
            Brand b;
            BrandsState s = Gyms(customers, out b);
            DistrictFile d = b.District(GymDistrict);
            bool rising = true, seenDaily = true;
            int wholeFlat = 0;
            for (int day = 2; day <= 21; day++)
            {
                int before = d.Rec;
                BrandRules.Day(s, b);
                rising &= d.Rec > before;
                seenDaily &= BrandRules.Shown(d.Rec) > BrandRules.Shown(before);
                wholeFlat += d.Rec / 100 == before / 100 ? 1 : 0;
            }
            Check(rising && d.Rec >= 700 && d.Rec <= 1200,
                "brand recognition: three gyms with standards of 85 and " + customers + " customers a day grow every morning, to " + d.Rec + " hundredths after 20 days (7 to 12 points)");
            Check(seenDaily && wholeFlat >= 5,
                "brand recognition: in tenths the app shows a change every morning; in whole points, as it was shown, " + wholeFlat + " of the 20 mornings looked flat");
            Check(b.City * BrandCatalogue.CityDistricts <= d.Rec && BrandRules.Shown(b.City) > 0 && b.City / 100 == 0,
                "brand recognition: city-wide a brand in one district heads for its district's figure divided by " + BrandCatalogue.CityDistricts + " (" + b.City + " hundredths: 0 in whole points, shown now)");
            Check(b.Age == 20 && BrandRules.Missed(b, 21) == 0, "brand recognition: twenty mornings, twenty steps, none missed");
        }
        int quoted = BrandRules.After(3, 100, 75, 20);
        Check(quoted >= 700 && quoted < 1000 && BrandRules.Gain(3, 100, 75, 0) == 47,
            "brand recognition: the Guide's example (three shops, 100 customers, standards of 75, 20 days) is " + quoted + " hundredths, from 0.47 a day");
        Check(BrandRules.Shown(-5) == 0 && BrandRules.Shown(45) == 4 && BrandRules.Shown(BrandCatalogue.RecMost + 50) == 1000, "brand recognition: tenths shown stay within 0 to 100");
    }

    // The district page's "tomorrow" is what the step does, a campaign's first and last day included.
    private static void BrandRecTomorrow()
    {
        Brand b;
        BrandsState s = Gyms(120, out b);
        DistrictFile d = b.District(GymDistrict);
        BrandRules.BookPush(b, BrandCatalogue.PushLocal, GymDistrict);
        bool same = true;
        for (int day = 0; day < BrandRules.PushDays(BrandCatalogue.PushLocal) + 3; day++)
        {
            int said = BrandRules.Step(d.Rec, BrandRules.GainOn(s, b, d, b.Age + 1), true);
            BrandRules.Day(s, b);
            same &= said == d.Rec;
        }
        Check(same, "brand recognition: the district page's tomorrow is the next morning's figure, through a campaign's first and last day");
    }

    private static void BrandRecMissed()
    {
        Brand b;
        BrandsState s = Gyms(120, out b);
        b.Founded = 10;
        for (int i = 0; i < 5; i++)
        {
            BrandRules.Day(s, b);
        }
        Check(BrandRules.Missed(b, 15) == 0 && BrandRules.Missed(b, 20) == 5 && BrandRules.Missed(b, 10) == 0 && BrandRules.Missed(b, 3) == 0,
            "brand recognition: mornings missed are those since the brand began with no step (Brands off), never under nothing");
        Check(BrandRules.StepsKey(0, 0) == "reallife_brand_fig_steps_first" && BrandRules.StepsKey(1, 1) == "reallife_brand_fig_steps_one"
              && BrandRules.StepsKey(1, 0) == "reallife_brand_fig_steps_one_still" && BrandRules.StepsKey(2, 1) == "reallife_brand_fig_steps",
            "brand recognition: the page says it starts tomorrow on the founding day, one morning in the singular, and so many of so many after");
        Check(BrandRules.MissedKey(1) == "reallife_brand_fig_missed_one" && BrandRules.MissedKey(5) == "reallife_brand_fig_missed",
            "brand recognition: one morning missed in the singular, several in the plural");
        b.Age = 400;   // (a test tool's year)
        Check(BrandRules.Missed(b, 20) == 0, "brand recognition: a brand whose clock ran ahead misses nothing");
    }

    // The case reported: the influencer hired stops posting (the month) before or during the deal.
    private static void FlareHireQuit()
    {
        FlareFile f = FlareCastFile(1015);
        FlareInfluencer fit = f.Influencers.Find(n => n.Niche == FlareNiche.Fitness);
        var shops = new List<FlareShop> { new FlareShop { Place = "Gym~1", Name = "Iron Works", Niche = FlareNiche.Fitness, District = fit.District } };
        var hire = new FlareHire { Id = 900, Kind = 6, Place = "Gym~1", Name = "Iron Works", From = 10, To = 16 };
        hire.Who.Add(fit.Id);
        f.Hires.Add(hire);
        fit.Quit = true;
        var visits = new List<FlarePost>();
        for (int day = 10; day <= 18; day++)
        {
            FlareCast.Day(f, day, shops, null, null, null, visits);
        }
        int paid = f.Posts.FindAll(p => p.Who == fit.Id && p.Source == FlareSource.Hired && p.Place == "Gym~1").Count;
        Check(paid == 5 && hire.Posted == 5 && hire.LastPost == 16,
            "Flare: an influencer who stopped posting still makes the 5 posts of a week's series paid for (" + paid + " made, " + hire.Posted + " counted, the last on day " + hire.LastPost + ")");
        Check(!f.Posts.Exists(p => p.Who == fit.Id && p.Day > 16), "Flare: and nothing more once the deal is over");

        var late = new FlareHire { Id = 901, Kind = 5, Place = "Gym~1", Name = "Iron Works", From = 30, To = 31, Late = true };
        late.Who.Add(fit.Id);
        f.Hires.Add(late);
        FlareCast.Day(f, 30, shops, null, null, null, visits);
        Check(late.Posted == 0 && FlareCast.HireStage(late, 30) == FlareCast.HireWaiting, "Flare: a late one has no post on the deal's first day, and the page says none yet");
        FlareCast.Day(f, 31, shops, null, null, null, visits);
        Check(late.Posted == 1 && late.LastPost == 31 && FlareCast.HireStage(late, 31) == FlareCast.HirePosting, "Flare: then the post comes, counted on the deal");
        Check(FlareCast.HireStage(late, 29) == FlareCast.HireBefore && FlareCast.HireStage(late, 32) == FlareCast.HireOver, "Flare: before its first day and after its last the page says so");
        var ended = new FlareHire { Id = 902, Kind = 6, Place = "Gym~1", Name = "Iron Works", From = 40, To = 46, Ended = true };
        ended.Who.Add(fit.Id);
        f.Hires.Add(ended);
        FlareCast.Day(f, 41, shops, null, null, null, visits);
        Check(ended.Posted == 0 && !f.Posts.Exists(p => p.Who == fit.Id && p.Day == 41), "Flare: a deal the player ended makes no post, from one who has quit or anyone");
    }

    private static void FlareHireCounted()
    {
        FlareFile f = FlareCastFile(1016);
        FlareInfluencer fit = f.Influencers.Find(n => n.Niche == FlareNiche.Fitness);
        var shops = new List<FlareShop> { new FlareShop { Place = "Gym~1", Name = "Iron Works", Niche = FlareNiche.Fitness, District = fit.District } };
        var hire = new FlareHire { Id = 910, Kind = 6, Place = "Gym~1", Name = "Iron Works", From = 5, To = 11 };
        hire.Who.Add(fit.Id);
        f.Hires.Add(hire);
        f.Posts.Add(new FlarePost { Id = 911, Day = 3, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Sponsored, Place = "Gym~1" });
        f.Posts.Add(new FlarePost { Id = 912, Day = 4, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Sponsored, Place = "Gym~2" });
        f.Posts.Add(new FlarePost { Id = 913, Day = 4, Hour = 10, Author = FlareAuthor.Player, Source = FlareSource.Review, Place = "Gym~1", Arg = 4 });   // (a review is counted apart on the card)
        var visits = new List<FlarePost>();
        for (int day = 5; day <= 8; day++)
        {
            FlareCast.Day(f, day, shops, null, null, null, visits);
        }
        int own, last;
        int all = FlareCast.PostsAbout(f, "Gym~1", out own, out last);
        Check(all == 4 && own == 1 && last == 8,
            "Flare: the Business tab counts a business's paid posts with the player's own (" + all + " in all, " + own + " own, the last on day " + last + "; the series' 3 so far were not counted before)");
        for (int day = 9; day < 9 + 40; day++)
        {
            FlareCast.Day(f, day, shops, new[] { "Rival Corp" }, new[] { "A Candidate" }, new[] { "A Brand" }, visits);
        }
        all = FlareCast.PostsAbout(f, "Gym~1", out own, out last);
        Check(hire.Posted == 5 && all == 6 && last == 11, "Flare: the count holds after the Feed has let the deal's posts go (" + all + ", the last on day " + last + ")");
        Check(FlareCast.PostsAbout(f, "Nowhere~0", out own, out last) == 0 && own == 0 && last == -1, "Flare: a business with no post has none, and no last day");
        // The Business tab's card and a deal's card read these, not a count of their own (Social/Flare/FlareApp.Business.cs).
        string screens = System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Social\Flare\FlareApp.Business.cs"));
        Check(screens.Contains("FlareCast.PostsAbout(file, row.Key, out own, out last)") && screens.Contains("FlareCast.HirePostsKey(h, day)") && screens.Contains("\"reallife_flare_shop_paid_posts\""),
            "Flare: the Business tab's shop card counts with PostsAbout and a deal's card says where its posts stand");
    }

    private static void FlareHireSaved()
    {
        FlareFile f = FlareCastFile(1017);
        var hire = new FlareHire { Id = 920, Kind = 6, Place = "Gym%20Street~1", Name = "Iron Works", From = 5, To = 11, Posted = 4, LastPost = 9 };
        hire.Who.Add(f.Influencers[0].Id);
        f.Hires.Add(hire);
        string line = FlareStore.WriteCast(f);
        var back = new FlareFile();
        FlareStore.ReadCast(back, line);
        Check(back.Hires.Count == 1 && back.Hires[0].Posted == 4 && back.Hires[0].LastPost == 9 && FlareStore.WriteCast(back) == line,
            "Flare: a deal's posts so far and its last post's day are kept in the save");
        int at = line.IndexOf("\nH|", StringComparison.Ordinal);
        int end = line.IndexOf('\n', at + 1);
        string hireLine = end < 0 ? line.Substring(at) : line.Substring(at, end - at);
        string[] fields = hireLine.Split('|');
        string older = line.Replace(hireLine, string.Join("|", fields, 0, 13));             // (as 0.26.0 and before wrote a deal)
        string counting = line.Replace(hireLine, string.Join("|", fields, 0, 15));          // (with the posts counted, before the mark was written)
        var old = new FlareFile();
        FlareStore.ReadCast(old, older);
        FlareHire o = old.Hires.Count == 1 ? old.Hires[0] : null;
        Check(o != null && o.Posted == 0 && o.LastPost == -1 && !o.Counted && o.To == 11,
            "Flare: a deal saved before the posts were counted loads as it was, its count marked as not known");
        var mid = new FlareFile();
        FlareStore.ReadCast(mid, counting);
        Check(mid.Hires.Count == 1 && mid.Hires[0].Counted && mid.Hires[0].Posted == 4, "Flare: a deal saved with its count but no mark is counted whole");

        // What a deal's card says from an older save, half run: not "No post yet", and then only the posts since the update, said as such.
        Check(o != null && FlareCast.HirePostsKey(o, 8) == null, "Flare: a deal from an older save, half run, says nothing of its posts until one comes (not \"No post yet\")");
        FlareInfluencer who = old.Influencers.Find(n => n.Id == o.Who[0]);
        var shops = new List<FlareShop> { new FlareShop { Place = o.Place, Name = o.Name, Niche = who.Niche, District = who.District } };
        var visits = new List<FlarePost>();
        FlareCast.Day(old, 8, shops, null, null, null, visits);
        Check(!o.Counted && o.Posted == 1 && FlareCast.HirePostsKey(o, 8) == "reallife_flare_hire_posts_since_one", "Flare: its first post after the update is said as one since the update");
        FlareCast.Day(old, 9, shops, null, null, null, visits);
        Check(!o.Counted && o.Posted == 2 && FlareCast.HirePostsKey(o, 9) == "reallife_flare_hire_posts_since", "Flare: then as so many since the update");

        // A deal from an older save that had not begun is counted whole from its first post.
        var fresh = new FlareFile();
        FlareStore.ReadCast(fresh, older);
        FlareHire n0 = fresh.Hires[0];
        Check(FlareCast.HirePostsKey(n0, 4) == "reallife_flare_hire_posts_from", "Flare: before its first day the card says when the posts begin");
        FlareCast.Day(fresh, 5, shops, null, null, null, visits);
        Check(n0.Counted && n0.Posted == 1 && FlareCast.HirePostsKey(n0, 5) == "reallife_flare_hire_posts_one", "Flare: one that begins after the update is counted whole: one paid post, said in the singular");
        FlareCast.Day(fresh, 6, shops, null, null, null, visits);
        Check(FlareCast.HirePostsKey(n0, 6) == "reallife_flare_hire_posts", "Flare: then so many paid posts");

        var counted = new FlareHire { From = 5, To = 11 };
        var waiting = new FlareHire { From = 5, To = 11, Late = true };
        Check(FlareCast.HirePostsKey(waiting, 5) == "reallife_flare_hire_posts_none" && FlareCast.HirePostsKey(counted, 12) == "reallife_flare_hire_posts_none_over",
            "Flare: a counted deal with no post yet says none yet, and one over with none says so");
    }

    private static void BrandRecWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("brand recognition", langs, codes))
        {
            return;
        }
        string[] keys =
        {
            "reallife_brand_fig_steps", "reallife_brand_fig_missed", "reallife_brand_fig_city", "reallife_flare_shop_paid_posts", "reallife_flare_hire_posts_from",
            "reallife_flare_hire_posts", "reallife_flare_hire_posts_none", "reallife_flare_hire_posts_none_over", "reallife_flare_hire_posts_where",
            "reallife_brand_fig_steps_first", "reallife_brand_fig_steps_one", "reallife_brand_fig_steps_one_still", "reallife_brand_fig_missed_one",
            "reallife_flare_hire_posts_one", "reallife_flare_hire_posts_since", "reallife_flare_hire_posts_since_one"
        };
        var missing = new List<string>();
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "brand recognition: every new text is in every language with the same {names} (" + string.Join(", ", missing) + ")");
        bool guide = true;
        for (int l = 0; l < langs.Length; l++)
        {
            guide &= langs[l].TryGetValue("reallife_guide_economy_brands", out string g) && g.Contains("{br_three_gain}") && g.Contains("{br_three_after}") && g.Contains("{br_city_one}");
        }
        Check(guide, "brand recognition: the Guide's Brands page says how fast recognition grows and why the city-wide figure stays low, in every language");
        // In What's new: the next row until the release, the version's own row after. Every row is looked in, the whole of it.
        bool line = false;
        foreach (KeyValuePair<string, string> row in langs[0])
        {
            line |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Fixed: an influencer you hired on Flare who stopped posting");
        }
        Check(line, "brand recognition: What's new has the fix's line, in some reallife_changes_ row");
    }
}
