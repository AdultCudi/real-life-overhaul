using System;
using System.Collections.Generic;
using BigAmbitions.Tags;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The mix pass: once a save, after the game's own generator has set the districts up, each of the band's districts' shops, offices and homes
    /// stand at exactly the share kept free for the player to rent (MixRules.ToLetShare, a third), and its shops hold a wide
    /// mix of kinds. A shop or office empty past the share is given a business as the game's generator opens one (the game's
    /// own start of a business, of a type whose AI businesses have a layout for its structure, run by a rival of its goods
    /// source), and one of the city's businesses short of the share is let go as the game lets one go; homes are let to
    /// residents, or freed, as the game's own swap of homes does. The kind a unit takes is the one its street holds fewest of
    /// (MixRules.Kind), the game's kinds and the mod's own shops (the ice cream parlour, the tobacconist) where a layout the
    /// game has for the structure shows one of the kind's primary goods; a street with more than two of one kind gives the
    /// rest another. Every pick is by the lot's id and the save's seed, the engine's chance seeded the same way for the game's
    /// own calls and put back after: the same on every machine of a save. Never a special building, a lease's lot, a place
    /// the mod holds, the player's unit or another player's; warehouses stay as the game lets them.
    /// COST: nothing per frame; once a save, at the late "game loaded" call behind the loading screen.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string RetailType = "ba:buildingtype_retail";
        private const string LentPrefix = "RealLifeLent";   // the start of a lent layout's name: no AI business of the game's names it, so its own picks never meet it

        private static readonly string[] BusinessTypes = { RetailType, BuildingsRules.OfficeType };   // the building types whose units hold businesses

        private static readonly List<MadeBuilding> Units = new List<MadeBuilding>(256);
        private static readonly List<MadeBuilding> Free = new List<MadeBuilding>(128);
        private static readonly List<MadeBuilding> Held = new List<MadeBuilding>(256);
        private static readonly List<string> Kinds = new List<string>(48);
        private static readonly List<int> KindCounts = new List<int>(48);
        private static readonly List<AiBusinessDefault> Fits = new List<AiBusinessDefault>(32);
        private static readonly List<BusinessLayoutSet> Lendable = new List<BusinessLayoutSet>(16);
        private static readonly Dictionary<string, int> OnStreet = new Dictionary<string, int>(512, StringComparer.Ordinal);   // "street|type": businesses there
        private static readonly List<AiBusinessDefault> LentDefaults = new List<AiBusinessDefault>(16);   // the mod's shops' hours and signs, kept for the session

        /// <summary>
        /// The pass over <paramref name="units"/> (the district's buildings standing, or those new to a save it lived in);
        /// <paramref name="all"/> is every building standing, whose businesses the streets' mix is counted over;
        /// <paramref name="skip"/> says which are another's. How many units it changed, and what it found for the log.
        /// </summary>
        public static int Mix(List<MadeBuilding> units, List<MadeBuilding> all, Func<MadeBuilding, bool> skip, GameInstance save, out string said)
        {
            said = string.Empty;
            string seed = save != null ? save.seed ?? string.Empty : string.Empty;
            CountStreets(all, skip);
            int changed = 0;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                string before = said;
                said = string.Empty;
                for (int t = 0; t < BusinessTypes.Length; t++)
                {
                    changed += MixType(BusinessTypes[t], units, d, skip, seed, save, ref said);
                }
                changed += MixHomes(units, d, skip, seed, ref said);
                said = before + (before.Length > 0 ? " | " : string.Empty) + DistrictSplit.IdOf(d) + ": " + said;
            }
            Units.Clear();
            Free.Clear();
            Held.Clear();
            OnStreet.Clear();
            return changed;
        }

        // A unit the pass may change: the city's, of the type, not special, not another's, not the player's or another player's.
        private static bool Ours(MadeBuilding made, string type, Func<MadeBuilding, bool> skip)
        {
            BuildingRegistration reg = made != null ? made.Registration : null;
            Building record = made != null ? made.Building : null;
            return reg != null && record != null && record.BuildingType == type && record.SpecialService == null && (skip == null || !skip(made))
                   && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && !GoingPublic.Foreign(reg);
        }

        private static bool Unoccupied(BuildingRegistration reg)
        {
            return string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == BuildingsRules.EmptyBusinessType;
        }

        private static void CountStreets(List<MadeBuilding> all, Func<MadeBuilding, bool> skip)
        {
            OnStreet.Clear();
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i] != null ? all[i].Registration : null;
                if (reg != null && !Unoccupied(reg))
                {
                    Street(reg.StreetName, reg.businessTypeName, 1);
                }
            }
        }

        private static int Street(string street, string type, int add)
        {
            string key = (street ?? string.Empty) + "|" + (type ?? string.Empty);
            OnStreet.TryGetValue(key, out int n);
            n = Math.Max(0, n + add);
            OnStreet[key] = n;
            return n;
        }

        // Shops or offices at the share: opened past it, let go short of it, then a street's surplus of one kind given others.
        private static int MixType(string type, List<MadeBuilding> units, int district, Func<MadeBuilding, bool> skip, string seed, GameInstance save, ref string said)
        {
            Units.Clear();
            Free.Clear();
            Held.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                MadeBuilding made = units[i];
                if (!Ours(made, type, skip) || DistrictSplit.Index(DistrictOfRecord(made)) != district)
                {
                    continue;
                }
                Units.Add(made);
                BuildingRegistration reg = made.Registration;
                if (reg.AvailableForRent && Unoccupied(reg))
                {
                    Free.Add(made);
                }
                else if (!Unoccupied(reg) && reg.HasEstablishedBusiness)
                {
                    Held.Add(made);
                }
            }
            int share = CompetitionHelper.GetIdealPercentageOfEmptyBuildings(type, DistrictSplit.IdOf(district));
            int change = MixRules.Change(Units.Count, Free.Count, share);
            int opened = 0, none = 0, freed = 0, spread = 0;
            if (change > 0)
            {
                ByOrder(Free, "open", seed);
                for (int i = 0; i < Free.Count && opened < change; i++)
                {
                    if (Open(Free[i], seed, save))
                    {
                        opened++;
                    }
                    else
                    {
                        none++;
                    }
                }
            }
            else if (change < 0)
            {
                ByOrder(Held, "free", seed);
                for (int i = 0; i < Held.Count && freed < -change; i++)
                {
                    LetGo(Held[i], seed);
                    freed++;
                }
            }
            if (type == RetailType)
            {
                Held.Clear();
                for (int i = 0; i < Units.Count; i++)
                {
                    BuildingRegistration reg = Units[i].Registration;
                    if (!Unoccupied(reg) && reg.HasEstablishedBusiness && !reg.AvailableForRent)
                    {
                        Held.Add(Units[i]);
                    }
                }
                ByOrder(Held, "spread", seed);
                for (int i = 0; i < Held.Count; i++)
                {
                    BuildingRegistration reg = Held[i].Registration;
                    if (!MixRules.TooMany(Street(reg.StreetName, reg.businessTypeName, 0)))
                    {
                        continue;
                    }
                    string was = reg.businessTypeName;
                    LetGo(Held[i], seed);
                    if (Open(Held[i], seed, save))
                    {
                        spread += reg.businessTypeName != was ? 1 : 0;
                    }
                }
            }
            said += (said.Length > 0 ? "; " : string.Empty) + OfficesRules.Word(type.Replace("ba:buildingtype_", "ba:businesstype_")) + ": " + Units.Count + " unit(s), "
                    + Free.Count + " free before, " + share + " % kept free (" + MixRules.Kept(Units.Count, share) + "), " + opened + " opened, " + freed + " let go"
                    + (type == RetailType ? ", " + spread + " given another kind for their street" : string.Empty)
                    + (none > 0 ? ", " + none + " with no business whose layout fits" : string.Empty);
            return opened + freed + spread;
        }

        // Homes at the share: let to residents past it, freed short of it, as the game's own swap of homes sets them.
        private static int MixHomes(List<MadeBuilding> units, int district, Func<MadeBuilding, bool> skip, string seed, ref string said)
        {
            Units.Clear();
            Free.Clear();
            Held.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                if (Ours(units[i], MixRules.HomeType, skip) && DistrictSplit.Index(DistrictOfRecord(units[i])) == district)
                {
                    Units.Add(units[i]);
                    (units[i].Registration.AvailableForRent ? Free : Held).Add(units[i]);
                }
            }
            int share = CompetitionHelper.GetIdealPercentageOfEmptyBuildings(MixRules.HomeType, DistrictSplit.IdOf(district));
            int change = MixRules.Change(Units.Count, Free.Count, share), done = 0;
            List<MadeBuilding> from = change > 0 ? Free : Held;
            ByOrder(from, change > 0 ? "residents" : "vacate", seed);
            for (int i = 0; i < from.Count && done < Math.Abs(change); i++)
            {
                from[i].Registration.AvailableForRent = change < 0;
                GlobalEvents.onBuildingRegistrationChange?.Invoke(from[i].Registration.Address);
                done++;
            }
            said += (said.Length > 0 ? "; " : string.Empty) + "homes: " + Units.Count + " unit(s), " + Free.Count + " free before, " + share + " % kept free ("
                    + MixRules.Kept(Units.Count, share) + "), " + (change > 0 ? done + " let to residents" : done + " freed");
            return done;
        }

        private static void ByOrder(List<MadeBuilding> list, string step, string seed)
        {
            list.Sort((a, b) =>
            {
                int byHash = MixRules.Order(a.Spec.Id, step, seed).CompareTo(MixRules.Order(b.Spec.Id, step, seed));
                return byHash != 0 ? byHash : string.CompareOrdinal(a.Spec.Id, b.Spec.Id);
            });
        }

        // One of the city's businesses let go as the game lets one go (BusinessHelper.SetBuildingForRent), its street's count with it.
        private static void LetGo(MadeBuilding made, string seed)
        {
            BuildingRegistration reg = made.Registration;
            Street(reg.StreetName, reg.businessTypeName, -1);
            UnityEngine.Random.State was = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)NewBuildingsRules.Hash(made.Spec.Id + "|let go|" + seed));
            try
            {
                BusinessHelper.SetBuildingForRent(reg);
            }
            finally
            {
                UnityEngine.Random.state = was;
            }
            GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
        }

        // A business in an empty unit: the kind its street holds fewest of, of the game's or the mod's own shops; false where none fits.
        private static bool Open(MadeBuilding made, string seed, GameInstance save)
        {
            BuildingRegistration reg = made.Registration;
            Building record = made.Building;
            var size = new BuildingSizeInfo(record);
            AiBusinessDefault[] all = CompetitionHelper.GetAllBusinessDefaults();
            Kinds.Clear();
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (Fitting(all[i], record, size) && !Kinds.Contains(all[i].businessTypeName))
                {
                    Kinds.Add(all[i].businessTypeName);
                }
            }
            IReadOnlyList<DistrictShopKind> shops = record.BuildingType == RetailType ? ShopKinds() : null;
            for (int i = 0; shops != null && i < shops.Count; i++)
            {
                if (LendableCount(shops[i], record, size) > 0 && !Kinds.Contains(shops[i].Type))
                {
                    Kinds.Add(shops[i].Type);
                }
            }
            Kinds.Sort(string.CompareOrdinal);
            KindCounts.Clear();
            for (int i = 0; i < Kinds.Count; i++)
            {
                KindCounts.Add(Street(reg.StreetName, Kinds[i], 0));
            }
            int k = MixRules.Kind(Kinds, KindCounts, made.Spec.Id, seed);
            if (k < 0)
            {
                return false;
            }
            string type = Kinds[k];
            DistrictShopKind shop = null;
            for (int i = 0; shops != null && i < shops.Count; i++)
            {
                shop = shops[i].Type == type ? shops[i] : shop;
            }
            AiBusinessDefault pick = shop != null ? LentDefault(shop, record, size, made.Spec.Id, seed) : GameDefault(type, all, record, size, made.Spec.Id, seed);
            if (pick == null)
            {
                return false;
            }
            string rival = Rival(pick, made.Spec.Id, seed, save, record.BuildingType == RetailType);
            UnityEngine.Random.State was = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)NewBuildingsRules.Hash(made.Spec.Id + "|opened|" + seed));
            try
            {
                CompetitionHelper.StartNewCompetitorBusiness(type, reg, impactMarket: false, pick, rival);
            }
            finally
            {
                UnityEngine.Random.state = was;
            }
            if (shop != null)
            {
                reg.BusinessName = MixRules.ShopName(type, made.Spec.Id, seed) ?? reg.BusinessName;   // the donor's own name is a coffee shop's or a liquor store's
            }
            Street(reg.StreetName, type, 1);
            Changed(reg);
            return true;
        }

        // The mod's own kinds, as the wiring reads them; none after a fault.
        private static IReadOnlyList<DistrictShopKind> ShopKinds()
        {
            Func<IReadOnlyList<DistrictShopKind>> kinds = DistrictShops.Kinds;
            try
            {
                return kinds != null ? kinds() : null;
            }
            catch (Exception e)
            {
                LifeDirector.Logger?.Error(e);
                return null;
            }
        }

        // One of the game's AI businesses of the type whose layout the game has for the structure, by the hash; names alike everywhere.
        private static AiBusinessDefault GameDefault(string type, AiBusinessDefault[] all, Building record, BuildingSizeInfo size, string lotId, string seed)
        {
            Fits.Clear();
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null && all[i].businessTypeName == type && Fitting(all[i], record, size))
                {
                    Fits.Add(all[i]);
                }
            }
            SortDefaults(Fits);
            int one = OfficesRules.Pick(NewBuildingsRules.Hash(lotId + "|business|" + seed), Fits.Count);
            AiBusinessDefault pick = one >= 0 ? Fits[one] : null;
            Fits.Clear();
            return pick;
        }

        private static void SortDefaults(List<AiBusinessDefault> list)
        {
            list.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.businessName, b.businessName);
                return byName != 0 ? byName : string.CompareOrdinal(a.buildingLayout, b.buildingLayout);
            });
        }

        // An AI business the game's generator could open in the unit (GetLayoutsSuitableWithBuildingAndBusinessDefaults):
        // of no corporation, of a type players may create, suited to the building type, its layout there for the structure.
        private static bool Fitting(AiBusinessDefault d, Building record, BuildingSizeInfo size)
        {
            if (d == null || string.IsNullOrEmpty(d.businessTypeName) || string.IsNullOrEmpty(d.buildingLayout) || !string.IsNullOrEmpty(d.corporationRivalId))
            {
                return false;
            }
            BusinessType data = BusinessTypeHelper.GetData(d.businessTypeName);
            return data != null && data.HasTag(TagRef.Businesstag.allowplayercreation) && data.suitableBuildingType == record.BuildingType
                   && BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(d.businessTypeName, size, d.buildingLayout, warnIfNotFound: false) != null;
        }

        // The donor's layouts for the structure that show one of a kind's primary goods, into Lendable, in the order of
        // their names; how many. A kind not furnished yet (its type has no simulation of the game's) has none.
        private static int LendableCount(DistrictShopKind kind, Building record, BuildingSizeInfo size)
        {
            Lendable.Clear();
            BusinessType data = kind != null ? BusinessTypeHelper.GetData(kind.Type) : null;
            if (data == null || data.simulator == null || BusinessLayoutSetHelper.loadingLayouts)
            {
                return 0;
            }
            Dictionary<string, BusinessLayoutSet> all = BusinessLayoutSetHelper.GetAllBusinessLayoutSets();
            if (all == null)
            {
                return 0;
            }
            foreach (KeyValuePair<string, BusinessLayoutSet> each in all)
            {
                BusinessLayoutSet set = each.Value;
                if (set != null && set.BusinessType == kind.Donor && set.BuildingSize == record.BuildingSize && set.BuildingVersion == record.BuildingVersion
                    && !set.LayoutName.StartsWith(LentPrefix, StringComparison.Ordinal) && Shows(set, kind.Primary))
                {
                    Lendable.Add(set);
                }
            }
            Lendable.Sort((a, b) => string.CompareOrdinal(a.LayoutName, b.LayoutName));
            return Lendable.Count;
        }

        // True when a layout's furniture sells one of the goods (what an AI business of it sells: BuildingRegistration.GetListOfItemsForSale).
        private static bool Shows(BusinessLayoutSet set, string[] goods)
        {
            for (int i = 0; set.Items != null && i < set.Items.Count; i++)
            {
                BusinessLayoutSets.Item item = set.Items[i];
                string sold = item != null && item.playerItemPurchaserSettings != null && item.playerItemPurchaserSettings.enabled ? item.playerItemPurchaserSettings.itemName : null;
                if (sold != null && Array.IndexOf(goods, sold) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // A kind's business for the unit: one of the donor's layouts that shows its goods filed under the kind's type by a name
        // no AI business of the game's has, and an AI business of the donor with that layout lending its hours and sign, its
        // copy naming the lent layout and the kind's type. Null when none can be lent.
        private static AiBusinessDefault LentDefault(DistrictShopKind kind, Building record, BuildingSizeInfo size, string lotId, string seed)
        {
            int count = LendableCount(kind, record, size);
            int one = OfficesRules.Pick(NewBuildingsRules.Hash(lotId + "|layout|" + seed), count);
            BusinessLayoutSet set = one >= 0 ? Lendable[one] : null;
            Lendable.Clear();
            if (set == null)
            {
                return null;
            }
            string name = LentPrefix + set.LayoutName;
            if (BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(kind.Type, size, name, warnIfNotFound: false) == null)
            {
                Dictionary<string, BusinessLayoutSet> all = BusinessLayoutSetHelper.GetAllBusinessLayoutSets();
                var lent = new BusinessLayoutSet
                {
                    Items = set.Items, interiorDesigns = set.interiorDesigns, BusinessType = kind.Type, BuildingSize = set.BuildingSize, BuildingVersion = set.BuildingVersion,
                    LayoutName = name, buildNumber = set.buildNumber, requiredModIds = set.requiredModIds
                };
                string key = LayoutKey(kind.Type, set.BuildingSize, set.BuildingVersion, name);
                all[key] = lent;
                if (BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(kind.Type, size, name, warnIfNotFound: false) != lent)
                {
                    all.Remove(key);
                    return null;   // the game files its layouts another way now
                }
            }
            for (int i = 0; i < LentDefaults.Count; i++)
            {
                if (LentDefaults[i] != null && LentDefaults[i].businessTypeName == kind.Type && LentDefaults[i].buildingLayout == name)
                {
                    return LentDefaults[i];
                }
            }
            Fits.Clear();
            AiBusinessDefault[] donors = CompetitionHelper.GetBusinessDefaultsByType(kind.Donor);
            for (int i = 0; donors != null && i < donors.Length; i++)
            {
                if (donors[i] != null && donors[i].buildingLayout == set.LayoutName && string.IsNullOrEmpty(donors[i].corporationRivalId))
                {
                    Fits.Add(donors[i]);
                }
            }
            SortDefaults(Fits);
            AiBusinessDefault from = Fits.Count > 0 ? Fits[0] : null;
            Fits.Clear();
            if (from == null)
            {
                return null;
            }
            AiBusinessDefault copy = UnityEngine.Object.Instantiate(from);   // kept for the session in LentDefaults; never among the game's own
            copy.hideFlags = HideFlags.DontUnloadUnusedAsset;
            copy.businessTypeName = kind.Type;
            copy.buildingLayout = name;
            LentDefaults.Add(copy);
            return copy;
        }

        // The rival that runs it, as the generator gives a unit to an importer or a wholesaler: of the business's goods source
        // for a shop, either for an office (whose goods the game never asks for); none where the save has no such rival.
        private static string Rival(AiBusinessDefault d, string lotId, string seed, GameInstance save, bool retail)
        {
            string[] rivals = save == null ? null
                              : !retail ? (save.importRivalIds != null && save.importRivalIds.Length > 0 ? save.importRivalIds : save.wholesaleRivalIds)
                              : d.goodsSource == AiBusinessGoodsSource.Wholesale ? save.wholesaleRivalIds : save.importRivalIds;
            int at = OfficesRules.Pick(NewBuildingsRules.Hash(lotId + "|rival|" + seed), rivals != null ? rivals.Length : 0);
            return at >= 0 ? rivals[at] ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// The census of the district's units by kind: of each building type (shops, offices, homes) the city's units and those
        /// free to rent, into the counts; each business's type with its count into <paramref name="kinds"/> (cleared).
        /// </summary>
        public static void MixCensus(List<MadeBuilding> standing, Func<MadeBuilding, bool> skip, int[] units, int[] free, Dictionary<string, int> kinds)
        {
            kinds.Clear();
            string[] types = { RetailType, BuildingsRules.OfficeType, MixRules.HomeType };
            for (int t = 0; t < types.Length; t++)
            {
                units[t] = free[t] = 0;
                for (int i = 0; i < standing.Count; i++)
                {
                    if (!Ours(standing[i], types[t], skip))
                    {
                        continue;
                    }
                    BuildingRegistration reg = standing[i].Registration;
                    units[t]++;
                    free[t] += reg.AvailableForRent && (t == 2 || Unoccupied(reg)) ? 1 : 0;
                    if (t < 2 && !Unoccupied(reg))
                    {
                        kinds.TryGetValue(reg.businessTypeName, out int n);
                        kinds[reg.businessTypeName] = n + 1;
                    }
                }
            }
        }
    }
}
