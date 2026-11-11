using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Schemes;
using SchemeOffice = RealLifeOverhaul.Schemes.Office;

// Schemes and inquiries (Scripts/Schemes): the game-free rules the director calls, driven here. Every figure of the
// catalogue, the trail and the line an inquiry opens at, an inquiry from its quiet opening to its end, paying back,
// telling everything and a lost file (this file); the money of each scheme, the people and the save (SchemeChecks.Money.cs);
// ten game years and more of every plan and the rivals' side (SchemeChecks.Sim.cs); the pages, the sources, the texts
// and the cost (SchemeChecks.Pages.cs).
internal static partial class Program
{
    private static bool _schemeTable;   // the simulation's rows are printed too (--schemes table)

    private const string SchemePlace = "Elm~7";

    private static void Schemes()
    {
        SchemeFigureChecks();
        SchemeTrailChecks();
        SchemeInquiryChecks();
        SchemeRepayChecks();
        SchemeMoneyChecks();
        SchemeWashChecks();
        SchemePeopleChecks();
        SchemeFundChecks();
        SchemeStoreChecks();
        SchemeSaveLoadChecks();
        SchemeSimChecks();
        SchemeRivalChecks();
        SchemePageChecks();
        SchemeFixChecks();
        SchemeSourceChecks();
        SchemeTextChecks();
        SchemeTimingChecks();
    }

    /// <summary>A world with one business that takes cash at a counter, the option on, and the game's tax at 20%.</summary>
    private static SchemeWorld SchemeWorldOf(bool on = true, int average = 3000)
    {
        var w = new SchemeWorld { On = on, TaxPercent = 20 };
        w.Tills.Add(new SchemeTill { Place = SchemePlace, Average = average, Cash = true });
        return w;
    }

    /// <summary>A new save whose clock is set at a day, as the first day's turn sets it.</summary>
    private static SchemeState SchemeStateOf(int seed, int day, SchemeWorld w)
    {
        var s = new SchemeState { Seed = seed };
        SchemeRules.Day(s, day, w, new List<SchemeEvent>());
        return s;
    }

    /// <summary>The first seed, counted from 1, that a test holds for: a save chosen by what it is, never by luck.</summary>
    private static int SchemeSeedWhere(Func<int, bool> want)
    {
        for (int i = 1; i < 5000; i++)
        {
            int seed = SchemeRules.SeedOf("seed " + i.ToString(CultureInfo.InvariantCulture));
            if (want(seed))
            {
                return seed;
            }
        }
        return 0;
    }

    /// <summary>Plays day after day until an event of a kind comes, up to a last day. The day it came on, or -1; <paramref name="day"/> is left on the last day played.</summary>
    private static int SchemeUntil(SchemeState s, ref int day, int last, SchemeWorld w, SchemeEventKind kind, out SchemeEvent hit, List<SchemeEventKind> seen = null)
    {
        var events = new List<SchemeEvent>();
        hit = default;
        while (day < last)
        {
            day++;
            events.Clear();
            SchemeRules.Day(s, day, w, events);
            int at = -1;
            for (int i = 0; i < events.Count; i++)
            {
                seen?.Add(events[i].Kind);
                if (events[i].Kind == kind && at < 0)
                {
                    at = i;
                }
            }
            if (at >= 0)
            {
                hit = events[at];
                return day;
            }
        }
        return -1;
    }

    private static int SchemeHeat(SchemeState s, SchemeKind kind) => s.Heat[(int)kind];

    // ------------------------------------------------------------------------------------------------- every figure

