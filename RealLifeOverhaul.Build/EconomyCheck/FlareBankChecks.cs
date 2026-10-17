using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Flare;

// The bank of city photos (Flare/FlareCityPhotoRules, FlareDirector.Photos, FlareCityCamera): the city's people post photos,
// realistic ones. Held here: what the bank takes where the player is and its
// caps (a district's streets in each light and weather, a front of a business, a sight, a day's event); the clean-up (too
// old, a past event, past the most, the oldest first) and the picks that go with it; the fit of a post's photo (its
// district, kind, place, light and weather: never a sunny photo for a rainy post, never a night photo for a morning one,
// an event's of its own day), never a wrong one in a fuzz of banks and posts, none when nothing fits; a photo carried by
// at most a few posts and never twice in a row; the share by the post's kind and the option, the city's day at "some";
// the same pick on every machine of a Going Public session from the same bank; the file's round trip; and the sources:
// nothing per frame, the pass on the director's second behind the pace, the fronts read a millisecond at a time, the
// files on a worker, the sights read without a search, nothing sent.
internal static partial class Program
{
    private static void FlareBankChecks()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        BankCaps();
        BankClean();
        BankFit();
        BankNeverWrong();
        BankUses();
        BankShare();
        BankShared();
        BankFiles();
        BankSources();
        BankCost();
        Console.WriteLine("  flare photo bank: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private const string Mid = "ba:neighborhood_midtown", Gar = "ba:neighborhood_garment";

    private static FlarePhotoScene BankScene(string district, int hour, bool wet, int day = 10)
    {
        var s = new FlarePhotoScene { District = district, Light = FlareCityPhotoRules.LightAt(hour), Wet = wet, Day = day, Minute = hour * 60 + 5 };
        return s;
    }

    // Takes what the plan says until it says nothing more (or 'most' photos), as the director's pass would. How many were taken.
    private static int BankFill(FlareBank bank, FlarePhotoScene here, int most = 200)
    {
        int n = 0;
        while (n < most && FlareCityPhotoRules.Plan(bank, here, out FlarePhotoShot shot))
        {
            FlareCityPhotoRules.Taken(bank, bank.NextId, shot, here.District, here.Light, here.Wet, here.Day, here.Minute);
            n++;
        }
        return n;
    }

    private static FlareBankPhoto BankAdd(FlareBank bank, FlarePhotoKind kind, string district, int hour, bool wet, int day, string place = null, int niche = -1)
    {
        return FlareCityPhotoRules.Taken(bank, bank.NextId, new FlarePhotoShot { Kind = kind, Place = place, Niche = niche }, district, FlareCityPhotoRules.LightAt(hour), wet, day, hour * 60);
    }

    // ---------------------------------------------------------------- what the bank takes, and its caps

    private static void BankCaps()
    {
        Check(FlareCityPhotoRules.LightAt(9) == FlarePhotoLight.Day && FlareCityPhotoRules.LightAt(16) == FlarePhotoLight.Day && FlareCityPhotoRules.LightAt(6) == FlarePhotoLight.Dusk
              && FlareCityPhotoRules.LightAt(18) == FlarePhotoLight.Dusk && FlareCityPhotoRules.LightAt(21) == FlarePhotoLight.Night && FlareCityPhotoRules.LightAt(3) == FlarePhotoLight.Night
              && FlareCityPhotoRules.LightAt(-1) == FlarePhotoLight.Night && FlareCityPhotoRules.LightAt(31) == FlarePhotoLight.Day,
            "Flare photo bank: the light by the game's hour: day 7 to 17, dusk at 6 and 17 to 21, night 21 to 6");
        var bank = new FlareBank();
        FlarePhotoScene street = BankScene(Mid, 10, false);
        Check(BankFill(bank, street) == FlareCityPhotoRules.StreetMost && bank.Photos.TrueForAll(p => p.Kind == FlarePhotoKind.Street && p.District == Mid && p.Light == FlarePhotoLight.Day && !p.Wet),
            "Flare photo bank: a district's street is taken " + FlareCityPhotoRules.StreetMost + " times in one light and weather, then no more there");
        Check(BankFill(bank, BankScene(Mid, 10, true)) == FlareCityPhotoRules.StreetMost && BankFill(bank, BankScene(Mid, 22, false)) == FlareCityPhotoRules.StreetMost
              && BankFill(bank, BankScene(Gar, 10, false)) == FlareCityPhotoRules.StreetMost && BankFill(bank, BankScene(Mid, 11, false)) == 0,
            "Flare photo bank: the rain, the night and another district each want their own; the same light an hour later wants none");
        var fronts = new FlareBank();
        FlarePhotoScene shops = BankScene(Mid, 12, false);
        for (int i = 0; i < 14; i++)
        {
            shops.Fronts.Add(new FlareFrontNear { Place = "5th Avenue~" + i, District = Mid, Niche = i % 3 == 0 ? (int)FlareNiche.Food : (int)FlareNiche.Tech, Metres = 20 + i * 15, Posts = i == 9 });
        }
        shops.Fronts.Add(new FlareFrontNear { Place = "Far~1", District = Mid, Niche = (int)FlareNiche.Food, Metres = FlareCityPhotoRules.FrontRange + 1 });
        shops.Fronts.Add(new FlareFrontNear { Place = "Other~1", District = Gar, Niche = (int)FlareNiche.Food, Metres = 5 });
        FlareCityPhotoRules.Plan(fronts, shops, out FlarePhotoShot first);
        Check(first.Kind == FlarePhotoKind.Front && first.Place == "5th Avenue~9", "Flare photo bank: a business that posts today is photographed first (" + first.Place + ")");
        FlareCityPhotoRules.Taken(fronts, fronts.NextId, first, Mid, shops.Light, false, 10, 720);
        FlareCityPhotoRules.Plan(fronts, shops, out FlarePhotoShot second);
        Check(second.Kind == FlarePhotoKind.Front && second.Place == "5th Avenue~0" && second.Niche == (int)FlareNiche.Food,
            "Flare photo bank: then the kinds posts ask for most, the nearest first (a cafe before a tech shop)");
        int taken = BankFill(fronts, shops) + 1;
        int frontCount = 0;
        bool once = true, inDistrict = true;
        var seen = new HashSet<string>();
        foreach (FlareBankPhoto p in fronts.Photos)
        {
            if (p.Kind == FlarePhotoKind.Front)
            {
                frontCount++;
                once &= seen.Add(p.Place);
                inDistrict &= p.District == Mid && p.Place != "Far~1" && p.Place != "Other~1";
            }
        }
        Check(frontCount == FlareCityPhotoRules.DistrictFrontsMost && once && inDistrict && taken == frontCount + FlareCityPhotoRules.StreetMost,
            "Flare photo bank: one front of a business in a light and weather, at most " + FlareCityPhotoRules.DistrictFrontsMost + " of a district, never one out of range or of another district; then the street ("
            + frontCount + " fronts, " + taken + " photos)");
        var events = new FlareBank();
        FlarePhotoScene parade = BankScene(Mid, 14, false);
        parade.Event = "reallife_event_parade";
        parade.Sight = "times_square";
        int eventsTaken = 0;
        for (int i = 0; i < 10 && FlareCityPhotoRules.Plan(events, parade, out FlarePhotoShot e) && e.Kind == FlarePhotoKind.Event; i++)
        {
            FlareCityPhotoRules.Taken(events, events.NextId, e, Mid, parade.Light, false, parade.Day, parade.Minute);
            eventsTaken++;
        }
        FlareCityPhotoRules.Plan(events, parade, out FlarePhotoShot after);
        Check(eventsTaken == FlareCityPhotoRules.EventMost && after.Kind == FlarePhotoKind.Landmark && after.Place == "times_square",
            "Flare photo bank: the street event that is on comes first, " + FlareCityPhotoRules.EventMost + " of a day's; then the sight the player stands at");
        parade.Day++;
        Check(FlareCityPhotoRules.Plan(events, parade, out FlarePhotoShot nextDay) && nextDay.Kind == FlarePhotoKind.Event, "Flare photo bank: the next day's event is a new one");
        Check(FlareCityPhotoRules.SightKind("madison_park") == FlarePhotoKind.Park && FlareCityPhotoRules.SightKind("golf_course") == FlarePhotoKind.Park
              && FlareCityPhotoRules.SightKind("ferris_wheel") == FlarePhotoKind.Landmark && FlareCityPhotoRules.SightKind("hospital") == FlarePhotoKind.None
              && FlareCityPhotoRules.SightKind("lower_manhattan") == FlarePhotoKind.None && FlareCityPhotoRules.SightKind(null) == FlarePhotoKind.None,
            "Flare photo bank: the parks and the sights are Leisure's landmarks; a district's middle or the hospital is no sight");
        Check(!FlareCityPhotoRules.Plan(new FlareBank(), BankScene(null, 10, false), out _) && !FlareCityPhotoRules.Plan(null, street, out _),
            "Flare photo bank: nothing is taken in no district the game knows");
    }

    // ---------------------------------------------------------------- the clean-up

    private static void BankClean()
    {
        var bank = new FlareBank();
        FlareBankPhoto old = BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 0);
        FlareBankPhoto fresh = BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 1 + FlareCityPhotoRules.FreshDays);
        FlareBankPhoto ev = BankAdd(bank, FlarePhotoKind.Event, Mid, 10, false, 28, "reallife_event_market");
        FlareBankPhoto today = BankAdd(bank, FlarePhotoKind.Event, Mid, 10, false, 29, "reallife_event_market");
        var gone = new List<int>();
        FlareCityPhotoRules.Stale(bank, 29, gone);
        Check(gone.Contains(old.Id) && !gone.Contains(fresh.Id) && gone.Contains(ev.Id) && !gone.Contains(today.Id),
            "Flare photo bank: a photo older than " + FlareCityPhotoRules.FreshDays + " game days goes, and a past day's event; today's stays");
        FlareCityPhotoRules.Picked(bank, 7, ev);
        FlareCityPhotoRules.Picked(bank, 8, fresh);
        int dropped = FlareCityPhotoRules.Drop(bank, gone);
        Check(dropped == 2 && bank.Photo(old.Id) == null && bank.PickOf(7) == -2 && bank.PickOf(8) == fresh.Id,
            "Flare photo bank: a photo let go takes the picks of the posts that carried it (they show none, never a broken picture)");
        var full = new FlareBank();
        for (int i = 0; i < FlareCityPhotoRules.BankMost + 20; i++)
        {
            BankAdd(full, FlarePhotoKind.Street, i % 2 == 0 ? Mid : Gar, 10, false, 40);
        }
        FlareCityPhotoRules.Stale(full, 40, gone);
        bool oldest = gone.Count == 20;
        for (int i = 0; i < gone.Count; i++)
        {
            oldest &= gone[i] <= 20;
        }
        FlareCityPhotoRules.Drop(full, gone);
        Check(oldest && full.Photos.Count == FlareCityPhotoRules.BankMost && FlareCityPhotoRules.Stale(full, 40, gone) == 0,
            "Flare photo bank: never more than " + FlareCityPhotoRules.BankMost + " kept for a save (" + (FlareCityPhotoRules.BankMost * FlareCityPhotoRules.Width * FlareCityPhotoRules.Height * 3 / (1024 * 1024))
            + " MB); the oldest go first");
        FlareBankPhoto used = full.Photos[0];
        FlareCityPhotoRules.Picked(full, 300, used);
        FlareCityPhotoRules.Picked(full, 301, null);
        int pruned = FlareCityPhotoRules.PrunePicks(full, post => post == 301);
        Check(pruned == 1 && used.Uses == 0 && full.PickOf(301) == -1 && full.PickOf(300) == -2,
            "Flare photo bank: the picks of posts that are gone are let go and their photos' uses given back");
    }

    // ---------------------------------------------------------------- the fit

    private static FlarePost BankPost(int id, FlareAuthor author, FlareSource source, int arg, int hour, string district = Mid, string place = null, string topic = null, int day = 10)
    {
        return new FlarePost { Id = id, Author = author, Who = 2, Source = source, Arg = arg, Day = day, Hour = hour, District = district, Place = place, Topic = topic, Seed = id * 7919 + 13 };
    }

    private static FlarePost BankTalk(int id, RealLifeOverhaul.Flare.FlareTalk topic, int hour, string district = Mid, string tag = null, int day = 10)
    {
        return BankPost(id, FlareAuthor.Citizen, FlareSource.CityTalk, (int)topic * 10, hour, district, null, tag, day);
    }

    private static FlareBankPhoto BankFor(FlarePost p, FlareBank bank, bool wet, int niche = -1, string home = null)
    {
        return FlareCityPhotoRules.Wish(p, niche, home, wet, out FlarePhotoWish w) ? FlareCityPhotoRules.Pick(bank, w, p.Seed) : null;
    }

    private static void BankFit()
    {
        var bank = new FlareBank();
        FlareBankPhoto sunny = BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 10);
        FlareBankPhoto rainy = BankAdd(bank, FlarePhotoKind.Street, Mid, 10, true, 10);
        FlareBankPhoto night = BankAdd(bank, FlarePhotoKind.Street, Mid, 22, false, 10);
        FlareBankPhoto garment = BankAdd(bank, FlarePhotoKind.Street, Gar, 10, false, 10);
        FlareBankPhoto cafe = BankAdd(bank, FlarePhotoKind.Front, Mid, 10, false, 10, "5th Avenue~3", (int)FlareNiche.Food);
        FlareBankPhoto club = BankAdd(bank, FlarePhotoKind.Front, Mid, 22, false, 10, "5th Avenue~8", (int)FlareNiche.Nightlife);
        FlareBankPhoto shop = BankAdd(bank, FlarePhotoKind.Front, Mid, 10, false, 10, "5th Avenue~5", (int)FlareNiche.Fashion);
        FlareBankPhoto park = BankAdd(bank, FlarePhotoKind.Park, Mid, 10, false, 10, "madison_park");
        FlareBankPhoto parade = BankAdd(bank, FlarePhotoKind.Event, Gar, 10, false, 10, "reallife_event_parade");

        Check(BankFor(BankTalk(1, RealLifeOverhaul.Flare.FlareTalk.Weather, 10, Mid, "rain"), bank, false) == rainy
              && BankFor(BankTalk(2, RealLifeOverhaul.Flare.FlareTalk.Weather, 10, Mid, "sunshine"), bank, true) == sunny,
            "Flare photo bank: a weather post carries a street of its district in the weather it speaks of (never a sunny photo for a rainy post)");
        Check(BankFor(BankTalk(3, RealLifeOverhaul.Flare.FlareTalk.Commute, 9), bank, false) == sunny && BankFor(BankTalk(4, RealLifeOverhaul.Flare.FlareTalk.Commute, 23), bank, false) == night
              && BankFor(BankTalk(5, RealLifeOverhaul.Flare.FlareTalk.Commute, 18), bank, false) == null,
            "Flare photo bank: a morning post a day photo, a night post a night one, a dusk post none when the bank has no dusk photo");
        Check(BankFor(BankTalk(6, RealLifeOverhaul.Flare.FlareTalk.Commute, 10, Gar), bank, false) == garment && BankFor(BankTalk(7, RealLifeOverhaul.Flare.FlareTalk.Commute, 10, "ba:neighborhood_hamptons"), bank, false) == null,
            "Flare photo bank: a post carries a photo of its own district, or none");
        Check(BankFor(BankTalk(8, RealLifeOverhaul.Flare.FlareTalk.Food, 11), bank, false) == cafe && BankFor(BankTalk(9, RealLifeOverhaul.Flare.FlareTalk.Food, 11), bank, true) == null,
            "Flare photo bank: a food post a cafe's or a restaurant's front in the same light and weather, never a street or a shop");
        Check(BankFor(BankTalk(10, RealLifeOverhaul.Flare.FlareTalk.Night, 22), bank, false) == club && BankFor(BankTalk(11, RealLifeOverhaul.Flare.FlareTalk.Night, 11), bank, false) == null,
            "Flare photo bank: a night out a night spot's front at night; never the street by day");
        Check(BankFor(BankTalk(12, RealLifeOverhaul.Flare.FlareTalk.Sport, 10), bank, false) == park && BankFor(BankTalk(13, RealLifeOverhaul.Flare.FlareTalk.Event, 10, Mid), bank, false) == parade
              && BankFor(BankTalk(14, RealLifeOverhaul.Flare.FlareTalk.Event, 10, Mid, null, 11), bank, false) == null,
            "Flare photo bank: a sport post a park; an event post the day's event (wherever it was), and none on another day");
        var own = BankPost(15, FlareAuthor.Business, FlareSource.BusinessPost, 0, 10, Mid, "5th Avenue~5");
        var other = BankPost(16, FlareAuthor.Business, FlareSource.BusinessPost, 0, 10, Mid, "5th Avenue~99");
        Check(BankFor(own, bank, false) == shop && BankFor(other, bank, false) == sunny,
            "Flare photo bank: a business's own post its own front when banked, else a street of its district");
        var visit = BankPost(17, FlareAuthor.Influencer, FlareSource.Niche, 0, 11, null);
        Check(BankFor(visit, bank, false, (int)FlareNiche.Food, Mid) == cafe && BankFor(visit, bank, false, (int)FlareNiche.Tech, Mid) == null,
            "Flare photo bank: an influencer's niche post fits their niche in their home district (a food writer a cafe); a tech one carries none");
        var rival = BankPost(18, FlareAuthor.Tycoon, FlareSource.BusinessPost, 0, 10, Mid, "Unknown~1");
        Check(BankFor(rival, bank, false) == null && BankFor(BankTalk(19, RealLifeOverhaul.Flare.FlareTalk.Prices, 10), bank, false) == null
              && BankFor(BankPost(20, FlareAuthor.Citizen, FlareSource.Review, 4, 10), bank, false) == null && BankFor(BankPost(21, FlareAuthor.Official, FlareSource.Official, 0, 10), bank, false) == null,
            "Flare photo bank: a rival's building not in the bank, prices, a review and an official notice carry none");
        var reply = BankTalk(22, RealLifeOverhaul.Flare.FlareTalk.Commute, 10);
        reply.ReplyTo = 3;
        var mine = BankTalk(23, RealLifeOverhaul.Flare.FlareTalk.Commute, 10);
        mine.Author = FlareAuthor.Player;
        Check(BankFor(reply, bank, false) == null && BankFor(mine, bank, false) == null, "Flare photo bank: a reply and the player's own post never carry one");
        var stale = new FlareBank();
        BankAdd(stale, FlarePhotoKind.Street, Mid, 10, false, 10 - FlareCityPhotoRules.FreshDays - 1);
        Check(BankFor(BankTalk(24, RealLifeOverhaul.Flare.FlareTalk.Commute, 10), stale, false) == null, "Flare photo bank: a photo past " + FlareCityPhotoRules.FreshDays + " days is never used");
    }

    // ---------------------------------------------------------------- a fuzz: whatever is picked fits

    private static void BankNeverWrong()
    {
        string[] districts = { Mid, Gar, "ba:neighborhood_hamptons" };
        var rnd = new Random(77);
        int picked = 0, wrong = 0, posts = 0;
        string first = null;
        for (int round = 0; round < 60; round++)
        {
            var bank = new FlareBank();
            for (int i = 0; i < 80; i++)
            {
                var kind = (FlarePhotoKind)(1 + rnd.Next(5));
                string place = kind == FlarePhotoKind.Front ? "Main~" + rnd.Next(12) : kind == FlarePhotoKind.Park ? "sunset_park" : kind == FlarePhotoKind.Landmark ? "bridge" : kind == FlarePhotoKind.Event ? "ev" : null;
                BankAdd(bank, kind, districts[rnd.Next(3)], rnd.Next(24), rnd.Next(2) == 0, 8 + rnd.Next(5), place, kind == FlarePhotoKind.Front ? rnd.Next(7) : -1);
            }
            for (int k = 0; k < 200; k++)
            {
                posts++;
                FlarePost p;
                int niche = -1;
                switch (rnd.Next(4))
                {
                    case 0: p = BankTalk(1000 + k, (RealLifeOverhaul.Flare.FlareTalk)rnd.Next(16), rnd.Next(24), districts[rnd.Next(3)], rnd.Next(2) == 0 ? "rain" : "sunshine", 8 + rnd.Next(5)); break;
                    case 1: p = BankPost(1000 + k, FlareAuthor.Business, FlareSource.BusinessPost, rnd.Next(5), rnd.Next(24), districts[rnd.Next(3)], "Main~" + rnd.Next(14), null, 8 + rnd.Next(5)); break;
                    case 2: p = BankPost(1000 + k, FlareAuthor.Influencer, rnd.Next(2) == 0 ? FlareSource.Niche : FlareSource.Visit, 0, rnd.Next(24), null, rnd.Next(2) == 0 ? "Main~" + rnd.Next(14) : null, null, 8 + rnd.Next(5)); niche = rnd.Next(7); break;
                    default: p = BankPost(1000 + k, FlareAuthor.Tycoon, FlareSource.BusinessPost, 0, rnd.Next(24), districts[rnd.Next(3)], "Main~" + rnd.Next(14), null, 8 + rnd.Next(5)); break;
                }
                bool wet = rnd.Next(2) == 0;
                string home = districts[rnd.Next(3)];
                if (!FlareCityPhotoRules.Wish(p, niche, home, wet, out FlarePhotoWish w))
                {
                    continue;
                }
                FlareBankPhoto photo = FlareCityPhotoRules.Pick(bank, w, p.Seed);
                if (photo == null)
                {
                    continue;
                }
                picked++;
                bool fits = false;
                for (int way = 0; way < w.Ways; way++)
                {
                    fits |= FlareCityPhotoRules.Fits(photo, w, way);
                }
                bool weather = p.Author == FlareAuthor.Citizen && (RealLifeOverhaul.Flare.FlareTalk)(p.Arg / 10) == RealLifeOverhaul.Flare.FlareTalk.Weather ? photo.Wet == (p.Topic == "rain") : photo.Wet == wet;
                bool ok = fits && weather && photo.Light == FlareCityPhotoRules.LightAt(p.Hour) && (photo.Kind == FlarePhotoKind.Event ? photo.Day == p.Day : Math.Abs(photo.Day - p.Day) <= FlareCityPhotoRules.FreshDays)
                          && (photo.Kind == FlarePhotoKind.Event || photo.District == (p.District ?? home)) && photo.Uses < FlareCityPhotoRules.UsesMost;
                if (!ok)
                {
                    wrong++;
                    first ??= "post " + p.Author + "/" + p.Source + "/" + p.Arg + " at " + p.Hour + " in " + p.District + " got " + photo.Kind + " " + photo.Place + " " + photo.District + " " + photo.Light;
                }
                FlareCityPhotoRules.Picked(bank, p.Id, photo);
            }
        }
        Check(wrong == 0 && picked > 500, "Flare photo bank: in " + posts + " posts over 60 banks, every one of the " + picked + " photos picked fits its post: kind, district, light, weather, day"
                                          + (first != null ? " (wrong: " + first + ")" : ""));
    }

    // ---------------------------------------------------------------- a photo carries a few posts, never twice in a row

    private static void BankUses()
    {
        var bank = new FlareBank();
        for (int i = 0; i < 6; i++)
        {
            BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 10);
        }
        int last = -1, repeats = 0, shown = 0;
        var used = new Dictionary<int, int>();
        for (int k = 0; k < 40; k++)
        {
            FlarePost p = BankTalk(500 + k, RealLifeOverhaul.Flare.FlareTalk.Commute, 10);
            FlareBankPhoto photo = BankFor(p, bank, false);
            FlareCityPhotoRules.Picked(bank, p.Id, photo);
            if (photo == null)
            {
                continue;
            }
            shown++;
            repeats += photo.Id == last ? 1 : 0;
            last = photo.Id;
            used[photo.Id] = used.TryGetValue(photo.Id, out int n) ? n + 1 : 1;
        }
        bool few = true;
        foreach (int n in used.Values)
        {
            few &= n <= FlareCityPhotoRules.UsesMost;
        }
        Check(few && repeats == 0 && shown <= 6 * FlareCityPhotoRules.UsesMost && shown >= 6 * FlareCityPhotoRules.UsesMost - FlareCityPhotoRules.RecentMost, "Flare photo bank: a photo carries at most " + FlareCityPhotoRules.UsesMost + " posts and never two in a row ("
                                                                            + shown + " posts carried one of six; then none, never a photo worn out)");
    }

    // ---------------------------------------------------------------- the share, by kind and option

    private static void BankShare()
    {
        FlarePost food = BankTalk(1, RealLifeOverhaul.Flare.FlareTalk.Food, 10), weather = BankTalk(2, RealLifeOverhaul.Flare.FlareTalk.Weather, 10), plain = BankTalk(3, RealLifeOverhaul.Flare.FlareTalk.Everyday, 10),
            ev = BankTalk(4, RealLifeOverhaul.Flare.FlareTalk.Event, 10), night = BankTalk(5, RealLifeOverhaul.Flare.FlareTalk.Night, 10);
        Check(FlareCityPhotoRules.KindShare(food, -1) > FlareCityPhotoRules.KindShare(plain, -1) && FlareCityPhotoRules.KindShare(weather, -1) > FlareCityPhotoRules.KindShare(plain, -1)
              && FlareCityPhotoRules.KindShare(ev, -1) > FlareCityPhotoRules.KindShare(plain, -1) && FlareCityPhotoRules.KindShare(night, -1) > FlareCityPhotoRules.KindShare(plain, -1)
              && FlareCityPhotoRules.KindShare(BankTalk(6, RealLifeOverhaul.Flare.FlareTalk.Prices, 10), -1) == 0,
            "Flare photo bank: food, night life, the weather and events carry a photo more often than a plain thought; prices never");
        Check(FlareCityPhotoRules.Share(food, -1, 0) == 0 && FlareCityPhotoRules.Share(food, -1, 1) * 2 == FlareCityPhotoRules.Share(food, -1, 2) && FlareCityPhotoRules.Share(food, -1, 3) > FlareCityPhotoRules.Share(food, -1, 2)
              && FlareCityPhotoRules.Share(ev, -1, 3) <= 900 && FlareCityPhotoRules.Share(food, -1, -4) == 0 && FlareCityPhotoRules.Share(food, -1, 99) == FlareCityPhotoRules.Share(food, -1, 3)
              && FlareCityPhotoRules.Options == 4 && FlareCityPhotoRules.OptionDefault == 2,
            "Flare photo bank: the option none, a few, some (the default), many scales every kind's share; never all");
        int seed = 4242, wanted = 0;
        const int Many = 4000;
        for (int i = 0; i < Many; i++)
        {
            FlarePost p = BankTalk(i + 1, RealLifeOverhaul.Flare.FlareTalk.Food, 10);
            p.Seed = FlareRules.Roll(9, i, 1) * 1000 + i;
            wanted += FlareCityPhotoRules.Wants(p, seed, 2, -1) ? 1 : 0;
        }
        int want = FlareCityPhotoRules.Share(food, -1, 2) * Many / 1000;
        Check(Math.Abs(wanted - want) < Many / 25, "Flare photo bank: the share comes out as set over many posts (" + wanted + " of " + Many + ", wanted about " + want + ")");
        int city = 0, carry = 0, able = 0;
        for (int day = 1; day <= 30; day++)
        {
            FlareFile f = FlareLiveFile(day);
            FlareCityFacts facts = FlareFakeCity(day, 1, day % 4 == 0);
            FlareLiveRules.CityDay(f, facts, FlareCitizensOf(f, facts), null);
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                if (p.ReplyTo >= 0 || p.Author == FlareAuthor.Player)
                {
                    continue;
                }
                city++;
                able += FlareCityPhotoRules.KindShare(p, -1) > 0 ? 1 : 0;
                carry += FlareCityPhotoRules.Wants(p, FlareLiveRules.CitySeed(f), 2, -1) ? 1 : 0;
            }
        }
        int share = city > 0 ? carry * 100 / city : 0, ofAble = able > 0 ? carry * 100 / able : 0;
        Check(ofAble >= 45 && ofAble <= 75 && share >= 22 && share <= 40, "Flare photo bank: at \"some\" a realistic part of the city's posts asks for a photo: " + carry + " of " + city + " (" + share + "%), "
                                                          + ofAble + "% of the " + able + " with something to see (a photo shows where the bank has one that fits)");
    }

    // ---------------------------------------------------------------- Going Public: the same on every machine

    private static void BankShared()
    {
        bool same = true, guestNone = true;
        var hostBank = new FlareBank();
        var guestBank = new FlareBank();
        for (int i = 0; i < 9; i++)
        {
            BankAdd(hostBank, FlarePhotoKind.Street, Mid, 10, false, 10);
            BankAdd(guestBank, FlarePhotoKind.Street, Mid, 10, false, 10);
        }
        var empty = new FlareBank();
        var spread = new HashSet<int>();
        for (int i = 1; i < 300; i++)
        {
            var host = BankTalk(i, RealLifeOverhaul.Flare.FlareTalk.Commute, 10);
            host.Seed = 11 * i;
            var guest = BankTalk(5000 + i, RealLifeOverhaul.Flare.FlareTalk.Commute, 10);
            guest.NetId = i;
            guest.Shared = true;
            guest.Seed = 987 * i;
            same &= FlareCityPhotoRules.Wants(host, 4242, 2, -1) == FlareCityPhotoRules.Wants(guest, 4242, 2, -1);
            FlareCityPhotoRules.Wish(host, -1, null, false, out FlarePhotoWish hw);
            FlareCityPhotoRules.Wish(guest, -1, null, false, out FlarePhotoWish gw);
            same &= FlareCityPhotoRules.Pick(new FlareBank(), hw, 1) == null;
            FlareBankPhoto a = FlareCityPhotoRules.Pick(hostBank, hw, FlareCommentRules.Seed(host, 4242)), b = FlareCityPhotoRules.Pick(guestBank, gw, FlareCommentRules.Seed(guest, 4242));
            same &= a != null && b != null && a.Id == b.Id;
            spread.Add(a != null ? a.Id : -1);
            guestNone &= FlareCityPhotoRules.Pick(empty, gw, FlareCommentRules.Seed(guest, 4242)) == null;
        }
        Check(same && spread.Count >= 6, "Flare photo bank Going Public: the pick goes by the post's seed alone (" + spread.Count + " of the nine photos picked over 300 posts), and a host and a guest agree which shared posts carry a photo, and pick the same one from the same bank (the city's seed and the post's session number)");
        Check(guestNone, "Flare photo bank Going Public: a machine whose bank has none that fits shows none");
        string net = ReviewSource("Social/Flare/FlareNetShare.cs"), share = ReviewSource("Social/Flare/FlareDirector.Share.cs"), texts = ReviewSource("Texts/TextsShare.cs");
        Check(!net.Contains("Bank_") && !net.Contains("FlareBank") && !share.Contains("FlareBank") && !share.Contains("CityPhotoKey") && !texts.Contains("FlareBank"),
            "Flare photo bank Going Public: nothing of the bank is sent (each machine's photos stay its own)");
    }

    // ---------------------------------------------------------------- the files

    private static void BankFiles()
    {
        Check(FlareCityPhotoRules.IdOf(FlareCityPhotoRules.FileName(42) + ".thumb") == 42 && FlareCityPhotoRules.IdOf("Bank_.png.thumb") == -1 && FlareCityPhotoRules.IdOf("Bank_x.png.thumb") == -1
              && FlareCityPhotoRules.IdOf("Bank_-3.png.thumb") == -1 && FlareCityPhotoRules.IdOf("City_4.png.thumb") == -1 && FlareCityPhotoRules.IdOf(null) == -1 && FlareCityPhotoRules.IdOf("Bank_5.png") == -1,
            "Flare photo bank: a photo's file name gives its number back, and nothing else is taken for one");
        Check(FlareCityPhotoRules.Legacy("City_12.png.thumb") && !FlareCityPhotoRules.Legacy("Bank_12.png.thumb") && !FlareCityPhotoRules.Legacy(null),
            "Flare photo bank: the old photos of one post each (0.29) are known, to be deleted");
        var bank = new FlareBank();
        BankAdd(bank, FlarePhotoKind.Front, Mid, 22, true, 9, "5th|Avenue~3", (int)FlareNiche.Nightlife).Uses = 2;
        BankAdd(bank, FlarePhotoKind.Event, Gar, 6, false, 10, "reallife_event_parade");
        BankAdd(bank, FlarePhotoKind.Street, null, 12, false, 10);
        FlareCityPhotoRules.Picked(bank, 31, bank.Photos[1]);
        FlareCityPhotoRules.Picked(bank, 32, null);
        FlareBank back = FlareCityPhotoRules.Read(FlareCityPhotoRules.Write(bank));
        bool same = back.Photos.Count == 3 && back.Picks.Count == 2 && back.NextId == bank.NextId;
        for (int i = 0; same && i < 3; i++)
        {
            FlareBankPhoto a = bank.Photos[i], b = back.Photos[i];
            same &= a.Id == b.Id && a.District == b.District && a.Kind == b.Kind && (a.Place ?? string.Empty).Replace('|', '/') == (b.Place ?? string.Empty) && a.Niche == b.Niche && a.Light == b.Light
                    && a.Wet == b.Wet && a.Day == b.Day && a.Minute == b.Minute && a.Uses == b.Uses;
        }
        Check(same && back.PickOf(31) == bank.Photos[1].Id && back.PickOf(32) == -1 && back.PickOf(33) == -2,
            "Flare photo bank: its file gives back every photo's district, kind, place, light, weather, time and uses, and every post's pick");
        Check(FlareCityPhotoRules.Read("v=2\nn=9\np|1|x|1||-1|0|0|1|1|0").Photos.Count == 0 && FlareCityPhotoRules.Read("rubbish").Photos.Count == 0 && FlareCityPhotoRules.Read(null).NextId == 1
              && FlareCityPhotoRules.Read("v=1\np|0|x|1||-1|0|0|1|1|0\np|3|x|9||-1|0|0|1|1|0\np|4|x|1||-1|7|0|1|1|0\np|5|x\nu|x|y").Photos.Count == 0,
            "Flare photo bank: a later version, rubbish or a broken line gives nothing (the photos then go as files without a record), never a fault");
    }

    // ---------------------------------------------------------------- the sources

    private static void BankSources()
    {
        string photos = ReviewSource("Social/Flare/FlareDirector.Photos.cs"), rules = ReviewSource("Social/Flare/FlareCityPhotoRules.cs"), game = ReviewSource("Social/Flare/FlarePictureGame.cs"),
            links = ReviewSource("Wiring/FlareLiveLinks.cs"), director = ReviewSource("Social/Flare/FlareDirector.cs"), camera = ReviewSource("Social/Flare/FlareCityCamera.cs");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("System.Linq") && !photos.Contains("System.Linq") && !photos.Contains("void Update(") && !photos.Contains("LateUpdate"),
            "Flare photo bank sources: the rules are game-free, nothing uses LINQ, nothing runs per frame");
        string second = FlareMethod(photos, "private void PhotosSecond()");
        int paced = second.IndexOf("t < _photoNext", StringComparison.Ordinal), planned = second.IndexOf("FlareCityPhotoRules.Plan(", StringComparison.Ordinal),
            started = second.IndexOf("PhotoTake(", StringComparison.Ordinal);
        Check(director.Contains("PhotosSecond();") && paced >= 0 && planned > paced && started > planned && second.Contains("_photoBusy") && second.Contains("_photoToday >= FlareCityPhotoRules.DayMost"),
            "Flare photo bank sources: the bank's pass runs on the director's one-second timer, plans only after the pace, one photo at a time under the day's most");
        Check(FlareMethod(photos, "private void FrontsRead(").Contains("Stopwatch.Frequency * FrontsSliceMs") && FlareMethod(game, "public static bool FrontSlice(").Contains("Stopwatch.GetTimestamp() > until")
              && !game.Contains("FindObjectsByType") && !game.Contains("FindObjectsOfType"),
            "Flare photo bank sources: the businesses' fronts are read a millisecond a second, with no search of the scene");
        Check(FlareMethod(photos, "private IEnumerator BankRead(").Contains("Task.Run(") && FlareMethod(photos, "private void BankWrite()").Contains("Task.Run(")
              && FlareMethod(photos, "private void BankClean(").Contains("Task.Run(") && !FlareMethod(photos, "private void PhotosSecond()").Contains("File."),
            "Flare photo bank sources: the bank's file and its photos are read, written and deleted on a worker");
        string sight = FlareMethod(links, "private static bool SightAt(");
        Check(sight.Contains("LeisureVenues.LandmarkAt(") && !sight.Contains("FindLandmarks") && FlareMethod(links, "private static bool StreetEventAt(").Contains("EventDirector.StreetEvent("),
            "Flare photo bank sources: the parks and sights come from Leisure's list as found behind the loading screen (never a search), the event from the event director");
        Check(FlareMethod(photos, "private IEnumerator PhotoTake(").Contains("FlareCityPhotoRules.NextPace(") && camera.Contains("SessionWatch.Piece(\"flare: a photo of the city for the bank\")"),
            "Flare photo bank sources: each photo's renders are timed for the session record, and the pace doubles after a slow one");
        Check(FlareMethod(photos, "public string CityPhotoKey(").Contains("_bankFiles.Contains(id)") && FlareMethod(photos, "public string CityPhotoKey(").Contains("Settings.FlareCityPhotos <= 0"),
            "Flare photo bank sources: a post shows a photo only while its file is there, and none with the option off");
    }

    // ---------------------------------------------------------------- the cost

    private static void BankCost()
    {
        var bank = new FlareBank();
        string[] districts = { Mid, Gar, "ba:neighborhood_hamptons", "ba:neighborhood_hells_kitchen" };
        for (int i = 0; i < FlareCityPhotoRules.BankMost; i++)
        {
            BankAdd(bank, (FlarePhotoKind)(1 + i % 5), districts[i % 4], i % 24, i % 3 == 0, 10, "Main~" + (i % 30), i % 7);
        }
        FlarePost p = BankTalk(9, RealLifeOverhaul.Flare.FlareTalk.Everyday, 10);
        FlareCityPhotoRules.Wish(p, -1, null, false, out FlarePhotoWish w);
        FlarePhotoScene here = BankScene(Mid, 12, false);
        for (int i = 0; i < 16; i++)
        {
            here.Fronts.Add(new FlareFrontNear { Place = "Main~" + i, District = Mid, Niche = i % 7, Metres = 10 + i });
        }
        BankMany(bank, w, here, 10);
        long bytes = BankBytes(bank, w, here, 1000);
        double ms = BankTime(bank, w, here, 1000);
        Check(bytes < 1024, "Flare photo bank: a pick and a plan over a full bank allocate nothing (" + bytes + " bytes for 1,000 of each)");
        Check(ms < 100.0, "Flare photo bank: 1,000 picks and plans over a full bank take " + ms.ToString("0.0") + " ms (one of each every pace, twenty real seconds at least)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void BankMany(FlareBank bank, FlarePhotoWish w, FlarePhotoScene here, int n)
    {
        for (int i = 0; i < n; i++)
        {
            FlareCityPhotoRules.Pick(bank, w, i);
            FlareCityPhotoRules.Plan(bank, here, out _);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long BankBytes(FlareBank bank, FlarePhotoWish w, FlarePhotoScene here, int n)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        BankMany(bank, w, here, n);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double BankTime(FlareBank bank, FlarePhotoWish w, FlarePhotoScene here, int n)
    {
        var watch = Stopwatch.StartNew();
        BankMany(bank, w, here, n);
        return watch.Elapsed.TotalMilliseconds;
    }
}
