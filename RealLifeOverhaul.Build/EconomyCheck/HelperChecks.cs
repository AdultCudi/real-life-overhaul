using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;

// Ten small helpers of 14 Oct 2026, each built on a rule that was already there: the recruitment office's last day
// (Careers/Work/ProRules.AgencyEnds), and the others added below as they come. For each: that it comes once, when it
// may and may not, the save's field and an old save without it, and its texts in every language (at most 240
// characters in English for a notification, and no dash or exclamation mark).
internal static partial class Program
{
    private static Dictionary<string, string>[] _helperWords;

    private static void Helpers()
    {
        if (!HelperTexts())
        {
            return;
        }
        HelperAgency();
        HelperClassStart();
        HelperRejectReason();
        HelperGoodChance();
        HelperDayChange();
        HelperBondSoFar();
        HelperRateEve();
        HelperCourseChip();
    }

    // ---------------------------------------------------------------- 1. the recruitment office's last day

    private static void HelperAgency()
    {
        int until = 30, h = ProRules.AgencyNoteHour;
        Check(!ProRules.AgencyEnds(until, 0, until - 1, 12) && !ProRules.AgencyEnds(until, 0, until + 1, 12), "helpers, office: no reminder the day before the last day, or the day after it");
        Check(!ProRules.AgencyEnds(until, 0, until, h - 1) && ProRules.AgencyEnds(until, 0, until, h) && ProRules.AgencyEnds(until, 0, until, 23),
            "helpers, office: on the last day from " + h + ":00 (or the first hour seen after it), not in the night before");
        Check(!ProRules.AgencyEnds(until, until, until, 12), "helpers, office: once told for this registration it is not told again");
        Check(ProRules.AgencyEnds(until + ProRules.AgencyDays, until, until + ProRules.AgencyDays, h), "helpers, office: a new registration's last day is told again");
        Check(!ProRules.AgencyEnds(0, 0, 0, 12) && !ProRules.AgencyEnds(0, 0, 1, 12), "helpers, office: never registered, nothing");

        // one pass over a day's hours, as the hour event comes, with a reload in the middle: one reminder
        var f = new StudyFile { Seed = 5 };
        f.Work.AgencyUntil = until;
        int sent = 0;
        for (int hour = 0; hour < 24; hour++)
        {
            if (hour == 15)
            {
                f = CareersStore.Read(CareersStore.Write(f), out _);   // a save and a load in the afternoon
            }
            if (ProRules.AgencyEnds(f.Work.AgencyUntil, f.Work.AgencyTold, until, hour))
            {
                f.Work.AgencyTold = f.Work.AgencyUntil;
                sent++;
            }
        }
        Check(sent == 1 && f.Work.AgencyTold == until, "helpers, office: one reminder in the whole last day, a reload after it included (" + sent + ")");
        string line = CareersStore.Write(f);
        Check(CareersStore.Read(line, out _).Work.AgencyTold == until, "helpers, office: the day it was told comes back from the save");
        string old = line.Replace("|" + until + "~", "~");
        StudyFile was = CareersStore.Read(old, out _);
        Check(old != line && was.Work.AgencyTold == 0 && was.Work.AgencyUntil == until && ProRules.AgencyEnds(was.Work.AgencyUntil, was.Work.AgencyTold, until, h),
            "helpers, office: an old save without the field loads with nothing told");

        HelperNote("reallife_work_note_agency_ends", "office");
        HelperText("reallife_work_opt_agency_note", "office");
        HelperText("reallife_opt_tip_careers_agency_note", "office");
    }

    // ---------------------------------------------------------------- 2. "your class starts now"