    private static void SchemeFigureChecks()
    {
        int kinds = Enum.GetValues(typeof(SchemeKind)).Length, favours = Enum.GetValues(typeof(Favour)).Length, offices = Enum.GetValues(typeof(SchemeOffice)).Length;
        Check(SchemeCatalogue.Kinds == kinds && SchemeCatalogue.HeatBase.Length == kinds && new SchemeState().Heat.Length == kinds && new SchemeState().Inquiries.Length == kinds,
            "schemes figures: every kind of scheme has a trail, a base heat and a count of its inquiries (" + kinds + ")");
        Check(SchemeCatalogue.FavourSize.Length == favours && SchemeCatalogue.FavourPrice.Length == favours && SchemeKeys.Favours.Length == favours
              && SchemeKeys.Asks.Length == favours && SchemeKeys.Taken.Length == favours,
            "schemes figures: every favour has a size, a price and its three texts (" + favours + ")");
        bool tempers = SchemeCatalogue.TakesPermille.Length == offices && SchemeCatalogue.ReportsPermille.Length == offices;
        for (int o = 0; tempers && o < offices; o++)
        {
            tempers &= SchemeCatalogue.TakesPermille[o] > 0 && SchemeCatalogue.ReportsPermille[o] > 0 && SchemeCatalogue.TakesPermille[o] + SchemeCatalogue.ReportsPermille[o] < 1000;
            tempers &= o == 0 || (SchemeCatalogue.TakesPermille[o] < SchemeCatalogue.TakesPermille[o - 1] && SchemeCatalogue.ReportsPermille[o] > SchemeCatalogue.ReportsPermille[o - 1]);
        }
        Check(tempers, "schemes figures: in every office some take money, some report it and some only remember; the higher the office, the fewer take and the more report");
        Check(SchemeCatalogue.Small > 0 && SchemeCatalogue.Small < SchemeCatalogue.Large && SchemeCatalogue.Large == 1000, "schemes figures: a small act is a part of a large one, which is the whole");
        Check(SchemeCatalogue.LetterAfter > 0 && SchemeCatalogue.LetterAfter + 2 < SchemeCatalogue.VisitAfter && SchemeCatalogue.VisitAfter < SchemeCatalogue.ChargeAfter
              && SchemeCatalogue.ChargeAfter < SchemeCatalogue.GiveUpAfter,
            "schemes figures: the letter, the days to act on it, the visit, the findings and the giving up come in that order");
        Check(SchemeCatalogue.ChargeFrom / 1000f >= JusticeLaw.DismissBelow && SchemeCatalogue.ChargeFrom < SchemeCatalogue.ChargeAtOnce && SchemeCatalogue.ChargeAtOnce < SchemeCatalogue.EvidenceMost
              && SchemeCatalogue.CoopEvidence <= SchemeCatalogue.EvidenceMost && SchemeCatalogue.EvidenceBase >= SchemeCatalogue.ChargeFrom,
            "schemes figures: a file an inquiry charges on is one the prosecutor would take to court (" + SchemeCatalogue.ChargeFrom + " against " + JusticeLaw.DismissBelow + ")");
        Check(SchemeCatalogue.SurchargePermille == 250 && SchemeCatalogue.SurchargeRepaidPermille * 2 == SchemeCatalogue.SurchargePermille,
            "schemes figures: what is found is taken back with a quarter on top, and paying it back first halves that, as the texts say");
        Check(SchemeCatalogue.LiftSteps[0] == 0 && SchemeCatalogue.LiftSteps[1] < SchemeCatalogue.LiftSteps[2] && SchemeCatalogue.LiftSteps[2] == SchemeCatalogue.LiftMost
              && SchemeCatalogue.WashSteps[0] == 0 && SchemeCatalogue.WashSteps[1] < SchemeCatalogue.WashSteps[2] && SchemeCatalogue.WashSteps[2] <= 250 && SchemeCatalogue.WashCut > 0,
            "schemes figures: the books and the till each have a plain step, a little and a lot, and the till never takes more than a quarter of a day's takings");
        Check(SchemeCatalogue.Diligence.Length == 4 && SchemeCatalogue.Diligence[1] > SchemeCatalogue.Diligence[3] && SchemeCatalogue.Diligence[3] > SchemeCatalogue.Diligence[0]
              && SchemeCatalogue.Diligence[0] > SchemeCatalogue.Diligence[2] && SchemeCatalogue.Diligence[1] < 1000,
            "schemes figures: an investor looks hardest at a business's books, then a chain, then a rival, and the staff least");
        Check(SchemeCatalogue.HeatDecay > 0 && SchemeCatalogue.HeatSpill > 0 && SchemeCatalogue.HeatSpill < 500 && SchemeCatalogue.HeatRepeat > 0 && SchemeCatalogue.HeatOnFile > 0
              && SchemeCatalogue.HeatOnFile <= SchemeCatalogue.HeatRepeat && SchemeCatalogue.SpotPermille > 0 && SchemeCatalogue.SpotPermille < 200,
            "schemes figures: suspicion fades, spills a little on the other trails, grows with every earlier act (the recent ones most), and no act is free of a routine check");
        Check(SchemeCatalogue.TrailDays >= 360 && SchemeCatalogue.RepeatDays < SchemeCatalogue.TrailDays && SchemeCatalogue.HoldDays > SchemeCatalogue.GiveUpAfter
              && SchemeCatalogue.OpenPeriodDays > SchemeCatalogue.GiveUpAfter,
            "schemes figures: a file reaches back longer than a pattern is read over, and somebody paid holds what they know longer than an inquiry lasts");
        Check(SchemeCatalogue.HeatSize == 600 && SchemeCatalogue.HeatRepeat == 70 && SchemeCatalogue.RepeatDays == 90 && SchemeCatalogue.HeatOnFile == 50 && SchemeCatalogue.HeatKnows == 40
              && SchemeCatalogue.HeatPaper == 60 && SchemeCatalogue.HeatDecay == 4 && SchemeCatalogue.SpotPermille == 60 && SchemeCatalogue.HeatSpotted == 400
              && SchemeCatalogue.OpenLeast == 250 && SchemeCatalogue.OpenSpread == 400 && SchemeCatalogue.OpenPeriodDays == 90 && SchemeCatalogue.EvidenceBase == 350
              && SchemeCatalogue.EvidencePace == 45 && SchemeCatalogue.LetterAfter == 3 && SchemeCatalogue.VisitAfter == 8 && SchemeCatalogue.ChargeAfter == 14
              && SchemeCatalogue.GiveUpAfter == 30 && SchemeCatalogue.ChargeAtOnce == 800 && SchemeCatalogue.LostFile == 400 && SchemeCatalogue.TrailDays == 720
              && SchemeCatalogue.FelonyFrom == 5000 && SchemeCatalogue.WashCut == 150 && SchemeCatalogue.ReportFrom == 2500 && SchemeCatalogue.PatternFrom == 25000
              && SchemeCatalogue.PatternDays == 30 && SchemeCatalogue.HoldDays == 180 && SchemeCatalogue.TalkPermille == 2 && SchemeCatalogue.TalkPressed == 20
              && SchemeCatalogue.FundReturn == 60 && SchemeCatalogue.FundFeePermille == 60 && SchemeCatalogue.FundDays == 30 && SchemeCatalogue.FundEveryDays == 60 && SchemeCatalogue.FundRecoverPermille == 600
              && SchemeCatalogue.RivalEveryDays == 60 && SchemeCatalogue.RivalCleanPermille == 300 && SchemeCatalogue.InvestigatorShare == 600 && SchemeCatalogue.JudgeLean == 200,
            "schemes figures: the figures are the ones the feature's page, the Guide and the texts were written for: whoever changes one changes those with it");
        Check(SchemeCatalogue.Share(1000, 250) == 250 && SchemeCatalogue.Share(999, 250) == 249 && SchemeCatalogue.Share(-5, 250) == 0 && SchemeCatalogue.Share(100, -5) == 0,
            "schemes figures: a share of a sum is rounded down and never negative");
        Check(SchemeCatalogue.AtPrices(5000, 1000) == 5000 && SchemeCatalogue.AtPrices(5000, 1500) == 7500 && SchemeCatalogue.AtPrices(5000, 0) == 5,
            "schemes figures: a sum written at the start's prices is asked at today's level");
        Check(SchemeState.Rivals == 3 && ModSource(@"Rivals\RivalCatalogue.cs").Contains("public const int TycoonCount = 3"),
            "schemes figures: the schemes keep a trail for each rival tycoon there is");
        Check(SchemeCatalogue.FundReturn <= SchemeCatalogue.FundFeePermille && SchemeCatalogue.FundRecoverPermille < 1000 && SchemeCatalogue.FundAnswerDays < SchemeCatalogue.FundDays
              && SchemeCatalogue.FundDays < SchemeCatalogue.FundEveryDays,
            "schemes figures: an honest fund never pays more than having it looked at costs (so looking first never pays on average), a sham's money never comes back whole, and one fund is over before the next is offered");
        foreach (string id in new[] { JusticeLaw.Fraud, JusticeLaw.FalseAccounting, JusticeLaw.MoneyLaundering, JusticeLaw.PervertingJustice, JusticeLaw.Bribery, JusticeLaw.TaxEvasion })
        {
            ChargeDef def = JusticeLaw.Find(id);
            Check(def != null && def.Fine > 0 && !string.IsNullOrEmpty(def.NameKey), "schemes figures: the courts know the charge " + id);
        }
        ChargeDef fraud = JusticeLaw.Find(JusticeLaw.Fraud), lesser = JusticeLaw.Find(JusticeLaw.FalseAccounting), wash = JusticeLaw.Find(JusticeLaw.MoneyLaundering);
        ChargeDef pervert = JusticeLaw.Find(JusticeLaw.PervertingJustice);
        Check(fraud != null && lesser != null && wash != null && pervert != null && fraud.Class == ChargeClass.Felony && lesser.Class == ChargeClass.Misdemeanour
              && wash.Class == ChargeClass.Felony && pervert.Class == ChargeClass.Felony && fraud.LesserId == JusticeLaw.FalseAccounting && wash.LesserId == JusticeLaw.FalseAccounting
              && pervert.LesserId == JusticeLaw.Bribery && lesser.PrisonDays == 0 && fraud.PrisonDays > 0 && pervert.PrisonDays > wash.PrisonDays && wash.PrisonDays > fraud.PrisonDays
              && pervert.Fine > wash.Fine && wash.Fine > fraud.Fine && fraud.Fine > lesser.Fine,
            "schemes figures: fraud, laundering and perverting justice are felonies, each graver than the last, with false accounting and bribery as their lesser charges");
    }

    // ------------------------------------------------------------------------------------------------- the trail

