using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for, with real money of the smallest step. Each says what happened in a notice and
    /// writes one "Giving check" line.
    /// </summary>
    public sealed partial class GivingDirector
    {
        private bool TestReady()
        {
            if (!Running)
            {
                GivingGame.Tell(NotificationType.Warning, "reallife_giving_test_off", null);
                return false;
            }
            if (_beforeTests == null)
            {
                _beforeTests = GivingStore.Write(_state);   // what "put giving back as before" restores
            }
            return true;
        }

        private static void TestSays(string key, Dictionary<string, string> data)
        {
            GivingGame.Tell(NotificationType.Info, key, data);
        }

        /// <summary>Options, Tests: a gift of the smallest step to each charity in turn, the year's deduction asked for at once.</summary>
        public void TestGift()
        {
            if (!TestReady())
            {
                return;
            }
            int amount = GivingRules.StepNow(2, GivingGame.Level);
            Charity c = (Charity)(int)(_state.TaxGifts / Math.Max(1, amount) % (int)Charity.Count);
            GiveResult r = GiveToCharity(c, amount, c == Charity.Upkeep ? 0 : -1);
            if (GivingResults.Moved(r))
            {
                TestSays("reallife_giving_test_gift", GivingGame.Data("amount", GivingGame.Money(amount), "charity", GivingText.Charity((int)c), "year", GivingGame.Money(_state.TaxGifts)));
            }
            else
            {
                Say(r);
            }
            Logger?.Info("Giving check (test, a gift of " + amount + " to " + c + "): " + (GivingResults.Moved(r) ? "PASS" : "not given (" + r + ")"));
        }

        /// <summary>Options, Tests: the month's pass now (standing gifts, the rivals' gifts, the pay-outs, the neighbours), one step a second.</summary>
        public void TestMonth()
        {
            if (!TestReady())
            {
                return;
            }
            bool began = GivingMonth.Begin(_state, Math.Max(0, _state.Month) + 1);
            TestSays("reallife_giving_test_month", null);
            Logger?.Info("Giving check (test, a month's pass now): " + (began ? "month " + _state.MonthNumber + " begins" : "a pass is under way already") + ": PASS");
        }

        private static SocialPerson FirstKnown()
        {
            for (int i = 0; i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met && !p.IsFamily)
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>Options, Tests: the smallest gift to the first person the player knows.</summary>
        public void TestPerson(bool lend)
        {
            if (!TestReady())
            {
                return;
            }
            SocialPerson p = FirstKnown();
            if (p == null)
            {
                TestSays("reallife_giving_test_nobody", null);
                return;
            }
            int amount = GivingRules.StepNow(2, GivingGame.Level);
            GiveResult r = lend ? Lend(p, amount) : GiveToPerson(p, amount, false);
            if (GivingResults.Moved(r))
            {
                TestSays(lend ? "reallife_giving_test_lend" : "reallife_giving_test_person", GivingGame.Data("amount", GivingGame.Money(amount), "name", p.Name));
            }
            else
            {
                Say(r);
            }
            Logger?.Info("Giving check (test, " + (lend ? "a loan" : "a gift") + " of " + amount + " to " + p.Name + "): " + (GivingResults.Moved(r) ? "PASS" : "not given (" + r + ")"));
        }

        /// <summary>
        /// Options, Tests: the first person the population holds helps with what they need most cheaply: a course, else a month's rent, else half a debt. Real money, the smallest of each, and
        /// the person may refuse, which the notice then says.
        /// </summary>
        public void TestHelp()
        {
            if (!TestReady())
            {
                return;
            }
            PopulationState s = GivingGame.People();
            HelpKind[] order = { HelpKind.Course, HelpKind.Rent, HelpKind.Debt };
            int[] option = { 0, 1, 1 };
            for (int i = 0; s != null && i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p == null || !p.Met)
                {
                    continue;
                }
                for (int k = 0; k < order.Length; k++)
                {
                    if (!PriceOf(p, order[k], option[k], out int amount, out _))
                    {
                        continue;
                    }
                    GiveResult r = GiveHelp(p, order[k], option[k], false);
                    if (GivingResults.Moved(r))
                    {
                        TestSays("reallife_giving_test_help", GivingGame.Data("amount", GivingGame.Money(amount), "name", p.Name));
                    }
                    else
                    {
                        Say(r);
                    }
                    Logger?.Info("Giving check (test, " + order[k] + " of " + amount + " for " + p.Name + "): " + (GivingResults.Moved(r) ? "PASS" : "not given (" + r + ")"));
                    return;
                }
            }
            TestSays("reallife_giving_test_nobody_to_help", null);
        }

        /// <summary>
        /// Options, Tests: a hall named for the player, with nothing paid: the naming is kept for the page and the City Ledger prints it the morning after, so how a naming reads can be seen
        /// without a gift of a quarter of a million. Put back with "put giving back as before".
        /// </summary>
        public void TestNaming()
        {
            if (!TestReady())
            {
                return;
            }
            Charity c = Charity.Scholars;
            long amount = GivingRules.At(GivingCatalogue.NamingFrom, GivingGame.Level);
            var naming = new GivingNaming { Day = GivingGame.Day, Charity = (int)c, Amount = amount, Name = GivingGame.PlayerName };
            _state.Namings.Add(naming);
            _state.Notes.Add(new GivingNote { Day = GivingGame.Day, Charity = (int)c, Amount = amount, Giver = -1, Naming = 1, Tag = -1 });
            _revision++;
            TestSays("reallife_giving_test_naming", GivingGame.Data("naming", GivingText.Naming(naming)));
            Logger?.Info("Giving check (test, a hall named for the player, nothing paid): the page lists it and the paper prints it tomorrow: PASS");
        }

        /// <summary>Options, Tests: the first person the population holds in trouble asks the player for help, at once.</summary>
        public void TestAsk()
        {
            if (!TestReady())
            {
                return;
            }
            PopulationState s = GivingGame.People();
            for (int i = 0; s != null && i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p == null || !p.Met || SocialState.Offer.Active)
                {
                    continue;
                }
                int r = GivingPeople.ResidentOf(s, p.Index);
                if (r < 0 || GivingPeople.NeedOf(s, r, out long owes) == GivingNeed.None)
                {
                    continue;
                }
                _state.LastAsk = 0;
                _state.PersonOf(p.Index, true).AskDay = 0;
                SocialOffer offer = SocialState.Offer;
                offer.Person = p.Index;
                offer.Kind = SocialOffer.Help;
                offer.Amount = Math.Max(GivingRules.StepNow(2, GivingGame.Level), GivingRules.AskAmount(owes, GivingGame.Level));
                offer.Expires = SocialLife.Now + GivingCatalogue.AskExpireHours * 60f;
                TestSays("reallife_giving_test_ask", GivingGame.Data("name", p.Name));
                Logger?.Info("Giving check (test, " + p.Name + " asks for " + offer.Amount + "): the call answers it: PASS");
                return;
            }
            TestSays("reallife_giving_test_nobody_in_trouble", null);
        }

        /// <summary>Options, Tests: the gifts, standing gifts and loans as they were before the tests (the money given stays given).</summary>
        public void TestBack()
        {
            if (!Running || _beforeTests == null)
            {
                TestSays("reallife_giving_test_back_none", null);
                return;
            }
            GivingState before = GivingStore.Read(_beforeTests, out _);
            if (before != null)
            {
                _state = before;
                _state.LastDay = 0;
            }
            _beforeTests = null;
            _revision++;
            TestSays("reallife_giving_test_back", null);
            Logger?.Info("Giving check (test, put back as before): the line is as it was: PASS");
        }
    }
}
