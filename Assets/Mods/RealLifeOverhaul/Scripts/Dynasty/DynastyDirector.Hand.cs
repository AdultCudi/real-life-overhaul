using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>The handover: the last days of a life, retiring, the steps of an estate being settled, the debt for tax and the retired founder.</summary>
    public sealed partial class DynastyDirector : IHandWorld
    {
        /// <summary>The people who may carry on or be left something today, read from the family and the people the founder knows. A press or a pass, never a frame.</summary>
        private List<KinPerson> ReadPeople()
        {
            _people.Clear();
            try
            {
                DynastyBridges.People?.Invoke(DynastyGame.AgeYears, DynastyGame.Day, _people);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return _people;
        }

        private static bool Ask(Func<bool> bridge)
        {
            try
            {
                return bridge != null && bridge();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // ---- the last days

        // A comparison a second: the played person's age against their last days. In them the estate passes, before the game's own end in old age comes; while that is held
        // back (a session, a cell) the age is kept just short of the end. With Permadeath on, or nobody to carry on, the game's own end is left to come as it always did.
        private void LastDays(int day)
        {
            int year = DynastyGame.DaysPerYear;
            int age = DynastyGame.AgeDays;
            if (age < DynastyRules.LastDays(year) || !DynastyGame.Ageing)
            {
                return;
            }
            DeathHold hold = HoldNow(out KinPerson heir, out bool byLaw);
            if (hold == DeathHold.None)
            {
                Begin(HandMode.Death, heir, byLaw);
                return;
            }
            if (DynastyRules.Holds(hold))
            {
                if (age > DynastyRules.HeldAge(year))
                {
                    DynastyGame.SetAgeDays(DynastyRules.HeldAge(year));
                }
                if (_state.HeldTold != day)
                {
                    _state.HeldTold = day;
                    DynastyGame.Tell(NotificationType.Warning, hold == DeathHold.Session ? "reallife_dynasty_held_session" : "reallife_dynasty_held_custody", null);
                }
            }
            else if (_state.HeldTold != day)
            {
                _state.HeldTold = day;
                DynastyGame.Tell(NotificationType.Warning, hold == DeathHold.Final ? "reallife_dynasty_end_final" : "reallife_dynasty_end_no_heir", null, OpenWill);
            }
        }

        /// <summary>What holds the last day back now, and who would carry on.</summary>
        private DeathHold HoldNow(out KinPerson heir, out bool byLaw)
        {
            heir = DynastyRules.Choose(_state.Will, ReadPeople(), out byLaw);
            return DynastyRules.Hold(Running, Ask(DynastyBridges.Final), GoingPublic.InSession, Ask(DynastyBridges.Custody), heir != null);
        }

        // ---- retiring

        /// <summary>Whether the founder may retire now, and who would carry on.</summary>
        public RetireWhy RetireWhyNow(out KinPerson heir, out bool byLaw)
        {
            heir = null;
            byLaw = true;
            if (!Running)
            {
                return RetireWhy.Off;
            }
            heir = DynastyRules.Choose(_state.Will, ReadPeople(), out byLaw);
            return DynastyRules.MayRetire(Running, _state.Hand != null, _state.Will, DynastyGame.AgeYears, heir != null, GoingPublic.InSession, Ask(DynastyBridges.LawOpen),
                _state.Elder != null);
        }

        /// <summary>
        /// The founder retires: everything is asked before any money is booked (the age, an heir, no session, nothing open with the law), then the estate is frozen and passes
        /// in its steps. Returns why not, or Ok once the handover has begun.
        /// </summary>
        public RetireWhy Retire()
        {
            RetireWhy why = RetireWhyNow(out KinPerson heir, out bool byLaw);
            if (why == RetireWhy.Ok)
            {
                Begin(HandMode.Retire, heir, byLaw);
            }
            return why;
        }

        // ---- an estate passes

        // The freeze: the estate is valued once and its statement fixed; the steps that follow only carry it out.
        private void Begin(HandMode mode, KinPerson heir, bool byLaw)
        {
            Statement s = DynastyRules.Settle(DynastyGame.Estate(mode, _state), _state.Will, heir, byLaw, _people);
            if (!DynastyHand.Begin(_state, s))
            {
                return;
            }
            _draft = null;
            _revision++;
            Logger?.Info("Dynasty: " + (mode == HandMode.Retire ? "the founder retires" : "the founder's last days") + ": the estate of " + s.Net + " passes to " + s.To + " ("
                         + (byLaw ? "by the law's order" : "by the will") + "); tax " + s.Tax + " (" + s.TaxCash + " from the cash, " + s.Loan + " owed), bequests " + s.Paid + ", kept "
                         + s.Keep + ", the heir's rest " + s.Residue + ".");
            if (Verbose)
            {
                Logger?.Info("Dynasty check (the estate frozen): " + s.Net + " = kept " + s.Keep + " + tax " + s.Tax + " + bequests " + s.Paid + " + the heir's " + s.Residue + ", cash out "
                             + DynastyRules.CashOut(s) + " of " + (s.Cash - s.Reserved) + " over the investors' floor: "
                             + (DynastyRules.Balanced(s) && DynastyRules.CashOut(s) <= Math.Max(0L, s.Cash - s.Reserved) ? "PASS" : "FAIL"));
            }
            DynastyGame.Tell(NotificationType.Info, mode == HandMode.Retire ? "reallife_dynasty_hand_retire" : "reallife_dynasty_hand_death",
                DynastyGame.Data("from", s.From, "to", s.To, "tax", DynastyGame.Money(s.Tax)), OpenEstate);
        }

        private void HandStep()
        {
            Handover h = _state.Hand;
            Statement s = h.S;
            bool done = DynastyHand.Step(_state, this, DynastyGame.Day, DynastyGame.DaysPerYear, DynastyGame.MonthDays);
            _revision++;
            if (!done)
            {
                return;
            }
            if (Verbose)
            {
                Logger?.Info("Dynasty check (the estate settled): " + s.Net + " = kept " + s.Keep + " + tax " + s.Tax + " + bequests " + s.Paid + " + the heir's " + s.Residue
                             + "; the head is now " + DynastyGame.Name + ", generation " + _state.Generation + ": " + (DynastyRules.Balanced(s) ? "PASS" : "FAIL"));
            }
            DynastyGame.Tell(NotificationType.Success, "reallife_dynasty_hand_done", DynastyGame.Data("to", s.To, "from", s.From, "age", s.HeirAge.ToString(),
                "share", (s.Upbringing / 10).ToString()), OpenEstate);
        }

        int IHandWorld.FounderAgeDays => Math.Max(0, DynastyGame.AgeDays);

        void IHandWorld.PayTax(long amount)
        {
            DynastyGame.Pay(amount, DynastyGame.TaxKey, true);
        }

        void IHandWorld.PayKeep(long amount)
        {
            DynastyGame.Pay(amount, DynastyGame.KeepKey, true);
        }

        // A bequest lands first and is booked for what landed: a household or a pot that takes less than its line (or nothing, after a fault) costs the estate no more than that.
        long IHandWorld.Land(BequestLine line)
        {
            long landed = 0;
            try
            {
                switch (line.Landing)
                {
                    case Landing.Pot:
                        landed = DynastyBridges.ToCharity != null ? DynastyBridges.ToCharity(line.Id, line.Amount) : 0L;
                        break;
                    case Landing.Household:
                        landed = DynastyBridges.ToHousehold != null ? DynastyBridges.ToHousehold(line.Id, line.Amount) : 0L;
                        break;
                    case Landing.Family:
                        landed = line.Amount;
                        break;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            landed = Math.Max(0L, Math.Min(line.Amount, landed));
            DynastyGame.Pay(landed, line.Landing == Landing.Pot ? DynastyGame.CharityKey : DynastyGame.BequestKey, true);
            return landed;
        }

        void IHandWorld.Succeed(Statement s)
        {
            try
            {
                DynastyBridges.Succeed?.Invoke(s);   // the family, the people, the education, the standing and the record first: they still read the founder by name
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            DynastyGame.Become(s.To, s.HeirAge, s.Upbringing);
        }

        // ---- the debt for tax the cash did not cover

        // A part a day, for each day gone by (a time skip's days one by one); a part the cash over the investors' floor does not cover is a late day.
        private void LoanDays(int day, int year)
        {
            EstateLoan l = _state.Loan;
            if (!l.Open)
            {
                return;
            }
            for (int d = Math.Max(l.LastDay + 1, day - 400); d <= day && l.Open; d++)
            {
                long cents = DynastyRules.LoanDay(l, DynastyGame.Spare * 100, year, out bool forced);
                l.LastDay = d;
                if (cents > 0)
                {
                    DynastyGame.PayCents(cents, DynastyGame.TaxPartKey, true);
                }
                if (forced)
                {
                    DynastyGame.Tell(NotificationType.Warning, "reallife_dynasty_loan_forced", DynastyGame.Data("amount", DynastyGame.Money(cents / 100)), OpenEstate);
                }
                else if (cents == 0 && l.Late == 1)
                {
                    DynastyGame.Tell(NotificationType.Warning, "reallife_dynasty_loan_late", DynastyGame.Data("days", DynastyCatalogue.LoanLateMost.ToString()), OpenEstate);
                }
            }
            if (!l.Open)
            {
                DynastyGame.Tell(NotificationType.Success, "reallife_dynasty_loan_paid", null, OpenEstate);
            }
        }

        // ---- the retired founder

        // Once a month they draw their living from their fund; at the game's own last age they die, and what is left of the fund is an estate of its own that goes to the head.
        private void ElderDay(int day, int year)
        {
            Elder e = _state.Elder;
            if (e == null)
            {
                return;
            }
            DynastyRules.ElderMonth(e, (Math.Max(1, day) - 1) / Math.Max(1, DynastyGame.MonthDays));
            if (!DynastyRules.ElderDies(e, day, year))
            {
                return;
            }
            Statement s = DynastyRules.SettleElder(e, DynastyGame.Name, day, DynastyGame.Level, year, Math.Max(1, _state.Generation - 1));
            _state.Elder = null;
            _state.Keep(s);
            DynastyGame.Receive(s.Residue, DynastyGame.FundKey);
            if (Verbose)
            {
                Logger?.Info("Dynasty check (the retired founder's fund): " + s.Net + " left = tax " + s.Tax + " + " + s.Residue + " to the head of the family, counted with "
                             + s.Prior + " handed over inside the window: " + (DynastyRules.Balanced(s) ? "PASS" : "FAIL"));
            }
            DynastyGame.Tell(NotificationType.Info, "reallife_dynasty_elder_died", DynastyGame.Data("name", e.Name, "amount", DynastyGame.Money(s.Residue), "tax", DynastyGame.Money(s.Tax)),
                OpenEstate);
        }
    }
}
