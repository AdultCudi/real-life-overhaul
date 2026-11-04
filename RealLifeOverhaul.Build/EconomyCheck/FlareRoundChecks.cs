using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

// Round 3 of the Flare audit fix: the to-let walk and the day's round driven through a stub bridge (a day's finish never pays for the walk, a pass is
// queued at start-up and walked a slice at a time), the city ads ring over many days through the real day pass, and a Rival notice meaning a rival.
internal static partial class Program
{
    private static void FlareRoundChecks()
    {
        FlareRoundToLet();
        FlareRoundRing();
        FlareRoundWiring();
        FlareRoundFaces();
    }

    // The trend's tag comes from its key, never from its words in the player's language; a person's ring is their mood, a review's its stars.
    private static void FlareRoundFaces()
    {
        string eating = FlareCityViews.TrendTag(FlareCityViews.TrendKindKey(3)), coffee = FlareCityViews.TrendTag(FlareCityViews.TrendKindKey(4));
        Check(eating == "eatingout" && coffee == "coffee" && FlareCityViews.TrendTag(RealLifeOverhaul.Tech.StudioViews.GenreKey(2)) == "rpg" && FlareCityViews.TrendTag("reallife_other") == null && FlareCityViews.TrendTag(null) == null,
            "flare round: a trend's tag is made from its key (" + eating + ", " + coffee + "), the same in every language");
        Check(FlareFace.MoodRing(2) == UiRole.Ok && FlareFace.MoodRing(1) == UiRole.Ok && FlareFace.MoodRing(0) == UiRole.Warn && FlareFace.MoodRing(-1) == UiRole.Danger && FlareFace.MoodRing(-2) == UiRole.Danger
              && FlareFace.StarRing(5) == UiRole.Ok && FlareFace.StarRing(4) == UiRole.Ok && FlareFace.StarRing(3) == UiRole.Warn && FlareFace.StarRing(2) == UiRole.Danger && FlareFace.StarRing(1) == UiRole.Danger,
            "flare round: the ring round a face is the person's mood (the social life's, -2 to +2) and a reviewer's is how they left (their stars)");
        string pages = PlayFile("Ui", "Flare", "FlareScreen.Rows.cs") + PlayFile("Ui", "Flare", "FlareScreen.Fill.cs") + PlayFile("Ui", "Flare", "FlareScreen.Desk.cs");
        Check(pages.Contains("r.Ring = FlareFace.MoodRing(person.Mood);") && pages.Contains("r.Ring = UiTokens.Grade(inf.Reliability);") && pages.Contains("_m.MyRing = UiTokens.Grade(FlareGame.PlayerHappiness());")
              && pages.Contains("r.Ring = FlareFace.StarRing(r.Stars);") && PlayFile("Wiring", "FlareLinks.cs").Contains("Mood = p.Mood"),
            "flare round: the screen fills the rings from the social life's mood, the HUD's happiness, an influencer's record and a review's stars");
    }

    // A city of buildings the bridge walks: every fourth is to let, in one of three districts. Counts what the bridge was asked.
    private sealed class FlareToLetCity
    {
        public readonly int Buildings;
        public int Restarts, Calls, Walked, Biggest, At;
        private readonly Dictionary<string, int> _into = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _done = new Dictionary<string, int>(StringComparer.Ordinal);

        public FlareToLetCity(int buildings)
        {
            Buildings = buildings;
        }

        public Dictionary<string, int> Slice(bool restart, int most)
        {
            Calls++;
            if (restart)
            {
                Restarts++;
                At = 0;
                _into.Clear();
            }
            int end = Math.Min(Buildings, At + most);
            Biggest = Math.Max(Biggest, end - At);
            for (; At < end; At++)
            {
                Walked++;
                if (At % 4 == 0)
                {
                    string d = "ba:neighborhood_" + (At % 3);
                    _into.TryGetValue(d, out int n);
                    _into[d] = n + 1;
                }
            }
            if (At < Buildings)
            {
                return null;
            }
            _done.Clear();
            foreach (KeyValuePair<string, int> kv in _into)
            {
                _done[kv.Key] = kv.Value;
            }
            return _done;
        }

