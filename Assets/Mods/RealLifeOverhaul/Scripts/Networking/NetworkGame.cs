using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Streets;
using UI.Guiders;
using UI.Notification;

namespace RealLifeOverhaul.Networking
{
    /// <summary>
    /// The networking life's only touch of the game: the save's data and day, the city's importers and wholesalers and the
    /// game's own contacts of theirs, the player's delivery contracts, the buildings for sale, the destination and the
    /// notifications. Called on a meeting and on a favour asked, never on a frame; a pass over the city's buildings costs
    /// a few thousand field reads and allocates nothing.
    /// </summary>
    public static class NetworkGame
    {
        private const string ImporterType = "ba:businesstype_importexport";
        private const string WholesaleType = "ba:businesstype_wholesalestore";
        private const float TipOfferRate = 0.9f;   // of the asking price: what the seller of a tipped building takes (the game's own rate is 0.98 to 1)

        /// <summary>The save's own data (made if it has none), or null without a save.</summary>
        public static Dictionary<string, string> Data()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return null;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            return save.modData;
        }

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        /// <summary>
        /// The city's importer (or wholesale store) drawn for a person: the lowest draw of all, so the same one on every
        /// machine whatever the order of the game's list. False if the city has none.
        /// </summary>
        public static bool Supplier(bool importer, int seed, int person, out string name, out string street, out int number)
        {
            name = street = null;
            number = 0;
            List<Building> all = BuildingHelper.allBuildings;
            string type = importer ? ImporterType : WholesaleType;
            uint best = uint.MaxValue;
            Building found = null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                Building b = all[i];
                if (b == null || b.SpecialService == null || b.SpecialService.businessTypeName != type || string.IsNullOrEmpty(b.StreetName))
                {
                    continue;
                }
                uint draw = NetworkRules.PlaceDraw(seed, person, b.StreetName, b.StreetNumber);
                if (draw < best)
                {
                    best = draw;
                    found = b;
                }
            }
            if (found == null)
            {
                return false;
            }
            BuildingRegistration reg = found.GetRegistration();
            name = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : null;
            street = found.StreetName;
            number = found.StreetNumber;
            return name != null;
        }

        /// <summary>
        /// The game's own contact of that importer or wholesaler, as talking to its manager in person gives it (its call
        /// opens the game's partnership or delivery dialog), and the place as the destination. False if it is gone.
        /// </summary>
        public static bool Introduce(string street, int number, out string name, out string address)
        {
            name = address = null;
            if (string.IsNullOrEmpty(street) || SaveGameManager.Current == null)
            {
                return false;
            }
            var at = new Address(street, number);
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(at);
            Contact contact = reg != null ? reg.GetOrAddBusinessContact(true) : null;
            if (contact == null)
            {
                return false;
            }
            name = !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : contact.id;
            address = at.ToFormattedString();
            Destination(at);
            return true;
        }

        /// <summary>
        /// Better terms at a wholesaler: its delivery fee waived on every delivery contract the player has with it, for as
        /// long as the contract runs (the fee is the contract's own, saved by the game). In a Going Public session only
        /// contracts of businesses that are this player's. The number of contracts changed; 0 when there are none.
        /// </summary>
        public static int WaiveFees(string street, int number, out float saved)
        {
            saved = 0f;
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.DeliveryContracts == null || string.IsNullOrEmpty(street))
            {
                return 0;
            }
            var at = new Address(street, number);
            int changed = 0;
            for (int i = 0; i < save.DeliveryContracts.Count; i++)
            {
                DeliveryContract c = save.DeliveryContracts[i];
                if (c == null || c.wholesaleAddress == null || c.wholesaleAddress != at || c.deliveryFee <= 0f || !Mine(c.businessAddress))
                {
                    continue;
                }
                saved += c.deliveryFee;
                c.deliveryFee = 0f;
                changed++;
            }
            return changed;
        }

        private static bool Mine(Address business)
        {
            if (!GoingPublic.InSession)
            {
                return true;
            }
            BuildingRegistration reg = business != null ? BuildingHelper.GetBuildingRegistration(business) : null;
            return reg != null && GoingPublic.Mine(reg);
        }

        /// <summary>
        /// A building for sale drawn for a person, made the destination; outside a Going Public session its seller will take
        /// an offer of <see cref="TipOfferRate"/> of the asking price through the game's own purchase screen. False if
        /// nothing is for sale.
        /// </summary>
        public static bool Tip(int seed, int person, out string address, out float price)
        {
            address = null;
            price = 0f;
            GameInstance save = SaveGameManager.Current;
            List<BuildingForSale> sale = save != null ? save.buildingsForSale : null;
            BuildingForSale best = null;
            uint low = uint.MaxValue;
            for (int i = 0; sale != null && i < sale.Count; i++)
            {
                BuildingForSale b = sale[i];
                if (b == null || b.address == null || string.IsNullOrEmpty(b.address.streetName) || b.buildingPrice <= 0f)
                {
                    continue;
                }
                uint draw = NetworkRules.PlaceDraw(seed, person, b.address.streetName, b.address.streetNumber);
                if (draw < low)
                {
                    low = draw;
                    best = b;
                }
            }
            if (best == null)
            {
                return false;
            }
            if (!GoingPublic.InSession)
            {
                best.acceptOfferRate = Math.Min(best.acceptOfferRate, TipOfferRate);
            }
            address = best.address.ToFormattedString();
            price = best.buildingPrice;
            Destination(best.address);
            return true;
        }

        /// <summary>The share of the asking price a tipped building's seller takes, for the line that says so.</summary>
        public static int TipPercent => (int)Math.Round(TipOfferRate * 100f);

        /// <summary>Whether a tip lowers the price here (never in a Going Public session: the listing is the city's).</summary>
        public static bool TipLowers => !GoingPublic.InSession;

        // As the game's own "Set destination" does (BizManBusiness.SetDestination).
        private static void Destination(Address address)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || InstanceBehavior<GuidersManager>.Instance == null)
            {
                return;
            }
            save.customDestination = address;
            GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);
        }

        /// <summary>A line in the game's language with its {placeholders} filled. Builds a string: on a meeting or a call, never per frame.</summary>
        public static string T(string key, Dictionary<string, string> data = null)
        {
            string text = key.GetLocalization();
            if (data != null && text != null)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    text = text.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
                }
            }
            return text ?? string.Empty;
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 10f, null, null, true, false);
        }
    }
}
