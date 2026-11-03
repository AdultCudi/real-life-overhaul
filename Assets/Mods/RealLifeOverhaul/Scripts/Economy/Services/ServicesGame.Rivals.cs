using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Entities;
using Helpers;
using InteriorDesign;
using RealLifeOverhaul.Economy.Ventures;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The buyers and sellers of a sale as the game has them: the game's own rivals (RivalsHelper), who hold businesses by
    /// BuildingRegistration.businessOwnerRivalId and run them through the game's AI; a business's staff; and its quality as
    /// the game scores it. A rival takes over a business it bought as the game opens a rival's business
    /// (CompetitionHelper.StartNewCompetitorBusiness), passes one to another rival by that one field, and sells one to the
    /// player as BizMan's own takeover does (BizManPresentation.OvertakeBusiness). Called on the day event or a press only.
    /// </summary>
    internal static partial class ServicesGame
    {
        private const string TakeoverKey = "ba:transaction_deposit";   // the game's own, for a business bought from a rival in BizMan

        private static readonly List<AiBusinessDefault> Fitting = new List<AiBusinessDefault>(8);
        private static readonly List<EmployeeInstance> Workers = new List<EmployeeInstance>(16);

        /// <summary>True while the game's rivals play (its own difficulty setting).</summary>
        public static bool RivalsOn
        {
            get
            {
                try
                {
                    return Save != null && RivalsHelper.IsFeatureEnabled;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        // ---------------------------------------------------------------- quality

        /// <summary>A business's quality as a buyer sees it: the game's interior score and its district's least, customer satisfaction, the staff's skill.</summary>
        public static void Quality(BuildingRegistration reg, ref SaleInputs i)
        {
            i.Quality = true;
            i.Interior = InteriorScore(reg);
            i.InteriorWant = InteriorWant(reg);
            i.Rating = reg.satisfaction != null ? Mathf.Clamp(reg.satisfaction.overall, 0, 100) : -1;
            StaffSkill(reg, out i.Skill, out i.Staff);
        }

        // The game's interior score (InteriorScoreCalculator, as its quests and its customers read it); -1 with no design or none it can score.
        private static int InteriorScore(BuildingRegistration reg)
        {
            try
            {
                if (reg.interiorDesigns == null || reg.interiorDesigns.Count == 0)
                {
                    return -1;
                }
                int score = InteriorScoreCalculator.GetInteriorScorePercentage(reg.interiorDesigns);
                return score < 0 || score > 100 ? -1 : score;   // no material it knows gives no number at all
            }
            catch (Exception)
            {
                return -1;
            }
        }

        // The least interior score the district's customers want (the game's InteriorDesignCustomerDemand); 0 where it has none.
        private static int InteriorWant(BuildingRegistration reg)
        {
            try
            {
                string district = GameRegistrations.District(reg);
                BigAmbitions.Neighborhoods.NeighborhoodData data = string.IsNullOrEmpty(district) ? null : NeighborhoodHelper.GetData(district);
                return data != null ? Mathf.Clamp(data.minimumInteriorScore, 0, 100) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // The staff's mean main skill and their number (the player not among them).
        private static void StaffSkill(BuildingRegistration reg, out int skill, out int count)
        {
            skill = count = 0;
            if (reg.Address == null)
            {
                return;
            }
            EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address }, Workers);
            EmployeeInstance self = Self();
            int sum = 0;
            for (int i = 0; i < Workers.Count; i++)
            {
                EmployeeInstance e = Workers[i];
                if (e != null && !e.IsCandidate && !ReferenceEquals(e, self))
                {
                    sum += SkillOf(e);
                    count++;
                }
            }
            Workers.Clear();
            skill = count > 0 ? Mathf.RoundToInt(sum / (float)count) : 0;
        }

        private static int SkillOf(EmployeeInstance e)
        {
            return e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null
                ? Mathf.Clamp(Mathf.RoundToInt(e.characterData.skills[0].value), 0, 100)
                : 0;
        }

        // ---------------------------------------------------------------- who could buy

        /// <summary>
        /// Everybody who could make an offer for a business of the player's: the game's rivals who could run it in that building
        /// (a building the player rents only: one they own stays theirs), its most skilled employee for its staff, the listed
        /// companies of its trade and the save's investors (<paramref name="investors"/>, by name).
        /// </summary>
        public static void Bidders(BuildingRegistration reg, IList<string> investors, List<SaleBidder> into)
        {
            into.Clear();
            if (!IsSellable(reg))
            {
                return;
            }
            Building building = GameRegistrations.BuildingOf(reg);
            if (building != null && !reg.BuildingOwnedByPlayer)
            {
                RivalBidders(reg, building, reg.businessTypeName, null, null, into);
            }
            StaffBidder(reg, into);
            SaleRules.Chains(reg.businessTypeName, (int)VenturesCatalogue.SectorOf(reg.businessTypeName), into);
            SaleRules.Investors(investors, into);
        }

        // The game's rivals who could run a business of this kind in this building: one of the game's businesses for it is
        // theirs to run (the default's own rival, a special rival for a wildcard, or one of the wholesale or import rivals by its
        // goods), they are not beaten, and they run something already. 'kept' asks for that one business (a rival's sale).
        private static void RivalBidders(BuildingRegistration reg, Building building, string type, AiBusinessDefault kept, string except, List<SaleBidder> into)
        {
            if (!RivalsOn)
            {
                return;
            }
            Fitting.Clear();
            if (kept != null)
            {
                Fitting.Add(kept);
            }
            else
            {
                Defaults(reg, building, type, Fitting);
            }
            if (Fitting.Count == 0)
            {
                return;
            }
            List<RivalData> rivals = RivalsHelper.GetAllRivalData();
            for (int r = 0; rivals != null && r < rivals.Count; r++)
            {
                RivalData rival = rivals[r];
                if (rival == null || string.IsNullOrEmpty(rival.id) || string.Equals(rival.id, except, StringComparison.Ordinal) || rival.ownedBusinesses == null
                    || rival.ownedBusinesses.Count == 0 || RivalsHelper.IsRivalDefeated(rival.id))
                {
                    continue;
                }
                bool runs = false;
                for (int d = 0; d < Fitting.Count && !runs; d++)
                {
                    runs = Runs(Fitting[d], rival.id);
                }
                if (runs)
                {
                    into.Add(new SaleBidder
                    {
                        Kind = (int)BuyerKind.Rival, Id = rival.id, Name = rival.rivalName ?? string.Empty, Purse = SaleRules.RivalPurse(Weekly(rival)), Count = rival.ownedBusinesses.Count
                    });
                }
            }
            Fitting.Clear();
        }

        // A rival's weekly takings as the game's Rivals app shows them; nothing where it cannot be read.
        private static float Weekly(RivalData rival)
        {
            try
            {
                return Mathf.Max(0f, rival.WeeklyIncome);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        // The game's businesses of a kind that have a layout for this building (CompetitionHelper.GetBuildingMatchingLayout's test), in its own order.
        private static void Defaults(BuildingRegistration reg, Building building, string type, List<AiBusinessDefault> into)
        {
            if (string.IsNullOrEmpty(type) || type == EmptyType || RivalsHelper.IgnoredAddresses.Contains(reg.Address))
            {
                return;
            }
            AiBusinessDefault[] defaults = CompetitionHelper.GetBusinessDefaultsByType(type);
            var size = new BuildingSizeInfo(building);
            for (int i = 0; defaults != null && i < defaults.Length; i++)
            {
                AiBusinessDefault def = defaults[i];
                if (def != null && !string.IsNullOrEmpty(def.buildingLayout) && BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(type, size, def.buildingLayout, false) != null)
                {
                    into.Add(def);
                }
            }
        }

        // Whether a rival may run one of the game's businesses, as the game gives them out (CompetitionHelper.GetRivalIdForBusinessDefault).
        private static bool Runs(AiBusinessDefault def, string rival)
        {
            string corporation = def.corporationRivalId;
            if (!string.IsNullOrEmpty(corporation))
            {
                return string.Equals(corporation, rival, StringComparison.Ordinal) || (corporation == "*" && rival.IsSpecialRival());
            }
            string[] ids = def.goodsSource == AiBusinessGoodsSource.Import ? RivalsHelper.GetImportRivalIds() : RivalsHelper.GetWholesaleRivalIds();
            return ids != null && Array.IndexOf(ids, rival) >= 0;
        }

        // The business's most skilled employee, for a buy-out by its staff; nothing with no staff.
        private static void StaffBidder(BuildingRegistration reg, List<SaleBidder> into)
        {
            if (reg.Address == null)
            {
                return;
            }
            EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address }, Workers);
            EmployeeInstance self = Self(), best = null;
            int count = 0, bestSkill = -1;
            for (int i = 0; i < Workers.Count; i++)
            {
                EmployeeInstance e = Workers[i];
                if (e == null || e.IsCandidate || e.isBeingReplaced || ReferenceEquals(e, self) || string.IsNullOrEmpty(e.id) || e.characterData == null
                    || string.IsNullOrEmpty(e.characterData.name))
                {
                    continue;
                }
                count++;
                int skill = SkillOf(e);
                if (skill > bestSkill || (skill == bestSkill && string.CompareOrdinal(e.id, best.id) < 0))
                {
                    best = e;
                    bestSkill = skill;
                }
            }
            Workers.Clear();
            if (best != null)
            {
                into.Add(new SaleBidder { Kind = (int)BuyerKind.Staff, Id = best.id, Name = best.characterData.name, Count = count });
            }
        }

        /// <summary>True if a rival can still buy the business it bid for: not beaten, and the game has a business for that building it may run.</summary>
        public static bool RivalStillBuys(BuildingRegistration reg, string rival)
        {
            Building building = GameRegistrations.BuildingOf(reg);
            if (building == null || reg.BuildingOwnedByPlayer || string.IsNullOrEmpty(rival) || !RivalsOn || RivalsHelper.IsRivalDefeated(rival))
            {
                return false;
            }
            return RivalDefault(reg, building, reg.businessTypeName, rival) != null;
        }

        // The first of the game's businesses for this building that the rival may run; null for none.
        private static AiBusinessDefault RivalDefault(BuildingRegistration reg, Building building, string type, string rival)
        {
            Fitting.Clear();
            Defaults(reg, building, type, Fitting);
            AiBusinessDefault found = null;
            for (int d = 0; d < Fitting.Count && found == null; d++)
            {
                found = Runs(Fitting[d], rival) ? Fitting[d] : null;
            }
            Fitting.Clear();
            return found;
        }

        // The rival who bought a business opens it as the game opens a rival's business; null, and nothing changed, if it cannot.
        private static string ReopenFor(BuildingRegistration reg, Building building, string type, string rival)
        {
            AiBusinessDefault def = string.IsNullOrEmpty(rival) || RivalsHelper.IsRivalDefeated(rival) ? null : RivalDefault(reg, building, type, rival);
            if (def == null)
            {
                return null;
            }
            CompetitionHelper.StartNewCompetitorBusiness(type, reg, true, def, rival);
            RivalsHelper.RefreshRivals();   // its lists of businesses are the game's, built from the registrations
            return def.businessName;
        }

        // ---------------------------------------------------------------- rivals selling their own

        /// <summary>
        /// The rivals' businesses that one of them could sell: retail and office businesses the game counts as theirs
        /// (RivalData.ownedRetailOfficeBusinesses), under one of the game's own unbranded names (a special rival's brand stays
        /// with it), never at a building the game sets aside or a DLC keeps. One pass over the rivals' own lists.
        /// </summary>
        public static void RivalHoldings(List<RivalHolding> into)
        {
            into.Clear();
            if (!RivalsOn)
            {
                return;
            }
            List<RivalData> rivals = RivalsHelper.GetAllRivalData();
            for (int r = 0; rivals != null && r < rivals.Count; r++)
            {
                RivalData rival = rivals[r];
                if (rival == null || string.IsNullOrEmpty(rival.id) || rival.ownedRetailOfficeBusinesses == null || rival.ownedBusinesses == null || RivalsHelper.IsRivalDefeated(rival.id))
                {
                    continue;
                }
                for (int b = 0; b < rival.ownedRetailOfficeBusinesses.Count; b++)
                {
                    BuildingRegistration reg = rival.ownedRetailOfficeBusinesses[b];
                    try
                    {
                        if (reg == null || reg.RentedByPlayer || reg.Address == null || string.IsNullOrEmpty(reg.BusinessName) || RivalsHelper.IgnoredAddresses.Contains(reg.Address)
                            || GameDlc.Gated(reg) || reg.dailyIncomes == null || reg.dailyIncomes.Count == 0 || !Unbranded(reg))
                        {
                            continue;
                        }
                        into.Add(new RivalHolding
                        {
                            Place = Place(reg), Business = ServicesStore.Clean(reg.BusinessName), Rival = rival.id, RivalName = rival.rivalName ?? string.Empty,
                            Daily = Mathf.RoundToInt(Mathf.Clamp(Recent(reg.dailyIncomes), 0f, int.MaxValue / 64)), Count = rival.ownedBusinesses.Count
                        });
                    }
                    catch (Exception)
                    {
                        // a business that cannot be read is not for sale
                    }
                }
            }
        }

        private static bool Unbranded(BuildingRegistration reg)
        {
            AiBusinessDefault def = CompetitionHelper.GetBusinessDefault(reg.BusinessName);
            return def != null && string.IsNullOrEmpty(def.corporationRivalId);
        }

        // The mean of the game's last seven daily takings of a business (its own figures, as its Rivals app sums a week).
        private static float Recent(List<float> daily)
        {
            float sum = 0f;
            int from = Mathf.Max(0, daily.Count - 7);
            for (int i = from; i < daily.Count; i++)
            {
                sum += daily[i];
            }
            return daily.Count > from ? sum / (daily.Count - from) : 0f;
        }

        /// <summary>The game's own price for taking a rival's business over (BizMan's least it accepts): its valuation by the rival's accept rate; 0 where it has none.</summary>
        public static float Takeover(BuildingRegistration reg, string rival)
        {
            try
            {
                return Mathf.Max(0f, CompetitionHelper.CalculateAiOwnedValuation(reg) * RivalsHelper.GetOvertakeBusinessAcceptRate(rival, reg.Address));
            }
            catch (Exception)
            {
                return 0f;   // no takings on its books yet
            }
        }

        /// <summary>The rivals who could take over a rival's business as it trades now (the same name, so the same goods): never its seller.</summary>
        public static void RivalBuyers(BuildingRegistration reg, string seller, List<SaleBidder> into)
        {
            into.Clear();
            Building building = GameRegistrations.BuildingOf(reg);
            AiBusinessDefault def = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? CompetitionHelper.GetBusinessDefault(reg.BusinessName) : null;
            if (building != null && def != null)
            {
                RivalBidders(reg, building, reg.businessTypeName, def, seller, into);
            }
        }

        /// <summary>True while a rival's business is still that rival's, as it was put up for sale.</summary>
        public static bool StillTheirs(BuildingRegistration reg, RivalSale sale)
        {
            return reg != null && sale != null && !reg.RentedByPlayer && string.Equals(reg.businessOwnerRivalId, sale.Seller, StringComparison.Ordinal)
                   && string.Equals(ServicesStore.Clean(reg.BusinessName), sale.Business, StringComparison.Ordinal);
        }

        /// <summary>A rival's business passes to the rival who bought it; true if it did. The business trades on as it was: only its owner changes.</summary>
        public static bool PassTo(BuildingRegistration reg, RivalSale sale)
        {
            if (!StillTheirs(reg, sale) || string.IsNullOrEmpty(sale.Buyer) || RivalsHelper.IsRivalDefeated(sale.Buyer))
            {
                return false;
            }
            AiBusinessDefault def = CompetitionHelper.GetBusinessDefault(reg.BusinessName);
            if (def == null || !Runs(def, sale.Buyer))
            {
                return false;
            }
            reg.businessOwnerRivalId = sale.Buyer;
            RivalsHelper.RefreshRivals();
            Told(reg.Address);
            return true;
        }

        /// <summary>Why the player cannot take a rival's business over now, as the game's own BizMan refuses it, with the game's own notification; true if nothing is in the way.</summary>
        public static bool MayBuy(BuildingRegistration reg, int price)
        {
            Address address = reg.Address;
            if (BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance.building != null && InstanceBehavior<BuildingManager>.Instance.building.Address == address)
            {
                Notifications.Show(NotificationType.Error, "notification_cant_takeover_inside");
                return false;
            }
            if (!EducationHelper.HasCompletedDiploma(DiplomaName.Headquarters))
            {
                Notifications.Show(NotificationType.Error, "notification_missing_diploma_overtake_business",
                    new Dictionary<string, string>(1) { { "name", DiplomaName.Headquarters.GetLocalization() } });
                return false;
            }
            if (GameRegistrations.BuildingType(reg) == "ba:buildingtype_office" && !EducationHelper.HasCompletedDiploma(DiplomaName.OfficeBusinesses))
            {
                Notifications.Show(NotificationType.Error, "notification_missing_diploma_overtake_business_office",
                    new Dictionary<string, string>(1) { { "name", DiplomaName.OfficeBusinesses.GetLocalization() } });
                return false;
            }
            SpecialRival landlord = RivalsOn ? RivalsHelper.GetSpecialRival(reg.buildingOwnerRivalId) : null;
            SpecialRivalState state = landlord != null && landlord.rivalData != null ? RivalsHelper.GetSpecialRivalState(landlord.rivalData.id) : null;
            if (state != null && state.isActive)
            {
                Notifications.Show(NotificationType.Error, "notification_cannot_rent_building_owned_by_rival", new Dictionary<string, string>(1) { { "name", landlord.rivalData.rivalName } });
                return false;
            }
            if (Save == null || Save.Money < price)
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            return true;
        }

        /// <summary>The player takes a rival's business over at a price, as BizMan's accepted offer does: paid under the game's own transaction, then the game's own takeover. True if done.</summary>
        public static bool Buy(BuildingRegistration reg, int price)
        {
            Address address = reg.Address;
            var info = new TransactionInfo(TakeoverKey, new Dictionary<string, string>(1) { { "address", address.ToFormattedString() } });
            if (BusinessHelper.IsTaxDeductibleBusinessBuilding(reg))
            {
                info.SetTaxDeductibleName(address.ToFormattedString());
            }
            if (!RealLifeOverhaul.Economy.BizBooks.Book(-price, info, null, address, false))
            {
                return false;
            }
            BizManPresentation.OvertakeBusiness(reg, null);
            RivalsHelper.RefreshRivals();
            Told(address);
            return true;
        }

        // The map and the game's lists are told a building changed hands.
        private static void Told(Address address)
        {
            try
            {
                GlobalEvents.onBuildingRegistrationChange?.Invoke(address);
            }
            catch (Exception e)
            {
                Logger?.Warn("Services: the game could not be told that " + address.ToFormattedString() + " changed hands (" + e.Message + "); it shows after the next load.");
            }
        }
    }
}
