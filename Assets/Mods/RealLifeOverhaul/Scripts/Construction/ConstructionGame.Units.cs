using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A unit of the player's a refit can be done in: a business, a home or an empty unit they rent or own, theirs in a session.</summary>
    public struct UnitView
    {
        public string Street;
        public int Number;
        public string Name;       // its business's name or its address, as words
        public string District;
        public int Sqm;           // the building's floor area, as the refit's works are sized
        public string Type;       // the game's building type
        public string Size;       // the game's size and version: the structure the firm's designs are made for
        public int Version;
        public string Business;   // its business's type, or the game's empty business for a home or an empty unit: the firm's folder
        public bool Trading;      // an established business runs in it: it closes for the works
        public bool Owned;        // the building is the player's own
        public Vector3 Door;      // its first door, for the nearest unit to the player
    }

    /// <summary>
    /// The construction game's reads and writes of a unit's interior: the player's units of every kind, the designs the game's
    /// interior installation firms offer for one (InteriorInstallationFirmHelper, the firm's own lists), what installing one
    /// costs as the game's own contract works it out, the game's own installation contract itself, and an AI business's
    /// layouts among the game's (an AI business's interior is laid from its layout whenever the player enters,
    /// BuildingManager). Nothing here keeps state. COST: nothing per frame; on a page, a press or a handover.
    /// </summary>
    internal static partial class ConstructionGame
    {
        private const string EmptyBusiness = "ba:businesstype_empty";

        /// <summary>The player's units a refit can be done in, in the save's order: every kind of unit with a door on the street.</summary>
        internal static void Units(List<UnitView> into)
        {
            into.Clear();
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (UnitOf(all[i], out UnitView view))
                {
                    into.Add(view);
                }
            }
        }

        /// <summary>One unit of the player's by its address, as <see cref="Units"/> reads it; false for none.</summary>
        internal static bool Unit(string street, int number, out UnitView view)
        {
            view = default(UnitView);
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            return UnitOf(reg, out view);
        }

        // A unit the player rents or owns and uses (the game's installation firm installs only where the player is the tenant),
        // theirs in a session, of one of the four types the construction game knows, with a door; never a Hamptons house or a
        // special service's building.
        private static bool UnitOf(BuildingRegistration reg, out UnitView view)
        {
            view = default(UnitView);
            if (reg == null || reg.Address == null || !reg.RentedByPlayer || !GoingPublic.Mine(reg))
            {
                return false;
            }
            Building b = GameRegistrations.BuildingOf(reg);
            if (b == null || b.IsHamptonsHouse() || b.SpecialService != null || BuildCatalogue.UseOf(b.BuildingType) < 0)
            {
                return false;
            }
            CityBuildingController c = CivicCity.Find(b.StreetName, b.StreetNumber);
            if (!CivicCity.HasDoor(c))
            {
                return false;
            }
            bool trading = reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.businessTypeName) && reg.businessTypeName != EmptyBusiness;
            view = new UnitView
            {
                Street = b.StreetName, Number = b.StreetNumber, Name = Words(reg), District = b.Neighbourhood, Sqm = b.totalSqm, Type = b.BuildingType, Size = b.BuildingSize,
                Version = b.BuildingVersion, Business = string.IsNullOrEmpty(reg.businessTypeName) ? EmptyBusiness : reg.businessTypeName, Trading = trading,
                Owned = reg.BuildingOwnedByPlayer, Door = c.entranceDoors[0].doorTransform.position
            };
            return true;
        }

        /// <summary>
        /// The designs the game's interior installation firms offer for a unit, in the order of their names (the firm reads its
        /// folder in the disk's order, which differs between machines): its business type's, or a home's or an empty unit's.
        /// </summary>
        internal static void Designs(UnitView u, List<string> into)
        {
            into.Clear();
            string[] names = InteriorInstallationFirmHelper.GetInteriorDesignsNamesForBuilding(u.Type, new BuildingSizeInfo(u.Size, u.Version), u.Business);
            for (int i = 0; names != null && i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    into.Add(names[i]);
                }
            }
            into.Sort(StringComparer.Ordinal);
        }

        /// <summary>
        /// What installing one of the firm's designs in a unit would cost now, as the game's own contract works it out: the
        /// firm's fee for the unit's size, the design's pieces at the game's market prices, and what the pieces there now sell
        /// for. False when the design cannot be read. A read of the design's file: on a page or a press only.
        /// </summary>
        internal static bool FurnishFigures(UnitView u, string design, out long fee, out long pieces, out long sold)
        {
            fee = pieces = sold = 0L;
            BusinessLayoutSet set = string.IsNullOrEmpty(design) ? null
                : InteriorInstallationFirmHelper.GetInteriorDesignLayout(design, u.Type, new BuildingSizeInfo(u.Size, u.Version), u.Business);
            if (set == null || set.Items == null)
            {
                return false;
            }
            fee = (long)Mathf.Round(InteriorInstallationFirmHelper.GetInstallationFee(u.Size));
            double sum = 0.0;
            for (int i = 0; i < set.Items.Count; i++)
            {
                if (set.Items[i] != null && !string.IsNullOrEmpty(set.Items[i].itemName))
                {
                    sum += ItemHelper.GetDefaultMarketPrice(set.Items[i].itemName);
                }
            }
            pieces = (long)Math.Round(sum);
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(new Address(u.Street, u.Number));
            double old = 0.0;
            if (reg != null && reg.itemInstances != null)
            {
                foreach (ItemInstance item in reg.itemInstances.Values)
                {
                    old += item != null ? item.GetSellingPrice() : 0f;
                }
            }
            sold = (long)Math.Round(old);
            return true;
        }

        /// <summary>
        /// The design installed through the game's own interior installation contract, dated today, by the firm that installs in
        /// a building of the unit's type: the game pays for it, sells what is there, lays the design and says so, now when the
        /// player is not inside the unit, else at its next day's pass as for any contract of its own. False, with why, when no
        /// firm of the game's installs in such a building. <paramref name="now"/>: installed at once.
        /// </summary>
        internal static bool Furnish(UnitView u, string design, out bool now, out string why)
        {
            now = false;
            why = null;
            GameInstance save = SaveGameManager.Current;
            Building firm = FirmFor(u.Type);
            if (save == null || firm == null || firm.Address == null)
            {
                why = "no interior installation firm of the game's installs in a building of its type";
                return false;
            }
            if (save.interiorInstallationFirmContracts == null)
            {
                save.interiorInstallationFirmContracts = new List<InteriorInstallationFirmContract>();
            }
            var at = new Address(u.Street, u.Number);
            var order = new InteriorInstallationFirmContract
            {
                interiorInstallationFirmAddress = firm.Address, addressToDoTheInstallation = at, designName = design, businessTypeName = u.Business, dayOfInstallation = save.Day
            };
            save.interiorInstallationFirmContracts.Add(order);
            now = !(save.CurrentStreetNumber == u.Number && save.CurrentStreetName == u.Street);
            if (now)
            {
                order.DoInstallation();
            }
            return true;
        }

        // The firm that installs in a building of a type, as the game's own helper finds it (InteriorInstallationFirmHelper.GetContactByBuildingType):
        // the first of the city's buildings whose special service is an installation firm for that type, never one a DLC keeps. A walk on a handover only.
        private static Building FirmFor(string buildingType)
        {
            List<Building> all = BuildingHelper.allBuildings;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                Building b = all[i];
                if (b != null && !GameDlc.Gated(b) && b.SpecialService != null && b.SpecialService.settings is InteriorInstallationFirmSettings firm && firm.buildingTypesThatCanInstall != null
                    && firm.buildingTypesThatCanInstall.Contains(buildingType))
                {
                    return b;
                }
            }
            return null;
        }

        /// <summary>
        /// The game's layouts an AI business of the city's could wear at its size: those of its type's AI businesses of no
        /// corporation that the game has at the building's size, in the order of their names, into <paramref name="into"/>
        /// (cleared); <paramref name="current"/> its own among them, or -1. False for no AI business there, one of the player's
        /// or another player's, or a corporation's branded store, which keeps its own.
        /// </summary>
        internal static bool AiLayouts(string street, int number, List<string> into, out int current)
        {
            into.Clear();
            current = -1;
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            Building b = reg != null ? GameRegistrations.BuildingOf(reg) : null;
            if (b == null || reg.RentedByPlayer || reg.BuildingOwnedByPlayer || !reg.HasEstablishedBusiness || string.IsNullOrEmpty(reg.Layout) || GoingPublic.Foreign(reg))
            {
                return false;
            }
            AiBusinessDefault[] defaults = CompetitionHelper.GetBusinessDefaultsByType(reg.businessTypeName);
            var size = new BuildingSizeInfo(b);
            for (int i = 0; defaults != null && i < defaults.Length; i++)
            {
                AiBusinessDefault d = defaults[i];
                if (d == null || string.IsNullOrEmpty(d.buildingLayout))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(d.corporationRivalId))
                {
                    if (d.buildingLayout == reg.Layout)
                    {
                        into.Clear();
                        return false;   // a chain's store keeps its branded interior
                    }
                    continue;
                }
                if (!into.Contains(d.buildingLayout) && BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(reg.businessTypeName, size, d.buildingLayout, warnIfNotFound: false) != null)
                {
                    into.Add(d.buildingLayout);
                }
            }
            into.Sort(StringComparer.Ordinal);
            current = into.IndexOf(reg.Layout);
            return true;
        }

        /// <summary>An AI business of the city's given another of the game's layouts for its type: its interior from the next time it is entered. False if it is no longer one.</summary>
        internal static bool SetAiLayout(string street, int number, string layout)
        {
            BuildingRegistration reg = string.IsNullOrEmpty(street) || string.IsNullOrEmpty(layout) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            if (reg == null || reg.RentedByPlayer || reg.BuildingOwnedByPlayer || !reg.HasEstablishedBusiness || GoingPublic.Foreign(reg))
            {
                return false;
            }
            reg.Layout = layout;
            return true;
        }

        /// <summary>The layout an AI business wears now, or null for no registration there: for a test's line.</summary>
        internal static string LayoutOf(string street, int number)
        {
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            return reg != null ? reg.Layout : null;
        }

        /// <summary>True while one of the game's installation contracts waits to be done at an address (the player inside on its day): for a test's line.</summary>
        internal static bool InstallationWaiting(string street, int number)
        {
            List<InteriorInstallationFirmContract> all = SaveGameManager.Current != null ? SaveGameManager.Current.interiorInstallationFirmContracts : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                Address a = all[i] != null ? all[i].addressToDoTheInstallation : null;
                if (a != null && a.streetNumber == number && a.streetName == street)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The pieces in a unit now, and the pieces a design of the firm's for it holds (-1 when it cannot be read): for a test's line.</summary>
        internal static void PiecesOf(UnitView u, string design, out int inUnit, out int inDesign)
        {
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(new Address(u.Street, u.Number));
            inUnit = reg != null && reg.itemInstances != null ? reg.itemInstances.Count : 0;
            BusinessLayoutSet set = string.IsNullOrEmpty(design) ? null
                : InteriorInstallationFirmHelper.GetInteriorDesignLayout(design, u.Type, new BuildingSizeInfo(u.Size, u.Version), u.Business);
            inDesign = set != null && set.Items != null ? set.Items.Count : -1;
        }
    }
}
