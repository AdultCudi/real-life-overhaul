using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A building sold, and the same ownership put back afterwards. Game-free, so EconomyCheck (SoldBackChecks.cs) checks
    /// the very rules the property business hands a building back by.
    ///
    /// A sale (the game's own to a competitor, RealEstateHelper.SimulateCompetitorBuyingPlayerBuildings, or the agency's)
    /// takes the building out of the game's list of the player's (GameInstance.realEstate) and off the market, and gives
    /// it a rival owner. Another mod that keeps buildings of its own in its own save data (a player's car park, 18 and 20
    /// Oct 2026: an underground car park another mod sells) put the very same entry back as the save loaded, its old
    /// purchase day and its listing with it, so the car park was the player's again for nothing and sold again for the
    /// same price, every time. The game's purchase (BizManPresentation) always writes the day it is made: an ownership that
    /// dates from before the sale, with no purchase of the building in the game's books since, is the one that was sold.
    ///
    /// A few compares, on a start, each game hour and each morning, over the handful of buildings remembered.
    /// </summary>
    public static partial class EstateRules
    {
        /// <summary>
        /// True when the building the player holds now is the ownership a sale took: bought (<paramref name="purchaseDay"/>)
        /// before the sale's day, and not bought again since (<paramref name="boughtAgainDay"/>, the newest purchase of it in
        /// the game's books, -1 for none). A purchase on the day of the sale or after is the player's.
        /// </summary>
        public static bool SoldBack(int soldDay, int purchaseDay, int boughtAgainDay)
        {
            return soldDay > 0 && purchaseDay < soldDay && boughtAgainDay < soldDay;
        }

        /// <summary>
        /// A sale remembered: one of the same building is replaced (a sale seen again keeps whether the player was told; a
        /// later sale is a new one), and past <see cref="EstateCatalogue.SoldMost"/> the oldest sale is forgotten. Null for a
        /// building with no place or a sale with no day.
        /// </summary>
        public static SoldBuilding Remember(List<SoldBuilding> sold, string place, int soldDay, int boughtDay, string rival)
        {
            if (sold == null || string.IsNullOrEmpty(place) || soldDay <= 0)
            {
                return null;
            }
            SoldBuilding b = null;
            for (int i = 0; i < sold.Count; i++)
            {
                if (sold[i].Place == place)
                {
                    b = sold[i];
                    break;
                }
            }
            if (b == null)
            {
                b = new SoldBuilding { Place = place };
                sold.Add(b);
            }
            else if (b.SoldDay != soldDay || b.BoughtDay != boughtDay)
            {
                b.Told = false;   // sold again after it was bought again: the player is told about this one
            }
            b.SoldDay = soldDay;
            b.BoughtDay = boughtDay;
            if (!string.IsNullOrEmpty(rival))
            {
                b.Rival = rival;
            }
            while (sold.Count > EstateCatalogue.SoldMost)
            {
                int oldest = 0;
                for (int i = 1; i < sold.Count; i++)
                {
                    if (sold[i].SoldDay < sold[oldest].SoldDay)
                    {
                        oldest = i;
                    }
                }
                sold.RemoveAt(oldest);
            }
            return b;
        }
    }
}
