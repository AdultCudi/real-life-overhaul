using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;

// An employee this mod let go resigned twice (found by the fix for a rival's offer, 19 Oct 2026). Business pressure's
// dismissal, the company's departures and the tech sector's resignations each took the first steps of the game's
// ComplaintResign (no satisfaction, the headhunter, Resign), but the game calls those inside its employees' hour
// (EmployeeHelper.RunHourly), which takes the employee off the list right after; called from anywhere else they stayed on
// it, and at their next hour the game's own check for no satisfaction (LowSatisfactionComplaint) resigned them again: a
// second complaint message, a second wage, a second notice. A satisfaction update the game still had queued could lift
// them off nothing first, and then they never left at all. And the company's and the tech sector's departures, made in
// the game's day event, paid the day that had ended a second time: the game pays it just before that event and clears the
// count just after. Now Core/StaffLeave takes ComplaintResign's steps, clears a count the game's night has paid, and takes
// the employee off the list as that hour does (Core/StaffLeaveRules).
// And one employee could have two rivals' offers at once: the company's and the tech sector's own, and the game's
// (EmployeeInstance.PoachByRival, made by its special rivals, which the mod cannot stop, or by business pressure). Now the
// mod makes none to somebody with another open, business pressure's poaching passes over those the company or the tech
// sector has one in hand for, and an offer of the mod's gives way the moment the game's opens.
// First the rules, then a model of the game's employees, hour and night as the decompiled game has them, with the old way
// and the new; the two offers on a model of both; the sources; the cost; the texts.
internal static partial class Program
{
    private static void StaffLeaving()
    {
        StaffLeaveRulesChecks();
        StaffLeaveGame();
        StaffLeaveOffers();
        StaffLeaveTech();
        StaffLeaveSources();
        StaffLeavePace();
        StaffLeaveWords();
    }

    // ---------------------------------------------------------------- the rules

    private static void StaffLeaveRulesChecks()
    {
        Check(StaffLeaveRules.CountPaid(8, 99, true, 100, 0) && StaffLeaveRules.HoursPaid(8, 99, true, 100, 0) == 0,
            "staff leaving: in the game's day event the count still holds the day that ended, which its night has paid: none paid again");
        Check(!StaffLeaveRules.CountPaid(6, 100, true, 100, 14) && StaffLeaveRules.HoursPaid(6, 100, true, 100, 14) == 6,
            "staff leaving: a count of today is owed and paid by the resignation");
        Check(!StaffLeaveRules.CountPaid(1, 100, true, 100, 0) && StaffLeaveRules.HoursPaid(1, 100, true, 100, 0) == 1,
            "staff leaving: an hour worked at the first hour of today is today's, and paid");
        Check(!StaffLeaveRules.CountPaid(0, 40, true, 100, 0) && StaffLeaveRules.HoursPaid(0, 40, true, 100, 0) == 0
              && StaffLeaveRules.HoursPaid(-3, 100, true, 100, 9) == 0,
            "staff leaving: a count of nothing (or below) pays nothing and is cleared of nothing");
        Check(StaffLeaveRules.CountPaid(5, -1, true, 100, 9) && StaffLeaveRules.CountPaid(5, 12, true, 100, 23),
            "staff leaving: a count never cleared since an earlier day (a stopped night) is the night's, paid already");
        Check(StaffLeaveRules.CountPaid(8, -1, false, 100, 0) && !StaffLeaveRules.CountPaid(6, -1, false, 100, 14)
              && StaffLeaveRules.HoursPaid(6, -1, false, 100, 14) == 6,
            "staff leaving: without the game's lastWorkedDay (a newer game) a count at the first hour of a day is the day before's, any other today's");
        Check(StaffLeaveRules.Why(true, false, true, false, false) == StaffLeaveRules.Refusal.None,
            "staff leaving: one of the player's employees, on the list, not being replaced, at their own business, is let go");
        Check(StaffLeaveRules.Why(false, true, true, true, true) == StaffLeaveRules.Refusal.Missing
              && StaffLeaveRules.Why(true, true, true, true, true) == StaffLeaveRules.Refusal.Candidate
              && StaffLeaveRules.Why(true, false, false, true, true) == StaffLeaveRules.Refusal.NotListed
              && StaffLeaveRules.Why(true, false, true, true, true) == StaffLeaveRules.Refusal.BeingReplaced
              && StaffLeaveRules.Why(true, false, true, false, true) == StaffLeaveRules.Refusal.Foreign,
            "staff leaving: nobody, a candidate, one off the list (gone already), one the headhunter replaces, one of another player's: none is let go, each for its own reason");
        var words = new HashSet<string>(StringComparer.Ordinal);
        bool plain = true;
        foreach (StaffLeaveRules.Refusal r in Enum.GetValues(typeof(StaffLeaveRules.Refusal)))
        {
            string w = StaffLeaveRules.Words(r);
            words.Add(w);
            plain &= !string.IsNullOrEmpty(w) && !w.Contains("—") && !w.Contains("–") && !w.Contains(" - ") && !w.Contains("!");
        }
        Check(plain && words.Count == Enum.GetValues(typeof(StaffLeaveRules.Refusal)).Length,
            "staff leaving: each refusal has its own plain words for Player.log");
        Check(RivalOfferRules.Open("special_rival_7", true, 3) && RivalOfferRules.Open("special_rival_7", true, 1),
            "two offers: the game's offer with a rival named and days left is open");
        Check(!RivalOfferRules.Open("special_rival_7", true, 0) && !RivalOfferRules.Open("special_rival_7", true, -1)
              && !RivalOfferRules.Open(string.Empty, true, 3) && !RivalOfferRules.Open(null, true, 3),
            "two offers: answered, run out, given up (no days) or matched (no rival) is not open");
        Check(RivalOfferRules.Open("special_rival_7", false, 0) && !RivalOfferRules.Open(string.Empty, false, 5),
            "two offers: without the game's days (a newer game) an offer is taken as open while a rival is named");
    }

