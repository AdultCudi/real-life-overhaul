using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Two players' suggestions (24 and 26 Nov 2026), checked here: seeing more than one person and being found out, one night stands with
// their outcomes, a child of a night, scandals by how known the player is, and what comes next after living together.
//
// Every rule is game-free (Social/Love/LoveRules.cs) and seeded, so the checks drive the very code the game runs. Each check can fail: the
// factors of the catch chance are each lowered by the check's own comparison, the pregnancy gate is run over twenty thousand seeds, and the
// save is read from an old record, a new one and one from a later version.
internal static partial class Program
{
    private static void Love29()
    {
        LoveCatchChecks();
        LoveAnswerChecks();
        LoveNightChecks();
        LoveChildChecks();
        LoveSaveChecks();
        LoveScandalChecks();
        LoveSourceChecks();
        LoveTextChecks();
    }

    private static SocialPerson LovePerson(float affection, float trust, SocialTrait traits = SocialTrait.None)
    {
        return new SocialPerson { Index = 3, Name = "Dana Whitlock", Female = true, Age = 28, Affection = affection, Trust = trust, Stage = SocialStage.Partner, Traits = traits, Met = true };
    }

    private static CatchFacts LoveBase() => new CatchFacts { Kind = PlanKind.Coffee, Hour = 15 };

    private static void LoveCatchChecks()
    {
        CatchFacts b = LoveBase();
        int baseChance = LoveRules.Chance(b);
        Check(baseChance == LoveRules.KindBase(PlanKind.Coffee) && baseChance > 0, "love: a coffee nobody is near is only the kind's own chance (" + baseChance + " per mille)");
        var up = new List<(string, CatchFacts)>();
        CatchFacts f = b; f.SameDistrict = true; up.Add(("the same district", f));
        f = b; f.Free = true; up.Add(("the watcher's free hours", f));
        f = b; f.Mutual = 2; up.Add(("friends of both", f));
        f = b; f.Tagged = true; up.Add(("a post that tags them", f));
        f = b; f.Close = 1; up.Add(("two plans on one day", f));
        f = b; f.Close = 2; up.Add(("two plans near in time", f));
        f = b; f.Close = 3; up.Add(("two plans near in time and place", f));
        f = b; f.Envious = true; up.Add(("an envious watcher", f));
        f = b; f.Careful = true; up.Add(("a careful watcher", f));
        f = b; f.Punctual = true; up.Add(("a punctual watcher", f));
        f = b; f.Suspicion = 60; up.Add(("suspicion", f));
        f = b; f.Around = true; up.Add(("a night that got around", f));
        f = b; f.LivesTogether = true; up.Add(("living together", f));
        foreach ((string what, CatchFacts facts) in up)
        {
            Check(LoveRules.Chance(facts) > baseChance, "love: " + what + " raises the chance of being found out");
        }
        f = b; f.Denied = true;
        Check(LoveRules.Chance(f) < baseChance, "love: a denial that held lowers it a little");
        f = b; f.Close = 3; int c1 = LoveRules.Chance(f); f.Close = 2; int c2 = LoveRules.Chance(f); f.Close = 1; int c3 = LoveRules.Chance(f);
        Check(c1 > c2 && c2 > c3 && c3 > baseChance, "love: the nearer two plans are in time and place, the likelier");
        CatchFacts park = new CatchFacts { Kind = PlanKind.Walk, Hour = 15 };
        CatchFacts club = new CatchFacts { Kind = PlanKind.Drinks, Hour = 23, SameDistrict = true, Free = true, Mutual = 1 };
        Check(LoveRules.Chance(park) * 5 < LoveRules.Chance(club) && LoveRules.Chance(park) < 20,
            "love: a walk at a far park (" + LoveRules.Chance(park) + ") is much lower than drinks near their home (" + LoveRules.Chance(club) + ")");
        CatchFacts all = new CatchFacts
        {
            Kind = PlanKind.Drinks, SameDistrict = true, Free = true, Mutual = 3, Tagged = true, Close = 3, Envious = true, Careful = true, Punctual = true,
            Suspicion = 100, Around = true, LivesTogether = true, Hour = 23
        };
        Check(LoveRules.Chance(all) == LoveRules.CatchMost && LoveRules.CatchMost < 1000, "love: never quite certain");
        Check(LoveRules.Chance(new CatchFacts { Kind = PlanKind.Lift }) == 0 && LoveRules.Chance(new CatchFacts { Kind = PlanKind.Business }) == 0 && !LoveRules.Public(PlanKind.Moving),
            "love: a lift, a move and a look round a business cannot be seen");
        bool order = true;
        for (int chance = 5; chance <= 800; chance += 5)
        {
            int signs = 0, caught = 0;
            for (int roll = 0; roll < 1000; roll++)
            {
                CatchVerdict v = LoveRules.Verdict(roll, chance);
                signs += v == CatchVerdict.Sign ? 1 : 0;
                caught += v == CatchVerdict.Caught ? 1 : 0;
                order &= v != CatchVerdict.Sign || roll >= chance;
            }
            order &= caught == chance && (signs > 0 || chance + chance / 2 + 40 >= 1000);
        }
        Check(order, "love: a catch is exactly the chance of a thousand rolls, and a sign comes just beyond it, never in its place");
        Check(LoveRules.Verdict(0, 0) == CatchVerdict.Nothing && LoveRules.Settled(60, 10) == 30 && LoveRules.Settled(5, 10) == 0, "love: nothing without a chance, and suspicion cools by the day");
        Check(LoveRules.Closeness(null, 1, 5, 600, 2) == 0, "love: no log, no closeness");
        var log = new LoveFile();
        log.RememberOuting(1, 5, 600, 2);
        Check(LoveRules.Closeness(log, 1, 5, 660, 2) == 3 && LoveRules.Closeness(log, 1, 5, 660, 4) == 2 && LoveRules.Closeness(log, 1, 5, 1100, 2) == 1
              && LoveRules.Closeness(log, 1, 6, 660, 2) == 0 && LoveRules.Closeness(log, 2, 5, 660, 2) == 0,
            "love: two plans one day are near in time, near in time and place, or far apart, and another day or another person is none");
        Check(LoveRules.MayAsk(false, true, false) == false && LoveRules.MayAsk(true, true, false) && !LoveRules.MayAsk(true, true, true) && LoveRules.MayAsk(false, false, false)
              && LoveRules.WarnsBeforeAsking(true, true, false) && !LoveRules.WarnsBeforeAsking(false, true, false) && !LoveRules.WarnsBeforeAsking(true, false, false),
            "love: with the option off nobody can be asked out while taken (as today), with it on the first asking warns, and a third person is never asked");
    }

