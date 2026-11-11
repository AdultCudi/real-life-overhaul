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

        /// <summary>What the law has open against the founder and whether they are held, from the bridge; a state that cannot be read counts as open.</summary>
        private static LawFacts LawNow()
        {
            try
            {
                return DynastyBridges.Law != null ? DynastyBridges.Law() : default(LawFacts);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return default(LawFacts);
            }
        }

        // ---- the last days

        // The last days are looked at a second at a time and, which is what matters, at every hour the game strikes (GlobalEvents.onNewHour, which a time skip passes through
        // hour by hour): the played person's age against their last days. In them the estate passes, before the game's own end in old age comes at a midnight; while that is held
        // back (a session, a cell) the age is kept short of the end. With Permadeath on, or nobody to carry on, the game's own end is left to come as it always did. A death
        // handover is settled in this very call, so nothing can come between its first step and its last.
        private void LastDays(int day)
        {
            int year = DynastyGame.DaysPerYear;
            int age = DynastyGame.AgeDays;
            if (!DynastyRules.InLastDays(age, year) || !DynastyGame.Ageing)
            {
                return;
            }
            DeathHold hold = HoldNow(out KinPerson heir, out bool byLaw);
            switch (DynastyRules.LastDaysAct(age, year, DynastyGame.Ageing, hold))
            {
                case LastAct.Begin:
                    Begin(HandMode.Death, heir, byLaw);
                    break;
                case LastAct.Hold:
                    _state.Held = true;
                    if (DynastyRules.Pinned(age, year) != age)
                    {
                        DynastyGame.SetAgeDays(DynastyRules.Pinned(age, year));
                    }
                    if (_state.HeldTold != day)
                    {
                        _state.HeldTold = day;
                        DynastyGame.Tell(NotificationType.Warning, hold == DeathHold.Session ? "reallife_dynasty_held_session" : "reallife_dynasty_held_custody", null);
                    }
                    break;
                case LastAct.Tell:
                    if (_state.HeldTold != day)
                    {
                        _state.HeldTold = day;
                        DynastyGame.Tell(NotificationType.Warning, hold == DeathHold.Final ? "reallife_dynasty_end_final" : "reallife_dynasty_end_no_heir", null, OpenWill);
                    }
                    break;
            }
        }

        /// <summary>The hour has struck (the game's own event, and a time skip passes through every one): the last days are looked at again.</summary>
        private void HourStruck()
        {
            if (_broken || !Running || DynastyGame.PlayerDead)
            {
                return;   // nothing of the dynasty runs under the death scene
            }
            try
            {
                if (_state.Hand != null)
                {
                    if (DynastyRules.InLastDays(DynastyGame.AgeDays, DynastyGame.DaysPerYear))
                    {
                        FinishNow(false);   // a handover left half done: the end is near, so it is finished in this call
                    }
                    return;
                }
                if (DynastyRules.InLastDays(DynastyGame.AgeDays, DynastyGame.DaysPerYear))
                {
                    LastDays(DynastyGame.Day);
                }
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Dynasty: switched off for this session after the error above. The save keeps the will, the statements and a handover under way as they were.");
            }
        }

        /// <summary>What holds the last day back now, and who would carry on.</summary>
        private DeathHold HoldNow(out KinPerson heir, out bool byLaw)
        {
            heir = DynastyRules.Choose(_state.Will, ReadPeople(), out byLaw);
            return DynastyGate.Hold(Running, Ask(DynastyBridges.Permadeath), Ask(DynastyBridges.Armed), GoingPublic.InSession, LawNow(), heir != null);
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
            return DynastyRules.MayRetire(Running, _state.Hand != null, _state.Will, DynastyGame.AgeYears, heir != null, GoingPublic.InSession, DynastyGate.LawOpen(LawNow()),
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

        // The freeze: the estate is valued once and its statement fixed; the steps that follow only carry it out. A death goes through every step at once.
        private void Begin(HandMode mode, KinPerson heir, bool byLaw)
        {
            EstateIn estate = DynastyGame.Estate(mode, _state);
            Statement s = DynastyRules.Settle(estate, _state.Will, heir, byLaw, _people);
            if (!DynastyHand.Begin(_state, s))
            {
                return;
            }
            _draft = null;
            _state.Held = false;
            _revision++;
            Logger?.Info("Dynasty: " + (mode == HandMode.Retire ? "the founder retires" : "the founder's last days") + ": the estate of " + s.Net + " passes to " + s.To + " ("
                         + (byLaw ? "by the law's order" : "by the will") + "); tax " + s.Tax + " (" + s.TaxCash + " from the cash, " + s.Loan + " owed), bequests " + s.Paid + ", kept "
                         + s.Keep + ", the heir's rest " + s.Residue + ".");
            if (Verbose)
            {
                bool ok = DynastyRules.Balanced(s) && DynastyRules.CashOut(s) <= Math.Max(0L, s.Cash - s.Reserved);
                Logger?.Info("Dynasty check (the estate frozen): " + s.Net + " = kept " + s.Keep + " + tax " + s.Tax + " + bequests " + s.Paid + " + the heir's " + s.Residue + ", cash out "
                             + DynastyRules.CashOut(s) + " of " + (s.Cash - s.Reserved) + " over the investors' floor: " + (ok ? "PASS" : "FAIL"));
            }
            DynastyGame.Tell(NotificationType.Info, mode == HandMode.Retire ? "reallife_dynasty_hand_retire" : "reallife_dynasty_hand_death",
                DynastyGame.Data("from", s.From, "to", s.To, "tax", DynastyGame.Money(s.Tax)), OpenEstate);
            if (mode == HandMode.Death)
            {
                FinishNow(true);
            }
        }

        // Every step that is left, in this call.
        private void FinishNow(bool began)
        {
            _cashBefore = DynastyGame.Cash;
            _out = _in = 0;
            try
            {
                DynastyHand.Finish(_state, this, DynastyGame.Day, DynastyGame.DaysPerYear, DynastyGame.MonthDays);
            }
            catch (HandFault f)
            {
                Stuck(f);
                return;
            }
            Settled(began);
        }

        private void HandStep()
        {
            bool done;
            _cashBefore = DynastyGame.Cash;
            _out = _in = 0;
            try
            {
                done = DynastyHand.Step(_state, this, DynastyGame.Day, DynastyGame.DaysPerYear, DynastyGame.MonthDays);
            }
            catch (HandFault f)
            {
                Stuck(f);
                return;
            }
            _revision++;
            if (done)
            {
                Settled(false);
            }
        }

        // A booking was refused or a landing could not be undone: the step stops where it is, loudly, with what was already taken kept in the save. Nothing is paid twice and no
        // money is made; the feature is switched off for the session and the handover goes on from this step when the save is loaded again.
        private void Stuck(HandFault f)
        {
            _broken = true;
            _revision++;
            Logger?.Error(f);
            Logger?.Warn("Dynasty: the handover stopped at step " + _state.Hand.Step + " and the feature is switched off for this session. "
                         + "What was taken from the wallet is kept in the save, and the handover goes on from here when it is loaded again.");
        }

        private long _cashBefore, _out, _in;   // the wallet when the call began, and what the call booked out of it and back into it

        // The handover is over: the heir is the head, the statement is kept, and the wallet is checked against what was booked (and, for one that began and ended in this very
        // call, against what the statement says it takes out: its tax in cash, what is kept and the bequests that landed).
        private void Settled(bool began)
        {
            Statement s = _state.Statements.Count > 0 ? _state.Statements[_state.Statements.Count - 1] : null;
            if (s == null || _state.Hand != null)
            {
                return;
            }
            _revision++;
            if (Verbose)
            {
                bool wallet = DynastyRules.WalletMatches(_cashBefore, _out - _in, DynastyGame.Cash) && (!began || _out - _in == DynastyRules.CashOut(s));
                bool ok = DynastyRules.Balanced(s) && DynastyGame.Name == s.To && _state.Generation == s.Generation + 1 && _state.Hand == null && wallet;
                Logger?.Info("Dynasty check (the estate settled): " + s.Net + " = kept " + s.Keep + " + tax " + s.Tax + " + bequests " + s.Paid + " + the heir's " + s.Residue
                             + "; the head is now " + DynastyGame.Name + ", generation " + _state.Generation + "; the wallet " + _cashBefore + " to " + DynastyGame.Cash + " after " + _out
                             + " booked out and " + _in + " back" + (began ? " against the statement's " + DynastyRules.CashOut(s) : string.Empty) + ": " + (ok ? "PASS" : "FAIL"));
            }
            DynastyGame.Tell(NotificationType.Success, "reallife_dynasty_hand_done", DynastyGame.Data("to", s.To, "from", s.From, "age", s.HeirAge.ToString(),
                "share", (s.Upbringing / 10).ToString()), OpenEstate);
        }

        int IHandWorld.FounderAgeDays => Math.Max(0, DynastyGame.AgeDays);

        long IHandWorld.PayTax(long amount) => Out(DynastyGame.PayUnits(amount, DynastyGame.TaxKey, true));

        long IHandWorld.PayKeep(long amount) => Out(DynastyGame.PayUnits(amount, DynastyGame.KeepKey, true));

        long IHandWorld.Book(BequestLine line, long amount) => Out(DynastyGame.PayUnits(amount, line.Landing == Landing.Pot ? DynastyGame.CharityKey : DynastyGame.BequestKey, true));

        private long Out(long paid)
        {
            _out += paid;
            return paid;
        }

        // A bequest lands where its line says: what really landed is what is told back. The money has already left the wallet; what did not land is given back by Refund.
        long IHandWorld.Land(BequestLine line, long amount)
        {
            long landed = 0;
            try
            {
                switch (line.Landing)
                {
                    case Landing.Pot:
                        landed = DynastyBridges.ToCharity != null ? DynastyBridges.ToCharity(line.Id, amount) : 0L;
                        break;
                    case Landing.Household:
                        landed = DynastyBridges.ToHousehold != null ? DynastyBridges.ToHousehold(line.Id, amount) : 0L;
                        break;
                    case Landing.Family:
                        landed = amount;
                        break;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return Math.Max(0L, Math.Min(amount, landed));
        }

        long IHandWorld.Refund(BequestLine line, long amount)
        {
            long came = DynastyGame.Receive(amount, line.Landing == Landing.Pot ? DynastyGame.CharityKey : DynastyGame.BequestKey);
            _in += came;
            return came;
        }

        // The other features, each in its own guard (the wiring), told while the played person is still the founder by name.
        void IHandWorld.Succeed(Statement s)
        {
            try
            {
                DynastyBridges.Succeed?.Invoke(s);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Then the played person: their name, their age, their learning.
        void IHandWorld.Become(Statement s) => DynastyGame.Become(s.To, s.HeirAge, s.Upbringing);

        // ---- the debt for tax the cash did not cover

        // A part a day, for each day gone by since the last one, however many (a time skip's days, a save loaded after years, the option off for a long while): each is charged one by
        // one, and a part the cash over the investors' floor does not cover is a late day. The spare cash is read once and taken down as parts are paid.
        private void LoanDays(int day, int year)
        {
            EstateLoan l = _state.Loan;
            if (!l.Open)
            {
                return;
            }
            DynastyRules.LoanDays(l, day, DynastyGame.Spare * 100, year, cents => DynastyGame.PayCents(cents, DynastyGame.TaxPartKey, true), (cents, forced, late) =>
            {
                if (forced)
                {
                    DynastyGame.Tell(NotificationType.Warning, "reallife_dynasty_loan_forced", DynastyGame.Data("amount", DynastyGame.Money(cents / 100)), OpenEstate);
                }
                else if (cents == 0 && late == 1)
                {
                    DynastyGame.Tell(NotificationType.Warning, "reallife_dynasty_loan_late", DynastyGame.Data("days", DynastyCatalogue.LoanLateMost.ToString()), OpenEstate);
                }
            });
            if (!l.Open)
            {
                DynastyGame.Tell(NotificationType.Success, "reallife_dynasty_loan_paid", null, OpenEstate);
            }
        }

        // ---- the retired founder

        // Once a month they draw their living from their fund and it is paid to the head of the family; at the game's own last age they die, and what is left of the fund is an
        // estate of its own that goes to the head.
        private void ElderDay(int day, int year)
        {
            Elder e = _state.Elder;
            if (e == null)
            {
                return;
            }
            DynastyRules.ElderPaid(e, (Math.Max(1, day) - 1) / Math.Max(1, DynastyGame.MonthDays), draw => DynastyGame.Receive(draw, DynastyGame.FundKey));
            if (!DynastyRules.ElderDies(e, day, year))
            {
                return;
            }
            Statement s = DynastyRules.SettleElder(e, DynastyGame.Name, day, DynastyGame.Level, year, Math.Max(1, _state.Generation - 1));
            long left = DynastyGame.Receive(s.Residue, DynastyGame.FundKey);
            if (left < s.Residue)
            {
                Logger?.Error(new HandFault("the retired founder's fund could not be booked whole: " + left + " of " + s.Residue + " came in"));
            }
            _state.Elder = null;
            _state.Keep(s);
            if (Verbose)
            {
                bool ok = DynastyRules.Balanced(s) && s.Residue == left;
                Logger?.Info("Dynasty check (the retired founder's fund): " + s.Net + " left = tax " + s.Tax + " + " + s.Residue + " to the head of the family (" + left + " came in), counted with "
                             + s.Prior + " handed over inside the window, after " + e.Month + " months of living drawn: " + (ok ? "PASS" : "FAIL"));
            }
            DynastyGame.Tell(NotificationType.Info, "reallife_dynasty_elder_died", DynastyGame.Data("name", e.Name, "amount", DynastyGame.Money(s.Residue), "tax", DynastyGame.Money(s.Tax)),
                OpenEstate);
        }
    }
}
