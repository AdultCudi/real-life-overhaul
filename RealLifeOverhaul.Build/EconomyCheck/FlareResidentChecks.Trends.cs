using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;

// Flare in the city, trends (the brief's fifth step), game-free on the population's city: a trend begins only for what has a taste and only once at a
// time, at most three; while it rises it gives the taste to about its share of those without it and to nobody who had it, so a household spends more
// of its money on the kind (the total kept); a district's going out is heard of across the city, so its places of the kind draw more and no other;
// a genre is taken up by residents who play; as it fades it lets go all but the few who keep the taste, found again by id after a load; the city's
// own talk starts one about once a month; the save, the guests' part, the screen, the words and the cost.
internal static partial class Program
{
    private static void FlareResidentTrends()
    {
        FlareResidentRig rig = FlareResidentCity(42, 5000, (int)FlareTier.Known);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int[] slots = rig.Pass.PersonSlot;
        HouseholdTable hh = s.Households;
        int day0 = s.Day;
        Check(FlareCityRules.StartTrend(c, TrendKind.Goods, (int)Spend.Groceries, -1, day0, false) == null && FlareCityRules.StartTrend(c, TrendKind.District, (int)Spend.NightLife, -1, day0, false) == null
              && FlareCityRules.StartTrend(c, TrendKind.Genre, GameRules.Genres, -1, day0, false) == null && c.Trends.Count == 0,
            "flare city trends: none for a kind no household has a taste for, a district's going out with no district, or a genre there is not");

        // a kind of goods in the whole city: about its share of the households without the taste take to it, nobody who had it
        uint taste = 1u << FlareCityRules.TasteBit((int)Spend.Clothing);
        uint[] before = (uint[])hh.Tastes.Clone();
        int lacking = 0;
        for (int h = 0; h < hh.Count; h++)
        {
            lacking += hh.Alive(h) && (hh.Tastes[h] & taste) == 0 ? 1 : 0;
        }
        FlareTrend goods = FlareCityRules.StartTrend(c, TrendKind.Goods, (int)Spend.Clothing, -1, day0, false);
        bool once = FlareCityRules.StartTrend(c, TrendKind.Goods, (int)Spend.Clothing, -1, day0, false) == null;
        int firstDay = 0;
        for (int d = 0; d < FlareCityCatalogue.TrendRiseDays; d++)
        {
            FlareCityRules.TrendsDay(c, s, day0 + d, slots);
            firstDay = d == 0 ? goods.Moved : firstDay;
        }
        int moved = goods.Moved;
        bool fresh = true;
        for (int i = 0; i < moved; i++)
        {
            int h = goods.Slots[i];
            fresh &= (before[h] & taste) == 0 && (hh.Tastes[h] & taste) != 0 && hh.Id[h] == goods.Ids[i];
        }
        long share = moved * 1000L / Math.Max(1, lacking);
        Check(goods != null && once && fresh && Math.Abs(share - FlareCityCatalogue.TrendReach) <= 25 && firstDay > 0 && firstDay < moved,
            "flare city trends: a trend of goods gives the taste to about its share of the households without it as it rises, day by day, and to nobody who had it ("
            + moved + " of " + lacking + ", " + firstDay + " the first day)");
        var plain = new int[SpendRules.Kinds];
        var liking = new int[SpendRules.Kinds];
        SpendRules.Shares(SpendRules.LevelMid, 0u, false, plain);
        SpendRules.Shares(SpendRules.LevelMid, taste, false, liking);
        int sumPlain = 0, sumLiking = 0;
        for (int k = 0; k < SpendRules.Kinds; k++)
        {
            sumPlain += plain[k];
            sumLiking += liking[k];
        }
        Check(liking[(int)Spend.Clothing] > plain[(int)Spend.Clothing] * 11 / 10 && Math.Abs(sumPlain - sumLiking) <= SpendRules.Kinds,
            "flare city trends: a household that has taken to it spends more of its money on the kind, its total the same (" + plain[(int)Spend.Clothing] + " to " + liking[(int)Spend.Clothing] + " thousandths)");
        FlareCityRules.TrendsDay(c, s, day0 + FlareCityCatalogue.TrendRiseDays + 3, slots);
        bool holds = goods.Moved == moved && FlareCityRules.Stage(goods, day0 + FlareCityCatalogue.TrendRiseDays + 3) == FlareCityRules.StageHeight;

        // a post about what is trending, and a viral post's trend
        int shop = FlareResidentShop(s, Spend.Clothing), club = FlareResidentShop(s, Spend.NightLife), grocer = FlareResidentShop(s, Spend.Groceries);
        var about = new CityPost { Business = shop, Kind = (int)Spend.Clothing };
        var other = new CityPost { Business = grocer, Kind = (int)Spend.Groceries };
        string hear = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Social", "Flare", "City", "FlareCityRules.Hear.cs"));
        Check(holds && FlareCityRules.Trending(c, s, about) && !FlareCityRules.Trending(c, s, other) && hear.Contains("Trending(c, s, post) ? FlareCityCatalogue.TrendInterest : 0"),
            "flare city trends: at its height it holds, and a post about what is trending interests more of the residents who hear it");

        // a district's going out: heard of across the city, its places of the kind draw more, no other
        int district = s.Businesses.District[club];
        int elsewhere = -1, besides = -1;
        for (int b = 0; b < s.Businesses.Count; b++)
        {
            bool sells = FlareCityRules.Sells(s.Businesses, b);
            elsewhere = elsewhere < 0 && sells && s.Businesses.Kind[b] == (byte)Spend.NightLife && s.Businesses.District[b] != district ? b : elsewhere;
            besides = besides < 0 && sells && s.Businesses.Kind[b] != (byte)Spend.NightLife && s.Businesses.District[b] == district ? b : besides;
        }
        long[] w = rig.Pass.Weights;
        int clubBefore = FlareCityRules.AttentionOf(c, s, club, w), elsewhereBefore = elsewhere >= 0 ? FlareCityRules.AttentionOf(c, s, elsewhere, w) : 0;
        int besidesBefore = besides >= 0 ? FlareCityRules.AttentionOf(c, s, besides, w) : 0;
        FlareTrend going = FlareCityRules.TrendOf(c, s, club, day0, true);
        for (int d = 0; d < FlareCityCatalogue.TrendRiseDays; d++)
        {
            FlareCityRules.TrendsDay(c, s, day0 + d, slots);
        }
        bool home = going != null && going.Moved > 0;
        for (int i = 0; home && i < going.Moved; i++)
        {
            home = hh.District[going.Slots[i]] == district;
        }
        int clubAfter = FlareCityRules.AttentionOf(c, s, club, w);
        Check(going != null && going.Kind == TrendKind.District && going.Mine && home && clubAfter > clubBefore + 100
              && (elsewhere < 0 || FlareCityRules.AttentionOf(c, s, elsewhere, w) == elsewhereBefore) && (besides < 0 || FlareCityRules.AttentionOf(c, s, besides, w) == besidesBefore),
            "flare city trends: a viral post about a place to go out makes its district's going out the city's: its households take to it, and its places of the kind draw more ("
            + clubBefore + " to " + clubAfter + " thousandths), no other");
        Check(FlareCityRules.TrendOf(c, s, grocer, day0, true) == null, "flare city trends: a viral post about a grocer starts none (no household has a taste for groceries)");

        // a genre: taken up by residents who play and did not like it
        const int genre = 3;
        int players = 0, liked = 0;
        int adults = c.AdultStart[c.AdultStart.Length - 1];
        for (int i = 0; i < adults; i++)
        {
            ushort g = s.People.Games[c.AdultList[i]];
            players += GameRules.PlatformsOf(g) != 0 && (g & (1 << genre)) == 0 ? 1 : 0;
            liked += (g & (1 << genre)) != 0 ? 1 : 0;
        }
        var watch = Stopwatch.StartNew();
        FlareTrend games = FlareCityRules.StartTrend(c, TrendKind.Genre, genre, -1, day0, false);
        bool most = FlareCityRules.StartTrend(c, TrendKind.Goods, (int)Spend.Gym, -1, day0, false) == null;   // three already
        double worst = 0;
        for (int d = 0; d < FlareCityCatalogue.TrendRiseDays; d++)
        {
            watch.Restart();
            FlareCityRules.TrendsDay(c, s, day0 + d, slots);
            worst = Math.Max(worst, watch.Elapsed.TotalMilliseconds);
        }
        int likedAfter = 0;
        bool played = true;
        for (int i = 0; i < adults; i++)
        {
            likedAfter += (s.People.Games[c.AdultList[i]] & (1 << genre)) != 0 ? 1 : 0;
        }
        for (int i = 0; i < games.Moved; i++)
        {
            played &= GameRules.PlatformsOf(s.People.Games[games.Slots[i]]) != 0;
        }
        long genreShare = games.Moved * 1000L / Math.Max(1, players);
        Check(games != null && most && played && likedAfter == liked + games.Moved && Math.Abs(genreShare - FlareCityCatalogue.TrendGenreReach) <= 30,
            "flare city trends: a genre is taken up by about its share of the residents who play and did not like it, which the studio's audience counts (" + liked + " to " + likedAfter + ")");
        Check(worst < FlareCityCatalogue.SliceMs * 4, "flare city trends: a day of three rising trends fits a few slices (" + worst.ToString("0.00") + " ms at worst)");

        // the save and the guests' part keep them, and a trend read back fades from its households found again by id
        string line = FlareCityStore.Encode(c);
        FlareCityState back = FlareCityStore.Decode(line, out _);
        bool same = back != null && back.Trends.Count == c.Trends.Count && back.NextTrend == c.NextTrend;
        for (int i = 0; same && i < c.Trends.Count; i++)
        {
            FlareTrend a = c.Trends[i], b = back.Trends[i];
            same = a.Id == b.Id && a.Kind == b.Kind && a.What == b.What && a.District == b.District && a.Day == b.Day && a.Mine == b.Mine && a.Moved == b.Moved && a.Let == b.Let;
            for (int m = 0; same && m < a.Moved; m++)
            {
                same = a.Ids[m] == b.Ids[m] && b.Slots[m] == -1;
            }
        }
        Check(same && back.TrendCity == c.TrendCity && back.TrendDistrict[(int)Spend.NightLife] == district, "flare city trends: the save keeps every trend and the residents it moved, by id");
        string part = FlareCityShare.Write(c, s, s.Day, slots, null);
        var followed = new FlareCityFollowed();
        bool read = FlareCityShare.Read(part, followed);
        Check(read && followed.Trends.Count == c.Trends.Count && followed.Trends.Exists(t => t.Kind == TrendKind.District && t.District == s.Districts[district].Id && t.Moved == going.Moved),
            "flare city trends: a guest hears the host's trends in its part, the district by the game's id");
        FlareCityView view = FlareCityViews.Trends(FlareCityRules.Seen(c, s), day0 + 2);
        Check(view.Rows.Count == c.Trends.Count && view.Rows.TrueForAll(r => r.TitleKey && r.What != null && r.Subtitle.EndsWith("trend_rising", StringComparison.Ordinal) && r.Second > 0)
              && view.Rows.Exists(r => r.Where == s.Districts[district].Id && r.Title.EndsWith("trend_district", StringComparison.Ordinal)),
            "flare city trends: Explore shows each trend, where, how far along, how many it moved and the days left");

        // fading on the state read back: all but the few who keep the taste let go, the rest of the city as it was
        for (int d = FlareCityCatalogue.TrendDays; d <= FlareCityCatalogue.TrendDays + FlareCityCatalogue.TrendFadeDays; d++)
        {
            FlareCityRules.TrendsDay(back, s, day0 + d, slots);
        }
        int kept = 0;
        var movedSet = new HashSet<int>(goods.Slots);
        bool restored = true;
        for (int i = 0; i < goods.Moved; i++)
        {
            kept += (hh.Tastes[goods.Slots[i]] & taste) != 0 ? 1 : 0;
        }
        for (int h = 0; h < hh.Count; h++)
        {
            restored &= movedSet.Contains(h) || (hh.Tastes[h] & taste) == (before[h] & taste);
        }
        long stick = kept * 1000L / Math.Max(1, goods.Moved);
        Check(back.Trends.Count == 0 && restored && Math.Abs(stick - FlareCityCatalogue.TrendStick) <= 35,
            "flare city trends: as it fades it lets go all but the few who keep the taste (" + kept + " of " + goods.Moved + "), found again by id after a load, and is done");

        // the city's own talk: about once a month, a kind of goods, a district's going out or a genre
        var city = new FlareCityState { Seed = c.Seed, Adults = c.Adults };
        int started = 0;
        var kinds = new int[3];
        for (int day = 1; day <= 2000; day++)
        {
            FlareTrend t = FlareCityRules.CityTrend(city, s, day);
            if (t != null)
            {
                started++;
                kinds[(int)t.Kind]++;
                city.Trends.Clear();
            }
        }
        Check(started > 2000 * FlareCityCatalogue.TrendChance / 1000 * 2 / 3 && started < 2000 * FlareCityCatalogue.TrendChance / 1000 * 3 / 2 && kinds[0] > kinds[1] && kinds[1] > 0 && kinds[2] > 0,
            "flare city trends: the city's own talk starts one about once a month (" + started + " in 2000 days: " + kinds[0] + " of goods, " + kinds[1] + " of going out, " + kinds[2] + " genres)");

        // the words and the sources
        var keys = new List<string>(FlareCityViews.Keys);
        foreach (Spend k in FlareCityRules.TrendGoods)
        {
            keys.Add(FlareCityViews.TrendKindKey((int)k));
        }
        foreach (Spend k in FlareCityRules.TrendGoingOut)
        {
            keys.Add(FlareCityViews.TrendKindKey((int)k));
        }
        keys.AddRange(new[] { "reallife_flare_city_trend_line", "reallife_flare_city_trend_note", "reallife_flare_city_trend_note_mine", "reallife_flare_city_trend_tags" });
        Dictionary<string, string> en = LocaleFile("en");
        Check(AllIn(keys.ToArray(), out string missing) && Fits(en, "reallife_flare_city_trend_note") && Fits(en, "reallife_flare_city_trend_note_mine"),
            "flare city trends: its words in en, pt and zh-cn, its notices within 240 characters (" + missing + ")");
        string scripts = ModScripts();
        string pass = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "City", "FlareCityPass.cs"));
        string flareCity = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.City.cs"));
        string screens = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareApp.Screens.cs"));
        Check(pass.Contains("Started = FlareCityRules.CityTrend(c, s, Day);") && pass.Contains("FlareCityRules.TrendsDay(c, s, Day, PersonSlot)") && flareCity.Contains("TrendFromPost(p);")
              && screens.Contains("if (WriteCityTrends())"),
            "flare city sources: the residents' day starts and runs the trends, a viral post of the player's starts one, and Explore lists them");
    }
}
