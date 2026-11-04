using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What a day's decisions of the company meet outside its model: the switches, the money, the notices and the records. The director answers it with the
    /// game (the cash through CompanyGame, the notices on the screen, the game's own leave); the run of the checks answers it with a seeded city. The
    /// decisions themselves are <see cref="CompanyDay"/>'s, one set of code for both, so that a check of the run is a check of what the game does. Game-free.
    /// </summary>
    public interface ICompanyDay
    {
        int Seed { get; }
        int Hour { get; }                            // the game's hour now
        bool On(CompanyPart part);                   // the part's option: off, it decides nothing
        bool ManagersOn { get; }
        bool HierarchyOn { get; }
        CompanyState State { get; }
        ManagerState Managers { get; }
        HierarchyState Hierarchy { get; }            // null while the hierarchy is off

        /// <summary>The rule in force at a place (the policy where the hierarchy runs, the manager's own settings where it does not).</summary>
        int Rule(string place, PolicyField field);
        int BonusOf(string place);                   // a one-off bonus in place of a refused raise, at today's prices
        int BudgetOf(ManagerFile m);                 // the month's budget of a manager, at today's prices, with what was added to it

        /// <summary>Pays out of the company's cash; false when the cash is not there (nothing is paid then).</summary>
        bool Pay(int amount, string place, string subject, string why);

        /// <summary>Whether the place is the company's to manage today (a business it owns, not one gone public or sold).</summary>
        bool Mine(string place);

        /// <summary>Whether the policy lets managers decide this kind of thing at the place.</summary>
        bool PolicyAllows(string place, ManagerTask task);

        /// <summary>Who decides at a place today: its manager, or the regional manager standing in for one off sick; false when nobody can, and the decisions wait a day.</summary>
        bool Decider(string place, out int skill, out int review, out string who);

        /// <summary>Whether a regional manager over the place is at work today, and whether the executive of a seat is in post: where an escalation may go.</summary>
        bool RegionInPost(string place);

        bool ExecutiveInPost(ExecSeat seat);

        /// <summary>An escalation was raised: the owner's count of new questions, and the log line.</summary>
        void Raised(Escalation e);

        /// <summary>The owner is told of a lease offer: one notice that opens the question (the escalation, where the hierarchy runs, is already raised).</summary>
        void LeaseNotice(string place, LeaseFile l, int day);

        /// <summary>The game's own leave of an employee (Core/StaffLeave); false when it was not done.</summary>
        bool Leave(string employee);

        /// <summary>A decision written down, counted for whoever made it (the place's manager, or the regional manager standing in).</summary>
        Decision Record(string place, ManagerTask task, string subject, string who, int day, int hour, bool mistake);

        /// <summary>The days a request waited, put in the ring of whoever settled it.</summary>
        void Took(string place, string who, int days);

        // ---- the hierarchy's rounds (CompanyDay.HandDownRound, TryOne, HandleRound)

        /// <summary>Whether the place is one the company manages (a question about a place it no longer runs is cleared); the manager of it is Managers.At(place).</summary>
        bool Managed(string place);

        /// <summary>The executive of a seat if they are at work; null for none.</summary>
        ExecutiveFile ExecutiveOf(ExecSeat seat);

        /// <summary>Who holds an escalation at its level, and whether they are in post to try it.</summary>
        bool HolderAt(Escalation e, out string who);

        /// <summary>The holder's own try at an escalation by its kind, under the policy: true when it is settled.</summary>
        bool SettleBy(Escalation e, int level, string who, int day, int hour);

        /// <summary>A try is over and its writes made: settled at a level, or passed up to <paramref name="level"/> (the log line and the count of what came to the owner).</summary>
        void Tried(Escalation e, TryWas was, string who, int level);

        /// <summary>A question left with the owner a day was handed down to the area's executive.</summary>
        void HandedDown(Escalation e);

        // ---- a lease's day (CompanyDay.LeaseShop)

        /// <summary>The market rent a day for these premises from the property feature; 0 where it knows none.</summary>
        int MarketRent(string place, string district, int rent);

        /// <summary>The pressure a rival's buying up puts on a district's landlords, per mille.</summary>
        int Pressure(string district);
    }

    /// <summary>What a manager's lease hour came to.</summary>
    public enum LeaseHour
    {
        None = 0,       // nothing waits
        Accepted,       // inside their limit, accepted
        Declined,       // inside their limit, declined by a slip
        PassedUp,       // past their limit: the owner asked, this once
        Kept            // past their limit and the owner asked already: nothing more is said
    }

    /// <summary>What a lease offer came to on the day it was made.</summary>
    public enum OfferWas
    {
        Made = 0,       // made; nobody answers it yet
        Manager,        // made; the place's manager answers it at their hour
        Asked,          // made; the owner is asked, once
        Renewed         // made after the lease had ended (days were skipped): renewed at the landlord's own figure
    }

    /// <summary>What an escalation's try came to.</summary>
    public enum TryWas
    {
        Lapsed = 0,     // what it asked about lapsed meanwhile
        Settled,        // the holder settled it
        Passed          // the holder could not: it goes up
    }

    /// <summary>
    /// The decisions of the company's day that the director and the run of the checks share: a manager's answer to a raise and to a lease, the let-go of somebody
    /// late, a question handed down to an executive and the try that settles it or passes it up. Every gate, every cash failure, every notice and every record is
    /// here, and the result of a try is the result of the settle it calls. Game-free (EconomyCheck compiles it).
    /// </summary>
    public static class CompanyDay
    {
        // ---------------------------------------------------------------- a manager's answer to a request for a raise

        /// <summary>
        /// A manager's answer to one request for a raise at their hour, inside the policy's pay band, the largest raise and the days between two raises; past
        /// it, to its approver where one is named; a refusal is a one-off bonus where the month's budget covers it and the cash is there. True when the
        /// request was answered (a decision written), false when it was left (the approver has it, or the option is off).
        /// </summary>
        public static bool Raise(ICompanyDay w, string place, ManagerFile m, StaffFile f, int skill, int review, string who, int day, int hour)
        {
            if (!w.On(CompanyPart.Staff) || f.AskDay == 0 || f.Place != place || f.Id == m.Id)
            {
                return false;   // a manager's own request is the player's to answer
            }
            int band = w.Rule(place, PolicyField.PayBand), most = w.Rule(place, PolicyField.RaiseMost), every = w.Rule(place, PolicyField.RaiseEvery);
            // The policy's ceiling: the band, the largest raise and the days between two (PolicyRules); at its start, the band alone.
            int ceiling = PolicyRules.RaiseDue(LastRaise(w.Managers, place, f.Id), day, every) ? PolicyRules.RaiseCeiling(f.Paid, f.Going, band, most) : f.Paid;
            RaiseAnswer asked = ManagerRules.RaiseAnswerFor(f.Paid, f.AskTo, ceiling, false);
            if (asked != RaiseAnswer.Agree && ToApprover(w, place, m, f, false, ceiling, day, hour))
            {
                return false;   // past what the manager may agree: left open for whoever the policy names to approve it
            }
            // A refusal is a one-off bonus instead when the month's budget left covers it (the bonus comes out of that budget).
            int bonus = w.BonusOf(place), left = ManagerRules.Left(w.BudgetOf(m), m.Spent);
            RaiseAnswer fair = ManagerRules.OrBonus(asked, bonus, left, false);
            bool slip = Slips(w.Seed, skill, review, day, place, ManagerTask.Raises, f.Id);
            RaiseAnswer answer = ManagerRules.OrBonus(ManagerRules.RaiseAnswerFor(f.Paid, f.AskTo, ceiling, slip), bonus, left, slip);
            if (answer == RaiseAnswer.Bonus && !w.Pay(bonus, place, f.Name, "a one-off bonus given by the manager in place of a raise"))
            {
                answer = RaiseAnswer.Refuse;   // the cash does not cover it: the request is refused as before
            }
            int askDay = f.AskDay, askTo = f.AskTo, rest = f.AskRest, standing = f.Standing, added = StaffRules.RaiseFor(f, answer);
            StaffRules.Answer(f, answer, day);
            m.Spent += answer == RaiseAnswer.Bonus ? bonus : 0;
            w.Took(place, who, day - askDay);
            Decision d = w.Record(place, ManagerTask.Raises, f.Id, who, day, hour, answer != fair);
            d.A = added;
            d.B = f.Standing - standing;
            d.C = askDay;
            d.D = askTo;
            d.E = rest;
            d.F = (int)answer;
            d.Detail = ManagerStore.Word(f.Name);
            d.G = answer == RaiseAnswer.Bonus ? bonus : 0;
            if (answer == RaiseAnswer.Bonus)
            {
                d.State = DecisionState.Closed;   // the money is paid: nothing to put back
            }
            return true;
        }

        /// <summary>
        /// The gates a manager's decision of a kind at a place passes, in the director's order: the managers' switch, a manager at the place who has the kind of
        /// work for this kind of place, the policy, the place the company's own, and somebody who can decide today. False at the first that does not.
        /// </summary>
        public static bool Deciding(ICompanyDay w, string place, ManagedKind kind, ManagerFile own, ManagerTask task, out int skill, out int review, out string who)
        {
            skill = review = 0;
            who = null;
            return w.ManagersOn && own != null && ManagerRules.Runs(own.Tasks, kind, task) && w.PolicyAllows(place, task) && w.Mine(place) && w.Decider(place, out skill, out review, out who);
        }

        /// <summary>The rule at a place when the hierarchy is off: the manager's own pay band and budget, and every other rule's default.</summary>
        public static int RuleOff(ManagerFile m, PolicyField field)
        {
            return field == PolicyField.PayBand && m != null ? m.WageBand : field == PolicyField.Budget && m != null ? m.Budget : PolicyCatalogue.Def(field).Default;
        }

        /// <summary>
        /// A request for a raise, or a rival's offer, that the band and the policy keep the manager from agreeing: with somebody named to approve exceptions it is
        /// left open for them, an exception escalated at once, and the manager does not answer it. False where nobody is named (or the hierarchy is off), and the
        /// manager answers as before.
        /// </summary>
        public static bool ToApprover(ICompanyDay w, string place, ManagerFile m, StaffFile f, bool offer, int ceiling, int day, int hour)
        {
            HierarchyState h = w.Hierarchy;
            if (h == null || PolicyCatalogue.Value(PolicyField.Approver, w.Rule(place, PolicyField.Approver)) == PolicyCatalogue.ApproveNobody)
            {
                return false;
            }
            if (OpenExceptionFor(h, f.Id))
            {
                return true;   // still with its approver
            }
            Escalation e = Escalate(w, EscalationKind.Exception, place, m, offer ? f.PoachOffer : f.AskTo, ceiling, offer ? 1 : 0, f.Id, f.Name, day, hour);
            if (e != null && m != null)
            {
                ManagerRules.AddSpeed(m.Speeds, day - (offer ? f.PoachDay : f.AskDay));   // the manager answered it by raising it to its approver
            }
            return e != null;
        }

        /// <summary>Whether an exception about somebody is open (still with its approver).</summary>
        public static bool OpenExceptionFor(HierarchyState h, string id)
        {
            for (int i = 0; i < h.Escalations.Count; i++)
            {
                Escalation e = h.Escalations[i];
                if (e.Open && e.Kind == (int)EscalationKind.Exception && string.Equals(e.Subject, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A new escalation, handed to the first holder in post who may settle it under the policy; the owner if nobody may.</summary>
        public static Escalation Escalate(ICompanyDay w, EscalationKind kind, string place, ManagerFile by, int a, int b, int c, string subject, string detail, int day, int hour)
        {
            int level = Holder(w, kind, place, (int)Rung.StoreManager);
            Escalation e = w.Hierarchy.Add(kind, place, level, day);
            e.A = a;
            e.B = b;
            e.C = c;
            e.Subject = HierarchyStore.Clean(subject);
            e.Detail = HierarchyStore.Clean(detail);
            e.By = by != null ? HierarchyStore.Clean(by.Name) : string.Empty;
            HierarchyEntry entry = w.Hierarchy.Log(HierarchyLog.Raised, (int)Rung.StoreManager, e.By, e.Place, day, hour);
            entry.A = e.Id;
            entry.B = e.Kind;
            entry.C = level;
            w.Raised(e);
            return e;
        }

        /// <summary>The first holder above a level who is in post and may settle this kind under the policy; the owner if nobody is.</summary>
        public static int Holder(ICompanyDay w, EscalationKind kind, string place, int from)
        {
            int approver = w.Rule(place, PolicyField.Approver), sources = w.Rule(place, PolicyField.Sources);
            bool region = w.RegionInPost(place), executive = w.ExecutiveInPost(EscalationRules.AreaOf(kind));
            int level = EscalationRules.Next(from, region, executive);
            while (level < EscalationRules.LevelOwner && !EscalationRules.Tries(kind, level, approver, sources))
            {
                level = EscalationRules.Next(level, region, executive);
            }
            return level;
        }

        /// <summary>Whether a manager's mistake is made today: the seeded roll of the day, the place, the task and the subject against their chance.</summary>
        public static bool Slips(int seed, int skill, int review, int day, string place, ManagerTask task, string subject)
        {
            return ManagerRules.Roll(seed, day, place, (int)task, subject) < ManagerRules.MistakeChance(skill, review);
        }

        /// <summary>The day of the last raise given to somebody at a place by a manager (a request or a rival's offer matched), not undone; 0 for none.</summary>
        public static int LastRaise(ManagerState ms, string place, string id)
        {
            for (int i = ms.Decisions.Count - 1; i >= 0; i--)
            {
                Decision d = ms.Decisions[i];
                if ((d.Task == (int)ManagerTask.Raises || d.Task == (int)ManagerTask.Offers) && d.A > 0 && d.State != DecisionState.Undone && d.Subject == id && d.Place == place)
                {
                    return d.Day;
                }
            }
            return 0;
        }

        /// <summary>A decision written into the managers' log and counted for the manager who made it. The one write both the director and the run make.</summary>
        public static Decision Written(ManagerState ms, ManagerFile by, string place, ManagerTask task, string subject, string who, int day, int hour, bool mistake)
        {
            Decision d = ms.Add(day, hour, place, task, ManagerStore.Clean(subject), ManagerStore.Clean(who));
            d.Mistake = mistake;
            if (by != null)
            {
                by.Decided++;
                by.Mistakes += mistake ? 1 : 0;
            }
            return d;
        }

        // ---------------------------------------------------------------- the hierarchy's rounds

        /// <summary>
        /// The owner's questions left a day, for a manager who gives them that way: handed down once to the area's executive (<see cref="HandDown"/>), who tries them in the round
        /// that follows. The one loop the director's day and the run of the checks both make. Returns how many were handed down.
        /// </summary>
        public static int HandDownRound(ICompanyDay w, string you, int day, int hour)
        {
            HierarchyState h = w.Hierarchy;
            int handed = 0;
            for (int i = 0; h != null && i < h.Escalations.Count; i++)
            {
                Escalation e = h.Escalations[i];
                if (!e.Open || e.Level < EscalationRules.LevelOwner || e.Place.Length == 0 || !w.Managed(e.Place))
                {
                    continue;
                }
                var kind = (EscalationKind)e.Kind;
                if (HandDown(h, e, w.Managers.At(e.Place), w.ExecutiveInPost(EscalationRules.AreaOf(kind)), w.Rule(e.Place, PolicyField.Approver), w.Rule(e.Place, PolicyField.Sources), you, day, hour))
                {
                    handed++;
                    w.HandedDown(e);
                }
            }
            return handed;
        }

        /// <summary>
        /// One escalation tried by its holder: settled by the kind's own settle (<see cref="ICompanyDay.SettleBy"/>), found lapsed, or passed up to the next who may settle it; one about
        /// a place no longer managed is cleared. True when it was settled.
        /// </summary>
        public static bool TryOne(ICompanyDay w, Escalation e, int day, int hour)
        {
            HierarchyState h = w.Hierarchy;
            if (!w.Managed(e.Place))
            {
                h.Close(e, EscalationState.Cleared, string.Empty, day);
                return false;
            }
            string who;
            bool inPost = w.HolderAt(e, out who);
            int level = e.Level;
            var kind = (EscalationKind)e.Kind;
            bool done;
            TryWas was = Try(e, inPost, w.Rule(e.Place, PolicyField.Approver), w.Rule(e.Place, PolicyField.Sources), () => w.SettleBy(e, level, who, day, hour), out done);
            if (was == TryWas.Lapsed)
            {
                return done;   // settled, or what it asked about lapsed meanwhile
            }
            if (was == TryWas.Settled)
            {
                Settled(h, e, who, level == EscalationRules.LevelExecutive ? w.ExecutiveOf(EscalationRules.AreaOf(kind)) : null, level, day, hour);
                w.Tried(e, was, who, level);
                return true;
            }
            int next = Holder(w, kind, e.Place, e.Level);
            PassedUp(h, e, next, who, day, hour);
            w.Tried(e, was, who, next);
            return false;
        }

        /// <summary>Every open escalation held below the owner tried once by its holder (<see cref="TryOne"/>); with <paramref name="exceptionsOnly"/> only the exceptions to the pay band. Returns how many were settled.</summary>
        public static int HandleRound(ICompanyDay w, int day, int hour, bool exceptionsOnly)
        {
            HierarchyState h = w.Hierarchy;
            int settled = 0;
            for (int i = 0; h != null && i < h.Escalations.Count; i++)
            {
                Escalation e = h.Escalations[i];
                if (!e.Open || e.Level >= EscalationRules.LevelOwner || (exceptionsOnly && e.Kind != (int)EscalationKind.Exception))
                {
                    continue;
                }
                settled += TryOne(w, e, day, hour) ? 1 : 0;
            }
            return settled;
        }

        // ---------------------------------------------------------------- a lease

        /// <summary>What one rented place's lease came to on a day.</summary>
        public struct LeaseDayWas
        {
            public LeaseFile Lease;             // null: no lease could be made (the leases are at their cap)
            public int TopUp;                   // the rent's rise to pay for yesterday, whole money, as it stood before today's renewal
            public bool Settled;                // the lease came to its end and its answer was applied today
            public bool Silent;                 // ...with nobody having answered: the landlord's own figure
            public int End;                     // the day the lease ended or ends, as it stood when the offer was made
            public bool Offered;                // the landlord's offer was made today
            public OfferWas Offer;              // ...and what it came to
        }

        /// <summary>
        /// One rented place's lease for the day, the one step the director's day and the run of the checks both take: a lease made for premises seen first, the rise to be paid
        /// worked out, a lease at its end settled, or the landlord's offer made when it is due (<see cref="Offer"/>). The caller books the rise and tells what was settled.
        /// </summary>
        public static LeaseDayWas LeaseShop(ICompanyDay w, LeaseState leases, string place, string name, string district, int rent, bool managerRuns, int trend, int monthDays, int day)
        {
            var was = new LeaseDayWas();
            LeaseFile l = leases.Of(place);
            if (l == null)
            {
                if (leases.Leases.Count >= LeaseStore.LeasesMost)
                {
                    return was;
                }
                l = LeaseRules.Begin(w.Seed, place, rent, day, monthDays);   // an old save's, or a new business's: starts today, a seeded term
                leases.Leases.Add(l);
            }
            l.Rent = rent;
            l.Name = name;
            l.District = district;
            was.Lease = l;
            was.TopUp = LeaseRules.TopUp(l);
            was.Silent = LeaseRules.Made(l) && l.Answer == 0 && day >= l.End;
            if (LeaseRules.Settle(l, day, w.Seed, monthDays))
            {
                was.Settled = true;
            }
            else
            {
                was.Silent = false;
                if (LeaseRules.OfferDue(l, day))
                {
                    was.Offered = true;
                    was.End = l.End;
                    was.Offer = Offer(w, place, l, managerRuns, trend, w.Pressure(district), w.MarketRent(place, district, l.Rent), monthDays, day);
                }
            }
            return was;
        }

        /// <summary>
        /// The landlord's offer made today (<see cref="LeaseRules.OfferDue"/>): the rise they ask, the market rent the property feature gives where it knows one.
        /// A lease that ended while days were skipped is renewed at once at their figure; otherwise the place's manager answers it at their hour where they have the
        /// Leases task, and the owner is asked, once, where they do not.
        /// </summary>
        public static OfferWas Offer(ICompanyDay w, string place, LeaseFile l, bool managerRuns, int trend, int pressure, int market, int monthDays, int day)
        {
            LeaseRules.Make(l, LeaseRules.Offer(l, w.Seed, trend, pressure, market), day);   // an offer of no rise is an offer too
            if (day >= l.End && LeaseRules.Settle(l, day, w.Seed, monthDays))
            {
                LeaseClosed(w, place, day);   // nobody had the notice: the landlord's own figure, from the day it ended
                return OfferWas.Renewed;
            }
            if (managerRuns)
            {
                return OfferWas.Manager;   // their hour is this morning's: inside their limit they answer it, past it they raise it to the owner
            }
            Ask(w, place, l, day);
            return OfferWas.Asked;
        }

        /// <summary>
        /// The owner is asked about an offer: once. The lease remembers the day; a second ask of the same offer says nothing, raises nothing and is not counted.
        /// Where the hierarchy runs the question is an escalation (one for a lease at a time); the owner is told in one notice always.
        /// </summary>
        public static bool Ask(ICompanyDay w, string place, LeaseFile l, int day)
        {
            if (l.Told != 0)
            {
                return false;
            }
            l.Told = day;
            HierarchyState h = w.Hierarchy;
            if (h != null && h.OpenOf(EscalationKind.Lease, place) == null)
            {
                Escalate(w, EscalationKind.Lease, place, w.Managers.At(place), LeaseRules.RentAt(l.Rent, l.Running), LeaseRules.RentAt(l.Rent, l.Offer), l.End - day, string.Empty, l.Name, day,
                    w.Hour);
            }
            w.LeaseNotice(place, l, day);
            return true;
        }

        /// <summary>The lease came to its end and its answer was applied: the question it asked closes wherever it was asked.</summary>
        public static void LeaseClosed(ICompanyDay w, string place, int day)
        {
            Escalation e = w.Hierarchy != null ? w.Hierarchy.OpenOf(EscalationKind.Lease, place) : null;
            if (e != null)
            {
                w.Hierarchy.Close(e, EscalationState.Lapsed, string.Empty, day);
            }
        }

        /// <summary>
        /// The owner's answer to a landlord's offer: accept the rent for a new term, or decline and stay month to month. It takes effect on the day the lease ends.
        /// Closes the question wherever it is asked (the escalation, where there is one). False when no offer waits.
        /// </summary>
        public static bool OwnerAnswers(ICompanyDay w, string place, LeaseFile l, bool accept, string you, int day)
        {
            if (l == null || !LeaseRules.Respond(l, day, accept))
            {
                return false;
            }
            Escalation e = w.Hierarchy != null ? w.Hierarchy.OpenOf(EscalationKind.Lease, place) : null;
            if (e != null)
            {
                w.Hierarchy.Close(e, EscalationState.Resolved, you, day);
                HierarchyEntry entry = w.Hierarchy.Log(HierarchyLog.Settled, (int)Rung.Owner, you, place, day, w.Hour);
                entry.A = e.Id;
                entry.B = e.Kind;
            }
            return true;
        }

        /// <summary>The owner asks the landlord to take less, once (LeaseRules.Haggle); the escalation, where there is one, shows the new figure. True when the offer came down.</summary>
        public static bool OwnerHaggles(ICompanyDay w, string place, LeaseFile l, int day, out bool asked)
        {
            asked = l != null && LeaseRules.CanHaggle(l, day);
            if (!asked)
            {
                return false;
            }
            bool less = LeaseRules.Haggle(l, w.Seed, day);
            Escalation e = w.Hierarchy != null ? w.Hierarchy.OpenOf(EscalationKind.Lease, place) : null;
            if (e != null)
            {
                e.B = LeaseRules.RentAt(l.Rent, l.Offer);
            }
            return less;
        }

        /// <summary>
        /// A manager with the Leases task at their hour: an offer inside their rise limit is answered by them (a mistake is a meaner answer: a fair offer
        /// declined, never one past the limit accepted); past it, the owner is asked, once, and the wait that follows is the owner's, not booked to the manager.
        /// </summary>
        public static LeaseHour Lease(ICompanyDay w, string place, ManagerFile m, LeaseFile l, int skill, int review, string who, int day, int hour)
        {
            if (l == null || !LeaseRules.Waiting(l, day))
            {
                return LeaseHour.None;
            }
            int limit = ManagerCatalogue.Step(ManagerCatalogue.LeaseRises, m.LeaseStep);
            bool fair = LeaseRules.Within(l, limit);
            if (!fair)
            {
                if (l.Told != 0)
                {
                    return LeaseHour.Kept;
                }
                w.Took(place, who, day - l.Asked);   // the manager's part is raising it to the owner, once
                Ask(w, place, l, day);
                return LeaseHour.PassedUp;
            }
            bool accept = fair && !Slips(w.Seed, skill, review, day, place, ManagerTask.Leases, string.Empty);
            LeaseRules.Respond(l, day, accept);
            w.Took(place, who, day - l.Asked);
            Decision d = w.Record(place, ManagerTask.Leases, string.Empty, who, day, hour, accept != fair);
            d.A = accept ? 1 : 0;
            d.B = LeaseRules.RentAt(l.Rent, l.Running);
            d.C = LeaseRules.RentAt(l.Rent, l.Offer);
            d.D = LeaseRules.OfferOver(l);
            d.E = l.End;
            return accept ? LeaseHour.Accepted : LeaseHour.Declined;
        }

        // ---------------------------------------------------------------- a hire from the job board

        /// <summary>Whether a manager hires for a place today: the crew they are told to keep is not there, and it is under the twelve a manager hires a place up to.</summary>
        public static bool Wants(ManagerFile m, int staff)
        {
            return m.Keep - staff > 0 && staff < ManagerCatalogue.HireCrewMost;
        }

        /// <summary>The game's own hire of the candidate at a place on the board; false when it was not done. The id and the name of the person hired.</summary>
        public delegate bool Taker(int pick, out string id, out string name);

        /// <summary>What a manager's hiring came to.</summary>
        public enum HireWas
        {
            None = 0,       // nobody on the board is inside the band, the hiring cap, the skill floor and the month's budget
            Failed,         // the pick was made and the game did not hire
            Hired
        }

        /// <summary>
        /// A manager's hire from a place's job board: the pick inside the policy's pay band, hiring cap and skill floor and what the month's budget has left
        /// (PolicyRules.Pick; a slip is a worse pick), the hire the game's own, its cost against the budget, and the decision written down.
        /// </summary>
        public static HireWas Hire(ICompanyDay w, string place, ManagerFile m, int count, int[] wages, int[] goings, int[] skills, Taker take, int skill, int review, string who, int day, int hour,
            out string name)
        {
            name = null;
            int left = ManagerRules.Left(w.BudgetOf(m), m.Spent);
            int band = w.Rule(place, PolicyField.PayBand), cap = w.Rule(place, PolicyField.HirePayCap), floor = w.Rule(place, PolicyField.SkillFloor);
            bool slip = Slips(w.Seed, skill, review, day, place, ManagerTask.Hiring, string.Empty);
            // The policy's pick: the band, the hiring cap and the skill floor; at the policy's start the same pick as ManagerRules.Pick.
            int fair = PolicyRules.Pick(wages, goings, skills, count, band, cap, floor, left, false);
            int pick = PolicyRules.Pick(wages, goings, skills, count, band, cap, floor, left, slip);
            if (pick < 0)
            {
                return HireWas.None;
            }
            string id;
            if (!take(pick, out id, out name))
            {
                return HireWas.Failed;
            }
            int cost = ManagerRules.HireCost(wages[pick]);
            m.Spent += cost;
            Decision d = w.Record(place, ManagerTask.Hiring, id, who, day, hour, pick != fair);
            d.A = wages[pick];
            d.B = skills[pick];
            d.C = cost;
            d.Detail = ManagerStore.Word(name);
            return HireWas.Hired;
        }

        // ---------------------------------------------------------------- somebody late, let go

        /// <summary>
        /// The person at a place most often late three days running (never the manager), if the place is not left with nobody and the hire that fills the place
        /// again is there (<paramref name="replacement"/>, asked only when there is somebody to let go), is let go the game's own way. Written down as a hiring
        /// decision of letting go. The caller takes one off the place's count.
        /// </summary>
        public static bool LetGo(ICompanyDay w, string place, ManagerFile m, int staffNow, Func<bool> replacement, string who, int day, int hour)
        {
            StaffFile worst = null;
            List<StaffFile> staff = w.State.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                StaffFile f = staff[i];
                if (f.Place == place && f.Id != m.Id && f.LeaveDay == 0 && f.LateDays >= ManagerCatalogue.HireLate && (worst == null || f.LateDays > worst.LateDays))
                {
                    worst = f;
                }
            }
            if (worst == null || staffNow <= 1 || !ManagerRules.LetGo(worst.LateDays, replacement()) || !w.Leave(worst.Id))
            {
                return false;
            }
            int days = worst.LateDays;
            StaffRules.LetGo(worst);
            Decision d = w.Record(place, ManagerTask.Hiring, worst.Id, who, day, hour, false);
            d.F = 1;   // let go; a hire has none
            d.D = days;
            d.State = DecisionState.Closed;   // a resignation is the game's own: nothing to put back
            d.Detail = ManagerStore.Word(worst.Name);
            return true;
        }

        // ---------------------------------------------------------------- a question handed down, tried, settled or passed up

        /// <summary>
        /// A question the owner left a day (<see cref="EscalationRules.HandsDown"/>), passed down once to the area's executive. True when it was.
        /// </summary>
        public static bool HandDown(HierarchyState h, Escalation e, ManagerFile m, bool executiveInPost, int approverStep, int sourcesStep, string youWord, int day, int hour)
        {
            if (m == null || !e.Open || e.Level < EscalationRules.LevelOwner || e.Place.Length == 0
                || !EscalationRules.HandsDown((EscalationKind)e.Kind, e.Level, e.Moved, day, e.Down, m.HandDown, executiveInPost, approverStep, sourcesStep))
            {
                return false;
            }
            e.Level = EscalationRules.LevelExecutive;
            e.Down = true;
            e.Moved = day;
            HierarchyEntry entry = h.Log(HierarchyLog.HandedDown, (int)Rung.Owner, youWord, e.Place, day, hour);
            entry.A = e.Id;
            entry.B = e.Level;
            entry.C = e.Kind;
            return true;
        }

        /// <summary>
        /// One escalation held below the owner tried once by its holder: the holder settles it only if they are in post, the policy lets them try this kind at this
        /// level, and <paramref name="settle"/> (the kind's own settle) says it is settled. What the settle returns is what is recorded (<paramref name="done"/>):
        /// nothing is marked settled because the policy allowed a try. Lapsed when the settle closed the escalation itself.
        /// </summary>
        public static TryWas Try(Escalation e, bool holderInPost, int approverStep, int sourcesStep, Func<bool> settle, out bool done)
        {
            done = holderInPost && EscalationRules.Tries((EscalationKind)e.Kind, e.Level, approverStep, sourcesStep) && settle();
            if (!e.Open)
            {
                return TryWas.Lapsed;
            }
            return done ? TryWas.Settled : TryWas.Passed;
        }

        /// <summary>An escalation closed by its holder: resolved, with the log's "settled by" and the executive's count.</summary>
        public static void Settled(HierarchyState h, Escalation e, string who, ExecutiveFile executive, int level, int day, int hour)
        {
            h.Close(e, EscalationState.Resolved, who, day);
            HierarchyEntry entry = h.Log(HierarchyLog.Settled, level, who, e.Place, day, hour);
            entry.A = e.Id;
            entry.B = e.Kind;
            if (executive != null)
            {
                executive.Decided++;
            }
        }

        /// <summary>An escalation its holder could not settle, passed to the next holder (the owner when nobody else may).</summary>
        public static void PassedUp(HierarchyState h, Escalation e, int next, string who, int day, int hour)
        {
            e.Level = next;
            e.Moved = day;
            e.By = HierarchyStore.Clean(who);
            HierarchyEntry entry = h.Log(HierarchyLog.Passed, CompanyCatalogue.Clamp(next, 0, (int)Rung.Owner), who, e.Place, day, hour);
            entry.A = e.Id;
            entry.B = next;
            entry.C = e.Kind;
        }

        /// <summary>
        /// An exception to the pay band (a raise asked, or a rival's offer) agreed by its holder: inside the approver's ceiling for a holder below the owner, any for
        /// the owner. False, and the exception stays open, when the ask is past the ceiling; the exception lapses when the person no longer waits or the option is off.
        /// </summary>
        public static bool Approve(ICompanyDay w, Escalation e, string place, int level, string who, bool owner, int day, int hour, out int agreed)
        {
            agreed = 0;
            StaffFile f = w.State.Employee(e.Subject);
            bool offer = e.C == 1;
            if (f == null || (offer ? f.PoachDay == 0 : f.AskDay == 0) || !w.On(CompanyPart.Staff))
            {
                w.Hierarchy.Close(e, EscalationState.Lapsed, string.Empty, day);
                return false;
            }
            int ceiling = PolicyRules.ExceptionCeiling(f.Going, w.Rule(place, PolicyField.PayBand), w.Rule(place, PolicyField.Approver), w.Rule(place, PolicyField.ExceptionMargin));
            int asked = offer ? f.PoachOffer : f.AskTo;
            if (!owner && asked > ceiling)
            {
                return false;
            }
            int raise = f.Raise;
            if (offer)
            {
                StaffRules.OfferAnswer(f, true, day);
            }
            else
            {
                StaffRules.Answer(f, RaiseAnswer.Agree, day);
            }
            agreed = f.Raise - raise;
            HierarchyEntry entry = w.Hierarchy.Log(HierarchyLog.Approved, CompanyCatalogue.Clamp(level, 0, (int)Rung.Owner), who, place, day, hour);
            entry.A = agreed;
            entry.B = asked;
            entry.C = ceiling;
            entry.Detail = HierarchyStore.Clean(f.Name);
            return true;
        }
    }
}
