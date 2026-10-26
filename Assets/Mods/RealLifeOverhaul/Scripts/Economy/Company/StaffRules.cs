using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How a request for a raise is answered.</summary>
    public enum RaiseAnswer
    {
        Agree = 0,
        Less = 1,
        Refuse = 2
    }

    /// <summary>How a strike ends.</summary>
    public enum StrikeAnswer
    {
        Raise = 0,      // everyone at the business is brought up to the going rate
        Payment = 1,    // a one-off payment to each
        SitOut = 2      // it runs its length
    }

    /// <summary>What a day brought for one employee.</summary>
    public enum StaffNote
    {
        None = 0,
        Asked = 1,
        AskLapsed = 2,      // unanswered: taken as refused
        AskWithdrawn = 3    // no longer underpaid: the request is dropped
    }

    /// <summary>What a raise for several picked employees comes to (<see cref="StaffRules.Plan"/>); pay in hundredths.</summary>
    public struct PickedPlan
    {
        public int Picked;          // employees picked in the game's list
        public int Raised;          // ...given the raise
        public int Rested;          // ...left out: a raise of theirs is in its rest
        public int Waiting;         // ...left out: a rival's offer to answer first, or a departure set
        public int AtRate;          // ...left out: up to the going rate, and paid it already
        public int Unknown;         // ...left out: not at one of the player's businesses
        public int Raise;           // what the raise adds an hour in all
        public long Before, After;          // the picked employees' pay an hour in all
        public long DayBefore, DayAfter;    // ...and for a working day of each

        public int Left => Rested + Waiting + AtRate + Unknown;
    }

    /// <summary>
    /// Staff as an economy: the going rate, raises, a rival's offer, a strike, overtime and sick pay. Game-free.
    /// The game's wage is never changed: what is agreed here is <see cref="StaffFile.Raise"/>, paid on top of it.
    /// </summary>
    public static class StaffRules
    {
        private const int PurposePoach = 61, PurposeOvertime = 62, PurposeWho = 63;

        /// <summary>
        /// The going rate against the game's wage formula, thousandths, by the economy and the district's income.
        /// At today's prices, as <see cref="StaffFile.Wage"/> is, so that prices alone underpay nobody.
        /// </summary>
        public static int Market(int index, CycleState cycle, int income)
        {
            int state = CompanyCatalogue.Clamp((int)cycle, 0, CompanyCatalogue.WageCycle.Length - 1);
            int market = 1000 + CompanyCatalogue.WageCycle[state] + (income - CompanyCatalogue.IncomeMiddle) * CompanyCatalogue.WageIncomePerPoint;
            market = CompanyCatalogue.Clamp(market, CompanyCatalogue.WageMarketLeast, CompanyCatalogue.WageMarketMost);
            return (int)((long)market * CompanyCatalogue.Clamp(index, 500, 4000) / CompanyCatalogue.IndexStart);
        }

        /// <summary>The going hourly rate, hundredths, from the game's wage formula for the skill.</summary>
        public static int Going(int formula, int market)
        {
            return CompanyCatalogue.Share(Math.Max(0, formula), market);
        }

        /// <summary>How far under the going rate somebody is paid, thousandths; negative when paid over it.</summary>
        public static int Gap(int going, int paid)
        {
            return going <= 0 ? 0 : (int)((long)(going - paid) * 1000 / going);
        }

        public static bool Underpaid(StaffFile f, int level)
        {
            return f.Going > 0 && Gap(f.Going, f.Paid) >= CompanyCatalogue.RaiseGap[CompanyCatalogue.Level(level)];
        }

        /// <summary>Pay for some hours at an hourly rate, both in hundredths: whole money, rounded.</summary>
        public static int Pay(int rate, int hours)
        {
            return (int)(((long)Math.Max(0, rate) * Math.Max(0, hours) + 5000) / 10000);
        }

        public static StaffNote Day(StaffFile f, int level, int day)
        {
            return Day(f, level, day, 0);
        }

        /// <summary>
        /// One employee's day: standing drifts down while underpaid, or up to a calm level (raised by the workplace's
        /// comfort, 0 to 10) while paid fairly; an unanswered request lapses; somebody underpaid long enough asks.
        /// </summary>
        public static StaffNote Day(StaffFile f, int level, int day, int comfort)
        {
            int calm = CompanyCatalogue.StandingCalm + CompanyCatalogue.Clamp(comfort, 0, 10);
            level = CompanyCatalogue.Level(level);
            bool under = Underpaid(f, level);
            f.UnderDays = under ? f.UnderDays + 1 : 0;
            if (under)
            {
                MoveStanding(f, CompanyCatalogue.StandUnderDay);
            }
            else if (f.Standing < calm)
            {
                MoveStanding(f, CompanyCatalogue.StandFairDay);
            }
            if (f.AskDay > 0)
            {
                if (day > f.AskDay + CompanyCatalogue.RaiseAnswerDays)
                {
                    Answer(f, RaiseAnswer.Refuse, day);
                    return StaffNote.AskLapsed;
                }
                if (!under)
                {
                    f.AskDay = f.AskTo = 0;
                    return StaffNote.AskWithdrawn;
                }
                f.AskTo = f.Going;   // the going rate may have moved since they asked
                return StaffNote.None;
            }
            if (under && f.UnderDays >= CompanyCatalogue.RaiseDays[level] && day >= f.AskRest && f.PoachDay == 0 && f.LeaveDay == 0)
            {
                f.AskDay = day;
                f.AskTo = f.Going;
                return StaffNote.Asked;
            }
            return StaffNote.None;
        }

        /// <summary>Whether a raise to the going rate may be given unasked: as often as they would ask.</summary>
        public static bool CanGive(StaffFile f, int day)
        {
            return f.AskDay == 0 && f.PoachDay == 0 && f.LeaveDay == 0 && f.Going > f.Paid && day >= f.AskRest;
        }

        /// <summary>The raise an answer gives, hundredths an hour.</summary>
        public static int RaiseFor(StaffFile f, RaiseAnswer answer)
        {
            int asked = Math.Max(0, f.AskTo - f.Paid);
            return answer == RaiseAnswer.Agree ? asked : answer == RaiseAnswer.Less ? asked * CompanyCatalogue.RaiseLessShare / 100 : 0;
        }

        public static void Answer(StaffFile f, RaiseAnswer answer, int day)
        {
            if (f.AskDay == 0)
            {
                return;
            }
            f.Raise += RaiseFor(f, answer);
            MoveStanding(f, answer == RaiseAnswer.Agree ? CompanyCatalogue.StandAgree
                : answer == RaiseAnswer.Less ? CompanyCatalogue.StandLess : CompanyCatalogue.StandRefuse);
            f.AskDay = f.AskTo = 0;
            f.UnderDays = 0;
            f.AskRest = day + CompanyCatalogue.RaiseRestDays;
        }

        /// <summary>The choices of a raise for several picked employees: up to the going rate, then the shares, then the amounts.</summary>
        public static int PickedChoices => 1 + CompanyCatalogue.PickedRaisePercents.Length + CompanyCatalogue.PickedRaiseAmounts.Length;

        /// <summary>A choice's share of the pay, per cent; 0 for a choice that is not a share.</summary>
        public static int PickedPercent(int choice)
        {
            int i = choice - 1;
            return i >= 0 && i < CompanyCatalogue.PickedRaisePercents.Length ? CompanyCatalogue.PickedRaisePercents[i] : 0;
        }

        /// <summary>A choice's amount, hundredths an hour; 0 for a choice that is not an amount.</summary>
        public static int PickedAmount(int choice)
        {
            int i = choice - 1 - CompanyCatalogue.PickedRaisePercents.Length;
            return i >= 0 && i < CompanyCatalogue.PickedRaiseAmounts.Length ? CompanyCatalogue.PickedRaiseAmounts[i] : 0;
        }

        /// <summary>
        /// The raise a choice gives one employee, hundredths an hour: up to the going rate (nothing for somebody at it or over),
        /// a share of what they are paid with the agreed raise, at least a cent, or the amount. 0 for a choice there is not.
        /// </summary>
        public static int PickedRaise(StaffFile f, int choice)
        {
            if (choice == 0)
            {
                return Math.Max(0, f.Going - f.Paid);
            }
            int percent = PickedPercent(choice);
            if (percent > 0)
            {
                return Math.Max(1, (int)(((long)Math.Max(0, f.Paid) * percent + 50) / 100));
            }
            return PickedAmount(choice);
        }

        /// <summary>
        /// Whether a picked employee may be given a raise today, by the rule of one raise: a request of theirs waiting is answered
        /// by it; otherwise one raise in the rest's time. A rival's offer to answer, or a departure set, is answered on its own.
        /// </summary>
        public static bool CanPick(StaffFile f, int day)
        {
            return f.PoachDay == 0 && f.LeaveDay == 0 && (f.AskDay > 0 || day >= f.AskRest);
        }

        /// <summary>
        /// A raise given to one of several picked employees, as <see cref="Answer"/> gives one: the agreed raise grows, the standing
        /// moves as for an agreed request (as for one offered less, when it falls short of a request of theirs), a waiting request
        /// is settled and the rest begins.
        /// </summary>
        public static void GivePicked(StaffFile f, int raise, int day)
        {
            if (raise <= 0)
            {
                return;
            }
            bool shortOfAsk = f.AskDay > 0 && f.Paid + raise < f.AskTo;
            f.Raise += raise;
            MoveStanding(f, shortOfAsk ? CompanyCatalogue.StandLess : CompanyCatalogue.StandAgree);
            f.AskDay = f.AskTo = 0;
            f.UnderDays = 0;
            f.AskRest = day + CompanyCatalogue.RaiseRestDays;
        }

        /// <summary>
        /// What a raise for several picked employees comes to before it is given: who it reaches, who is left out and why, and
        /// their pay an hour and a working day in all, before and after, hundredths. A null file is somebody the company does
        /// not know (not at one of the player's businesses).
        /// </summary>
        public static PickedPlan Plan(IList<StaffFile> picked, int choice, int day)
        {
            var plan = new PickedPlan();
            for (int i = 0; picked != null && i < picked.Count; i++)
            {
                StaffFile f = picked[i];
                plan.Picked++;
                if (f == null)
                {
                    plan.Unknown++;
                    continue;
                }
                int raise = CanPick(f, day) ? PickedRaise(f, choice) : 0;
                if (f.PoachDay > 0 || f.LeaveDay > 0)
                {
                    plan.Waiting++;
                }
                else if (!CanPick(f, day))
                {
                    plan.Rested++;
                }
                else if (raise <= 0)
                {
                    plan.AtRate++;
                }
                else
                {
                    plan.Raised++;
                    plan.Raise += raise;
                }
                long hours = Math.Max(0, f.DayHours);
                plan.Before += f.Paid;
                plan.After += f.Paid + Math.Max(0, raise);
                plan.DayBefore += (long)f.Paid * hours / 100;
                plan.DayAfter += (long)(f.Paid + Math.Max(0, raise)) * hours / 100;
            }
            return plan;
        }

        /// <summary>
        /// Whether one of the company's own rivals would want somebody today: skilled, underpaid enough, nothing waiting, past
        /// the rest after an answer, and no other rival's offer to them open or departure in hand (<see cref="Free"/>).
        /// </summary>
        public static bool Wanted(StaffFile f, int level, int day)
        {
            return CompanyCatalogue.PoachChance[CompanyCatalogue.Level(level)] > 0 && f.Skill >= CompanyCatalogue.PoachSkill && f.Going > 0
                   && Gap(f.Going, f.Paid) >= CompanyCatalogue.PoachGap && f.PoachDay == 0 && f.LeaveDay == 0 && f.AskDay == 0
                   && day >= f.PoachRest && Free(f);
        }

        /// <summary>
        /// Whether a rival tycoon's holding may make somebody an offer today: as the company's own rivals, never on Relaxed,
        /// nobody with something waiting and nobody within the rest after an answer. It wants the best, underpaid or not.
        /// </summary>
        public static bool RivalMay(StaffFile f, int level, int day)
        {
            return CompanyCatalogue.PoachChance[CompanyCatalogue.Level(level)] > 0 && f.Going > 0 && f.PoachDay == 0 && f.LeaveDay == 0 && f.AskDay == 0
                   && day >= f.PoachRest && Free(f);
        }

        /// <summary>
        /// One rival's offer to an employee at a time: the company makes none while the game's own offer to them is open
        /// (EmployeeInstance.PoachByRival) or the tech sector has its own offer or a departure in hand for them.
        /// </summary>
        public static bool Free(StaffFile f)
        {
            return !f.GameOffer && !f.Held;
        }

        /// <summary>
        /// Whether the company's own offer to somebody gives way: the game's own rival offer to them has opened (its special
        /// rivals choose whom they want, and the mod cannot stop them) while the company's offer waits or the notice after
        /// it runs. The game's offer, in their messages, then decides alone whether they stay.
        /// </summary>
        public static bool GivesWay(StaffFile f)
        {
            return f.GameOffer && (f.PoachDay > 0 || f.LeaveDay > 0);
        }

        /// <summary>The company's offer and the notice after it called off, as if never made; no new offer while the rest after an answer runs.</summary>
        public static void GiveWay(StaffFile f, int day)
        {
            f.PoachDay = f.PoachOffer = 0;
            f.LeaveDay = 0;
            f.PoachRest = Math.Max(f.PoachRest, day + CompanyCatalogue.PoachRestDays);
        }

        public static bool Approach(int seed, int age, int level)
        {
            return Approach(seed, age, level, 0);
        }

        /// <summary>
        /// Whether a rival approaches today, the same for a seed and a day. The player's esteem as an employer
        /// (-10 to 10) makes it rarer.
        /// </summary>
        public static bool Approach(int seed, int age, int level, int esteem)
        {
            int chance = CompanyCatalogue.PoachChance[CompanyCatalogue.Level(level)];
            esteem = CompanyCatalogue.Clamp(esteem, -CompanyCatalogue.EsteemMost, CompanyCatalogue.EsteemMost);
            return CityModel.Draw(seed, PurposePoach, age, 1000) < chance * (1000 - esteem * CompanyCatalogue.EsteemPoachStep) / 1000;
        }

        /// <summary>Which of so many who fit is approached.</summary>
        public static int Who(int seed, int age, int count)
        {
            return CityModel.Draw(seed, PurposeWho, age, count);
        }

        public static void Offer(StaffFile f, string rival, int day)
        {
            f.PoachDay = day;
            f.PoachOffer = CompanyCatalogue.Share(f.Going, 1000 + CompanyCatalogue.PoachPremium);
            f.Rival = rival ?? string.Empty;
        }

        public static bool OfferLapsed(StaffFile f, int day)
        {
            return f.PoachDay > 0 && day > f.PoachDay + CompanyCatalogue.PoachAnswerDays;
        }

        /// <summary>Matched, they are paid what the rival offered; not, they work their notice and go.</summary>
        public static void OfferAnswer(StaffFile f, bool match, int day)
        {
            if (f.PoachDay == 0)
            {
                return;
            }
            if (match)
            {
                f.Raise += Math.Max(0, f.PoachOffer - f.Paid);
                MoveStanding(f, CompanyCatalogue.StandMatch);
            }
            else
            {
                f.LeaveDay = day + CompanyCatalogue.PoachNoticeDays;
            }
            f.PoachDay = f.PoachOffer = 0;
            f.PoachRest = day + CompanyCatalogue.PoachRestDays;
        }

        public static bool StandsLow(StaffFile f)
        {
            return f.Standing <= CompanyCatalogue.StrikeStanding;
        }

        /// <summary>Enough of a business's staff stand low for a strike. Never on Relaxed.</summary>
        public static bool StrikeDue(int low, int total, int level)
        {
            return CompanyCatalogue.StrikeDays[CompanyCatalogue.Level(level)] > 0 && total >= CompanyCatalogue.StrikeStaffLeast
                   && (long)low * 1000 >= (long)total * CompanyCatalogue.StrikeShare;
        }

        public static bool CanStrike(ShopFile s, int day)
        {
            return s.StrikeFrom == 0 && day >= s.StrikeRest;
        }

        /// <summary>
        /// Called today, from tomorrow, for the length the strictness gives. Staff who down tools work no extra hours: an
        /// offer of overtime waiting is dropped, and overtime agreed stops before the strike's first day.
        /// </summary>
        public static void Call(ShopFile s, int level, int day)
        {
            s.StrikeFrom = day + 1;
            s.StrikeEnd = s.StrikeFrom + CompanyCatalogue.StrikeDays[CompanyCatalogue.Level(level)];
            if (s.OvertimeAsk > 0)
            {
                OvertimeAnswer(s, false, 0, day);
            }
            if (s.OvertimeFrom > 0)
            {
                s.OvertimeEnd = Math.Min(s.OvertimeEnd, s.StrikeFrom);
            }
        }

        /// <summary>Whether agreed overtime is worked on a day: not on a day of a strike.</summary>
        public static bool OvertimeRuns(ShopFile s, int day)
        {
            return s.Overtime(day) && !s.Striking(day);
        }

        /// <summary>The day a line about a strike names: its last while it runs, as a campaign's line does; its first while called.</summary>
        public static int StrikeShown(ShopFile s, int day)
        {
            return s.Striking(day) ? s.StrikeEnd - 1 : s.StrikeFrom;
        }

        public static void End(ShopFile s, int day)
        {
            s.StrikeFrom = s.StrikeEnd = 0;
            s.StrikeRest = day + CompanyCatalogue.StrikeRestDays;
        }

        /// <summary>What the end of a strike leaves one employee of that business with.</summary>
        public static void After(StaffFile f, StrikeAnswer how)
        {
            switch (how)
            {
                case StrikeAnswer.Raise:
                    f.Raise += StrikeRaise(f);
                    f.Standing = Math.Max(f.Standing, CompanyCatalogue.StrikeRaiseStanding);
                    break;
                case StrikeAnswer.Payment:
                    MoveStanding(f, CompanyCatalogue.StrikePayStanding);
                    break;
                default:
                    f.Standing = Math.Max(f.Standing, CompanyCatalogue.StrikeAfterStanding);
                    break;
            }
            f.UnderDays = 0;
            f.AskDay = f.AskTo = 0;
        }

        public static int StrikePayment(StaffFile f)
        {
            return Pay(f.Paid, CompanyCatalogue.StrikePayHours * 100);
        }

        /// <summary>What a raise for all adds to one employee's pay, hundredths an hour.</summary>
        public static int StrikeRaise(StaffFile f)
        {
            return Math.Max(0, f.Going - f.Paid);
        }

        /// <summary>Whether a busy business's staff offer overtime today; the same for a seed, day and shop.</summary>
        public static bool OvertimeOffered(ShopFile s, bool busy, int seed, int age, int shop, int day)
        {
            return busy && s.OvertimeAsk == 0 && s.OvertimeFrom == 0 && s.StrikeFrom == 0 && day >= s.OvertimeRest
                   && CityModel.Draw(seed, PurposeOvertime, age * CompanyCatalogue.ShopsMost + shop, 1000) < CompanyCatalogue.OvertimeChance;
        }

        public static int OvertimePay(StaffFile f)
        {
            return Pay(CompanyCatalogue.Share(f.Paid, CompanyCatalogue.OvertimeRate), CompanyCatalogue.OvertimeHours * 100);
        }

        public static void OvertimeAnswer(ShopFile s, bool agree, int cost, int day)
        {
            agree &= s.StrikeFrom == 0;   // not while a strike is called or on
            s.OvertimeAsk = 0;
            if (agree)
            {
                s.OvertimeFrom = day;
                s.OvertimeEnd = day + CompanyCatalogue.OvertimeDays;
                s.OvertimeCost = Math.Max(0, cost);
                s.OvertimeRest = s.OvertimeEnd + CompanyCatalogue.OvertimeRestDays;
            }
            else
            {
                s.OvertimeRest = day + CompanyCatalogue.OvertimeRestDays;
            }
        }

        /// <summary>Overtime that has run its days is over, and an offer nobody answered is dropped.</summary>
        public static void OvertimeDay(ShopFile s, int day)
        {
            if (s.OvertimeFrom > 0 && day >= s.OvertimeEnd)
            {
                s.OvertimeFrom = s.OvertimeEnd = s.OvertimeCost = 0;
            }
            if (s.OvertimeAsk > 0 && day > s.OvertimeAsk + CompanyCatalogue.OvertimeAnswerDays)
            {
                OvertimeAnswer(s, false, 0, day);
            }
        }

        public static int SickPay(StaffFile f, int policy)
        {
            policy = CompanyCatalogue.Clamp(policy, 0, CompanyCatalogue.SickShare.Length - 1);
            int hours = f.DayHours > 0 ? f.DayHours : CompanyCatalogue.SickDayHours * 100;
            return CompanyCatalogue.Share(Pay(f.Paid, hours), CompanyCatalogue.SickShare[policy]);
        }

        public static void SickDay(StaffFile f, int policy)
        {
            MoveStanding(f, CompanyCatalogue.SickStanding[CompanyCatalogue.Clamp(policy, 0, CompanyCatalogue.SickStanding.Length - 1)]);
        }

        /// <summary>What a strike and overtime do to a business's customers today, per cent.</summary>
        public static int Footfall(ShopFile s, int day)
        {
            return (s.Striking(day) ? CompanyCatalogue.StrikeFootfall : 0) + (OvertimeRuns(s, day) ? CompanyCatalogue.OvertimeFootfall : 0);
        }

        /// <summary>A number that changes when an agreed raise of anybody at a place does: the rota planner reads wages with them.</summary>
        public static int RaisesAt(List<StaffFile> staff, string place)
        {
            int sum = 17;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                if (string.Equals(staff[i].Place, place, StringComparison.Ordinal))
                {
                    sum = unchecked(sum * 31 + staff[i].Raise);
                }
            }
            return sum;
        }

        public static void MoveStanding(StaffFile f, int points)
        {
            f.Standing = CompanyCatalogue.Clamp(f.Standing + points, 0, CompanyCatalogue.StandingMost);
        }
    }
}
