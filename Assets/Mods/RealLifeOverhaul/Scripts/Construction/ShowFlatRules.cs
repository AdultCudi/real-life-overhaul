using System;
using RealLifeOverhaul.Interiors;

namespace RealLifeOverhaul.Construction
{
    /// <summary>Who shows a viewing's buyers round the show flat.</summary>
    public enum ViewingHost
    {
        Nobody = 0,   // the suite is open and the buyers look round alone
        Agent = 1,    // the sales agent from the estate agency
        Player = 2    // the player, there on the show flat's floor at the viewing
    }

    /// <summary>
    /// A show flat of a block sold off plan: its quality (the interior and decor scores of its design, as Interiors scores a
    /// business) against the district's standard sets how many viewings a day it has, how often a viewing brings a buyer and
    /// what that buyer pays against the plain off plan price; who shows the buyers round matters too. What it costs to dress
    /// and keep open. Every chance is a thousandth drawn from the save's seed, the project and the hour. Game-free.
    /// </summary>
    public static class ShowFlatRules
    {
        /// <summary>The show flat's quality, 0 to 100: Interiors' overall figure of its walls and floors and its decor.</summary>
        public static int Quality(int interior, int decor)
        {
            return InteriorRules.Overall(interior, decor);
        }

        /// <summary>Viewings a day: one, a second when it is over the district's standard, a third well over it.</summary>
        public static int Viewings(int quality, int standard)
        {
            int gap = quality - standard;
            int n = BuildCatalogue.ViewingsLeast + (gap >= BuildCatalogue.ViewingOver ? 1 : 0) + (gap >= BuildCatalogue.ViewingWellOver ? 1 : 0);
            return BuildCatalogue.Clamp(n, BuildCatalogue.ViewingsLeast, BuildCatalogue.ViewingHours.Length);
        }

        /// <summary>The hour of a day's viewing, from the first; -1 past the day's viewings.</summary>
        public static int ViewingHour(int index, int viewings)
        {
            return index >= 0 && index < viewings && index < BuildCatalogue.ViewingHours.Length ? BuildCatalogue.ViewingHours[index] : -1;
        }

        /// <summary>The viewing at an hour, by its index, or -1 for an hour with none.</summary>
        public static int ViewingAt(int hour, int viewings)
        {
            for (int i = 0; i < viewings && i < BuildCatalogue.ViewingHours.Length; i++)
            {
                if (BuildCatalogue.ViewingHours[i] == hour)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The chance in thousandths that a viewing brings a buyer: more for each point over the district's standard and for the
        /// company's reputation, more again with the agent or the player showing them round, much less with nobody there.
        /// </summary>
        public static int BuyerChance(int quality, int standard, int reputation, ViewingHost host)
        {
            long chance = BuildCatalogue.ShowBuyerBase + (long)(quality - standard) * BuildCatalogue.ShowBuyerPerPoint
                          + (long)(BuildCatalogue.Clamp(reputation, 0, 100) - 50) * BuildCatalogue.ShowBuyerRepPerPoint;
            if (host == ViewingHost.Player)
            {
                chance += BuildCatalogue.ShowShownPermille;
            }
            else if (host == ViewingHost.Agent)
            {
                chance += BuildCatalogue.ShowAgentPermille;
            }
            else
            {
                chance = chance * BuildCatalogue.ShowAloneShare / 1000L;
            }
            return (int)Math.Max(BuildCatalogue.ShowBuyerLeast, Math.Min(BuildCatalogue.ShowBuyerMost, chance));
        }

        /// <summary>What a show flat's buyer pays, in thousandths of the plain off plan price: a little more for each point over the standard, less under.</summary>
        public static int PricePermille(int quality, int standard)
        {
            return BuildCatalogue.Clamp(1000 + (quality - standard) * BuildCatalogue.ShowPricePerPoint, BuildCatalogue.ShowPriceLeast, BuildCatalogue.ShowPriceMost);
        }

        /// <summary>A show flat's buyer's price for one flat: the plain off plan price at the show flat's share.</summary>
        public static int Price(int offPlanPrice, int quality, int standard)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(0L, (long)offPlanPrice * PricePermille(quality, standard) / 1000L));
        }

        /// <summary>What dressing the show flat costs: its furniture at the game's market prices and the installation firm's fee.</summary>
        public static long FitCost(long furniture, long fee)
        {
            return Math.Max(0L, furniture) + Math.Max(0L, fee);
        }

        /// <summary>A day of the open suite at today's prices (a price index in thousandths), with the agent's day when there is one.</summary>
        public static long DayCost(bool agent, int prices)
        {
            long day = BuildCatalogue.ShowUpkeepDay + (agent ? BuildCatalogue.AgentDay : 0);
            return day * Math.Max(1, prices) / 1000L;
        }

        /// <summary>The agent's day alone at today's prices.</summary>
        public static long AgentCost(int prices)
        {
            return (long)BuildCatalogue.AgentDay * Math.Max(1, prices) / 1000L;
        }

        /// <summary>True when a project may have a show flat: a block of homes on its lot, sold off plan, not built yet, with flats left.</summary>
        public static bool Allowed(ProjectFile p, int flats)
        {
            return p != null && p.Kind == ProjectKind.Plot && p.Use == BuildUse.Homes && !p.InDistrict && (p.Stage == ProjectStage.Ready || p.Stage == ProjectStage.Building)
                   && flats - p.FlatsSold > 0;
        }

        /// <summary>The floor area of one of a block's flats: what the show flat's structure is chosen by.</summary>
        public static int FlatArea(int area, int flats)
        {
            return flats > 0 ? Math.Max(20, area / flats) : BuildCatalogue.FlatArea;
        }
    }
}
