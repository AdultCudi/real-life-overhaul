using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Ui;
using PolicyField = RealLifeOverhaul.Economy.PolicyField;

// The raises staff ask for, answered together and by themselves (two players, 5 and 6 Nov 2026: "Its only agree or refuse no offer less" with 28 requests waiting, and "What settings do I
// need to configure so that employees who, for example, request a 3% pay raise automatically have it approved"). Offer less to all stands beside Agree to all and Refuse all and is
// StaffRules.AnswerAll; an option agrees by itself, on each morning's pass, a request that asks a set share of the pay or less (StaffRules.WithinLimit) and leaves it to a manager or
// an executive wherever one answers raises (CompanyDay.AgreeByItself, the one step the director's morning and the run of the Company checks both take). Nobody asks for less than
// 7 to 18 per cent of their pay, by strictness, so a limit under that agrees nothing (StaffRules.LimitLeast), and the option's sentence says so. And the review before hiring several
// candidates in My Employees, which named the game's wage where the candidates' table names the wage paid (PaidWage.Shown). Run alone by --staff-raises, and in the full list.
// Each rule is run as the game runs it, through the world of the 120-day run (CompanyRunWorld.cs), never through a copy of its loop; a check of a case that agrees nothing stands
// beside the case that agrees, so that a rule that agrees nothing fails.
internal static partial class Program
{
    private static void StaffRaiseAnswers()
    {
        int before = _run, failedBefore = _failed;
        RaiseAllLess();
        RaiseLimitRule();
        RaiseLimitLeast();
        RaiseLimitCases();
        RaiseOthers();
        RaiseWrites();
        RaiseMorningPass();
        RaiseOptionOff();
        RaiseRun();
        RaiseTimed();
        RaiseAnswerScreens();
        RaiseAnswerDirector();
        RaiseAnswerOption();
        RaiseHireReview();
        RaiseAnswerTexts();
        Console.WriteLine("  raises answered together and by themselves: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // Somebody paid so much an hour whose request, for so much more, waits.
    private static StaffFile Asking(int paid, int asked)
    {
        StaffFile f = Worker(paid, paid + asked);
        f.AskDay = 10;
        f.AskTo = paid + asked;
        return f;
    }

    // Everything a request moves on a person, to see who was touched.
    private static string RaiseState(StaffFile f)
    {
        return f.Standing + ":" + f.Raise + ":" + f.AskDay + ":" + f.AskTo + ":" + f.AskRest + ":" + f.UnderDays + ":" + f.PoachDay + ":" + f.LeaveDay;
    }

    // Twelve people whose requests for the going rate wait.
    private static List<StaffFile> RaiseWaiting()
    {
        var crew = new List<StaffFile>();
        for (int i = 0; i < 12; i++)
        {
            StaffFile f = Worker(1600 + i * 110, 1900 + i * 110 + i % 5 * 41);
            f.AskDay = 20;
            f.AskTo = f.Going;
            crew.Add(f);
        }
        return crew;
    }

    // ---------------------------------------------------------------- Offer less to all

    // 28 requests waiting among 31 people, as a player's screenshot has them: offering less to all gives each who waits half of what they asked, takes the standing offering less
    // takes and starts the rest; the three who ask nothing are left as they were.
    private static void RaiseAllLess()
    {
        var staff = new List<StaffFile>();
        long asked = 0;
        for (int i = 0; i < 31; i++)
        {
            StaffFile f = Worker(1500 + i * 130, 1700 + i * 130 + i % 7 * 37);
            if (i % 11 != 5)
            {
                f.AskDay = 20;
                f.AskTo = f.Going;
                asked += f.AskTo - f.Paid;
            }
            staff.Add(f);
        }
        var half = new List<int>();
        var untouched = new List<string>();
        foreach (StaffFile f in staff)
        {
            half.Add((f.AskTo - f.Paid) * CompanyCatalogue.RaiseLessShare / 100);
            untouched.Add(RaiseState(f));
        }
        int raise, again;
        int count = StaffRules.AnswerAll(staff, RaiseAnswer.Less, 21, out raise);
        long expected = 0;
        bool each = true, rest = true;
        for (int i = 0; i < staff.Count; i++)
        {
            StaffFile f = staff[i];
            if (i % 11 != 5)
            {
                expected += half[i];
                each &= f.Raise == half[i] && f.Standing == CompanyCatalogue.StandingStart + CompanyCatalogue.StandLess && f.AskDay == 0 && f.AskTo == 0
                        && f.AskRest == 21 + CompanyCatalogue.RaiseRestDays;
            }
            else
            {
                rest &= RaiseState(f) == untouched[i];
            }
        }
        Check(count == 28 && raise == expected && raise > 0 && raise * 2 <= asked && raise * 2 >= asked - count,
            "offer less to all: of 31 people 28 wait and are answered, and the raises come to half of what agreeing to all would give (" + raise + " of " + asked + " hundredths an hour)");
        Check(each && rest, "offer less to all: each of the 28 gets half of what they asked, loses the standing offering less costs and rests for " + CompanyCatalogue.RaiseRestDays
                            + " days, and the three who asked nothing are left exactly as they were");
        Check(count == 28 && StaffRules.AnswerAll(staff, RaiseAnswer.Less, 22, out again) == 0 && again == 0,
            "offer less to all: with nobody waiting any more nothing is answered a second time");

        // The other answers go through the same step: agree gives all that was asked, refuse nothing; a bonus is paid one by one, so it answers nobody.
        List<StaffFile> agree = RaiseWaiting(), refuse = RaiseWaiting(), bonus = RaiseWaiting();
        long all = 0;
        foreach (StaffFile f in agree)
        {
            all += f.AskTo - f.Paid;
        }
        int rAgree, rRefuse, rBonus;
        int nAgree = StaffRules.AnswerAll(agree, RaiseAnswer.Agree, 21, out rAgree), nRefuse = StaffRules.AnswerAll(refuse, RaiseAnswer.Refuse, 21, out rRefuse);
        int nBonus = StaffRules.AnswerAll(bonus, RaiseAnswer.Bonus, 21, out rBonus);
        bool agreed = true, refused = true, waiting = true;
        for (int i = 0; i < 12; i++)
        {
            agreed &= agree[i].Paid == 1900 + i * 110 + i % 5 * 41 && agree[i].Standing == CompanyCatalogue.StandingStart + CompanyCatalogue.StandAgree && agree[i].AskDay == 0;
            refused &= refuse[i].Raise == 0 && refuse[i].Standing == CompanyCatalogue.StandingStart + CompanyCatalogue.StandRefuse && refuse[i].AskDay == 0;
            waiting &= bonus[i].AskDay == 20 && bonus[i].AskTo == bonus[i].Going && bonus[i].Raise == 0 && bonus[i].Standing == CompanyCatalogue.StandingStart;
        }
        Check(nAgree == 12 && rAgree == all && agreed && nRefuse == 12 && rRefuse == 0 && refused && nBonus == 0 && rBonus == 0 && waiting,
            "answers for all: agreeing to all gives each the going rate they asked for, refusing all gives nothing, and a bonus is not an answer for all (it leaves every request waiting)");
    }

    // ---------------------------------------------------------------- the limit

    // The edge of each limit: the most that is its share of the pay is agreed, one hundredth more is not. And what is no request, or no limit, beside the request that is.
    private static void RaiseLimitRule()
    {
        int[] pays = { 400, 1000, 1859, 2000, 3437, 9999, 25000 };
        int[] limits = { 1, 2, 3, 5, 10, 25, CompanyCatalogue.AgreeUpToMost };
        string bad = null;
        int cases = 0;
        foreach (int pay in pays)
        {
            foreach (int limit in limits)
            {
                int most = (int)((long)pay * limit / 100);
                cases++;
                if (!StaffRules.WithinLimit(Asking(pay, most), limit) || StaffRules.WithinLimit(Asking(pay, most + 1), limit))
                {
                    bad = bad ?? pay + " at " + limit + "% (" + most + ")";
                }
            }
        }
        Check(bad == null, "agreed by itself: at the edge of every limit the share of the pay is agreed and a hundredth more is not (" + cases + " cases" + (bad != null ? ", first wrong " + bad : string.Empty) + ")");
        int top = CompanyCatalogue.AgreeUpToMost;
        Check(StaffRules.WithinLimit(Asking(2000, 1), 1) && !StaffRules.WithinLimit(Asking(2000, 1), 0) && !StaffRules.WithinLimit(Asking(2000, 1), -5),
            "agreed by itself: the same tiny request is agreed at a limit of 1 and is not at 0, whatever is asked, which is off");
        int most50 = 1000 * top / 100;
        Check(StaffRules.WithinLimit(Asking(1000, most50), 999) && !StaffRules.WithinLimit(Asking(1000, most50 + 1), 999), "agreed by itself: a limit past its most is held to the most (" + top + "% of the pay)");
        StaffFile none = Asking(2000, 100);
        none.AskDay = 0;
        StaffFile nothing = Asking(2000, 100);
        nothing.AskTo = 2000;
        StaffFile under = Asking(2000, 100);
        under.AskTo = 1900;
        Check(StaffRules.WithinLimit(Asking(2000, 100), top) && !StaffRules.WithinLimit(none, top) && !StaffRules.WithinLimit(nothing, top) && !StaffRules.WithinLimit(under, top),
            "agreed by itself: the request that waits is agreed at the most, and nobody waiting, a request for nothing and one for less than they are paid are no request");
        StaffFile raised = Worker(2000, 2700);
        raised.Raise = 500;   // paid 25.00 with the agreed raise: a request for 26.25 is 5% of that, and 6.25% of the game's wage alone
        raised.AskDay = 10;
        raised.AskTo = 2625;
        Check(StaffRules.WithinLimit(raised, 5) && !StaffRules.WithinLimit(raised, 4), "agreed by itself: the share is of what they are paid now, the agreed raise included");
        StaffFile free = Worker(0, 500);
        free.AskDay = 10;
        free.AskTo = 500;
        StaffFile huge = Worker(int.MaxValue / 4, int.MaxValue / 4 + 1000);
        huge.AskDay = 10;
        huge.AskTo = huge.Going;
        StaffFile vast = Worker(1, int.MaxValue / 2);
        vast.AskDay = 10;
        vast.AskTo = vast.Going;
        Check(!StaffRules.WithinLimit(free, top) && StaffRules.WithinLimit(huge, 1) && !StaffRules.WithinLimit(vast, top),
            "agreed by itself: pay of nothing is never a share to ask by, and the largest figures do not overflow");
    }

    // Nobody asks for less than g / (1000 - g) of their pay, g being how far under the going rate they must be to ask: found by running the day's own rule over many pays and going
    // rates, then asking for the least limit that agrees what it asked. That least limit is StaffRules.LimitLeast, and no limit under it agrees any request there can be.
    private static void RaiseLimitLeast()
    {
        int[] expected = { 18, 12, 9, 7 };
        string said = string.Empty;
        bool right = true, formula = true, nothing = true;
        for (int level = CompanyCatalogue.Relaxed; level <= CompanyCatalogue.Hardcore; level++)
        {
            int found = int.MaxValue, asks = 0;
            int[] goings = { 1000, 1500, 1999, 2000, 2640, 3333, 5000, 8000 };
            foreach (int going in goings)
            {
                for (int paid = going / 2; paid <= going; paid++)
                {
                    StaffFile f = Worker(paid, going);
                    for (int day = 1; day <= CompanyCatalogue.RaiseDays[level] && f.AskDay == 0; day++)
                    {
                        StaffRules.Day(f, level, day);
                    }
                    if (f.AskDay == 0)
                    {
                        continue;
                    }
                    asks++;
                    for (int limit = 1; limit <= CompanyCatalogue.AgreeUpToMost; limit++)
                    {
                        if (StaffRules.WithinLimit(f, limit))
                        {
                            found = Math.Min(found, limit);
                            break;
                        }
                    }
                }
            }
            formula &= StaffRules.LimitLeast(level) == expected[level];
            right &= found == StaffRules.LimitLeast(level) && asks > 0;
            for (int limit = 0; limit <= CompanyCatalogue.AgreeUpToMost; limit++)
            {
                nothing &= StaffRules.AgreesNothing(limit, level) == (limit > 0 && limit < found);   // on and under every request the day's rule makes
            }
            said += " " + level + ": " + found + " (" + asks + " asked);";
        }
        formula &= StaffRules.LimitLeast(-3) == StaffRules.LimitLeast(CompanyCatalogue.Relaxed) && StaffRules.LimitLeast(99) == StaffRules.LimitLeast(CompanyCatalogue.Hardcore);
        Check(formula, "least limit: it is 18, 12, 9 and 7 per cent of the pay at Relaxed, Standard, Realistic and Hardcore, the share of the going rate somebody must be under to ask (150, 100, 80 and 60 thousandths) over what is left,"
                       + " and a strictness out of range is the nearest one");
        Check(right, "least limit: the smallest limit that agrees any request the day's rule makes is StaffRules.LimitLeast, at each strictness, so none under it agrees anything:" + said);
        bool three = true;
        for (int level = CompanyCatalogue.Relaxed; level <= CompanyCatalogue.Hardcore; level++)
        {
            three &= StaffRules.LimitLeast(level) > 3;
        }
        Check(three && right && nothing, "least limit: a player's limit of 3% is under the least at every strictness, so it agrees nothing (the least is " + StaffRules.LimitLeast(CompanyCatalogue.Hardcore)
                                         + "% at Hardcore), and a limit that is on is said to agree nothing exactly when it is under the least request the day's rule makes (StaffRules.AgreesNothing)");
    }

    // The players' cases: the thirteen rows of the Staff page in the screenshot (paid, going rate asked) at each limit, and a request for 3%.
    private static void RaiseLimitCases()
    {
        int[][] rows =
        {
            new[] { 1859, 2044 }, new[] { 2212, 2371 }, new[] { 1614, 1778 }, new[] { 1751, 1928 }, new[] { 1598, 1760 }, new[] { 1523, 1632 }, new[] { 2987, 3265 },
            new[] { 3437, 3747 }, new[] { 2793, 3069 }, new[] { 3210, 3511 }, new[] { 3042, 3334 }, new[] { 3267, 3569 }, new[] { 3215, 3479 }
        };
        int[] limits = { 7, 8, 9, 10, 11 }, wanted = { 0, 2, 3, 10, 13 };
        string said = string.Empty;
        bool all = true;
        for (int l = 0; l < limits.Length; l++)
        {
            int agreed = 0;
            foreach (int[] row in rows)
            {
                StaffFile f = Worker(row[0], row[1]);
                f.AskDay = 10;
                f.AskTo = row[1];
                agreed += StaffRules.WithinLimit(f, limits[l]) ? 1 : 0;
            }
            all &= agreed == wanted[l];
            said += " " + limits[l] + "%: " + agreed + (agreed == wanted[l] ? string.Empty : " (wanted " + wanted[l] + ")") + ";";
        }
        Check(all, "agreed by itself: of the thirteen requests on a player's Staff page (paid 15.23 to 34.37, each asking the going rate, 7 to 10 per cent more) 0, 2, 3, 10 and 13 are agreed at 7, 8, 9, 10 and 11 per cent:" + said);
        StaffFile three = Asking(2000, 60), more = Asking(2000, 61);
        Check(StaffRules.WithinLimit(three, 3) && !StaffRules.WithinLimit(three, 2) && !StaffRules.WithinLimit(more, 3),
            "agreed by itself: a request for 3% of the pay is agreed at a limit of 3% and not at 2%, and one for a hundredth more is not agreed at 3%");
    }

    // ---------------------------------------------------------------- who else answers

    // A person at Elm~7, where no manager answers anything, whose request for a tenth of their pay waits; the run's company around them (a manager at Broadway~12 who answers raises,
    // the executives in post, the hierarchy on).
    private static StaffFile RaiseFresh(out CoWorld w, out CoDayWorld world)
    {
        w = NewCoWorld(RunSeed);
        world = new CoDayWorld(w);
        StaffFile f = w.S.Staff[6];
        f.AskDay = Epoch + 1;
        f.AskTo = f.Paid + f.Paid / 10;
        return f;
    }

    // A manager at Elm~7 with one kind of work.
    private static ManagerFile RaiseManager(CoWorld w, ManagerTask task, bool seen)
    {
        var m = new ManagerFile { Place = "Elm~7", Id = "m7", Name = "Elm manager", Role = ManagerCatalogue.RolePlace, From = 1, Skill = 60, Seen = seen, Tasks = ManagerRules.Bit(task) };
        w.M.Managers.Add(m);
        return m;
    }

    private static void RaiseCase(List<string> bad, string name, bool got, bool want)
    {
        if (got != want)
        {
            bad.Add(name + " " + (got ? "agreed" : "left"));
        }
    }

    // Each way somebody else may answer a request, set up on its own, and the same case without it beside it: the manager who answers raises, the exception waiting for its approver,
    // and the people executive for a place that has a manager at work. A rule that agrees nothing fails the cases that agree; one that agrees everything, the cases that leave.
    private static void RaiseOthers()
    {
        CoWorld w;
        CoDayWorld world;
        StaffFile f;
        var bad = new List<string>();

        f = RaiseFresh(out w, out world);
        RaiseCase(bad, "nobody else", CompanyDay.AgreesByItself(world, f, 10), true);
        RaiseCase(bad, "nobody else, a limit under it", CompanyDay.AgreesByItself(world, f, 9), false);
        RaiseCase(bad, "nobody else, off", CompanyDay.AgreesByItself(world, f, 0), false);
        RaiseCase(bad, "nobody else, others", CompanyDay.RaiseWithOthers(world, f), false);
        f.AskDay = 0;
        RaiseCase(bad, "nobody waiting", CompanyDay.AgreesByItself(world, f, 10), false);
        f = RaiseFresh(out w, out world);
        w.PartOff[(int)CompanyPart.Staff] = true;
        RaiseCase(bad, "the staff part off", CompanyDay.AgreesByItself(world, f, 10), false);
        w.PartOff[(int)CompanyPart.Staff] = false;
        RaiseCase(bad, "the staff part on", CompanyDay.AgreesByItself(world, f, 10), true);
        Check(bad.Count == 0, "agreed by itself: a request inside the limit that nobody else answers is agreed, and not one past it, one with the limit off, none waiting or with the staff part off (" + string.Join("; ", bad) + ")");

        // The place's manager.
        bad.Clear();
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Raises, true);
        RaiseCase(bad, "a manager who answers raises", CompanyDay.AgreesByItself(world, f, 10), false);
        RaiseCase(bad, "a manager who answers raises, others", CompanyDay.RaiseWithOthers(world, f), true);
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Raises, false);
        RaiseCase(bad, "a manager not found at work", CompanyDay.AgreesByItself(world, f, 10), true);
        f = RaiseFresh(out w, out world);
        ManagerFile mgr = RaiseManager(w, ManagerTask.Raises, true);
        mgr.Id = f.Id;
        w.HierarchyOn = false;
        RaiseCase(bad, "the manager's own request", CompanyDay.AgreesByItself(world, f, 10), true);
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Hiring, true);
        w.HierarchyOn = false;
        RaiseCase(bad, "a manager with other work, hierarchy off", CompanyDay.AgreesByItself(world, f, 10), true);
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Raises, true);
        w.ManagersOn = false;
        w.HierarchyOn = false;
        RaiseCase(bad, "the managers off", CompanyDay.AgreesByItself(world, f, 10), true);
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Raises, true);
        w.H.Executives.RemoveAll(x => x.Area == (int)ExecSeat.Chro);
        RaiseCase(bad, "a manager, no people executive", CompanyDay.AgreesByItself(world, f, 10), false);
        w.H.Company.Steps[(int)PolicyField.AutoRaises] = 0;   // the policy takes the raises from the managers
        RaiseCase(bad, "the policy takes raises from managers", CompanyDay.AgreesByItself(world, f, 10), true);
        w.H.Company.Steps[(int)PolicyField.AutoRaises] = 1;
        RaiseCase(bad, "the policy gives raises to managers", CompanyDay.AgreesByItself(world, f, 10), false);
        Check(bad.Count == 0, "agreed by itself: a request a manager answers is left to them, and not when they are not found at work, it is their own, they have other work, the managers are off or the policy takes raises from them ("
                              + string.Join("; ", bad) + ")");

        // An exception to the pay band waiting for its approver.
        bad.Clear();
        f = RaiseFresh(out w, out world);
        Escalation open = w.H.Add(EscalationKind.Exception, "Elm~7", EscalationRules.LevelExecutive, Epoch + 1);
        open.Subject = f.Id;
        RaiseCase(bad, "an exception open for them", CompanyDay.AgreesByItself(world, f, 10), false);
        StaffFile other = w.S.Staff[9];
        other.AskDay = Epoch + 1;
        other.AskTo = other.Paid + other.Paid / 10;
        RaiseCase(bad, "an exception open for somebody else", CompanyDay.AgreesByItself(world, other, 10), true);
        w.HierarchyOn = false;
        RaiseCase(bad, "an exception, hierarchy off", CompanyDay.AgreesByItself(world, f, 10), true);
        w.HierarchyOn = true;
        w.H.Close(open, EscalationState.Cleared, string.Empty, Epoch + 2);
        RaiseCase(bad, "an exception closed", CompanyDay.AgreesByItself(world, f, 10), true);
        Check(bad.Count == 0, "agreed by itself: a request waiting as an exception for its approver is left to them, not when it is somebody else's, closed, or with the hierarchy off (" + string.Join("; ", bad) + ")");

        // The people executive, for a place that has a manager at work.
        bad.Clear();
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Hiring, true);
        RaiseCase(bad, "a manager at work and the people executive in post", CompanyDay.AgreesByItself(world, f, 10), false);
        RaiseCase(bad, "a manager at work and the people executive in post, others", CompanyDay.RaiseWithOthers(world, f), true);
        w.H.Executives.RemoveAll(x => x.Area == (int)ExecSeat.Cfo || x.Area == (int)ExecSeat.Coo);
        RaiseCase(bad, "the people executive alone", CompanyDay.AgreesByItself(world, f, 10), false);
        w.H.Executives.RemoveAll(x => x.Area == (int)ExecSeat.Chro);
        RaiseCase(bad, "no people executive", CompanyDay.AgreesByItself(world, f, 10), true);
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Hiring, false);
        RaiseCase(bad, "the manager not found at work", CompanyDay.AgreesByItself(world, f, 10), true);
        f = RaiseFresh(out w, out world);
        RaiseManager(w, ManagerTask.Hiring, true);
        w.Unmanaged.Add("Elm~7");
        RaiseCase(bad, "a place the company does not manage", CompanyDay.AgreesByItself(world, f, 10), true);
        f = RaiseFresh(out w, out world);
        RaiseCase(bad, "no manager at the place, the people executive in post", CompanyDay.AgreesByItself(world, f, 10), true);
        Check(bad.Count == 0, "agreed by itself: where the hierarchy runs a place with a manager at work is left to the people executive in post, and not when none is in post, the manager is not at work, the place is not managed, or it has no manager ("
                              + string.Join("; ", bad) + ")");
    }

    // Agreed by itself writes what the player's own Agree writes, and nothing where it is not agreed.
    private static void RaiseWrites()
    {
        CoWorld w;
        CoDayWorld world;
        StaffFile f = RaiseFresh(out w, out world), g = w.S.Staff[9];   // both paid 16.50 with a going rate of 19.00: the same case twice
        g.AskDay = f.AskDay;
        g.AskTo = f.AskTo;
        int day = Epoch + 2, raise;
        string before = RaiseState(f);
        bool left = CompanyDay.AgreeByItself(world, f, 9, day, out raise);
        bool untouched = !left && raise == 0 && RaiseState(f) == before;
        bool agreed = CompanyDay.AgreeByItself(world, f, 10, day, out raise);
        StaffRules.Answer(g, RaiseAnswer.Agree, day);
        Check(untouched && agreed && raise == 165 && RaiseState(f) == RaiseState(g) && f.Paid == 1815 && f.Raise == 165 && f.AskDay == 0 && f.AskTo == 0 && f.AskRest == day + CompanyCatalogue.RaiseRestDays
              && f.Standing == CompanyCatalogue.StandingStart + CompanyCatalogue.StandAgree,
            "agreed by itself: a request past the limit is left with nothing written, and one inside it is paid the going rate with the standing, the rest and the agreed raise the player's own Agree gives ("
            + RaiseState(f) + " against " + RaiseState(g) + ")");
        Check(agreed && !CompanyDay.AgreeByItself(world, f, 10, day + 1, out raise) && raise == 0, "agreed by itself: a request agreed is not agreed a second time");
    }

    // ---------------------------------------------------------------- the morning's pass

    // Forty people short of the going rate by 11, 15, 20, 25 and 30 per cent of it, every eighth at Broadway~12 where a manager answers raises and the rest at Elm~7: on Standard each
    // asks on the seventh day, for 12, 18, 25, 33 and 43 per cent more than they are paid.
    private static List<StaffFile> RaiseCrew(CoWorld w, out int[] gaps)
    {
        gaps = new[] { 110, 150, 200, 250, 300 };
        w.S.Staff.Clear();
        for (int i = 0; i < 40; i++)
        {
            int wage = 1200 + i * 90;
            w.S.Staff.Add(new StaffFile { Id = "c" + i, Place = i % 8 == 0 ? "Broadway~12" : "Elm~7", Name = "Crew " + i, Wage = wage, Going = wage * 1000 / (1000 - gaps[i % gaps.Length]), DayHours = 800 });
        }
        return w.S.Staff;
    }

    // One morning as the director takes it: each person's day, then a request agreed by itself. Returns who was agreed.
    private static List<StaffFile> RaiseMorning(ICompanyDay world, List<StaffFile> crew, int limit, int day, out int raised)
    {
        var agreed = new List<StaffFile>();
        raised = 0;
        foreach (StaffFile f in crew)
        {
            StaffRules.Day(f, CompanyCatalogue.Standard, day);
            int raise;
            if (CompanyDay.AgreeByItself(world, f, limit, day, out raise))
            {
                agreed.Add(f);
                raised += raise;
            }
        }
        return agreed;
    }

    private static void RaiseMorningPass()
    {
        CoWorld w = NewCoWorld(RunSeed);
        int[] gaps;
        List<StaffFile> crew = RaiseCrew(w, out gaps);
        var world = new CoDayWorld(w);
        var ids = new List<string>();
        int total = 0;
        bool early = false;
        for (int day = 1; day <= 7; day++)
        {
            int raised;
            List<StaffFile> agreed = RaiseMorning(world, crew, 15, day, out raised);
            early |= day < 7 && agreed.Count > 0;
            foreach (StaffFile f in agreed)
            {
                ids.Add(f.Id);
            }
            total += raised;
        }
        string wanted = "c5,c10,c15,c20,c25,c30,c35";
        Check(!early && string.Join(",", ids) == wanted,
            "agreed by itself: on the seventh morning, when forty people ask, those agreed are the seven at Elm~7 whose request is inside 15% and nobody else (" + string.Join(",", ids) + " against " + wanted + ")");
        bool right = true, left = true;
        int waiting = 0;
        long sum = 0;
        for (int i = 0; i < crew.Count; i++)
        {
            StaffFile f = crew[i];
            sum += f.Raise;
            waiting += f.AskDay > 0 ? 1 : 0;
            if (ids.Contains(f.Id))
            {
                right &= f.AskDay == 0 && f.Paid == f.Going && f.Raise == f.Going - f.Wage && f.Standing == CompanyCatalogue.StandingStart - 7 + CompanyCatalogue.StandAgree && f.AskRest == 7 + CompanyCatalogue.RaiseRestDays;
            }
            else
            {
                left &= f.AskDay == 7 && f.AskTo == f.Going && f.Raise == 0;
            }
        }
        Check(right && ids.Count == 7, "agreed by itself: each of the seven agreed is paid the going rate with the standing and the rest an agreed request gives");
        Check(left && waiting == 33, "agreed by itself: a request past the limit, and one a manager answers, are left as they were for the player (" + waiting + " of 40 wait)");
        Check(total == sum && total > 0, "agreed by itself: the raises told in the notice (" + total + ") are the raises given (" + sum + ")");
        int more = 0;
        for (int day = 8; day <= 30; day++)
        {
            int raised;
            more += RaiseMorning(world, crew, 15, day, out raised).Count;
        }
        Check(more == 0 && ids.Count == 7, "agreed by itself: the requests left over lapse and are asked again, and are still left, and the seven agreed are paid the going rate and do not ask again (" + more + " more agreed in 23 days)");

        // The limit's whole range over the same forty: each step of the crew's requests is agreed, at Elm~7, as the limit reaches it.
        int[] limits = { 0, 12, 13, 17, 18, 24, 26, 33, 34, 42, 43, 50, 999 }, counts = { 0, 0, 7, 7, 14, 14, 21, 21, 28, 28, 35, 35, 35 };
        string said = string.Empty;
        bool steps = true;
        for (int s = 0; s < limits.Length; s++)
        {
            CoWorld fresh = NewCoWorld(RunSeed);
            List<StaffFile> people = RaiseCrew(fresh, out gaps);
            var freshWorld = new CoDayWorld(fresh);
            int agreed = 0;
            for (int day = 1; day <= 7; day++)
            {
                int raised;
                agreed += RaiseMorning(freshWorld, people, limits[s], day, out raised).Count;
            }
            steps &= agreed == counts[s];
            said += " " + limits[s] + "%: " + agreed + (agreed == counts[s] ? string.Empty : " (wanted " + counts[s] + ")") + ";";
        }
        Check(steps, "agreed by itself: seven of the forty are agreed as the limit reaches 13%, 18%, 26%, 34% and 43%, and a limit past the most is the most:" + said);
    }

    // Off, the pass changes nothing: a crew run through the very step at a limit of 0 is a crew run through the days alone, and at 15 the seven it agrees are the difference.
    private static void RaiseOptionOff()
    {
        CoWorld wa = NewCoWorld(RunSeed), wb = NewCoWorld(RunSeed), wc = NewCoWorld(RunSeed);
        int[] gaps;
        List<StaffFile> off = RaiseCrew(wa, out gaps), days = RaiseCrew(wb, out gaps), on = RaiseCrew(wc, out gaps);
        var offWorld = new CoDayWorld(wa);
        var onWorld = new CoDayWorld(wc);
        int agreedOff = 0, agreedOn = 0;
        for (int day = 1; day <= 30; day++)
        {
            int raised;
            agreedOff += RaiseMorning(offWorld, off, 0, day, out raised).Count;
            agreedOn += RaiseMorning(onWorld, on, 15, day, out raised).Count;
            foreach (StaffFile f in days)
            {
                StaffRules.Day(f, CompanyCatalogue.Standard, day);
            }
        }
        int same = 0, differ = 0;
        for (int i = 0; i < days.Count; i++)
        {
            same += RaiseState(off[i]) == RaiseState(days[i]) ? 1 : 0;
            differ += RaiseState(on[i]) != RaiseState(days[i]) ? 1 : 0;
        }
        Check(agreedOff == 0 && same == 40 && agreedOn == 7 && differ == 7,
            "agreed by itself: with the option at 0 nothing is agreed in thirty days and all forty are exactly as the days alone leave them (" + same + " of 40 the same), and at 15 the seven it agrees are the difference, each agreed once ("
            + agreedOn + " agreed, " + differ + " differ)");
    }

    // The run: a hundred and twenty days of the company with the option at 20, by the same steps the director takes. The people at Broadway~12, where a manager answers raises, live
    // exactly the days they live with the option off; at Elm~7, with nobody to answer, the requests inside the limit are agreed; and the run is the same twice.
    private static void RaiseRun()
    {
        CoWorld on = NewCoWorld(RunSeed), off = NewCoWorld(RunSeed), twice = NewCoWorld(RunSeed);
        on.AgreeLimit = 20;
        twice.AgreeLimit = 20;
        for (int day = Epoch + 1; day <= Epoch + RunDays; day++)
        {
            CoDay(on, day);
            CoDay(off, day);
            CoDay(twice, day);
        }
        Func<CoWorld, string, string> at = (world, place) =>
        {
            var lines = new List<string>();
            foreach (StaffFile f in world.S.Staff)
            {
                lines.Add(f.Place == place ? f.Id + "=" + RaiseState(f) : string.Empty);
            }
            return string.Join("|", lines);
        };
        Check(on.Agreed >= 2 && off.Agreed == 0, "agreed by itself, in the run: the option at 20 agrees requests over the days (" + on.Agreed + ") and at 0 none (" + off.Agreed + ")");
        Check(on.Agreed >= 2 && at(on, "Broadway~12") == at(off, "Broadway~12") && at(on, "Broadway~12").Length > 100,
            "agreed by itself, in the run: the people at Broadway~12, where a manager answers raises, live exactly the same days with the option at 20 as at 0, while the option agrees others");
        int raisedOn = 0, raisedOff = 0;
        foreach (StaffFile f in on.S.Staff)
        {
            raisedOn += f.Place == "Elm~7" && f.Raise > 0 ? 1 : 0;
        }
        foreach (StaffFile f in off.S.Staff)
        {
            raisedOff += f.Place == "Elm~7" && f.Raise > 0 ? 1 : 0;
        }
        Check(raisedOn >= 2 && raisedOff == 0, "agreed by itself, in the run: at Elm~7, with nobody else to answer, the option gives raises (" + raisedOn + " people) that no one gives with it off (" + raisedOff + ")");
        Check(on.Agreed >= 2 && string.Join("|", on.Log) == string.Join("|", twice.Log) && on.Agreed == twice.Agreed && string.Join(";", CoWrite(on)) == string.Join(";", CoWrite(twice)),
            "agreed by itself, in the run: the same seed with the option at 20 agrees the same requests, lives the same days twice and writes the same save");
    }

    // The step is taken for every person each morning: 2,000 people with a request waiting, a third of them at a place whose manager answers raises, asked afresh in each of ten
    // rounds, in the least of three tries (a loaded machine slows one).
    private static void RaiseTimed()
    {
        CoWorld w = NewCoWorld(RunSeed);
        var world = new CoDayWorld(w);
        var crew = new List<StaffFile>();
        for (int i = 0; i < 2000; i++)
        {
            crew.Add(new StaffFile { Id = "t" + i, Place = i % 3 == 0 ? "Broadway~12" : "Elm~7", Wage = 1500 + i, Going = 1700 + i, DayHours = 800 });
        }
        double best = double.MaxValue;
        int agreed = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            agreed = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int round = 0; round < 10; round++)
            {
                foreach (StaffFile f in crew)
                {
                    int raise;
                    f.Raise = 0;
                    f.AskDay = 5;
                    f.AskTo = f.Wage + f.Wage / 10;
                    agreed += CompanyDay.AgreeByItself(world, f, 15, 6, out raise) ? 1 : 0;
                }
            }
            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }
        Check(agreed == 10 * (2000 - 667) && best < 250,
            "agreed by itself: 20,000 steps over 2,000 people, a third of them left to a manager, agree " + agreed + " requests in " + best.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " ms, the least of three tries, against a budget of 250 ms");
    }

    // ---------------------------------------------------------------- the screens and the director's source

    private static void RaiseAnswerScreens()
    {
        CompanyFacts facts = UiSamples.CompanyFactsOf(UiWords("en"), "staff_summary");
        var drawn = new List<int>();
        CompanyCard top = facts.Staff != null ? facts.Staff.Top : null;
        for (int i = 0; top != null && top.Blocks != null && i < top.Blocks.Length; i++)
        {
            CompanyBlock block = top.Blocks[i];
            for (int b = 0; block.Kind == CompanyBlockKind.Buttons && block.Buttons != null && b < block.Buttons.Length; b++)
            {
                if (block.Buttons[b].Act.Do == CompanyDo.RaisesAll)
                {
                    drawn.Add(block.Buttons[b].Act.A);
                }
            }
        }
        Check(drawn.Count == 3 && drawn[0] == 0 && drawn[1] == 2 && drawn[2] == 1, "offer less to all: the Staff page draws Agree to all, Offer less to all and Refuse all, in that order (" + string.Join(",", drawn) + ")");
        string live = ModSource(@"Ui\Company\CompanyLive.Staff.cs"), does = ModSource(@"Ui\Company\CompanyLiveDoes.cs"), app = ModSource(@"Economy\Company\CompanyApp.Staff.cs");
        Check(live.Contains("Btn(CompanyText.T(\"reallife_co_staff_less_all\"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaisesAll, null, 2), UiAction.Option)"),
            "offer less to all: the live Staff page draws the button, as the person's own page draws Offer less");
        Check(does.Contains("d.AnswerAllRaises(a.A == 0 ? RaiseAnswer.Agree : a.A == 2 ? RaiseAnswer.Less : RaiseAnswer.Refuse);"), "offer less to all: the press map answers every request with the offer-less answer");
        Check(app.Contains("InfoBox.Button(buttons, InfoBox.Additional, \"reallife_co_staff_less_all\", () => _director.AnswerAllRaises(RaiseAnswer.Less));"),
            "offer less to all: the game-style Staff page has it too, between Agree to all and Refuse all");
        Check(ModSource(@"Ui\UiScreens.Company.cs").Contains("\"reallife_co_staff_less_all\""), "offer less to all: the Company screen names the button's text, so every language is held to it");
    }

    // What the director's morning must still do itself, which is plumbing: read the option, take each person's day, call the one step the run calls, count and tell. Its rules
    // are CompanyDay's and StaffRules', run above; a director that makes its own decision here fails these.
    private static void RaiseAnswerDirector()
    {
        string staff = CompanySource("CompanyDirector.Staff.cs"), managers = CompanySource("CompanyDirector.Managers.cs"), world = CompanySource("CompanyDirector.DayWorld.cs");
        string day = CompanyMethod(staff, "partial void StaffDay(int day)"), all = CompanyMethod(staff, "public void AnswerAllRaises(RaiseAnswer answer)");
        string limit = CompanyMethod(staff, "private static int AgreeLimit()");
        Check(day.Length > 0 && InOrder(day, "int limit = AgreeLimit()", "StaffRules.Day(f, _level, day", "CompanyDay.AgreeByItself(this, f, limit, day, out raise)", "agreed++", "\"reallife_co_note_raise_auto_one\"",
                  "\"reallife_co_note_raise_auto_many\"", "StaffRules.AgreesNothing(limit, _level)", "\"reallife_co_note_raise_auto_low\"", "StaffRules.LimitLeast(_level)"),
            "agreed by itself: the day's pass reads the limit once, takes each person's day, then agrees through CompanyDay.AgreeByItself, the very step the run takes, counts and tells");
        Check(day.Contains("if (!agree && note == StaffNote.Asked && !Manages(f.Place, ManagerTask.Raises, f.Id))")
              && day.Contains("if (agree)\n                {\n                    agreedRaise += raise;\n                    agreed++;\n                    firstAgreed = firstAgreed ?? f;"),
            "agreed by itself: a request agreed is not also asked about, and what is counted, told and added up is the requests the one step agreed and nothing else");
        Check(day.Contains("int limit = AgreeLimit(), agreed = 0, agreedRaise = 0;") && day.Contains("if (asked > 0 && StaffRules.AgreesNothing(limit, _level) && _lowTold != limit * 10 + _level)")
              && day.Contains("{ \"least\", CompanyText.Share(StaffRules.LimitLeast(_level) * 10) }"),
            "agreed by itself: the limit is read as it is set, and a limit under every request there is is said once for each limit at each strictness, with the least there is at this strictness");
        Check(day.Contains("CompanyDay.AgreeByItself(this, f, limit, day, out raise)") && !staff.Contains("WithinLimit(") && !staff.Contains("RaiseWithOthers(") && !staff.Contains("AgreesByItself(")
              && !staff.Contains("StaffRules.Answer(f, RaiseAnswer.Agree"),
            "agreed by itself: the director holds no rule of its own for it: the limit, the managers, the exceptions and the executive are CompanyDay's and StaffRules', and it asks them through the one step");
        Check(managers.Contains("return CompanyDay.Manages(this, place, task, employee);") && world.Contains("bool ICompanyDay.PolicyAllows(string place, ManagerTask task) => PolicyAllows(ManagedAt(place), task);"),
            "agreed by itself: whether a manager answers a kind of thing is one rule, CompanyDay.Manages, which the director's notices ask as the agreement does");
        string tests = CompanySource("CompanyDirector.Tests.cs");
        string test = CompanyMethod(tests, "public void TestAgree()");
        Check(test.Length > 0 && test.Contains("AgreeLimit()") && test.Contains("!CompanyDay.RaiseWithOthers(this, x)") && test.Contains("CompanyDay.AgreeByItself(this, f, limit, Save.Day, out raise)")
              && test.Contains("\"reallife_co_test_agree_off\"") && test.Contains("\"reallife_co_test_agree_none\"") && test.Contains("BeforeTest();")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains(".AddButton(\"reallife_co_test_agree\", () => Economy.CompanyDirector.Instance?.TestAgree())"),
            "agreed by itself: a test button asks for a request inside the limit and agrees it at once by the day's own step, says why not while the option is 0, and can be put back");
        Check(limit.Contains("CompanyCatalogue.Clamp(Settings.EconomyCompanyAgreeUpTo, 0, CompanyCatalogue.AgreeUpToMost)"), "agreed by itself: the option is read held to its ends");
        Check(all.Length > 0 && all.Contains("StaffRules.AnswerAll(_state.Staff, answer, Save.Day, out raise)") && all.Contains("answer == RaiseAnswer.Less ? \"reallife_co_note_raise_all_less\""),
            "offer less to all: every request is answered by StaffRules.AnswerAll, and offering less has its own notice");
    }

    private static void RaiseAnswerOption()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), list = ModSource(@"Meta\FeatureList.Economy.cs");
        Check(settings.Contains("public const string EconomyCompanyAgreeUpToId = \"economy_company_agree_up_to\";") && settings.Contains("public static int EconomyCompanyAgreeUpTo = 0;")
              && settings.Contains("EconomyCompanyAgreeUpTo = Int(modId, EconomyCompanyAgreeUpToId, EconomyCompanyAgreeUpTo);"),
            "agreed by itself: the option is saved with the others and starts at 0, which is off");
        Check(mod.Contains(".AddSlider(Settings.EconomyCompanyAgreeUpToId, \"reallife_co_opt_agree_up_to\", 0, Economy.CompanyCatalogue.AgreeUpToMost, Settings.EconomyCompanyAgreeUpTo,")
              && mod.Contains("v => Settings.EconomyCompanyAgreeUpTo = v, Meta.SettingsTable.SliderPercent)"),
            "agreed by itself: a slider on the options page from 0 to its most, shown as a per cent");
        Check(list.Contains("Settings.EconomyCompanyAgreeUpToId"), "agreed by itself: the option is the Company feature's");
        TreeSection section = OptionsTree.SectionOf("economy_company_agree_up_to");
        TreeRow row = OptionsTree.Row("economy_company_agree_up_to");
        Check(section != null && section.Id == "company" && row != null && Array.IndexOf(row.Needs, "economy_company_staff") >= 0 && !row.Free && section.Rows.Count <= OptionsTree.MostRows
              && row.TipKey == "reallife_opt_tip_economy_company_agree_up_to",
            "agreed by itself: the option stands under Running a business in the options tree, kept back while the staff part is off, with its sentence");
    }

    // ---------------------------------------------------------------- the wage in My Employees' review before hiring

    private static void RaiseHireReview()
    {
        string batch = ModSource(@"Ui\Employees\StaffBatchHire.cs"), shown = ModSource(@"Core\WageShown.cs");
        Check(batch.Contains("Wages.Add(WageShown.PaidCents(e));") && !batch.Contains("e.hourlyWage * 100f"),
            "one wage: the review before hiring several candidates shows what each costs an hour at the city's prices, the figure of the candidates' table, not the game's");
        Check(shown.Contains("return e == null ? 0 : PaidWage.Shown(e.hourlyWage, e.id, Ours(e));"), "one wage: that figure is PaidWage.Shown, for somebody this machine pays or not");
        // The candidates ask 18.00, 21.00 and 25.50 at prices 25% up, and a raise of 1.50 is agreed for the first: 24.00 and 21.00 and 25.50 are the game's own, 24.00, 26.25 and 31.88 what is paid.
        Func<int> held = Prices.Factor;
        Func<string, int> raise = PaidWage.Raise;
        try
        {
            Prices.Factor = () => 1250;
            PaidWage.Raise = id => id == "first" ? 150 : 0;
            int[] ours = { PaidWage.Shown(18f, "first", true), PaidWage.Shown(21f, "second", true), PaidWage.Shown(25.5f, "third", true) };
            int[] game = { PaidWage.Shown(18f, "first", false), PaidWage.Shown(21f, "second", false), PaidWage.Shown(25.5f, "third", false) };
            Check(ours[0] == 2400 && ours[1] == 2625 && ours[2] == 3188 && game[0] == 1800 && game[1] == 2100 && game[2] == 2550,
                "one wage: a candidate this machine pays is shown at the city's prices with any raise agreed (" + ours[0] + ", " + ours[1] + ", " + ours[2] + ") and another player's at the game's own (" + game[0] + ", " + game[1] + ", " + game[2] + ")");
            StaffSheet sheet = StaffBatchRules.HireSheet(new[] { "a", "b", "c" }, new[] { "A", "B", "C" }, new[] { "r", "r", "r" }, ours, new[] { BatchState.Ready, BatchState.Ready, BatchState.NoFit }, "Shop");
            Check(sheet.Now == 2400 + 2625 && sheet.Next == sheet.Now && sheet.Left == 1 && sheet.Lines[2].Now == 3188,
                "one wage: a review of hiring adds up the wages shown of those it will hire, and names each (" + sheet.Now + ")");
        }
        finally
        {
            Prices.Factor = held;
            PaidWage.Raise = raise;
        }
    }

    // ---------------------------------------------------------------- the texts

    private static void RaiseAnswerTexts()
    {
        string[] keys =
        {
            "reallife_co_staff_less_all", "reallife_co_note_raise_all_less", "reallife_co_opt_agree_up_to", "reallife_opt_tip_economy_company_agree_up_to",
            "reallife_co_note_raise_auto_one", "reallife_co_note_raise_auto_many", "reallife_co_note_raise_auto_low", "reallife_co_test_agree", "reallife_co_test_agree_off",
            "reallife_co_test_agree_none"
        };
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        bool read = true;
        for (int l = 0; l < words.Length; l++)
        {
            read &= words[l] != null;
        }
        if (!read)
        {
            Check(false, "raises answered together and by themselves: a language's file could not be read from " + LocalesFolder());
            return;
        }
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(words, codes, key);
            Check(lacking.Length == 0, "raises answered together and by themselves: " + key + " is in every language with the English names in curly brackets (" + lacking + ")");
        }
        Dictionary<string, string> en = words[0];
        Func<int, string, string> say = (l, key) => words[l].TryGetValue(key, out string v) ? v : string.Empty;
        string less = say(0, "reallife_co_note_raise_all_less"), one = say(0, "reallife_co_note_raise_auto_one"), many = say(0, "reallife_co_note_raise_auto_many"), low = say(0, "reallife_co_note_raise_auto_low");
        string label = say(0, "reallife_co_opt_agree_up_to"), tip = say(0, "reallife_opt_tip_economy_company_agree_up_to"), offer = say(0, "reallife_co_staff_less_all"), button = say(0, "reallife_co_test_agree");
        Check(Fits(en, "reallife_co_note_raise_all_less") && Fits(en, "reallife_co_note_raise_auto_one") && Fits(en, "reallife_co_note_raise_auto_many") && Fits(en, "reallife_co_note_raise_auto_low")
              && Fits(en, "reallife_co_test_agree_off") && Fits(en, "reallife_co_test_agree_none"), "raises answered together and by themselves: the notices are 240 characters or fewer");
        bool short240 = true;
        for (int l = 0; l < codes.Length; l++)
        {
            foreach (string key in new[] { "reallife_co_note_raise_all_less", "reallife_co_note_raise_auto_one", "reallife_co_note_raise_auto_many", "reallife_co_note_raise_auto_low" })
            {
                short240 &= say(l, key).Length > 0 && say(l, key).Length <= 240;
            }
        }
        Check(short240, "raises answered together and by themselves: the four notices are 240 characters or fewer in every language");
        Check(button.Length > 0 && button.Length <= 80 && button.Split(' ').Length <= 13 && char.IsUpper(button[0]) && !button.StartsWith("Test", StringComparison.OrdinalIgnoreCase),
            "agreed by itself: the test button's label keeps to the options page's limits for a test");
        Check(less.Contains("{count}") && less.Contains("{raise}") && one.Contains("{name}") && one.Contains("{raise}") && one.Contains("{limit}") && one.Contains("{standing}")
              && many.Contains("{count}") && many.Contains("{raise}") && many.Contains("{limit}") && low.Contains("{limit}") && low.Contains("{least}"),
            "raises answered together and by themselves: each notice names what it counts");
        int tipWords = tip.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;   // the options page's limits: a label of 9 words and 60 characters, a sentence of 32 words and 190 characters
        Check(label.Split(' ').Length <= 9 && label.Length <= 60 && label.Length > 0 && !label.EndsWith(".") && tipWords <= 32 && tip.Length <= 190 && tip.EndsWith("."),
            "agreed by itself: the option's label and sentence keep to the options page's limits (" + label.Split(' ').Length + " words, " + tipWords + " words, " + tip.Length + " characters)");
        // The sentence names the least limits as the code has them, in every language; the notice is given the same figure for the strictness in force.
        string least = StaffRules.LimitLeast(CompanyCatalogue.Hardcore) + "%", most = StaffRules.LimitLeast(CompanyCatalogue.Relaxed) + "%";
        bool figures = true;
        for (int l = 0; l < codes.Length; l++)
        {
            string text = say(l, "reallife_opt_tip_economy_company_agree_up_to");
            figures &= Regex.IsMatch(text, @"(?<!\d)" + (StaffRules.LimitLeast(CompanyCatalogue.Hardcore)) + @"(?!\d)") && Regex.IsMatch(text, @"(?<!\d)" + (StaffRules.LimitLeast(CompanyCatalogue.Relaxed)) + @"(?!\d)");
        }
        Check(tip.Contains("under " + least + " (Hardcore) to " + most + " (Relaxed) agree nothing") && figures,
            "agreed by itself: the option's sentence says that limits under " + least + " (Hardcore) to " + most + " (Relaxed), the least that can agree anything, agree nothing, in every language");
        bool plain = true;
        for (int l = 0; l < codes.Length; l++)
        {
            foreach (string key in keys)
            {
                string v = say(l, key);
                plain &= v.Length > 0 && v.IndexOf('!') < 0 && v.IndexOf((char)0x2014) < 0 && v.IndexOf((char)0x2013) < 0 && !v.Contains("  ");
            }
        }
        Check(plain, "raises answered together and by themselves: no text has a dash, an exclamation mark or a double space in any language");
        // The Guide names the two as they are worded: StandardCheck's bold names must be texts of the mod.
        Check(en.TryGetValue("reallife_guide_economy_company", out string guide) && guide.Contains("<b>" + offer + "</b>") && guide.Contains("<b>" + label + "</b>") && guide.Contains("<b>Give a raise</b>")
              && guide.Contains("{co_raise_rest} days for the next"), "raises answered together and by themselves: the Guide's page on running a business names Offer less to all and the option as they are worded");
        Check(WhatsNewHas(en, "Offer less to all beside Agree to all and Refuse all") && WhatsNewHas(en, "agrees raises automatically") && WhatsNewHas(en, "Fixed: the review before hiring several candidates in My Employees"),
            "raises answered together and by themselves: What's new has the three lines in English, in any of its rows");
    }
}
