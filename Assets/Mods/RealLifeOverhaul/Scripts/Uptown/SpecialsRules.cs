using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>What a special place's staff work at, in the game's layout for it (the census asks the layout for it).</summary>
    public enum SpecialStaff
    {
        Desk,       // a special employee's desk: talked to, it opens the service's dialog and adds its contact
        Till,       // a checkout counter, worked by the player in the service's own job
        Showroom,   // showcase vehicles, bought on the floor
        None        // nobody: the game's own clinic has no staff, it is where a player who faints wakes up
    }

    /// <summary>
    /// One of Uptown's special places: a lot of the plan, the game's special service it copies (by its asset name), the
    /// place's own business name (the contact's key with the type; null for a branch, which keeps the game's), the game's
    /// structure whose layout the service has, and what its staff work at.
    /// </summary>
    public sealed class SpecialPlace
    {
        public readonly string Lot;          // the plan's lot id
        public readonly string Service;      // the game's SpecialService asset name ("CityWorkforceInc")
        public readonly string ServiceType;  // its business type, as the game's own building of it has
        public readonly string Name;         // the place's business name, the same in every language as the game's are; null: a branch
        public readonly string Size;         // the game's structure the service's layout is for
        public readonly int Version;
        public readonly string Type;         // the building type it stands as with its service
        public readonly string PlainType;    // the type it stands as where a player holds its lot
        public readonly SpecialStaff Staff;
        public readonly string Employer;     // the boss's name of the job it offers, or null
        public readonly string[] Goods;      // an importer's own goods, or null

        public SpecialPlace(string lot, string service, string serviceType, string name, string size, int version, string type, string plainType, SpecialStaff staff,
                            string employer = null, string[] goods = null)
        {
            Lot = lot;
            Service = service;
            ServiceType = serviceType;
            Name = name;
            Size = size;
            Version = version;
            Type = type;
            PlainType = plainType;
            Staff = staff;
            Employer = employer;
            Goods = goods;
        }

        /// <summary>True for a branch of one of the city's own businesses: its name, contact and loans the head office's.</summary>
        public bool Branch => Name == null;
    }

    /// <summary>
    /// Uptown's special places (the owner, 24 Oct 2026: "Need more special working buildings ... Make it a new proper
    /// district"), game-free (EconomyCheck's UptownSpecialsChecks): a fixed table of the band's lots built from the first
    /// day, each a copy of one of the game's special services in a structure the game has its layout for, placed where a
    /// district has them: shops and the bank on the avenues, the agencies and the office supplies near the offices of the
    /// second row, the dealership on a big lot, the supermarket and the importer in the far row. Never a lot left open, a
    /// leased office, a landmark's block or the lot a tree of the game's leans over (the checks hold the table to that), and
    /// never a lot a player holds (the made buildings' engine leaves that one ordinary). The same on every machine.
    /// COST: nothing per frame; a look through eleven entries at a load.
    /// </summary>
    public static class SpecialsRules
    {
        public const string Retail = "ba:buildingtype_retail";
        public const string Office = "ba:buildingtype_office";
        public const string Warehouse = "ba:buildingtype_warehouse";
        public const string Special = BuildingsRules.SpecialType;
        public const string ImportType = "ba:businesstype_importexport";
        public const string BankType = "ba:businesstype_bank";

        private const string C = "ba:buildingsize_c", D = "ba:buildingsize_d", M = "ba:buildingsize_m", H = "ba:buildingsize_h";

        // An importer's own goods: a mix from the shelves of the game's importers of finished goods (never their raw
        // materials, whose importers deliver another way), none of them all of one importer's, so no importer's goods are
        // all doubled; the game itself sells some goods from several importers (the paper bag from four).
        private static readonly string[] ImportedGoods =
        {
            "ba:itemname_paperbag", "ba:itemname_bottleofwine", "ba:itemname_whisky", "ba:itemname_cheeseplatter", "ba:itemname_cigar", "ba:itemname_expensiveflower",
            "ba:itemname_cheapflower", "ba:itemname_expensivegift", "ba:itemname_limitededitionbook", "ba:itemname_cheapjewelry", "ba:itemname_umbrella", "ba:itemname_headphones01"
        };

        /// <summary>The table: the order is the tests' order through them.</summary>
        public static readonly SpecialPlace[] All =
        {
            new SpecialPlace("uptown_2_06_001", "VantanderBank", BankType, null, D, 2, Office, Office, SpecialStaff.Desk),
            new SpecialPlace("uptown_2_09_004", "CityWorkforceInc", "ba:businesstype_recruitmentagency", "Summit Talent Partners", C, 2, Retail, Retail, SpecialStaff.Desk),
            new SpecialPlace("uptown_2_09_008", "McCainseMarketing", "ba:businesstype_marketingagency", "Northline Media", C, 2, Retail, Retail, SpecialStaff.Desk),
            new SpecialPlace("uptown_2_10_009", "Kingdom Office", "ba:businesstype_interiorinstallationfirm", "Crestwork Interiors", D, 2, Retail, Retail, SpecialStaff.Desk),
            new SpecialPlace("uptown_3_10_001", "MrScottsOfficeSupplies", "ba:businesstype_officesupplystore", "Paper Trail Office Supply", D, 2, Retail, Retail, SpecialStaff.Desk),
            new SpecialPlace("uptown_2_12_008", "Square Appliances", "ba:businesstype_appliancestore", "Uptown Appliance Hall", C, 1, Retail, Retail, SpecialStaff.Desk),
            new SpecialPlace("uptown_2_09_005", "Hamptons Furniture", "ba:businesstype_furniturestore", "Hearth and Oak Furniture", M, 1, Retail, Retail, SpecialStaff.Desk),
            new SpecialPlace("uptown_3_10_004", "El Gato Food Market", "ba:businesstype_supermarket", "Corner Basket Market", C, 1, Retail, Retail, SpecialStaff.Till, "Corner Basket Manager"),
            new SpecialPlace("uptown_3_08_008", "IndustryCityIntensiveCareUnit", "ba:businesstype_clinic", "Summit Care Clinic", C, 2, Retail, Retail, SpecialStaff.None),
            new SpecialPlace("uptown_2_10_006", "Manhattan Luxury Cars", "ba:businesstype_cardealership", "Crest Motors", M, 1, Retail, Retail, SpecialStaff.Showroom),
            new SpecialPlace("uptown_3_14_001", "BlueStone Imports", ImportType, "Northgate Trading", H, 1, Special, Warehouse, SpecialStaff.Desk, null, ImportedGoods),
        };

        /// <summary>The special place on that lot, or null.</summary>
        public static SpecialPlace At(string lotId)
        {
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < All.Length; i++)
            {
                if (string.Equals(All[i].Lot, lotId, StringComparison.Ordinal))
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The size of the class of lot each structure needs: the plan's lot classes hold the game's sizes A, B, C and F (small),
        /// D, J and K (offices), M, L and N (big) and H, I, P and Q (warehouses).
        /// </summary>
        public static bool Fits(BuildingsLot lot, SpecialPlace place)
        {
            return place != null && BuildingsRules.TakesSize(lot, place.Size);
        }

        /// <summary>
        /// Why the table cannot stand on the plan (English, for the checks), or null: a lot the plan lacks, named twice, of a
        /// class that does not take the structure, not built from the first day (open land, a landmark's block), leased to
        /// another feature's business, or kept empty; an ordinary type that is the special one or a special type with
        /// none; a contact key (name and type) shared by two places, or a place that shares the game's name and is not
        /// a bank's branch; an importer with no goods, or goods named twice.
        /// </summary>
        public static string TableRefusal(IList<BuildingsLot> plan, ICollection<string> built, ICollection<string> leased, IList<SpecialPlace> table)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; table != null && i < table.Count; i++)
            {
                SpecialPlace p = table[i];
                int at = -1;
                for (int j = 0; plan != null && j < plan.Count; j++)
                {
                    at = plan[j].Id == p.Lot ? j : at;
                }
                if (at < 0)
                {
                    return p.Lot + ": no lot of the plan has that id";
                }
                if (!seen.Add(p.Lot))
                {
                    return p.Lot + ": two places on one lot";
                }
                if (!Fits(plan[at], p))
                {
                    return p.Lot + ": its class does not take the size " + p.Size;
                }
                if (!BuildingsRules.Clear(p.Lot) || built == null || !built.Contains(p.Lot))
                {
                    return p.Lot + ": not built from the first day (open land, a landmark's block, or kept empty)";
                }
                if (leased != null && leased.Contains(p.Lot))
                {
                    return p.Lot + ": leased to another feature's business";
                }
                if (string.IsNullOrEmpty(p.Service) || string.IsNullOrEmpty(p.ServiceType) || string.IsNullOrEmpty(p.Type) || string.IsNullOrEmpty(p.PlainType) || p.PlainType == Special)
                {
                    return p.Lot + ": a service, its type and an ordinary type to stand as are wanted";
                }
                if (p.Branch && p.ServiceType != BankType)
                {
                    return p.Lot + ": only a bank's branch keeps the game's name (its contact and loans the head office's); every other place has its own";
                }
                if (!p.Branch && !keys.Add(p.Name + "|" + p.ServiceType))
                {
                    return p.Lot + ": its name and type are another place's, so the two would share one contact";
                }
                if (p.Goods != null && (p.Goods.Length == 0 || p.ServiceType != ImportType || new HashSet<string>(p.Goods, StringComparer.Ordinal).Count != p.Goods.Length))
                {
                    return p.Lot + ": an importer's own goods are a list of distinct goods, and only an importer has them";
                }
                if (p.Type == Special && p.ServiceType != ImportType)
                {
                    return p.Lot + ": the special type is the importers' (the warehouse type's swap would put one up for rent)";
                }
            }
            return null;
        }

        /// <summary>The next place of the table after <paramref name="last"/> (-1 for the first), round again; -1 for an empty table.</summary>
        public static int Next(int last, int count)
        {
            return count <= 0 ? -1 : (last < 0 || last >= count - 1 ? 0 : last + 1);
        }
    }
}
