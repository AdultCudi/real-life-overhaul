using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The player's businesses as the stock market counts their takings: the game's list of businesses that generate revenue, and the data centres, which the game's
        /// list leaves out (a warehouse is no such business) though their takings, the cloud's among them, stand in the game's daily statements at their addresses.
        /// One place for the company's takings by trade, its day's takings on the tape and its books, so none of them leaves the cloud out.
        /// </summary>
        public static class CompanyBooks
        {
            /// <summary>The businesses whose statements make up the company's takings; cleared first.</summary>
            internal static void Businesses(List<BuildingRegistration> into)
            {
                Books.Businesses(into);
                TechDirector.Instance?.AddCentres(into);
            }

            /// <summary>The trade a business's takings count in: a data centre's are software's, any other's its type's; -1 for none.</summary>
            internal static int TradeOf(BuildingRegistration reg)
            {
                if (reg == null)
                {
                    return -1;
                }
                return TechDirector.Instance != null && TechDirector.Instance.IsCentre(reg) ? MarketInputs.SoftwareTrade : MarketInputs.TradeOf(reg.businessTypeName);
            }
        }
    }
}
