using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Parties;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Ui;

// Parties and company events (run alone with --parties): the rules, and every step from a booking to its end through the very code the game runs (PartyDesk, StaffDesk),
// against a world of the checks' own that keeps a ledger of every money move: the caps and rates, each refusal before any money, the exploits (a cap however the money is
// split, no money back as more, nothing kept by booking and cancelling, the investors' reserve, a guest of a session), the option off and on, a save and load between a
// booking and its event and in the middle of one, old and newer saves, a year of days, the managers, the rivals, invitations, the pages and every button, the wiring and the texts.
internal static partial class Program
{
    /// <summary>The checks' own world: what the game would answer, set by each check, and a ledger of every money move.</summary>
    private sealed class PartyWorld : IPartyWorld
    {
        public bool IsOn = true, IsGuest, IsThere = true, HomeFound = true, VenueFound = true, VenueOpen = true, VenueOwn, TextsGo = true;
        public int Minute = 100 * 1440 + 9 * 60, PriceLevel = PriceRules.Start, Sqm = 96, District = 2, ShutAnswer = 0, Messed, Asked;
        public long Cash0 = 100000, Reserve;
        public HomeFacts HomeNow = new HomeFacts { Tidy = 80 };
        public readonly List<SocialPerson> Cast = new List<SocialPerson>();
        public readonly List<string> Ledger = new List<string>(), Notes = new List<string>(), Texts = new List<string>();
        public readonly Dictionary<string, List<StaffHand>> Staffs = new Dictionary<string, List<StaffHand>>();
        public readonly Dictionary<string, float> Moods = new Dictionary<string, float>();
        public readonly HashSet<string> Managers = new HashSet<string>();
        public long Paid, Back;
        public int Changes;

        public bool On => IsOn;
        public bool Guest => IsGuest;
        public int Now => Minute;
        public int Day => Minute / 1440;
        public int Level => PriceLevel;
        public long Money => Cash0;
        public long Floor => Reserve;

        public bool Pay(int amount, string key, string place)
        {
            if (amount > Cash0)
            {
                return false;
            }
            Cash0 -= amount;
            Paid += amount;
            Ledger.Add("-" + amount + " " + key + " " + (place ?? "pocket"));
            return true;
        }

        public void Refund(int amount, string key, string place)
        {
            Cash0 += amount;
            Back += amount;
            Ledger.Add("+" + amount + " " + key + " " + (place ?? "pocket"));
        }

        public SocialPerson Person(int index) => index >= 0 && index < Cast.Count ? Cast[index] : null;
        public int People => Cast.Count;

        public void Change(SocialPerson person, float familiarity, float affection, float trust)
        {
            Changes++;
            SocialRules.Change(person, familiarity, affection, trust);
        }

        public void Remember(SocialPerson person, MemoryCode code) => person.Remember(code, Day);

        public PlaceFacts Place(PartyPlace place, string street, int number, int start, int minutes)
        {
            Asked++;
            if (place == PartyPlace.Home)
            {
                return new PlaceFacts { Found = HomeFound, Open = true, Street = "Home Road", Number = 1, Name = "Your home", Sqm = Sqm, District = District };
            }
            return new PlaceFacts { Found = VenueFound, Open = VenueOpen, Own = VenueOwn, Street = street ?? "Venue Street", Number = number > 0 ? number : 7, Name = "The Venue", Sqm = Sqm,
                District = District };
        }

        public PlaceFacts FindVenue(PartyPlace place, int start, int minutes) => Place(place, "Venue Street", 7, start, minutes);
        public HomeFacts Home() => HomeNow;
        public bool There(PartyPlace place, string street, int number) => IsThere;
        public void Mess(int points) => Messed += points;

        public int Staff(string place, List<StaffHand> into)
        {
            into.Clear();
            if (place == null || !Staffs.TryGetValue(place, out List<StaffHand> hands))
            {
                return -1;
            }
            into.AddRange(hands);
            return into.Count;
        }

        public void Mood(string id, float points) => Moods[id] = (Moods.TryGetValue(id, out float had) ? had : 0f) + points;

        public StaffFile File(string id)
        {
            foreach (List<StaffHand> hands in Staffs.Values)
            {
                foreach (StaffHand h in hands)
                {
                    if (h.Id == id)
                    {
                        return h.File;
                    }
                }
            }
            return null;
        }

        public string Business(string place) => Staffs.ContainsKey(place) ? "Shop " + place : string.Empty;
        public string Why(PartyRefusal why) => why.ToString();
        public int Shut(string place, int day, int hour, int minutes) => ShutAnswer;
        public bool Manager(string place) => Managers.Contains(place);
        public void Tell(PartyTone tone, string key, params string[] pairs) => Notes.Add(key);

        public bool Text(SocialPerson person, string key, params string[] pairs)
        {
            if (TextsGo)
            {
                Texts.Add(person.Name + ":" + key);
            }
            return TextsGo;
        }

        public string Cash(int amount) => "$" + amount;
        public string When(int day, int hour) => "day " + day + " at " + hour + ":00";

        public void At(int day, int hour, int minute = 0) => Minute = day * 1440 + hour * 60 + minute;
    }

    private static SocialPerson PartyFriend(int index, string name, SocialStage stage = SocialStage.Friend, SocialTrait traits = SocialTrait.Reliable)
    {
        return new SocialPerson
        {
            Index = index, Name = name, Met = true, MetDay = 1, Stage = stage, Familiarity = 40f, Affection = 40f, Trust = 30f, District = 2, FreeFrom = 8, FreeTo = 23, Traits = traits,
            Origin = SocialOrigin.Street
        };
    }

    private static PartyWorld PartyTown(int friends = 6)
    {
        var w = new PartyWorld();
        for (int i = 0; i < friends; i++)
        {
            w.Cast.Add(PartyFriend(i, "Friend " + i));
        }
        return w;
    }

    private static PartyAsk PartyAskOf(PartyWorld w, int day, int hour, PartyTier tier = PartyTier.Good, PartyPlace place = PartyPlace.Home, params int[] guests)
    {
        return new PartyAsk { Place = place, Tier = tier, Day = day, Hour = hour, Street = place == PartyPlace.Home ? null : "Venue Street", Number = 7,
            Guests = guests.Length > 0 ? guests : Enumerable.Range(0, w.Cast.Count).ToArray() };
    }

    // A party booked, begun at its hour and run to its end; the party as it stands afterwards.
    private static Party PartyHeld(PartyFile f, PartyWorld w, int day, int hour, PartyTier tier, params int[] guests)
    {
        w.At(day, hour - PartyCatalogue.LeadHours - 1);
        if (PartyDesk.Host(f, w, PartyAskOf(w, day, hour, tier, PartyPlace.Home, guests), out Party p) != PartyRefusal.None)
        {
            return null;
        }
        w.At(day, hour);
        PartyDesk.Begin(f, w, p.Id);
        w.Minute = p.End;
        PartyDesk.Tick(f, w);
        return p;
    }

    private static void Parties()
    {
        PartyRulesChecks();
        PartyHostChecks();
        PartyRefusalChecks();
        PartyCapChecks();
        PartyMoneyChecks();
        PartyCancelChecks();
        PartyMissChecks();
        PartyOffChecks();
        PartyInviteChecks();
        PartyStaffChecks();
        PartyManagerChecks();
        PartyRivalChecks();
        PartySaveChecks();
        PartyYearChecks();
        PartyPageChecks();
        PartyWiringChecks();
        PartyTextChecks();
    }

    // ------------------------------------------------------------------------------------------------- the rules