        // What a whole pass finds in one district.
        public int Expected(string district)
        {
            int n = 0;
            for (int i = 0; i < Buildings; i += 4)
            {
                n += "ba:neighborhood_" + (i % 3) == district ? 1 : 0;
            }
            return n;
        }
    }

    private static int FlareRoundMapTotal(FlareToLetWalk walk)
    {
        int total = 0;
        foreach (KeyValuePair<string, int> kv in walk.Map)
        {
            total += kv.Value;
        }
        return total;
    }

    // The day's step 7 as the director runs it (it only queues the walk), and the end after the last.
    private static void FlareRoundStep(FlareDayRound round, FlareToLetWalk walk, List<int> ran)
    {
        int step = round.Take();
        ran?.Add(step);
        if (step == 7)
        {
            walk.Queue();
        }
        else if (step >= 8)
        {
            round.End();
        }
    }

    private static void FlareRoundToLet()
    {
        var city = new FlareToLetCity(2000);
        var walk = new FlareToLetWalk();
        // Start-up queues one pass; the slices walk it, none bigger than the slice, and the map is there at the end.
        walk.Queue();
        int steps = 0;
        bool ended = false;
        while (!ended && steps < 100)
        {
            ended = walk.Step(city.Slice);
            steps++;
        }
        int expected = 0;
        foreach (string d in new[] { "ba:neighborhood_0", "ba:neighborhood_1", "ba:neighborhood_2" })
        {
            expected += city.Expected(d);
        }
        Check(ended && steps == (2000 + FlareToLetWalk.Slice - 1) / FlareToLetWalk.Slice && city.Biggest == FlareToLetWalk.Slice && city.Walked == 2000 && city.Restarts == 1
              && FlareRoundMapTotal(walk) == expected && walk.Map["ba:neighborhood_0"] == city.Expected("ba:neighborhood_0") && !walk.Wanted,
            "flare round: the pass queued at start-up is walked " + FlareToLetWalk.Slice + " buildings a step and leaves the map (" + steps + " steps, " + expected + " premises to let)");

        // The day's round: finishing it at once (a save just after midnight, a day of a time skip) plays every real step and never walks the city.
        var round = new FlareDayRound();
        var ran = new List<int>();
        walk.Queue();
        walk.Step(city.Slice);   // a pass is under way when the save comes
        int walkedMid = city.Walked, callsMid = city.Calls;
        round.Begin(30);
        round.Finish(() => FlareRoundStep(round, walk, ran));
        Check(ran.Count == 9 && ran[0] == 0 && ran[7] == 7 && ran[8] == 8 && !round.Owed && city.Calls == callsMid && city.Walked == walkedMid && walk.Wanted,
            "flare round: FinishDay with the walk under way plays the day's nine steps and asks the bridge for no slice (" + (city.Calls - callsMid) + " slices)");

        // The queue restarts the pass: the slices that come start it over and finish it, once.
        int restartsBefore = city.Restarts, walkedBefore = city.Walked;
        steps = 0;
        ended = false;
        while (!ended && steps < 100)
        {
            ended = walk.Step(city.Slice);
            steps++;
        }
        Check(ended && city.Restarts == restartsBefore + 1 && city.Walked - walkedBefore == 2000 && FlareRoundMapTotal(walk) == expected,
            "flare round: the pass that was under way starts over when the slices come and finishes (" + steps + " steps)");

        // A time skip of twenty days: each day's round is finished at once and queues; the walk is paid for once, after the last.
        int callsBefore = city.Calls;
        restartsBefore = city.Restarts;
        walkedBefore = city.Walked;
        for (int day = 31; day <= 50; day++)
        {
            round.Begin(day);
            round.Finish(() => FlareRoundStep(round, walk, null));
        }
        bool nothing = city.Calls == callsBefore && city.Walked == walkedBefore;
        steps = 0;
        ended = false;
        while (!ended && steps < 100)
        {
            ended = walk.Step(city.Slice);
            steps++;
        }
        Check(nothing && ended && city.Restarts == restartsBefore + 1 && city.Walked - walkedBefore == 2000,
            "flare round: twenty days finished at once cost no slice, and one walk follows for the last (" + (city.Walked - walkedBefore) + " buildings walked, "
            + (city.Restarts - restartsBefore) + " pass)");

        // No bridge, no pass; a stood-down Flare forgets the map and wants nothing.
        var bare = new FlareToLetWalk();
        bare.Queue();
        bool noBridge = bare.Step(null) && !bare.Wanted;
        walk.Queue();
        walk.Forget();
        Check(noBridge && !walk.Wanted && walk.Map.Count == 0 && walk.Step(city.Slice), "flare round: with no bridge or after standing down no pass is wanted and the map is empty");
        // A round whose step wants another go (the days before it) is taken again, and an ended round owes nothing.
        round.Begin(60);
        round.Take();
        round.Back(0);
        int again = round.Take();
        round.End();
        Check(again == 0 && !round.Owed && round.Take() == -1, "flare round: a step that wants another go is taken again, and an ended round owes nothing");
    }