    // ---------------------------------------------------------------- a model of the game's employees

    // One employee as the game keeps one (EmployeeInstance): what a resignation, the hour and the night read and write.
    private sealed class LeavePerson
    {
        public string Name;
        public float Satisfaction = 70f;
        public int Worked;                  // workedHoursToday: what PayWage pays
        public int LastWorked = -1;         // lastWorkedDay (private): set as an hour is counted
        public bool Assigned = true;        // assignedAddress set (OnRemove clears it, and the shifts with it)
        public bool OnShift = true;         // a shift this hour (IsWorking)
        public bool Absent, BeingReplaced, Complaining;
        public bool Headhunter;             // their plan replaces them automatically on resigning, and its fee can be paid
        public float Lift;                  // what UpdateSatisfaction adds at nothing: the net of their demands, -0.6 to 0.6
    }

    // The game around them: its list, its queue of satisfaction updates, its notices, the wages and messages it sends.
    private sealed class LeaveWorld
    {
        public int Day = 100, Hour = 14;
        public readonly List<LeavePerson> Staff = new List<LeavePerson>();   // GameInstance.EmployeeInstances
        public readonly List<LeavePerson> Queue = new List<LeavePerson>();   // UpdateSatisfactionWork: objects, kept after removal
        public readonly List<string> Notices = new List<string>();           // ResignedEmployeesAndLastRegistrationAssigned
        public int Shown, Paid, Messages, Complaints, Replacing;

        public LeavePerson Hire(string name, int worked, int lastWorked)
        {
            var p = new LeavePerson { Name = name, Worked = worked, LastWorked = lastWorked };
            Staff.Add(p);
            return p;
        }

        // EmployeeInstance.PayWage: the hours on the count (Resign has unassigned them: no driver's fixed day).
        private void PayWage(LeavePerson p)
        {
            Paid += p.Worked;
        }

        // EmployeeInstance.Resign: OnRemove, the tasks, PayWage, the notice's line. Not off the list.
        public void Resign(LeavePerson p)
        {
            p.Assigned = false;
            p.OnShift = false;
            PayWage(p);
            Notices.Add(p.Name);
        }

        // EmployeeInstance.ReplaceAutomaticallyOnResign: HeadhunterPlan.SetEmployeeToReplace's ResignBeforeReplacement
        // (PayWage) and isBeingReplaced, which the game's hour passes by.
        public bool ReplaceOnResign(LeavePerson p)
        {
            if (!p.Headhunter)
            {
                return false;
            }
            PayWage(p);
            p.BeingReplaced = true;
            Replacing++;
            return true;
        }

        // EmployeeInstance.ComplaintResign
        private void ComplaintResign(LeavePerson p)
        {
            p.Complaining = false;
            p.Satisfaction = 0f;
            if (!ReplaceOnResign(p))
            {
                Resign(p);
            }
        }

        // The queue worked off (ProgressWork over the next frames, ForceCompleteAllWork at the next hour): UpdateSatisfaction.
        public void WorkQueue()
        {
            foreach (LeavePerson p in Queue)
            {
                p.Satisfaction = Math.Max(0f, Math.Min(100f, p.Satisfaction + p.Lift));
            }
            Queue.Clear();
        }

        // EmployeeHelper.RunHourly: the queue completed, each employee's hour (EmployeeInstance.RunHourly with
        // RunComplaintsHourly: LowSatisfactionComplaint, under 10 and hours to answer 0), the list's resigned taken off,
        // then the hour's notices shown.
        public void GameHour()
        {
            WorkQueue();
            for (int i = Staff.Count - 1; i >= 0; i--)
            {
                LeavePerson p = Staff[i];
                if (p.BeingReplaced)
                {
                    continue;
                }
                if (p.OnShift && p.Assigned)
                {
                    p.Worked++;
                    p.LastWorked = Day;
                }
                if (!p.Absent)
                {
                    Queue.Add(p);
                }
                if (!p.Absent && p.Satisfaction <= 0f && p.Satisfaction < 10f)
                {
                    Messages++;          // StartComplaint's message in their chat
                    Complaints++;        // daysWithComplaints: one of the week's two
                    ComplaintResign(p);
                }
                if (!(p.Satisfaction > 0f) && !p.Absent && !p.BeingReplaced && !p.Complaining)
                {
                    Staff.RemoveAt(i);
                }
            }
            Shown += Notices.Count;
            Notices.Clear();
        }

        // GameManager.NewDay, then the hour: the hour event, PayDailyWages, the day event (the mod's day passes),
        // EmployeeHelper.RunDaily (absences and the day's count cleared), then the employees' hour.
        public void Night(Action dayEvent)
        {
            Day++;
            Hour = 0;
            foreach (LeavePerson p in Staff)
            {
                if (!p.BeingReplaced)
                {
                    PayWage(p);
                }
            }
            dayEvent?.Invoke();
            foreach (LeavePerson p in Staff)
            {
                if (!p.BeingReplaced)
                {
                    p.Absent = false;
                    p.Worked = 0;
                }
            }
            GameHour();
        }