    private static void PartyRulesChecks()
    {
        Check(PartyRules.Stamp(3, 19) == 3 * 1440 + 19 * 60 && !PartyRules.Weekend(5) && PartyRules.Weekend(6) && PartyRules.Weekend(7) && !PartyRules.Weekend(8) && PartyRules.Weekend(13),
            "parties: a day's hour is the game's own minute, and day 6 and day 7 of each week are the weekend");
        bool inRange = true, same = true;
        var spread = new HashSet<int>();
        for (int i = 0; i < 400; i++)
        {
            int r = PartyRules.Roll(77, PartyRules.PurposeAnswer, i, i * 3);
            inRange &= r >= 0 && r < 1000;
            same &= r == PartyRules.Roll(77, PartyRules.PurposeAnswer, i, i * 3);
            spread.Add(r / 100);
        }
        Check(inRange && same && spread.Count == 10 && PartyRules.Roll(77, 1, 2, 3) != PartyRules.Roll(78, 1, 2, 3) && PartyRules.Hash("abc") == PartyRules.Hash("abc")
              && PartyRules.Hash("abc") != PartyRules.Hash("abd"), "parties: a draw is 0 to 999, the same for the same save and subject, spread over the range, and another save draws otherwise");

        // What it costs a head, from the social life's own prices: two coffees, a meal, a meal and drinks; half as much again at a venue; at cost at a venue of one's own.
        int coffee = SocialCatalogue.Outing(PlanKind.Coffee).BaseCost, meal = SocialCatalogue.Outing(PlanKind.Meal).BaseCost, drinks = SocialCatalogue.Outing(PlanKind.Drinks).BaseCost;
        Check(PartyCatalogue.HeadBase(PartyTier.Simple) == coffee * 2 && PartyCatalogue.HeadBase(PartyTier.Good) == meal && PartyCatalogue.HeadBase(PartyTier.Lavish) == meal + drinks
              && PartyRules.HeadCost(PartyTier.Good, PartyPlace.Home, false, 1000) == meal && PartyRules.HeadCost(PartyTier.Good, PartyPlace.Restaurant, false, 1000) == meal * 150 / 100
              && PartyRules.HeadCost(PartyTier.Lavish, PartyPlace.Bar, false, 1000) == (meal + drinks) * 150 / 100
              && PartyRules.HeadCost(PartyTier.Lavish, PartyPlace.Bar, true, 1000) == meal + drinks,
            "parties: a head costs two coffees, a meal, or a meal and drinks as the social life prices them, half as much again at a venue, and the goods alone at a venue of one's own");
        Check(PartyRules.HeadCost(PartyTier.Good, PartyPlace.Home, false, 2000) == PriceRules.At(meal, 2000) && PartyRules.HeadCost(PartyTier.Good, PartyPlace.Home, false, 2000) > meal
              && PartyRules.Cost(PartyTier.Good, PartyPlace.Home, false, 1000, 5) == meal * 6 && PartyRules.Cost(PartyTier.Good, PartyPlace.Home, false, 1000, 0) == meal,
            "parties: the price follows the city's price level, and a party costs a head for everybody who said yes and the host");
        Check(PartyRules.Capacity(PartyPlace.Home, 0) == PartyCatalogue.GuestsLeast && PartyRules.Capacity(PartyPlace.Home, 80) == 10 && PartyRules.Capacity(PartyPlace.Home, 5000) == 16
              && PartyRules.Capacity(PartyPlace.Restaurant, 5000) == 24 && PartyRules.Capacity(PartyPlace.Bar, 5000) == 40 && PartyRules.Capacity(PartyPlace.Bar, 8) == PartyCatalogue.GuestsLeast,
            "parties: a place holds a guest to eight square metres, four at least, and 16, 24 or 40 at most by place");
        int start = PartyRules.Stamp(50, 19);
        Check(PartyRules.RefundShare(start, start - 24 * 60) == 80 && PartyRules.RefundShare(start, start - 24 * 60 + 1) == 50 && PartyRules.RefundShare(start, start - 3 * 60) == 50
              && PartyRules.RefundShare(start, start - 3 * 60 + 1) == 0 && PartyRules.Refund(1000, start, start - 2000) == 800 && PartyRules.Refund(999, start, start - 200) == 499,
            "parties: calling off gives 80 per cent back a day or more ahead, 50 per cent later, and nothing in the last three hours");

        // Who comes, and why not: each reason from what the social life holds.
        SocialPerson p = PartyFriend(3, "Dana");
        int seed = 4242, today = 20, sat = 27, mon = 22;
        Check(PartyRules.Answer(seed, 1, p, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.Yes && PartyRules.Answer(seed, 1, null, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.NotClose,
            "parties: a reliable friend who is free comes; nobody is no guest");
        var stranger = PartyFriend(4, "Stranger");
        stranger.Met = false;
        var drifted = PartyFriend(5, "Drifted");
        drifted.Drifted = true;
        var faint = PartyFriend(6, "Faint", SocialStage.Acquaintance);
        faint.Familiarity = faint.Affection = faint.Trust = 15f;
        Check(PartyRules.Answer(seed, 1, stranger, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.NotClose && PartyRules.Answer(seed, 1, drifted, PartyPlace.Bar, sat, 19, today, 2)
            == PartyAnswer.NotClose
              && PartyRules.Answer(seed, 1, faint, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.NotClose && PartyRules.Answer(seed, 1, faint, PartyPlace.Bar, sat, 19, today, 2)
                  == PartyAnswer.Yes,
            "parties: somebody not met or drifted away does not come, and an acquaintance of closeness 15 comes to a venue (10) but not to the player's home (20)");
        var cross = PartyFriend(7, "Cross");
        cross.Remember(MemoryCode.StoodUp, today - 14);
        var over = PartyFriend(8, "Over it");
        over.Remember(MemoryCode.Rude, today - 15);
        Check(PartyRules.Answer(seed, 1, cross, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.Quarrel && PartyRules.Answer(seed, 1, over, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.Yes,
            "parties: a quarrel keeps somebody away for 14 days and no longer");
        var worker = PartyFriend(9, "Worker");
        worker.FreeFrom = 17;
        Check(PartyRules.Answer(seed, 1, worker, PartyPlace.Home, mon, 12, today, 2) == PartyAnswer.Working && PartyRules.Answer(seed, 1, worker, PartyPlace.Home, sat, 12, today, 2) == PartyAnswer.Yes
              && PartyRules.Answer(seed, 1, worker, PartyPlace.Home, mon, 18, today, 2) == PartyAnswer.Yes && PartyRules.Answer(seed, 1, p, PartyPlace.Home, mon, 23, today, 2) == PartyAnswer.Asleep,
            "parties: somebody at work at that hour does not come, on a weekend or after work they do, and nobody comes in their sleep");
        var home = PartyFriend(10, "Homebody", SocialStage.Friend, SocialTrait.Homebody | SocialTrait.Reliable);
        Check(PartyRules.Answer(seed, 1, home, PartyPlace.Bar, sat, 19, today, 5) == PartyAnswer.Far && PartyRules.Answer(seed, 1, home, PartyPlace.Bar, sat, 19, today, 2) == PartyAnswer.Yes
              && PartyRules.Answer(seed, 1, p, PartyPlace.Bar, sat, 19, today, 5) == PartyAnswer.Yes && PartyRules.Answer(seed, 1, home, PartyPlace.Bar, sat, 19, today, -1) == PartyAnswer.Yes,
            "parties: a homebody stays away from another district, a friend does not mind it, and a district nobody knows keeps nobody away");
        int rough = 0, busyFlaky = 0, busyPlain = 0, busyReliable = 0, farFaint = 0;
        var moody = PartyFriend(11, "Moody");
        moody.Mood = -2;
        var flaky = PartyFriend(12, "Flaky", SocialStage.Friend, SocialTrait.Flaky);
        var plain = PartyFriend(13, "Plain", SocialStage.Friend, SocialTrait.None);
        var acquaintance = PartyFriend(14, "Acquaintance", SocialStage.Acquaintance);
        for (int party = 1; party <= 1000; party++)
        {
            rough += PartyRules.Answer(seed, party, moody, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.RoughDay ? 1 : 0;
            busyFlaky += PartyRules.Answer(seed, party, flaky, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.Busy ? 1 : 0;
            busyPlain += PartyRules.Answer(seed, party, plain, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.Busy ? 1 : 0;
            busyReliable += PartyRules.Answer(seed, party, p, PartyPlace.Home, sat, 19, today, 2) == PartyAnswer.Busy ? 1 : 0;
            farFaint += PartyRules.Answer(seed, party, acquaintance, PartyPlace.Home, sat, 19, today, 5) == PartyAnswer.Far ? 1 : 0;
        }
        Check(rough > 430 && rough < 570 && busyFlaky > 200 && busyFlaky < 300 && busyPlain > 50 && busyPlain < 115 && busyReliable == 0 && farFaint > 240 && farFaint < 360,
            "parties: half of those on a rough day say no, a quarter of the flaky and one in twelve of the rest have something on, the reliable never, and three in ten acquaintances "
            + "find another district too far (" + rough + ", " + busyFlaky + ", " + busyPlain + ", " + busyReliable + ", " + farFaint + " of 1000)");
        int showsFlaky = 0, showsReliable = 0;
        for (int party = 1; party <= 1000; party++)
        {
            showsFlaky += PartyRules.ShowsUp(seed, party, flaky, PartyPlace.Home) ? 1 : 0;
            showsReliable += PartyRules.ShowsUp(seed, party, p, PartyPlace.Home) ? 1 : 0;
        }
        Check(showsReliable == 1000 && showsFlaky > 770 && showsFlaky < 870 && !PartyRules.ShowsUp(seed, 1, stranger, PartyPlace.Home),
            "parties: the reliable always turn up, the flaky by the odds the social life cancels a plan with (18 in 100 stay away: " + (1000 - showsFlaky) + " of 1000)");

        // How it went.
        var fine = new HomeFacts { Tidy = 80 };
        Check(PartyRules.Quality(PartyTier.Simple, PartyPlace.Bar, 4, 4, default, false, false) == 60 && PartyRules.Quality(PartyTier.Good, PartyPlace.Bar, 4, 4, default, false, false) == 75
              && PartyRules.Quality(PartyTier.Lavish, PartyPlace.Bar, 4, 4, default, false, false) == 90 && PartyRules.Quality(PartyTier.Good, PartyPlace.Bar, 3, 5, default, false, false) == 70
              && PartyRules.Quality(PartyTier.Good, PartyPlace.Bar, 2, 5, default, false, false) == 60,
            "parties: a party's quality is its tier's (55, 70, 85), five more when three in four of those who said yes came, ten less under half");
        Check(PartyRules.Quality(PartyTier.Good, PartyPlace.Home, 4, 4, fine, false, false) == 80 && PartyRules.Quality(PartyTier.Good, PartyPlace.Home, 4, 4, new HomeFacts { Tidy = 30 }, false,
            false) == 60
              && PartyRules.Quality(PartyTier.Good, PartyPlace.Home, 4, 4, new HomeFacts { Tidy = 55 }, false, false) == 75
              && PartyRules.Quality(PartyTier.Good, PartyPlace.Home, 4, 4, new HomeFacts { Tidy = 80, Dark = true }, false, false) == 55
              && PartyRules.Quality(PartyTier.Good, PartyPlace.Home, 4, 4, new HomeFacts { Tidy = 80, Dry = true }, false, false) == 70
              && PartyRules.Quality(PartyTier.Good, PartyPlace.Home, 4, 4, new HomeFacts { Tidy = -1 }, false, false) == 75
              && PartyRules.Quality(PartyTier.Good, PartyPlace.Bar, 4, 4, new HomeFacts { Tidy = 0, Dark = true }, false, false) == 75,
            "parties: at home a tidy flat adds five and a messy one takes fifteen, no power takes 25 and no water ten; a venue has none of these");
        Check(PartyRules.Quality(PartyTier.Good, PartyPlace.Bar, 4, 4, default, true, false) == 60 && PartyRules.Quality(PartyTier.Good, PartyPlace.Bar, 4, 4, default, false, true) == 55
              && PartyRules.Quality(PartyTier.Simple, PartyPlace.Home, 1, 5, new HomeFacts { Tidy = 10, Dark = true, Dry = true }, true, true) == 0
              && PartyRules.Quality(PartyTier.Lavish, PartyPlace.Home, 5, 5, fine, false, false) == 95,
            "parties: a late host takes fifteen and one gone at the end twenty, and the figure stays within 0 and 100");
        Check(PartyRules.Worth(PartyTier.Lavish, 100, p, PartyPlace.Home) == 1000 && PartyRules.Worth(PartyTier.Simple, 100, p, PartyPlace.Home) == 450
              && PartyRules.Worth(PartyTier.Good, 80, p, PartyPlace.Home) == 560 && PartyRules.Worth(PartyTier.Lavish, 39, p, PartyPlace.Home) == 0
              && PartyRules.Worth(PartyTier.Good, 80, home, PartyPlace.Home) == 728 && PartyRules.Worth(PartyTier.Lavish, 100, home, PartyPlace.Home) == 1000,
            "parties: a party is worth 450, 700 or 1000 per mille of the cap by tier at full quality, by how it went and how much it is their kind of thing, "
            + "never more than the cap, and nothing when bad");
        Check(PartyRules.Points(8f, 1000, 40f) == 8f && PartyRules.Points(8f, 500, 40f) == 4f && PartyRules.Points(8f, 1000, 66f) == 4f && PartyRules.Points(8f, 1000, 70f) == 0f
              && PartyRules.Points(8f, 1000, 90f) == 0f, "parties: the whole cap is 8 points of familiarity, half of it 4, and nothing lifts a figure past 70");
    }

    // ------------------------------------------------------------------------------------------------- a party from the booking to its end

    private static void PartyHostChecks()
    {
        var f = new PartyFile { Seed = 9001 };
        PartyWorld w = PartyTown();
        w.At(100, 9);
        long before = w.Cash0;
        HostedParty told = default;
        int tolds = 0;
        Action<HostedParty> hear = h => { told = h; tolds++; };
        PartyHosted.Held += hear;
        PartyRefusal why = PartyDesk.Host(f, w, PartyAskOf(w, 100, 19), out Party p);
        int meal = SocialCatalogue.Outing(PlanKind.Meal).BaseCost;
        Check(why == PartyRefusal.None && p != null && p.Coming == 6 && p.Paid == meal * 7 && w.Cash0 == before - meal * 7 && w.Ledger.Count == 1
              && w.Ledger[0] == "-" + (meal * 7) + " " + PartyDesk.HostKey + " pocket" && f.Parties.Count == 1 && p.State == PartyState.Planned && f.NextId == p.Id + 1,
            "party: booking pays a head for the six who said yes and the host from the player's pocket, once, and records it");
        Check(PartyDesk.CanBegin(p, w) == PartyRefusal.TooEarly, "party: the host cannot begin ten hours ahead");
        w.At(100, 18, 29);
        bool early = PartyDesk.CanBegin(p, w) == PartyRefusal.TooEarly;
        w.At(100, 18, 30);
        w.IsThere = false;
        bool away = PartyDesk.CanBegin(p, w) == PartyRefusal.NotThere && PartyDesk.Begin(f, w, p.Id) == PartyRefusal.NotThere && p.State == PartyState.Planned;
        w.IsThere = true;
        Check(early && away && PartyDesk.CanBegin(p, w) == PartyRefusal.None, "party: it can be begun from half an hour before its start, and only at the place");
        w.At(100, 18, 0);
        PartyDesk.Tick(f, w);
        Check(p.Reminded && w.Notes.Contains("reallife_party_note_soon"), "party: a reminder goes out an hour before");
        w.At(100, 19, 5);
        Check(PartyDesk.Begin(f, w, p.Id) == PartyRefusal.None && p.State == PartyState.Running && p.Began == w.Now && !p.Late && p.Came == 6
              && PartyDesk.Begin(f, w, p.Id) == PartyRefusal.Begun && PartyDesk.Cancel(f, w, p.Id) == PartyRefusal.Begun,
            "party: begun on time with all six reliable guests there; it cannot be begun twice or called off once it runs");
        w.Minute = p.End - 1;
        bool waits = !PartyDesk.Tick(f, w) && p.State == PartyState.Running && tolds == 0;
        w.Minute = p.End;
        bool ends = PartyDesk.Tick(f, w) && p.State == PartyState.Held;
        Check(waits && ends && p.Quality == 80 && w.Cash0 == before - meal * 7 && w.Ledger.Count == 1,
            "party: it takes its three hours of game time, ends at its end and not before, and no money moves after the booking");
        SocialPerson guest = w.Cast[0];
        Lift lift = f.Of(0, false);
        // Good tier at quality 80, liking 1.0: 700 x 80 / 100 = 560 per mille of the cap: 4.48 familiarity, 3.36 affection, 1.68 trust.
        Check(lift != null && PartyRules.Counted(lift, w.Day, PartyCatalogue.WindowDays) == 560 && Math.Abs(guest.Familiarity - 44.48f) < 0.01f && Math.Abs(guest.Affection - 43.36f) < 0.01f
              && Math.Abs(guest.Trust - 31.68f) < 0.01f && guest.Shared == 1 && guest.Remembers(MemoryCode.Party) && guest.LastContactDay == 100,
            "party: each guest who came gains their share of the week's cap (560 per mille: 4.48, 3.36 and 1.68 points), a thing done together, and remembers it");
        Check(w.Messed == 6 * PartyCatalogue.TidyPerGuest && tolds == 1 && told.Id == p.Id && told.Guests.Length == 6 && told.Quality == 80 && told.Cost == meal * 7 && told.Place == PartyPlace.Home,
            "party: six guests leave the home messier, and another feature hears once that the player hosted it, with who came");
        var list = new List<HostedParty>();
        Check(PartyHosted.Since(f, 100, list) == 1 && list[0].Guests.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }) && PartyHosted.Since(f, 101, null) == 0 && PartyHosted.Since(null, 0, list) == 0,
            "party: the parties held since a day can be read back, with their guests");
        PartyHosted.Held -= hear;
        // The paper: a party that ten guests or more came to is a piece the next morning; a smaller one is not.
        var big = new PartyFile { Seed = 9001 };
        PartyWorld wbig = PartyTown(12);
        wbig.Sqm = 200;
        Party crowd = PartyHeld(big, wbig, 100, 19, PartyTier.Good);
        Check(crowd != null && crowd.Came == 12 && PartyHosted.Large(big, 100) == crowd && PartyHosted.Large(big, 101) == null && PartyHosted.Large(f, 100) == null
              && PartyHosted.Large(null, 100) == null && PartyCatalogue.LargeFrom == 10,
            "party: one that ten guests or more came to is the talk of the next morning's paper, and a party of six is not");

        // A late host, a host gone at the end, a bad party, a party nobody came to.
        var f2 = new PartyFile { Seed = 9001 };
        PartyWorld w2 = PartyTown();
        w2.HomeNow = new HomeFacts { Tidy = 10, Dark = true, Dry = true };
        w2.At(100, 9);
        PartyDesk.Host(f2, w2, PartyAskOf(w2, 100, 19, PartyTier.Simple), out Party poor);
        w2.At(100, 19, 21);
        PartyDesk.Begin(f2, w2, poor.Id);
        w2.IsThere = false;
        w2.Minute = poor.End;
        PartyDesk.Tick(f2, w2);
        Check(poor.Late && poor.Left && poor.State == PartyState.Held && poor.Quality == 0 && Math.Abs(w2.Cast[0].Affection - 39f) < 0.01f && w2.Cast[0].Familiarity == 40f
              && w2.Cast[0].Shared == 0 && w2.Cast[0].Remembers(MemoryCode.PartyBad) && f2.Of(0, false) == null && w2.Notes.Contains("reallife_party_note_bad"),
            "party: a host 21 minutes late who leaves a dark, dry, messy flat gives a bad party: nothing gained, a point of affection lost with each guest, and remembered as such");
        var f3 = new PartyFile { Seed = 9001 };
        var w3 = new PartyWorld();
        w3.Cast.Add(PartyFriend(0, "Gone"));
        w3.At(100, 9);
        PartyDesk.Host(f3, w3, PartyAskOf(w3, 100, 19), out Party lonely);
        w3.Cast[0].Met = false;   // lost touch between the booking and the party: nobody turns up
        w3.At(100, 19);
        long paid = w3.Paid;
        Check(PartyDesk.Begin(f3, w3, lonely.Id) == PartyRefusal.None && lonely.State == PartyState.Empty && lonely.Came == 0 && w3.Notes.Contains("reallife_party_note_empty")
              && w3.Paid == paid && w3.Back == 0 && w3.Changes == 0 && f3.Lifts.Count == 0,
            "party: a party nobody came to does nothing and says so");
        var f4 = new PartyFile { Seed = 9001 };
        PartyWorld w4 = PartyTown();
        w4.At(100, 9);
        PartyDesk.Host(f4, w4, PartyAskOf(w4, 100, 19, PartyTier.Good, PartyPlace.Bar), out Party out4);
        w4.VenueOpen = false;
        w4.At(100, 19);
        Check(PartyDesk.Begin(f4, w4, out4.Id) == PartyRefusal.VenueShut && out4.State == PartyState.Cancelled && w4.Back == w4.Paid && out4.Paid == 0 && w4.Changes == 0
              && w4.Notes.Contains("reallife_party_note_off_place"), "party: a venue found shut on the day calls the party off with everything back, and nobody minds");
    }

    private static void PartyRefusalChecks()
    {
        var reasons = new List<string>();
        void Refused(string what, PartyRefusal want, Action<PartyWorld, PartyFile> set, Func<PartyWorld, PartyAsk> ask = null)
        {
            var f = new PartyFile { Seed = 5 };
            PartyWorld w = PartyTown();
            w.At(100, 9);
            set(w, f);
            int parties = f.Parties.Count, next = f.NextId;
            PartyRefusal why = PartyDesk.Host(f, w, ask != null ? ask(w) : PartyAskOf(w, 100, 19), out Party booked);
            if (why != want || booked != null || w.Ledger.Count != 0 || w.Paid != 0 || f.Parties.Count != parties || f.NextId != next)
            {
                reasons.Add(what + " gave " + why + (w.Ledger.Count != 0 ? " with money moved" : string.Empty));
            }
        }
        Refused("the option off", PartyRefusal.Off, (w, f) => w.IsOn = false);
        Refused("a guest of a session", PartyRefusal.Guest, (w, f) => w.IsGuest = true);
        Refused("no home", PartyRefusal.NoHome, (w, f) => w.HomeFound = false);
        Refused("no venue", PartyRefusal.NoVenue, (w, f) => w.VenueFound = false, w => PartyAskOf(w, 100, 19, PartyTier.Good, PartyPlace.Bar));
        Refused("a shut venue", PartyRefusal.VenueShut, (w, f) => w.VenueOpen = false, w => PartyAskOf(w, 100, 19, PartyTier.Good, PartyPlace.Restaurant));
        Refused("an hour too early in the day", PartyRefusal.BadHour, (w, f) => { }, w => PartyAskOf(w, 101, 9));
        Refused("an hour too late in the day", PartyRefusal.BadHour, (w, f) => { }, w => PartyAskOf(w, 101, 23));
        Refused("under three hours ahead", PartyRefusal.TooSoon, (w, f) => w.At(100, 16, 1));
        Refused("over fourteen days ahead", PartyRefusal.TooFar, (w, f) => { }, w => PartyAskOf(w, 115, 10));
        Refused("three planned already", PartyRefusal.TooMany, (w, f) =>
        {
            for (int d = 0; d < 3; d++)
            {
                f.Parties.Add(new Party { Id = f.NextId++, Day = 105 + d, Hour = 19 });
            }
        });
        Refused("a party that day already", PartyRefusal.DayTaken, (w, f) => f.Parties.Add(new Party { Id = f.NextId++, Day = 100, Hour = 12 }));
        Refused("nobody asked", PartyRefusal.NoGuests, (w, f) => w.Cast.Clear());
        Refused("more guests than the place holds", PartyRefusal.TooManyGuests, (w, f) => w.Sqm = 40);
        Refused("nobody who can come", PartyRefusal.NobodyComes, (w, f) =>
        {
            foreach (SocialPerson p in w.Cast)
            {
                p.Remember(MemoryCode.Rude, 99);
            }
        });
        Refused("not enough money", PartyRefusal.NoMoney, (w, f) => w.Cash0 = 100);
        Refused("the investors' reserve", PartyRefusal.Reserve, (w, f) => { w.Cash0 = 5000; w.Reserve = 4900; });
        Check(reasons.Count == 0, "party refusals: each of sixteen reasons refuses before any money moves or anything is recorded (" + string.Join("; ", reasons) + ")");

        // The session's guest is refused before the place is even asked for, and can begin, cancel, answer and schedule nothing.
        var f2 = new PartyFile { Seed = 5 };
        PartyWorld w2 = PartyTown();
        w2.Staffs["shop"] = new List<StaffHand> { new StaffHand { Id = "e1", Name = "Sam", Mood = 60 } };
        w2.At(100, 9);
        PartyDesk.Host(f2, w2, PartyAskOf(w2, 100, 19), out Party mine);
        StaffDesk.Book(f2, w2, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 101, Hour = 12 }, out StaffEvent ev);
        f2.Invites.Add(new Invite { Id = f2.NextId++, Person = 1, State = InviteState.Asked, Day = 100, Hour = 20 });
        int asked = w2.Asked, moves = w2.Ledger.Count;
        w2.IsGuest = true;
        w2.At(100, 19);
        bool guest = PartyDesk.Host(f2, w2, PartyAskOf(w2, 102, 19), out Party _) == PartyRefusal.Guest && w2.Asked == asked
                     && PartyDesk.Begin(f2, w2, mine.Id) == PartyRefusal.Guest && PartyDesk.Cancel(f2, w2, mine.Id) == PartyRefusal.Guest
                     && StaffDesk.Book(f2, w2, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 109, Hour = 12 }, out StaffEvent _) == PartyRefusal.Guest
                     && StaffDesk.Cancel(f2, w2, ev.Id) == PartyRefusal.Guest && StaffDesk.Schedule(f2, w2, "shop", 30, StaffEventKind.Lunch) == PartyRefusal.Guest
                     && PartyDesk.Reply(f2, w2, f2.Invites[0].Id, true) == PartyRefusal.Guest && PartyDesk.Join(f2, w2, f2.Invites[0].Id) == PartyRefusal.Guest;
        w2.Minute = PartyRules.Stamp(103, 12);
        PartyDesk.Tick(f2, w2);
        PartyDesk.Day(f2, w2, 12);
        Check(guest && w2.Ledger.Count == moves && mine.State == PartyState.Planned && ev.State == StaffEventState.Planned && f2.Invites[0].State == InviteState.Asked && w2.Changes == 0
              && f2.Plans.Count == 0 && f2.Invites.Count == 1,
            "Going Public: a guest of another player's city can book, begin, call off, answer and schedule nothing, is refused before the place is read or any money is booked, "
            + "and nothing is missed, charged or drawn while they are one");
    }

    // ------------------------------------------------------------------------------------------------- the exploits

    private static void PartyCapChecks()
    {
        // One lavish party against five simple ones in a week, the same guest at each: neither gives more than the week's cap.
        var one = new PartyFile { Seed = 31 };
        PartyWorld w1 = PartyTown(1);
        PartyHeld(one, w1, 100, 19, PartyTier.Lavish, 0);
        float lavish = w1.Cast[0].Familiarity - 40f;
        var five = new PartyFile { Seed = 31 };
        PartyWorld w5 = PartyTown(1);
        int held = 0;
        for (int d = 0; d < 5; d++)
        {
            held += PartyHeld(five, w5, 100 + d, 19, PartyTier.Simple, 0) != null ? 1 : 0;
        }
        float split = w5.Cast[0].Familiarity - 40f, splitAffection = w5.Cast[0].Affection - 40f, splitTrust = w5.Cast[0].Trust - 30f;
        Check(held == 5 && lavish > 0f && lavish <= PartyCatalogue.CapFamiliarity + 0.001f && split <= PartyCatalogue.CapFamiliarity + 0.001f && splitAffection <= PartyCatalogue.CapAffection + 0.001f
              && splitTrust <= PartyCatalogue.CapTrust + 0.001f && PartyRules.Counted(five.Of(0, false), 104, PartyCatalogue.WindowDays) == 1000,
            "the cap: one lavish party gives a guest " + lavish.ToString("0.00") + " familiarity and five simple ones in a week " + split.ToString("0.00")
                + ": neither more than the week's cap of 8, 6 and 3");
        Check(w1.Cast[0].Shared == 1 && w5.Cast[0].Shared == 0, "the cap: a party counts as a thing done together only when it gave half the cap: the lavish one does, "
                                                                 + "and none of five simple ones (292 per mille each at best)");

        // Any seven days hold no more than the cap, whatever is asked each day for a hundred days; and the cap is given again in full once the window has passed.
        var lift = new Lift { Person = 0 };
        var given = new int[130];
        bool bounded = true;
        for (int day = 0; day < 130; day++)
        {
            given[day] = PartyRules.Give(lift, day, PartyCatalogue.WindowDays, 1000 - day % 3 * 350);
            PartyRules.Prune(lift, day, PartyCatalogue.WindowDays);
            int window = 0;
            for (int back = 0; back < PartyCatalogue.WindowDays && day - back >= 0; back++)
            {
                window += given[day - back];
            }
            bounded &= window <= 1000 && lift.Gains.Count <= PartyCatalogue.WindowDays;
        }
        Check(bounded && given[0] == 1000 && given[1] == 0 && given[6] == 0 && given[7] > 0 && given.Sum() <= (130 / PartyCatalogue.WindowDays + 1) * 1000,
            "the cap: asked for all of it every day for 130 days, no seven days ever give more than the cap, and it is given again only once the week has passed");
        var back2 = new Lift { Person = 0 };
        PartyRules.Give(back2, 50, 7, 1000);
        Check(PartyRules.Give(back2, 40, 7, 500) == 0 && PartyRules.Counted(back2, 40, 7) == 1000 && PartyRules.Give(back2, 57, 7, 500) == 500,
            "the cap: a clock set back gives nothing early");
        Check(PartyRules.Faded(back2, 57, 7) == 500 && PartyRules.Faded(new Lift(), 3, 7) == 0 && PartyRules.Counted(null, 3, 7) == 0,
            "the cap: a gain is felt in full on its day and not at all a window later");

        // The ceiling: no number of parties makes a close friend by itself.
        var many = new PartyFile { Seed = 31 };
        PartyWorld wm = PartyTown(1);
        wm.Cast[0].Familiarity = 68f;
        wm.Cast[0].Affection = 69.5f;
        wm.Cast[0].Trust = 75f;
        for (int week = 0; week < 20; week++)
        {
            PartyHeld(many, wm, 100 + week * 7, 19, PartyTier.Lavish, 0);
            wm.Cast[0].LastContactDay = 100 + week * 7;
        }
        Check(wm.Cast[0].Familiarity <= PartyCatalogue.Ceiling + 0.001f && wm.Cast[0].Familiarity > 69.9f && wm.Cast[0].Affection <= PartyCatalogue.Ceiling + 0.001f && wm.Cast[0].Trust == 75f
              && wm.Cast[0].Dates == 0 && wm.Cast[0].Stage == SocialStage.Friend,
            "the cap: twenty weekly lavish parties lift nothing past 70 of 100, count as no date, and change no stage by themselves");
    }

    private static void PartyMoneyChecks()
    {
        // Every money move of a party is under its one key from the player's pocket, and what comes back is never more than what was paid.
        var f = new PartyFile { Seed = 77 };
        PartyWorld w = PartyTown();
        w.VenueOwn = true;
        w.At(100, 9);
        long before = w.Cash0;
        PartyDesk.Host(f, w, PartyAskOf(w, 100, 19, PartyTier.Lavish, PartyPlace.Bar), out Party own);
        int meal = SocialCatalogue.Outing(PlanKind.Meal).BaseCost, drinks = SocialCatalogue.Outing(PlanKind.Drinks).BaseCost;
        w.At(100, 19);
        PartyDesk.Begin(f, w, own.Id);
        w.Minute = own.End;
        PartyDesk.Tick(f, w);
        Check(own.Own && own.Paid == (meal + drinks) * 7 && w.Cash0 == before - (meal + drinks) * 7 && w.Ledger.Count == 1 && w.Ledger.All(l => l.StartsWith("-", StringComparison.Ordinal))
              && w.Back == 0,
            "no money loop: a party at a venue of the player's own costs the food and drink at cost, and nothing of it is booked back as takings there or anywhere");
        var all = new PartyFile { Seed = 78 };
        PartyWorld wa = PartyTown();
        wa.Staffs["shop"] = new List<StaffHand> { new StaffHand { Id = "e1", Name = "Sam", Mood = 60 }, new StaffHand { Id = "e2", Name = "Kim", Mood = 60 } };
        long start = wa.Cash0;
        for (int d = 0; d < 60; d++)
        {
            wa.At(100 + d, 9);
            if (PartyDesk.Host(all, wa, PartyAskOf(wa, 100 + d, 19, (PartyTier)(d % 3), d % 2 == 0 ? PartyPlace.Home : PartyPlace.Bar), out Party p) == PartyRefusal.None && d % 4 == 0)
            {
                wa.At(100 + d, 9 + d % 9);
                PartyDesk.Cancel(all, wa, p.Id);
            }
            if (StaffDesk.Book(all, wa, new StaffAsk { Place = "shop", Kind = (StaffEventKind)(d % 3), Day = 101 + d, Hour = PartyCatalogue.EventHourFrom[d % 3] }, out StaffEvent e)
                == PartyRefusal.None
                && d % 14 == 0)
            {
                StaffDesk.Cancel(all, wa, e.Id);
            }
            wa.VenueOpen = d % 7 != 3;
            wa.ShutAnswer = d % 11 == 5 ? -1 : 1;
            for (int hour = 10; hour <= 23; hour++)
            {
                wa.At(100 + d, hour);
                PartyDesk.Tick(all, wa);
                foreach (Party q in all.Parties.Where(x => x.State == PartyState.Planned && wa.Now >= x.Start && d % 5 != 2).ToList())
                {
                    PartyDesk.Begin(all, wa, q.Id);
                }
            }
            PartyDesk.Day(all, wa, 12);
        }
        long outstanding = all.Parties.Sum(p => (long)p.Paid) + all.Events.Sum(e => (long)e.Paid);
        bool keys = wa.Ledger.All(l => l.Contains(" " + PartyDesk.HostKey + " pocket") || l.Contains(" " + StaffDesk.StaffKey + " shop"));
        Check(wa.Back <= wa.Paid && wa.Cash0 == start - wa.Paid + wa.Back && wa.Cash0 <= start && keys && wa.Paid > 0 && wa.Back > 0 && outstanding <= wa.Paid - wa.Back,
            "no money loop: over sixty days of booking, cancelling, shut venues and events, the wallet only ever falls by what was paid less what came back (paid " + wa.Paid
            + ", back " + wa.Back + "), a party from the pocket and an event at its business, each under its own key");
    }

    private static void PartyCancelChecks()
    {
        // Booking and cancelling keeps nothing: no gain, no memory, no lift; and what comes back falls with the notice given.
        var f = new PartyFile { Seed = 12 };
        PartyWorld w = PartyTown();
        w.At(100, 9);
        PartyDesk.Host(f, w, PartyAskOf(w, 102, 19), out Party far);
        int paid = far.Paid;
        Check(PartyDesk.Cancel(f, w, far.Id) == PartyRefusal.None && far.State == PartyState.Cancelled && w.Back == paid * 80 / 100 && far.Paid == paid - paid * 80 / 100 && w.Changes == 0
              && f.Lifts.Count == 0 && !w.Cast[0].Remembers(MemoryCode.Party) && w.Cast[0].Familiarity == 40f && PartyDesk.Cancel(f, w, far.Id) == PartyRefusal.NotPlanned,
            "calling off: two days ahead 80 per cent comes back, nobody minds, nothing is gained or remembered, and it cannot be called off twice");
        PartyDesk.Host(f, w, PartyAskOf(w, 100, 19), out Party near);
        w.At(100, 15);
        long back = w.Back;
        PartyDesk.Cancel(f, w, near.Id);
        Check(w.Back - back == near.Paid && w.Back - back == paid * 50 / 100 && Math.Abs(w.Cast[0].Trust - 29f) < 0.01f && Math.Abs(w.Cast[0].Affection - 39f) < 0.01f && w.Cast[0].Familiarity == 40f,
            "calling off: four hours ahead half comes back and each who said yes minds a little (a point of trust and of affection)");
        w.At(101, 9);
        PartyDesk.Host(f, w, PartyAskOf(w, 101, 19), out Party last);
        w.At(101, 17);
        back = w.Back;
        PartyDesk.Cancel(f, w, last.Id);
        Check(w.Back == back && last.Paid == paid && Math.Abs(w.Cast[0].Trust - 29f - SocialRules.CancelledTrust) < 0.01f
            && Math.Abs(w.Cast[0].Affection - 39f - SocialRules.CancelledAffection) < 0.01f
              && f.Lifts.Count == 0 && w.Cast[0].Shared == 0,
            "calling off: in the last three hours nothing comes back and each who said yes minds as for any plan called off at short notice; still nothing is gained");
        long net = w.Paid - w.Back;
        Check(net > 0 && w.Cast.All(p => p.Familiarity <= 40f && p.Affection < 40f && p.Trust < 30f), "calling off: three parties booked and called off cost money and goodwill and bank nothing");
    }

    private static void PartyMissChecks()
    {
        var f = new PartyFile { Seed = 12 };
        PartyWorld w = PartyTown();
        w.At(100, 9);
        PartyDesk.Host(f, w, PartyAskOf(w, 100, 19), out Party p);
        w.At(100, 20, 15);
        bool still = !PartyDesk.Tick(f, w) && p.State == PartyState.Planned && PartyDesk.CanBegin(p, w) == PartyRefusal.None;
        w.At(100, 20, 16);
        bool tooLate = PartyDesk.CanBegin(p, w) == PartyRefusal.TooLate;
        bool missed = PartyDesk.Tick(f, w) && p.State == PartyState.Missed;
        Check(still && tooLate && missed && w.Back == 0 && p.Paid > 0 && Math.Abs(w.Cast[0].Trust - (30f + SocialRules.StoodUpTrust / 2f)) < 0.01f
              && Math.Abs(w.Cast[0].Affection - (40f + SocialRules.StoodUpAffection / 2f)) < 0.01f && w.Cast[0].Remembers(MemoryCode.StoodUp) && f.Lifts.Count == 0
              && w.Notes.Contains("reallife_party_note_missed") && PartyDesk.Begin(f, w, p.Id) == PartyRefusal.NotPlanned,
            "missing your own party: 75 minutes after the start with no host it is missed, nothing comes back, and every guest who said yes loses half of what standing one person up costs");
        // The guests who would not have come lose nothing.
        var f2 = new PartyFile { Seed = 12 };
        PartyWorld w2 = PartyTown(3);
        w2.Cast[2].Remember(MemoryCode.Rude, 99);
        float crossTrust = w2.Cast[2].Trust;
        w2.At(100, 9);
        PartyDesk.Host(f2, w2, PartyAskOf(w2, 100, 19), out Party _);
        w2.At(101, 9);
        PartyDesk.Tick(f2, w2);
        Check(w2.Cast[2].Trust == crossTrust && w2.Cast[0].Trust < 30f, "missing your own party: somebody who had said no is not stood up");
    }

    private static void PartyOffChecks()
    {
        // The option off: what is planned is kept, nothing is charged, begun or missed; back on, what came due meanwhile is called off with everything back and nobody minding.
        var f = new PartyFile { Seed = 44 };
        PartyWorld w = PartyTown();
        w.Staffs["shop"] = new List<StaffHand> { new StaffHand { Id = "e1", Name = "Sam", Mood = 60 } };
        w.Managers.Add("shop");
        w.At(100, 9);
        PartyDesk.Host(f, w, PartyAskOf(w, 100, 19), out Party soon);
        PartyDesk.Host(f, w, PartyAskOf(w, 105, 19), out Party later);
        StaffDesk.Book(f, w, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 101, Hour = 12 }, out StaffEvent lunch);
        StaffDesk.Schedule(f, w, "shop", 30, StaffEventKind.Lunch);
        f.Invites.Add(new Invite { Id = f.NextId++, Person = 2, State = InviteState.Accepted, Day = 101, Hour = 19 });
        int moves = w.Ledger.Count;
        w.IsOn = false;
        w.At(100, 12);
        PartyDesk.Tick(f, w);
        bool refused = PartyDesk.Host(f, w, PartyAskOf(w, 103, 19), out Party _) == PartyRefusal.Off && PartyDesk.Begin(f, w, soon.Id) == PartyRefusal.Off
                       && PartyDesk.Cancel(f, w, soon.Id) == PartyRefusal.Off && StaffDesk.Cancel(f, w, lunch.Id) == PartyRefusal.Off
                       && StaffDesk.Book(f, w, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 110, Hour = 12 }, out StaffEvent _) == PartyRefusal.Off;
        for (int day = 100; day <= 103; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                w.At(day, hour);
                PartyDesk.Tick(f, w);
                PartyDesk.Day(f, w, hour);
            }
        }
        Check(refused && w.Ledger.Count == moves && soon.State == PartyState.Planned && later.State == PartyState.Planned && lunch.State == StaffEventState.Planned
              && f.Invites[0].State == InviteState.Accepted && w.Changes == 0 && f.OffSince == PartyRules.Stamp(100, 12) && f.Events.Count == 1 && w.Notes.Count(n => n.Contains("missed")) == 0,
            "the option off: three days pass with nothing booked, begun, charged, refunded or missed, the managers book nothing, and everything planned is kept");
        w.IsOn = true;
        w.At(104, 9);
        long back = w.Back;
        PartyDesk.Tick(f, w);
        Check(soon.State == PartyState.Cancelled && soon.Paid == 0 && lunch.State == StaffEventState.Cancelled && lunch.Paid == 0 && w.Back - back > 0 && later.State == PartyState.Planned
              && f.Invites[0].State == InviteState.Off && w.Changes == 0 && f.OffSince == -1 && w.Notes.Contains("reallife_party_note_off_while")
              && w.Notes.Contains("reallife_party_note_event_off_while") && !w.Cast[0].Remembers(MemoryCode.StoodUp),
            "the option on again: the party and the event that came due meanwhile are called off with everything back and nobody minding, and what is still ahead resumes");
        w.At(105, 19);
        Check(PartyDesk.Begin(f, w, later.Id) == PartyRefusal.None && later.State == PartyState.Running, "the option on again: the party still ahead is held as planned");

        // A party under way when the option goes off is kept, and settled when it is on again.
        w.IsOn = false;
        w.Minute = later.End + 30;
        PartyDesk.Tick(f, w);
        bool kept = later.State == PartyState.Running;
        w.IsOn = true;
        PartyDesk.Tick(f, w);
        Check(kept && later.State == PartyState.Held, "the option off during a party: it is settled when the option is on again");
    }

    // ------------------------------------------------------------------------------------------------- other people's parties

    private static void PartyInviteChecks()
    {
        // Rare and never spamming: at most one in the gap, one open at a time, only from a friend in touch, and only when its text goes out.
        var f = new PartyFile { Seed = 2026 };
        PartyWorld w = PartyTown(8);
        w.Cast[0].Origin = SocialOrigin.Family;
        w.Cast[1].Stage = SocialStage.Acquaintance;
        w.Cast[2].Drifted = true;
        int invites = 0, lastDay = -1000, least = 1000;
        var hosts = new HashSet<int>();
        for (int day = 100; day < 100 + 730; day++)
        {
            w.At(day, 10);
            int had = f.Invites.Count(i => i.Open);
            Invite got = PartyDesk.InviteDay(f, w);
            if (got != null)
            {
                invites++;
                least = Math.Min(least, day - lastDay);
                lastDay = day;
                hosts.Add(got.Person);
                bool right = had == 0 && got.Day == day + PartyCatalogue.InviteAhead && got.Hour == PartyCatalogue.InviteHour && got.State == InviteState.Asked;
                least = right ? least : -1;
                PartyDesk.Reply(f, w, got.Id, false);
            }
        }
        Check(invites >= 20 && invites <= 60 && least >= PartyCatalogue.InviteGapDays && hosts.Count >= 3 && !hosts.Contains(0) && !hosts.Contains(1) && !hosts.Contains(2)
              && w.Texts.Count == invites && w.Texts.All(t => t.EndsWith("reallife_party_text_invite", StringComparison.Ordinal)),
            "invitations: in two years " + invites + " come, never two within ten days, each by a text of the host's own, only from friends in touch, never from family or an acquaintance");
        var f2 = new PartyFile { Seed = 2026 };
        PartyWorld w2 = PartyTown(8);
        var f3 = new PartyFile { Seed = 2026 };
        PartyWorld w3 = PartyTown(8);
        w3.TextsGo = false;
        var days2 = new List<int>();
        for (int day = 100; day < 400; day++)
        {
            w2.At(day, 10);
            w3.At(day, 10);
            Invite a = PartyDesk.InviteDay(f2, w2);
            if (a != null)
            {
                days2.Add(day * 100 + a.Person);
                a.State = InviteState.Declined;
            }
            PartyDesk.InviteDay(f3, w3);
        }
        var f2b = new PartyFile { Seed = 2026 };
        PartyWorld w2b = PartyTown(8);
        var days2b = new List<int>();
        for (int day = 100; day < 400; day++)
        {
            w2b.At(day, 10);
            Invite a = PartyDesk.InviteDay(f2b, w2b);
            if (a != null)
            {
                days2b.Add(day * 100 + a.Person);
                a.State = InviteState.Declined;
            }
        }
        Check(days2.Count > 0 && days2.SequenceEqual(days2b) && f3.Invites.Count == 0 && f3.NextId == 1,
            "invitations: the same save draws the same days and hosts, and with no text sent none is made");
        var f4 = new PartyFile { Seed = 2026 };
        PartyWorld w4 = PartyTown(8);
        w4.VenueFound = false;
        var f5 = new PartyFile { Seed = 2026 };
        PartyWorld w5 = PartyTown(8);
        f5.Invites.Add(new Invite { Id = f5.NextId++, Person = 3, State = InviteState.Asked, Day = 5000, Hour = 19 });
        for (int day = 100; day < 400; day++)
        {
            w4.At(day, 10);
            w5.At(day, 10);
            PartyDesk.InviteDay(f4, w4);
            PartyDesk.InviteDay(f5, w5);
        }
        Check(f4.Invites.Count == 0 && w4.Texts.Count == 0 && f5.Invites.Count == 1 && w5.Texts.Count == 0, "invitations: none without a place open then, and none while one is still open");

        // Going, declining, never answering, and saying yes and not coming.
        PartyWorld Town(out PartyFile file, out Invite invite, InviteState state)
        {
            file = new PartyFile { Seed = 3 };
            PartyWorld town = PartyTown(4);
            invite = new Invite { Id = file.NextId++, Person = 1, State = state, Asked = 100, Day = 102, Hour = 19, Place = PartyPlace.Restaurant, Street = "Venue Street", Number = 7,
                Name = "The Venue" };
            file.Invites.Add(invite);
            town.At(100, 12);
            return town;
        }
        PartyWorld wg = Town(out PartyFile fg, out Invite go, InviteState.Asked);
        bool said = PartyDesk.Reply(fg, wg, go.Id, true) == PartyRefusal.None && go.State == InviteState.Accepted && wg.Changes == 0 && PartyDesk.Reply(fg, wg, go.Id, false)
            == PartyRefusal.NotPlanned;
        wg.At(102, 18);
        bool early = PartyDesk.Join(fg, wg, go.Id) == PartyRefusal.TooEarly;
        wg.At(102, 19, 10);
        wg.IsThere = false;
        bool away = PartyDesk.Join(fg, wg, go.Id) == PartyRefusal.NotThere;
        wg.IsThere = true;
        bool joined = PartyDesk.Join(fg, wg, go.Id) == PartyRefusal.None && go.Began == wg.Now && PartyDesk.Join(fg, wg, go.Id) == PartyRefusal.Begun;
        wg.Minute = go.End - 1;
        PartyDesk.Tick(fg, wg);
        bool during = go.State == InviteState.Accepted;
        wg.Minute = go.End;
        PartyDesk.Tick(fg, wg);
        SocialPerson host = wg.Cast[1];
        // Going is worth 600 per mille of the cap with the host: 4.8 familiarity, 3.6 affection, 1.8 trust.
        Check(said && early && away && joined && during && go.State == InviteState.Went && Math.Abs(host.Familiarity - 44.8f) < 0.01f && Math.Abs(host.Affection - 43.6f) < 0.01f
              && Math.Abs(host.Trust - 31.8f) < 0.01f && host.Shared == 1 && host.Remembers(MemoryCode.PartyTheirs) && wg.Ledger.Count == 0
              && PartyRules.Counted(fg.Of(1, false), 102, PartyCatalogue.WindowDays) == PartyCatalogue.InviteWorth,
            "an invitation: said yes to, joined at the place in its time and stayed for, it brings the player closer to the host by 600 per mille of the same weekly cap, and costs nothing");
        PartyWorld wd = Town(out PartyFile fd, out Invite no, InviteState.Asked);
        PartyDesk.Reply(fd, wd, no.Id, false);
        PartyWorld wl = Town(out PartyFile fl, out Invite lapse, InviteState.Asked);
        wl.At(102, 19, 1);
        bool lateAnswer = PartyDesk.Reply(fl, wl, lapse.Id, true) == PartyRefusal.TooLate;
        PartyDesk.Tick(fl, wl);
        PartyWorld wn = Town(out PartyFile fn, out Invite stood, InviteState.Accepted);
        wn.At(102, 20, 16);
        PartyDesk.Tick(fn, wn);
        Check(no.State == InviteState.Declined && Math.Abs(wd.Cast[1].Affection - 39f) < 0.01f && wd.Cast[1].Trust == 30f && lateAnswer && lapse.State == InviteState.Lapsed
              && Math.Abs(wl.Cast[1].Affection - 38f) < 0.01f && stood.State == InviteState.Missed && Math.Abs(wn.Cast[1].Trust - 24f) < 0.01f && Math.Abs(wn.Cast[1].Affection - 35f) < 0.01f
              && wn.Cast[1].Remembers(MemoryCode.StoodUp) && fd.Lifts.Count == 0 && fl.Lifts.Count == 0 && fn.Lifts.Count == 0,
            "an invitation: saying no costs a point of affection, never answering two, and saying yes and not coming six of trust and five of affection, remembered as being stood up");
        PartyWorld ws = Town(out PartyFile fs, out Invite shut, InviteState.Accepted);
        ws.VenueOpen = false;
        ws.At(102, 19);
        Check(PartyDesk.Join(fs, ws, shut.Id) == PartyRefusal.VenueShut && shut.State == InviteState.Off && ws.Changes == 0,
            "an invitation: a place found shut calls their party off, and nobody minds");
        PartyWorld wf = Town(out PartyFile ff, out Invite gone, InviteState.Accepted);
        ff.Of(1, true).Gains.Add(new LiftGain { Day = 100, Amount = 300 });
        ff.Parties.Add(new Party { Id = ff.NextId++, Day = 110, Hour = 19, Guests = { new PartyGuest { Person = 1 }, new PartyGuest { Person = 2 } } });
        Check(PartyDesk.Forget(ff, 1) && ff.Invites.Count == 0 && ff.Of(1, false) == null && ff.Parties[0].Guests.Count == 1 && ff.Parties[0].Guests[0].Person == 2 && !PartyDesk.Forget(ff, 1)
              && wf.Changes == 0, "losing touch: their invitations, what parties gave them and their place on every guest list go with them, so whoever takes the place starts afresh");
    }

    // ------------------------------------------------------------------------------------------------- company events

    private static StaffFile PartyStaffFile(string id, int standing, bool underpaid)
    {
        return new StaffFile { Id = id, Place = "shop", Standing = standing, Going = 2000, Wage = underpaid ? 1500 : 2000 };
    }

    private static PartyWorld PartyShop(out List<StaffHand> hands, int staff = 4)
    {
        var w = new PartyWorld();
        hands = new List<StaffHand>();
        for (int i = 0; i < staff; i++)
        {
            hands.Add(new StaffHand { Id = "e" + i, Name = "Staff " + i, Mood = 60, File = PartyStaffFile("e" + i, 60, false) });
        }
        w.Staffs["shop"] = hands;
        w.At(100, 9);
        return w;
    }

    private static void PartyStaffChecks()
    {
        int meal = SocialCatalogue.Outing(PlanKind.Meal).BaseCost, drinks = SocialCatalogue.Outing(PlanKind.Drinks).BaseCost;
        int day0 = meal + drinks + SocialCatalogue.Outing(PlanKind.Cinema).BaseCost + SocialCatalogue.Outing(PlanKind.Workout).BaseCost;
        Check(PartyCatalogue.EventHeadBase(StaffEventKind.Lunch) == meal && PartyCatalogue.EventHeadBase(StaffEventKind.EveningOut) == meal + drinks
              && PartyCatalogue.EventHeadBase(StaffEventKind.TeamDay) == day0 && StaffDesk.Cost(StaffEventKind.Lunch, 1000, 4) == meal * 4 && StaffDesk.Cost(StaffEventKind.Lunch, 1000, 0) == 0
              && StaffDesk.Cost(StaffEventKind.TeamDay, 2000, 3) == PriceRules.At(day0, 2000) * 3,
            "staff events: a lunch costs a meal a head, an evening out a meal and drinks, a team day those and the cinema and the gym, at the city's prices, for every one of the staff");
        PartyWorld w = PartyShop(out List<StaffHand> hands);
        var f = new PartyFile { Seed = 606 };
        long before = w.Cash0;
        PartyRefusal why = StaffDesk.Book(f, w, new StaffAsk { Place = "shop", Kind = StaffEventKind.TeamDay, Day = 101, Hour = 9 }, out StaffEvent e);
        Check(why == PartyRefusal.None && e.Heads == 4 && e.Paid == day0 * 4 && w.Cash0 == before - day0 * 4 && w.Ledger.Count == 1
              && w.Ledger[0] == "-" + (day0 * 4) + " " + StaffDesk.StaffKey + " shop" && BizLines.Of(StaffDesk.StaffKey) == BizLine.Cost && BizLines.InDomain(StaffDesk.StaffKey),
            "staff events: booking pays a head for each of the staff at the business, under a key the business's statement counts as a running cost");
        w.ShutAnswer = 8;
        w.At(101, 9);
        bool begins = PartyDesk.Tick(f, w) && e.State == StaffEventState.Running && e.Shut == 8 && w.Moods.Count == 0 && hands[0].File.Standing == 60;
        w.Minute = e.End - 1;
        bool waits = !PartyDesk.Tick(f, w) && e.State == StaffEventState.Running;
        w.Minute = e.End;
        PartyDesk.Tick(f, w);
        Check(begins && waits && e.State == StaffEventState.Held && e.Came == 4 && Math.Abs(w.Moods["e0"] - PartyCatalogue.CapMood) < 0.001f && hands[0].File.Standing == 60
            + PartyCatalogue.CapStanding
              && f.Of("e0", false).Stand == PartyCatalogue.CapStanding && w.Notes.Contains("reallife_party_note_event_held"),
            "staff events: a team day shuts the business for its eight opening hours, takes its day of game time, and gives each of the staff the whole cap: 12 of satisfaction, 8 of standing");

        // The cap however the money is split, and a second event too soon.
        PartyWorld ws = PartyShop(out List<StaffHand> sh, 1);
        var fs = new PartyFile { Seed = 606 };
        ws.ShutAnswer = 1;
        int booked = 0, tooOften = 0;
        for (int d = 0; d < 30; d++)
        {
            ws.At(100 + d, 9);
            PartyRefusal r = StaffDesk.Book(fs, ws, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 101 + d, Hour = 12 }, out StaffEvent _);
            booked += r == PartyRefusal.None ? 1 : 0;
            tooOften += r == PartyRefusal.TooOften ? 1 : 0;
            for (int hour = 10; hour < 24; hour++)
            {
                ws.At(100 + d, hour);
                PartyDesk.Tick(fs, ws);
            }
            PartyDesk.Day(fs, ws, 12);
        }
        Check(booked == 5 && tooOften == 25 && ws.Moods["e0"] <= PartyCatalogue.CapMood + 0.001f && sh[0].File.Standing <= 60 + PartyCatalogue.CapStanding
              && PartyRules.Counted(fs.Of("e0", false), 129, PartyCatalogue.StaffWindowDays) <= 1000,
            "staff events: a business takes one event a week at most (5 booked, 25 refused in thirty days), and five lunches give one employee no more than the month's cap ("
            + ws.Moods["e0"].ToString("0.0") + " of 12 satisfaction)");
        var cap = new PartyFile { Seed = 1 };
        var hand = new StaffHand { Id = "x", Mood = 50, File = PartyStaffFile("x", 60, false) };
        var wc = new PartyWorld();
        int total = 0;
        for (int i = 0; i < 6; i++)
        {
            total += StaffDesk.LiftOne(cap, wc, hand, StaffEventKind.TeamDay, 100 + i);
        }
        Check(total == 1000 && Math.Abs(wc.Moods["x"] - PartyCatalogue.CapMood) < 0.001f && hand.File.Standing == 66 && cap.Of("x", false).Stand == 6 && cap.Of("x", false).Gains.Count == 1,
            "staff events: six team days in a week give what one gives, the cap, and five days on its 8 points of standing have worn to 6");

        // No substitute for pay: somebody underpaid gains half; standing never passes its most; and the standing is taken back as the lift fades.
        var fu = new PartyFile { Seed = 1 };
        var wu = new PartyWorld();
        var under = new StaffHand { Id = "u", Mood = 50, Underpaid = true, File = PartyStaffFile("u", 60, true) };
        var top = new StaffHand { Id = "t", Mood = 50, File = PartyStaffFile("t", 97, false) };
        wu.Staffs["shop"] = new List<StaffHand> { under, top };
        int half = StaffDesk.LiftOne(fu, wu, under, StaffEventKind.TeamDay, 100), full = StaffDesk.LiftOne(fu, wu, top, StaffEventKind.TeamDay, 100);
        Check(half == 500 && full == 1000 && Math.Abs(wu.Moods["u"] - 6f) < 0.001f && under.File.Standing == 64 && top.File.Standing == 100 && fu.Of("t", false).Stand == 3
              && StaffRules.Underpaid(under.File, 1) && !StaffRules.Underpaid(top.File, 1),
            "staff events: somebody underpaid gains half (6 of satisfaction, 4 of standing), and standing is lifted only as far as its most, with only that much to give back");
        var seen = new List<int>();
        for (int day = 101; day <= 131; day++)
        {
            wu.At(day, 12);
            StaffDesk.Fade(fu, wu);
            seen.Add(under.File.Standing);
        }
        Check(seen[0] <= 64 && seen[13] == 62 && seen[28] == 60 && seen[29] == 60 && under.File.Standing == 60 && top.File.Standing == 97 && fu.Lifts.Count == 0
              && seen.Zip(seen.Skip(1), (a, b) => b <= a).All(x => x),
            "staff events: the standing an event gave wears off evenly over thirty days and is all given back, never more than was given");
        var gone = new PartyFile { Seed = 1 };
        var wg = new PartyWorld();
        var leaver = new StaffHand { Id = "l", Mood = 50, File = PartyStaffFile("l", 60, false) };
        StaffDesk.LiftOne(gone, wg, leaver, StaffEventKind.TeamDay, 100);
        wg.At(140, 12);
        StaffDesk.Fade(gone, wg);
        Check(gone.Lifts.Count == 0, "staff events: the lift of somebody who has left goes with its window, with nothing to take back");

        // The working time it takes: in opening hours everybody comes and the shop is shut; after hours some have a reason, and the sick never come.
        PartyWorld wa = PartyShop(out List<StaffHand> crowd, 200);
        crowd[0] = new StaffHand { Id = "e0", Name = "Sick", Mood = 60, Sick = true, File = PartyStaffFile("e0", 60, false) };
        crowd[1] = new StaffHand { Id = "e1", Name = "Fed up", Mood = 15, File = PartyStaffFile("e1", 60, false) };
        var fa = new PartyFile { Seed = 88 };
        wa.ShutAnswer = 0;
        StaffDesk.Book(fa, wa, new StaffAsk { Place = "shop", Kind = StaffEventKind.EveningOut, Day = 100, Hour = 19 }, out StaffEvent evening);
        wa.Minute = evening.End;
        PartyDesk.Tick(fa, wa);
        PartyDesk.Tick(fa, wa);
        bool same = StaffDesk.Comes(88, evening.Id, 0, crowd[5]) == StaffDesk.Comes(88, evening.Id, 0, crowd[5]);
        Check(evening.State == StaffEventState.Held && evening.Shut == 0 && evening.Came > 140 && evening.Came < 175 && !wa.Moods.ContainsKey("e0") && !wa.Moods.ContainsKey("e1") && same
              && StaffDesk.Comes(88, 1, 3, crowd[1]) && !StaffDesk.Comes(88, 1, 3, crowd[0]),
            "staff events: after hours one in five has a reason not to come (" + evening.Came
                + " of 198 came), nor does somebody at the end of their patience; in opening hours all but the sick come");
        PartyWorld wo = PartyShop(out List<StaffHand> _);
        var fo = new PartyFile { Seed = 88 };
        wo.ShutAnswer = -1;
        StaffDesk.Book(fo, wo, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 100, Hour = 13 }, out StaffEvent open);
        wo.At(100, 13);
        PartyDesk.Tick(fo, wo);
        Check(open.State == StaffEventState.Cancelled && open.Paid == 0 && wo.Back == wo.Paid && wo.Moods.Count == 0 && wo.Notes.Contains("reallife_party_note_event_off_open"),
            "staff events: a business that is open then and cannot be shut has its event called off with everything back: no event without its working time");

        // No free events: no staff, a business no longer the player's, and calling off.
        PartyWorld wn = PartyShop(out List<StaffHand> none);
        var fn = new PartyFile { Seed = 88 };
        StaffDesk.Book(fn, wn, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 100, Hour = 13 }, out StaffEvent empty);
        StaffDesk.Book(fn, wn, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 108, Hour = 13 }, out StaffEvent sold);
        none.Clear();
        wn.At(100, 13);
        PartyDesk.Tick(fn, wn);
        wn.Staffs.Remove("shop");
        wn.At(108, 13);
        PartyDesk.Tick(fn, wn);
        Check(empty.State == StaffEventState.Empty && sold.State == StaffEventState.Cancelled && wn.Back == wn.Paid && wn.Moods.Count == 0 && fn.Lifts.Count == 0
              && wn.Notes.Contains("reallife_party_note_event_empty") && wn.Notes.Contains("reallife_party_note_event_off_gone"),
            "staff events: an event with no staff left does nothing and says so, as does one at a business no longer the player's, and the money comes back");
        var reasons = new List<string>();
        void Refused(string what, PartyRefusal want, Action<PartyWorld, PartyFile> set, StaffAsk ask)
        {
            PartyWorld town = PartyShop(out List<StaffHand> _);
            var file = new PartyFile { Seed = 1 };
            set(town, file);
            PartyRefusal got = StaffDesk.Book(file, town, ask, out StaffEvent made);
            if (got != want || made != null || town.Ledger.Count != 0 || file.Events.Count(x => x.Id > 0 && x.Paid > 0) != 0)
            {
                reasons.Add(what + " gave " + got);
            }
        }
        var lunch = new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 100, Hour = 13 };
        Refused("the option off", PartyRefusal.Off, (t, x) => t.IsOn = false, lunch);
        Refused("a guest", PartyRefusal.Guest, (t, x) => t.IsGuest = true, lunch);
        Refused("another's business", PartyRefusal.NoBusiness, (t, x) => { }, new StaffAsk { Place = "theirs", Kind = StaffEventKind.Lunch, Day = 100, Hour = 13 });
        Refused("no staff", PartyRefusal.NoStaff, (t, x) => t.Staffs["shop"].Clear(), lunch);
        Refused("a lunch at night", PartyRefusal.BadHour, (t, x) => { }, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 101, Hour = 20 });
        Refused("too soon", PartyRefusal.TooSoon, (t, x) => t.At(100, 11), lunch);
        Refused("too far ahead", PartyRefusal.TooFar, (t, x) => { }, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 120, Hour = 13 });
        Refused("within a week of another", PartyRefusal.TooOften, (t, x) => x.Events.Add(new StaffEvent { Place = "shop", State = StaffEventState.Held, Day = 94 }), lunch);
        Refused("no money", PartyRefusal.NoMoney, (t, x) => t.Cash0 = 10, lunch);
        Refused("the investors' reserve", PartyRefusal.Reserve, (t, x) => { t.Cash0 = 1000; t.Reserve = 950; }, lunch);
        Check(reasons.Count == 0, "staff event refusals: each of ten reasons refuses before any money moves (" + string.Join("; ", reasons) + ")");
        PartyWorld wr = PartyShop(out List<StaffHand> _);
        var fr = new PartyFile { Seed = 1 };
        wr.Reserve = 5000;
        wr.Cash0 = 5000 + meal * 4;
        bool atFloor = StaffDesk.Book(fr, wr, lunch, out StaffEvent ok) == PartyRefusal.None && wr.Cash0 == 5000;
        bool under2 = StaffDesk.Book(fr, wr, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 110, Hour = 13 }, out StaffEvent _) == PartyRefusal.NoMoney
                      || StaffDesk.Book(fr, wr, new StaffAsk { Place = "shop", Kind = StaffEventKind.Lunch, Day = 110, Hour = 13 }, out StaffEvent _) == PartyRefusal.Reserve;
        Check(atFloor && ok != null && under2 && wr.Cash0 == 5000, "company money: an event may take the wallet down to what it keeps for a firm's investors and not a coin under it");
        wr.At(99, 9);
        var fc = new PartyFile { Seed = 1 };
        PartyWorld wcx = PartyShop(out List<StaffHand> _);
        wcx.At(99, 9);
        StaffDesk.Book(fc, wcx, lunch, out StaffEvent call);
        Check(StaffDesk.Cancel(fc, wcx, call.Id) == PartyRefusal.None && wcx.Back == call.Heads * meal * 80 / 100 && call.State == StaffEventState.Cancelled && wcx.Moods.Count == 0
              && fc.Lifts.Count == 0 && StaffDesk.Cancel(fc, wcx, call.Id) == PartyRefusal.NotPlanned && wcx.Ledger[1].StartsWith("+", StringComparison.Ordinal) && wcx.Ledger[1].EndsWith(" shop",
                  StringComparison.Ordinal),
            "staff events: calling one off a day ahead gives 80 per cent back to the business, and nothing was gained");
    }

    private static void PartyManagerChecks()
    {
        PartyWorld w = PartyShop(out List<StaffHand> _);
        var f = new PartyFile { Seed = 9 };
        Check(StaffDesk.Schedule(f, w, "theirs", 30, StaffEventKind.Lunch) == PartyRefusal.NoBusiness && StaffDesk.Schedule(f, w, "shop", 45, StaffEventKind.Lunch) == PartyRefusal.None
              && f.Plans.Count == 0 && StaffDesk.Schedule(f, w, "shop", 30, StaffEventKind.EveningOut) == PartyRefusal.None && f.Plans.Count == 1 && f.Plans[0].Since == 100
              && w.Ledger.Count == 0, "the manager's schedule: off, or every 30, 60 or 90 days, for the player's own business, and setting it moves no money");
        w.ShutAnswer = 0;
        int byManager = 0;
        var days = new List<int>();
        for (int day = 100; day < 100 + 200; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                w.At(day, hour);
                PartyDesk.Tick(f, w);
                int had = f.Events.Count;
                PartyDesk.Day(f, w, hour);
                if (f.Events.Count > had)
                {
                    byManager += f.Events[f.Events.Count - 1].ByManager ? 1 : 0;
                    days.Add(day);
                }
            }
        }
        Check(byManager == 0 && days.Count == 0 && w.Ledger.Count == 0, "the manager's schedule: with no manager at work it waits, and nothing is booked or charged");
        w.Managers.Add("shop");
        for (int day = 300; day < 300 + 200; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                w.At(day, hour);
                PartyDesk.Tick(f, w);
                int had = f.Events.Count;
                PartyDesk.Day(f, w, hour);
                if (f.Events.Count > had)
                {
                    byManager += f.Events[f.Events.Count - 1].ByManager ? 1 : 0;
                    days.Add(day);
                }
            }
        }
        bool spaced = days.Zip(days.Skip(1), (a, b) => b - a).All(gap => gap == 30);
        Check(byManager == 7 && days.Count == 7 && days[0] == 300 && spaced && f.Events.All(e => e.State == StaffEventState.Held && e.Kind == StaffEventKind.EveningOut && e.Day
            == days[f.Events.IndexOf(e)] + 1)
              && w.Ledger.Count == 7 && w.Ledger.All(l => l.EndsWith(" " + StaffDesk.StaffKey + " shop", StringComparison.Ordinal)),
            "the manager's schedule: a manager at work books the event for the next day, by the same rules and at the business's cost, every thirty days (" + days.Count + " in 200 days)");
        w.Cash0 = 5;
        int notes = w.Notes.Count(n => n == "reallife_party_note_manager_refused");
        for (int day = 500; day < 540; day++)
        {
            w.At(day, 10);
            PartyDesk.Day(f, w, 10);
        }
        int told = w.Notes.Count(n => n == "reallife_party_note_manager_refused") - notes;
        Check(told >= 4 && told <= 6 && f.Events.Count == 7, "the manager's schedule: a manager who cannot pay books nothing and says so once a week, not every day (" + told + " in 40 days)");
        Check(StaffDesk.Schedule(f, w, "shop", 0, StaffEventKind.Lunch) == PartyRefusal.None && f.Plans.Count == 0, "the manager's schedule: switched off, it is gone");
    }

    // ------------------------------------------------------------------------------------------------- the rivals

    private static RivalFile PartyRivals(int crew, long worth, int landed)
    {
        var rivals = new RivalFile { Seed = 5 };
        rivals.Tycoons[0].ActiveDay = 1;
        rivals.Tycoons[0].Worth = worth;
        rivals.Moves.Add(new RivalMove { Id = 3, Tycoon = 0, Kind = RivalMoveKind.Outlet, Land = landed });
        var outlet = new RivalOutlet { Move = 3, Tycoon = 0, District = "ba:district_midtown", Type = "ba:businesstype_coffeeshop" };
        for (int i = 0; i < crew; i++)
        {
            outlet.Crew.Add(new StaffFile { Id = "r" + i, Name = "Crew " + i, Standing = 60, Going = 2000, Wage = i == 0 ? 1500 : 2000 });
        }
        rivals.Outlets.Add(outlet);
        return rivals;
    }

    private static void PartyRivalChecks()
    {
        // The rivals' outlets hold staff events by the same rule: paid from the tycoon's fortune, the crew's standing lifted by the same cap, half for the underpaid, fading.
        var f = new PartyFile { Seed = 321 };
        RivalFile rivals = PartyRivals(5, 1000000, 100);
        long worth = rivals.Tycoons[0].Worth, paid = 0;
        var days = new List<int>();
        int most = 0;
        for (int day = 100; day < 100 + 400; day++)
        {
            long cost = StaffDesk.RivalDay(f, rivals, day, 1000);
            paid += cost;
            if (cost > 0)
            {
                days.Add(day);
            }
            most = Math.Max(most, rivals.Outlets[0].Crew.Max(c => c.Standing));
        }
        bool spaced = days.Zip(days.Skip(1), (a, b) => b - a).All(gap => gap >= PartyCatalogue.RivalEveryDays && gap < PartyCatalogue.RivalEveryDays + PartyCatalogue.RivalSpreadDays);
        Check(days.Count >= 6 && days.Count <= 9 && days[0] >= 100 + PartyCatalogue.RivalEveryDays && spaced && rivals.Tycoons[0].Worth == worth - paid && paid > 0
              && f.Rivals.Count == days.Count && f.Rivals.All(r => r.Outlet == RivalFirms.Key(rivals.Outlets[0]) && r.Cost == StaffDesk.Cost(r.Kind, 1000, 5) && r.Heads <= 5),
            "rivals: an outlet holds a staff event every 45 to 59 days (" + days.Count + " in 400 days), each at the player's own price a head, paid from its tycoon's fortune");
        Check(most <= 60 + PartyCatalogue.CapStanding && most > 60 && rivals.Outlets[0].Crew.All(c => c.Standing >= 60 && c.Standing <= 60 + PartyCatalogue.CapStanding),
            "rivals: their crew's standing is lifted by the same cap (" + most + " at most from 60), and never past it");
        var f2 = new PartyFile { Seed = 321 };
        RivalFile again = PartyRivals(5, 1000000, 100);
        for (int day = 100; day < 500; day++)
        {
            StaffDesk.RivalDay(f2, again, day, 1000);
        }
        Check(PartyStore.Write(f) == PartyStore.Write(f2) && again.Tycoons[0].Worth == rivals.Tycoons[0].Worth, "rivals: the same save gives the same events on every machine");
        // The underpaid crew member gains half; a team day gives the others the whole cap, and it fades back.
        var f3 = new PartyFile { Seed = 321 };
        RivalFile one = PartyRivals(3, 1000000, 100);
        int eventDay = -1;
        for (int day = 100; day < 200 && eventDay < 0; day++)
        {
            eventDay = StaffDesk.RivalDay(f3, one, day, 1000) > 0 ? day : -1;
        }
        RivalEvent held = f3.Rivals[0];
        int full = PartyCatalogue.CapStanding * PartyCatalogue.EventWorth[(int)held.Kind] / 1000, halfLift = PartyCatalogue.CapStanding * (PartyCatalogue.EventWorth[(int)held.Kind] / 2) / 1000;
        StaffFile underpaid = one.Outlets[0].Crew[0], fair = one.Outlets[0].Crew[1];
        bool cameUnder = f3.Of(StaffDesk.RivalId(held.Outlet, "r0"), false) != null, cameFair = f3.Of(StaffDesk.RivalId(held.Outlet, "r1"), false) != null;
        Check(eventDay > 0 && (!cameUnder || underpaid.Standing == 60 + halfLift) && (!cameFair || fair.Standing == 60 + full) && (cameUnder || cameFair),
            "rivals: on the day of the event the crew gain by its kind, the underpaid one half (" + held.Kind + ": " + fair.Standing + " and " + underpaid.Standing + ")");
        for (int day = eventDay + 1; day <= eventDay + PartyCatalogue.StaffWindowDays + 1; day++)
        {
            StaffDesk.RivalDay(f3, one, day, 1000);
        }
        Check(underpaid.Standing == 60 && fair.Standing == 60 && !f3.Lifts.Any(l => l.Id.StartsWith("r/", StringComparison.Ordinal) && l.Gains.Count == 0 && l.Stand > 0),
            "rivals: the crew's lift fades back over thirty days, as the player's staff's does");
        // A tycoon who cannot pay, one out of play, an outlet with no crew, and an outlet that closed.
        var f4 = new PartyFile { Seed = 321 };
        RivalFile poor = PartyRivals(5, 10, 100), outOfPlay = PartyRivals(5, 1000000, 100), bare = PartyRivals(0, 1000000, 100);
        outOfPlay.Tycoons[0].DefeatedDay = 50;
        long none = 0;
        for (int day = 100; day < 400; day++)
        {
            none += StaffDesk.RivalDay(f4, poor, day, 1000) + StaffDesk.RivalDay(f4, outOfPlay, day, 1000) + StaffDesk.RivalDay(f4, bare, day, 1000) + StaffDesk.RivalDay(f4, null, day, 1000);
        }
        Check(none == 0 && f4.Rivals.Count == 0 && poor.Tycoons[0].Worth == 10 && f4.Lifts.Count == 0, "rivals: a tycoon who cannot pay, one out of play and an outlet with no crew hold none");
        rivals.Outlets.Clear();
        StaffDesk.RivalDay(f, rivals, 600, 1000);
        Check(f.Rivals.Count == 0 && !f.Lifts.Any(l => l.Id.StartsWith("r/", StringComparison.Ordinal)), "rivals: an outlet that closed takes its events and its crew's lifts with it");
        var crowd = new PartyFile { Seed = 321 };
        var manyOutlets = new RivalFile { Seed = 5 };
        manyOutlets.Tycoons[0].ActiveDay = 1;
        manyOutlets.Tycoons[0].Worth = 100000000;
        for (int o = 0; o < 6; o++)
        {
            manyOutlets.Moves.Add(new RivalMove { Id = 10 + o, Tycoon = 0, Kind = RivalMoveKind.Outlet, Land = 100 });
            var outlet = new RivalOutlet { Move = 10 + o, Tycoon = 0 };
            outlet.Crew.Add(new StaffFile { Id = "c" + o, Standing = 60, Going = 2000, Wage = 2000 });
            manyOutlets.Outlets.Add(outlet);
        }
        for (int day = 100; day < 1200; day++)
        {
            StaffDesk.RivalDay(crowd, manyOutlets, day, 1000);
        }
        Check(crowd.Rivals.Count <= PartyCatalogue.RivalLogMost && manyOutlets.Outlets.All(o => crowd.Rivals.Any(r => r.Outlet == RivalFirms.Key(o))),
            "rivals: the log stays bounded over three years of six outlets, and always keeps each outlet's newest event");
    }

    // ------------------------------------------------------------------------------------------------- the save

    private static void PartySaveChecks()
    {
        Check(PartyStore.Key == "reallife:parties" && PartyStore.Load(new Dictionary<string, string>(), out int none) == null && none == 0 && PartyStore.Load(null, out none) == null
              && PartyStore.Read(null, out none) == null, "the save: one line, reallife:parties, and a save from before has none and starts with nothing");
        var f = new PartyFile { Seed = 123456, Settled = 140, LastInvite = 133, OffSince = PartyRules.Stamp(139, 4) };
        PartyWorld w = PartyTown();
        w.Staffs["Main St~3"] = new List<StaffHand> { new StaffHand { Id = "e|1#x", Name = "Sam", Mood = 60, File = PartyStaffFile("e|1#x", 60, false) } };
        w.At(140, 9);
        PartyDesk.Host(f, w, PartyAskOf(w, 140, 19, PartyTier.Lavish, PartyPlace.Bar), out Party planned);
        planned.Name = "Odd, name: 50% #1 ^ | venue";
        PartyDesk.Host(f, w, PartyAskOf(w, 142, 20), out Party second);
        StaffDesk.Book(f, w, new StaffAsk { Place = "Main St~3", Kind = StaffEventKind.TeamDay, Day = 141, Hour = 9 }, out StaffEvent ev);
        StaffDesk.Schedule(f, w, "Main St~3", 60, StaffEventKind.EveningOut);
        f.Invites.Add(new Invite { Id = f.NextId++, Person = 4, State = InviteState.Accepted, Asked = 139, Day = 141, Hour = 19, Place = PartyPlace.Bar, Street = "Venue Street", Number = 7,
            Name = "Bar, The", Began = 5, Reminded = true });
        f.Rivals.Add(new RivalEvent { Day = 138, Tycoon = 1, Outlet = "rival1#4", District = "ba:d", Kind = StaffEventKind.TeamDay, Heads = 4, Cost = 452 });
        f.Of(3, true).Gains.Add(new LiftGain { Day = 137, Amount = 450 });
        Lift staffLift = f.Of("r/rival1#4/x:1", true);
        staffLift.Gains.Add(new LiftGain { Day = 120, Amount = 330 });
        staffLift.Gains.Add(new LiftGain { Day = 138, Amount = 580 });
        staffLift.Stand = 6;
        string line = PartyStore.Write(f);
        PartyFile back = PartyStore.Read(line, out int version);
        Check(version == PartyStore.Version && back != null && PartyStore.Write(back) == line && back.Seed == 123456 && back.NextId == f.NextId && back.Settled == 140 && back.LastInvite == 133
              && back.OffSince == f.OffSince && back.Parties.Count == 2 && back.Find(planned.Id).Name == planned.Name && back.Find(planned.Id).Guests.Count == 6
              && back.Find(planned.Id).Paid == planned.Paid && back.Find(second.Id).Hour == 20 && back.Event(ev.Id).Place == "Main St~3" && back.Event(ev.Id).Paid == ev.Paid
              && back.Plan("Main St~3", false).Every == 60 && back.Plan("Main St~3", false).Kind == StaffEventKind.EveningOut && back.Invites[0].Began == 5 && back.Invites[0].Name == "Bar, The"
              && back.Rivals[0].Cost == 452 && back.Of(3, false).Gains[0].Amount == 450 && back.Of("r/rival1#4/x:1", false).Gains.Count == 2 && back.Of("r/rival1#4/x:1", false).Stand == 6,
            "the save: parties with their guests, events, a schedule, an invitation, lifts and the rivals' events read back to the very line, with every mark in a name escaped");

        // Between the booking and the event, and in the middle of one: the loaded file carries on exactly as the one that never left memory.
        var live = new PartyFile { Seed = 55 };
        PartyWorld wl = PartyTown();
        wl.Staffs["shop"] = new List<StaffHand> { new StaffHand { Id = "e1", Name = "Sam", Mood = 60, File = PartyStaffFile("e1", 60, false) } };
        wl.ShutAnswer = 2;
        wl.At(100, 9);
        PartyDesk.Host(live, wl, PartyAskOf(wl, 100, 19), out Party p);
        StaffDesk.Book(live, wl, new StaffAsk { Place = "shop", Kind = StaffEventKind.EveningOut, Day = 100, Hour = 18 }, out StaffEvent e);
        PartyFile loaded = PartyStore.Read(PartyStore.Write(live), out _);   // saved and loaded between the booking and the event
        PartyWorld wb = PartyTown();
        wb.Staffs["shop"] = new List<StaffHand> { new StaffHand { Id = "e1", Name = "Sam", Mood = 60, File = PartyStaffFile("e1", 60, false) } };
        wb.ShutAnswer = 2;
        wb.Cash0 = wl.Cash0;
        foreach (int minute in new[] { PartyRules.Stamp(100, 18), PartyRules.Stamp(100, 19) })
        {
            wl.Minute = wb.Minute = minute;
            PartyDesk.Tick(live, wl);
            PartyDesk.Tick(loaded, wb);
        }
        PartyDesk.Begin(live, wl, p.Id);
        PartyDesk.Begin(loaded, wb, p.Id);
        bool midSame = PartyStore.Write(live) == PartyStore.Write(loaded) && loaded.Find(p.Id).State == PartyState.Running && loaded.Event(e.Id).State == StaffEventState.Running;
        loaded = PartyStore.Read(PartyStore.Write(loaded), out _);   // saved and loaded in the middle of both
        wl.Minute = wb.Minute = PartyRules.Stamp(100, 22);
        PartyDesk.Tick(live, wl);
        PartyDesk.Tick(loaded, wb);
        Check(midSame && PartyStore.Write(live) == PartyStore.Write(loaded) && loaded.Find(p.Id).State == PartyState.Held && loaded.Event(e.Id).State == StaffEventState.Held
              && loaded.Event(e.Id).Shut == 2 && wb.Cast[0].Familiarity == wl.Cast[0].Familiarity && wb.Cast[0].Familiarity > 40f && wb.Moods["e1"] == wl.Moods["e1"] && wb.Ledger.Count == 0,
            "the save: a save and load between a booking and its event, and again in the middle of a party and of a staff event, end exactly as if never saved, and charge nothing again");

        // Older and newer lines, and lines that are no line.
        PartyFile old = PartyStore.Read("v=1|77", out int v1);
        PartyFile shorter = PartyStore.Read("v=1|77|9|3^P#4#1#2#0#120#19^E#5#shop^L##2#0#110:400^I#6#3", out _);
        Check(old != null && v1 == 1 && old.Seed == 77 && old.NextId == 1 && old.Settled == -1 && old.Parties.Count == 0 && shorter != null && shorter.Find(4) != null
              && shorter.Find(4).Guests.Count == 0 && shorter.Find(4).Began == -1 && shorter.Find(4).Place == PartyPlace.Restaurant && shorter.Event(5).Kind == StaffEventKind.Lunch
              && shorter.Of(2, false).Gains[0].Amount == 400 && shorter.Invitation(6).Began == -1 && shorter.NextId == 9,
            "the save: a line with only its first fields loads, the rest as they start");
        PartyFile newer = PartyStore.Read(line.Replace("v=1|", "v=7|") + "^Z#a#thing#of#later^P#900#0#1#0#150#19#Later Street#2#Later#0#10#-1#0#0#0#0#40##a#field#more", out int v7);
        Check(newer != null && v7 == 7 && newer.Parties.Count == 3 && newer.Find(900) != null && newer.Find(900).Street == "Later Street" && newer.Find(planned.Id).Guests.Count == 6
              && newer.Rivals.Count == 1 && newer.NextId == 901,
            "the save: a line from a newer version is read as far as it goes: the parts and fields this version knows, the rest passed over");
        var data = new Dictionary<string, string> { { PartyStore.Key, "garbage" } };
        Check(PartyStore.Load(data, out int bad) == null && bad == -1 && PartyStore.Read("v=x|1", out _) == null && PartyStore.Read("v=1|x", out _) == null && PartyStore.Read("v=0|5", out _) == null
              && PartyStore.Read("v=1|5^^P^P#x^E#1^L#both#3#0#1:1^I#0#1^R#1#1", out _).Parties.Count == 0,
            "the save: a line whose head cannot be read gives nothing (and is kept as it was read), and parts that are no record are passed over");
        PartyFile heavy = PartyStore.Read("v=1|5^L##1#0#10:900,11:900,12:900", out _);
        Check(heavy.Of(1, false).Gains.Sum(g => g.Amount) == 1000 && PartyRules.Counted(heavy.Of(1, false), 12, 7) == 1000, "the save: a line edited to hold more than the cap reads as the cap");
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            string there = PartyStore.Write(f);
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Check(there == line && PartyStore.Write(PartyStore.Read(there, out _)) == line, "the save: numbers are written and read the same whatever the machine's language");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    // ------------------------------------------------------------------------------------------------- a year and more

    private static void PartyYearChecks()
    {
        // Four hundred days across a year's turn and every month's, a party a week and an event a fortnight, saved and loaded every night: the records stay bounded, the
        // money only ever leaves, no week gives a guest more than the cap, and the same save gives the same year.
        string Year(out long paid, out float most, out int kept, out bool capped)
        {
            var f = new PartyFile { Seed = 777 };
            PartyWorld w = PartyTown(5);
            w.Cast[1].Traits = SocialTrait.Flaky;
            w.Cast[2].Traits = SocialTrait.NightOwl;
            w.Staffs["shop"] = new List<StaffHand>();
            for (int i = 0; i < 6; i++)
            {
                w.Staffs["shop"].Add(new StaffHand { Id = "e" + i, Name = "Staff " + i, Mood = 60, Underpaid = i == 0, File = PartyStaffFile("e" + i, 60, i == 0) });
            }
            w.Managers.Add("shop");
            w.ShutAnswer = 1;
            w.Cash0 = 10000000;
            StaffDesk.Schedule(f, w, "shop", 30, StaffEventKind.Lunch);
            capped = true;
            var gains = new Dictionary<int, float[]>();
            for (int day = 350; day < 750; day++)
            {
                float[] before = w.Cast.Select(p => p.Familiarity).ToArray();
                w.At(day, 9);
                if (day % 7 == 0)
                {
                    PartyDesk.Host(f, w, PartyAskOf(w, day, 19, (PartyTier)(day % 3), day % 2 == 0 ? PartyPlace.Home : PartyPlace.Bar), out Party _);
                }
                for (int hour = 9; hour < 24; hour++)
                {
                    w.At(day, hour);
                    PartyDesk.Tick(f, w);
                    PartyDesk.Day(f, w, hour);
                    foreach (Party q in f.Parties.Where(x => x.State == PartyState.Planned && w.Now >= x.Start).ToList())
                    {
                        PartyDesk.Begin(f, w, q.Id);
                    }
                }
                gains[day] = w.Cast.Select((p, i) => p.Familiarity - before[i]).ToArray();
                for (int i = 0; i < w.Cast.Count; i++)
                {
                    float week = 0f;
                    for (int back = 0; back < PartyCatalogue.WindowDays && gains.ContainsKey(day - back); back++)
                    {
                        week += Math.Max(0f, gains[day - back][i]);
                    }
                    capped &= week <= PartyCatalogue.CapFamiliarity + 0.01f;
                    w.Cast[i].LastContactDay = day;
                }
                f = PartyStore.Read(PartyStore.Write(f), out _);
            }
            paid = w.Paid - w.Back;
            most = w.Cast.Max(p => p.Familiarity);
            kept = f.Parties.Count + f.Events.Count + f.Lifts.Count + f.Invites.Count;
            capped &= w.Staffs["shop"].All(h => h.File.Standing <= 60 + PartyCatalogue.CapStanding && h.File.Standing >= 60) && w.Moods.Values.All(m => m > 0f);
            return PartyStore.Write(f);
        }
        string a = Year(out long paidA, out float mostA, out int keptA, out bool cappedA), b = Year(out long paidB, out float mostB, out int _, out bool _);
        Check(a == b && paidA == paidB && mostA == mostB, "a year and more: four hundred days across the year's turn give the same file twice");
        Check(cappedA && paidA > 0 && mostA <= PartyCatalogue.Ceiling + 0.01f && keptA <= PartyCatalogue.PlannedMost + PartyCatalogue.HostedMost + PartyCatalogue.EventsKept + 4 + 5 + 6
            + PartyCatalogue.HostedMost + 2,
            "a year and more: no week gives a guest more than the cap, nothing passes the ceiling (" + mostA.ToString("0.0") + "), the staff's standing stays within the cap, the money only leaves ("
            + paidA + "), and the records stay bounded (" + keptA + ")");
    }

    // ------------------------------------------------------------------------------------------------- the pages

    private sealed class PartySampleSource : IVentSource
    {
        private readonly string _state;
        public readonly UiSamples.PartyStub Stub = new UiSamples.PartyStub();

        public PartySampleSource(string state)
        {
            _state = state;
        }

        public int Generation => 0;

        public void Opened(VentView view)
        {
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            VentModel from = UiSamples.Parties((key, english) => english, _state, v, Stub);
            foreach (var field in typeof(VentModel).GetFields())
            {
                field.SetValue(m, field.GetValue(from));
            }
        }
    }

    private static void PartyPageChecks()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Parties");
        var seenTabs = new HashSet<string>();
        int pressed = 0, dead = 0;
        var deadList = new List<string>();
        var reached = new HashSet<string>();
        foreach (string state in UiSamples.PartiesStates)
        {
            var source = new PartySampleSource(state);
            var screen = new VentScreen(source, "parties");
            screen.Lay(new UiScene(), frame, true, UiDefaultKeys.Keyboard);
            VentModel m = screen.Model;
            Check(m.Shape == VentShape.Off ? !string.IsNullOrEmpty(m.OffTitle) && !string.IsNullOrEmpty(m.OffBody) : m.Nav.Length == 3 && (m.Left.Length > 0 || !string.IsNullOrEmpty(m.Empty)),
                "parties pages: " + state + " is a page with something on it");
            int primaries = m.Left.Concat(m.Right).Where(x => x.Kind == VentBlockKind.Actions).SelectMany(x => x.Actions).Count(x => x.Kind == UiButtonKind.Primary)
                            + m.HeadActions.Count(x => x.Kind == UiButtonKind.Primary);
            Check(primaries <= 1, "parties pages: " + state + " has one primary action at most (" + primaries + ")");
            seenTabs.Add(m.Tab + ":" + string.Join("|", m.Left.Select(x => x.Kind + (x.Text ?? string.Empty))));
            var targets = new List<(int command, int arg, string what)>();
            foreach (VentBlock block in m.Left.Concat(m.Right))
            {
                switch (block.Kind)
                {
                    case VentBlockKind.Actions:
                        targets.AddRange(block.Actions.Where(x => !x.Disabled).Select(x => (VentFace.ActionCommand, x.Arg, "button " + x.Label)));
                        break;
                    case VentBlockKind.Items:
                        targets.AddRange(block.Items.Where(x => x.Run >= 0).Select(x => (VentFace.ItemCommand, x.Run, "row " + x.Title)));
                        break;
                    case VentBlockKind.Link:
                    case VentBlockKind.Switch:
                    case VentBlockKind.Choice:
                        if (!block.Disabled)
                        {
                            targets.Add((VentFace.LinkCommand, block.Run, block.Kind + " " + block.Text));
                        }
                        break;
                    case VentBlockKind.Stepper:
                        targets.Add((VentFace.StepLess, block.Run, "less " + block.Text));
                        targets.Add((VentFace.StepMore, block.Run, "more " + block.Text));
                        break;
                    case VentBlockKind.Table:
                        for (int r = 0; r < block.RowRuns.Length; r++)
                        {
                            targets.Add((block.Base + UiDataTable.Act, r, "row action " + r));
                        }
                        break;
                }
            }
            foreach (var t in targets)
            {
                var src = new PartySampleSource(state);
                var sc = new VentScreen(src, "parties");
                sc.Lay(new UiScene(), frame, true, UiDefaultKeys.Keyboard);
                string view = sc.View.Tab + "|" + sc.View.Pick;
                bool done = sc.Press(t.command, t.arg);
                pressed++;
                if (!done || (src.Stub.Count == 0 && sc.View.Tab + "|" + sc.View.Pick == view && !t.what.StartsWith("row ", StringComparison.Ordinal)))
                {
                    dead++;
                    deadList.Add(state + ": " + t.what);
                }
                if (src.Stub.Last != null)
                {
                    reached.Add(src.Stub.Last.Split(':')[0]);
                }
            }
            for (int tab = 0; tab < 3 && m.Shape != VentShape.Off; tab++)
            {
                var sc = new VentScreen(new PartySampleSource(state), "parties");
                sc.Lay(new UiScene(), frame, true, UiDefaultKeys.Keyboard);
                sc.Press(UiAppWindow.NavCommand, tab);
                sc.Lay(new UiScene(), frame, true, UiDefaultKeys.Keyboard);
                dead += sc.View.Tab == tab ? 0 : 1;
            }
        }
        string[] every =
        {
            "place", "venue", "tier", "day_less", "day_more", "hour_less", "hour_more", "toggle", "host", "business", "kind", "event_day_less", "event_day_more", "event_hour_less",
            "event_hour_more", "book", "schedule", "plan_kind", "managers", "cancel", "accept", "decline", "locate"
        };
        Check(pressed > 100 && dead == 0, "parties pages: all " + pressed + " targets of every state reach their run and do something (" + dead + " dead"
                                         + (dead > 0 ? ": " + string.Join("; ", deadList.Take(6)) : string.Empty) + ")");
        Check(every.All(reached.Contains), "parties pages: every one of the " + every.Length + " actions is reached by a button of some page (missing: "
                                           + string.Join(", ", every.Where(x => !reached.Contains(x))) + ")");
        Check(seenTabs.Select(x => x.Split(':')[0]).Distinct().Count() == 3, "parties pages: Host a party, Staff events and the Diary are three pages of their own");

        // A refusal greys the one primary action and says why; a guest of a session sees why on every page and can press nothing that books.
        VentModel refused = UiSamples.Parties((k, e) => e, "host_refused"), guest = UiSamples.Parties((k, e) => e, "guest"), ok = UiSamples.Parties((k, e) => e, "host");
        bool Disabled(VentModel x) => x.Left.Where(y => y.Kind == VentBlockKind.Actions).SelectMany(y => y.Actions).Single(y => y.Kind == UiButtonKind.Primary).Disabled;
        var guestStaff = new VentView { Tab = PartyPages.TabStaff };
        var guestDiary = new VentView { Tab = PartyPages.TabDiary };
        VentModel gs = UiSamples.Parties((k, e) => e, "guest", guestStaff), gd = UiSamples.Parties((k, e) => e, "guest", guestDiary);
        Check(Disabled(refused) && refused.Left.Any(x => x.Kind == VentBlockKind.Note && x.Tone == UiRole.Warn) && !Disabled(ok) && Disabled(guest) && Disabled(gs)
              && gs.Right.Where(x => x.Kind == VentBlockKind.Choice).All(x => x.Disabled) && !gd.Right.Any(x => x.Kind == VentBlockKind.Actions)
              && gd.Right.Any(x => x.Kind == VentBlockKind.Note && x.Tone == UiRole.Warn),
            "parties pages: a refusal greys the primary action and says why, and a guest of a session finds every booking, schedule and answer closed with the reason on the page");
        UiScreenDef def = UiScreens.Find("parties");
        string pages = ModSource(@"Ui\Parties\PartyPages.cs");
        var used = System.Text.RegularExpressions.Regex.Matches(pages, "\"(reallife_[a-z0-9_]+)\"").Select(x => x.Groups[1].Value).Distinct().ToList();
        Check(def != null && def.Template == UiTemplate.AppWindow && def.Runner == "VentScreen" && def.Section == UiScreenSwitches.Life && used.Count > 60 && used.All(def.Keys.Contains),
            "parties pages: the screen is registered with its own switch and every label its pages write with (missing: " + (def == null ? "the screen"
                : string.Join(", ", used.Where(x => !def.Keys.Contains(x)).Take(5))) + ")");
    }

    // ------------------------------------------------------------------------------------------------- the wiring and the texts

    private static void PartyWiringChecks()
    {
        string director = ModSource(@"Parties\PartyDirector.cs"), game = ModSource(@"Parties\PartyGame.cs"), links = ModSource(@"Wiring\Wiring.PartyLinks.cs");
        string live = ModSource(@"Ui\Parties\PartiesLive.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), guide = ModSource(@"Parties\PartyGuide.cs");
        string app = ModSource(@"Parties\PartyApp.cs"), tests = ModSource(@"Parties\PartyDirector.Tests.cs");
        Check(director.Contains("public static bool On => Settings.LivingPopulation && Settings.LivingParties")
            && game.Contains("public bool On => Settings.LivingPopulation && Settings.LivingParties;")
              && game.Contains("public bool Guest => GoingPublic.IsGuest;") && ModSource(@"Core\Settings.cs").Contains("public static bool LivingParties = true;")
              && tree.Contains("S(\"parties\").For(\"parties\").Opt(\"living_parties\", \"living_population\")") && mod.Contains("Settings.LivingPartiesId, \"reallife_party_enabled\""),
            "parties wiring: one option, on by default under Living population, and the game's world answers it and the session's guest");
        Check(director.Contains("PartyDesk.Tick(_file, _world)") && director.Contains("PartyDesk.Day(_file, _world, now % 1440 / 60)")
            && director.Contains("PartyDesk.Host(_file, _world, ask, out Party booked)")
              && director.Contains("PartyDesk.Begin(_file, _world, asked)") && director.Contains("PartyDesk.Join(_file, _world, -asked)") && director.Contains("PartyDesk.Cancel(_file, _world, id)")
              && director.Contains("PartyDesk.Reply(_file, _world, id, yes)") && director.Contains("StaffDesk.Book(_file, _world, ask, out StaffEvent booked)")
              && director.Contains("StaffDesk.Cancel(_file, _world, id)") && director.Contains("StaffDesk.Schedule(_file, _world, place, every, kind)")
              && director.Contains("StaffDesk.RivalDay(_file, rivals, day, level)") && director.Contains("PartyDesk.Forget(_file, person)"),
            "parties wiring: the director runs the very steps the checks drive, each through the game's world");
        Check(director.Contains("StartCoroutine(Guard.Routine(Tick()))") && director.Contains("new WaitForSecondsRealtime(1f)") && !director.Contains("void Update(")
            && !director.Contains("void LateUpdate(")
              && director.Contains("if (now != _minute)") && director.Contains("SaveHook.Keep(_onSave)") && director.Contains("save.modData[PartyStore.Key] = PartyStore.Write(_file);")
              && director.Contains("SaveLines.Unreadable(save.modData, PartyStore.Key"),
            "parties wiring: no Update: a look once a second that does its work only when the game's minute has moved; the line is written in the game's save event and kept when unreadable");
        Check(game.Contains("BizBooks.Book(-amount, new TransactionInfo(key), null, address, force: false, showNotification: false)")
            && game.Contains("BizBooks.Book(amount, new TransactionInfo(key), null,")
              && !game.Contains("ChangeMoneySafe") && !director.Contains("BizBooks") && !live.Contains("BizBooks") && !director.Contains("ChangeMoneySafe"),
            "parties wiring: money moves only through BizBooks.Book, in the one file that touches the game");
        Check(director.Contains("GameConfirm.Show(PartyText.T(\"reallife_party_ask_begin_title\")") && director.Contains("PartyDesk.CanBegin(p, _world) == PartyRefusal.None")
              && director.Contains("PartyGame.Skip(p.End, \"reallife_party_skip_caption\")") && game.Contains("GoingPublic.ClockShared") && !live.Contains("PartyDesk.Begin")
              && !live.Contains("PartyGame.Skip") && !ModSource(@"Ui\Parties\PartyPages.cs").Contains("Begin"),
            "parties wiring: a party is begun at its place, by the game's own question box, never from an app button, and the time passes with the game's own skip (not in a shared clock)");
        Check(links.Contains("PartyBridges.Floor = Floor;") && links.Contains("(tech.FloorNow + 99) / 100")
            && links.Contains("BusinessDirector.Instance.CloseFor(reg, hours, ClosureSource.StaffEvent)")
              && links.Contains("SocialLinks.PartiesForget = Forget;") && links.Contains("RivalBridges.StaffEvents = RivalDay;") && links.Contains("RivalBridges.StaffEventsPaper = RivalPaper;")
              && links.Contains("PlannerDirector.Instance?.AddSource(Source);") && links.Contains("SocialPhone.PartyLabel = PartyLabel;")
              && ModSource(@"Wiring\Wiring.cs").Contains("Step(\"parties and company events\", PartyLinks.Connect);")
              && ModSource(@"Wiring\Wiring.cs").Contains("Step(\"parties and company events\", PartyLinks.Disconnect);"),
            "parties wiring: startup money's floor, business pressure's closures, the social life, the rivals and the Planner are met through bridges, connected and disconnected");
        string rivals = ModSource(@"Rivals\RivalDirector.cs");
        Check(rivals.Contains("paid += RivalBridges.StaffEvents != null ? RivalBridges.StaffEvents(_file, day, index * 10) : 0L;")
            && rivals.Contains("RivalBridges.StaffEventsPaper(day, into, RivalCatalogue.PiecesMost - added)")
              && links.Contains("Key = \"reallife_party_paper_rival\"") && links.Contains("if (e.Day != day - 1)"),
            "parties wiring: the rivals' own day holds their outlets' staff events, and the City Ledger's rivals' pieces tell of yesterday's");
        Check(links.Contains("_paperBefore = News.Extra;") && links.Contains("News.Extra = Paper;") && links.Contains("News.Extra = _paperBefore;")
            && links.Contains("_paperBefore?.Invoke(day, into);")
              && links.Contains("PartyHosted.Large(f, day - 1)") && links.Contains("Key = \"reallife_party_paper_host\""),
            "parties wiring: a large party of the day before is a piece of the morning's City Ledger, after the pieces of the features before it, which it hands on and gives back");
        Check(ModSource(@"Social\PeopleDialog.cs").Contains("entry.OnSecondOption = HostParty;") && ModSource(@"Social\SocialDirector.Lose.cs").Contains("SocialLinks.PartiesForget?.Invoke(index);")
              && ModSource(@"Ui\Company\CompanyLive.Staff.cs").Contains("CompanyAct.Of(CompanyDo.OpenParties)")
                  && ModSource(@"Ui\Company\CompanyLive.Do.cs").Contains("PartyDirector.Open(PartyPages.TabStaff)")
              && links.Contains("PartyDirector.Open(RealLifeOverhaul.Ui.PartyPages.TabHost)") && app.Contains("PhoneApp.Wants[PartyDirector.AppId] = RealLifeOverhaul.Ui.PartiesLive.WantFrom;")
              && app.Contains("Kit = () => new RealLifeOverhaul.Ui.VentScreen(RealLifeOverhaul.Ui.PartiesLive.Instance, RealLifeOverhaul.Ui.PartiesLive.ScreenId)")
              && ModSource(@"Wiring\Wiring.CalendarLinks.cs").Contains("\"standing\", \"parties\""),
            "parties wiring: Host a party is on the People page, Hold a staff event on the Company app's staff page, each opening the app on its tab, and the Calendar shows the Planner's rows");
        Check(live.Contains("d.Host(Ask(d.World))") && live.Contains("d.Book(EventAsk(d.World, place))") && live.Contains("d.CancelEvent(id)") && live.Contains("d.CancelParty(id)")
              && live.Contains("PartyDirector.Instance?.Reply(id, yes)") && live.Contains("d.Schedule(place,") && live.Contains("EstateGame.Locate(BusinessState.PlaceKey(street, number))")
              && live.Contains("PartyDesk.Draft(file, w, Ask(w), out PlaceFacts place, out int capacity, out int cost)") && live.Contains("PartyDesk.Refuse(file, w, draft, place, capacity, cost)")
              && live.Contains("StaffDesk.Refuse(file, w, EventAsk(w, chosen.Place), chosen.Staff, cost)"),
            "parties wiring: every button of the pages reaches the director's call, and the page shows the very draft and refusal the booking would give");
        Check(guide.Contains("Features.Add(\"parties\", \"reallife_meta_title_parties\")") && guide.Contains(".Owns(Settings.LivingPartiesId)") && guide.Contains(".Data(PartyStore.Key)")
              && guide.Contains(".Test(\"reallife_party_test_invite\", \"reallife_party_test_now\", \"reallife_party_test_round\")") && guide.Contains(".ResetData(")
              && mod.Contains("\"reallife_party_test_invite\", () => Parties.PartyDirector.Instance?.TestInvite()")
                  && mod.Contains("\"reallife_party_test_now\", () => Parties.PartyDirector.Instance?.TestNow()")
              && mod.Contains("\"reallife_party_test_round\", () => Parties.PartyDirector.Instance?.TestRound()") && tests.Contains("public void TestInvite()")
                  && tests.Contains("public void TestNow()")
              && tests.Contains("public void TestRound()") && ModSource(@"Meta\FeatureList.cs").Contains("Parties.PartyGuide.Register();")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"parties\", CityScope.AnyCity")
                  && ModSource(@"Core\FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Parties\", \"parties\" }"),
            "parties wiring: the feature has its entry, its option, its save line, its Guide page and three tests in Options");
        string[] free = { "PartyCatalogue.cs", "PartyFile.cs", "IPartyWorld.cs", "PartyRules.cs", "PartyDesk.cs", "StaffDesk.cs", "PartyStore.cs", "PartyHosted.cs", "PartyKeys.cs" };
        bool gameFree = free.All(file => !ModSource(@"Parties\" + file).Contains("UnityEngine") && !ModSource(@"Parties\" + file).Contains("System.Random") && !ModSource(@"Parties\"
            + file).Contains("GetHashCode"));
        string every = string.Join("\n", free.Select(file => ModSource(@"Parties\" + file))) + director + game + live + app + tests;
        Check(gameFree && !every.Contains("UnityEngine.Random") && !every.Contains("Random.value") && !every.Contains("Random.Range") && CountOfText(every, "new System.Random()") == 2
              && director.Contains("Seed = new System.Random().Next(1, int.MaxValue)"),
            "parties wiring: the rules are game-free and draw only from the save's own seed, made once a save and kept with it");
        var seen = new HashSet<int>();
        int[] purposes =
        {
            PartyRules.PurposeAnswer, PartyRules.PurposeFar, PartyRules.PurposeRough, PartyRules.PurposeShow, PartyRules.PurposeInvite, PartyRules.PurposeHost, PartyRules.PurposeStaff,
            PartyRules.PurposeRivalKind, PartyRules.PurposeRivalWhen, PartyRules.PurposeInvitePlace
        };
        Check(purposes.All(seen.Add), "parties wiring: every draw has a purpose of its own");
    }

    private static void PartyTextChecks()
    {
        string[] sources =
        {
            @"Parties\PartyDesk.cs", @"Parties\StaffDesk.cs", @"Parties\PartyKeys.cs", @"Parties\PartyText.cs", @"Parties\PartyDirector.cs", @"Parties\PartyDirector.Tests.cs", @"Parties\PartyApp.cs",
            @"Parties\PartyGame.cs", @"Parties\PartyGuide.cs", @"Ui\Parties\PartyPages.cs", @"Ui\Parties\PartiesLive.cs", @"Wiring\Wiring.PartyLinks.cs"
        };
        var keys = new HashSet<string>
        {
            "reallife_party_enabled", "reallife_opt_sec_parties", "reallife_opt_tip_living_parties", "reallife_ui_screen_parties", "reallife_opt_tip_ui_screen_parties",
            "reallife_social_memory_party", "reallife_social_memory_party_bad", "reallife_social_memory_party_theirs", "reallife_party_co_link", "reallife_party_co_open",
            "reallife_transaction_party_host_label", "reallife_transaction_parties_staff_label"
        };
        foreach (string source in sources)
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ModSource(source),
                "\"(reallife_(?:party|guide_parties|meta_title_parties|transaction_part)[a-z0-9_]*)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        bool allIn = AllIn(keys.ToArray(), out string missing);
        Check(keys.Count > 200 && allIn, "parties texts: all " + keys.Count + " keys in English, Portuguese and Chinese (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        var long1 = keys.Where(k => k.Contains("_note_") && en.TryGetValue(k, out string v) && v.Length > 240).ToList();
        var marks = keys.Where(k => en.TryGetValue(k, out string v) && (v.Contains("!") || v.Contains("—") || v.Contains("–") || v.Contains(" - "))).ToList();
        Check(long1.Count == 0 && marks.Count == 0, "parties texts: every notice fits 240 characters and no text has an exclamation mark or a dash as punctuation ("
                                                    + string.Join(", ", long1.Concat(marks).Take(4)) + ")");
        string[] notices = keys.Where(k => k.Contains("_note_") || k.StartsWith("reallife_party_no_", StringComparison.Ordinal)).ToArray();
        Check(notices.Length > 50 && notices.All(k => en.TryGetValue(k, out string v) && (v.TrimEnd().EndsWith(".", StringComparison.Ordinal) || v.EndsWith(". {why}", StringComparison.Ordinal))),
            "parties texts: every notice and refusal ends in a full stop (or in the refusal's own sentence, which does)");
        foreach (PartyRefusal why in Enum.GetValues(typeof(PartyRefusal)))
        {
            Check(why == PartyRefusal.None || en.ContainsKey(PartyKeys.Refusal(why)), "parties texts: the refusal " + why + " has its sentence");
        }
        Check(Enum.GetValues(typeof(PartyAnswer)).Cast<PartyAnswer>().All(a => en.ContainsKey(PartyKeys.Answer(a)))
            && Enum.GetValues(typeof(PartyState)).Cast<PartyState>().All(s => en.ContainsKey(PartyKeys.State(s)))
              && Enum.GetValues(typeof(StaffEventState)).Cast<StaffEventState>().All(s => en.ContainsKey(PartyKeys.Event(s)))
              && Enum.GetValues(typeof(InviteState)).Cast<InviteState>().All(s => en.ContainsKey(PartyKeys.Invite(s, false)) && en.ContainsKey(PartyKeys.Invite(s, true)))
              && en[PartyKeys.Answer(PartyAnswer.Yes)] == PartyPages.AnswerEnglish(PartyAnswer.Yes) && en[PartyKeys.Place(PartyPlace.Bar)] == PartyPages.PlaceEnglish(PartyPlace.Bar)
              && en[PartyKeys.Tier(PartyTier.Lavish)] == PartyPages.TierEnglish(PartyTier.Lavish),
            "parties texts: every answer and every state has its words, and the pages' own English is the locale's");
        // Each notice's names are the ones the code fills.
        var fills = new Dictionary<string, string[]>
        {
            { "reallife_party_note_booked", new[] { "place", "when", "count", "cost" } }, { "reallife_party_note_cancelled", new[] { "place", "back" } },
            { "reallife_party_note_begun", new[] { "place", "count", "until" } }, { "reallife_party_note_good", new[] { "place", "count", "closer" } },
            { "reallife_party_note_missed", new[] { "place", "count" } }, { "reallife_party_note_event_booked", new[] { "business", "when", "count", "cost" } },
            { "reallife_party_note_event_held", new[] { "business", "count", "staff", "hours" } }, { "reallife_party_note_manager_refused", new[] { "business", "why" } },
            { "reallife_party_text_invite", new[] { "place", "when" } }, { "reallife_party_ask_begin", new[] { "place", "count", "until" } },
            { "reallife_party_paper_rival", new[] { "rival", "holding", "kind", "count" } }, { "reallife_party_when_day", new[] { "day", "time" } },
            { "reallife_party_paper_host", new[] { "place", "count" } }
        };
        var wrong = new List<string>();
        foreach (string code in ModLanguages.All)
        {
            Dictionary<string, string> words = LocaleFile(code);
            foreach (KeyValuePair<string, string[]> fill in fills)
            {
                if (words == null || !words.TryGetValue(fill.Key, out string text))
                {
                    continue;   // a language that has not the key yet reads it in English
                }
                var names = System.Text.RegularExpressions.Regex.Matches(text, "\\{([a-z_]+)\\}").Select(x => x.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();
                if (!names.SequenceEqual(fill.Value.OrderBy(x => x)))
                {
                    wrong.Add(code + ":" + fill.Key);
                }
            }
        }
        Check(wrong.Count == 0, "parties texts: each notice names exactly what the code fills, in every language that has it (" + string.Join(", ", wrong.Take(5)) + ")");
        string guideText = en.TryGetValue("reallife_guide_parties", out string g) ? g : string.Empty;
        string guideCode = ModSource(@"Parties\PartyGuide.cs");
        var facts = System.Text.RegularExpressions.Regex.Matches(guideText, "\\{([a-z_]+)\\}").Select(x => x.Groups[1].Value).Distinct().ToList();
        Check(facts.Count >= 12 && facts.All(x => guideCode.Contains(".Fact(\"" + x + "\"")) && guideText.Contains("<b>" + en["reallife_party_enabled"] + "</b>")
              && guideText.Contains("<b>" + en["reallife_party_tab_host"] + "</b>") && guideText.Contains("<b>" + en["reallife_party_tab_staff"] + "</b>")
              && guideText.Contains("<b>" + en["reallife_party_tab_diary"] + "</b>") && guideText.Contains("<b>" + en["reallife_party_sec_manager"] + "</b>"),
            "parties texts: the Guide's page names the option and the app's pages by their own names, and every figure in it is filled from the catalogue");
        // What's new: found by its English line in whichever row holds it, then read in that row in each language (the release numbers the next row).
        string row = en.Where(r => r.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !r.Key.EndsWith("_title", StringComparison.Ordinal)
                                   && r.Value.Contains("Parties and company events, asked for by a player")).Select(r => r.Key).FirstOrDefault();
        Check(row != null, "parties texts: What's new tells of parties and company events, with no prefix");
        if (row != null)
        {
            string enRow = en[row];
            int at = enRow.IndexOf("Parties and company events, asked for by a player", StringComparison.Ordinal), fix = enRow.IndexOf("Fixed: ", StringComparison.Ordinal);
            Check(fix < 0 || at < fix, "parties texts: the feature's line stands before the first fix of its row");
            foreach (string code in ModLanguages.PerFeature)
            {
                Dictionary<string, string> words = LocaleFile(code);
                Check(code == "en" || (words != null && words.TryGetValue(row, out string theirs) && theirs != enRow && theirs.Length > 200 && words.TryGetValue("reallife_party_app",
                    out string appName)
                                       && theirs.Contains(appName)), "parties texts: What's new has the line in " + code + ", naming the app as that language does");
            }
        }
    }
}