    // A shop of the player's in the rig: a business of the city made theirs, so the rival rule has one to compete with.
    private static int FlareRoundOwn(FlareResidentRig rig, int kind)
    {
        for (int b = 0; b < rig.S.Businesses.Count; b++)
        {
            if (rig.S.Businesses.Kind[b] == (byte)kind && FlareCityRules.Sells(rig.S.Businesses, b))
            {
                rig.S.Businesses.Owner[b] = (byte)RealLifeOverhaul.Population.Owner.Player;
                return b;
            }
        }
        return -1;
    }

    private static void FlareRoundDay(FlareResidentRig rig, int d)
    {
        if (d > 0)
        {
            int day = rig.S.Day + d;
            rig.Pass.Inputs.Day = day;
            rig.Pass.Begin(rig.C, rig.S, rig.Sim.Demand, day);
            rig.Pass.RunAll();
        }
    }

    private static void FlareRoundRing()
    {
        // Twenty days through the real day pass: the ring holds at its cap, and the shared-reach line finds only today's ads.
        FlareResidentRig rig = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        int mine = FlareRoundOwn(rig, (int)Spend.EatingOut);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        bool cap = true, todays = true, shape = true, capped = false;
        int rivalNoticeDays = 0, rivalNotices = 0, flagged = 0, ads = 0;
        var file = FlareRules.Open(2, s.Day, 30, 50);
        for (int d = 0; d < 20; d++)
        {
            int day = s.Day + d;
            FlareRoundDay(rig, d);
            cap &= c.CityAds.Count <= FlareCityCatalogue.CityAdsKept;
            capped |= c.CityAds.Count == FlareCityCatalogue.CityAdsKept;
            FlareAd today = null, old = null;
            foreach (FlareAd ad in c.CityAds)
            {
                shape &= !ad.Mine && ad.From <= day && ad.Rival == FlareCityRules.Competes(s, s.BusinessOfKey(ad.Place));
                today = ad.From == day ? ad : today;
                old = ad.From < day ? ad : old;
                flagged += ad.From == day && ad.Rival ? 1 : 0;
                ads += ad.From == day ? 1 : 0;
            }
            if (today != null)
            {
                FlareAd found = FlareDeskRules.SharedWith(c.CityAds, today.District, day, "not~theirs");
                todays &= found != null && found.From == day;
            }
            if (old != null)
            {
                // a district that only an older ad was shown in finds nothing for today
                bool onlyOld = true;
                foreach (FlareAd ad in c.CityAds)
                {
                    onlyOld &= ad.From < day || ad.District != old.District;
                }
                todays &= !onlyOld || FlareDeskRules.SharedWith(c.CityAds, old.District, day, "not~theirs") == null;
            }
            var told = new List<FlareNotice>();
            FlareNoticeRules.AddDerived(file, FlareLiveRules.Now(day, 12, 0), c.CityAds, told, new List<string>(), new List<int>());
            int here = 0;
            foreach (FlareNotice n in told)
            {
                if (n.Kind == FlareNoticeKind.Rival)
                {
                    here++;
                    int b = s.BusinessOfKey(n.Who);
                    shape &= b >= 0 && FlareCityRules.Competes(s, b) && s.Businesses.Owner[b] != (byte)RealLifeOverhaul.Population.Owner.Player;
                }
            }
            shape &= here <= FlareNoticeRules.RivalMost;
            rivalNotices += here;
            rivalNoticeDays += here > 0 ? 1 : 0;
        }
        Check(mine >= 0 && cap && capped && todays && shape,
            "flare round: twenty days of the real day pass keep the city ads ring at " + FlareCityCatalogue.CityAdsKept + " and the shared-reach line finds only today's ("
            + ads + " ads made, " + flagged + " of them rivals)");
        Check(rivalNotices > 0 && flagged >= rivalNoticeDays,
            "flare round: a Rival notice is an ad by a business that competes with one of the player's shops, a few a day at most (" + rivalNotices + " in 20 days, "
            + rivalNoticeDays + " days with one)");

        // Without a shop of the player's nobody competes with them: no notice at all.
        FlareResidentRig none = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        int quiet = 0;
        for (int d = 0; d < 6; d++)
        {
            FlareRoundDay(none, d);
            var told = new List<FlareNotice>();
            FlareNoticeRules.AddDerived(file, FlareLiveRules.Now(none.S.Day + d, 12, 0), none.C.CityAds, told, new List<string>(), new List<int>());
            foreach (FlareNotice n in told)
            {
                quiet += n.Kind == FlareNoticeKind.Rival ? 1 : 0;
            }
        }
        Check(quiet == 0, "flare round: with no shop of the player's no city ad is a rival's (" + quiet + " notices in 6 days)");
        // The same save makes the same ads and rivals on a second run.
        string first = FlareRoundAdsLine(), second = FlareRoundAdsLine();
        Check(first.Length > 0 && first == second, "flare round: the city ads and which of them are rivals are the same on a second run of the same save");
    }