        // The mod's leaving as it was (BusinessGame.Leave, TalentGame.Resign): ComplaintResign's steps, left on the list.
        public void OldLeave(LeavePerson p)
        {
            p.Complaining = false;
            p.Satisfaction = 0f;
            if (!ReplaceOnResign(p))
            {
                Resign(p);
            }
        }

        // Core/StaffLeave.Resign, by its rules: refused for one not the player's to let go; a count the night paid
        // cleared; ComplaintResign's steps; off the list as the game's hour takes one off.
        public bool NewLeave(LeavePerson p, bool dayKnown = true)
        {
            if (StaffLeaveRules.Why(p != null, false, Staff.Contains(p), p != null && p.BeingReplaced, false) != StaffLeaveRules.Refusal.None)
            {
                return false;
            }
            int pays = StaffLeaveRules.HoursPaid(p.Worked, dayKnown ? p.LastWorked : -1, dayKnown, Day, Hour);
            if (p.Worked > 0 && pays == 0)
            {
                p.Worked = 0;
            }
            p.Complaining = false;
            p.Satisfaction = 0f;
            if (!ReplaceOnResign(p))
            {
                Resign(p);
                Staff.Remove(p);
            }
            return true;
        }
    }

    private static void StaffLeaveGame()
    {
        // Business pressure's dismissal, at the player's word in the afternoon, after six hours' work.
        var old = new LeaveWorld();
        LeavePerson ann = old.Hire("Ann Reed", 6, 100);
        old.OldLeave(ann);
        old.Hour = 15;
        old.GameHour();
        Check(old.Paid == 12 && old.Shown == 2 && old.Messages == 1 && old.Complaints == 1 && !old.Staff.Contains(ann),
            "staff leaving, as it was: the dismissal paid her six hours, and at her next hour the game's own check for no satisfaction resigned"
            + " her again: the six hours paid twice, two notices, a complaint message in her chat and one of the week's two complaints");
        var now = new LeaveWorld();
        ann = now.Hire("Ann Reed", 6, 100);
        Check(now.NewLeave(ann) && !now.Staff.Contains(ann) && now.Paid == 6 && now.Notices.Count == 1,
            "staff leaving: the dismissal pays her six hours once, puts one notice on the hour's list and takes her off the list at once");
        now.Hour = 15;
        now.GameHour();
        now.Night(null);
        Check(now.Paid == 6 && now.Shown == 1 && now.Messages == 0 && now.Complaints == 0,
            "staff leaving: her next hour and the night find nothing of her: one wage, one notice, no message, no complaint counted");

        // The rare case: a satisfaction update of the game's still queued for her lands after the dismissal.
        old = new LeaveWorld();
        ann = old.Hire("Ann Reed", 6, 100);
        ann.Lift = 0.6f;
        old.Queue.Add(ann);
        old.OldLeave(ann);
        old.WorkQueue();
        old.Hour = 15;
        old.GameHour();
        bool stayed = old.Staff.Contains(ann);
        old.Night(null);
        Check(stayed && old.Staff.Contains(ann) && old.Shown == 1 && old.Paid == 12,
            "staff leaving, as it was: with an update of the game's still queued, she was lifted off nothing and never left: still on the list,"
            + " unassigned, her day paid again by the night, though her resignation's notice was shown");
        now = new LeaveWorld();
        ann = now.Hire("Ann Reed", 6, 100);
        ann.Lift = 0.6f;
        now.Queue.Add(ann);
        now.NewLeave(ann);
        now.WorkQueue();
        now.Hour = 15;
        now.GameHour();
        now.Night(null);
        Check(!now.Staff.Contains(ann) && now.Paid == 6 && now.Shown == 1,
            "staff leaving: off the list at once, the queued update lands on nobody the game counts: she has left, paid once");

        // The company's departure (and the tech sector's), made in the game's day event after a day of eight hours' work.
        old = new LeaveWorld();
        LeavePerson bob = old.Hire("Bob Lee", 8, 100);
        old.Night(() => old.OldLeave(bob));
        Check(old.Paid == 16 && old.Shown == 2 && old.Messages == 1 && !old.Staff.Contains(bob),
            "staff leaving, as it was: the departure in the day event paid the day the night had just paid, and the game's hour resigned him"
            + " again: sixteen hours for eight, two notices");
        now = new LeaveWorld();
        bob = now.Hire("Bob Lee", 8, 100);
        now.Night(() => now.NewLeave(bob));
        Check(now.Paid == 8 && now.Shown == 1 && now.Messages == 0 && !now.Staff.Contains(bob) && bob.Worked == 0,
            "staff leaving: in the day event the count the night paid is cleared first: eight hours for eight, one notice, off the list");
        now = new LeaveWorld();
        bob = now.Hire("Bob Lee", 8, 100);
        now.Night(() => now.NewLeave(bob, false));
        Check(now.Paid == 8 && now.Shown == 1,
            "staff leaving: without the game's lastWorkedDay the first hour of the day tells the night's count the same way");

        // The headhunter's replacement takes their place, as the game's own resignation has it.
        old = new LeaveWorld();
        LeavePerson cy = old.Hire("Cy Moss", 8, 100);
        cy.Headhunter = true;
        old.Night(() => old.OldLeave(cy));
        Check(old.Paid == 16 && old.Replacing == 1,
            "staff leaving, as it was: in the day event the headhunter's replacement paid the night's day again too");
        now = new LeaveWorld();
        cy = now.Hire("Cy Moss", 8, 100);
        cy.Headhunter = true;
        now.Night(() => now.NewLeave(cy));
        now.GameHour();
        Check(now.Paid == 8 && now.Replacing == 1 && cy.BeingReplaced && now.Staff.Contains(cy) && now.Shown == 0 && now.Messages == 0,
            "staff leaving: with the headhunter's plan they are replaced as the game does it, the night's day paid once, nothing resigned again");
        now = new LeaveWorld();
        cy = now.Hire("Cy Moss", 6, 100);
        cy.Headhunter = true;
        now.NewLeave(cy);
        Check(now.Paid == 6 && cy.BeingReplaced,
            "staff leaving: a dismissal with the headhunter's plan pays today's hours once, before the replacement");

        // Off sick: the game's check for no satisfaction and its taking off both pass an absent employee by.
        old = new LeaveWorld();
        LeavePerson di = old.Hire("Di Shaw", 0, 99);
        di.Absent = true;
        old.OldLeave(di);
        old.GameHour();
        bool sickStayed = old.Staff.Contains(di);
        old.Night(null);
        Check(sickStayed && !old.Staff.Contains(di) && old.Shown == 2,
            "staff leaving, as it was: one let go while off sick stayed on the list until the next day, then resigned a second time");
        now = new LeaveWorld();
        di = now.Hire("Di Shaw", 0, 99);
        di.Absent = true;
        now.NewLeave(di);
        now.GameHour();
        now.Night(null);
        Check(!now.Staff.Contains(di) && now.Shown == 1,
            "staff leaving: one off sick leaves at once, with one notice");

        // Refused: nothing changes.
        now = new LeaveWorld();
        LeavePerson ed = now.Hire("Ed Park", 4, 100);
        ed.BeingReplaced = true;
        LeavePerson gone = new LeavePerson { Name = "Fay Hill", Worked = 3 };
        Check(!now.NewLeave(ed) && ed.Satisfaction == 70f && ed.Worked == 4 && !now.NewLeave(gone) && now.Paid == 0 && now.Notices.Count == 0,
            "staff leaving: one the headhunter is replacing, or one no longer on the list, is not touched: no wage, no notice, no satisfaction changed");
        Check(!now.NewLeave(null), "staff leaving: nobody to let go is refused, nothing thrown");
    }

