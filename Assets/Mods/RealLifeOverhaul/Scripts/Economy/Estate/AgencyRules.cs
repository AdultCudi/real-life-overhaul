using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What happened to an instruction on a day, for the director to pay, charge and tell.</summary>
    public struct AgencyEvent
    {
        public const int Withdrawn = 1, Agreed = 2, NoDeal = 3, Completed = 4, FellThrough = 5, Lapsed = 6;

        public int Kind;
        public int Id;
        public bool Sale;
        public string District;
        public int Amount;      // the commission on a completion; the price on a deal agreed
    }

    /// <summary>The estate agency by rule: instructions, their marketing, deals and commission. Game-free; money moves in the director.</summary>
    public static class AgencyRules
    {
        private const int ForArrive = 31, ForDistrict = 32, ForKind = 33, ForSqm = 34, ForAsk = 35, ForDeal = 36, ForPrice = 37, ForFall = 38;

        /// <summary>The chance, in thousandths, that an owner comes to the agency on a day.</summary>
        public static int InstructionChance(int reputation, int cycle)
        {
            long chance = EstateCatalogue.InstructionBase + (long)EstateCatalogue.Clamp(reputation, 0, 1000) * EstateCatalogue.InstructionPerPoint / 1000;
            return (int)Math.Min(1000, chance * EstateCatalogue.ApplicantCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000);
        }

        /// <summary>An agent's hours an instruction takes, in tenths.</summary>
        public static int Hours(bool sale, int sqm)
        {
            return sale ? (EstateCatalogue.SaleHours + sqm / EstateCatalogue.SaleSqmPerHour) * 10 : (EstateCatalogue.LetHours + sqm / EstateCatalogue.LetSqmPerHour) * 10;
        }

        /// <summary>What an employee adds to a day's capacity, in tenths of an hour: the week's hours spread over its days, by skill.</summary>
        public static int Capacity(int weeklyHours, int skill)
        {
            return Math.Max(0, weeklyHours) * 10 * EstateCatalogue.Clamp(skill, 0, 100) / 100 / 7;
        }

        public static int MarketingCost(bool sale, int value)
        {
            return Math.Max(EstateCatalogue.MarketingLeast, (int)((long)Math.Max(0, value) * (sale ? EstateCatalogue.MarketingSale : EstateCatalogue.MarketingLet) / 10000));
        }

        /// <summary>What a completed instruction pays: a share of the price of a sale, or of the first year's rent of a letting.</summary>
        public static int Commission(bool sale, int price)
        {
            return sale
                ? EstateCatalogue.Share(price, EstateCatalogue.CommissionSale)
                : EstateCatalogue.Share((long)EstateCatalogue.Share(price, EstateCatalogue.AgencyYield), EstateCatalogue.CommissionLet);
        }

        /// <summary>The chance, in thousandths, that the work ends in a deal: less the more the owner wants, more with a name and in good times.</summary>
        public static int DealChance(int ask, int reputation, int cycle)
        {
            int span = EstateCatalogue.OwnerAskMost - EstateCatalogue.OwnerAskLeast;
            int at = EstateCatalogue.Clamp(ask, EstateCatalogue.OwnerAskLeast, EstateCatalogue.OwnerAskMost) - EstateCatalogue.OwnerAskLeast;
            long chance = EstateCatalogue.DealAtLeast + (long)(EstateCatalogue.DealAtMost - EstateCatalogue.DealAtLeast) * at / span;
            chance = chance * EstateCatalogue.SaleCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000 + EstateCatalogue.Clamp(reputation, 0, 1000) / EstateCatalogue.DealPerReputation;
            return EstateCatalogue.Clamp((int)chance, EstateCatalogue.DealLeast, EstateCatalogue.DealMost);
        }

        public static void Reputation(AgencyFile agency, string district, int change)
        {
            DistrictRep d = agency.District(district);
            if (d != null)
            {
                d.Rep = EstateCatalogue.Clamp(d.Rep + change, 0, 1000);
            }
        }

        /// <summary>An owner asks today, or null: never with no office, no negotiator at work or every instruction taken.</summary>
        public static Instruction Arrive(AgencyFile agency, int seed, int day, EstateClimate c, int real, bool whatever = false)
        {
            if (!agency.Running || agency.Districts.Count == 0 || agency.Open.Count >= EstateCatalogue.InstructionsMost || (!whatever && agency.Capacity <= 0))
            {
                return null;
            }
            DistrictRep d = agency.Districts[EstateRules.Between(EstateRules.Roll(seed, day, ForDistrict, 0), 0, agency.Districts.Count - 1)];
            if (d.SqmPrice <= 0 || (!whatever && EstateRules.Roll(seed, day, ForArrive, 0) >= InstructionChance(d.Rep, c.Cycle)))
            {
                return null;
            }
            int n = agency.NextId++;
            bool sale = EstateRules.Roll(seed, day, ForKind, n) < EstateCatalogue.InstructionSaleShare;
            int sqm = EstateRules.Between(EstateRules.Roll(seed, day, ForSqm, n), EstateCatalogue.UnitSqmLeast, EstateCatalogue.UnitSqmMost);
            long value = (long)sqm * d.SqmPrice * EstateCatalogue.Clamp(real, EstateCatalogue.RealLeast, EstateCatalogue.RealMost) / 1000 * EstateCatalogue.Clamp(c.Index, 500, 4000) / 1000;
            var i = new Instruction
            {
                Id = n, District = d.Name, Sale = sale, Sqm = sqm, Value = (int)Math.Min(int.MaxValue / 4, value),
                Ask = EstateRules.Between(EstateRules.Roll(seed, day, ForAsk, n), EstateCatalogue.OwnerAskLeast, EstateCatalogue.OwnerAskMost),
                Stage = (int)InstructionStage.Offered, Hours = Hours(sale, sqm), Day = day + EstateCatalogue.InstructionAnswerDays
            };
            agency.Open.Add(i);
            return i;
        }

        /// <summary>The marketing bill for taking an instruction on, paid first; 0 if it cannot be taken.</summary>
        public static int TakeCost(Instruction i)
        {
            return i != null && i.Stage == (int)InstructionStage.Offered ? MarketingCost(i.Sale, i.Value) : 0;
        }

        /// <summary>An instruction is marketed from today and must end in a deal within <see cref="EstateCatalogue.InstructionLapseDays"/>.</summary>
        public static bool Take(Instruction i, int day)
        {
            if (TakeCost(i) <= 0)
            {
                return false;
            }
            i.Stage = (int)InstructionStage.Marketing;
            i.Day = day + EstateCatalogue.InstructionLapseDays;
            return true;
        }

        /// <summary>One day of the agency, its hours spent oldest first; every outcome goes into <paramref name="events"/>.</summary>
        public static void Day(AgencyFile agency, int seed, int day, EstateClimate c, List<AgencyEvent> events)
        {
            int hours = agency.Running ? Math.Max(0, agency.Capacity) : 0;
            for (int n = 0; n < agency.Open.Count; n++)
            {
                Instruction i = agency.Open[n];
                switch ((InstructionStage)i.Stage)
                {
                    case InstructionStage.Offered:
                        if (day > i.Day)
                        {
                            End(agency, ref n, events, AgencyEvent.Withdrawn, i, 0, 0);
                        }
                        break;
                    case InstructionStage.Marketing:
                    {
                        int spent = Math.Min(hours, i.Hours - i.Done);
                        i.Done += spent;
                        hours -= spent;
                        if (i.Done >= i.Hours)
                        {
                            DistrictRep d = agency.District(i.District);
                            if (EstateRules.Roll(seed, day, ForDeal, i.Id) < DealChance(i.Ask, d != null ? d.Rep : 0, c.Cycle))
                            {
                                i.Stage = (int)InstructionStage.Completing;
                                int share = EstateRules.Between(EstateRules.Roll(seed, day, ForPrice, i.Id), EstateCatalogue.OwnerAskLeast, Math.Max(EstateCatalogue.OwnerAskLeast, i.Ask));
                                i.Price = EstateCatalogue.Share(i.Value, share);
                                i.Day = day + (i.Sale ? EstateCatalogue.SaleCompletes : EstateCatalogue.LetCompletes);
                                i.Fails = EstateRules.Roll(seed, day, ForFall, i.Id) < EstateCatalogue.FallThrough[EstateCatalogue.Clamp(c.Cycle, 0, 3)];
                                events.Add(new AgencyEvent { Kind = AgencyEvent.Agreed, Id = i.Id, Sale = i.Sale, District = i.District, Amount = i.Price });
                            }
                            else
                            {
                                End(agency, ref n, events, AgencyEvent.NoDeal, i, 0, EstateCatalogue.RepNoDeal);
                            }
                        }
                        else if (day > i.Day)
                        {
                            End(agency, ref n, events, AgencyEvent.Lapsed, i, 0, EstateCatalogue.RepLapsed);
                            agency.Lost++;
                        }
                        break;
                    }
                    default:
                        if (day >= i.Day)
                        {
                            if (i.Fails)
                            {
                                End(agency, ref n, events, AgencyEvent.FellThrough, i, 0, EstateCatalogue.RepFellThrough);
                                agency.Lost++;
                            }
                            else
                            {
                                int commission = Commission(i.Sale, i.Price);
                                agency.Commission += commission;
                                agency.Deals++;
                                End(agency, ref n, events, AgencyEvent.Completed, i, commission, EstateCatalogue.RepCompleted);
                            }
                        }
                        break;
                }
            }
        }

        private static void End(AgencyFile agency, ref int n, List<AgencyEvent> events, int kind, Instruction i, int amount, int reputation)
        {
            Reputation(agency, i.District, reputation);
            events.Add(new AgencyEvent { Kind = kind, Id = i.Id, Sale = i.Sale, District = i.District, Amount = amount });
            agency.Open.RemoveAt(n);
            n--;
        }
    }
}