    private static void LoveAnswerChecks()
    {
        SocialPerson warm = LovePerson(80, 80, SocialTrait.Supportive), cold = LovePerson(40, 40, SocialTrait.Envious | SocialTrait.Careful), mid = LovePerson(60, 60);
        int a = LoveRules.ForgiveChance(warm, LoveAnswer.Confess, CatchHow.Seen, 0, false, false), b = LoveRules.ForgiveChance(cold, LoveAnswer.Confess, CatchHow.Seen, 0, false, false);
        Check(a > b && a <= 900 && b >= 40, "love: the warm and supportive forgive more than the cold and envious");
        Check(LoveRules.ForgiveChance(mid, LoveAnswer.EndOther, CatchHow.Seen, 0, false, false) > LoveRules.ForgiveChance(mid, LoveAnswer.Confess, CatchHow.Seen, 0, false, false),
            "love: ending it with the other forgives more than only confessing");
        Check(LoveRules.ForgiveChance(mid, LoveAnswer.Confess, CatchHow.Seen, 1, false, false) < LoveRules.ForgiveChance(mid, LoveAnswer.Confess, CatchHow.Seen, 0, false, false)
              && LoveRules.ForgiveChance(mid, LoveAnswer.Confess, CatchHow.Seen, 0, true, false) < LoveRules.ForgiveChance(mid, LoveAnswer.Confess, CatchHow.Seen, 0, false, false)
              && LoveRules.ForgiveChance(mid, LoveAnswer.Confess, CatchHow.Child, 0, false, false) < LoveRules.ForgiveChance(mid, LoveAnswer.Confess, CatchHow.Seen, 0, false, false),
            "love: a second chance already used, a lie found out and a child each make forgiving less likely");
        Check(LoveRules.DenyChance(mid, CatchHow.Child, 0, false) == 0 && LoveRules.DenyChance(mid, CatchHow.Post, 0, false) > LoveRules.DenyChance(mid, CatchHow.Seen, 0, false)
              && LoveRules.DenyChance(mid, CatchHow.Seen, 80, false) < LoveRules.DenyChance(mid, CatchHow.Seen, 0, false) && LoveRules.DenyChance(mid, CatchHow.Seen, 0, true) < LoveRules.DenyChance(mid, CatchHow.Seen, 0, false),
            "love: a child cannot be denied, a post is thinner proof than seeing, and suspicion or an earlier lie lowers a denial's chance");
        LoveResult held = LoveRules.Result(LoveAnswer.Deny, false, true, false, 0), fails = LoveRules.Result(LoveAnswer.Deny, false, false, false, 0), lie = LoveRules.Result(LoveAnswer.Deny, false, false, true, 0);
        Check(held.Held && held.Stays && held.Affection == 0 && held.Trust == 0 && !fails.Stays && fails.Trust < 0 && lie.Double && lie.Trust == fails.Trust * 2 && lie.Affection == fails.Affection * 2,
            "love: a denial that holds costs nothing, one that fails costs, and a lie found out costs double");
        LoveResult sorry = LoveRules.Result(LoveAnswer.Confess, true, false, false, 0), second = LoveRules.Result(LoveAnswer.Confess, true, false, false, 1), gone = LoveRules.Result(LoveAnswer.Confess, false, false, false, 0);
        LoveResult endThis = LoveRules.Result(LoveAnswer.EndThis, false, false, false, 0), endOther = LoveRules.Result(LoveAnswer.EndOther, true, false, false, 0);
        Check(sorry.Stays && sorry.Trust < 0 && second.Trust == sorry.Trust * 2 && !gone.Stays && gone.Trust < sorry.Trust && !endThis.Stays && endOther.Stays && endOther.Trust > sorry.Trust,
            "love: being forgiven costs trust, a second time costs double, being left costs most, and ending it with the other costs less than only saying sorry");
        Check(LoveRules.LearnsChance(CatchHow.Child, false) > LoveRules.LearnsChance(CatchHow.Seen, false) && LoveRules.LearnsChance(CatchHow.Seen, true) > LoveRules.LearnsChance(CatchHow.Seen, false),
            "love: the other person is likelier to hear of a child, and an envious person is likelier to tell");
        LoveRules.LearnsFigures(out float la, out float lt);
        Check(la < 0 && lt < 0, "love: the other person who learns loses affection and trust");
        // Determinism: the same cast, day and person give the same roll, and another salt gives its own.
        int seed = LoveRules.SeedOf(12345);
        Check(seed == LoveRules.SeedOf(12345) && seed != LoveRules.SeedOf(12346) && seed > 0, "love: the feature's own seed is the same for the same cast");
        bool same = true, differs = false;
        for (int i = 0; i < 200; i++)
        {
            same &= LoveRules.Roll(seed, 40 + i, 7, LoveRules.SaltCatch) == LoveRules.Roll(seed, 40 + i, 7, LoveRules.SaltCatch);
            differs |= LoveRules.Roll(seed, 40 + i, 7, LoveRules.SaltCatch) != LoveRules.Roll(seed, 40 + i, 7, LoveRules.SaltOffer);
        }
        Check(same && differs, "love: every roll is seeded and repeats, and a roll of another kind is its own (no other roll moves)");
        var seen = new HashSet<int>();
        for (int i = 0; i < 200; i++)
        {
            int v = LoveRules.Variant(seed, 100 + i, 3, 5);
            seen.Add(v);
        }
        Check(seen.Count == 5 && LoveRules.Variant(seed, 1, 1, 1) == 0, "love: the variant of a line takes every one of its pool over a few days");
    }