    // ---------------------------------------------------------------- two offers

    // The company's file of one employee with the game's own offer to them, day by day: the company's rules for real
    // (StaffRules), the game's offer as EmployeeInstance has it (PoachByRival, Accept, its deadline in RunDaily), and the
    // employee's leaving counted.
    private sealed class TwoOffers
    {
        public readonly StaffFile File = new StaffFile { Id = "emp1", Name = "Gil Webb", Wage = 2000, Going = 2400, Skill = 70 };
        public string RivalId = string.Empty;   // poachedByRivalId
        public int Deadline, OfferDay = -1;     // poachDeadlineDays, lastPoachDay
        public bool Employed = true;
        public int Leavings, Raises;
        public bool GiveWayOn;                  // the rule of one offer at a time

        public bool GameOpen => RivalOfferRules.Open(RivalId, true, Deadline);

        // The company's read of the game (CompanyGame.ReadStaff, CompanyGame.Offers).
        public void Read()
        {
            File.GameOffer = Employed && GameOpen;
            File.Seen = Employed;
        }

        public void GameOffers(int day, int days)
        {
            RivalId = "special_rival_7";
            Deadline = days;
            OfferDay = day;
        }

        public void AcceptGame()
        {
            if (GameOpen && Employed)
            {
                RivalId = string.Empty;
                Deadline = 0;
                Raises++;
            }
        }

        // The company's day pass (CompanyDirector.StaffDay) and the game's night (RunDaily: an offer past its deadline
        // moves them to the rival: MoveEmployeeToRival, one leaving).
        public void Day(int day)
        {
            if (Employed && Deadline > 0 && OfferDay + Deadline < day)
            {
                Deadline = 0;
                Employed = false;
                Leavings++;
            }
            Read();
            if (!File.Seen)
            {
                return;   // the file is dropped: nothing of the company's is left for them
            }
            if (GiveWayOn && StaffRules.GivesWay(File))
            {
                StaffRules.GiveWay(File, day);
            }
            if (StaffRules.OfferLapsed(File, day))
            {
                StaffRules.OfferAnswer(File, false, day);
            }
            if (File.LeaveDay > 0 && day >= File.LeaveDay)
            {
                Employed = false;   // Core/StaffLeave: one leaving, off the list at once
                Leavings++;
                File.LeaveDay = 0;
            }
        }
    }