    private static void SchemeTrailChecks()
    {
        // The seed and the draw.
        Check(SchemeRules.SeedOf("abc") == SchemeRules.SeedOf("abc") && SchemeRules.SeedOf("abc") != SchemeRules.SeedOf("abd") && SchemeRules.SeedOf(null) == SchemeRules.SeedOf("city")
              && SchemeRules.SeedOf(string.Empty) == SchemeRules.SeedOf("city") && SchemeRules.SeedOf("abc") != 0,
            "schemes trail: a save's seed is its playthrough's own name as a number, the same on every load and never nothing");
        var deciles = new int[10];
        bool inRange = true, fixedDraw = true;
        for (int i = 0; i < 100000; i++)
        {
            int roll = SchemeRules.Roll(77, 3, i, i / 7);
            inRange &= roll >= 0 && roll < 1000;
            fixedDraw &= i > 500 || roll == SchemeRules.Roll(77, 3, i, i / 7);
            deciles[Math.Min(9, Math.Max(0, roll) / 100)]++;
        }
        bool even = true;
        for (int d = 0; d < 10; d++)
        {
            even &= deciles[d] > 9400 && deciles[d] < 10600;
        }
        Check(inRange && fixedDraw && even, "schemes trail: a draw is 0 to 999, the same for the same question, and even over a hundred thousand of them");
        int differs = 0;
        for (int i = 0; i < 200; i++)
        {
            int roll = SchemeRules.Roll(5, 6, i, 8);
            differs += roll != SchemeRules.Roll(6, 6, i, 8) ? 1 : 0;
            differs += roll != SchemeRules.Roll(5, 7, i, 8) ? 1 : 0;
            differs += roll != SchemeRules.Roll(5, 6, i + 1, 8) ? 1 : 0;
            differs += roll != SchemeRules.Roll(5, 6, i, 9) ? 1 : 0;
        }
        Check(differs > 780, "schemes trail: the seed, what the draw is about and both its figures each change it (" + differs + " of 800)");
        Check(SchemeRules.Text("Elm~7") == JusticeLaw.Hash("Elm~7") && SchemeRules.Text(null) == SchemeRules.Text(string.Empty) && SchemeRules.Text("a") != SchemeRules.Text("b"),
            "schemes trail: a text's number is the courts' own hash, the same on every machine");

        // What an act adds.
        int small = SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 0, 0, 0, true), large = SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Large, 0, 0, 0, true);
        Check(small == SchemeCatalogue.HeatBase[0] + SchemeCatalogue.HeatSize * SchemeCatalogue.Small / 1000 + SchemeCatalogue.HeatPaper && large == SchemeCatalogue.HeatBase[0]
              + SchemeCatalogue.HeatSize + SchemeCatalogue.HeatPaper,
            "schemes trail: an act's heat is its kind's, its size's and its paper trail's: a small claim " + small + ", a large one " + large);
        Check(SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 1, 0, 0, true) - small == SchemeCatalogue.HeatRepeat
              && SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 3, 0, 0, true) - small == 3 * SchemeCatalogue.HeatRepeat
              && SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 99, 0, 0, true) - small == SchemeCatalogue.RepeatMost * SchemeCatalogue.HeatRepeat
              && SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 0, 2, 0, true) - small == 2 * SchemeCatalogue.HeatOnFile
              && SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 0, 99, 0, true) - small == SchemeCatalogue.OnFileMost * SchemeCatalogue.HeatOnFile
              && SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 0, 0, 3, true) - small == 3 * SchemeCatalogue.HeatKnows
              && small - SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Small, 0, 0, 0, false) == SchemeCatalogue.HeatPaper,
            "schemes trail: each recent act of the kind, each older one still on file, each person who knows and a paper trail add their own, as far as each is counted");
        Check(SchemeRules.HeatOf(SchemeKind.Club, SchemeCatalogue.Large, 99, 99, 99, true) == SchemeCatalogue.HeatMost && SchemeRules.HeatOf(SchemeKind.Wash, 0, 0, 0, 0, false) == 0
              && SchemeRules.HeatOf(SchemeKind.Claim, -5, 0, 0, -3, false) == SchemeCatalogue.HeatBase[0] && SchemeRules.HeatOf(SchemeKind.Claim, 5000, 0, 0, 0, false)
              == SchemeRules.HeatOf(SchemeKind.Claim, SchemeCatalogue.Large, 0, 0, 0, false),
            "schemes trail: an act's heat is bounded, and a size or a count out of range is brought into it");

        // The line.
        bool within = true, steady = true;
        int least = int.MaxValue, most = 0, byPeriod = 0, byKind = 0, byCount = 0;
        for (int i = 0; i < 4000; i++)
        {
            int seed = SchemeRules.SeedOf("line " + i.ToString(CultureInfo.InvariantCulture));
            int line = SchemeRules.Line(seed, SchemeKind.Claim, 10, 0);
            within &= line >= SchemeCatalogue.OpenLeast && line < SchemeCatalogue.OpenLeast + SchemeCatalogue.OpenSpread;
            steady &= line == SchemeRules.Line(seed, SchemeKind.Claim, SchemeCatalogue.OpenPeriodDays - 1, 0) && line == SchemeRules.Line(seed, SchemeKind.Claim, 0, 0);
            least = Math.Min(least, line);
            most = Math.Max(most, line);
            byPeriod += line != SchemeRules.Line(seed, SchemeKind.Claim, SchemeCatalogue.OpenPeriodDays, 0) ? 1 : 0;
            byKind += line != SchemeRules.Line(seed, SchemeKind.Expense, 10, 0) ? 1 : 0;
            byCount += line != SchemeRules.Line(seed, SchemeKind.Claim, 10, 1) ? 1 : 0;
        }
        Check(within && steady && least < SchemeCatalogue.OpenLeast + 10 && most > SchemeCatalogue.OpenLeast + SchemeCatalogue.OpenSpread - 10,
            "schemes trail: the line an inquiry opens at lies between its least and its most and stands for a whole period: " + least + " to " + most);
        Check(byPeriod > 3900 && byKind > 3900 && byCount > 3900, "schemes trail: the line is drawn again for each period, each kind and after each inquiry, so none is safe for ever");
        Check(SchemeRules.Band(0) == 0 && SchemeRules.Band(SchemeCatalogue.OpenLeast / 2 - 1) == 0 && SchemeRules.Band(SchemeCatalogue.OpenLeast / 2) == 1
              && SchemeRules.Band(SchemeCatalogue.OpenLeast - 1) == 1 && SchemeRules.Band(SchemeCatalogue.OpenLeast) == 2 && SchemeRules.Band(SchemeCatalogue.HeatMost) == 2,
            "schemes trail: a trail is calm, talked about, or watched from the least an inquiry can open at");

        // What a page says: the odds of a first act, against the saves themselves.
        foreach (SchemeKind kind in SchemeMoneyKinds)
        {
            bool club = kind == SchemeKind.Club, books = kind == SchemeKind.Books;
            int size = books ? SchemeCatalogue.LiftSteps[1] * 1000 / SchemeCatalogue.LiftMost : SchemeCatalogue.Small, knows = club ? SchemeCatalogue.ClubSmall / SchemeCatalogue.ClubKnowsEvery : 0;
            int heat = SchemeRules.HeatOf(kind, size, 0, 0, knows, !club), opens = 0;
            const int saves = 20000;
            for (int i = 0; i < saves; i++)
            {
                int seed = SchemeRules.SeedOf("odds " + i.ToString(CultureInfo.InvariantCulture));
                opens += heat + (SchemeRules.Spotted(seed, kind, 0) ? SchemeCatalogue.HeatSpotted : 0) >= SchemeRules.Line(seed, kind, 30, 0) ? 1 : 0;
            }
            int said = SchemeRules.FirstChance(kind, size, knows, !club), fullSize = SchemeCatalogue.Large;
            Check(Math.Abs(opens * 1000 / saves - said * 10) <= 15 && said >= 5 && said <= 45,
                "schemes trail: a first small " + kind + " passes its line in " + opens * 100.0 / saves + "% of saves, and its page says " + said + "%");
            Check(SchemeRules.FirstChance(kind, fullSize, club ? SchemeCatalogue.ClubLarge / SchemeCatalogue.ClubKnowsEvery : 0, !club) == 100
                  && SchemeRules.HeatOf(kind, fullSize, 0, 0, 0, !club) - SchemeCatalogue.HeatDecay >= SchemeCatalogue.OpenLeast + SchemeCatalogue.OpenSpread,
                "schemes trail: a large " + kind + " is over every line there can be, a day later too: always looked into, as its page says");
        }

        // An act goes on its trail.
        SchemeWorld w = SchemeWorldOf();
        int plain = SchemeSeedWhere(seed => !SchemeRules.Spotted(seed, SchemeKind.Expense, 0) && !SchemeRules.Spotted(seed, SchemeKind.Expense, 1)
            && !SchemeRules.Spotted(seed, SchemeKind.Expense, 2));
        SchemeState s = SchemeStateOf(plain, 10, w);
        SchemeAct first = SchemeRules.Expensed(s, 10, 3000, SchemeCatalogue.Small);
        int one = SchemeRules.HeatOf(SchemeKind.Expense, SchemeCatalogue.Small, 0, 0, 0, true);
        bool spilt = true;
        for (int k = 0; k < SchemeCatalogue.Kinds; k++)
        {
            spilt &= k == (int)SchemeKind.Expense || s.Heat[k] == one * SchemeCatalogue.HeatSpill / 1000;
        }
        Check(SchemeHeat(s, SchemeKind.Expense) == one && spilt && s.Acted == 1 && s.Acts.Count == 1 && first.Gain == 3000 && first.Paper && first.Inquiry == 0 && first.Owed == 3000
              && s.ExpenseDay == 10,
            "schemes trail: an act is recorded once, with its heat on its own trail and a tenth of it on every other");
        Check(SchemeRules.Recent(s, SchemeKind.Expense, 10 + SchemeCatalogue.RepeatDays) == 1 && SchemeRules.Older(s, SchemeKind.Expense, 10 + SchemeCatalogue.RepeatDays) == 0
              && SchemeRules.Recent(s, SchemeKind.Expense, 11 + SchemeCatalogue.RepeatDays) == 0 && SchemeRules.Older(s, SchemeKind.Expense, 11 + SchemeCatalogue.RepeatDays) == 1
              && SchemeRules.Older(s, SchemeKind.Expense, 10 + SchemeCatalogue.TrailDays) == 1 && SchemeRules.Older(s, SchemeKind.Expense, 11 + SchemeCatalogue.TrailDays) == 0
              && SchemeRules.Recent(s, SchemeKind.Claim, 20) == 0,
            "schemes trail: an earlier act is part of a pattern for " + SchemeCatalogue.RepeatDays + " days, an older one on file for " + SchemeCatalogue.TrailDays + ", and then it is gone");
        int before = SchemeHeat(s, SchemeKind.Expense);
        SchemeRules.Expensed(s, 10 + SchemeCatalogue.ExpenseGapDays, 3000, SchemeCatalogue.Small);
        Check(SchemeHeat(s, SchemeKind.Expense) - before == one + SchemeCatalogue.HeatRepeat && SchemeHeat(s, SchemeKind.Expense) - before > one,
            "schemes trail: a second act within the pattern's days adds its own heat and the pattern's");
        SchemeState old = SchemeStateOf(plain, 10, w);
        SchemeRules.Expensed(old, 10, 3000, SchemeCatalogue.Small);
        old.Heat[(int)SchemeKind.Expense] = 0;
        SchemeRules.Expensed(old, 11 + SchemeCatalogue.RepeatDays, 3000, SchemeCatalogue.Small);
        Check(SchemeHeat(old, SchemeKind.Expense) == one + SchemeCatalogue.HeatOnFile && SchemeHeat(old, SchemeKind.Expense) > one,
            "schemes trail: an act with an older one still on file adds its own heat and the file's");

        // Fading, by the day.
        SchemeState fade = SchemeStateOf(plain, 10, w);
        fade.Heat[(int)SchemeKind.Books] = 10;
        fade.Heat[(int)SchemeKind.Graft] = 200;
        var events = new List<SchemeEvent>();
        SchemeRules.Day(fade, 11, w, events);
        SchemeRules.Day(fade, 12, w, events);
        SchemeRules.Day(fade, 13, w, events);
        Check(SchemeHeat(fade, SchemeKind.Books) == 0 && SchemeHeat(fade, SchemeKind.Graft) == 200 - 3 * SchemeCatalogue.HeatDecay && events.Count == 0,
            "schemes trail: heat fades by " + SchemeCatalogue.HeatDecay + " a day and never under nothing, and heat with nothing on file opens no inquiry");
        SchemeRules.Day(fade, 50, w, events);
        Check(SchemeHeat(fade, SchemeKind.Graft) == Math.Max(0, 200 - (3 + 37) * SchemeCatalogue.HeatDecay) && fade.Day == 50,
            "schemes trail: a stretch nobody played is caught up day by day, every one of its 37 days");
        var fresh = new SchemeState { Seed = plain };
        SchemeRules.Day(fresh, 300, w, events);
        Check(fresh.Day == 300 && fresh.StartDay == 300 && events.Count == 0, "schemes trail: the first day on a save only sets the clock");

        // Splitting: many small ones are the pattern they are.
        SchemeState split = SchemeStateOf(plain, 10, w), whole = SchemeStateOf(plain, 10, w);
        SchemeRules.Claimed(whole, SchemePlace, 10, 4000, SchemeCatalogue.Large, 1);
        for (int i = 0; i < 4; i++)
        {
            SchemeRules.Claimed(split, SchemePlace, 10, 1000, SchemeCatalogue.Small, 10 + i);
        }
        var q = new SchemeInquiry { Id = 1, Kind = SchemeKind.Claim, Opened = 10 };
        Check(SchemeHeat(split, SchemeKind.Claim) > SchemeHeat(whole, SchemeKind.Claim) && SchemeRules.Holds(split, q, 10) > SchemeRules.Holds(whole, q, 10)
              && SchemeRules.Brought(split, SchemeKind.Claim, 10) == SchemeRules.Brought(whole, SchemeKind.Claim, 10),
            "schemes trail: a sum split into four small claims leaves a hotter trail and a fuller file than the one large claim: heat " + SchemeHeat(split, SchemeKind.Claim) + " against "
            + SchemeHeat(whole, SchemeKind.Claim));
        SchemeState ten = SchemeStateOf(plain, 10, w);
        long each = SchemeCatalogue.FelonyFrom / 10;
        SchemeRules.Record(ten, SchemeKind.Claim, 10, each, SchemeCatalogue.Small, 0, true, SchemePlace, 1);
        bool alone = !SchemeRules.Felony(ten, SchemeKind.Claim, 10, 1000) && SchemeRules.ChargeOf(ten, SchemeKind.Claim, 10, 1000, false) == JusticeLaw.FalseAccounting;
        for (int i = 1; i < 10; i++)
        {
            SchemeRules.Record(ten, SchemeKind.Claim, 10 + i * 60, each, SchemeCatalogue.Small, 0, true, SchemePlace, 1 + i);
        }
        Check(alone && SchemeRules.Felony(ten, SchemeKind.Claim, 600, 1000) && SchemeRules.ChargeOf(ten, SchemeKind.Claim, 600, 1000, false) == JusticeLaw.Fraud
              && SchemeRules.Count(ten, SchemeKind.Claim, 600) == 10 && !SchemeRules.Felony(ten, SchemeKind.Claim, 600, 1100),
            "schemes trail: ten small frauds over six hundred days are charged as the one large fraud they add up to, each of them alone a misdemeanour");

        // The file keeps so many acts of a kind: the oldest found one goes first.
        SchemeState full = SchemeStateOf(plain, 10, w);
        for (int i = 0; i < SchemeCatalogue.ActsMost; i++)
        {
            SchemeRules.Record(full, SchemeKind.Graft, 10 + i, 0, 100, 1, false, string.Empty, i);
        }
        full.Acts[5].Inquiry = 3;
        SchemeRules.Record(full, SchemeKind.Graft, 100, 0, 100, 1, false, string.Empty, 500);
        bool keptOldest = full.Acts[0].Ref == 0, foundGone = true;
        for (int i = 0; i < full.Acts.Count; i++)
        {
            foundGone &= full.Acts[i].Ref != 5;
        }
        SchemeRules.Record(full, SchemeKind.Graft, 101, 0, 100, 1, false, string.Empty, 501);
        Check(full.Acts.Count == SchemeCatalogue.ActsMost && keptOldest && foundGone && full.Acts[0].Ref == 1 && full.Acts[full.Acts.Count - 1].Ref == 501,
            "schemes trail: a kind's file keeps " + SchemeCatalogue.ActsMost + " acts: one already found makes room first, else the oldest");
        SchemeRules.Day(full, 11 + SchemeCatalogue.TrailDays + 6, w, events);
        int still = 0;
        for (int i = 0; i < full.Acts.Count; i++)
        {
            still += full.Acts[i].Day <= 16 ? 1 : 0;
        }
        Check(still == 0 && full.Acts.Count > 0, "schemes trail: what is older than a file reaches is taken off the trails");
    }

    // ------------------------------------------------------------------------------------------------- an inquiry

    private static void SchemeInquiryChecks()
    {
        SchemeWorld w = SchemeWorldOf();
        int seed = SchemeRules.SeedOf("inquiry");
        SchemeState s = SchemeStateOf(seed, 10, w);
        SchemeAct act = SchemeRules.Expensed(s, 10, 12000, SchemeCatalogue.Large);
        var seen = new List<SchemeEventKind>();
        int day = 10;
        int opened = SchemeUntil(s, ref day, 400, w, SchemeEventKind.Opened, out SchemeEvent open, seen);
        SchemeInquiry q = s.Inquiry(open.Inquiry);
        Check(opened == 11 && q != null && q.Kind == SchemeKind.Expense && q.Stage == InquiryStage.Quiet && !q.Known && q.Open && s.Inquiries[(int)SchemeKind.Expense] == 1
              && s.OpenOf(SchemeKind.Expense) == q && q.Evidence == SchemeCatalogue.EvidencePace,
            "schemes inquiry: a large invented expense opens an inquiry the next day, quietly: nobody is told");
        int letter = SchemeUntil(s, ref day, 400, w, SchemeEventKind.Letter, out _, seen);
        Check(letter == opened + SchemeCatalogue.LetterAfter && q.Stage == InquiryStage.Letter && q.Known && seen.Count == 2,
            "schemes inquiry: the letter comes " + SchemeCatalogue.LetterAfter + " days after the opening, and nothing else happens before it");
        int visit = SchemeUntil(s, ref day, 400, w, SchemeEventKind.Visit, out _, seen);
        Check(visit == opened + SchemeCatalogue.VisitAfter && q.Stage == InquiryStage.Visit && !s.Watched && seen.Count == 3,
            "schemes inquiry: the visit comes " + SchemeCatalogue.VisitAfter + " days after the opening; the cash in hand is not watched for a tax return");
        int holds = SchemeRules.Holds(s, q, day);
        int found = SchemeUntil(s, ref day, 400, w, SchemeEventKind.Findings, out SchemeEvent findings, seen);
        Check(holds == SchemeCatalogue.EvidenceBase + SchemeCatalogue.EvidenceAct + SchemeCatalogue.EvidenceSize + SchemeCatalogue.EvidencePaper
              && q.Evidence == Math.Min(holds, (SchemeCatalogue.ChargeAfter + 1) * SchemeCatalogue.EvidencePace),
            "schemes inquiry: the file holds its base, the act, its size and its paper trail (" + holds + "), and evidence is gathered at " + SchemeCatalogue.EvidencePace + " a day up to it");
        Check(found == opened + SchemeCatalogue.ChargeAfter && seen.Count == 4 && findings.Amount == 12000 && findings.Net == 3000 && findings.FromCash == 0
              && findings.Text == JusticeLaw.TaxEvasion && findings.B == 1 && findings.Inquiry == q.Id,
            "schemes inquiry: the findings come " + SchemeCatalogue.ChargeAfter + " days after the opening: everything the file holds taken back, a quarter on top, and the charge");
        Check(q.Stage == InquiryStage.Charged && q.End == InquiryEnd.Charged && q.Found == 12000 && q.Surcharge == 3000 && act.Inquiry == q.Id && act.Owed == 0
              && SchemeRules.Count(s, SchemeKind.Expense, day) == 0 && SchemeRules.Owed(s, SchemeKind.Expense, day) == 0,
            "schemes inquiry: an act is found once: after the findings it owes nothing and is on no file");
        var later = new List<SchemeEvent>();
        for (int d = day + 1; d < day + 60; d++)
        {
            SchemeRules.Day(s, d, w, later);
        }
        Check(later.Count == 0 && q.Stage == InquiryStage.Charged && q.Open, "schemes inquiry: once before the court an inquiry waits for the verdict and does nothing more of its own");
        s.Heat[(int)SchemeKind.Expense] = 900;
        SchemeRules.Close(s, q, InquiryEnd.Convicted, day + 60);
        Check(!q.Open && q.End == InquiryEnd.Convicted && q.Closed == day + 60 && SchemeHeat(s, SchemeKind.Expense) == SchemeCatalogue.OpenLeast && s.OpenOf(SchemeKind.Expense) == null,
            "schemes inquiry: a conviction leaves the trail watched: its heat is brought to the least an inquiry opens at");
        SchemeRules.Close(s, q, InquiryEnd.Cleared, day + 61);
        Check(q.End == InquiryEnd.Convicted && q.Closed == day + 60, "schemes inquiry: an inquiry is closed once");
        later.Clear();
        s.Heat[(int)SchemeKind.Expense] = SchemeCatalogue.HeatMost;
        SchemeRules.Day(s, day + 62, w, later);
        Check(later.Count == 0, "schemes inquiry: with everything found, the hottest trail opens nothing: there is nothing on file");

        // The other ends: cleared and dropped cool the trail by their shares.
        foreach (InquiryEnd end in new[] { InquiryEnd.Cleared, InquiryEnd.Caution, InquiryEnd.Dropped, InquiryEnd.Settled })
        {
            SchemeState e = SchemeStateOf(seed, 10, w);
            var x = new SchemeInquiry { Id = 1, Kind = SchemeKind.Books, Opened = 10, Stage = InquiryStage.Letter };
            e.Cases.Add(x);
            e.Heat[(int)SchemeKind.Books] = 600;
            SchemeRules.Close(e, x, end, 20);
            int want = end == InquiryEnd.Dropped ? 600 * SchemeCatalogue.GaveUpPermille / 1000 : end == InquiryEnd.Settled ? SchemeCatalogue.OpenLeast : 600 * SchemeCatalogue.ClearedPermille / 1000;
            Check(SchemeHeat(e, SchemeKind.Books) == want && !x.Open, "schemes inquiry: an inquiry that ended " + end + " leaves the trail at " + want + " of 600");
        }

        // A small act under the line: nothing opens, and it fades.
        int calm = SchemeSeedWhere(k => !SchemeRules.Spotted(k, SchemeKind.Expense, 0) && SchemeRules.Line(k, SchemeKind.Expense, 10, 0) > 400
                                        && SchemeRules.Line(k, SchemeKind.Expense, 100, 0) > 400);
        SchemeState quiet = SchemeStateOf(calm, 10, w);
        SchemeRules.Expensed(quiet, 10, 3000, SchemeCatalogue.Small);
        later.Clear();
        for (int d = 11; d <= 120; d++)
        {
            SchemeRules.Day(quiet, d, w, later);
        }
        Check(later.Count == 0 && SchemeHeat(quiet, SchemeKind.Expense) == 0 && SchemeRules.Count(quiet, SchemeKind.Expense, 120) == 1 && quiet.Cases.Count == 0,
            "schemes inquiry: a small act under its save's line is not looked into, its heat fades, and it stays on file");

        // The same small act where a routine check spots it.
        int spotted = SchemeSeedWhere(k => SchemeRules.Spotted(k, SchemeKind.Expense, 0) && SchemeRules.Line(k, SchemeKind.Expense, 10, 0) > 400);
        SchemeState unlucky = SchemeStateOf(spotted, 10, w);
        SchemeRules.Expensed(unlucky, 10, 3000, SchemeCatalogue.Small);
        int at = 10;
        Check(SchemeHeat(unlucky, SchemeKind.Expense) == SchemeRules.HeatOf(SchemeKind.Expense, SchemeCatalogue.Small, 0, 0, 0, true) + SchemeCatalogue.HeatSpotted
              && SchemeUntil(unlucky, ref at, 30, w, SchemeEventKind.Opened, out _) == 11,
            "schemes inquiry: a routine check spots an act whatever its size (" + SchemeCatalogue.SpotPermille / 10 + " in a hundred), and that one is looked into");

        // Evidence strong enough is acted on at once, never before the player has had two days with the letter.
        SchemeState many = SchemeStateOf(seed, 10, w);
        for (int i = 0; i < 4; i++)
        {
            SchemeRules.Record(many, SchemeKind.Claim, 10, 2000, SchemeCatalogue.Large, 0, true, SchemePlace, i + 1);
        }
        at = 10;
        int o = SchemeUntil(many, ref at, 60, w, SchemeEventKind.Opened, out _);
        SchemeInquiry strong = many.OpenOf(SchemeKind.Claim);
        strong.Evidence = SchemeCatalogue.EvidenceMost;
        seen.Clear();
        int f = SchemeUntil(many, ref at, 60, w, SchemeEventKind.Findings, out SchemeEvent quick, seen);
        Check(o == 11 && f == o + SchemeCatalogue.LetterAfter + 2 && seen.Contains(SchemeEventKind.Letter) && quick.Amount == 8000 && quick.Text == JusticeLaw.Fraud,
            "schemes inquiry: a file as strong as " + SchemeCatalogue.ChargeAtOnce + " is acted on without waiting, but never sooner than two days after the letter (day " + (f - o) + ")");

        // Telling them everything.
        SchemeState told = SchemeStateOf(seed, 10, w);
        SchemeRules.Record(told, SchemeKind.Claim, 10, 9000, SchemeCatalogue.Large, 0, true, SchemePlace, 1);
        at = 10;
        SchemeUntil(told, ref at, 60, w, SchemeEventKind.Opened, out _);
        SchemeInquiry tq = told.OpenOf(SchemeKind.Claim);
        bool early = SchemeRules.Cooperate(tq);
        SchemeUntil(told, ref at, 60, w, SchemeEventKind.Letter, out _);
        bool first = SchemeRules.Cooperate(tq), second = SchemeRules.Cooperate(tq);
        int toldDay = at;
        int tf = SchemeUntil(told, ref at, 60, w, SchemeEventKind.Findings, out SchemeEvent coop);
        Check(!early && first && !second && tf == toldDay + 1 && tq.Evidence == SchemeCatalogue.CoopEvidence && coop.Text == JusticeLaw.FalseAccounting && coop.B == 1
              && coop.Amount == 9000 && coop.Net == 2250,
            "schemes inquiry: telling the investigators everything can be done once, from the letter on: the findings follow the next day, the file complete, the charge the lesser one");
        Check(!SchemeRules.Cooperate(tq) && !SchemeRules.Cooperate(null), "schemes inquiry: nothing can be told once the file is before the court");

        // A lost file: one act's worth, never a pattern's.
        SchemeState lost = SchemeStateOf(seed, 10, w);
        SchemeRules.Expensed(lost, 10, 12000, SchemeCatalogue.Large);
        at = 10;
        int lo = SchemeUntil(lost, ref at, 60, w, SchemeEventKind.Letter, out _) - SchemeCatalogue.LetterAfter;
        SchemeInquiry lq = lost.OpenOf(SchemeKind.Expense);
        SchemeRules.LoseFile(lq);
        lost.Heat[(int)SchemeKind.Expense] = 500;
        seen.Clear();
        int dropped = SchemeUntil(lost, ref at, 100, w, SchemeEventKind.Dropped, out _, seen);
        Check(lq.Lost && dropped == lo + SchemeCatalogue.GiveUpAfter && !seen.Contains(SchemeEventKind.Findings) && lq.End == InquiryEnd.Dropped
              && SchemeRules.Count(lost, SchemeKind.Expense, at) == 1 && lq.Evidence < SchemeCatalogue.ChargeFrom,
            "schemes inquiry: with the investigator bought a single act's file never stands, and the inquiry is dropped after " + SchemeCatalogue.GiveUpAfter
            + " days; the act stays on file for the next one");
        SchemeState pattern = SchemeStateOf(seed, 40, w);   // the clock stands where the last act is, as it would have run day by day
        SchemeRules.Expensed(pattern, 10, 3000, SchemeCatalogue.Small);
        SchemeRules.Expensed(pattern, 25, 3000, SchemeCatalogue.Small);
        SchemeRules.Expensed(pattern, 40, 3000, SchemeCatalogue.Small);
        pattern.Heat[(int)SchemeKind.Expense] = SchemeCatalogue.HeatMost;
        at = 40;
        SchemeUntil(pattern, ref at, 100, w, SchemeEventKind.Letter, out _);
        SchemeRules.LoseFile(pattern.OpenOf(SchemeKind.Expense));
        seen.Clear();
        int pf = SchemeUntil(pattern, ref at, 100, w, SchemeEventKind.Findings, out SchemeEvent three, seen);
        Check(pf > 0 && three.Amount == 9000 && !seen.Contains(SchemeEventKind.Dropped), "schemes inquiry: a bought investigator cannot lose a pattern: three acts on file are found all the same");

        // A file whose acts are all too old is no case.
        SchemeState stale = SchemeStateOf(seed, 10, w);
        SchemeRules.Expensed(stale, 10, 3000, SchemeCatalogue.Small);
        Array.Clear(stale.Heat, 0, stale.Heat.Length);   // forgotten long since: nothing opens on its own
        var events = new List<SchemeEvent>();
        SchemeRules.Day(stale, 5 + SchemeCatalogue.TrailDays, w, events);
        SchemeInquiry sq = SchemeRules.OpenNow(stale, SchemeKind.Expense, 5 + SchemeCatalogue.TrailDays, events);
        at = 5 + SchemeCatalogue.TrailDays;
        bool withLetter = sq != null && sq.Stage == InquiryStage.Letter;
        seen.Clear();
        int sd = SchemeUntil(stale, ref at, at + 40, w, SchemeEventKind.Dropped, out _, seen);
        Check(withLetter && sq.End == InquiryEnd.Dropped && sd == 11 + SchemeCatalogue.TrailDays && !seen.Contains(SchemeEventKind.Findings),
            "schemes inquiry: an inquiry whose every act has grown older than a file reaches is dropped: no case to answer");
        Check(SchemeRules.OpenNow(stale, SchemeKind.Claim, at, events) == null, "schemes inquiry: the test's door opens nothing with nothing on file");

        // One at a time for a kind; different kinds side by side.
        SchemeState two = SchemeStateOf(seed, 10, w);
        SchemeRules.Expensed(two, 10, 12000, SchemeCatalogue.Large);
        SchemeRules.Claimed(two, SchemePlace, 10, 9000, SchemeCatalogue.Large, 1);
        events.Clear();
        SchemeRules.Day(two, 11, w, events);
        SchemeRules.Expensed(two, 11, 12000, SchemeCatalogue.Large);
        SchemeRules.Day(two, 12, w, events);
        int opens = 0;
        for (int i = 0; i < events.Count; i++)
        {
            opens += events[i].Kind == SchemeEventKind.Opened ? 1 : 0;
        }
        Check(opens == 2 && two.Cases.Count == 2 && two.OpenOf(SchemeKind.Expense) != null && two.OpenOf(SchemeKind.Claim) != null && two.Cases[0].Id != two.Cases[1].Id
              && SchemeRules.OpenNow(two, SchemeKind.Expense, 12, events) == null,
            "schemes inquiry: one inquiry at a time into a kind, and inquiries into different kinds side by side");
        events.Clear();
        for (int d = 13; d <= 11 + SchemeCatalogue.ChargeAfter; d++)
        {
            SchemeRules.Day(two, d, w, events);
        }
        long expenses = 0, claims = 0;
        for (int i = 0; i < events.Count; i++)
        {
            expenses += events[i].Kind == SchemeEventKind.Findings && events[i].A == (int)SchemeKind.Expense ? events[i].Amount : 0;
            claims += events[i].Kind == SchemeEventKind.Findings && events[i].A == (int)SchemeKind.Claim ? events[i].Amount : 0;
        }
        Check(expenses == 24000 && claims == 9000, "schemes inquiry: an act done while its inquiry runs is found with the rest, and each kind's findings take its own (" + expenses + ", "
            + claims + ")");

        // The tests' door: a day of every open inquiry now.
        SchemeState hurry = SchemeStateOf(seed, 10, w);
        SchemeRules.Record(hurry, SchemeKind.Expense, 10, 0, SchemeCatalogue.Small, 0, true, string.Empty, 0);
        events.Clear();
        SchemeInquiry hq = SchemeRules.OpenNow(hurry, SchemeKind.Expense, 10, events);
        int presses = 0;
        while (hq.Stage != InquiryStage.Charged && presses < 60)
        {
            presses++;
            Check(SchemeRules.Hurry(hurry, 10, w, events) == 1, "schemes inquiry: the test plays a day of the one open inquiry (press " + presses + ")");
        }
        Check(presses == SchemeCatalogue.ChargeAfter && events[events.Count - 1].Kind == SchemeEventKind.Findings && events[events.Count - 1].Amount == 0
              && SchemeRules.Hurry(hurry, 10, w, events) == 0,
            "schemes inquiry: the tests reach a test entry's findings in " + SchemeCatalogue.ChargeAfter + " presses, with nothing to take back, and then there is nothing left to play");

        // The option off: what is open runs its course, nothing new starts, nothing is forgotten.
        SchemeWorld off = SchemeWorldOf(false);
        SchemeState gone = SchemeStateOf(seed, 10, w);
        Check(!gone.CarriesOn, "schemes inquiry: a save nothing was done in has nothing to carry on with the option off");
        SchemeRules.Expensed(gone, 10, 12000, SchemeCatalogue.Large);
        gone.Dirty = 5000;
        gone.Shop(SchemePlace, true).Wash = SchemeCatalogue.WashSteps[2];
        at = 10;
        seen.Clear();
        int offFound = SchemeUntil(gone, ref at, 60, off, SchemeEventKind.Findings, out SchemeEvent offFindings, seen);
        Check(gone.CarriesOn && offFound == 11 + SchemeCatalogue.ChargeAfter && offFindings.Amount == 12000 && !seen.Contains(SchemeEventKind.Washed) && gone.Dirty == 5000,
            "schemes inquiry: with the option off an inquiry still opens on what was done and runs to its findings, while no cash is washed");
    }

    // ------------------------------------------------------------------------------------------------- paying back

    private static void SchemeRepayChecks()
    {
        SchemeWorld w = SchemeWorldOf();
        int seed = SchemeRules.SeedOf("repay");
        var none = new List<int>();

        // A first small file paid back in time: a caution, half the surcharge, no court, and the acts are dealt with.
        SchemeState s = SchemeStateOf(seed, 10, w);
        SchemeAct a = SchemeRules.Expensed(s, 10, 3000, SchemeCatalogue.Small);
        var events = new List<SchemeEvent>();
        SchemeInquiry q = SchemeRules.OpenNow(s, SchemeKind.Expense, 10, events);
        Check(SchemeRules.RepayCost(s, SchemeKind.Expense, 10, none) == 3000 && SchemeRules.RepayCost(s, SchemeKind.Claim, 10, none) == 0,
            "schemes repay: paying a file back costs what its acts still owe");
        long paid = SchemeRules.Repay(s, SchemeKind.Expense, 11, none, out long fromCash);
        Check(paid == 3000 && fromCash == 0 && a.Owed == 0 && a.Back == 3000 && q.Repaid == 3000 && SchemeRules.RepayCost(s, SchemeKind.Expense, 11, none) == 0
              && SchemeRules.Repay(s, SchemeKind.Expense, 11, none, out _) == 0 && q.Repaid == 3000,
            "schemes repay: a file is paid back once: every act settled, and the inquiry notes what was paid");
        int day = 10;
        var seen = new List<SchemeEventKind>();
        int cautioned = SchemeUntil(s, ref day, 60, w, SchemeEventKind.Caution, out SchemeEvent caution, seen);
        Check(cautioned == 10 + SchemeCatalogue.LetterAfter + 2 && caution.Net == 375 && q.End == InquiryEnd.Caution && !q.Open && q.Surcharge == 375 && a.Inquiry == q.Id
              && !seen.Contains(SchemeEventKind.Findings) && (s.Cautioned & (1 << (int)SchemeKind.Expense)) != 0 && SchemeRules.Count(s, SchemeKind.Expense, day) == 0,
            "schemes repay: a first small file paid back whole ends with a caution two days after the letter at the soonest: half the surcharge, no court, and the act never found again");

        // The next file of the kind goes to court, paid back or not: still at half the surcharge on what was paid back.
        SchemeRules.Day(s, 99, w, events);
        SchemeRules.Expensed(s, 100, 3000, SchemeCatalogue.Small);
        events.Clear();
        SchemeInquiry again = SchemeRules.OpenNow(s, SchemeKind.Expense, 100, events);
        SchemeRules.Repay(s, SchemeKind.Expense, 101, none, out _);
        day = 100;
        seen.Clear();
        int charged = SchemeUntil(s, ref day, 200, w, SchemeEventKind.Findings, out SchemeEvent findings, seen);
        Check(again != null && charged == 100 + SchemeCatalogue.ChargeAfter && !seen.Contains(SchemeEventKind.Caution) && findings.Amount == 0 && findings.Net == 375
              && findings.Text == JusticeLaw.TaxEvasion && findings.B == 0,
            "schemes repay: a caution is given once for a kind: the next file paid back goes to court all the same, with nothing left to take and half the surcharge");

        // A felony's worth is never cautioned.
        SchemeState big = SchemeStateOf(seed, 10, w);
        SchemeRules.Claimed(big, SchemePlace, 10, 9000, SchemeCatalogue.Large, 1);
        events.Clear();
        SchemeRules.OpenNow(big, SchemeKind.Claim, 10, events);
        SchemeRules.Repay(big, SchemeKind.Claim, 11, none, out _);
        day = 10;
        seen.Clear();
        SchemeUntil(big, ref day, 60, w, SchemeEventKind.Findings, out SchemeEvent bigFound, seen);
        Check(!seen.Contains(SchemeEventKind.Caution) && bigFound.Amount == 0 && bigFound.Net == 1125 && bigFound.Text == JusticeLaw.Fraud && bigFound.B == 1,
            "schemes repay: a file of a felony's worth is charged even when it was paid back: nothing left to take, half the surcharge, and the court");

        // Paid back in part (a new act after paying): what is still owed carries the whole surcharge, what was paid back half.
        SchemeState part = SchemeStateOf(seed, 10, w);
        SchemeRules.Expensed(part, 10, 3000, SchemeCatalogue.Small);
        events.Clear();
        SchemeRules.OpenNow(part, SchemeKind.Expense, 10, events);
        SchemeRules.Repay(part, SchemeKind.Expense, 11, none, out _);
        SchemeRules.Expensed(part, 12, 1000, SchemeCatalogue.Small);
        day = 10;
        seen.Clear();
        SchemeUntil(part, ref day, 60, w, SchemeEventKind.Findings, out SchemeEvent partFound, seen);
        Check(!seen.Contains(SchemeEventKind.Caution) && partFound.Amount == 1000 && partFound.Net == 250 + 375,
            "schemes repay: a file paid back and then added to is no caution: the new act is taken back with a quarter on top, the rest carries its half");

        // Cash sales and payments to people cannot be paid back.
        SchemeState no = SchemeStateOf(seed, 10, w);
        SchemeRules.Record(no, SchemeKind.Graft, 10, 0, 500, 1, true, string.Empty, (int)Favour.Vote).Paid = 6000;
        no.Acts.Add(new SchemeAct { Kind = SchemeKind.Wash, Day = 10, Gain = 800, Place = SchemePlace, Size = 250 });
        Check(SchemeRules.RepayCost(no, SchemeKind.Graft, 10, none) == 0 && SchemeRules.RepayCost(no, SchemeKind.Wash, 10, none) == 0
              && SchemeRules.Repay(no, SchemeKind.Wash, 10, none, out _) == 0 && no.Acts[1].Back == 0,
            "schemes repay: cash sales and money paid to people cannot be paid back");

        // A claim its insurer has not paid yet is withdrawn, not paid back; at the findings it is refused, and nothing of it is taken.
        SchemeState claims = SchemeStateOf(seed, 10, w);
        SchemeAct paidClaim = SchemeRules.Claimed(claims, SchemePlace, 10, 2000, SchemeCatalogue.Small, 7);
        claims.Shop(SchemePlace, false).ClaimDay = -1000;
        SchemeAct unpaidClaim = SchemeRules.Claimed(claims, SchemePlace, 12, 1500, SchemeCatalogue.Small, 8);
        var unpaid = new List<int> { 8 };
        events.Clear();
        SchemeInquiry cq = SchemeRules.OpenNow(claims, SchemeKind.Claim, 12, events);
        Check(SchemeRules.RepayCost(claims, SchemeKind.Claim, 12, unpaid) == 2000 && SchemeRules.RepayCost(claims, SchemeKind.Claim, 12, null) == 3500,
            "schemes repay: paying claims back does not cost what the insurer has not paid yet");
        long back = SchemeRules.Repay(claims, SchemeKind.Claim, 13, unpaid, out _);
        Check(back == 2000 && paidClaim.Back == 2000 && unpaidClaim.Gain == 0 && unpaidClaim.Owed == 0 && cq.Repaid == 2000,
            "schemes repay: the claim already paid is paid back, the one not yet paid is withdrawn with nothing to pay");
        SchemeState refused = SchemeStateOf(seed, 10, w);
        SchemeRules.Claimed(refused, SchemePlace, 10, 2000, SchemeCatalogue.Small, 7);
        refused.Shop(SchemePlace, false).ClaimDay = -1000;
        SchemeAct late = SchemeRules.Claimed(refused, SchemePlace, 12, 1500, SchemeCatalogue.Small, 8);
        events.Clear();
        SchemeRules.OpenNow(refused, SchemeKind.Claim, 12, events);
        SchemeWorld withUnpaid = SchemeWorldOf();
        withUnpaid.Unpaid.Add(8);
        day = 12;
        SchemeUntil(refused, ref day, 60, withUnpaid, SchemeEventKind.Findings, out SchemeEvent claimFound);
        Check(claimFound.Amount == 2000 && claimFound.Net == 500 && late.Gain == 0 && late.Inquiry != 0,
            "schemes repay: at the findings a claim the insurer has not paid is refused: only what was paid is taken back, and the unpaid one is found with the rest");
    }
}
