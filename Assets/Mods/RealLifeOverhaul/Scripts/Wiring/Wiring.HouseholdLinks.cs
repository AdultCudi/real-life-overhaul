using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Household;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Household staff (Home/Household/HouseholdBridges) with the city's economy, the dealerships, family life and car
        /// ownership:
        ///
        ///   WageMarket   the staff's going rate follows the city's wage market (Economy/Company StaffRules.Market: the
        ///                price level and the cycle), as the player's own businesses' staff are priced
        ///   NotOurs      a dealership's stock car and the partner's own car are never washed or serviced on the player's bill
        ///   Service      the mobile mechanic does car ownership's workshop service where the car stands, at the bay's
        ///                prices (Transport/CarOwnership.ServiceWhereItStands)
        ///
        /// Called on an hour's round, a page of the call or a test; a fault is said once (<see cref="Fault"/>) and the default given.
        /// </summary>
        public static class HouseholdLinks
        {
            internal static void Connect()
            {
                HouseholdBridges.WageMarket = WageMarket;
                HouseholdBridges.NotOurs = NotOurs;
                HouseholdBridges.Service = Service;
            }

            internal static void Disconnect()
            {
                HouseholdBridges.Reset();
            }

            private static int WageMarket()
            {
                try
                {
                    return StaffRules.Market(Prices.Level, CityEconomy.State, CompanyCatalogue.IncomeMiddle);
                }
                catch (System.Exception e)
                {
                    Fault("HouseholdLinks.WageMarket", e);
                    return Prices.Level;
                }
            }

            private static bool NotOurs(VehicleInstance car)
            {
                try
                {
                    return Dealerships.Stock(car) || Family.PartnerCar.Is(car);
                }
                catch (System.Exception e)
                {
                    Fault("HouseholdLinks.NotOurs", e);
                    return false;
                }
            }

            private static float Service(VehicleInstance car, bool doIt)
            {
                try
                {
                    return CarOwnership.ServiceWhereItStands(car, doIt);
                }
                catch (System.Exception e)
                {
                    Fault("HouseholdLinks.Service", e);
                    return 0f;
                }
            }
        }
    }
}
