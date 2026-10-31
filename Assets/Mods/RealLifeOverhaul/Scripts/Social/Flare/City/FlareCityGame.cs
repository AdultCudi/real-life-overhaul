using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city, the only file of it that touches the game: the save's data, a business's registration by the population's key, what a
    /// district's customers bear (the city economy's), and an ad's cost booked at its business through the mod's one money call (Economy/BizBooks).
    /// Every call is small and named, so a game member a build lacks fails its own method only (rules.md, Mono). COST: nothing per frame.
    /// </summary>
    internal static class FlareCityGame
    {
        public const string AdKey = "reallife_transaction_flare_ad";   // an ad on Flare, booked at its business as a marketing cost

        /// <summary>The save's modData, or null.</summary>
        public static Dictionary<string, string> Data()
        {
            return SaveGameManager.Current != null ? SaveGameManager.Current.modData : null;
        }

        /// <summary>What each of the population's districts' customers bear against the game's prices (the city economy's; 1000 while it is off).</summary>
        public static void Bears(PopulationState s, int[] into)
        {
            for (int d = 0; d < into.Length; d++)
            {
                try
                {
                    into[d] = d < s.Districts.Count ? CityEconomy.Bears(s.Districts[d].Id) : 1000;
                }
                catch (Exception)
                {
                    into[d] = 1000;
                }
            }
        }

        /// <summary>The business at the population's key ("street|number"), or null.</summary>
        public static BuildingRegistration Registration(string key)
        {
            int cut = key != null ? key.LastIndexOf('|') : -1;
            if (cut <= 0 || !int.TryParse(key.Substring(cut + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }
            try
            {
                return BuildingHelper.GetBuildingRegistration(new Address(key.Substring(0, cut), number));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The population's key of a registration ("street|number"), or null.</summary>
        public static string KeyOf(BuildingRegistration reg)
        {
            return reg != null && reg.Address != null ? PopulationState.KeyOf(reg.Address.streetName, reg.Address.streetNumber) : null;
        }

        /// <summary>A business's name as the game shows it, by the population's key; the key's address when it has none.</summary>
        public static string NameOf(string key)
        {
            BuildingRegistration reg = Registration(key);
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : key ?? "?";
        }

        /// <summary>An ad's cost taken from the player, booked at the business as Flare's marketing; false (nothing taken) if they cannot pay it.</summary>
        public static bool PayAd(BuildingRegistration reg, long cents)
        {
            if (cents <= 0)
            {
                return true;
            }
            return BizBooks.Book(-cents / 100f, new TransactionInfo(AdKey), null, reg != null ? reg.Address : null, force: false);
        }

        public const string DealKey = "reallife_transaction_flare_deal";         // a business of the city's paying the player for a post about it
        public const string CreatorKey = "reallife_transaction_flare_creator";   // Flare's creator income for the views of the player's posts

        /// <summary>A paid post's fee booked to the player, as the mod's other personal income is (no business of theirs).</summary>
        public static bool PayDeal(long cents)
        {
            return cents <= 0 || BizBooks.Book(cents / 100f, new TransactionInfo(DealKey), null, null, force: true);
        }

        /// <summary>A month's creator income booked to the player.</summary>
        public static bool PayCreator(long cents)
        {
            return cents <= 0 || BizBooks.Book(cents / 100f, new TransactionInfo(CreatorKey), null, null, force: true);
        }

        /// <summary>The game's prices against the start's, thousandths.</summary>
        public static int PriceLevel => Prices.Level;
    }
}