    private static string FlareRoundAdsLine()
    {
        FlareResidentRig twin = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        FlareRoundOwn(twin, (int)Spend.EatingOut);
        var line = new System.Text.StringBuilder();
        for (int d = 0; d < 5; d++)
        {
            FlareRoundDay(twin, d);
        }
        foreach (FlareAd ad in twin.C.CityAds)
        {
            line.Append(ad.Place).Append(ad.Rival ? "!" : ".");
        }
        return line.ToString();
    }

    // The director uses them as the checks drive them: start-up queues the pass, the day's step only queues, the finish is the round's, and the bridge is
    // called by the walk's slices only.
    private static void FlareRoundWiring()
    {
        string director = PlayFile("Social", "Flare", "FlareDirector.cs");
        string pages = PlayFile("Social", "Flare", "FlareDirector.Pages.cs");
        string links = PlayFile("Wiring", "Wiring.FlareCityLinks.cs");
        int start = director.IndexOf("private void StartUp()", StringComparison.Ordinal);
        int stand = director.IndexOf("private void StandDown(", StringComparison.Ordinal);
        string startUp = start >= 0 && stand > start ? director.Substring(start, stand - start) : string.Empty;
        Check(startUp.Contains("ToLetQueue();") && director.Contains("_round.Finish(DayStep);") && director.Contains("ToLetQueue();   // a new pass over the city's premises to let")
              && director.IndexOf("ToLetStep", StringComparison.Ordinal) < 0 && pages.Contains("_toLet.Step(FlareBridges.ToLetSlice);") && !pages.Contains("FlareBridges.ToLetSlice(")
              && links.Contains("_toLetScan.Step(most)") && pages.Contains("LoadScene.isLoading || _round.Owed || _hourSteps.Busy"),
            "flare round: start-up queues the pass, the day's step only queues it, FinishDay is the round's finish, and only the walk's slices (not in a loading, hour or day frame) call the bridge");
        string game = PlayFile("Districts", "DistrictGame.cs");
        Check(game.Contains("internal sealed class ToLetScan") && PlayFile("Districts", "ToLetScan.cs").Length == 0,
            "flare round: the scan of the game's buildings sits in DistrictGame.cs, a file of the DLC census, not a file of its own");
    }
}
