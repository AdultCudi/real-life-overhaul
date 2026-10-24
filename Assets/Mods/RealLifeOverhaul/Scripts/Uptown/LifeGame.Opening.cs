using System;
using System.Collections.Generic;
using BigAmbitions.Tags;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Helpers;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The opening pass: once a save, after the game's own generator has set the district up, the shops and offices it left
    /// empty past the district's share to let are given businesses of the game's own, as its generator gives them. The
    /// generator opens one only where an importer or wholesaler rival takes the unit's size, and Uptown is no corporation
    /// rival's home district, so about a fifth of its shops stood empty against the 8 % the game keeps to let. Each unit
    /// opened takes a business type whose AI businesses have a layout for its structure, one of them, and a rival of the
    /// business's goods source, every pick by the lot's id and the save's seed, the engine's chance seeded the same way for
    /// the game's start of the business and put back after: the same on every machine of a save. Never a special building,
    /// a leased office, the player's unit or another player's. Warehouses are left as the game lets them: its share to let
    /// for them (30 %) is what stands empty.
    /// COST: nothing per frame; once a save, at the late "game loaded" call behind the loading screen.
    /// </summary>
    internal static partial class LifeGame
    {
        private static readonly string[] OpenTypes = { "ba:buildingtype_retail", BuildingsRules.OfficeType };   // the building types the pass opens units of

        private static readonly List<MadeBuilding> Units = new List<MadeBuilding>(256);
        private static readonly List<MadeBuilding> EmptyOnes = new List<MadeBuilding>(64);
        private static readonly List<string> Kinds = new List<string>(32);
        private static readonly List<AiBusinessDefault> Fits = new List<AiBusinessDefault>(32);

        /// <summary>
        /// The pass over the district's buildings standing; <paramref name="skip"/> says which are another's (a leased office).
        /// How many units were opened, and per building type what was found, into <paramref name="said"/> for the log.
        /// </summary>
        public static int OpenUnits(List<MadeBuilding> standing, Func<MadeBuilding, bool> skip, GameInstance save, out string said)
        {
            said = string.Empty;
            string seed = save != null ? save.seed ?? string.Empty : string.Empty;
            int opened = 0;
            for (int t = 0; t < OpenTypes.Length; t++)
            {
                string type = OpenTypes[t];
                Units.Clear();
                EmptyOnes.Clear();
                for (int i = 0; i < standing.Count; i++)
                {
                    MadeBuilding made = standing[i];
                    BuildingRegistration reg = made != null ? made.Registration : null;
                    Building record = made != null ? made.Building : null;
                    if (reg == null || record == null || record.BuildingType != type || record.SpecialService != null || (skip != null && skip(made))
                        || reg.RentedByPlayer || reg.BuildingOwnedByPlayer || GoingPublic.Foreign(reg))
                    {
                        continue;   // a special building (its service is the game's), a lease's, the player's or another player's
                    }
                    Units.Add(made);
                    string business = reg.businessTypeName;
                    if (reg.AvailableForRent && (string.IsNullOrEmpty(business) || business == BuildingsRules.EmptyBusinessType))
                    {
                        EmptyOnes.Add(made);
                    }
                }
                int share = CompetitionHelper.GetIdealPercentageOfEmptyBuildings(type, DistrictValues.Id);
                int wanted = OfficesRules.ToOpen(Units.Count, EmptyOnes.Count, share);
                EmptyOnes.Sort((a, b) =>
                {
                    int byHash = OfficesRules.OpenOrder(a.Spec.Id, seed).CompareTo(OfficesRules.OpenOrder(b.Spec.Id, seed));
                    return byHash != 0 ? byHash : string.CompareOrdinal(a.Spec.Id, b.Spec.Id);
                });
                int here = 0, none = 0;
                for (int i = 0; i < EmptyOnes.Count && here < wanted; i++)
                {
                    if (Open(EmptyOnes[i], seed, save))
                    {
                        here++;
                    }
                    else
                    {
                        none++;
                    }
                }
                opened += here;
                said += (said.Length > 0 ? "; " : string.Empty) + OfficesRules.Word(type.Replace("ba:buildingtype_", "ba:businesstype_")) + ": " + here + " opened of " + EmptyOnes.Count
                        + " empty to let among " + Units.Count + " (" + share + " % kept to let)" + (none > 0 ? ", " + none + " with no business of the game's whose layout fits" : string.Empty);
            }
            Units.Clear();
            EmptyOnes.Clear();
            return opened;
        }

        // A business of the game's in an empty unit, as its generator opens one; false where no business has a layout for its structure.
        private static bool Open(MadeBuilding made, string seed, GameInstance save)
        {
            BuildingRegistration reg = made.Registration;
            Building record = made.Building;
            AiBusinessDefault pick = Business(record, made.Spec.Id, seed);
            if (pick == null)
            {
                return false;
            }
            string rival = Rival(pick, made.Spec.Id, seed, save, record.BuildingType == "ba:buildingtype_retail");
            UnityEngine.Random.State was = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)NewBuildingsRules.Hash(made.Spec.Id + "|opened|" + seed));
            try
            {
                CompetitionHelper.StartNewCompetitorBusiness(pick.businessTypeName, reg, impactMarket: false, pick, rival);
            }
            finally
            {
                UnityEngine.Random.state = was;
            }
            GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
            return true;
        }

        // The business opened in a unit: of the business types the game lets players and rivals run in its building type,
        // those with an AI business of no corporation whose layout the game has for the unit's structure, one type by the
        // hash, then one of its businesses. The game's own names, never a language's, keep the order alike everywhere.
        private static AiBusinessDefault Business(Building record, string lotId, string seed)
        {
            AiBusinessDefault[] all = CompetitionHelper.GetAllBusinessDefaults();
            var size = new BuildingSizeInfo(record);
            Kinds.Clear();
            for (int i = 0; all != null && i < all.Length; i++)
            {
                AiBusinessDefault d = all[i];
                if (Fitting(d, record, size) && !Kinds.Contains(d.businessTypeName))
                {
                    Kinds.Add(d.businessTypeName);
                }
            }
            Kinds.Sort(string.CompareOrdinal);
            int kind = OfficesRules.Pick(NewBuildingsRules.Hash(lotId + "|kind|" + seed), Kinds.Count);
            if (kind < 0)
            {
                return null;
            }
            Fits.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].businessTypeName == Kinds[kind] && Fitting(all[i], record, size))
                {
                    Fits.Add(all[i]);
                }
            }
            Fits.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.businessName, b.businessName);
                return byName != 0 ? byName : string.CompareOrdinal(a.buildingLayout, b.buildingLayout);
            });
            int one = OfficesRules.Pick(NewBuildingsRules.Hash(lotId + "|business|" + seed), Fits.Count);
            AiBusinessDefault pick = one >= 0 ? Fits[one] : null;
            Kinds.Clear();
            Fits.Clear();
            return pick;
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
    }
}