    private static NightFacts LoveNightBase() => new NightFacts { Kind = NightKind.Drinks, WentWell = true, Affection = 60, Trust = 50, Stage = SocialStage.Friend, Hour = 23 };

    private static void LoveNightChecks()
    {
        NightFacts n = LoveNightBase();
        int baseOffer = LoveRules.OfferChance(n);
        Check(baseOffer > 0 && baseOffer < 400, "love: an evening that went well can lead to an offer, but rarely (" + baseOffer + " per mille)");
        NightFacts bad = n; bad.WentWell = false;
        NightFacts partner = n; partner.Together = true;
        Check(LoveRules.OfferChance(bad) == 0 && LoveRules.OfferChance(partner) == 0, "love: no offer after an evening that went badly, and none to somebody already the partner");
        NightFacts d1 = n; d1.Drinks = 2.4f; NightFacts d2 = n; d2.Drinks = 4f; NightFacts aff = n; aff.Affection = 85; NightFacts late = n; late.Hour = 14;
        Check(LoveRules.OfferChance(d1) > baseOffer && LoveRules.OfferChance(d2) > LoveRules.OfferChance(d1) && LoveRules.OfferChance(aff) > baseOffer && LoveRules.OfferChance(late) < baseOffer,
            "love: drink, affection and the late hour raise the offer's chance");
        NightFacts careful = n; careful.Careful = true; NightFacts owl = n; owl.NightOwl = true;
        Check(LoveRules.OfferChance(careful) < baseOffer && LoveRules.OfferChance(owl) > baseOffer, "love: the careful offer less and night owls more");
        NightFacts y1 = n; y1.Drinks = 3f;
        Check(LoveRules.YesChance(y1) > LoveRules.YesChance(n) && LoveRules.YesChance(careful) < LoveRules.YesChance(n), "love: the more they have had, the likelier they say yes without a second thought");
        var w = new int[6];
        var w2 = new int[6];
        LoveRules.Weights(n, w);
        NightFacts drunk = n; drunk.Drinks = 4f; LoveRules.Weights(drunk, w2);
        Check(w2[(int)NightOutcome.Regret] > w[(int)NightOutcome.Regret] && w2[(int)NightOutcome.Mishap] > w[(int)NightOutcome.Mishap] && w2[(int)NightOutcome.Warm] < w[(int)NightOutcome.Warm],
            "love: drink raises the bad outcomes (regret, mishaps) and lowers warmth");
        NightFacts fond = n; fond.Affection = 85; LoveRules.Weights(fond, w2);
        Check(w2[(int)NightOutcome.Warm] > w[(int)NightOutcome.Warm] && w2[(int)NightOutcome.WantsMore] > w[(int)NightOutcome.WantsMore] && w2[(int)NightOutcome.Awkward] < w[(int)NightOutcome.Awkward],
            "love: affection raises warmth and wanting more, and lowers an awkward morning");
        NightFacts taken = n; taken.Taken = true; LoveRules.Weights(taken, w2);
        Check(w2[(int)NightOutcome.GotAround] > w[(int)NightOutcome.GotAround], "love: when the player is with somebody, word getting round is likelier");
        NightFacts flaky = n; flaky.Flaky = true; LoveRules.Weights(flaky, w2);
        Check(w2[(int)NightOutcome.Awkward] > w[(int)NightOutcome.Awkward], "love: the flaky leave an awkward morning");
        var reached = new int[6];
        bool every = true;
        for (int roll = 0; roll < 1000; roll++)
        {
            NightOutcome o = LoveRules.Pick(roll, w);
            reached[(int)o]++;
            every &= (int)o >= 0 && (int)o <= 5;
        }
        Check(every && Array.TrueForAll(reached, r => r > 0), "love: every outcome of the table can come up");
        int total = 0;
        foreach (int x in w)
        {
            total += x;
        }
        bool share = true;
        for (int i = 0; i < 6; i++)
        {
            share &= Math.Abs(reached[i] - 1000 * w[i] / total) <= 6;
        }
        Check(share, "love: each outcome comes as often as its weight says");
        Check(LoveRules.Pick(500, new int[6]) == NightOutcome.Warm, "love: weights of nothing give a warm morning rather than a fault");
        // The figures and the little things.
        bool figures = true;
        for (int i = 0; i < 6; i++)
        {
            LoveRules.Figures((NightOutcome)i, out float fam, out float a, out float t);
            figures &= fam > 0 && (i == (int)NightOutcome.Warm || i == (int)NightOutcome.WantsMore ? a > 0 && t > 0 : true);
        }
        Check(figures && LoveRules.MoodLifts(NightOutcome.Warm) && LoveRules.MoodDips(NightOutcome.Regret, false) && !LoveRules.MoodDips(NightOutcome.GotAround, false)
              && LoveRules.MoodDips(NightOutcome.GotAround, true), "love: warm nights lift the mood, regret and awkwardness dip it, and word getting round dips it for somebody who is taken");
        bool costs = true;
        for (int v = 0; v < 8; v++)
        {
            int cost = LoveRules.MishapCost(v);
            costs &= cost >= 10 && cost <= 60;
        }
        Check(costs, "love: a mishap costs a few tens of dollars at most");
        Check(LoveRules.NightHours(23) == 9 && LoveRules.NightHours(2) == 6 && LoveRules.NightHours(14) <= 12 && LoveRules.NightHours(21) >= 6, "love: the night is hours that end in the morning (" + LoveRules.NightHours(23) + " from 23:00)");
        // Determinism of a whole night.
        int r1 = LoveRules.Roll(LoveRules.SeedOf(9), 50, 3 * 31 + 23, LoveRules.SaltOutcome), r2 = LoveRules.Roll(LoveRules.SeedOf(9), 50, 3 * 31 + 23, LoveRules.SaltOutcome);
        Check(r1 == r2 && LoveRules.Pick(r1, w) == LoveRules.Pick(r2, w), "love: the same night in the same save gives the same morning");
    }

