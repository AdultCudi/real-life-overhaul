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

        public static bool Wanted(StaffFile f, int level, int day)
        {
            return CompanyCatalogue.PoachChance[CompanyCatalogue.Level(level)] > 0 && f.Skill >= CompanyCatalogue.PoachSkill && f.Going > 0
                   && Gap(f.Going, f.Paid) >= CompanyCatalogue.PoachGap && f.PoachDay == 0 && f.LeaveDay == 0 && f.AskDay == 0
                   && day >= f.PoachRest;
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

        /// <summary>Called today, from tomorrow, for the length the strictness gives.</summary>
        public static void Call(ShopFile s, int level, int day)
        {
            s.StrikeFrom = day + 1;
            s.StrikeEnd = s.StrikeFrom + CompanyCatalogue.StrikeDays[CompanyCatalogue.Level(level)];
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
            return (s.Striking(day) ? CompanyCatalogue.StrikeFootfall : 0) + (s.Overtime(day) ? CompanyCatalogue.OvertimeFootfall : 0);
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
