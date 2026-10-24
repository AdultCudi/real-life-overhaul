using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a landlord's morning did at one unit, for the director to tell and the books to show.</summary>
    public struct DutyDay
    {
        public bool NewCall;        // a fault was reported today
        public bool Fixed;          // a contractor put one right today
        public bool Lapsed;         // a call went unanswered so long it did damage
        public bool DiyLapsed;      // the player's own job waited past its days and is open again
        public bool Complained;
        public bool Reported;       // ...to the city, which brings its inspector
        public bool Quits;          // gave notice to go
        public bool Stays;          // ...and, better treated since, stays after all
    }

    /// <summary>The city's inspection of a building, as it came out.</summary>
    public struct Inspection
    {
        public bool Done;
        public bool Passed;
        public bool Postponed;      // repair works under way: the inspector comes back after them
        public bool Lifted;         // a pass that ends a bar on new lettings
        public int Score;           // the building's condition, less what old calls take off
        public int Fine;
        public int Strikes;         // failed in a row now
    }

    /// <summary>What a letting agent or a property manager decided this morning at a unit.</summary>
    public struct ManagerMove
    {
        public int Take;            // an applicant to take, by number; 0 for none
        public bool Contractor;     // send a contractor to the open call
        public bool Remind;
        public bool Plan;
        public bool Notice;
        public bool Court;
    }

    /// <summary>
    /// A landlord's duties by rule: how satisfied a tenant is and what follows from it, repair calls, the city's
    /// inspections, reminders and payment plans, and what an agent or a manager does. Game-free; every draw is a
    /// <see cref="Roll"/>, so a save always gives the same history. None of it runs while the option is off.
    /// </summary>
    public static partial class EstateRules
    {
        private const int ForCall = 41, ForCallKind = 42, ForComplain = 43, ForQuit = 44, ForInspect = 45, ForPlan = 46, ForPlanPay = 47;

        /// <summary>The satisfaction a tenant of a unit heads for today: the building, the rent against the market, and how they are treated.</summary>
        public static int HappyTarget(EstateHolding h, int unit, int day, EstateClimate c)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null || !u.Let)
            {
                return 0;
            }
            long target = EstateCatalogue.HappyBase;
            target += (long)(EstateCatalogue.Clamp(h.Cond, 0, 1000) - EstateCatalogue.HappyCondFrom) * EstateCatalogue.HappyCondPer / 1000;
            target += EstateCatalogue.Clamp(h.Level, 0, EstateCatalogue.LevelMost) * EstateCatalogue.HappyLevel;
            int market = UnitRent(h, c);
            if (market > 0)
            {
                long share = (long)u.Rent * 1000 / market;
                target += EstateCatalogue.Clamp((int)((1000 - share) * EstateCatalogue.HappyRentSlope / 1000), EstateCatalogue.HappyRentLeast, EstateCatalogue.HappyRentMost);
            }
            target -= CallWeight(u, day);
            target -= u.NoticeEnd > 0 ? EstateCatalogue.HappyNotice : 0;
            target -= h.OrderDay > 0 ? EstateCatalogue.HappyOrder : 0;
            return EstateCatalogue.Clamp((int)target, 0, 1000);
        }

        /// <summary>What an open call takes off satisfaction: some at once, more for each day it waits past the grace.</summary>
        public static int CallWeight(EstateUnit u, int day)
        {
            if (u == null || u.Call <= 0)
            {
                return 0;
            }
            int late = Math.Max(0, day - u.CallDay - EstateCatalogue.CallGraceDays);
            return Math.Min(EstateCatalogue.HappyCallMost, EstateCatalogue.HappyCall + late * EstateCatalogue.HappyCallPerDay);
        }

        /// <summary>A day's move towards the target: part of the gap, a point at least; a first look sets it there.</summary>
        public static int Drift(int happy, int target)
        {
            if (happy < 0)
            {
                return EstateCatalogue.Clamp(target, 0, 1000);
            }
            int gap = target - happy;
            int step = gap * EstateCatalogue.HappyPull / 1000;
            if (step == 0 && gap != 0)
            {
                step = Math.Sign(gap);
            }
            return EstateCatalogue.Clamp(happy + step, 0, 1000);
        }

        /// <summary>The chance, in thousandths, that a tenant renews at a lease's end, by how satisfied they are.</summary>
        public static int RenewChance(int happy)
        {
            return EstateCatalogue.Clamp(EstateCatalogue.RenewLeast + EstateCatalogue.Clamp(happy, 0, 1000) * EstateCatalogue.RenewPerHappy / 1000,
                EstateCatalogue.RenewLeast, EstateCatalogue.RenewMost);
        }

        /// <summary>A miss chance raised for a tenant who is not satisfied; as it is for one who is, or not yet judged.</summary>
        public static int UnhappyMiss(int chance, int happy)
        {
            if (happy < 0 || happy >= EstateCatalogue.HappyFair)
            {
                return chance;
            }
            long more = (long)(EstateCatalogue.HappyFair - happy) * EstateCatalogue.UnhappyMissPerPoint / 1000;
            return (int)Math.Min(1000, (long)chance * (1000 + more) / 1000);
        }

        /// <summary>The chance, in thousandths, that a let unit reports a fault on a day: more the worse the building.</summary>
        public static int CallChance(int cond)
        {
            return EstateCatalogue.CallBase + (1000 - EstateCatalogue.Clamp(cond, 0, 1000)) / EstateCatalogue.CallPerPoint;
        }

        /// <summary>A contractor's quote for a call: a share of the unit's part of the building's value, by the kind of job and prices.</summary>
        public static int CallCost(EstateHolding h, int kind, int index)
        {
            long unitValue = (long)Math.Max(0, h.Base) / Math.Max(1, h.Units.Count);
            long cost = unitValue * EstateCatalogue.CallShare / 1000 * EstateCatalogue.CallKindCost[EstateCatalogue.Clamp(kind, 0, EstateCatalogue.CallKinds)] / 1000;
            int least = EstateCatalogue.Share(EstateCatalogue.CallLeast, EstateCatalogue.Clamp(index, 500, 4000));
            return (int)Math.Max(least, Math.Min(int.MaxValue / 4, cost * EstateCatalogue.Clamp(index, 500, 4000) / 1000));
        }

        /// <summary>What the materials cost when the player does a job himself.</summary>
        public static int DiyCost(int quote)
        {
            return Math.Max(1, EstateCatalogue.Share(quote, EstateCatalogue.DiyShare));
        }

        /// <summary>A fault is reported at a unit: its kind and the contractor's quote. False if one is open, or nobody is in.</summary>
        public static bool RaiseCall(EstateHolding h, int unit, int seed, int day, EstateClimate c)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null || !u.Let || u.Call > 0)
            {
                return false;
            }
            u.Call = Between(Roll(seed, day, ForCallKind, UnitKey(h, unit)), 1, EstateCatalogue.CallKinds);
            u.CallDay = day;
            u.CallCost = CallCost(h, u.Call, c.Index);
            u.CallAnswer = (int)CallAnswer.Waiting;
            u.CallDue = 0;
            return true;
        }

        /// <summary>A contractor is sent: paid for now, fixed on the next morning. False if there is nothing to send them to.</summary>
        public static bool SendContractor(EstateUnit u, int day)
        {
            if (u == null || u.Call <= 0 || u.CallAnswer == (int)CallAnswer.Contractor)
            {
                return false;
            }
            u.CallAnswer = (int)CallAnswer.Contractor;
            u.CallDue = day + EstateCatalogue.ContractorDays;
            return true;
        }

        /// <summary>The player will do the job: it waits for them for some days, then is open again.</summary>
        public static bool DoItYourself(EstateUnit u, int day)
        {
            if (u == null || u.Call <= 0 || u.CallAnswer != (int)CallAnswer.Waiting)
            {
                return false;
            }
            u.CallAnswer = (int)CallAnswer.Yourself;
            u.CallDue = day + EstateCatalogue.DiyDays;
            return true;
        }

        /// <summary>The call is put right, by whoever: the tenant is glad, gladder if it was quick. Returns what satisfaction gained.</summary>
        public static int Fix(EstateUnit u, int day)
        {
            if (u == null || u.Call <= 0)
            {
                return 0;
            }
            int gain = day - u.CallDay <= EstateCatalogue.CallGraceDays ? EstateCatalogue.HappyFixedQuick : EstateCatalogue.HappyFixed;
            u.CallClosed();
            if (u.Happy >= 0)
            {
                u.Happy = EstateCatalogue.Clamp(u.Happy + gain, 0, 1000);
            }
            return gain;
        }

        /// <summary>
        /// One unit's landlord's morning, after its tenancy's day: a contractor's job done, a call raised or left too long,
        /// satisfaction, a complaint and a tenant who has had enough. The building's condition may drop here.
        /// </summary>
        public static DutyDay Duty(EstateHolding h, int unit, int seed, int day, EstateClimate c)
        {
            var d = new DutyDay();
            EstateUnit u = h.Unit(unit);
            if (u == null || !u.Let)
            {
                return d;
            }
            int key = UnitKey(h, unit);
            if (u.Call > 0 && u.CallAnswer == (int)CallAnswer.Contractor && day >= u.CallDue)
            {
                Fix(u, day);
                d.Fixed = true;
            }
            else if (u.Call > 0 && u.CallAnswer == (int)CallAnswer.Yourself && day > u.CallDue)
            {
                u.CallAnswer = (int)CallAnswer.Waiting;
                u.CallDue = 0;
                d.DiyLapsed = true;
            }
            if (u.Call == 0 && !h.Busy && Roll(seed, day, ForCall, key) < CallChance(h.Cond))
            {
                d.NewCall = RaiseCall(h, unit, seed, day, c);
            }
            bool lapsed = u.Call > 0 && u.CallAnswer == (int)CallAnswer.Waiting && day - u.CallDay == EstateCatalogue.CallLapseDays;
            if (lapsed)
            {
                h.Cond = Math.Max(0, h.Cond - EstateCatalogue.CallLapseLoss);
                d.Lapsed = true;
            }
            u.Happy = Drift(u.Happy, HappyTarget(h, unit, day, c));
            if (lapsed || (u.Happy < EstateCatalogue.ComplainBelow && Roll(seed, day, ForComplain, key) < EstateCatalogue.ComplainChance))
            {
                u.Moans++;
                d.Complained = true;
                if (u.Moans >= EstateCatalogue.ComplaintsToCity && u.Call > 0 && day - u.CallDay >= EstateCatalogue.CityCallDays
                    && (h.InspectDay == 0 || h.InspectDay > day + EstateCatalogue.CityDays))
                {
                    h.InspectDay = day + EstateCatalogue.CityDays;
                    d.Reported = true;
                }
            }
            if (u.QuitDay == 0 && u.Arrears == 0 && u.CourtDay == 0 && u.Happy < EstateCatalogue.QuitBelow && Roll(seed, day, ForQuit, key) < EstateCatalogue.QuitChance)
            {
                u.QuitDay = day + EstateCatalogue.QuitDays;
                d.Quits = true;
            }
            else if (u.QuitDay > 0 && u.Happy >= EstateCatalogue.QuitBelow + EstateCatalogue.StayAbove)
            {
                u.QuitDay = 0;
                d.Stays = true;
            }
            return d;
        }

        /// <summary>What a tenant minds most today, for a complaint: 0 the rent, 1 the building, 2 a repair, 3 notice, 4 the city's order.</summary>
        public static int Grievance(EstateHolding h, int unit, int day, EstateClimate c)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null)
            {
                return 1;
            }
            int market = UnitRent(h, c);
            int rent = market > 0 ? (int)Math.Max(0L, ((long)u.Rent * 1000 / market - 1000) * EstateCatalogue.HappyRentSlope / 1000) : 0;
            int building = Math.Max(0, (EstateCatalogue.HappyCondFrom - h.Cond) * EstateCatalogue.HappyCondPer / 1000);
            int call = CallWeight(u, day), notice = u.NoticeEnd > 0 ? EstateCatalogue.HappyNotice : 0, order = h.OrderDay > 0 ? EstateCatalogue.HappyOrder : 0;
            int[] weights = { rent, building, call, notice, order };
            int what = 1;
            for (int i = 0, most = 0; i < weights.Length; i++)
            {
                if (weights[i] > most)
                {
                    most = weights[i];
                    what = i;
                }
            }
            return what;
        }

        /// <summary>The average satisfaction of a building's tenants; -1 with nobody judged yet.</summary>
        public static int BuildingHappy(EstateHolding h)
        {
            int sum = 0, count = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                if (h.Units[i].Let && h.Units[i].Happy >= 0)
                {
                    sum += h.Units[i].Happy;
                    count++;
                }
            }
            return count > 0 ? sum / count : -1;
        }

        /// <summary>How many of a building's units have a call open.</summary>
        public static int OpenCalls(EstateHolding h)
        {
            int count = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                count += h.Units[i].Let && h.Units[i].Call > 0 ? 1 : 0;
            }
            return count;
        }

        /// <summary>How many of a building's calls nobody has been sent to.</summary>
        public static int CallsWaiting(EstateHolding h)
        {
            int count = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                count += h.Units[i].Let && h.Units[i].Call > 0 && h.Units[i].CallAnswer == (int)CallAnswer.Waiting ? 1 : 0;
            }
            return count;
        }

        /// <summary>True while two failed inspections in a row keep new tenants out.</summary>
        public static bool Barred(EstateHolding h) => h.Strikes >= EstateCatalogue.StrikesBarred;

        /// <summary>The first inspection of a let building that has none due: drawn, so a city of buildings is not inspected on one day.</summary>
        public static bool Schedule(EstateHolding h, int seed, int day)
        {
            if (h.InspectDay > 0 || !h.Managed || !h.AnyLet)
            {
                return false;
            }
            h.InspectDay = day + Between(Roll(seed, day, ForInspect, Hash(h.Place)), EstateCatalogue.InspectFirstLeast, EstateCatalogue.InspectEvery);
            return true;
        }

        /// <summary>What the inspector counts: the building's condition, less a share for each call left past its days.</summary>
        public static int InspectScore(EstateHolding h, int day)
        {
            int score = h.Cond;
            for (int i = 0; i < h.Units.Count; i++)
            {
                EstateUnit u = h.Units[i];
                if (u.Let && u.Call > 0 && day - u.CallDay > EstateCatalogue.InspectCallOld)
                {
                    score -= EstateCatalogue.InspectCallPenalty;
                }
            }
            return Math.Max(0, score);
        }

        public static int InspectFine(int strikes, int index)
        {
            return EstateCatalogue.Share(strikes >= EstateCatalogue.StrikesBarred ? EstateCatalogue.FineSecond : EstateCatalogue.FineFirst, EstateCatalogue.Clamp(index, 500, 4000));
        }

        /// <summary>The city's inspection, if its day has come: a pass, or a fine and an order to put it right by a day.</summary>
        public static Inspection Inspect(EstateHolding h, int day, EstateClimate c, bool whatever = false)
        {
            var r = new Inspection();
            if (h.InspectDay <= 0 || (!whatever && day < h.InspectDay))
            {
                return r;
            }
            if (h.Busy && h.Work == (int)EstateWork.Repair)
            {
                h.InspectDay = h.WorkEnd + 1;   // the builders are in: the inspector looks at their work
                r.Postponed = true;
                return r;
            }
            r.Done = true;
            r.Score = InspectScore(h, day);
            r.Passed = r.Score >= EstateCatalogue.InspectPass;
            if (r.Passed)
            {
                r.Lifted = Barred(h);
                h.Strikes = 0;
                h.OrderDay = 0;
                h.InspectDay = h.AnyLet ? day + EstateCatalogue.InspectEvery : 0;
            }
            else
            {
                h.Strikes = Math.Min(9, h.Strikes + 1);
                r.Fine = InspectFine(h.Strikes, c.Index);
                h.OrderDay = day + (h.Strikes >= EstateCatalogue.StrikesBarred ? EstateCatalogue.ReinspectDays : EstateCatalogue.OrderDays);
                h.InspectDay = h.OrderDay;
            }
            r.Strikes = h.Strikes;
            return r;
        }

        /// <summary>A reminder of the arrears: the tenant is likelier to pay them, and a little put out. Once in a spell.</summary>
        public static bool Remind(EstateUnit u)
        {
            if (u == null || !u.Let || u.Arrears <= 0 || u.Reminded || u.OnPlan || u.CourtDay > 0)
            {
                return false;
            }
            u.Reminded = true;
            if (u.Happy >= 0)
            {
                u.Happy = Math.Max(0, u.Happy - EstateCatalogue.HappyReminder);
            }
            return true;
        }

        /// <summary>The chance, in thousandths, that arrears are paid in full today, the notice and a reminder counted.</summary>
        public static int PayUpChance(EstateUnit u, bool duties)
        {
            int chance = CatchUpChance(u.Record);
            if (u.NoticeEnd > 0)
            {
                chance = Math.Max(EstateCatalogue.NoticePaysUp, chance);
            }
            return duties && u.Reminded ? Math.Min(1000, chance + EstateCatalogue.ReminderLift) : chance;
        }

        public static int PlanDays(int arrearsDays)
        {
            return EstateCatalogue.Clamp(arrearsDays * 2, EstateCatalogue.PlanDaysLeast, EstateCatalogue.PlanDaysMost);
        }

        public static int PlanAcceptChance(int record)
        {
            return EstateCatalogue.Clamp(EstateCatalogue.PlanAcceptBase + EstateCatalogue.Clamp(record, 0, 100) * EstateCatalogue.PlanAcceptPerPoint, 0, 1000);
        }

        public static bool MayOfferPlan(EstateUnit u)
        {
            return u != null && u.Let && u.Arrears > 0 && !u.OnPlan && !u.PlanAsked && u.NoticeEnd == 0 && u.CourtDay == 0;
        }

        /// <summary>
        /// A payment plan offered: the tenant takes it or not, by their record. Taken, the rent is paid again from tonight
        /// and the arrears come in instalments. Either way it is not offered again in this spell. True if taken.
        /// </summary>
        public static bool OfferPlan(EstateHolding h, int unit, int seed, int day)
        {
            EstateUnit u = h.Unit(unit);
            if (!MayOfferPlan(u))
            {
                return false;
            }
            u.PlanAsked = true;
            if (Roll(seed, day, ForPlan, UnitKey(h, unit)) >= PlanAcceptChance(u.Record))
            {
                return false;
            }
            int days = PlanDays(u.ArrearsDays);
            u.OnPlan = true;
            u.PlanEach = Math.Max(1, (u.Arrears + days - 1) / days);
            return true;
        }

        /// <summary>A day of a plan: an instalment with the rent, or a missed day that ends it. True if it was paid.</summary>
        public static bool PlanDay(EstateHolding h, int unit, int seed, int day, EstateClimate c, ref TenancyDay d)
        {
            EstateUnit u = h.Units[unit];
            int miss = UnhappyMiss((int)((long)MissChance(u.Record, c.Cycle) * EstateCatalogue.PlanMissShare / 1000), u.Happy);
            if (Roll(seed, day, ForPlanPay, UnitKey(h, unit)) < miss)
            {
                u.OnPlan = false;
                u.PlanEach = 0;
                u.Arrears += u.Rent;
                u.ArrearsDays++;
                d.Missed = true;
                d.PlanBroken = true;
                return false;
            }
            int pay = Math.Min(u.PlanEach, u.Arrears);
            d.Lump += pay;
            u.Arrears -= pay;
            if (u.Arrears <= 0)
            {
                u.Cleared();
                d.PlanDone = true;
                if (u.Happy >= 0)
                {
                    u.Happy = Math.Min(1000, u.Happy + EstateCatalogue.HappyPlanDone);
                }
            }
            return true;
        }

        /// <summary>The option goes off: plans end with their arrears owed, a booked contractor finishes, and nothing else is pending.</summary>
        public static void DutiesOff(EstateHolding h, int day)
        {
            for (int i = 0; i < h.Units.Count; i++)
            {
                EstateUnit u = h.Units[i];
                if (u.CallAnswer == (int)CallAnswer.Contractor)
                {
                    Fix(u, day);   // paid for already
                }
                u.CallClosed();
                u.OnPlan = u.Reminded = u.PlanAsked = false;
                u.PlanEach = u.QuitDay = 0;
            }
            h.InspectDay = h.OrderDay = h.Strikes = 0;
        }

        /// <summary>How much more often applicants come with an agent or a manager marketing the building.</summary>
        public static int ApplicantLift(EstateHolding h)
        {
            return h.Manager != (int)EstateManager.Self ? EstateCatalogue.ManagedApplicants : 1000;
        }

        /// <summary>A letting agent's fee for a letting at a rent.</summary>
        public static int AgentFee(int rent) => Math.Max(0, rent) * EstateCatalogue.AgentLetDays;

        /// <summary>A property manager's cut of a night's rent.</summary>
        public static int ManagerFee(int rent) => rent <= 0 ? 0 : EstateCatalogue.Share(rent, EstateCatalogue.ManagerShare);

        /// <summary>The applicant an agent or a manager takes for an empty unit today; 0 for none yet.</summary>
        public static int ManagerPick(EstateHolding h, int unit, int day, EstateClimate c)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null || u.Let || !u.ToLet || h.Busy || h.SaleDay > 0 || h.Cond < EstateCatalogue.ConditionUnfit)
            {
                return 0;
            }
            int asking = EstateCatalogue.Share(UnitRent(h, c), u.Ask);
            RentApplicant best = null;
            long bestScore = -1;
            for (int i = 0; i < u.Applicants.Count; i++)
            {
                RentApplicant a = u.Applicants[i];
                if (a.Record < EstateCatalogue.ManagerRecordLeast)
                {
                    continue;
                }
                long score = (long)a.Offer * (a.Record + 40);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }
            if (best == null)
            {
                return 0;
            }
            bool enough = (long)best.Offer * 1000 >= (long)asking * EstateCatalogue.ManagerOfferLeast;
            return enough || best.Lapse <= day ? best.Id : 0;
        }

        /// <summary>What a property manager does at a let unit this morning: a contractor, a reminder, a plan, notice, the court.</summary>
        public static ManagerMove ManagerDay(EstateHolding h, int unit, int day)
        {
            var m = new ManagerMove();
            EstateUnit u = h.Unit(unit);
            if (h.Manager != (int)EstateManager.Manager || u == null || !u.Let)
            {
                return m;
            }
            m.Contractor = u.Call > 0 && u.CallAnswer == (int)CallAnswer.Waiting;
            if (u.Arrears > 0 && !u.OnPlan)
            {
                m.Remind = !u.Reminded && u.CourtDay == 0;
                m.Plan = !m.Remind && MayOfferPlan(u) && u.Record >= EstateCatalogue.ManagerRecordLeast;
            }
            m.Notice = !m.Remind && !m.Plan && MayServeNotice(h, unit, day) && (u.PlanAsked || u.Record < EstateCatalogue.ManagerRecordLeast || day > u.Until);
            m.Court = MayGoToCourt(h, unit, day);
            return m;
        }

        /// <summary>Whether a manager starts the repair works an improvement order needs: near its day, with the building below the mark.</summary>
        public static bool ManagerRepairs(EstateHolding h, int day)
        {
            return h.Manager == (int)EstateManager.Manager && h.OrderDay > 0 && !h.Busy && h.SaleDay == 0 && h.OrderDay - day <= EstateCatalogue.ManagerRepairDays
                   && h.Cond < EstateCatalogue.InspectPass + EstateCatalogue.InspectCallPenalty;
        }
    }
}
