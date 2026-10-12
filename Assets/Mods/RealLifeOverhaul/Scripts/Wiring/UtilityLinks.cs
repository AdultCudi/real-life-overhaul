using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Power and water (Scripts/Utilities) with the rest of the mod:
        ///
        ///   Grade        the credit rating (Economy): the deposit a new account asks for
        ///   Household    who lives at the player's home besides them (Family: a partner who moved in, children at
        ///                home; the roommate at the home they share): the home's power and water
        ///   CloseFor     a business closed for want of power or water (Business, the game's own switch)
        ///   Taken        the courthouse's building (Justice), which the office is not to be
        ///   Life         the electricity company's events need a power account of the player's (LifeAccounts.Power)
        ///   Home         a home without water has dry taps, one without power dark screens and no cooker (HomeTidy)
        /// </summary>
        public static class UtilityLinks
        {
            internal static void Connect()
            {
                UtilitiesBridges.Grade = () => (int)Economy.CreditLedger.Grade;
                UtilitiesBridges.Household = Household;
                UtilitiesBridges.CloseFor = (reg, hours) => BusinessDirector.Instance != null && BusinessDirector.Instance.CloseFor(reg, hours);
                UtilitiesBridges.Taken = building =>
                {
                    JusticeDirector justice = JusticeDirector.Instance;
                    return justice != null && justice.Court != null && ReferenceEquals(justice.Court.Building, building);
                };
                LifeAccounts.Power = () => UtilitiesDirector.HasPowerAccount;
                HomeTidy.HasPower = reg => UtilitiesDirector.Supplied(reg, Service.Power);
                HomeTidy.HasWater = reg => UtilitiesDirector.Supplied(reg, Service.Water);
            }

            internal static void Disconnect()
            {
                UtilitiesBridges.Reset();
                LifeAccounts.Power = null;
                HomeTidy.HasPower = null;
                HomeTidy.HasWater = null;
            }

            // The household lives at the player's first rented home (as HomeTidy.Home has it when they are out); the
            // roommate at the home the two share.
            private static int Household(BuildingRegistration reg)
            {
                if (reg == null)
                {
                    return 0;
                }
                int people = 0;
                if (ReferenceEquals(reg, FirstHome()))
                {
                    people += (Family.FamilyApi.LivesTogether ? 1 : 0) + Family.FamilyApi.Children;
                }
                Roommates.RoommateDirector roommates = Roommates.RoommateDirector.Instance;
                Roommates.RoommateState state = roommates != null ? roommates.State : null;
                if (state != null && state.Living && reg.StreetName == state.HomeStreet && reg.StreetNumber == state.HomeNumber)
                {
                    people++;
                }
                return people;
            }

            private static BuildingRegistration FirstHome()
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || save.BuildingRegistrations == null)
                {
                    return null;
                }
                foreach (BuildingRegistration reg in save.BuildingRegistrations)
                {
                    if (reg != null && reg.RentedByPlayer && reg.HasValidAddress)
                    {
                        Buildings.Building building = Helpers.BuildingHelper.GetBuilding(reg.Address);
                        if (building != null && building.BuildingType == "ba:buildingtype_residential")
                        {
                            return reg;
                        }
                    }
                }
                return null;
            }
        }
    }
}
