using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Giving
{
    /// <summary>Loans to people, and the people who ask. A loan is paid back in parts out of what the person's household can spare, or written off; nobody is taken to court.</summary>
    public sealed partial class GivingDirector
    {
        // ---- a loan

        /// <summary>
        /// Lends <paramref name="amount"/> to a person: no interest, paid back in <see cref="GivingCatalogue.LoanParts"/> monthly parts from what their household can spare. The money
        /// lands in their household as a gift does. Only a resident the population holds can be lent to, because they are the ones who can pay it back out of their own money.
        /// The warmth it earns is counted in the person's window with their gifts (half of a gift of the same size, <see cref="GivingCatalogue.LoanShare"/>), so lending the same
        /// small sum again and again earns no more than lending it once; paying it back earns none. A loan to somebody who is asking answers the ask.
        /// </summary>
        public GiveResult Lend(SocialPerson person, int amount)
        {
            if (!Running || person == null || !person.Met)
            {
                return GiveResult.Off;
            }
            PopulationState s = GivingGame.People();
            if (s == null)
            {
                return GoingPublic.InSession ? GiveResult.Session : GiveResult.NoCause;
            }
            int level = GivingGame.Level;
            GiveResult allowed = GivingLoans.Check(_state, s, person.Index, amount, level, out int p, out int h);
            if (allowed != GiveResult.Done)
            {
                return allowed;
            }
            if (!GivingGame.Pay(amount, GivingGame.LoanKey))
            {
                return GiveResult.Broke;
            }
            bool inNeed = GivingPeople.NeedOf(s, p, out _) != GivingNeed.None;
            int day = GivingGame.Day;
            GivingLoans.Make(_state, s, h, person.Index, person.Name, amount, day, GivingGame.MonthDays);
            GivingRules.PersonGain(_state.PersonOf(person.Index, true), GivingRules.LoanCounts(GivingRules.Back(amount, level)), day, inNeed, out int affection, out int trust);
            SocialLife.Contacted(person);
            SocialLife.Remember(person, MemoryCode.Loan);
            SocialLife.Change(person, 0f, affection / 10f, trust / 10f);
            Answered(person);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (a loan of " + amount + " to " + person.Name + "): in their household, parts of " + GivingRules.LoanPart(amount)
                             + " from day " + (day + GivingGame.MonthDays) + "; affection +" + affection / 10f + ", trust +" + trust / 10f + ": PASS");
            }
            return GiveResult.Done;
        }

        // The day's loans: a part falls due once a month from the loan (GivingLoans.Collect); the person pays what their household can spare, or nothing, and after a few months of
        // nothing it is written off.
        private void CollectLoans(int day)
        {
            PopulationState s = GivingGame.People();
            if (s == null)
            {
                return;   // the city's people are not here (a session, Living population off): the part waits and nothing is held against the person
            }
            for (int i = 0; i < _state.Loans.Count; i++)
            {
                LoanOut l = _state.Loans[i];
                LoanTurn turn = GivingLoans.Collect(l, s, day, GivingGame.MonthDays, out long took);
                if (turn == LoanTurn.None)
                {
                    continue;
                }
                if (took > 0)
                {
                    GivingGame.Receive(took, GivingGame.LoanBackKey);
                    GivingGame.Tell(NotificationType.Info, turn == LoanTurn.Repaid ? "reallife_giving_loan_repaid" : "reallife_giving_loan_part",
                        GivingGame.Data("name", l.Name, "amount", GivingGame.Money(took), "left", GivingGame.Money(Math.Max(0, l.Remaining))));
                    continue;
                }
                if (turn != LoanTurn.WrittenOff)
                {
                    continue;
                }
                SocialPerson person = SocialState.Get(l.Person);
                if (person != null)
                {
                    SocialLife.Remember(person, MemoryCode.NotRepaid);
                    SocialLife.Change(person, 0f, -3f, -5f);
                }
                GivingGame.Tell(NotificationType.Warning, "reallife_giving_loan_off", GivingGame.Data("name", l.Name, "amount", GivingGame.Money(l.Remaining)));
                GivingBridges.GaveToPerson?.Invoke(Math.Max(0, l.Remaining));   // what was never paid back was given
                if (Verbose)
                {
                    Logger?.Info("Giving check (a loan to " + l.Name + " written off): " + l.Remaining + " of " + l.Principal + " left after " + l.Misses
                                 + " months of nothing; no deduction, no case: PASS");
                }
            }
        }

        // ---- the people who ask

        /// <summary>
        /// Someone in real trouble asks the player for help, rarely: out of work, owing more than a month's income, or with a rent over the line, by text and then on the call. Asked from
        /// the social life's hour for a person it has chosen, only when nothing else is waiting. A seeded draw, never more than one ask every two weeks, nor of one person in two months,
        /// and a little likelier the richer and better known the player is. Returns true when the ask was sent.
        /// </summary>
        public bool TryAsk(SocialPerson person)
        {
            if (!Running || person == null || !person.Met || !(person.Stage >= SocialStage.Friend || person.IsFamily))
            {
                return false;
            }
            PopulationState s = GivingGame.People();
            int day = GivingGame.Day, hour = SocialLife.Hour;
            PersonGiving g = _state.PersonOf(person.Index, false);
            if (s == null || !GivingRules.MayAsk(day, _state.LastAsk, g != null ? g.AskDay : 0, (int)person.Affection, (int)person.Trust))
            {
                return false;
            }
            int chance = GivingRules.AskPerTenThousand(GivingBridges.WealthTier != null ? GivingBridges.WealthTier() : 0, GivingBridges.Recognition != null ? GivingBridges.Recognition() : 0);
            if (!GivingRules.Asks(_state.Seed, person.Index, day, hour, chance))
            {
                return false;
            }
            if (!GivingPeople.AskOf(s, person.Index, GivingGame.Level, out GivingNeed need, out int amount))
            {
                return false;
            }
            SocialOffer offer = SocialState.Offer;
            offer.Person = person.Index;
            offer.Kind = SocialOffer.Help;
            offer.Amount = amount;
            offer.Expires = SocialLife.Now + GivingCatalogue.AskExpireHours * 60f;
            string key = need == GivingNeed.Work ? "reallife_giving_ask_work" : need == GivingNeed.Debt ? "reallife_giving_ask_debt" : "reallife_giving_ask_rent";
            if (!SocialLife.SendText(person, key, SocialLife.Data(person, "amount", SocialLife.Money(amount))))
            {
                offer.Clear();
                return false;
            }
            _state.LastAsk = day;
            _state.PersonOf(person.Index, true).AskDay = day;
            if (Verbose)
            {
                Logger?.Info("Giving check (" + person.Name + " asks for " + amount + " in " + need + "): chance " + chance + " in ten thousand, the last ask day " + _state.LastAsk + ": PASS");
            }
            return true;
        }

        /// <summary>What a person's ask is for now (to word the call): their need as the population holds it, None when they are no longer in trouble.</summary>
        public GivingNeed NeedOfPerson(SocialPerson person)
        {
            PopulationState s = GivingGame.People();
            int p = s != null ? GivingPeople.ResidentOf(s, person.Index) : -1;
            return p >= 0 ? GivingPeople.NeedOf(s, p, out _) : GivingNeed.None;
        }
    }
}