    private static void StaffLeaveOffers()
    {
        var f = new StaffFile { Id = "emp1", Name = "Gil Webb", Wage = 2000, Going = 2400, Skill = 70, PoachRest = 0 };
        int level = CompanyCatalogue.Standard;
        Check(StaffRules.Wanted(f, level, 50) && StaffRules.RivalMay(f, level, 50) && StaffRules.Free(f),
            "two offers: an underpaid, skilled employee with nothing open is one the company's rivals may want");
        f.GameOffer = true;
        Check(!StaffRules.Wanted(f, level, 50) && !StaffRules.RivalMay(f, level, 50) && !StaffRules.Free(f),
            "two offers: the company makes no offer, of its own rivals or a rival tycoon's, while the game's own offer to them is open");
        f.GameOffer = false;
        f.Held = true;
        Check(!StaffRules.Wanted(f, level, 50) && !StaffRules.RivalMay(f, level, 50),
            "two offers: nor while the tech sector has its own offer or a departure in hand for them");
        f.Held = false;

        var g = new StaffFile { Id = "emp2", Name = "Hal Cole", PoachDay = 40, PoachOffer = 2520, Rival = "Rival Co", PoachRest = 0 };
        Check(!StaffRules.GivesWay(g), "two offers: the company's offer stands while no offer of the game's is open");
        g.GameOffer = true;
        Check(StaffRules.GivesWay(g), "two offers: the company's waiting offer gives way once the game's opens");
        StaffRules.GiveWay(g, 41);
        Check(g.PoachDay == 0 && g.PoachOffer == 0 && g.LeaveDay == 0 && g.PoachRest == 41 + CompanyCatalogue.PoachRestDays && !StaffRules.OfferLapsed(g, 60)
              && !StaffRules.GivesWay(g),
            "two offers: given way, nothing of the company's is left to answer or to lapse into a notice, and no new offer while the rest runs");
        var h = new StaffFile { Id = "emp3", Name = "Ida Ross", LeaveDay = 45, PoachRest = 80, GameOffer = true };
        Check(StaffRules.GivesWay(h), "two offers: the notice worked after the company's offer gives way to the game's as well");
        StaffRules.GiveWay(h, 43);
        Check(h.LeaveDay == 0 && h.PoachRest == 80, "two offers: the notice is called off, and a longer rest already running is kept");

        // The report's case: the company's offer on day 10, the game's on day 11 (three days). The player matches the
        // game's in their messages and leaves the company's alone.
        var before = new TwoOffers();
        StaffRules.Offer(before.File, "Rival Co", 10);
        before.GameOffers(11, 3);
        before.AcceptGame();
        for (int day = 11; day <= 20; day++)
        {
            before.Day(day);
        }
        Check(before.Raises == 1 && before.Leavings == 1 && !before.Employed,
            "two offers, as it was: kept by matching the game's offer, then lost all the same when the company's offer lapsed into a notice");
        var after = new TwoOffers { GiveWayOn = true };
        StaffRules.Offer(after.File, "Rival Co", 10);
        after.GameOffers(11, 3);
        after.Read();
        bool gaveWay = StaffRules.GivesWay(after.File);
        StaffRules.GiveWay(after.File, 11);   // the company's hour after the game's offer (CompanyDirector.GiveWayAll)
        after.AcceptGame();
        for (int day = 11; day <= 20; day++)
        {
            after.Day(day);
        }
        Check(gaveWay && after.Raises == 1 && after.Leavings == 0 && after.Employed && after.File.PoachDay == 0 && after.File.LeaveDay == 0,
            "two offers: the company's offer gives way within the hour; matched in their messages, they stay, and nothing of the company's takes them");

        // Neither answered: they leave once, by the game's offer, whichever way the days fall.
        var lapse = new TwoOffers { GiveWayOn = true };
        StaffRules.Offer(lapse.File, "Rival Co", 10);
        lapse.GameOffers(11, 3);
        for (int day = 11; day <= 25; day++)
        {
            lapse.Day(day);
        }
        Check(lapse.Leavings == 1 && !lapse.Employed, "two offers: neither answered, they leave once, by the game's offer");
        var notice = new TwoOffers { GiveWayOn = true };
        StaffRules.Offer(notice.File, "Rival Co", 10);
        StaffRules.OfferAnswer(notice.File, false, 11);   // let go: they work two days' notice
        notice.GameOffers(12, 3);
        notice.Day(12);                                   // the company's next look sees the game's offer: the notice gives way
        notice.AcceptGame();
        for (int day = 13; day <= 20; day++)
        {
            notice.Day(day);
        }
        Check(notice.Leavings == 0 && notice.Employed && notice.Raises == 1,
            "two offers: the company's notice gives way to the game's offer too; matched there, they stay");
        var both = new TwoOffers();
        StaffRules.Offer(both.File, "Rival Co", 10);
        both.GameOffers(12, 6);   // the company's notice ends before the game's deadline
        for (int day = 11; day <= 25; day++)
        {
            both.Day(day);
        }
        Check(both.Leavings == 1, "two offers, as it was: even unanswered they left once (the second way finds them gone): what hurt was the answer that did nothing");

        // The determinism of who a rival approaches is the company's own seed's, and only the free are among them.
        var fit = new List<StaffFile>();
        for (int i = 0; i < 6; i++)
        {
            fit.Add(new StaffFile { Id = "e" + i, Wage = 2000, Going = 2400, Skill = 70, GameOffer = i % 2 == 0 });
        }
        int free = 0;
        foreach (StaffFile s in fit)
        {
            free += StaffRules.Wanted(s, level, 50) ? 1 : 0;
        }
        Check(free == 3 && StaffRules.Who(77, 12, free) == StaffRules.Who(77, 12, free),
            "two offers: of six who fit, the three with the game's offer open are left out, and the pick among the rest is the seed's");
    }

