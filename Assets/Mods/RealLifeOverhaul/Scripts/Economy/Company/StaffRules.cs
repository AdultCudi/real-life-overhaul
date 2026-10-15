using System;

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
        Raise = 0,      // every employee of the business is brought up to the going rate
        Payment = 1,    // a one-off payment to each
        SitOut = 2      // nothing: it runs its length
    }

    /// <summary>What a day brought for one employee.</summary>
    public enum StaffNote
    {
        None = 0,
        Asked = 1,          // they ask for a raise
        AskLapsed = 2,      // a request went unanswered: taken as refused
        AskWithdrawn = 3    // they are no longer underpaid: the request is dropped
    }

    /// <summary>
    /// Staff as an economy, by rule: the going rate, who is underpaid, raises asked for and answered, a rival's offer,
    /// a strike, overtime and sick pay. Game-free: the director reads the game's wages, skills and absences into the
    /// files and calls these; nothing here knows the game. The game's own wage is never changed: what is agreed here
    /// is <see cref="StaffFile.Raise"/>, paid on top of it.
    /// </summary>
    public static class StaffRules
    {
        private const int PurposePoach = 61, PurposeOvertime = 62, PurposeWho = 63;

        // ---------------------------------------------------------------- the wage market

        /// <summary>
        /// The going rate against the game's own wage formula, thousandths: higher in a boom and lower in bad times, and
        /// higher where the district's income is, within the market's bounds; then at today's prices. The city's cost of
        /// living pays every wage at today's prices (Economy/City: the game's wages times the price level), and what
        /// somebody is paid is counted the same way (<see cref="StaffFile.Wage"/>), so prices moving make nobody underpaid
        /// or overpaid by themselves.
        /// </summary>
        public static int Market(int index, CycleState cycle, int income)
        {
            int state = CompanyCatalogue.Clamp((int)cycle, 0, CompanyCatalogue.WageCycle.Length - 1);
            int market = 1000 + CompanyCatalogue.WageCycle[state] + (income - CompanyCatalogue.IncomeMiddle) * CompanyCatalogue.WageIncomePerPoint;
            market = CompanyCatalogue.Clamp(market, CompanyCatalogue.WageMarketLeast, CompanyCatalogue.WageMarketMost);
            return (int)((long)market * CompanyCatalogue.Clamp(index, 500, 4000) / CompanyCatalogue.IndexStart);
        }

        /// <summary>The going hourly rate for somebody, hundredths, from what the game's own wage formula gives for their skill.</summary>
        public static int Going(int formula, int market)
        {
            return CompanyCatalogue.Share(Math.Max(0, formula), market);
        }

        /// <summary>How far under the going rate somebody is paid, thousandths; less than nothing if they are paid over it.</summary>
        public static int Gap(int going, int paid)
        {
            return going <= 0 ? 0 : (int)((long)(going - paid) * 1000 / going);
        }

        public static bool Underpaid(StaffFile f, int level)
        {
            return f.Going > 0 && Gap(f.Going, f.Paid) >= CompanyCatalogue.RaiseGap[CompanyCatalogue.Level(level)];
        }

        /// <summary>Pay for a number of hours at an hourly rate, both in hundredths: whole money, rounded.</summary>
        public static int Pay(int rate, int hours)
        {
            return (int)(((long)Math.Max(0, rate) * Math.Max(0, hours) + 5000) / 10000);
        }

        // ---------------------------------------------------------------- one day for one employee

        /// <summary>
        /// A day: their standing drifts (down while underpaid, back up to a calm level while paid fairly), a request
        /// nobody answered is taken as refused, and somebody underpaid for long enough asks for the going rate.
        /// </summary>
        public static StaffNote Day(StaffFile f, int level, int day)
        {
            return Day(f, level, day, 0);
        }

        /// <summary>The same, with the comfort of their workplace (0 to 10): the calm level they climb back to is that much higher.</summary>
        public static StaffNote Day(StaffFile f, int level, int day, int comfort)
        {
            int calm = CompanyCatalogue.StandingCalm + CompanyCatalogue.Clamp(comfort, 0, 10);
            level = CompanyCatalogue.Level(level);
            bool under = Underpaid(f, level);
            f.UnderDays = under ? f.UnderDays + 1 : 0;
            if (under)
            {
                f.Standing = Bound(f.Standing + CompanyCatalogue.StandUnderDay);
            }
            else if (f.Standing < calm)
            {
                f.Standing = Bound(f.Standing + CompanyCatalogue.StandFairDay);
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

        /// <summary>
        /// Whether the player may bring somebody up to the going rate unasked today: underpaid, nothing waiting from them or a
        /// rival, not leaving, and the rest after the last answer over (one raise in <see cref="CompanyCatalogue.RaiseRestDays"/>
        /// days, as their own requests come).
        /// </summary>
        public static bool CanGive(StaffFile f, int day)
        {
            return f.AskDay == 0 && f.PoachDay == 0 && f.LeaveDay == 0 && f.Going > f.Paid && day >= f.AskRest;
        }

        /// <summary>The raise an answer gives, hundredths an hour: all of what is asked, part of it, or nothing.</summary>
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
            f.Standing = Bound(f.Standing + (answer == RaiseAnswer.Agree ? CompanyCatalogue.StandAgree : answer == RaiseAnswer.Less ? CompanyCatalogue.StandLess : CompanyCatalogue.StandRefuse));
            f.AskDay = f.AskTo = 0;
            f.UnderDays = 0;
            f.AskRest = day + CompanyCatalogue.RaiseRestDays;
        }

        // ---------------------------------------------------------------- a rival's offer

        /// <summary>Somebody a rival would want: good at the job by the game's own skill figure, and clearly underpaid.</summary>
        public static bool Wanted(StaffFile f, int level, int day)
        {
            return CompanyCatalogue.PoachChance[CompanyCatalogue.Level(level)] > 0 && f.Skill >= CompanyCatalogue.PoachSkill && f.Going > 0
                   && Gap(f.Going, f.Paid) >= CompanyCatalogue.PoachGap && f.PoachDay == 0 && f.LeaveDay == 0 && f.AskDay == 0 && day >= f.PoachRest;
        }

        /// <summary>Whether a rival makes an approach today; the same answer for a seed and a day.</summary>
        public static bool Approach(int seed, int age, int level)
        {
            return Approach(seed, age, level, 0);
        }

        /// <summary>The same, with the player's esteem as an employer (-10 to 10): a rival approaches less often an employer people think well of.</summary>
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

        /// <summary>Matched: they are paid what the rival offered from now on. Not matched: they work their notice and go.</summary>
        public static void OfferAnswer(StaffFile f, bool match, int day)
        {
            if (f.PoachDay == 0)
            {
                return;
            }
            if (match)
            {
                f.Raise += Math.Max(0, f.PoachOffer - f.Paid);
                f.Standing = Bound(f.Standing + CompanyCatalogue.StandMatch);
            }
            else
            {
                f.LeaveDay = day + CompanyCatalogue.PoachNoticeDays;
            }
            f.PoachDay = f.PoachOffer = 0;
            f.PoachRest = day + CompanyCatalogue.PoachRestDays;
        }

        // ---------------------------------------------------------------- a strike

        public static bool StandsLow(StaffFile f)
        {
            return f.Standing <= CompanyCatalogue.StrikeStanding;
        }

        /// <summary>Most of a business's staff stand low with the player: a strike is due. Never on Relaxed.</summary>
        public static bool StrikeDue(int low, int total, int level)
        {
            return CompanyCatalogue.StrikeDays[CompanyCatalogue.Level(level)] > 0 && total >= CompanyCatalogue.StrikeStaffLeast
                   && (long)low * 1000 >= (long)total * CompanyCatalogue.StrikeShare;
        }

        public static bool CanStrike(ShopFile s, int day)
        {
            return s.StrikeFrom == 0 && day >= s.StrikeRest;
        }

        /// <summary>Called today for tomorrow, for the length the strictness gives.</summary>
        public static void Call(ShopFile s, int level, int day)
        {
            s.StrikeFrom = day + 1;
            s.StrikeEnd = s.StrikeFrom + CompanyCatalogue.StrikeDays[CompanyCatalogue.Level(level)];
        }

        /// <summary>It is over, however it ended.</summary>
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
                    f.Raise += Math.Max(0, f.Going - f.Paid);
                    f.Standing = Math.Max(f.Standing, CompanyCatalogue.StrikeRaiseStanding);
                    break;
                case StrikeAnswer.Payment:
                    f.Standing = Bound(f.Standing + CompanyCatalogue.StrikePayStanding);
                    break;
                default:
                    f.Standing = Math.Max(f.Standing, CompanyCatalogue.StrikeAfterStanding);
                    break;
            }
            f.UnderDays = 0;
            f.AskDay = f.AskTo = 0;
        }

        /// <summary>The one-off payment to one employee: so many hours at what they are paid.</summary>
        public static int StrikePayment(StaffFile f)
        {
            return Pay(f.Paid, CompanyCatalogue.StrikePayHours * 100);
        }

        /// <summary>What a raise for all adds to one employee's pay, hundredths an hour.</summary>
        public static int StrikeRaise(StaffFile f)
        {
            return Math.Max(0, f.Going - f.Paid);
        }

        // ---------------------------------------------------------------- overtime and sick pay

        /// <summary>Whether a busy business's staff offer overtime today; the same answer for a seed, a day and a business.</summary>
        public static bool OvertimeOffered(ShopFile s, bool busy, int seed, int age, int shop, int day)
        {
            return busy && s.OvertimeAsk == 0 && s.OvertimeFrom == 0 && s.StrikeFrom == 0 && day >= s.OvertimeRest
                   && CityModel.Draw(seed, PurposeOvertime, age * CompanyCatalogue.ShopsMost + shop, 1000) < CompanyCatalogue.OvertimeChance;
        }

        /// <summary>A day of overtime for one employee: the extra hours at time and a half.</summary>
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

        /// <summary>The day ends: agreed overtime that has run its days is over, and an offer nobody answered is dropped.</summary>
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

        /// <summary>A day's sick pay for somebody the game has off sick, under a policy.</summary>
        public static int SickPay(StaffFile f, int policy)
        {
            policy = CompanyCatalogue.Clamp(policy, 0, CompanyCatalogue.SickShare.Length - 1);
            return CompanyCatalogue.Share(Pay(f.Paid, f.DayHours > 0 ? f.DayHours : CompanyCatalogue.SickDayHours * 100), CompanyCatalogue.SickShare[policy]);
        }

        public static void SickDay(StaffFile f, int policy)
        {
            f.Standing = Bound(f.Standing + CompanyCatalogue.SickStanding[CompanyCatalogue.Clamp(policy, 0, CompanyCatalogue.SickStanding.Length - 1)]);
        }

        /// <summary>What staff add to or take from a business's customers today, per cent: a strike, overtime.</summary>
        public static int Footfall(ShopFile s, int day)
        {
            return (s.Striking(day) ? CompanyCatalogue.StrikeFootfall : 0) + (s.Overtime(day) ? CompanyCatalogue.OvertimeFootfall : 0);
        }

        private static int Bound(int standing)
        {
            return CompanyCatalogue.Clamp(standing, 0, CompanyCatalogue.StandingMost);
        }
    }
}