    private static void HelperClassStart()
    {
        CourseDef fork = StudyCatalogue.Find(StudyCatalogue.Forklift);       // the school, Mon and Wed at 09:00, 2 classes
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);  // the university, Mon Wed Fri at 10:00
        int start = StudyRules.Hour(fork, StudyMode.FullTime) * 60, at = 8 * 1440 + start;
        var e = new Enrolment { Course = fork.Id, Mode = (int)StudyMode.FullTime, Start = 2, Seen = 1, Done = 1, SettledDay = 3, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.StartsNow(e, 8, start, -1, 0), "helpers, class: told at the minute the class starts");
        Check(!StudyRules.StartsNow(e, 8, start - 60, -1, 0) && !StudyRules.StartsNow(e, 8, start + 60, -1, 0) && !StudyRules.StartsNow(e, 9, start, -1, 0),
            "helpers, class: not an hour before, not an hour after, not on a day without a class");
        Check(!StudyRules.StartsNow(e, 8, start, -1, at), "helpers, class: once told for this class, not again");
        Check(!StudyRules.StartsNow(e, 8, start, fork.Id, 0), "helpers, class: not while that class is being sat");
        var gone = new Enrolment { Course = fork.Id, Mode = (int)StudyMode.FullTime, Start = 2, Seen = 2, Done = 2, SettledDay = 3, Stage = (int)EnrolStage.Studying };
        var exam = new Enrolment { Course = fork.Id, Mode = (int)StudyMode.FullTime, Start = 2, Seen = 2, Done = 2, SettledDay = 3, Stage = (int)EnrolStage.Exam };
        Check(!StudyRules.StartsNow(gone, 8, start, -1, 0) && !StudyRules.StartsNow(exam, 8, start, -1, 0) && !StudyRules.StartsNow(null, 8, start, -1, 0),
            "helpers, class: none once every class of the course is held (StudyRules.Held), none at the exam, none for no enrolment");
        var degree = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 3, Done = 3, SettledDay = 5, Stage = (int)EnrolStage.Studying };
        int csStart = StudyRules.Hour(cs, StudyMode.FullTime) * 60;
        Check(StudyRules.StartsNow(degree, 8, csStart, -1, 0) && StudyRules.StartsNow(degree, 10, csStart, -1, 8 * 1440 + csStart),
            "helpers, class: the next class is told in its turn");
        Check(StudyRules.Elsewhere("reallife_study_why_not_school") && StudyRules.Elsewhere("reallife_study_why_not_university") && StudyRules.Elsewhere("reallife_study_why_not_hospital"),
            "helpers, class: told when the player is not inside the school, the university or the ward");
        Check(!StudyRules.Elsewhere(null) && !StudyRules.Elsewhere("reallife_study_why_no_class") && !StudyRules.Elsewhere("reallife_study_why_fee_owed") && !StudyRules.Elsewhere("reallife_study_why_not_home"),
            "helpers, class: not when the class can be joined where the player is (inside), when there is none, or for another reason");

        // a day's hours with a reload just after the class's hour, and two courses at once: each told once
        var f = new StudyFile { Seed = 3 };
        f.Courses.Add(e);
        f.Courses.Add(new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 3, Done = 3, SettledDay = 5, Stage = (int)EnrolStage.Studying });
        int told = 0;
        for (int hour = 0; hour < 24; hour++)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < f.Courses.Count; i++)
                {
                    Enrolment x = f.Courses[i];
                    if (StudyRules.StartsNow(x, 8, hour * 60, -1, x.StartTold))
                    {
                        x.StartTold = 8 * 1440 + hour * 60;
                        told++;
                    }
                }
                f = CareersStore.Read(CareersStore.Write(f), out _);   // the hour seen again after a save and a load
            }
        }
        Check(told == 2 && f.Courses[0].StartTold == at, "helpers, class: on a day with two courses' classes, two notices, a reload after each included (" + told + ")");
        string line = CareersStore.Write(f);
        string old = line.Replace("|" + at + ";", ";").Replace("|" + at + "~", "~");
        StudyFile was = CareersStore.Read(old, out _);
        Check(old != line && was.Courses.Count == 2 && was.Courses[0].StartTold == 0 && was.Courses[0].Seen == 1, "helpers, class: an old save without the field loads with nothing told");

        HelperNote("reallife_study_note_class_starts", "class", "course", "place", "late");
        string filled = HelperFill("reallife_study_note_class_starts", "course", new string('c', 48), "place", new string('p', 60), "late", "1h 00m");
        Check(filled.Length <= 240, "helpers, class: filled with a long course and place, " + filled.Length + " characters, over 240");
        HelperText("reallife_study_opt_class_start", "class");
        HelperText("reallife_opt_tip_careers_class_start", "class");
    }

    // ---------------------------------------------------------------- 3. the reason for a rejection

    private static void HelperRejectReason()
    {
        Check(ProRules.ScoreShort(46, false) == ProRules.ScreenPass - 46 && ProRules.ScoreShort(46, true) == ProRules.OfferPass - 46,
            "helpers, rejection: the points short of the screening's pass, or of the interview's");
        Check(ProRules.ScoreShort(ProRules.ScreenPass, false) == 0 && ProRules.ScoreShort(ProRules.OfferPass + 9, true) == 0 && ProRules.ScoreShort(100, false) == 0,
            "helpers, rejection: a score at or over what they wanted is not short (it was close)");
        Check(ProRules.ScoreShort(0, false) == ProRules.ScreenPass && ProRules.ScoreShort(-5, true) == ProRules.OfferPass, "helpers, rejection: never more short than the whole pass");
        bool agrees = true;
        for (int score = 0; score <= 100; score++)
        {
            agrees &= (ProRules.ScoreShort(score, false) == 0) == ProRules.Screened(score, 0) || score == 0;
            agrees &= (ProRules.ScoreShort(score, true) == 0) == ProRules.Offered(score, 0) || score == 0;
            agrees &= ProRules.ScoreShort(score, true) >= ProRules.ScoreShort(score, false);
        }
        Check(agrees, "helpers, rejection: 'close' is said exactly when the score passes with no luck either way, at both steps");

        HelperNote("reallife_work_note_rejected_why", "rejection", "employer", "title", "why");
        HelperNote("reallife_work_note_chosen_why", "rejection", "employer", "why");
        HelperText("reallife_work_why_short", "rejection", "points");
        HelperText("reallife_work_why_short_one", "rejection");
        HelperText("reallife_work_why_close", "rejection");
        HelperText("reallife_work_opt_reject_reason", "rejection");
        HelperText("reallife_opt_tip_careers_reject_reason", "rejection");
        string employer = new string('e', 28), title = new string('t', 34);
        foreach (string why in new[] { HelperFill("reallife_work_why_short", "points", "12"), HelperFill("reallife_work_why_short_one"), HelperFill("reallife_work_why_close") })
        {
            foreach (string key in new[] { "reallife_work_note_rejected_why", "reallife_work_note_chosen_why" })
            {
                string all = HelperFill(key, "employer", employer, "title", title, "why", why);
                Check(all.Length <= 240, "helpers, rejection: " + key + " with its reason is " + all.Length + " characters, over 240");
            }
        }
        string example = HelperFill("reallife_work_note_rejected_why", "employer", "BlueStone Imports", "title", "Junior Accountant", "why", HelperFill("reallife_work_why_short", "points", "4"));
        Check(example.StartsWith("BlueStone Imports turned down your application", StringComparison.Ordinal) && example.Contains("about 4 points short of the score they wanted"),
            "helpers, rejection: the example reads as asked (" + example + ")");
    }

    // ---------------------------------------------------------------- 4. the points for a good chance, in the vacancy box

    private static void HelperGoodChance()
    {
        bool exact = true, same = true;
        int seen = 0;
        for (int points = 0; points <= 100; points += 5)
        {
            for (int need = 10; need <= 70; need += 15)
            {
                var c = new Candidate { Qualified = true, Points = points, Need = need, Experience = 40, Cycle = 1, Rank = 1 };
                int more = ProPath.PointsFor(c, ProPath.GoodChance);
                same &= ProPath.PointsFor(c, 1) == ProPath.PointsShort(c) && c.Points == points;
                if (more > 100)
                {
                    continue;
                }
                seen++;
                Candidate at = c, before = c;
                at.Points = points + more;
                before.Points = points + more - 1;
                exact &= ProPath.OfferChance(ProRules.HireScore(at)) >= ProPath.GoodChance && (more == 0 || ProPath.OfferChance(ProRules.HireScore(before)) < ProPath.GoodChance);
            }
        }
        Check(seen > 20 && exact, "helpers, good chance: the points named are the fewest that bring a " + ProPath.GoodChance + "% chance or better (" + seen + " cases)");
        Check(same, "helpers, good chance: with any chance at all it is the points short the box already gave, and the candidate is left as it was");
        var strong = new Candidate { Qualified = true, Points = 90, Need = 40, Experience = 400, Good = 2, Referral = true, Cycle = 0 };
        var unqualified = new Candidate { Qualified = false, Points = 90, Need = 10 };
        var record = new Candidate { Qualified = true, Points = 0, Need = 100, Record = true, Poor = 3, Reputation = -10, Cycle = 3 };
        Check(ProPath.PointsFor(strong, ProPath.GoodChance) == 0 && ProPath.PointsFor(unqualified, ProPath.GoodChance) > 100 && ProPath.PointsFor(record, ProPath.GoodChance) > 100,
            "helpers, good chance: 'You have it' for a strong candidate; out of reach by skill for one not qualified or held back by a record");
        Check(ProPath.GoodChance == 50 && ProPath.PointsFor(strong, 0) == 0, "helpers, good chance: a good chance is an even one or better");
        HelperText("reallife_career_good_chance", "good chance");
        HelperText("reallife_career_good_chance_have", "good chance");
        HelperText("reallife_career_good_chance_points", "good chance", "points");
        HelperText("reallife_career_good_chance_point", "good chance");
        HelperText("reallife_career_good_chance_far", "good chance");
    }

    // ---------------------------------------------------------------- 7. the Portfolio's change today

    private static void HelperDayChange()
    {
        var m = new MarketState();
        m.Stocks.Add(new Stock { Id = 1, PrevClose = 10000, Last = 10500 });   // up 5%
        m.Stocks.Add(new Stock { Id = 2, PrevClose = 4000, Last = 3800 });     // down 5%
        m.Stocks.Add(new Stock { Id = 3, PrevClose = 2500, Last = 2500 });
        long before;
        Check(MarketTrading.DayChange(m, out before) == 0 && before == 0 && MarketTrading.DayChangeBp(0, before) == 0, "helpers, day change: nothing held, no change and no row");
        m.Positions.Add(new Position { StockId = 1, Shares = 10 });
        long one = MarketTrading.DayChange(m, out before);
        Check(one == 10 * 500 && before == 10 * 10000 && MarketTrading.DayChangeBp(one, before) == 500, "helpers, day change: ten shares up 5.00 each are 50 up, +5.00%");
        m.Positions.Add(new Position { StockId = 2, Shares = -20 });
        long two = MarketTrading.DayChange(m, out before);
        Check(two == 5000 + 4000 && before == 100000 + 80000, "helpers, day change: a short holding gains as its price falls, and counts at its size in the per cent");
        m.Positions.Add(new Position { StockId = 2, Shares = 50 });
        m.Positions.Add(new Position { StockId = 3, Shares = 7 });
        m.Positions.Add(new Position { StockId = 99, Shares = 7 });
        long all = MarketTrading.DayChange(m, out before);
        Check(all == 9000 - 50 * 200 && all < 0 && before == 180000 + 50 * 4000 + 7 * 2500, "helpers, day change: a fall shows as a loss (red), an unchanged one adds nothing, an unknown company is left out");
        Check(MarketTrading.DayChangeBp(all, before) == (int)(all * 10000L / before) && MarketTrading.DayChangeBp(-1, 3) < 0, "helpers, day change: the per cent against the previous close, with its sign");
        m.Stocks[0].PrevClose = 0;
        MarketTrading.DayChange(m, out long withoutBase);
        Check(withoutBase == before - 10 * 10000, "helpers, day change: a company with no previous close yet is left out");
        HelperText("reallife_mkt_day_change", "day change");
        HelperText("reallife_mkt_day_change_value", "day change", "money", "percent");
    }

    // ---------------------------------------------------------------- 8. a bond's interest so far

    private static void HelperBondSoFar()
    {
        // A year's Treasury bond paying every quarter, nothing put back: what each notice says it has paid so far.
        MarketEngine m = BondMarket(211, 60, 1000000, out MarketWorld world);
        var soFar = new List<long>();
        m.BondTold = n =>
        {
            if (n.Kind == BondNoteKind.Interest)
            {
                soFar.Add(BondRules.InterestSoFar(m.State.Bonds.Holding(n.Holding)));   // (read as the director reads it when it is told)
            }
        };
        BondOffer offer = OfferOf(m, 2);
        BondHolding h = m.BuyBond(offer, 100000, 0, m.Trading, true, out BondWhy why);
        Check(h != null && BondRules.InterestSoFar(h) == 0 && BondRules.InterestSoFar(null) == 0, "helpers, bond: a bond just bought has paid nothing so far");
        long each = h.Face * offer.YieldBp / 40000L - ListingVotes.DividendTax(h.Face * offer.YieldBp / 40000L, 0);   // cents after tax, each payment
        RunTo(m, h.EndDay + 8);
        bool adds = soFar.Count == 4;
        for (int k = 0; adds && k < soFar.Count; k++)
        {
            adds &= soFar[k] == (k + 1) * each;
        }
        Check(adds, "helpers, bond: each notice's total is the interest after tax of every payment so far, this one included (" + string.Join(",", soFar) + ")");

        // Half put back: what is put back counts as paid on the bond too.
        m = BondMarket(212, 60, 1000000, out world);
        h = m.BuyBond(OfferOf(m, 12), 200000, 50, m.Trading, true, out why);
        RunTo(m, TradingOnOrAfter(BondRules.PayDay(h, 2)) + 1);
        BondHolding held = m.State.Bonds.Holding(h.Id);
        Check(held != null && held.Reinvested > 0 && BondRules.InterestSoFar(held) == held.Received + held.Reinvested, "helpers, bond: interest put back is part of what the bond has paid");

        // The running totals are in the save: a round trip keeps them, a record with none starts at 0.
        var book = new BondBook();
        book.Holdings.Add(held);
        var text = new System.Text.StringBuilder();
        BondStore.Write(text, book);
        var back = new BondBook();
        BondStore.Read(text.ToString(), back);
        Check(back.Holdings.Count == 1 && BondRules.InterestSoFar(back.Holdings[0]) == BondRules.InterestSoFar(held), "helpers, bond: the total comes back from the save");
        var fresh = new BondBook();
        BondStore.Read("2;1|2|-1||1000000|1000000|400|10|70|4|0|0|0|0|0|0|0", fresh);
        Check(fresh.Holdings.Count == 1 && BondRules.InterestSoFar(fresh.Holdings[0]) == 0, "helpers, bond: a bond with no payment kept starts at 0");

        HelperNote("reallife_mkt_bond_note_paid_total", "bond", "amount", "name", "total");
        HelperNote("reallife_mkt_bond_note_paid_back_total", "bond", "amount", "name", "back", "total");
        string filled = HelperFill("reallife_mkt_bond_note_paid_back_total", "name", new string('n', 48), "amount", "$123,456", "back", "$123,456", "total", "$1,234,567");
        Check(filled.Length <= 240, "helpers, bond: the notice with a long name and large sums is " + filled.Length + " characters, over 240");
        HelperText("reallife_mkt_bond_opt_so_far", "bond");
        HelperText("reallife_opt_tip_markets_bond_so_far", "bond");
    }

    // ---------------------------------------------------------------- 9. the rate decision ahead

    private static void HelperRateEve()
    {
        const int month = 10;
        int eve = CityModel.DecisionEveHour;
        var s = new CityState { Age = month - 1, Pending = true, PendingMove = 1 };   // the decision takes effect tomorrow
        Check(CityModel.DaysToTurn(s, month) == 1, "helpers, rate: the state used is the day before a decision takes effect");
        Check(!CityModel.DecisionEve(s, month, 40, eve - 1, 0) && CityModel.DecisionEve(s, month, 40, eve, 0) && CityModel.DecisionEve(s, month, 40, 23, 0),
            "helpers, rate: in the evening (from " + eve + ":00), not before");
        Check(!CityModel.DecisionEve(s, month, 40, 21, 40) && CityModel.DecisionEve(s, month, 41, 21, 40), "helpers, rate: once a day it is due");
        s.Pending = false;
        Check(!CityModel.DecisionEve(s, month, 40, 21, 0), "helpers, rate: nothing while no decision is announced");
        s.Pending = true;
        s.Age = month - 3;
        Check(!CityModel.DecisionEve(s, month, 40, 21, 0) && !CityModel.DecisionEve(null, month, 40, 21, 0), "helpers, rate: nothing two days before it takes effect, nothing without a city");
        s.Age = month - 1;

        // the evening's hours with a reload in the middle: one notice
        int told = 0, sent = 0;
        for (int hour = 0; hour < 24; hour++)
        {
            if (hour == 20)
            {
                told = int.Parse(told.ToString());   // (the day kept in the save's line and read back)
            }
            if (CityModel.DecisionEve(s, month, 40, hour, told))
            {
                told = 40;
                sent++;
            }
        }
        Check(sent == 1, "helpers, rate: one notice on the evening, a reload after it included (" + sent + ")");
        Check(CityModel.DecisionEveKey(1) == "reallife_macro_note_eve_up" && CityModel.DecisionEveKey(-2) == "reallife_macro_note_eve_down" && CityModel.DecisionEveKey(0) == "reallife_macro_note_eve_hold",
            "helpers, rate: a rise, a cut and a hold each have their words");
        Check(CityStore.EveKey.StartsWith("reallife:", StringComparison.Ordinal) && CityStore.EveKey != CityStore.Key && CityStore.EveKey != CityStore.TestKey,
            "helpers, rate: the day told is a line of the player's own, apart from the city's line a Going Public host sends");
        string line = CityStore.Write(new CityState { Seed = 3, Age = 4 });
        Check(line.IndexOf("economy_city_eve", StringComparison.Ordinal) < 0, "helpers, rate: the city's line has nothing of it");

        HelperNote("reallife_macro_note_eve_up", "rate", "points", "rate");
        HelperNote("reallife_macro_note_eve_down", "rate", "points", "rate");
        HelperNote("reallife_macro_note_eve_hold", "rate", "rate");
        HelperText("reallife_macro_opt_eve", "rate");
        HelperText("reallife_opt_tip_economy_rate_ahead", "rate");
    }

    // ---------------------------------------------------------------- 10. the course's progress chip

    private static void HelperCourseChip()
    {
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);   // the university, Mon Wed Fri at 10:00
        int start = StudyRules.Hour(cs, StudyMode.FullTime) * 60;
        var e = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 3, Done = 2, SettledDay = 5, Stage = (int)EnrolStage.Studying };
        string Chip(int day, int minute, int sitting) => HelperFill("reallife_ibox_chip_classes", "held", StudyRules.Held(e, day, minute, sitting).ToString(), "of", cs.Sessions.ToString());
        Check(Chip(8, start - 60, -1) == "3 of " + cs.Sessions + " classes", "helpers, chip: before Monday's class, the three held so far (" + Chip(8, start - 60, -1) + ")");
        Check(Chip(8, start + 30, cs.Id) == "4 of " + cs.Sessions + " classes" && Chip(8, start + StudyCatalogue.LateMinutes + 1, -1) == "4 of " + cs.Sessions + " classes",
            "helpers, chip: the class being sat counts, and so does one whose door has shut");
        var last = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = cs.Sessions - 1, Done = cs.Sessions - 1, SettledDay = 5, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.Held(last, 30, 23 * 60, -1) == cs.Sessions, "helpers, chip: never more than the course's classes");
        HelperText("reallife_ibox_chip_classes", "chip", "held", "of");
    }

    // ---------------------------------------------------------------- the texts

    private static bool HelperTexts()
    {
        _helperWords = LanguageWords(out string[] codes);
        return LanguagesRead("helpers", _helperWords, codes);
    }

    // A text in every language, each with the same names in curly brackets as the English, and plain in English.
    private static void HelperText(string key, string what, params string[] slots)
    {
        bool all = true;
        for (int l = 0; l < _helperWords.Length; l++)
        {
            all &= _helperWords[l].TryGetValue(key, out string w) && w.Trim().Length > 0 && SlotsOf(w) == SlotsOf(_helperWords[0].TryGetValue(key, out string e) ? e : string.Empty);
        }
        Check(all, "helpers, " + what + ": " + key + " is in every language with the same names to fill");
        string en = _helperWords[0].TryGetValue(key, out string t) ? t : string.Empty;
        Check(en.IndexOf('!') < 0 && en.IndexOf('—') < 0 && en.IndexOf('–') < 0 && en.IndexOf(" - ", StringComparison.Ordinal) < 0,
            "helpers, " + what + ": " + key + " has no exclamation mark and no dash");
        for (int i = 0; i < slots.Length; i++)
        {
            Check(en.Contains("{" + slots[i] + "}"), "helpers, " + what + ": " + key + " names {" + slots[i] + "}");
        }
    }

    // A notification's text: as a text, and at most 240 characters in English.
    private static void HelperNote(string key, string what, params string[] slots)
    {
        HelperText(key, what, slots);
        string en = _helperWords[0].TryGetValue(key, out string t) ? t : string.Empty;
        Check(en.Length <= 240, "helpers, " + what + ": " + key + " is " + en.Length + " characters, over 240");
    }

    // An English text filled in as the game would ({name} replaced), for the length of a notification made of two texts.
    private static string HelperFill(string key, params string[] pairs)
    {
        string w = _helperWords[0].TryGetValue(key, out string t) ? t : string.Empty;
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            w = w.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return w;
    }

    private static string SlotsOf(string words)
    {
        var names = new List<string>();
        int at = words.IndexOf('{');
        while (at >= 0)
        {
            int end = words.IndexOf('}', at);
            if (end < 0)
            {
                break;
            }
            names.Add(words.Substring(at, end - at + 1));
            at = words.IndexOf('{', end);
        }
        names.Sort(StringComparer.Ordinal);
        return string.Join(",", names);
    }
}