    private static void LoveChildChecks()
    {
        var ok = new ConceiveFacts { Surprise = true, Children = true, SexesDiffer = true, CarrierAge = 30, Room = 2 };
        Check(LoveRules.MayConceive(ok) && LoveRules.ConceiveChance(ok) > 0, "love: a woman and a man of an age, with the options on and room, can have a child");
        ConceiveFacts g = ok; g.Surprise = false;
        ConceiveFacts g2 = ok; g2.Children = false;
        ConceiveFacts g3 = ok; g3.SexesDiffer = false;
        ConceiveFacts g4 = ok; g4.CarrierAge = 50;
        ConceiveFacts g5 = ok; g5.CarrierAge = 17;
        ConceiveFacts g6 = ok; g6.Room = 0;
        Check(LoveRules.ConceiveChance(g) == 0 && LoveRules.ConceiveChance(g2) == 0 && LoveRules.ConceiveChance(g3) == 0 && LoveRules.ConceiveChance(g4) == 0 && LoveRules.ConceiveChance(g5) == 0
              && LoveRules.ConceiveChance(g6) == 0, "love: never with the option off, Family's children off, the same sex, an age outside 18 to 44 or no room for a child");
        int hits = 0, drunkHits = 0, carefulHits = 0;
        ConceiveFacts drunk = ok; drunk.Drinks = 3f; ConceiveFacts care = ok; care.Careful = true;
        int seed = LoveRules.SeedOf(777);
        int cOk = LoveRules.ConceiveChance(ok), cDrunk = LoveRules.ConceiveChance(drunk), cCare = LoveRules.ConceiveChance(care);
        for (int i = 0; i < 20000; i++)
        {
            int roll = LoveRules.Roll(seed, i, 11, LoveRules.SaltChild);
            hits += roll < cOk ? 1 : 0;
            drunkHits += roll < cDrunk ? 1 : 0;
            carefulHits += roll < cCare ? 1 : 0;
        }
        Check(hits > 20000 * 15 / 1000 && hits < 20000 * 40 / 1000, "love: rare, a few in a hundred over 20,000 seeds (" + hits + " of 20,000)");
        Check(drunkHits > hits && carefulHits < hits, "love: drink makes a child likelier and care less");
        Check(LoveRules.Carrier(true) == 1 && LoveRules.Carrier(false) == 0, "love: the woman carries");
        int news = LoveRules.NewsDay(100, 28), due = LoveRules.DueDay(100, 28), news52 = LoveRules.NewsDay(100, 364), due52 = LoveRules.DueDay(100, 364);
        Check(news > 100 && news < due && due == 100 + RealLifeOverhaul.Family.ChildYearRules.Span(0.75f, 28) && news52 - 100 > news - 100 && due52 - 100 > due - 100 && news52 < due52,
            "love: the news comes part of the way through a pregnancy, timed by the child's year (day " + news + " of " + due + " at four weeks, " + news52 + " of " + due52 + " at 52)");
        Check(LoveRules.SupportAmount(0f) == 400 && LoveRules.SupportAmount(6f) == 500 && LoveRules.MonthsDue(100, 130) == 1 && LoveRules.MonthsDue(100, 129) == 0 && LoveRules.MonthsDue(100, 400) == 3
              && LoveRules.MonthsDue(0, 400) == 0, "love: support is a set sum a month, due every thirty days, at most three at a time");
        Check(LoveRules.PartnerLearns(LoveChoice.Involved) == 1000 && LoveRules.PartnerLearns(LoveChoice.Support) < LoveRules.PartnerLearns(LoveChoice.Involved)
              && LoveRules.PartnerLearns(LoveChoice.StayOut) > 0 && LoveRules.PartnerLearns(LoveChoice.None) == 0, "love: a partner hears at once of a child the player is involved in, and by chance of the rest");
        LoveRules.StayOutCosts(out float ma, out float mt, out int standing, out int followers);
        Check(ma < 0 && mt < 0 && standing > 0 && followers > 0, "love: staying out of a child's life costs with Mom, in standing and on Flare");
        // Family's side: a child of a night is a real child with another parent, and away children are not at home.
        string state = ModSource(@"Family\FamilyState.cs"), children = ModSource(@"Family\Children.cs"), night = ModSource(@"Family\Children.Night.cs");
        Check(state.Contains("public string OtherParent;") && state.Contains("public bool Away;") && MemberText(state, "public static int AtHome()").Contains("!S.Children[i].Grown && !S.Children[i].Away")
              && MemberText(state, "public static ChildRecord Smallest()").Contains("!c.Away") && MemberText(state, "public static bool AnyUnder(float years)").Contains("!S.Children[i].Away"),
            "love: a child who lives with the other parent is not at home, not a baby to care for and not a child to wake for");
        Check(MemberText(children, "internal static int WeekCharges(").Contains("c.OtherParent != null ? 1f - FamilyBalance.SupportShare : Share") && MemberText(children, "internal static int WeekCharges(").Contains("c.Away"),
            "love: the other parent of a child of a night pays half of what the child costs, and a child away costs support, not care");
        Check(night.Contains("OtherParent = otherName") && night.Contains("s.Children.Count >= FamilyBalance.MaxChildren") && night.Contains("Settings.FamilyChildren"),
            "love: a child of a night joins Family's children with the other parent named, only inside Family's own limit and option");
    }