    // The tech sector's own offers and notices, by its rules for real (TalentRules).
    private static void StaffLeaveTech()
    {
        var p = new Profile { Id = "emp9" };
        TalentRules.Offer(p, "Rival Labs", 30);
        Check(TalentRules.Holds(p) && !TalentRules.GivesWay(p, false) && TalentRules.GivesWay(p, true),
            "two offers, the tech sector: its waiting offer is held, stands alone, and gives way once the game's opens");
        TalentRules.GiveWay(p, 31);
        Check(p.PoachBp == 0 && !TalentRules.OfferOpen(p, 31) && !TalentRules.Holds(p) && p.PoachDay >= 31 + TalentCatalogue.PoachRestDays
              && p.NoticeEnd == 0,
            "two offers, the tech sector: given way, nothing of the sector's is open, and no new approach while the rest runs");
        var q = new Profile { Id = "emp10" };
        TalentRules.Offer(q, "Rival Labs", 30);
        Check(TalentRules.GivesWay(q, true) && !TalentRules.OfferOpen(q, 34) && q.PoachBp > 0,
            "two offers, the tech sector: an offer unanswered on its last day is still in hand, and gives way");
        var r = new Profile { Id = "emp11" };
        TalentRules.Offer(r, "Rival Labs", 30);
        TalentRules.Answer(r, false, 31, 365);
        Check(r.NoticeEnd > 0 && r.Leaving == (int)LeaveKind.Poached && TalentRules.Holds(r) && TalentRules.GivesWay(r, true),
            "two offers, the tech sector: the notice worked after its offer is held, and gives way to the game's");
        TalentRules.GiveWay(r, 32);
        Check(r.NoticeEnd == 0 && r.Leaving == (int)LeaveKind.None, "two offers, the tech sector: that notice is called off");
        var s = new Profile { Id = "emp12" };
        TalentRules.Notice(s, 31, 365, LeaveKind.LaidOff);
        int end = s.NoticeEnd;
        Check(TalentRules.Holds(s) && !TalentRules.GivesWay(s, true),
            "two offers, the tech sector: a lay-off the player gave is held (no rival's offer to them) and stands, game's offer or not");
        TalentRules.GiveWay(s, 32);
        Check(s.NoticeEnd == end && s.Leaving == (int)LeaveKind.LaidOff, "two offers, the tech sector: a lay-off is never called off");
        var t = new Profile { Id = "emp13" };
        TalentRules.Notice(t, 31, 365, LeaveKind.BurntOut);
        Check(!TalentRules.GivesWay(t, true), "two offers, the tech sector: nor is a burn-out's notice");
        Check(!TalentRules.Holds(new Profile { Id = "emp14" }) && !TalentRules.GivesWay(new Profile { Id = "emp15" }, true),
            "two offers, the tech sector: nothing in hand, nothing held and nothing to give way");
    }

    // ---------------------------------------------------------------- the sources

    // Where a piece of code stands in a text, counting only where it is code: not on a line commented out before it.
    // -1 if it stands nowhere as code.
    private static int CodeAt(string code, string piece)
    {
        for (int at = code.IndexOf(piece, StringComparison.Ordinal); at >= 0; at = code.IndexOf(piece, at + piece.Length, StringComparison.Ordinal))
        {
            int line = code.LastIndexOf('\n', at) + 1;
            if (code.IndexOf("//", line, at - line, StringComparison.Ordinal) < 0)
            {
                return at;
            }
        }
        return -1;
    }

    private static void StaffLeaveSources()
    {
        string leave = ModSource(@"Core\StaffLeave.cs");
        string resign = MemberText(leave, "public static bool Resign(EmployeeInstance e, string who)");
        int why = CodeAt(resign, "StaffLeaveRules.Why(");
        int clear = CodeAt(resign, "e.workedHoursToday = 0;");
        int replace = CodeAt(resign, "e.ReplaceAutomaticallyOnResign()");
        int game = CodeAt(resign, "e.Resign();");
        int off = CodeAt(resign, "TakeOff(e, staff);");
        int look = CodeAt(resign, "RivalOfferGuard.Look(");
        Check(why > 0 && clear > why && replace > clear && game > replace && off > game && look > off
              && CodeAt(resign, "StaffLeaveRules.HoursPaid(counted, lastWorked, known, save.Day, save.Hour)") > 0 && CodeAt(resign, "e.satisfaction = 0f;") > 0,
            "staff leaving: the steps in the game's order, as code: refusals first, the night's count cleared before the headhunter (who pays"
            + " it too), then Resign, then off the list, then the rival offer's buttons");
        string takeOff = MemberText(leave, "private static void TakeOff(EmployeeInstance e, List<EmployeeInstance> staff)");
        Check(CodeAt(takeOff, "staff.RemoveAt(i)") > 0 && CodeAt(takeOff, "EmployeeHelper.EmployeeInstancesDictionary.Remove(e.id)") > 0
              && CodeAt(leave, "RemoveEmployee()") < 0 && CodeAt(leave, "LastWorkedField = GameContract.LastWorkedDay.Field") > 0,
            "staff leaving: off the save's list and the game's index as the game's hour takes one off, never by RemoveEmployee (the firing Going"
            + " Public sends to a merged company's owner); which day the count is of read through the contract");
        Check(leave.Contains("GoingPublic.Foreign(reg)") && !leave.Contains("SharedCity") && !leave.Contains("GoingPublicLink"),
            "staff leaving, Going Public: another player's employee is left alone, and nothing is sent");
        string contract = ModSource(@"Core\GameContract.cs");
        Check(contract.Contains("LastWorkedDay = Optional(Field(StaffLeaving, Game, \"Entities.EmployeeInstance\", \"lastWorkedDay\", \"Int32\"))")
              && contract.Contains("ContractBook.Part(\"staff_leaving\", null,"),
            "staff leaving: the game's private lastWorkedDay is an optional line of the contract, under a part of its own");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("StaffLeave.Logger = context.Logger"), "staff leaving: started with the mod's logger");

