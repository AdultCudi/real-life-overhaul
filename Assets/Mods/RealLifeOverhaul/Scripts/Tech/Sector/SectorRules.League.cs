using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One line of the sector league: a listed company (an index of HiringCatalogue.RivalFirms) or the player's own firms together (-1), with what it takes a month.</summary>
    public struct LeagueRow
    {
        public int Company;
        public long Monthly;      // cents at the start's prices
        public long Customers;
    }

    public static partial class SectorRules
    {
        /// <summary>The player's own firms stand in the league as this company.</summary>
        public const int LeagueYou = -1;

        /// <summary>
        /// What a rival's product takes a month, cents: its customers at the price its tier asks in its market, by the same sums as the player's products (a licence's upkeep,
        /// an app's share after the store's cut, a subscription's month). The rivals' takings are not kept, so the league works them out from what the sector keeps of them.
        /// </summary>
        public static long RivalMonthly(RivalProduct r)
        {
            int tier = Clamp(r.Tier, 0, 2);
            long price = (long)FirmsCatalogue.Price[Clamp(BuyersOf(r.Market), 0, 2)] * FirmsCatalogue.PriceTiers[tier] / 1000;
            switch ((SectorMarket)Clamp(r.Market, 0, (int)SectorMarket.Count - 1))
            {
                case SectorMarket.Licences:
                    return r.Customers * FirmsCatalogue.LicenceDeal * FirmsCatalogue.LicenceMaintenance / 1000 / 12;
                case SectorMarket.Apps:
                    return r.Customers * price * (1000 - FirmsCatalogue.AppStoreCut) / 1000;
                default:
                    return r.Customers * price;
            }
        }

        /// <summary>
        /// The league of the sector by what each takes a month: the listed companies that sell software, from the rivals' products the sector keeps, with the player's own firms
        /// among them. Highest first; the player's rank (1 first) comes out, or 0 when the player has no takings. Ties go to the company that comes first in the catalogue, so the
        /// order never changes between two reads of one state.
        /// </summary>
        public static int League(SectorFile f, long mine, List<LeagueRow> into)
        {
            into.Clear();
            for (int c = 0; f != null && c < HiringCatalogue.RivalFirms.Length; c++)
            {
                long monthly = 0, customers = 0;
                bool sells = false;
                for (int i = 0; i < f.Rivals.Count; i++)
                {
                    RivalProduct r = f.Rivals[i];
                    if (r.Company != c)
                    {
                        continue;
                    }
                    sells = true;
                    monthly += RivalMonthly(r);
                    customers += r.Customers;
                }
                if (sells)
                {
                    into.Add(new LeagueRow { Company = c, Monthly = monthly, Customers = customers });
                }
            }
            if (mine > 0)
            {
                into.Add(new LeagueRow { Company = LeagueYou, Monthly = mine });
            }
            into.Sort((a, b) =>
            {
                int byMoney = b.Monthly.CompareTo(a.Monthly);
                return byMoney != 0 ? byMoney : a.Company == LeagueYou ? -1 : b.Company == LeagueYou ? 1 : a.Company.CompareTo(b.Company);
            });
            for (int i = 0; i < into.Count; i++)
            {
                if (into[i].Company == LeagueYou)
                {
                    return i + 1;
                }
            }
            return 0;
        }
    }
}