    private static void LoveSaveChecks()
    {
        Check(LoveStore.Write(new LoveFile()).Length == 0 && LoveStore.Write(null).Length == 0, "love: a save with no love life writes no line");
        LoveFile none = LoveStore.Read(null), empty = LoveStore.Read(string.Empty);
        Check(none != null && empty != null && none.Second == -1 && none.ConfrontWho == -1 && none.Hooks.Count == 0 && none.Babies.Count == 0 && none.Nights == 0, "love: a save made before it loads as no second person, no nights and no children");
        LoveFile old = LoveStore.Read("1|7|42");
        Check(old != null && old.Second == 7 && old.SecondSince == 42 && old.Suspicion[0] == 0 && old.ConfrontWho == -1 && old.DateOfferWho == -1 && old.ForgivenWho == -1,
            "love: an old record with only its first fields reads, and every field it lacks is none");
        var f = new LoveFile { Second = 12, SecondSince = 30, Forgiven = 1, ForgivenWho = 4, AroundUntil = 80, ConfrontWho = 4, ConfrontOf = 12, ConfrontDay = 61, ConfrontHow = CatchHow.Post,
            DateOfferWho = 9, DateOfferDay = 60, LastOfferDay = 55, LastScandalDay = 50, Nights = 3, LastSupportDay = 40 };
        f.Suspicion[0] = 34; f.Suspicion[1] = 5; f.SignDay[0] = 58; f.DeniedDay[1] = 44; f.Knows[1] = 1; f.TagDay[0] = 57;
        f.RememberOuting(12, 60, 1190, 3);
        f.RememberHook(new LoveHook { Person = 9, Day = 55, Hour = 23, Outcome = NightOutcome.Awkward, Drinks = 25, Taken = true, Mine = true, TextDay = 0, QuietUntil = 61, Around = true });
        f.Babies.Add(new LoveBaby { Person = 9, Day = 55, NewsDay = 63, DueDay = 76, Carrier = 1, Told = true, Choice = LoveChoice.Support, ChoiceDay = 64, Born = false, Girl = true, Name = "Maya", Support = 400,
            PaidDay = 76, Behind = 1, Partner = true });
        string text = LoveStore.Write(f);
        LoveFile back = LoveStore.Read(text);
        Check(back != null && LoveStore.Write(back) == text, "love: a record with every field reads back and writes the same line");
        Check(back.Second == 12 && back.Suspicion[0] == 34 && back.DeniedDay[1] == 44 && back.Knows[1] == 1 && back.ConfrontHow == CatchHow.Post && back.Nights == 3 && back.Hooks[0].Outcome == NightOutcome.Awkward
              && back.Hooks[0].QuietUntil == 61 && back.Hooks[0].Around && back.Outings[0].District == 3 && back.Babies[0].Choice == LoveChoice.Support && back.Babies[0].Name == "Maya" && back.Babies[0].Partner
              && back.Babies[0].Support == 400, "love: the second, who is suspicious, who has found out, the nights and the child all come back");
        LoveFile later = LoveStore.Read(text + "\nZ|a|b\nO|1|2|3|4");
        Check(later != null && later.Kept.Count == 1 && later.Kept[0] == "Z|a|b" && LoveStore.Write(later).Contains("Z|a|b") && later.Outings.Count == 2,
            "love: a line of a later version is kept as it was read and written back");
        Check(LoveStore.Read("2|1|1") == null && LoveStore.Read("garbage") == null, "love: a record of another version or a damaged one is not read, so the game keeps it as it is");
        LoveFile shortHook = LoveStore.Read("1|-1|0\nH|1|2|3|0|0|0|0|0\nB|1|2|3|4|0|0|0|0|0");
        Check(shortHook != null && shortHook.Hooks.Count == 1 && shortHook.Babies.Count == 1 && shortHook.Hooks[0].QuietUntil == 0 && shortHook.Babies[0].Support == 0,
            "love: an older night or child line without its later fields reads with them as none");
        var many = new LoveFile();
        for (int i = 0; i < 20; i++)
        {
            many.RememberOuting(1, i, 0, 0);
            many.RememberHook(new LoveHook { Person = i });
        }
        Check(many.Outings.Count == LoveFile.OutingsMost && many.Hooks.Count == LoveFile.HooksMost && many.Hooks[0].Person == 20 - LoveFile.HooksMost, "love: the log of outings and nights keeps the latest only");
        LoveFile ended = LoveStore.Read(text);
        ended.EndSecond();
        Check(ended.Second == -1 && ended.Suspicion[0] == 0 && ended.Knows[1] == 0 && ended.Outings.Count == 0 && ended.Nights == 3, "love: ending the second relationship clears what was secret and keeps the nights");
        Check((int)MemoryCode.PartyTheirs == 30 && (int)MemoryCode.Night == 31 && (int)MemoryCode.CoParent == 37, "love: the new memories go at the end, after every one a save may hold");
        Check((int)NightOutcome.Mishap == 5 && (int)CatchHow.Child == 4 && (int)LoveChoice.StayOut == 3, "love: the saved numbers are fixed");
    }