        string business = ModSource(@"Business\BusinessGame.cs");
        string tech = ModSource(@"Tech\People\TalentGame.cs");
        string links = ModSource(@"Wiring\CompanyLinks.cs");
        Check(CodeAt(MemberText(business, "public static bool Leave(EmployeeInstance e)"), "StaffLeave.Resign(e, \"business pressure\")") > 0
              && CodeAt(MemberText(tech, "public static bool Resign(EmployeeInstance e)"), "StaffLeave.Resign(e, \"the tech sector\")") > 0
              && CodeAt(MemberText(links, "private static bool Leave(EmployeeInstance employee)"), "StaffLeave.Resign(employee,") > 0,
            "staff leaving: business pressure's dismissal, the company's departures and the tech sector's resignations all go the one way");

        // No other place of the mod resigns an employee or sets one's satisfaction at nothing.
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var resigners = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = System.IO.File.ReadAllText(file);
            if (Regex.IsMatch(code, @"\.Resign\(\)\s*;") || Regex.IsMatch(code, @"\.satisfaction\s*=\s*0f?\s*;"))
            {
                resigners.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(resigners.Count == 1 && resigners[0] == "StaffLeave.cs",
            "staff leaving: only Core/StaffLeave resigns an employee or puts their satisfaction at nothing (found: " + string.Join(", ", resigners) + ")");

        string issues = ModSource(@"Business\BusinessIssues.cs");
        Check(CodeAt(MemberText(issues, "private static EmployeeInstance Poachable(BuildingRegistration reg)"), "!BusinessDirector.HeldElsewhere(e.id)") > 0
              && CodeAt(issues, "BusinessGame.Leave(e) ? \"reallife_biz_stafftheft_dismissed\" : \"reallife_biz_staff_gone\"") > 0,
            "two offers: business pressure's poaching passes over anybody the company or the tech sector has an offer or a departure in hand for;"
            + " its dismissal says so when somebody could not be let go");
        string director = ModSource(@"Business\BusinessDirector.cs");
        Check(CodeAt(director, "public static Func<string, bool> Held;") > 0 && CodeAt(MemberText(director, "public static bool HeldElsewhere(string id)"), "catch (Exception)") > 0,
            "two offers: business pressure's bridge for the other features' offers, asked safely");

        string read = ModSource(@"Economy\Company\CompanyGame.cs");
        Check(CodeAt(read, "f.GameOffer = RivalOfferGuard.OfferOpen(e);") > 0 && CodeAt(read, "f.Held = CompanyBridges.HeldAt(e.id);") > 0
              && CodeAt(MemberText(read, "public static void Offers(StaffFile f)"), "RivalOfferGuard.OfferOpen(e)") > 0
              && CodeAt(read, "public static Func<EmployeeInstance, bool> Leave;") > 0 && CodeAt(read, "Held = null;") > 0,
            "two offers: the company reads the game's open offer and the tech sector's for each employee, with every read of its staff and before a decision");
        string staff = ModSource(@"Economy\Company\CompanyDirector.Staff.cs");
        string day = MemberText(staff, "partial void StaffDay(int day)");
        string hour = MemberText(staff, "partial void StaffHour(int day, int hour)");
        string answer = MemberText(staff, "public void AnswerOffer(string id, bool match)");
        Check(CodeAt(day, "StaffRules.GivesWay(f)") > 0 && CodeAt(day, "StaffRules.GivesWay(f)") < CodeAt(day, "StaffRules.OfferLapsed(f, day)")
              && CodeAt(hour, "GiveWayAll(day);") > 0 && CodeAt(hour, "GiveWayAll(day);") < CodeAt(hour, "CompanyCatalogue.StrikeHour")
              && CodeAt(answer, "CompanyGame.Offers(f);") > 0 && CodeAt(answer, "StaffRules.GivesWay(f)") > CodeAt(answer, "CompanyGame.Offers(f);")
              && CodeAt(answer, "StaffRules.GivesWay(f)") < CodeAt(answer, "StaffRules.OfferAnswer(")
              && CodeAt(staff, "!CompanyBridges.Leave(e)") > 0 && CodeAt(MemberText(staff, "private void GaveWay(StaffFile f, int day)"), "\"reallife_staff_offer_gave_way\"") > 0,
            "two offers: the company gives way on its day before an offer lapses, every hour before anything else, and at a press before it is answered;"
            + " a departure that could not be made leaves the file staying");
        string rivals = MemberText(ModSource(@"Economy\Company\CompanyDirector.Rivals.cs"), "public bool RivalOffer(string rival, int premium, out string name, out string business)");
        Check(CodeAt(rivals, "CompanyGame.Offers(f);") > 0 && CodeAt(rivals, "CompanyGame.Offers(f);") < CodeAt(rivals, "StaffRules.RivalMay("),
            "two offers: a rival tycoon's offer reads the other offers first");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        string peopleDay = MemberText(people, "private void PeopleDay(int day)");
        string roles = ModSource(@"Tech\TechDirector.Roles.cs");
        Check(CodeAt(peopleDay, "TalentRules.GivesWay(p, gameOffer)") > 0
              && CodeAt(peopleDay, "TalentRules.GivesWay(p, gameOffer)") < CodeAt(peopleDay, "TalentRules.BurnsOut(")
              && CodeAt(peopleDay, "&& !Held(p) && !gameOffer") > 0
              && CodeAt(MemberText(people, "private void PeopleHour()"), "if (GiveWayAll(day))") > 0
              && CodeAt(MemberText(people, "private bool GiveWayAll(int day)"), "TalentRules.GivesWay(person.File, TalentGame.GameOffer(person.Game))") > 0
              && CodeAt(MemberText(roles, "internal bool RolesOpened()"), "if (GiveWayAll(Save.Day))") > 0
              && CodeAt(MemberText(roles, "internal void AnswerRival(string id, bool match)"), "TalentRules.GivesWay(person.File, TalentGame.GameOffer(person.Game))") > 0,
            "two offers: the tech sector gives way on its day, every hour, as Open Roles opens and at a press, and approaches nobody with the game's offer open");
        Check(CodeAt(MemberText(ModSource(@"Economy\Company\CompanyDirector.cs"), "public bool AppOpened()"), "GiveWayAll(Save.Day);") > 0,
            "two offers: the company gives way as its app opens, before an offer's buttons are drawn");
        string techLinks = ModSource(@"Wiring\TechLinks.cs");
        Check(CodeAt(links, "CompanyBridges.Held = TechLinks.Holds;") > 0 && CodeAt(links, "BusinessDirector.Held = Offered;") > 0
              && CodeAt(links, "BusinessDirector.Held = null;") > 0 && CodeAt(MemberText(links, "private static bool Offered(string id)"), "return Holds(id) || TechLinks.Holds(id);") > 0
              && CodeAt(techLinks, "TechBridges.Held = CompanyLinks.Holds;") > 0 && CodeAt(MemberText(techLinks, "internal static bool Holds(string id)"), "director.Holds(id)") > 0,
            "two offers: the company asks the tech sector, the tech sector the company, business pressure both: one rival's offer at a time");
        string guard = ModSource(@"Core\RivalOfferGuard.cs");
        Check(CodeAt(MemberText(guard, "public static bool OfferOpen(EmployeeInstance e)"), "RivalOfferRules.Open(e.poachedByRivalId, known,") > 0,
            "two offers: the game's open offer is told by the poach fix's contract part, through the one rule");
    }

