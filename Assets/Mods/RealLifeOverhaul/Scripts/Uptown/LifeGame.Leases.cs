using System;
using System.Collections.Generic;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Helpers;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The leased offices on the game's side: a lessee's business moved into its building by the game's own start of a
    /// business (CompetitionHelper.StartNewCompetitorBusiness), as the game opens one of its own, with an AI business of the
    /// type it borrows from lending its hours, layout and sign; that type's layouts filed under the lessee's type in the game's
    /// own cache, so the business is furnished, staffed and valued as the game's are; and the building held off the market.
    /// Nothing here runs a frame: once at a load, for at most eight buildings.
    /// </summary>
    internal static partial class LifeGame
    {
        private const int IncomeDays = 7;   // days of income an AI business of the game's starts its valuation from (the game keeps up to 20; fewer than 14 is never closed for low income)
        private const string LeaseHold = "lease_office_";   // a leased office's hold in the special buildings' book, then its lessee's word and its index

        private static readonly List<BusinessLayoutSet> Borrowed = new List<BusinessLayoutSet>(8);
        private static readonly List<AiBusinessDefault> Defaults = new List<AiBusinessDefault>(8);
        private static readonly List<string> Claims = new List<string>(BuildingsRules.LeasedOffices);   // the special buildings' entries the leases filed at this load

        /// <summary>
        /// A lease's business in its building: there already (the lease's business type at its own lot, whatever its name says,
        /// <paramref name="renamed"/> when its name or description was put back to the lease's), or moved in where the building
        /// is new at this load or stands empty to let and nobody's (<see cref="BuildingsRules.LeaseOf"/>); then held off the
        /// market. Never the player's, nor another player's in a Going Public session. Null when it stands, else why not;
        /// <paramref name="movedIn"/> when it moved in now.
        /// </summary>
        public static string Lease(MadeBuilding made, DistrictLease lease, int index, GameInstance save, out bool movedIn, out bool renamed)
        {
            movedIn = renamed = false;
            BuildingRegistration reg = made != null ? made.Registration : null;
            Building record = made != null ? made.Building : null;
            if (reg == null || record == null)
            {
                return "its records are not there";
            }
            LeaseVerdict verdict = BuildingsRules.LeaseOf(GoingPublic.Foreign(reg), reg.RentedByPlayer || reg.BuildingOwnedByPlayer, record.BuildingType, reg.businessTypeName,
                reg.BusinessName, reg.BusinessDescription, made.NewRegistration, reg.AvailableForRent, lease.BusinessType, lease.Name, lease.DescriptionKey, out string why);
            if (verdict == LeaseVerdict.Refused)
            {
                return why;
            }
            why = Borrow(lease.LayoutsFrom, lease.BusinessType);
            if (why != null)
            {
                return why;
            }
            if (verdict == LeaseVerdict.MoveIn)
            {
                AiBusinessDefault from = Lender(lease.LayoutsFrom, record, index);
                if (from == null)
                {
                    return "no business of " + lease.LayoutsFrom + " has a layout for " + NewBuildingsRules.StructureName(record.BuildingSize, record.BuildingVersion);
                }
                MoveIn(reg, lease, from, save);
                movedIn = true;
            }
            else if (verdict == LeaseVerdict.Renamed)
            {
                Rename(reg, lease);
                renamed = true;
            }
            Claim(made, lease, index);
            return null;
        }

        // A lease's business whose name or description is not the lease's, as a text changed since it moved in: the lease's
        // again, drawn and raised as MoveIn's are.
        private static void Rename(BuildingRegistration reg, DistrictLease lease)
        {
            reg.BusinessName = lease.Name;
            reg.BusinessDescription = lease.DescriptionKey;
            Changed(reg);
        }

        // The game's own start of a business in the building, seeded so every machine starts it alike, then the lessee's own:
        // its name, BizMan's description, no rival of the game's as its owner (the card and BizMan then show neither), and its
        // days' income, which the game's valuation averages and its daily pass never closes it for while it has fewer than 14.
        private static void MoveIn(BuildingRegistration reg, DistrictLease lease, AiBusinessDefault from, GameInstance save)
        {
            UnityEngine.Random.State was = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)NewBuildingsRules.Hash(lease.BusinessType + "|" + lease.Name + "|" + (save != null ? save.seed : string.Empty)));
            try
            {
                CompetitionHelper.StartNewCompetitorBusiness(lease.BusinessType, reg, impactMarket: false, from, string.Empty, setUpFirstDailyIncome: false);
            }
            finally
            {
                UnityEngine.Random.state = was;
            }
            reg.BusinessName = lease.Name;
            reg.BusinessDescription = lease.DescriptionKey;
            reg.businessOwnerRivalId = string.Empty;
            reg.AvailableForRent = false;
            reg.dailyIncomes = new List<float>(IncomeDays);
            for (int d = 0; d < IncomeDays; d++)
            {
                reg.dailyIncomes.Add(lease.DailyIncome);
            }
            Changed(reg);
        }

        // A registration changed under the game's own drawing of it: the sign drawn again (the game's start drew it before the
        // name was the lessee's), and the event the game raises when a registration changes (the marker, the lights, the map's search).
        private static void Changed(BuildingRegistration reg)
        {
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            city?.FindCityBuildingController(reg.Address)?.UpdateSign();
            GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
        }

        // The AI business of the lending type whose layout the game has for the building's structure (filed under the lessee's
        // type by Borrow), in the order of their names, the one the lease's index picks: the same on every machine.
        private static AiBusinessDefault Lender(string type, Building record, int index)
        {
            Defaults.Clear();
            AiBusinessDefault[] all = CompetitionHelper.GetBusinessDefaultsByType(type);
            var size = new BuildingSizeInfo(record);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                AiBusinessDefault d = all[i];
                if (d != null && !string.IsNullOrEmpty(d.buildingLayout) && BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(type, size, d.buildingLayout, warnIfNotFound: false) != null)
                {
                    Defaults.Add(d);
                }
            }
            if (Defaults.Count == 0)
            {
                return null;
            }
            Defaults.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.businessName, b.businessName);
                return byName != 0 ? byName : string.CompareOrdinal(a.buildingLayout, b.buildingLayout);
            });
            AiBusinessDefault pick = Defaults[Math.Abs(index) % Defaults.Count];
            Defaults.Clear();
            return pick;
        }

        /// <summary>
        /// The business type <paramref name="to"/> laid out as <paramref name="from"/>: each of the game's layout sets of
        /// <paramref name="from"/> filed under <paramref name="to"/> as well, in the game's own cache of them, so the game finds a
        /// layout for an AI business of <paramref name="to"/> (its furniture when the player goes in, its staff, its valuation).
        /// The game makes no business of <paramref name="to"/> itself, having no AI business of that type, so its own picks are
        /// as before. Looked at once a load; null when every set is there, else why not.
        /// </summary>
        private static string Borrow(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                return "it borrows no layouts";
            }
            if (BusinessLayoutSetHelper.loadingLayouts)
            {
                return "the game's business layouts are still loading";
            }
            Dictionary<string, BusinessLayoutSet> all = BusinessLayoutSetHelper.GetAllBusinessLayoutSets();
            if (all == null)
            {
                return "the game's business layouts could not be read";
            }
            Borrowed.Clear();
            foreach (KeyValuePair<string, BusinessLayoutSet> each in all)
            {
                if (each.Value != null && each.Value.BusinessType == from)
                {
                    Borrowed.Add(each.Value);
                }
            }
            if (Borrowed.Count == 0)
            {
                return "the game has no layout of " + from;
            }
            int filed = 0;
            for (int i = 0; i < Borrowed.Count; i++)
            {
                BusinessLayoutSet set = Borrowed[i];
                var size = new BuildingSizeInfo(set);
                if (BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(to, size, set.LayoutName, warnIfNotFound: false) != null)
                {
                    continue;   // filed at an earlier load of this run
                }
                var lent = new BusinessLayoutSet
                {
                    Items = set.Items, interiorDesigns = set.interiorDesigns, BusinessType = to, BuildingSize = set.BuildingSize, BuildingVersion = set.BuildingVersion,
                    LayoutName = set.LayoutName, buildNumber = set.buildNumber, requiredModIds = set.requiredModIds
                };
                string key = LayoutKey(to, set.BuildingSize, set.BuildingVersion, set.LayoutName);
                all[key] = lent;
                if (BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(to, size, set.LayoutName, warnIfNotFound: false) != lent)
                {
                    all.Remove(key);
                    Borrowed.Clear();
                    return "the game files its layouts another way now (a game update?)";
                }
                filed++;
            }
            if (filed > 0)
            {
                LifeDirector.Logger?.Info("Uptown: the business type " + to + " laid out as " + from + ": " + filed + " of the game's layouts filed under it.");
            }
            Borrowed.Clear();
            return null;
        }

        // The key the game files a layout set under (BusinessLayoutSetHelper.CreateLayoutSetKey); Borrow checks the game finds it there.
        private static string LayoutKey(string type, string size, int version, string layout)
        {
            return type + "|" + size + "|" + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (layout ?? string.Empty).ToLowerInvariant();
        }

        // The leased office held off the market in the special buildings' book while the district lives: refused for rent,
        // sale and takeover in the game's pages (the game's own takeover asks no rival of a business with none), its own sign
        // kept, never anything in the save. On the open map the game's own row of its business type lights it.
        private static void Claim(MadeBuilding made, DistrictLease lease, int index)
        {
            string own = (lease.Owner ?? "lease") + "_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (SpecialBuildings.SetNamed(LeaseHold + own, made.Spec.Street, made.Spec.Number, SpecialKind.Other, lease.Name, null, () => DistrictBuildings.Lives)
                && !Claims.Contains(LeaseHold + own))
            {
                Claims.Add(LeaseHold + own);
            }
        }

        /// <summary>The leases' holds in the special buildings' book given up (a new load files them again).</summary>
        public static void Unclaim()
        {
            for (int i = 0; i < Claims.Count; i++)
            {
                SpecialBuildings.Remove(Claims[i]);
            }
            Claims.Clear();
        }

        /// <summary>True when a building's registration is a lease's business: of the lessee's type, not to let and not the player's (its name is text, never asked).</summary>
        public static bool Leased(MadeBuilding made, DistrictLease lease)
        {
            BuildingRegistration reg = made != null ? made.Registration : null;
            return reg != null && lease != null && BuildingsRules.LeasedBy(reg.businessTypeName, reg.AvailableForRent, reg.RentedByPlayer, lease.BusinessType);
        }
    }
}
