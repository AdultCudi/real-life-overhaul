using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A business of somebody else's that could want its floor cleaned, as the director read it from the game.</summary>
    public struct ContractCandidate
    {
        public string Place, Name, District;
        public int Sqm, Capacity, X, Z;
        public int Income;               // its district's income level, 60 to 140
    }

    /// <summary>How a contract's week ended, if it did.</summary>
    public enum ContractWeek { None, Paid, Missed, Lost }

    /// <summary>
    /// Cleaning for other businesses, game-free and seeded. The game keeps no dirt for a business that is not the player's,
    /// so a client's floor is one figure of the mod's: it gains a day's dirt each morning and loses what visits take off.
    /// </summary>
    public static class ContractRules
    {
        private const int PurposeStart = 11, PurposeStandard = 12;

        /// <summary>The dirt a client's floor gains in a day, in hundredths: more where there is room for more customers.</summary>
        public static int DirtDay(int capacity)
        {
            return ServicesCatalogue.Clamp(ServicesCatalogue.ContractDirtBase + Math.Max(0, capacity) * ServicesCatalogue.ContractDirtPerCustomer,
                ServicesCatalogue.ContractDirtBase, ServicesCatalogue.ContractDirtMost);
        }

        /// <summary>The minutes a day such a floor takes from the cleaner a fee is worked out for.</summary>
        public static int MinutesDay(int sqm, int capacity)
        {
            return CleaningRules.Minutes(DirtDay(capacity), Math.Max(1, ServicesCatalogue.ContractSkillRef / CleaningRules.Force(sqm)));
        }

        /// <summary>The weekly fee: the wages of the cleaning it takes with a margin, by district income (100 in the middle) and price index.</summary>
        public static int Fee(int sqm, int capacity, int income, int index)
        {
            long wages = (long)MinutesDay(sqm, capacity) * ServicesCatalogue.ContractWeekDays * ServicesCatalogue.ContractWageRef / 60;
            long fee = wages * ServicesCatalogue.ContractMarginPct / 100 * ServicesCatalogue.Clamp(income, 60, 140) / 100;
            return Math.Max(ServicesCatalogue.ContractFeeLeast, ServicesCatalogue.Round10(ServicesCatalogue.AtPrices(fee, index)));
        }

        public static bool OfferDue(ServicesState s, int day)
        {
            return s.Offers.Count < ServicesCatalogue.OffersMost && day - s.LastOfferDay >= ServicesCatalogue.OfferEveryDays;
        }

        /// <summary>Draws an offer from a seeded start, a district with no open offer first; null if nobody can ask. The day is noted either way.</summary>
        public static CleaningOffer Draw(ServicesState s, List<ContractCandidate> candidates, int day, int index)
        {
            s.LastOfferDay = day;
            if (s.Offers.Count >= ServicesCatalogue.OffersMost || candidates == null || candidates.Count == 0)
            {
                return null;
            }
            int from = ServicesCatalogue.Roll(s.Seed, day, PurposeStart, s.Offers.Count, candidates.Count), fallback = -1, chosen = -1;
            for (int n = 0; n < candidates.Count && chosen < 0; n++)
            {
                int i = (from + n) % candidates.Count;
                ContractCandidate c = candidates[i];
                if (string.IsNullOrEmpty(c.Place) || s.Client(c.Place) != null || s.Offer(c.Place) != null)
                {
                    continue;
                }
                if (DistrictAsking(s, c.District))
                {
                    fallback = fallback < 0 ? i : fallback;
                    continue;
                }
                chosen = i;
            }
            chosen = chosen < 0 ? fallback : chosen;
            if (chosen < 0)
            {
                return null;
            }
            ContractCandidate got = candidates[chosen];
            var offer = new CleaningOffer
            {
                Place = got.Place, Name = got.Name ?? string.Empty, District = got.District ?? string.Empty, Sqm = got.Sqm, Capacity = got.Capacity, X = got.X, Z = got.Z,
                Fee = Fee(got.Sqm, got.Capacity, got.Income, index),
                Standard = ServicesCatalogue.ContractStandards[ServicesCatalogue.Roll(s.Seed, day, PurposeStandard, ServicesCatalogue.Number(got.Place), ServicesCatalogue.ContractStandards.Length)],
                Until = day + ServicesCatalogue.OfferOpenDays
            };
            s.Offers.Add(offer);
            return offer;
        }

        private static bool DistrictAsking(ServicesState s, string district)
        {
            for (int i = 0; i < s.Offers.Count; i++)
            {
                if (string.Equals(s.Offers[i].District, district, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Takes away offers whose days have run out. Returns how many.</summary>
        public static int Expire(ServicesState s, int day)
        {
            int gone = 0;
            for (int i = s.Offers.Count - 1; i >= 0; i--)
            {
                if (day > s.Offers[i].Until)
                {
                    s.Offers.RemoveAt(i);
                    gone++;
                }
            }
            return gone;
        }

        /// <summary>How many contracts are running.</summary>
        public static int Running(ServicesState s)
        {
            int count = 0;
            for (int i = 0; i < s.Clients.Count; i++)
            {
                count += s.Clients[i].Outside ? 1 : 0;
            }
            return count;
        }

        /// <summary>Takes an offer up: the business joins the round as needed, its first week from today; null if there is no room.</summary>
        public static CleaningClient Accept(ServicesState s, CleaningOffer offer, int day)
        {
            if (offer == null || s.Client(offer.Place) != null || Running(s) >= ServicesCatalogue.ContractsMost || s.Clients.Count >= ServicesCatalogue.ClientsMost)
            {
                return null;
            }
            var client = new CleaningClient
            {
                Place = offer.Place, Name = offer.Name, District = offer.District, Outside = true, X = offer.X, Z = offer.Z, Sqm = offer.Sqm, Capacity = offer.Capacity,
                Fee = offer.Fee, Standard = offer.Standard, WeekFrom = day, Dirt = ServicesCatalogue.ContractStartDirt, DirtDay = DirtDay(offer.Capacity),
                Mode = ServicesCatalogue.ModeAsNeeded, Threshold = ThresholdFor(offer.Standard), Force = CleaningRules.Force(offer.Sqm)
            };
            client.NeedDay = client.DirtDay;
            client.CleanBefore = CleaningRules.Even(client.Dirt);
            s.Clients.Add(client);
            s.Offers.Remove(offer);
            return client;
        }

        /// <summary>The lowest threshold the page offers above a standard, so a visit falls due before it is missed; the highest if none is above.</summary>
        public static int ThresholdFor(int standard)
        {
            int[] steps = ServicesCatalogue.Thresholds;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] > standard)
                {
                    return steps[i];
                }
            }
            return steps[steps.Length - 1];
        }

        /// <summary>A client's morning: the floor is measured against the standard as the crew left it, then gains the day's dirt. True if it was under.</summary>
        public static bool Morning(CleaningClient c)
        {
            bool under = CleaningRules.Even(c.Dirt) < c.Standard;
            c.DaysUnder += under ? 1 : 0;
            c.Dirt = Math.Min(ServicesCatalogue.DirtMost, c.Dirt + Math.Max(0, c.DirtDay));
            return under;
        }

        public static void Visit(CleaningClient c, int rate, int minutes)
        {
            c.Dirt = CleaningRules.Clean(c.Dirt, CleaningRules.Off(rate, minutes));
        }

        /// <summary>The end of a client's week, if this is it: the fee, or a penalty; <see cref="ContractWeek.Lost"/> and the caller takes it off.</summary>
        public static ContractWeek Week(CleaningClient c, int day, out int fee, out int penalty)
        {
            fee = penalty = 0;
            if (!c.Outside || day - c.WeekFrom < ServicesCatalogue.ContractWeekDays)
            {
                return ContractWeek.None;
            }
            bool kept = c.DaysUnder <= ServicesCatalogue.ContractDaysUnderMost;
            c.WeekFrom = day;
            c.DaysUnder = 0;
            if (kept)
            {
                fee = c.Fee;
                c.Missed = 0;
                c.Earned += fee;
                return ContractWeek.Paid;
            }
            penalty = ServicesCatalogue.Pct(c.Fee, ServicesCatalogue.ContractPenaltyPct);
            c.Missed++;
            return c.Missed >= ServicesCatalogue.ContractMissesMost ? ContractWeek.Lost : ContractWeek.Missed;
        }
    }
}