    // ---------------------------------------------------------------- the cost

    private static void StaffLeavePace()
    {
        var f = new StaffFile { Id = "emp1", PoachDay = 40, GameOffer = true };
        var p = new Profile { Id = "emp2", PoachBp = 1500 };
        int n = 0;
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000000; i++)
        {
            n += StaffLeaveRules.HoursPaid(i & 15, 99 + (i & 1), true, 100, i & 7) + (StaffRules.GivesWay(f) ? 1 : 0) + (TalentRules.GivesWay(p, (i & 1) == 0) ? 1 : 0)
                 + (RivalOfferRules.Open("r", true, i & 3) ? 1 : 0);
        }
        watch.Stop();
        Check(n > 0 && watch.Elapsed.TotalMilliseconds < 250.0,
            "staff leaving and two offers: a million of each judgement take " + watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms (under 250)");
    }

    // ---------------------------------------------------------------- the texts

    private static void StaffLeaveWords()
    {
        Check(FixLineEverywhere("an employee the mod lets go now leaves once, with one wage and one notice", "funcionário que o mod dispensa agora sai uma só vez",
                                "被本模组让其离开的员工现在只离开一次", "den die Mod gehen lässt, geht jetzt nur einmal", "un employé que le mod laisse partir ne part plus qu'une fois",
                                "un empleado al que el mod despide ahora se va una sola vez", "сотрудник, которого отпускает мод, теперь уходит один раз",
                                "een medewerker die de mod laat gaan, vertrekt nu één keer", "modun bıraktığı bir çalışan artık yalnızca bir kez ayrılıyor"),
            "staff leaving: What's new has the fix's line, with its fix mark, in some row of every language");
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("staff leaving", langs, codes))
        {
            return;
        }
        foreach (string key in new[] { "reallife_staff_offer_gave_way", "reallife_contract_part_staff_leaving" })
        {
            bool all = LanguagesLacking(langs, codes, key).Length == 0;
            for (int l = 0; all && l < langs.Length; l++)
            {
                string text = langs[l][key];
                all &= text.Length > 10 && (l == 0 || text != langs[0][key]) && !text.Contains("!") && !text.Contains("—") && !text.Contains("–")
                       && !text.Contains(" - ");
            }
            Check(all && codes.Length == ModLanguages.All.Length,
                "staff leaving: " + key + " is in every language, each its own, with the English names in curly brackets, no dash and no exclamation mark");
        }
        string gaveWay = langs[0]["reallife_staff_offer_gave_way"];
        Check(gaveWay.Length <= 240 && gaveWay.Contains("{name}") && gaveWay.Contains("{rival}"),
            "two offers: the notice that an offer gave way names the employee and the rival, within 240 characters (" + gaveWay.Length + ")");
    }
}
