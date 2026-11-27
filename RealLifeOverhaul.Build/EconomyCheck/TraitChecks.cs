using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Family;

// People's passions and the moments on outings (Social/SocialPassionRules, SocialMomentRules, their catalogues). Each person has one
// passion from the ten new traits, rolled from their seed on a stream of its own (so an old save's people get theirs without any other
// roll moving), hidden until the player finds it out; it tilts what outings, talks, neglect and money are worth. About one outing in three
// has a moment with three reactions weighed against their traits, passion and mood. Run alone: EconomyCheck --traits29.
internal static partial class Program
{
    private const int TraitYear = 28;
    private const int TraitDistricts = 9;

    private static void Traits29()
    {
        int before = _run, failedBefore = _failed;
        TraitBits();
        TraitRoll();
        TraitOldSave();
        TraitEffects();
        TraitDiscovery();
        TraitMomentRate();
        TraitWeighing();
        TraitSwitches();
        TraitTexts();
        Console.WriteLine("  traits29: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private static SocialPerson TraitPerson(SocialTrait passion, SocialStage stage = SocialStage.Friend, int seed = 4711)
    {
        return new SocialPerson { Index = 9, Name = "Dana Reyes", Origin = SocialOrigin.Street, Seed = seed, Met = true, Stage = stage, Traits = SocialTrait.Punctual | SocialTrait.Sporty | passion, Shared = 3, Affection = 50f };
    }

    private static SocialFacts TraitFacts() => new SocialFacts { WealthTier = 1 };

    // ---------------------------------------------------------------- the number a save holds

    private static void TraitBits()
    {
        bool above = true;
        foreach (SocialTrait t in SocialPassionRules.All)
        {
            above &= (int)t > (int)SocialTrait.Envious && ((int)t & ((int)t - 1)) == 0;
        }
        Check(above && SocialPassionRules.All.Length == 10 && (int)SocialTrait.Envious == 2048 && (int)SocialTrait.Punctual == 1 && (int)SocialTrait.PassionMask > 4095,
            "traits: the ten passions are single bits above the twelve old traits, which keep theirs");
        Check((int)MemoryCode.PartyTheirs == 30 && (int)MemoryCode.MomentSweet == 31 && (int)MemoryCode.PassionFound == 34,
            "traits: the new memory codes are added at the end of the enum, the old ones keep their numbers");
        var p = TraitPerson(SocialTrait.Cinephile);
        var cast = new SocialPerson[SocialRules.MaxCast];
        SocialCatalogue.MakeCast(cast, 77, 3, TraitYear, TraitDistricts);
        cast[2].Traits |= SocialTrait.FitnessMinded;
        SocialState.Load(new Dictionary<string, string>(), 3, TraitYear, TraitDistricts, 77);
        SocialState.People[2].Traits |= SocialTrait.PassionMask;
        var data = new Dictionary<string, string>();
        SocialState.Save(data);
        int seen = SocialState.People[2].Traits.GetHashCode();
        SocialState.Load(data, 3, TraitYear, TraitDistricts, 5);
        Check(SocialState.People[2].Traits.GetHashCode() == seen && ((int)SocialState.People[2].Traits & (int)SocialTrait.CareerFocused) != 0 && p.Has(SocialTrait.Cinephile),
            "traits: the codec saves and loads a traits number wider than 4095");
    }

    // ---------------------------------------------------------------- the roll

    private static void TraitRoll()
    {
        var seen = new HashSet<SocialTrait>();
        bool deterministic = true, clash = true, family = true;
        for (int seed = 1; seed <= 4000; seed++)
        {
            SocialTrait a = SocialPassionRules.Roll(seed, false, SocialTrait.None);
            deterministic &= a == SocialPassionRules.Roll(seed, false, SocialTrait.None) && SocialPassionRules.IndexOf(a) >= 0;
            seen.Add(a);
            clash &= !SocialPassionRules.Clashes(SocialTrait.Homebody, SocialPassionRules.Roll(seed, false, SocialTrait.Homebody))
                     && !SocialPassionRules.Clashes(SocialTrait.Envious, SocialPassionRules.Roll(seed, false, SocialTrait.Envious))
                     && !SocialPassionRules.Clashes(SocialTrait.Flaky, SocialPassionRules.Roll(seed, false, SocialTrait.Flaky))
                     && !SocialPassionRules.Clashes(SocialTrait.Careful, SocialPassionRules.Roll(seed, false, SocialTrait.Careful));
            SocialTrait m = SocialPassionRules.Roll(seed, true, SocialTrait.None);
            family &= m == SocialTrait.SocialButterfly || m == SocialTrait.Cinephile || m == SocialTrait.KindSoul || m == SocialTrait.ChildOfEarth;
        }
        Check(deterministic && seen.Count == 10, "traits: a passion is a pure function of the seed, and every one of the ten is dealt over many seeds");
        Check(clash && family, "traits: nobody gets a passion that clashes with their traits (a homebody a clubber), and Mom a passion that suits a mother");
        // The rolls of the cast do not move: the cast for a seed is the same before and after every person is asked for a passion.
        bool same = true;
        for (int seed = 1; seed <= 200; seed++)
        {
            var a = new SocialPerson[SocialRules.BaseCast];
            var b = new SocialPerson[SocialRules.BaseCast];
            SocialCatalogue.MakeCast(a, seed, 3, TraitYear, TraitDistricts);
            SocialCatalogue.MakeCast(b, seed, 3, TraitYear, TraitDistricts);
            for (int i = 0; i < SocialRules.BaseCast; i++)
            {
                _ = a[i].Passion;
                same &= a[i].Name == b[i].Name && a[i].Age == b[i].Age && a[i].Seed == b[i].Seed && a[i].Birthday == b[i].Birthday && a[i].Job == b[i].Job
                        && a[i].District == b[i].District && ((int)a[i].Traits & 4095) == (int)b[i].Traits && a[i].Female == b[i].Female && a[i].FreeFrom == b[i].FreeFrom;
            }
            var later = SocialCatalogue.MakeNew(9, SocialOrigin.Street, -1, seed, 5, TraitYear, TraitDistricts, a, SocialRules.BaseCast);
            var later2 = SocialCatalogue.MakeNew(9, SocialOrigin.Street, -1, seed, 5, TraitYear, TraitDistricts, b, SocialRules.BaseCast);
            _ = later.Passion;
            same &= later.Name == later2.Name && later.Age == later2.Age && later.Seed == later2.Seed && ((int)later.Traits & 4095) == (int)later2.Traits;
        }
        Check(same, "traits: asking for every passion changes no other roll of a cast, nor of somebody met later");
    }

    private static void TraitOldSave()
    {
        const string main = "reallife:social", people = "reallife:social_people";
        SocialState.Load(new Dictionary<string, string>(), 10, TraitYear, TraitDistricts, 321);
        var first = new SocialTrait[SocialRules.BaseCast];
        var old = new SocialTrait[SocialRules.BaseCast];
        for (int i = 0; i < SocialRules.BaseCast; i++)
        {
            first[i] = SocialState.People[i].Passion;
            old[i] = SocialState.People[i].Traits & (SocialTrait)4095;
        }
        var data = new Dictionary<string, string>();
        SocialState.Save(data);
        // As a version before passions wrote it: no field past the 51st, the traits as the twelve, a main record of twelve fields.
        var lines = new List<string>();
        foreach (string line in data[people].Split('\n'))
        {
            string[] f = line.Split('|');
            f[6] = ((int.Parse(f[6]) & 4095)).ToString();
            lines.Add(string.Join("|", f, 0, 51));
        }
        data[people] = string.Join("\n", lines);
        string[] m = data[main].Split('|');
        data[main] = string.Join("|", m, 0, 12);
        SocialState.Load(data, 40, TraitYear, TraitDistricts, 999);
        bool every = true, hidden = true, traits = true, kept = true;
        for (int i = 0; i < SocialRules.BaseCast; i++)
        {
            SocialPerson p = SocialState.People[i];
            every &= (p.Traits & SocialTrait.PassionMask) != 0 && p.Passion == first[i];
            hidden &= !p.PassionFound && p.PassionClues == 0 && p.MomentLast == -1 && !p.MomentRow;
            traits &= (p.Traits & (SocialTrait)4095) == old[i];
        }
        kept = SocialState.RiseDay == 0 && SocialState.HelpedDay == 0;
        Check(every && hidden && traits && kept,
            "traits: an old save's people each get the passion their seed rolls, all hidden, their two old traits unchanged, and nothing else of the record moves");
        // Found passions and the moments' marks survive a save.
        SocialState.People[1].PassionFound = true;
        SocialState.People[1].PassionClues = 3;
        SocialState.People[2].MomentLast = 21;
        SocialState.People[2].MomentRow = true;
        SocialState.People[2].MomentEcho = true;
        SocialState.RiseDay = 12;
        SocialState.HelpedDay = 14;
        var again = new Dictionary<string, string>();
        SocialState.Save(again);
        SocialState.Load(again, 40, TraitYear, TraitDistricts, 999);
        Check(SocialState.People[1].PassionFound && SocialState.People[1].PassionClues == 3 && SocialState.People[2].MomentLast == 21 && SocialState.People[2].MomentRow
              && SocialState.People[2].MomentEcho && !SocialState.People[3].PassionFound && SocialState.RiseDay == 12 && SocialState.HelpedDay == 14,
            "traits: a found passion, the clues and the marks of a moment come back from a save");
    }

    // ---------------------------------------------------------------- what each passion does

    private static float TraitAff(SocialTrait passion, PlanKind kind, SocialFacts f, int pay = 2, SocialStage stage = SocialStage.Friend)
    {
        SocialPerson p = TraitPerson(passion, stage);
        SocialTilt t = SocialPassionRules.Outing(p, kind, f, pay);
        return SocialPassionRules.Tilted(6f, t, 1f);
    }

    private static void TraitEffects()
    {
        SocialFacts f = TraitFacts();
        Check(TraitAff(SocialTrait.Cinephile, PlanKind.Cinema, f) > 6f + 4f && TraitAff(SocialTrait.Cinephile, PlanKind.Coffee, f) == 6f,
            "traits: a cinephile at the cinema counts far more, at anything else nothing");
        Check(TraitAff(SocialTrait.Clubber, PlanKind.Drinks, f) >= 12f && TraitAff(SocialTrait.Clubber, PlanKind.Coffee, f) < 6f && TraitAff(SocialTrait.Clubber, PlanKind.Walk, f) < 6f,
            "traits: a clubber's drinks count double, coffee and walks cost them");
        Check(TraitAff(SocialTrait.SocialButterfly, PlanKind.Coffee, f) > TraitAff(SocialTrait.SocialButterfly, PlanKind.Cinema, f) && TraitAff(SocialTrait.SocialButterfly, PlanKind.Cinema, f) > 6f,
            "traits: a social butterfly counts coffee most and every outing a little more");
        SocialFacts lux = f;
        lux.Arrival = SocialArrival.Car;
        lux.CarLuxury = true;
        SocialFacts rich = f;
        rich.WealthTier = 4;
        Check(TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, f, 1) > TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, f, 2) && TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, f, 2) > TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, f, 3)
              && TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, lux, 2) > TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, f, 2) && TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, rich, 2) > TraitAff(SocialTrait.GoldDigger, PlanKind.Meal, f, 2),
            "traits: a gold digger is won by the player paying, a luxury car and wealth, and put off by a split bill");
        SocialTilt slow = SocialPassionRules.Outing(TraitPerson(SocialTrait.GoldDigger), PlanKind.Meal, f, 2);
        Check(SocialPassionRules.TiltedTrust(3f, slow, 1f) < 3f, "traits: a gold digger's trust grows slowly");
        SocialPassionRules.Daily(TraitPerson(SocialTrait.GoldDigger), 1, 3, out float drop, out float dropTrust);
        SocialPassionRules.Daily(TraitPerson(SocialTrait.GoldDigger), 4, 4, out float well, out _);
        SocialPassionRules.Daily(TraitPerson(SocialTrait.Cinephile), 1, 3, out float other, out _);
        Check(drop <= -3f && dropTrust < 0f && well > 0f && other == 0f, "traits: a gold digger cools fast the day the player's money falls a step and warms a little when it is high, nobody else does");
        SocialFacts good = f;
        good.CharityMonth = true;
        SocialFacts helped = f;
        helped.Helped = true;
        Check(TraitAff(SocialTrait.KindSoul, PlanKind.Coffee, good) > 6f && TraitAff(SocialTrait.KindSoul, PlanKind.Coffee, helped) > 6f && TraitAff(SocialTrait.KindSoul, PlanKind.Coffee, f) == 6f,
            "traits: a kind soul is lifted by charity and a good turn");
        SocialPerson rude = TraitPerson(SocialTrait.KindSoul);
        rude.Remember(MemoryCode.Rude, 3);
        Check(SocialPassionRules.Tilted(6f, SocialPassionRules.Outing(rude, PlanKind.Coffee, f, 2), 1f) < 6f, "traits: rudeness puts a kind soul off");
        SocialFacts uniform = f;
        uniform.InUniform = true;
        Check(TraitAff(SocialTrait.LovesUniforms, PlanKind.Coffee, uniform) >= 10f && TraitAff(SocialTrait.LovesUniforms, PlanKind.Coffee, f) == 6f, "traits: a person who loves uniforms is lifted a great deal by a work uniform");
        SocialFacts foot = f, bike = f, bus = f, electric = f, petrol = f;
        foot.Arrival = SocialArrival.Foot;
        bike.Arrival = SocialArrival.Bike;
        bus.Arrival = SocialArrival.Transit;
        electric.Arrival = SocialArrival.Car;
        electric.CarElectric = true;
        petrol.Arrival = SocialArrival.Car;
        float earth(SocialFacts x) => TraitAff(SocialTrait.ChildOfEarth, PlanKind.Coffee, x);
        Check(earth(foot) > 6f && earth(bike) > earth(bus) && earth(bus) > earth(foot) && earth(electric) == 6f && earth(petrol) < 6f - 2f,
            "traits: a child of the earth is delighted by a bike, a bus or arriving on foot, an electric car is fine, a petrol car at the door costs");
        SocialFacts park = f;
        park.AtSight = true;
        Check(TraitAff(SocialTrait.ChildOfEarth, PlanKind.Walk, park) > TraitAff(SocialTrait.ChildOfEarth, PlanKind.Walk, f) && TraitAff(SocialTrait.ChildOfEarth, PlanKind.Walk, f) > 6f,
            "traits: a walk at the city's sights matters more to a child of the earth");
        SocialFacts day = f;
        day.Special = true;
        Check(TraitAff(SocialTrait.Romantic, PlanKind.Coffee, day) >= 6f * 2f && TraitAff(SocialTrait.Romantic, PlanKind.Walk, park) > TraitAff(SocialTrait.Romantic, PlanKind.Walk, f),
            "traits: a romantic counts a birthday or an anniversary double, and a walk at a sight more");
        Check(SocialPassionRules.IsSpecialDay(TraitPerson(SocialTrait.Romantic, SocialStage.Partner), 100, 0, TraitYear)
              && !SocialPassionRules.IsSpecialDay(TraitPerson(SocialTrait.Romantic, SocialStage.Partner), 100, 5, TraitYear)
              && SocialPassionRules.IsSpecialDay(new SocialPerson { Stage = SocialStage.Partner, StageDay = 44 }, 44 + TraitYear * 2, 9, TraitYear),
            "traits: a birthday is a special day, and so is a whole year of being partners");
        SocialFacts fit = f, unfit = f;
        fit.FitnessKnown = unfit.FitnessKnown = true;
        fit.Fitness = 85;
        unfit.Fitness = 15;
        Check(TraitAff(SocialTrait.FitnessMinded, PlanKind.Workout, fit) >= 12f && TraitAff(SocialTrait.FitnessMinded, PlanKind.Workout, fit) > TraitAff(SocialTrait.FitnessMinded, PlanKind.Workout, unfit)
              && TraitAff(SocialTrait.FitnessMinded, PlanKind.Coffee, fit) > 6f && TraitAff(SocialTrait.FitnessMinded, PlanKind.Coffee, unfit) < 6f,
            "traits: a fitness minded person counts workouts double and reads the player's fitness: fit lifts, unfit costs");
        SocialFacts diet = f;
        diet.Diet = -1;
        Check(TraitAff(SocialTrait.FitnessMinded, PlanKind.Meal, diet) < TraitAff(SocialTrait.FitnessMinded, PlanKind.Meal, f), "traits: a poor diet costs with a fitness minded person at a meal");
        SocialFacts rose = f;
        rose.Rose = true;
        Check(TraitAff(SocialTrait.CareerFocused, PlanKind.Coffee, rose) > 6f && TraitAff(SocialTrait.CareerFocused, PlanKind.Business, f) > 6f + 2f && TraitAff(SocialTrait.CareerFocused, PlanKind.Coffee, f) == 6f,
            "traits: a career focused person is impressed by a promotion or a new business, and by the look round the shop");
        SocialPerson c = TraitPerson(SocialTrait.CareerFocused), n = TraitPerson(SocialTrait.Cinephile);
        Check(SocialPassionRules.PlanMinds(c) == 0.5f && SocialPassionRules.PlanMinds(n) == 1f && SocialPassionRules.KeepsWorking(c) && !SocialPassionRules.KeepsWorking(n)
              && SocialPassionRules.Grace(c, 7) == 9 && SocialPassionRules.Grace(n, 7) == 7 && PartnerWorkRules.Scale(true) > PartnerWorkRules.Scale(false)
              && !PartnerWishRules.MayAsk(PartnerWishRules.Stop, false, true) && PartnerWishRules.MayAsk(PartnerWishRules.Retrain, false, true)
              && PartnerWishRules.MayAsk(PartnerWishRules.Stop, false, false),
            "traits: a career focused person minds a moved plan half as much, will not step back or stop working for a successful partner, and does not mind a silence as soon");
        SocialPerson b = TraitPerson(SocialTrait.SocialButterfly);
        SocialWorth w = SocialPassionRules.Talk(b, new SocialWorth { Familiarity = 3f, Affection = 2f, Trust = 0.5f });
        Check(w.Familiarity > 3f && w.Affection > 2f && w.Trust == 0.5f && SocialPassionRules.Grace(b, 7) == 5 && SocialPassionRules.ExtraDecay(b) < 0f && SocialPassionRules.IntroExtra(b) > 0f
              && SocialPassionRules.Party(b, 100f) == 150f && SocialPassionRules.Party(n, 100f) == 100f && SocialPassionRules.Keenness(b) > 0f,
            "traits: a social butterfly counts calls and parties for more, feels neglect sooner and brings friends along");
        SocialPerson cl = TraitPerson(SocialTrait.Clubber);
        Check(!SocialPassionRules.MindsLate(cl) && SocialPassionRules.MindsLate(n), "traits: a clubber does not mind being late");
        float giftFlop = SocialPassionRules.Gift(TraitPerson(SocialTrait.GoldDigger), out int c1, out bool l1);
        float giftLand = SocialPassionRules.Gift(TraitPerson(SocialTrait.Romantic), out int c2, out bool l2);
        Check(giftFlop < 0f && !l1 && c1 == 1 && giftLand > 0f && l2 && c2 == 1 && SocialPassionRules.Gift(n, out _, out _) == 0f, "traits: a gift lands with a romantic and flops with a gold digger");
        Check(SocialPassionRules.Liking(cl, PlanKind.Drinks) > 0f && SocialPassionRules.Liking(cl, PlanKind.Coffee) < 0f && SocialPassionRules.Liking(n, PlanKind.Cinema) > 0f
              && SocialRules.Liking(TraitPerson(SocialTrait.Cinephile), PlanKind.Cinema) > SocialRules.Liking(TraitPerson(SocialTrait.LovesUniforms), PlanKind.Cinema),
            "traits: what they ask for follows their passion");
        // Same inputs, same answer.
        SocialTilt t1 = SocialPassionRules.Outing(TraitPerson(SocialTrait.GoldDigger), PlanKind.Meal, rich, 1), t2 = SocialPassionRules.Outing(TraitPerson(SocialTrait.GoldDigger), PlanKind.Meal, rich, 1);
        Check(t1.AffAdd == t2.AffAdd && t1.AffMul == t2.AffMul && t1.Clue == t2.Clue && t1.Led == t2.Led, "traits: the same person and outing always weigh the same");
    }

    // ---------------------------------------------------------------- finding it out

    private static void TraitDiscovery()
    {
        SocialPerson f = TraitPerson(SocialTrait.Cinephile);
        bool first = SocialPassionRules.AddClue(f, 2, 5);
        bool second = SocialPassionRules.AddClue(f, 1, 5);
        bool third = SocialPassionRules.AddClue(f, 3, 6);
        Check(!first && second && !third && f.PassionFound && f.Remembers(MemoryCode.PassionFound) && SocialPassionRules.Bar(f) == 3,
            "traits: a friend's passion is found out at three points of clues, once, and remembered");
        SocialPerson close = TraitPerson(SocialTrait.Cinephile, SocialStage.CloseFriend), acq = TraitPerson(SocialTrait.Cinephile, SocialStage.Acquaintance);
        SocialPerson partner = TraitPerson(SocialTrait.Cinephile, SocialStage.Partner), mom = TraitPerson(SocialTrait.Cinephile);
        mom.Origin = SocialOrigin.Family;
        Check(SocialPassionRules.Bar(acq) == 4 && SocialPassionRules.Bar(close) == 2 && SocialPassionRules.Bar(partner) == 2 && SocialPassionRules.Bar(mom) == 2,
            "traits: partners, close friends and family give theirs away sooner than acquaintances");
        partner.StageDay = 10;
        Check(!SocialPassionRules.ShowsOfThemselves(partner, 16) && SocialPassionRules.ShowsOfThemselves(partner, 17) && !SocialPassionRules.ShowsOfThemselves(close, 100),
            "traits: a partner shows what they live for of themselves within a week");
        SocialPerson one = TraitPerson(SocialTrait.Cinephile, SocialStage.Acquaintance);
        Check(!SocialPassionRules.AddClue(one, 10, 5) && one.PassionClues == SocialPassionRules.CluePerOuting, "traits: one outing gives at most three points of clues");
        int due = 0;
        for (int d = 1; d <= 2000; d++)
        {
            due += SocialPassionRules.HintDue(TraitPerson(SocialTrait.Clubber, SocialStage.Friend, 900 + d % 7), d) ? 1 : 0;
        }
        SocialPerson hinted = TraitPerson(SocialTrait.Clubber);
        hinted.HintDay = 50;
        Check(due > 400 && due < 1400 && !SocialPassionRules.HintDue(hinted, 53) && SocialPassionRules.HintDue(f, 5) == false,
            "traits: a hint slips into a text on some days, never within five days of the last, and never once it is found");
        Check(SocialPassionRules.Variant(hinted, 5, 4, 29) == SocialPassionRules.Variant(hinted, 5, 4, 29) && SocialPassionRules.Variant(hinted, 5, 1, 29) == 0,
            "traits: which line a person says is theirs and the day's, the same every time");
    }

    // ---------------------------------------------------------------- moments

    private static void TraitMomentRate()
    {
        int outings = 0, moments = 0;
        bool never2 = true, noFirst = true, kinds = true;
        for (int seed = 1; seed <= 3000; seed++)
        {
            SocialPerson p = TraitPerson(SocialTrait.Cinephile, SocialStage.Friend, seed);
            bool last = false;
            for (int n = 0; n < 12; n++)
            {
                p.Shared = n;
                bool has = SocialMomentRules.Occurs(p, (PlanKind)(n % 8));
                noFirst &= n > 0 || !has;
                never2 &= !(has && last);
                p.MomentRow = has;
                last = has;
                outings++;
                moments += has ? 1 : 0;
            }
        }
        double rate = moments / (double)outings;
        Check(noFirst && never2 && rate > 0.28 && rate < 0.38, "moments: about one outing in three over many seeds (" + rate.ToString("F3") + "), never the first, never two in a row with the same person");
        SocialPerson lift = TraitPerson(SocialTrait.Cinephile);
        lift.Shared = 4;
        for (int s = 1; s < 80 && kinds; s++)
        {
            lift.Seed = s;
            lift.MomentRow = false;
            kinds &= !SocialMomentRules.Occurs(lift, PlanKind.Lift) && !SocialMomentRules.Occurs(lift, PlanKind.Moving);
        }
        SocialPerson a = TraitPerson(SocialTrait.Cinephile, SocialStage.Friend, 12);
        SocialPerson b = TraitPerson(SocialTrait.Cinephile, SocialStage.Friend, 12);
        a.Shared = b.Shared = 3;
        Check(kinds && SocialMomentRules.Occurs(a, PlanKind.Cinema) == SocialMomentRules.Occurs(b, PlanKind.Cinema)
              && SocialMomentRules.Pick(a, PlanKind.Cinema).Id == SocialMomentRules.Pick(b, PlanKind.Cinema).Id, "moments: favours have none, and the roll and the pick are the same for the same person");
        bool notSame = true, allSix = true;
        for (int k = 0; k < 8; k++)
        {
            var kind = (PlanKind)k;
            var seen = new HashSet<int>();
            SocialPerson p = TraitPerson(SocialTrait.Cinephile);
            for (int n = 0; n < 200; n++)
            {
                p.Shared = n;
                p.Seed = 10 + n;
                MomentDef d = SocialMomentRules.Pick(p, kind);
                notSame &= d != null && d.Id != p.MomentLast;
                p.MomentLast = d.Id;
                seen.Add(d.Id);
            }
            allSix &= seen.Count == SocialMomentRules.PerKind;
        }
        Check(notSame && allSix, "moments: each outing kind plays all six of its moments, never the one just had");
    }

    private static SocialPerson Weigher(SocialTrait passion, int mood = 0)
    {
        SocialPerson p = TraitPerson(passion);
        p.Traits = passion;   // no old traits: only the passion and the mood lean
        p.Mood = mood;
        return p;
    }

    private static void TraitWeighing()
    {
        MomentDef dj = SocialMomentRules.ById(21);       // drinks: the DJ plays their song (lively, tender, funny)
        MomentDef film = SocialMomentRules.ById(31);     // cinema: the phone in the row behind (bold, calm, kind)
        MomentDef bar = SocialMomentRules.ById(20);      // drinks: too close at the bar (bold, kind, calm)
        MomentResult clubL = SocialMomentRules.Weigh(Weigher(SocialTrait.Clubber), dj, 0, false), clubT = SocialMomentRules.Weigh(Weigher(SocialTrait.Clubber), dj, 1, false);
        MomentResult romL = SocialMomentRules.Weigh(Weigher(SocialTrait.Romantic), dj, 0, false), romT = SocialMomentRules.Weigh(Weigher(SocialTrait.Romantic), dj, 1, false);
        Check(clubL.Score > clubT.Score && romT.Score > romL.Score && clubL.Tier == MomentTier.Loved && clubL.Led == SocialTrait.Clubber && clubL.Clue == 2,
            "moments: a clubber loves the lively answer, a romantic the tender one, and the passion that weighed in is named");
        MomentResult cinCalm = SocialMomentRules.Weigh(Weigher(SocialTrait.Cinephile), film, 1, false), cinBold = SocialMomentRules.Weigh(Weigher(SocialTrait.Cinephile), film, 0, false);
        Check(cinCalm.Score > cinBold.Score && cinCalm.Pleased && !cinBold.Pleased, "moments: a cinephile wants the film left alone, and is put off by a scene made in the cinema");
        MomentResult kindKind = SocialMomentRules.Weigh(Weigher(SocialTrait.KindSoul), bar, 1, false), kindBold = SocialMomentRules.Weigh(Weigher(SocialTrait.KindSoul), bar, 0, false);
        Check(kindKind.Score > kindBold.Score, "moments: a kind soul prefers kindness to a bold stand");
        SocialPerson gold = Weigher(SocialTrait.GoldDigger);
        MomentDef round = SocialMomentRules.ById(24);    // drinks: a drink named after them (tender, generous, funny)
        Check(SocialMomentRules.Weigh(gold, round, 1, false).Score > SocialMomentRules.Weigh(gold, round, 0, false).Score, "moments: a gold digger likes the generous answer");
        SocialPerson tired = Weigher(SocialTrait.Cinephile, -2), merry = Weigher(SocialTrait.Cinephile, 2);
        Check(SocialMomentRules.Weigh(merry, bar, 2, false).Score > SocialMomentRules.Weigh(tired, bar, 2, false).Score, "moments: their mood weighs too");
        SocialPerson old = Weigher(SocialTrait.None);
        old.Traits = SocialTrait.Supportive;
        SocialPerson env = Weigher(SocialTrait.None);
        env.Traits = SocialTrait.Envious;
        Check(SocialMomentRules.Weigh(old, round, 1, false).Score > SocialMomentRules.Weigh(env, round, 1, false).Score, "moments: their old traits weigh too, a supportive person against an envious one");
        SocialPerson loves = Weigher(SocialTrait.LovesUniforms);
        Check(SocialMomentRules.Weigh(loves, dj, 2, true).Score > SocialMomentRules.Weigh(loves, dj, 2, false).Score, "moments: a person who loves uniforms likes every answer more when the player wears one");
        MomentResult r = SocialMomentRules.Weigh(clubT.Led == SocialTrait.None ? Weigher(SocialTrait.Clubber) : Weigher(SocialTrait.Clubber), dj, 0, false);
        Check(r.Affection == 4f && r.Trust == 1.5f && r.Familiarity == 1.5f && SocialMomentRules.Weigh(Weigher(SocialTrait.Clubber, -2), SocialMomentRules.ById(20), 2, false).Tier <= MomentTier.Flat,
            "moments: the figures of a tier are those of the table (loved: 1.5, 4 and 1.5)");
        SocialPerson hiddenFound = Weigher(SocialTrait.Clubber);
        hiddenFound.PassionFound = true;
        Check(SocialMomentRules.Weigh(hiddenFound, dj, 0, false).Clue == 0, "moments: a passion already found gives no more clues");
    }

    // ---------------------------------------------------------------- the options off

    private static void TraitSwitches()
    {
        Func<bool> passions = SocialPassionRules.OnSwitch, moments = SocialMomentRules.OnSwitch;
        SocialFacts f = TraitFacts();
        f.CharityMonth = f.InUniform = f.Rose = f.Special = true;
        try
        {
            SocialPassionRules.OnSwitch = () => false;
            bool none = true;
            foreach (SocialTrait passion in SocialPassionRules.All)
            {
                SocialPerson p = TraitPerson(passion);
                foreach (PlanKind k in new[] { PlanKind.Coffee, PlanKind.Meal, PlanKind.Drinks, PlanKind.Cinema, PlanKind.Walk, PlanKind.Drive, PlanKind.Workout, PlanKind.Business })
                {
                    SocialTilt t = SocialPassionRules.Outing(p, k, f, 1);
                    none &= t.AffMul == 1f && t.TrustMul == 1f && t.AffAdd == 0f && t.TrustAdd == 0f && t.FamAdd == 0f && t.Clue == 0 && t.Led == SocialTrait.None
                            && SocialPassionRules.Liking(p, k) == 0f;
                }
                SocialPassionRules.Daily(p, 0, 4, out float a, out float tr);
                none &= SocialPassionRules.Grace(p, 7) == 7 && SocialPassionRules.ExtraDecay(p) == 0f && SocialPassionRules.PlanMinds(p) == 1f && SocialPassionRules.MindsLate(p)
                        && SocialPassionRules.IntroExtra(p) == 0f && SocialPassionRules.Keenness(p) == 0f && SocialPassionRules.Party(p, 80f) == 80f && !SocialPassionRules.KeepsWorking(p)
                        && a == 0f && tr == 0f && SocialPassionRules.Gift(p, out _, out _) == 0f && !SocialPassionRules.AddClue(p, 5, 1) && !p.PassionFound
                        && SocialPassionRules.Talk(p, new SocialWorth { Familiarity = 3f, Affection = 2f }).Affection == 2f && SocialPassionGame_PageIsNone(p);
                MomentResult m = SocialMomentRules.Weigh(p, SocialMomentRules.ById(21), 0, true);
                none &= m.Led == SocialTrait.None && m.Passion == 0f && m.Clue == 0;
            }
            Check(none, "traits: with Hidden passions off every rule hands back the ordinary game: no tilt, no liking, no clue, no found passion, no drift, and the weighing ignores it");
            SocialPerson plain = TraitPerson(SocialTrait.Cinephile);
            Check(SocialRules.Liking(plain, PlanKind.Cinema) == SocialRules.Liking(new SocialPerson { Traits = plain.Traits & (SocialTrait)4095 | SocialTrait.Late, Met = true, Seed = 1 }, PlanKind.Cinema)
                  && SocialRules.Grace(plain) == 7, "traits: with Hidden passions off an outing is liked and neglect counted as before");
        }
        finally
        {
            SocialPassionRules.OnSwitch = passions;
        }
        try
        {
            SocialMomentRules.OnSwitch = () => false;
            bool any = false;
            for (int seed = 1; seed <= 1000; seed++)
            {
                SocialPerson p = TraitPerson(SocialTrait.Cinephile, SocialStage.Friend, seed);
                p.Shared = 3 + seed % 5;
                any |= SocialMomentRules.Occurs(p, (PlanKind)(seed % 8));
            }
            Check(!any, "moments: with Moments on outings off, none in a thousand outings");
        }
        finally
        {
            SocialMomentRules.OnSwitch = moments;
        }
        // Passions on, moments off: the tilt stays. Moments on, passions off: they come, and nothing of a passion shows.
        SocialPerson q = TraitPerson(SocialTrait.Cinephile);
        Check(SocialPassionRules.Outing(q, PlanKind.Cinema, TraitFacts(), 2).AffAdd > 0f, "traits: the two options are independent, passions alone still tilt an outing");
    }

    private static bool SocialPassionGame_PageIsNone(SocialPerson p) => !SocialPassionRules.Active(p);

    // ---------------------------------------------------------------- the texts

    private static void TraitTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        if (en == null)
        {
            Check(false, "traits: en.json could not be read");
            return;
        }
        var keys = new List<string>();
        keys.AddRange(SocialPassionCatalogue.Names);
        keys.AddRange(SocialPassionCatalogue.Means);
        keys.AddRange(SocialPassionCatalogue.Cold);
        keys.AddRange(SocialPassionCatalogue.PageHints);
        keys.AddRange(SocialPassionCatalogue.Cooled);
        keys.AddRange(SocialPassionCatalogue.GiftLands);
        keys.AddRange(SocialPassionCatalogue.GiftFlops);
        foreach (string[] row in SocialPassionCatalogue.Found)
        {
            keys.AddRange(row);
        }
        foreach (string[] row in SocialPassionCatalogue.Seen)
        {
            keys.AddRange(row);
        }
        foreach (string[] row in SocialPassionCatalogue.TextHints)
        {
            keys.AddRange(row);
        }
        keys.AddRange(new[] { SocialPassionCatalogue.PageUnknown, SocialPassionCatalogue.PageKnown, SocialPassionCatalogue.HistoryUnknown, SocialPassionCatalogue.HistoryKnown,
            SocialPassionCatalogue.CardFound, SocialPassionCatalogue.CardFoundCool, SocialPassionCatalogue.MomentFound });
        keys.AddRange(SocialMomentCatalogue.Loved);
        keys.AddRange(SocialMomentCatalogue.Liked);
        keys.AddRange(SocialMomentCatalogue.Flat);
        keys.AddRange(SocialMomentCatalogue.Sour);
        keys.AddRange(new[] { SocialMomentCatalogue.ButtonOn, SocialMomentCatalogue.CannotPay, SocialMomentCatalogue.MemorySweet, SocialMomentCatalogue.MemoryTense, SocialMomentCatalogue.MemorySilly,
            SocialMomentCatalogue.MemoryPassion, SocialMomentCatalogue.PlaceFallback, "reallife_social_passions", "reallife_opt_tip_social_passions", "reallife_social_moments",
            "reallife_opt_tip_social_moments", "reallife_social_test_passion" });
        bool built = SocialMomentCatalogue.All.Length == 48 && SocialPassionCatalogue.Names.Length == 10;
        var ids = new HashSet<int>();
        var tones = new Dictionary<PlanKind, HashSet<MomentTone>>();
        bool costed = true;
        foreach (MomentDef d in SocialMomentCatalogue.All)
        {
            built &= ids.Add(d.Id) && d.Reactions.Length == 3 && SocialMomentRules.ById(d.Id) == d && SocialMomentRules.HasMoments(d.Kind);
            keys.Add(d.Situation);
            keys.Add(d.Echo);
            if (!tones.ContainsKey(d.Kind))
            {
                tones[d.Kind] = new HashSet<MomentTone>();
            }
            tones[d.Kind].Add(d.Tone);
            foreach (MomentReaction r in d.Reactions)
            {
                keys.Add(r.Label);
                keys.Add(r.Good);
                keys.Add(r.Flat);
                costed &= r.Cost == 0 || r.Style == MomentStyle.Generous;
            }
        }
        bool mix = tones.Count == 8;
        foreach (HashSet<MomentTone> t in tones.Values)
        {
            mix &= t.Count == 3;
        }
        int missing = 0, style = 0, tall = 0;
        string first = string.Empty;
        foreach (string k in keys)
        {
            if (!en.TryGetValue(k, out string text))
            {
                missing++;
                first = first.Length == 0 ? k : first;
                continue;
            }
            style += text.Contains("!") || text.Contains(";") || text.Contains("—") || text.Contains("–") || text.Contains(" - ") ? 1 : 0;
            tall += (k.StartsWith("reallife_passion_") && k.Contains("_found_") && text.Length > 240) || text.Length > 600 ? 1 : 0;
        }
        Check(built && mix && costed && missing == 0 && style == 0 && tall == 0,
            "traits: 48 moments, six for each of eight outings with all three tones, every line of them and of the passions in en.json, none with a dash, an exclamation mark or a semicolon, none too long (missing " + missing + " " + first + ")");
        Check(en["reallife_opt_tip_social_passions"].Length <= 190 && en["reallife_opt_tip_social_moments"].Length <= 190
              && en["reallife_opt_tip_social_passions"].Split(' ').Length <= 32 && en["reallife_opt_tip_social_moments"].Split(' ').Length <= 32,
            "traits: the two options' tips fit the 32 words and 190 characters");
    }
}