    private static void LoveScandalChecks()
    {
        ScandalScale unknown = LoveRules.Scandal(5, false, false), local = LoveRules.Scandal(20, false, false), known = LoveRules.Scandal(40, false, false), famous = LoveRules.Scandal(90, false, false);
        Check(unknown.Gossip && unknown.Headline == 0 && unknown.FlareLost == 0 && unknown.Dent == 0, "love: an unknown player gets no headline, only gossip among friends");
        Check(local.Headline > 0 && local.Dent == 0 && known.Headline > local.Headline && known.Dent > 0 && famous.Headline == 100 && famous.Dent > known.Dent && famous.FlareGained > famous.FlareLost,
            "love: the more the player is known, the likelier the paper, the bigger the dent, and a scandal draws more followers than it loses");
        ScandalScale child = LoveRules.Scandal(40, true, false), bound = LoveRules.Scandal(40, false, true);
        Check(child.Headline > known.Headline && bound.Headline > known.Headline && child.FlareLost > known.FlareLost, "love: a child counts a half more, a marriage or an engagement a third");
        Check(LoveRules.ScandalMay(0, 10) && !LoveRules.ScandalMay(10, 12) && LoveRules.ScandalMay(10, 17), "love: the paper prints a scandal at most once a week");
    }

    private static void LoveSourceChecks()
    {
        string hooks = ModSource(@"Social\Love\LoveHooks.cs"), life = ModSource(@"Social\Love\LoveLife.cs"), night = ModSource(@"Social\Love\LoveNight.cs"), babies = ModSource(@"Social\Love\LoveBabies.cs");
        string person = ModSource(@"Social\PersonDialog.cs"), people = ModSource(@"Social\PeopleDialog.cs"), state = ModSource(@"Social\SocialState.cs"), outing = ModSource(@"Social\OutingScene.After.cs");
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), list = ModSource(@"Meta\FeatureList.Life.cs"), tree = ModSource(@"Meta\OptionsTree.cs");
        // All options off: the same as today.
        Check(hooks.Contains("public static bool SeeingOn => Settings.Social && Settings.LoveSeeing;") && hooks.Contains("public static bool CanDate => LoveRules.MayAsk(SeeingOn, Taken, HasSecond);")
              && person.Contains("if (LoveHooks.MayAskOut(_p))") && person.Contains("&& LoveHooks.CanDate)") && !person.Contains("SocialState.Partner == null"),
            "love: with Seeing more than one person off, asking out is offered and accepted only while single, as before");
        Check(night.Contains("if (!LoveHooks.HookupsOn || !LoveState.Loaded || !Eligible(p))") && MemberText(ModSource(@"Social\OutingScene.Love.cs"), "private void LoveSettled()").Contains("LoveHooks.HookupsOn")
              && MemberText(hooks, "public static bool HookupsOn").Length > 0 && hooks.Contains("Settings.LoveHookups && !GoingPublic.ClockShared"),
            "love: with One night stands off nothing is offered and a drinks outing counts no drinks; in a shared-clock session no night can pass");
        Check(babies.Contains("if (!LoveHooks.SurpriseOn || !LoveState.Loaded)") && hooks.Contains("HookupsOn && Settings.LoveSurprise && Settings.Family && Settings.FamilyChildren"),
            "love: with Surprise pregnancies off, or Family's children off, no child comes of a night");
        Check(life.Contains("if (!LoveHooks.SeeingOn || !LoveState.Loaded || who == null || !LoveRules.Public(kind))"), "love: with the second relationship off no outing is rolled against being found out");
        Check(state.Contains("if (LoveState.IsSecond(i))") && state.Contains("return second;"), "love: the partner Family sees is the first, and the second only when nobody else is together with the player");
        Check(ModSource(@"Social\SocialCardText.cs").Contains("LoveHooks.CanDate, out float need") && outing.Contains("SocialCardText.NextOf("), "love: the card's step for dating follows the same rule");
        // Option plumbing.
        Check(settings.Contains("LoveSeeingId = \"love_seeing\"") && settings.Contains("public static bool LoveSeeing = true;") && settings.Contains("public static bool LoveHookups = true;") && settings.Contains("public static bool LoveSurprise = true;")
              && settings.Contains("LoveSurprise = Bool(modId, LoveSurpriseId, LoveSurprise);"), "love: three options, on by default, read from the settings");
        Check(mod.Contains(".AddToggle(Settings.LoveSeeingId, \"reallife_love_seeing\"") && mod.Contains(".AddToggle(Settings.LoveHookupsId, \"reallife_love_hookups\"") && mod.Contains(".AddToggle(Settings.LoveSurpriseId, \"reallife_love_surprise\""),
            "love: each option is on the options page with its label");
        Check(list.Contains("OnFrom(Settings.LoveSeeingId") && list.Contains("OnFrom(Settings.LoveHookupsId") && list.Contains("OnFrom(Settings.LoveSurpriseId") && list.Contains("\"reallife:love\"")
              && tree.Contains("S(\"love\")") && tree.Contains("\"love_seeing\", \"love_hookups\"") && tree.Contains("Opt(\"love_surprise\", \"love_hookups\", \"family_children\")"),
            "love: the options are in the feature list (with their presets and the save line) and in the options tree");
        // Going Public: nothing personal is shared.
        string[] files = { "LoveFile.cs", "LoveStore.cs", "LoveRules.cs", "LoveState.cs", "LoveLife.cs", "LoveNight.cs", "LoveBabies.cs", "LoveTalk.cs", "LoveHooks.cs", "LoveCatalogue.cs" };
        bool nothingSent = true;
        foreach (string file in files)
        {
            string text = ModSource(@"Social\Love\" + file).Replace("ClockShared", string.Empty).Replace("p.Shared", string.Empty);
            nothingSent &= !text.Contains("Share") && !text.Contains("Forward") && !text.Contains("Network") && (!text.Contains("GoingPublic") || file == "LoveHooks.cs");
        }
        Check(nothingSent && hooks.Contains("!GoingPublic.ClockShared") && 10 >= RealLifeOverhaul.Ledger.LedgerShare.CityKinds && !RealLifeOverhaul.Ledger.LedgerShare.CityKind(10),
            "love: Going Public: nothing personal is sent (no shared, forwarded or networked part), the City Ledger's story stays on this machine, and no night passes under a shared clock");
        // Married players go through Family's own flow.
        string affairs = ModSource(@"Family\FamilyAffairs.cs");
        Check(affairs.Contains("HardTimes.Separate(theirs)") && life.Contains("LoveLinks.Separate(!byPlayer)") && !life.Contains("FamilyState") && !life.Contains("Family."),
            "love: a partner who lives with the player, is engaged or married leaves through Family's own separation, never a second flow");
        Check(ModSource(@"Family\PartnerVoice.cs").Contains("case FamilyAffairs.ConfrontKind: return FamilyBridges.LoveTalk;") && affairs.Contains("s.PAsk = ConfrontKind;"),
            "love: at home the partner waits through Family's own ask, and speaking to them opens the talk");
        Check(ModSource(@"Economy\BizLines.cs").Contains("P + \"love_\"") && ModSource(@"Economy\BizLines.cs").Contains("P + \"love_support\", P + \"love_mishap\"")
              && RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_love_support") == RealLifeOverhaul.Economy.BizLine.Capital && RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_love_mishap") == RealLifeOverhaul.Economy.BizLine.Capital
              && RealLifeOverhaul.Economy.BizLines.Audit(new[] { "reallife_transaction_love_support", "reallife_transaction_love_mishap" }) == null,
            "love: the support payment and a mishap are in Economy/BizLines.cs, as the player's own money");
        Check(babies.Contains("\"reallife_transaction_love_support\"") && life.Length > 0 && night.Contains("\"reallife_transaction_love_mishap\"") && MemberText(babies, "private static void Support(").Contains("MonthsDue"),
            "love: the support is a monthly transaction through the bank");
    }

    private static void LoveTextChecks()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var keys = new HashSet<string>();
        string[] sources = { @"Social\Love\LoveCatalogue.cs", @"Social\Love\LoveLife.cs", @"Social\Love\LoveTalk.cs", @"Social\Love\LoveNight.cs", @"Social\Love\LoveBabies.cs", @"Social\Love\LoveHooks.cs",
            @"Social\Love\LoveRules.cs", @"Social\SocialDirector.Love.cs", @"Family\Children.Night.cs", @"Family\FamilyAffairs.cs", @"Core\RealLifeOverhaulMod.cs", @"Meta\OptionsTree.cs" };
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_(?:love|transaction_love|family_memory_born_away|opt_tip_love|opt_sec_love)[a-z_0-9]*)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        var missing = new List<string>();
        foreach (string key in keys)
        {
            if (!en.ContainsKey(key) && !key.EndsWith("_", StringComparison.Ordinal))
            {
                missing.Add(key);
            }
        }
        Check(missing.Count == 0 && keys.Count > 150, "love: every text key the code names is in en.json (" + keys.Count + " keys)" + (missing.Count > 0 ? ", missing " + string.Join(" ", missing) : string.Empty));
        string[] options = { "reallife_love_seeing", "reallife_opt_tip_love_seeing", "reallife_love_hookups", "reallife_opt_tip_love_hookups", "reallife_love_surprise", "reallife_opt_tip_love_surprise",
            "reallife_opt_sec_love", "reallife_transaction_love_support", "reallife_transaction_love_support_label", "reallife_transaction_love_mishap", "reallife_transaction_love_mishap_label" };
        bool all = true, short_ = true;
        foreach (string key in options)
        {
            all &= en.ContainsKey(key);
            if (key.StartsWith("reallife_opt_tip_", StringComparison.Ordinal) && en.TryGetValue(key, out string tip))
            {
                short_ &= tip.Length <= 190 && tip.Split(' ').Length <= 32;
            }
        }
        Check(all && short_, "love: each option has its label and a tip of at most 32 words and 190 characters, the section its title and each bank transaction its label");
        bool clean = true;
        foreach (string key in keys)
        {
            if (en.TryGetValue(key, out string text))
            {
                clean &= text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && text.IndexOf('!') < 0 && text.IndexOf(';') < 0 && !text.Contains(" - ");
                if (key.EndsWith("_toast", StringComparison.Ordinal))
                {
                    clean &= text.Length <= 240;
                }
            }
        }
        Check(clean, "love: no text has a dash, an exclamation mark or a semicolon, and a notice is at most 240 characters");
        Check(en.TryGetValue("reallife_guide_social", out string guide) && guide.Contains("More than one person") && guide.Contains("Nights out") && guide.Contains("From dating to marriage")
              && en["reallife_guide_family"].Contains("Children of a night"), "love: the Guide explains the whole path from dating to marriage, hard times and divorce, and what the love life adds");
        bool next = false;
        foreach (KeyValuePair<string, string> pair in en)
        {
            next |= pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && pair.Key != "reallife_changes_title" && pair.Value.Contains("see a second person in secret");
        }
        Check(next, "love: What's new tells of it, in some row");
        string wiring = ModSource(@"Wiring\Wiring.LoveBridges.cs"), page = ModSource(@"Social\PeopleDialog.cs");
        Check(page.Contains("LoveLinks.NextStep(p)") && wiring.Contains("FamilyAffairs.NextStep(out int days)") && ModSource(@"Family\FamilyAffairs.cs").Contains("\"reallife_love_next_ring\"")
              && ModSource(@"Family\FamilyAffairs.cs").Contains("\"reallife_love_next_plan\"") && ModSource(@"Family\FamilyAffairs.cs").Contains("\"reallife_love_next_wedding\""),
            "love: the partner's page in People says what comes next after living together: the ring, the proposal and the wedding planner");
    }
}
