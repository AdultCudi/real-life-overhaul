using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>What is read of one standing building of the district for its completeness check (LifeGame.Complete fills it).</summary>
    public struct CompleteFacts
    {
        public string Street;        // the registration's street id and number
        public int Number;
        public bool DistrictStreet;  // a street of the district's own (Uptown's ids)
        public int PlanNumber;       // the number the plan gives its lot
        public int AtAddress;        // registrations at its address (one wanted)
        public bool HasBusiness;     // a business is set up in it (its name)
        public string BusinessName;
        public string DisplayName;   // as the game writes it on a card: the business's name, else the address
        public string Type;          // the game's building record's type and size
        public string Size;
        public string RecordType;    // the district's record's
        public string RecordSize;
        public bool Government;      // its business type is the government's
        public bool PlayerDeed;      // the player's real estate at its address
        public string RivalOwner;    // buildingOwnerRivalId
        public bool OnSale;          // on the game's list of buildings for sale
        public bool PlayerRecord;    // the district's record says the player's
        public bool ForRent;         // AvailableForRent
        public bool RentedByPlayer;
        public bool TypeWords;       // the game has words for its type (the card's first line)
        public int ScheduleDays;     // a business's opening days (the card's open or closed)
        public bool InRegistrations; // among the save's registrations
        public bool InLookup;        // in the city's lookup by address (BizMan, the doors)
        public bool InCityList;      // among the city's list of building controllers (the map's filters and search read it)
        public bool DoorOnMesh;      // its entrance within reach of the walking mesh
        public int Occupancy;        // the player's deed's tenants, per cent; 0 for none
    }

    /// <summary>The items of the check, in the line's order.</summary>
    public enum CompleteItem
    {
        Address = 0,
        Name = 1,
        Type = 2,
        Owner = 3,
        Market = 4,
        Card = 5,
        BizMan = 6,
        Filter = 7,
        Search = 8,
        Listing = 9,
        Door = 10,
        Occupants = 11,
        Count = 12
    }

    /// <summary>
    /// Whether a building of the district is a whole building of the game's (plans/build-mode.md section 4; the owner, 26 Oct
    /// 2026: "make sure everything gets properly named, populated etc. I should not face the same issue I faced with warehouse
    /// or the school"): its address, name, type, owner, market, card, BizMan page, the map's filters and search, listings,
    /// door and occupants, each PASS or FAIL. The owner rule is the game's own (BusinessHelper.GetBuildingOwnerDescription):
    /// the government's, the player's deed, a rival's id, or the city's sale list; anything else BizMan writes as
    /// "Undefined". Game-free (EconomyCheck's BuildModeChecks). COST: nothing per frame; a few comparisons a building, on a
    /// stand and once a load.
    /// </summary>
    public static class CompleteRules
    {
        public const string StreetPrefix = "reallife:street_uptown_";   // the district's own streets (LayoutStreets' ids)

        private static readonly string[] Names = { "address", "name", "type", "owner", "market", "card", "BizMan", "filter", "search", "listing", "door", "occupants" };

        /// <summary>Who BizMan says owns it: "government", "player", "rival", "city", or null where the game would write "Undefined".</summary>
        public static string Owner(CompleteFacts f)
        {
            if (f.Government)
            {
                return "government";
            }
            if (f.PlayerDeed)
            {
                return "player";
            }
            if (!string.IsNullOrEmpty(f.RivalOwner))
            {
                return "rival";
            }
            return f.OnSale ? "city" : null;
        }

        /// <summary>Each item's result, a bit each (bit set: PASS).</summary>
        public static int Passed(CompleteFacts f)
        {
            int pass = 0;
            string owner = Owner(f);
            bool address = f.DistrictStreet && f.Number > 0 && f.Number == f.PlanNumber && f.AtAddress == 1;
            bool name = f.HasBusiness ? !string.IsNullOrEmpty(f.BusinessName) : !string.IsNullOrEmpty(f.DisplayName);
            bool type = !string.IsNullOrEmpty(f.Type) && f.Type == f.RecordType && f.Size == f.RecordSize;
            bool market = f.PlayerRecord ? f.PlayerDeed && !f.OnSale : !f.PlayerDeed && (f.ForRent || f.HasBusiness || f.OnSale || f.RentedByPlayer);
            bool card = f.TypeWords && (!f.HasBusiness || f.ScheduleDays > 0);
            bool bizman = f.InRegistrations && f.InLookup && owner != null;
            bool filter = f.InCityList;
            bool search = !f.HasBusiness || (f.InCityList && !string.IsNullOrEmpty(f.BusinessName));
            bool listing = f.PlayerRecord || f.HasBusiness || f.RentedByPlayer || f.ForRent || f.OnSale;
            bool occupants = f.HasBusiness || f.RentedByPlayer || f.Occupancy > 0 || f.ForRent || (f.PlayerRecord && f.PlayerDeed);
            Set(ref pass, CompleteItem.Address, address);
            Set(ref pass, CompleteItem.Name, name);
            Set(ref pass, CompleteItem.Type, type);
            Set(ref pass, CompleteItem.Owner, owner != null);
            Set(ref pass, CompleteItem.Market, market);
            Set(ref pass, CompleteItem.Card, card);
            Set(ref pass, CompleteItem.BizMan, bizman);
            Set(ref pass, CompleteItem.Filter, filter);
            Set(ref pass, CompleteItem.Search, search);
            Set(ref pass, CompleteItem.Listing, listing);
            Set(ref pass, CompleteItem.Door, f.DoorOnMesh);
            Set(ref pass, CompleteItem.Occupants, occupants);
            return pass;
        }

        private static void Set(ref int bits, CompleteItem item, bool on)
        {
            if (on)
            {
                bits |= 1 << (int)item;
            }
        }

        /// <summary>Every item passed.</summary>
        public static bool All(int passed) => passed == (1 << (int)CompleteItem.Count) - 1;

        /// <summary>The first item failed, or null.</summary>
        public static string FirstFailed(int passed)
        {
            for (int i = 0; i < (int)CompleteItem.Count; i++)
            {
                if ((passed & (1 << i)) == 0)
                {
                    return Names[i];
                }
            }
            return null;
        }

        /// <summary>The check's line for Player.log: each item PASS or FAIL, then the verdict.</summary>
        public static string Line(string when, string address, CompleteFacts f, int passed)
        {
            var b = new StringBuilder(320);
            b.Append("Uptown check (a whole building, ").Append(when).Append(", ").Append(address).Append("): ");
            for (int i = 0; i < (int)CompleteItem.Count; i++)
            {
                b.Append(i > 0 ? ", " : string.Empty).Append(Names[i]).Append(' ').Append((passed & (1 << i)) != 0 ? "PASS" : "FAIL");
            }
            b.Append(" (owner ").Append(Owner(f) ?? "Undefined").Append(", type ").Append(f.Type ?? "none");
            b.Append(", number ").Append(f.Number).Append(" of the plan's ").Append(f.PlanNumber).Append(')');
            b.Append(All(passed) ? ": PASS" : ": FAIL");
            return b.ToString();
        }

        /// <summary>
        /// The rival a city's building with no owner is given, as the game's own generator gives every building one: of the rivals
        /// already owning buildings in the save (sorted, no repeats), the one its address picks; null with none. The same on
        /// every machine for the same save.
        /// </summary>
        public static string PickOwner(IList<string> rivals, string street, int number)
        {
            if (rivals == null || rivals.Count == 0)
            {
                return null;
            }
            uint hash = 2166136261u;   // FNV-1a over the address: no machine-local hash
            string key = (street ?? string.Empty) + "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < key.Length; i++)
            {
                hash = (hash ^ key[i]) * 16777619u;
            }
            return rivals[(int)(hash % (uint)rivals.Count)];
        }

        /// <summary>The rivals owning buildings, from each registration's owner id, sorted ordinally with no repeats, into <paramref name="into"/> (cleared).</summary>
        public static void Rivals(IEnumerable<string> owners, List<string> into)
        {
            into.Clear();
            foreach (string id in owners)
            {
                if (!string.IsNullOrEmpty(id) && into.BinarySearch(id, StringComparer.Ordinal) < 0)
                {
                    into.Insert(~into.BinarySearch(id, StringComparer.Ordinal), id);
                }
            }
        }
    }
}
