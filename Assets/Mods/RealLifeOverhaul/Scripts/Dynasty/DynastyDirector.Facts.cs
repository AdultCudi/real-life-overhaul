using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>What the pages read: filled when a page is written (the revision moved, or a press), never a frame.</summary>
    public sealed partial class DynastyDirector
    {
        private readonly DynastyEstateCache<EstatePair> _estateCache = new DynastyEstateCache<EstatePair>();

        /// <summary>Times the estate was valued for a page or the tax side (it is kept for a day, a revision and the cash).</summary>
        public int EstatesWorked => _estateCache.Worked;

        // The estate as it would pass today, in both ways, worked out once for the day, the director's revision and the cash: a page rebuilt often, or the tax side's row, is
        // answered from memory. One reading of the game's holdings serves both.
        private EstatePair EstateNow()
        {
            return _estateCache.Get(DynastyGame.Day, _revision, DynastyGame.Cash, () =>
            {
                List<KinPerson> people = ReadPeople();
                KinPerson heir = DynastyRules.Choose(_state.Will, people, out bool byLaw);
                EstateIn e = DynastyGame.Estate(HandMode.Death, _state);
                return new EstatePair
                {
                    Death = DynastyRules.Settle(e, _state.Will, heir, byLaw, people),
                    Retire = DynastyRules.Settle(e.For(HandMode.Retire, _state.Will.Keep), _state.Will, heir, byLaw, people)
                };
            });
        }

        /// <summary>
        /// Fills the facts. False, with Ready off, while the dynasty does not run for this save. The estate is valued once for a day, a revision and the cash (EstateNow) whatever the
        /// tab; only the Estate tab (<paramref name="estate"/>) asks the law what holds a handover or refuses retiring.
        /// </summary>
        public bool Facts(DynastyFacts f, bool estate = true)
        {
            f.Ready = Running;
            f.People.Clear();
            f.Statements.Clear();
            f.Line.Clear();
            f.Kin.Clear();
            f.RivalLines.Clear();
            f.Today = f.RetireToday = null;
            f.HeirName = string.Empty;
            f.Retire = RetireWhy.Off;
            f.Hold = DeathHold.Off;
            if (!Running)
            {
                return false;
            }
            int day = DynastyGame.Day, year = DynastyGame.DaysPerYear;
            f.Session = GoingPublic.InSession;
            f.Name = DynastyGame.Name;
            f.Age = DynastyGame.AgeYears;
            f.Generation = _state.Generation;
            f.Level = DynastyGame.Level;
            f.Cash = DynastyGame.Cash;
            f.Spare = DynastyGame.Spare;
            f.Gifts = DynastyRules.GiftsInWindow(_state.Gifts, day, year);
            f.Signed = _state.Will;
            f.Draft = Draft;
            f.Dirty = _draft != null && !(_state.Will.Written && _state.Will.SameTerms(_draft));
            f.DraftWhy = DynastyRules.Valid(f.Draft);
            Notary notary = NotaryNow();
            f.Firm = notary.Name ?? string.Empty;
            f.OwnFirm = notary.Own;
            f.Fee = DynastyRules.Fee(!_state.Will.Written, notary.Own, f.Level);
            List<KinPerson> people = ReadPeople();
            f.People.AddRange(people);
            KinPerson heir = DynastyRules.Choose(_state.Will, people, out bool byLaw);
            f.HeirKnown = heir != null;
            f.HeirName = heir != null ? heir.Name ?? string.Empty : string.Empty;
            EstatePair pair = EstateNow();   // from memory unless the day, the revision or the cash moved
            f.Today = pair.Death;
            f.RetireToday = pair.Retire;
            if (estate)
            {
                LawFacts law = LawNow();
                f.Retire = DynastyRules.MayRetire(Running, _state.Hand != null, _state.Will, f.Age, heir != null, f.Session, DynastyGate.LawOpen(law), _state.Elder != null);
                f.Hold = DynastyGate.Hold(Running, Ask(DynastyBridges.Permadeath), Ask(DynastyBridges.Armed), f.Session, law, heir != null);
            }
            f.LastDays = DynastyRules.InLastDays(DynastyGame.AgeDays, year);
            f.Handing = _state.Hand != null;
            f.Step = DynastyHand.Done(_state.Hand);
            f.Steps = DynastyHand.Steps(_state.Hand);
            f.Elder = _state.Elder;
            f.ElderAge = _state.Elder != null ? DynastyRules.ElderAge(_state.Elder, day, year) : 0;
            f.LoanOwed = (_state.Loan.Owed + 99) / 100;
            f.LoanPerDay = (_state.Loan.PerDay + 99) / 100;
            f.LoanPrincipal = _state.Loan.Principal;
            f.LoanLate = _state.Loan.Late;
            f.Statements.AddRange(_state.Statements);
            f.Line.AddRange(_state.Line);
            f.Kin.AddRange(_state.Kin);
            for (int i = 0; i < _state.RivalLines.Length; i++)
            {
                string line = RivalLineOf(i);
                if (!string.IsNullOrEmpty(line) && DynastyBridges.RivalWorth != null && DynastyBridges.RivalWorth(i) >= 0)
                {
                    f.RivalLines.Add(line);
                }
            }
            f.City = _state.City;
            return true;
        }

        /// <summary>The tax the estate would bear if it passed today, for the line on the tax side; -1 while the dynasty does not run.</summary>
        public long TaxToday()
        {
            if (!Running)
            {
                return -1L;
            }
            return EstateNow().Death.Tax;
        }

        /// <summary>One line on the will and the heir for the family side: who carries on, and whether a will says so.</summary>
        public string FamilyLine()
        {
            if (!Running)
            {
                return string.Empty;
            }
            KinPerson heir = DynastyRules.Choose(_state.Will, ReadPeople(), out bool byLaw);
            if (heir == null)
            {
                return DynastyText.Fill(_state.Will.Written ? "reallife_dynasty_family_no_heir_will" : "reallife_dynasty_family_no_heir");
            }
            return DynastyText.Fill(byLaw ? "reallife_dynasty_family_by_law" : "reallife_dynasty_family_by_will", "name", heir.Name);
        }
    }
}
