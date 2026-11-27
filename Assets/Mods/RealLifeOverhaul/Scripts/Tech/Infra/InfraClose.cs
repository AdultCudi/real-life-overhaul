using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What closing a data centre comes to, cents at the start's prices: what its racks fetch, what ending its clients' terms early costs, and how many contracts end.</summary>
    public struct CentreClose
    {
        public long Salvage, Penalty;
        public int Contracts, Racks;
    }

    /// <summary>
    /// Closing a data centre the player set up: the racks go back at the game's own selling multiplier on their value in the books (the game pays that fraction of what an item
    /// cost when a lease ends and its furniture is sold; the racks are the mod's own capital, so the books' value stands for the price), every colocation contract ends with a month's
    /// fee to its client, or what is left of the term when that is less, and the centre is no more: its power, water, upkeep and crews are booked by the day for the centres that
    /// stand. The building is the game's and stays the player's. Game-free; the director books the money and cancels the leased line.
    /// </summary>
    public static class InfraClose
    {
        /// <summary>The racks' return: their value in the books at the selling multiplier (thousandths, 0 to 1000).</summary>
        public static long Salvage(DataCentre d, int sellingMillis) => Math.Max(0L, d.Book) * Math.Max(0, Math.Min(1000, sellingMillis)) / 1000;

        /// <summary>What a client is paid for its term ended early: a month's fee, or the part of it the term had left when that is under a month.</summary>
        public static long Penalty(ColoContract c, int day)
        {
            int left = Math.Max(0, Math.Min(FirmsCatalogue.MonthDays, c.End - day));
            return Math.Max(0L, c.Fee) * left / FirmsCatalogue.MonthDays;
        }

        public static CentreClose Quote(InfraFile f, DataCentre d, int day, int sellingMillis)
        {
            var q = new CentreClose { Salvage = Salvage(d, sellingMillis), Racks = d.Racks + d.GpuRacks };
            for (int i = 0; i < f.Colo.Count; i++)
            {
                if (f.Colo[i].Place == d.Place)
                {
                    q.Contracts++;
                    q.Penalty += Penalty(f.Colo[i], day);
                }
            }
            return q;
        }

        /// <summary>The centre ended: its contracts struck out and the centre taken off the list. Returns the contracts ended.</summary>
        public static int End(InfraFile f, DataCentre d)
        {
            int ended = 0;
            for (int i = f.Colo.Count - 1; i >= 0; i--)
            {
                if (f.Colo[i].Place == d.Place)
                {
                    f.Colo.RemoveAt(i);
                    ended++;
                }
            }
            f.Centres.Remove(d);
            return ended;
        }
    }
}
