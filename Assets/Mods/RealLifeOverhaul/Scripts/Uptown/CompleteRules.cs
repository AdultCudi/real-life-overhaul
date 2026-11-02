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
        public bool Warehouse;       // its record's type is the game's warehouse: a vehicle drives in, and out
        public bool WarehouseRegistration;   // its registration is the game's Warehouse (it has vehicle slots)
        public int GatesWanted;      // the vehicle slots of its structure: a garage door whole for each
        public int GatesWhole;       // of its copy's garage doors, those whole: a component, its trigger and the closed door
        public bool GatesServe;      // every garage door of its look looks out on its street (GateRules.Serves)
        public bool LetByGame;       // its type is one the game's market lets out by itself (CompetitionHelper.SwapBuilding: homes and warehouses): see CompleteRules.Tenanted
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
        Entrances = 12,
        Count = 13
    }

    /// <summary>
    /// Whether a building of the district is a whole building of the game's (plans/build-mode.md section 4; the owner, 26 Oct
    /// 2026: "make sure everything gets properly named, populated etc. I should not face the same issue I faced with warehouse
    /// or the school"): its address, name, type, owner, market, card, BizMan page, the map's filters and search, listings,
    /// door and occupants, each PASS or FAIL. The owner rule is the game's own (BusinessHelper.GetBuildingOwnerDescription):
    /// the government's, the player's deed, a rival's id, or the city's sale list; anything else BizMan writes as
    /// "Undefined". A home or a warehouse the game's market has let out (its own daily swap, or the district's letting of a grown
    /// block's homes and of a block given to its residents) is not for rent and has no business, and is whole all the same
    /// (<see cref="Tenanted"/>). Game-free (EconomyCheck's BuildModeChecks). COST: nothing per frame; a few comparisons a building,
    /// on a stand and once a load.
    /// </summary>
    public static class CompleteRules
    {
        public const string StreetPrefix = "reallife:street_uptown_";   // the district's own streets (LayoutStreets' ids)

        private static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;

        private static readonly string[] Names = { "address", "name", "type", "owner", "market", "card", "BizMan", "filter", "search", "listing", "door", "occupants", "entrances" };

        /// <summary>True for a type the game's market lets out and takes back by itself, daily and per district (CompetitionHelper.SwapBuilding): homes and warehouses.</summary>
        public static bool GameLets(string type)
        {
            return type == DistrictCensus.HomeType || type == DistrictCensus.WarehouseType;
        }

        /// <summary>
        /// A home or a warehouse let to residents or tenants: not to let, no business in it, not rented by the player. The game's own
        /// homes and warehouses are in this state whenever its market has let them out, and the district's letting of a grown block's
        /// homes (LifeGame.LetOne) and a block given to its residents (LifeGame.ToResidents) write the same. Any other kind of building
        /// is either to let or has a business, so for it the state is a missing step.
        /// </summary>
        public static bool Tenanted(CompleteFacts f)
        {
            return f.LetByGame && !f.ForRent && !f.HasBusiness && !f.RentedByPlayer;
        }

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
            bool tenanted = Tenanted(f);
            bool address = f.DistrictStreet && f.Number > 0 && f.Number == f.PlanNumber && f.AtAddress == 1;
            bool name = f.HasBusiness ? !string.IsNullOrEmpty(f.BusinessName) : !string.IsNullOrEmpty(f.DisplayName);
            bool type = !string.IsNullOrEmpty(f.Type) && f.Type == f.RecordType && f.Size == f.RecordSize;
            bool market = f.PlayerRecord ? f.PlayerDeed && !f.OnSale : !f.PlayerDeed && (f.ForRent || f.HasBusiness || f.OnSale || f.RentedByPlayer || tenanted);
            bool card = f.TypeWords && (!f.HasBusiness || f.ScheduleDays > 0);
            bool bizman = f.InRegistrations && f.InLookup && owner != null;
            bool filter = f.InCityList;
            bool search = !f.HasBusiness || (f.InCityList && !string.IsNullOrEmpty(f.BusinessName));
            bool listing = f.PlayerRecord || f.HasBusiness || f.RentedByPlayer || f.ForRent || f.OnSale || tenanted;
            bool occupants = f.HasBusiness || f.RentedByPlayer || f.Occupancy > 0 || f.ForRent || tenanted || (f.PlayerRecord && f.PlayerDeed);
            bool entrances = Entrances(f);
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
            Set(ref pass, CompleteItem.Entrances, entrances);
            return pass;
        }

        /// <summary>
        /// A warehouse takes a vehicle in and out as the game's own does (the owner, 27 Oct 2026: "this breaks the immersion and the game"):
        /// its registration has vehicle slots, it has a garage door whole for each of them, and every door looks out on its street.
        /// Any other building is not asked.
        /// </summary>
        public static bool Entrances(CompleteFacts f)
        {
            return !f.Warehouse || (f.WarehouseRegistration && f.GatesWanted > 0 && f.GatesWhole == f.GatesWanted && f.GatesServe);
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

        /// <summary>What is missing for each item that failed, one phrase each joined by "; ", for the line's verdict: the step the building lacks. Empty when every item passed.</summary>
        public static string Missing(CompleteFacts f, int passed)
        {
            var b = new StringBuilder(192);
            for (int i = 0; i < (int)CompleteItem.Count; i++)
            {
                if ((passed & (1 << i)) == 0)
                {
                    b.Append(b.Length > 0 ? "; " : string.Empty).Append(Names[i]).Append(": ").Append(Lacks((CompleteItem)i, f));
                }
            }
            return b.ToString();
        }

        private static string Lacks(CompleteItem item, CompleteFacts f)
        {
            string number = f.Number.ToString(CI);
            switch (item)
            {
                case CompleteItem.Address:
                    return !f.DistrictStreet ? "its street " + (f.Street ?? "none") + " is not one of the district's"
                        : f.Number <= 0 || f.Number != f.PlanNumber ? "its number " + number + " is not the plan's " + f.PlanNumber.ToString(CI)
                        : f.AtAddress.ToString(CI) + " registration(s) in the save at its address, one wanted";
                case CompleteItem.Name: return "no business name and no address to write on its card";
                case CompleteItem.Type:
                    return "the game's record says " + (f.Type ?? "none") + " " + (f.Size ?? "none") + ", the district's line " + (f.RecordType ?? "none") + " " + (f.RecordSize ?? "none");
                case CompleteItem.Owner: return "no government type, deed, rival owner or sale listing names its owner, so BizMan writes Undefined";
                case CompleteItem.Market:
                    return f.PlayerRecord ? (f.PlayerDeed ? "the player's building is on the city's sale list" : "the player's building has no deed")
                        : f.PlayerDeed ? "a deed of the player's stands on a building of the city's"
                        : "not to let, no business, not for sale and nobody renting it (only a home or a warehouse is let out without one)";
                case CompleteItem.Card: return !f.TypeWords ? "the game has no words for its type " + (f.Type ?? "none") : "a business with no opening days";
                case CompleteItem.BizMan:
                    return !f.InRegistrations ? "its registration is not among the save's" : !f.InLookup ? "the game's lookup by address does not give this building" : "no owner (see owner)";
                case CompleteItem.Filter: return "not in the city's list of buildings that the map's filters read";
                case CompleteItem.Search: return !f.InCityList ? "not in the city's list of buildings that the map's search reads" : "its business has no name for the map's search";
                case CompleteItem.Listing: return "not the player's, no business, not to let, not for sale and nobody renting it";
                case CompleteItem.Door: return "its door is out of reach of the walking mesh";
                case CompleteItem.Occupants: return "no business, no tenant, no deed's tenants and not to let";
                default:
                    return !f.WarehouseRegistration ? "its registration is not the game's Warehouse"
                        : f.GatesWanted <= 0 ? "its structure has no vehicle slot"
                        : f.GatesWhole != f.GatesWanted ? f.GatesWhole.ToString(CI) + " of " + f.GatesWanted.ToString(CI) + " garage doors whole"
                        : "a garage door looks away from its street";
            }
        }

        /// <summary>
        /// Whether the notice that a building is not whole is shown (the owner, 2 Nov 2026: a player cannot act on it): never when none
        /// failed; when more failed than the last time the count was told in this save, or, with Developer diagnostics on, once a load.
        /// The line in Player.log is written whatever this says.
        /// </summary>
        public static bool NoticeDue(int failed, int told, bool diagnostics, bool saidThisLoad)
        {
            return failed > 0 && (failed > told || (diagnostics && !saidThisLoad));
        }

        /// <summary>
        /// The check's line for Player.log: each item PASS or FAIL, then the verdict, and inside the brackets, for a failure, what is
        /// missing. <paramref name="lot"/> names its lot when given.
        /// </summary>
        public static string Line(string when, string address, CompleteFacts f, int passed, string lot = null)
        {
            var b = new StringBuilder(320);
            b.Append("Uptown check (a whole building, ").Append(when).Append(", ").Append(address).Append("): ");
            for (int i = 0; i < (int)CompleteItem.Count; i++)
            {
                b.Append(i > 0 ? ", " : string.Empty).Append(Names[i]).Append(' ').Append((passed & (1 << i)) != 0 ? "PASS" : "FAIL");
            }
            b.Append(" (owner ").Append(Owner(f) ?? "Undefined").Append(", type ").Append(f.Type ?? "none");
            b.Append(", number ").Append(f.Number).Append(" of the plan's ").Append(f.PlanNumber);
            b.Append(string.IsNullOrEmpty(lot) ? string.Empty : ", lot " + lot);
            b.Append(All(passed) ? string.Empty : "; missing " + Missing(f, passed)).Append(')');
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
