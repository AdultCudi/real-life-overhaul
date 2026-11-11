using System;
using UI.Notification;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait years for. A handover cannot be put back (the played person, the family and the money have changed),
    /// so the test that begins one says so; test it on a copy of a save. Each says what happened in a notice and writes one "Dynasty check" line.
    /// </summary>
    public sealed partial class DynastyDirector
    {
        private bool TestReady()
        {
            if (!Running)
            {
                DynastyGame.Tell(NotificationType.Warning, "reallife_dynasty_test_off", null);
                return false;
            }
            return true;
        }

        /// <summary>Options, Tests: what the estate would be and bear if it passed today. Changes nothing.</summary>
        public void TestStatement()
        {
            if (!TestReady())
            {
                return;
            }
            KinPerson heir = DynastyRules.Choose(_state.Will, ReadPeople(), out bool byLaw);
            Statement s = DynastyRules.Settle(DynastyGame.Estate(HandMode.Death, _state), _state.Will, heir, byLaw, _people);
            DynastyGame.Tell(NotificationType.Info, "reallife_dynasty_test_statement", DynastyGame.Data("estate", DynastyGame.Money(s.Net), "tax", DynastyGame.Money(s.Tax),
                "heir", heir != null ? heir.Name : DynastyText.Fill("reallife_dynasty_fig_nobody")), OpenEstate);
            Logger?.Info("Dynasty check (test, the estate today): " + s.Net + " = kept " + s.Keep + " + tax " + s.Tax + " + bequests " + s.Paid + " + the heir's " + s.Residue + "; taxable "
                         + s.Taxable + " with gifts " + s.Gifts + ", charities " + s.Charity + " and a partner's " + s.Spouse + " taken off; heir " + (heir != null ? heir.Name : "nobody")
                         + (byLaw ? " by the law's order" : " by the will") + ": " + (DynastyRules.Balanced(s) ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the played person is the age their will sets for retiring (or the earliest, with no will), so retiring can be tried.</summary>
        public void TestRetireAge()
        {
            if (!TestReady())
            {
                return;
            }
            int age = _state.Will.Written ? _state.Will.RetireAge : DynastyCatalogue.RetireLeast;
            int year = DynastyGame.DaysPerYear;
            if (DynastyGame.AgeDays < age * year)
            {
                DynastyGame.SetAgeDays(age * year);
            }
            _revision++;
            RetireWhy why = RetireWhyNow(out KinPerson _, out bool _);
            DynastyGame.Tell(NotificationType.Info, "reallife_dynasty_test_age", DynastyGame.Data("age", DynastyGame.AgeYears.ToString()), OpenEstate);
            Logger?.Info("Dynasty check (test, the retiring age): the played person is " + DynastyGame.AgeYears + " years old; retiring now: " + why + ": "
                         + (DynastyGame.AgeYears >= age ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the played person is in their last days now, so the estate passes within a second or two (or the notice says what holds it). Cannot be put back.</summary>
        public void TestLastDays()
        {
            if (!TestReady())
            {
                return;
            }
            if (_state.Hand != null)
            {
                DynastyGame.Tell(NotificationType.Warning, "reallife_dynasty_retire_running", null);
                return;
            }
            int year = DynastyGame.DaysPerYear;
            DeathHold hold = HoldNow(out KinPerson heir, out bool _);
            DynastyGame.SetAgeDays(DynastyRules.LastDays(year));
            DynastyGame.Tell(hold == DeathHold.None ? NotificationType.Info : NotificationType.Warning, "reallife_dynasty_test_last",
                DynastyGame.Data("heir", heir != null ? heir.Name : DynastyText.Fill("reallife_dynasty_fig_nobody")));
            Logger?.Info("Dynasty check (test, the last days): the age is " + DynastyGame.AgeDays + " days, the last days begin at " + DynastyRules.LastDays(year) + "; hold " + hold
                         + ", heir " + (heir != null ? heir.Name : "nobody") + ": " + (DynastyGame.AgeDays >= DynastyRules.LastDays(year) ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the first rival tycoon who is forward hands over today, as if they had reached the age.</summary>
        public void TestRival()
        {
            if (!TestReady())
            {
                return;
            }
            int day = DynastyGame.Day, year = DynastyGame.DaysPerYear;
            for (int i = 0; DynastyBridges.RivalWorth != null && i < _state.RivalLines.Length; i++)
            {
                long worth = DynastyBridges.RivalWorth(i);
                if (worth < 0)
                {
                    continue;
                }
                RivalLine r = _state.RivalLines[i];
                DynastyRivals.Begin(r, _state.Seed, i, day, year);
                int before = r.Generation;
                RivalHands(i, r, r.RetireAge == int.MaxValue ? RivalEvent.Died : RivalEvent.Retired, worth, day, year);
                _revision++;
                DynastyGame.Tell(NotificationType.Info, "reallife_dynasty_test_rival", DynastyGame.Data("line", RivalLineOf(i)));
                Logger?.Info("Dynasty check (test, a rival's handover): generation " + before + " to " + r.Generation + ", tax paid " + r.HandTax + " of a fortune of " + r.HandWorth + ": "
                             + (r.Generation == before + 1 && r.HandTax <= r.HandWorth ? "PASS" : "FAIL"));
                return;
            }
            DynastyGame.Tell(NotificationType.Warning, "reallife_dynasty_test_no_rival", null);
            Logger?.Info("Dynasty check (test, a rival's handover): no tycoon is forward, or they rest in a session: nothing done.");
        }
    }
}
