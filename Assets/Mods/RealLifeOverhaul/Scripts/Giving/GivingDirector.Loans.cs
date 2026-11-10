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
            int p = GivingPeople.ResidentOf(s, person.Index);
            int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
            if (h < 0 || !GivingPeople.Helpable(s, h))
            {
                return GiveResult.NoPerson;
            }
            if (amount <= 0)
            {
                return GiveResult.Nothing;
            }
            if (amount > GivingRules.At(GivingCatalogue.LoanMost, GivingGame.Level) || _state.OpenLoans() >= GivingCatalogue.LoansMost || _state.LoanOf(person.Index) != null)
            {
                return GiveResult.Limit;
            }
            if (!GivingGame.Pay(amount, GivingGame.LoanKey))
            {
                return GiveResult.Broke;
            }
            GivingPeople.Help(s, h, amount, out _, out _);
            int day = GivingGame.Day;
            _state.Loans.Add(new LoanOut
            {
                Person = person.Index, Name = person.Name, Principal = amount, Remaining = amount, Made = day, Part = GivingRules.LoanPart(amount), Next = day + GivingGame.MonthDays
            });
            TrimLoans();
            SocialLife.Contacted(person);
            SocialLife.Remember(person, MemoryCode.Loan);
            SocialLife.Change(person, 0f, 0.2f, 0.5f);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (a loan of " + amount + " to " + person.Name + "): in their household, parts of " + GivingRules.LoanPart(amount)
                             + " from day " + (day + GivingGame.MonthDays) + ": PASS");
            }
            return GiveResult.Done;
        }

        // The closed loans a page keeps, the oldest dropped.
        private void TrimLoans()
        {
            int closed = 0;
            for (int i = _state.Loans.Count - 1; i >= 0; i--)
            {
                if (_state.Loans[i].State != LoanState.Open && ++closed > GivingCatalogue.LoanKept)
                {
                    _state.Loans.RemoveAt(i);
                }
            }
        }

        // The day's loans: a part falls due once a month from the loan; the person pays what their household can spare, or nothing, and after a few months of nothing it is written off.
        private void CollectLoans(int day)
        {
            for (int i = 0; i < _state.Loans.Count; i++)
            {
                LoanOut l = _state.Loans[i];
                if (l.State != LoanState.Open || day < l.Next)
                {
                    continue;
                }
                PopulationState s = GivingGame.People();
                if (s == null)
                {
                    continue;   // the city's people are not here (a session, Living population off): the part waits and nothing is held against the person
                }
                int p = GivingPeople.ResidentOf(s, l.Person);
                int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
                long spare = h >= 0 && s.Households.Alive(h) ? GivingPeople.Spare(s, h) : 0L;
                int want = GivingRules.Repayment(l.Remaining, l.Part, spare);
                long took = want > 0 ? GivingPeople.Take(s, h, want) : 0L;
                l.Next = day + GivingGame.MonthDays;
                SocialPerson person = SocialState.Get(l.Person);
                if (took > 0)
                {
                    GivingGame.Receive(took, GivingGame.LoanBackKey);
                    l.Remaining -= (int)took;
                    l.Paid += (int)took;
                    l.Misses = 0;
                    if (l.Remaining <= 0)
                    {
                        l.State = LoanState.Repaid;
                        l.Closed = day;
                        if (person != null)
                        {
                            SocialLife.Change(person, 0f, 0.5f, 1f);
                        }
                    }
                    GivingGame.Tell(NotificationType.Info, l.State == LoanState.Repaid ? "reallife_giving_loan_repaid" : "reallife_giving_loan_part",
                        GivingGame.Data("name", l.Name, "amount", GivingGame.Money(took), "left", GivingGame.Money(Math.Max(0, l.Remaining))));
                    continue;
                }
                l.Misses++;
                if (l.Misses < GivingCatalogue.LoanMisses)
                {
                    continue;
                }
                l.State = LoanState.WrittenOff;
                l.Closed = day;
                if (person != null)
                {
                    SocialLife.Remember(person, MemoryCode.NotRepaid);
                    SocialLife.Change(person, 0f, -3f, -5f);
                }
                GivingGame.Tell(NotificationType.Warning, "reallife_giving_loan_off", GivingGame.Data("name", l.Name, "amount", GivingGame.Money(l.Remaining)));
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
            if (GivingRules.Hash(_state.Seed, 91, person.Index, day * 24 + hour) % 10000 >= (uint)chance)
            {
                return false;
            }
            int p = GivingPeople.ResidentOf(s, person.Index);
            GivingNeed need = p >= 0 ? GivingPeople.NeedOf(s, p, out long owes) : GivingNeed.None;
            if (need == GivingNeed.None)
            {
                return false;
            }
            int h = GivingPeople.HouseholdOf(s, p);
            long basis = need == GivingNeed.Rent ? GivingPeople.MonthRent(s, h) : s.Households.Debt[h];
            int amount = GivingRules.AskAmount(basis, GivingGame.Level);
            if (amount < GivingRules.At(100, GivingGame.Level))
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
