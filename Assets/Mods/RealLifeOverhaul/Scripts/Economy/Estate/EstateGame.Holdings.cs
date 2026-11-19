using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Buildings;
using Helpers;
using UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The result of one day's look at the rivals' holdings, for the director's log line.</summary>
    internal struct HoldingsReport
    {
        public int Rivals, Buildings, Sold, Bought, Placed, DistanceBefore, DistanceAfter;
        public int FirstHeld, FirstTarget, LastHeld, LastTarget;
    }

    /// <summary>
    /// The rivals' holdings as the game has them (RivalHoldingsRules): the Rivals app's ranking read as the game sorts it, the buildings the
    /// rivals hold and the ones for sale read into a book, and the plan's moves made the way the game makes its own: a building put up for sale
    /// by RealEstateHelper.SetBuildingForSale, a building bought by a rival as RealEstateHelper.SimulateCompetitorBuyingAIBuildings does it
    /// (the owner written, the listing taken off), or in a new game the owner written at once. A building of the player's, rented by the
    /// player, a special service, a Hamptons house, one of a DLC the player does not own, one on the game's ignore list or another player's is
    /// never read into the book, so never touched. COST: one pass over the registrations once a game day, a few hundred bytes of garbage
    /// for the ranking; nothing per frame.
    /// </summary>
    internal static partial class EstateGame
    {
        private static readonly HoldingsBook HoldingsBookKept = new HoldingsBook();
        private static readonly List<BuildingRegistration> HoldingsRegs = new List<BuildingRegistration>(2048);   // the registration of each building in the book, in its order
        private static readonly List<string> HoldingsIds = new List<string>(32);                                    // the rivals' ids by place
        private static readonly Dictionary<string, int> HoldingsPlace = new Dictionary<string, int>(32, StringComparer.Ordinal);
        private static readonly Dictionary<string, int> HoldingsDistrict = new Dictionary<string, int>(16, StringComparer.Ordinal);
        private static readonly List<RivalData> HoldingsRanked = new List<RivalData>(32);

        /// <summary>
        /// The rivals' holdings brought towards their place in the ranking by <paramref name="pace"/>; false, with nothing done, where the game's rivals cannot be read. The buildings in
        /// <paramref name="kept"/> (places as <see cref="Place"/> writes them: a rival's head office, a building a managed block is tied to) are left out of the book, so never sold or dealt.
        /// </summary>
        public static bool HoldRivals(int day, RivalHoldingsPace pace, HashSet<string> kept, out HoldingsReport report)
        {
            report = default;
            GameInstance save = Save;
            if (save == null || save.BuildingRegistrations == null || pace == RivalHoldingsPace.None || !RivalsHelper.IsFeatureEnabled)
            {
                return false;
            }
            if (!ReadRanking())
            {
                return false;
            }
            ReadBook(save, kept);
            HoldingsBook book = HoldingsBookKept;
            report.Rivals = book.Rivals;
            report.Buildings = book.Count;
            var counts = new int[book.Rivals];
            var targets = new int[book.Rivals];
            int total = RivalHoldingsRules.Counts(book, counts);
            RivalHoldingsRules.Targets(book.Rivals, total, targets);
            report.DistanceBefore = RivalHoldingsRules.Distance(counts, targets, book.Rivals);
            if (pace == RivalHoldingsPace.AtOnce)
            {
                report.Placed = RivalHoldingsRules.AtOnce(book);
            }
            else
            {
                RivalHoldingsRules.Day(book, day, RivalHoldingsRules.SalesPerDay, RivalHoldingsRules.BuysPerDay, out report.Sold, out report.Bought);
            }
            Apply(save);
            if (book.Moves.Count > 0)
            {
                RivalsHelper.RefreshRivals();   // the rivals' lists of buildings are built from the registrations
                if (pace == RivalHoldingsPace.Gentle)
                {
                    RefreshMapMarks();
                }
            }
            total = RivalHoldingsRules.Counts(book, counts);
            RivalHoldingsRules.Targets(book.Rivals, total, targets);
            report.DistanceAfter = RivalHoldingsRules.Distance(counts, targets, book.Rivals);
            report.FirstHeld = counts[0];
            report.FirstTarget = targets[0];
            report.LastHeld = counts[book.Rivals - 1];
            report.LastTarget = targets[book.Rivals - 1];
            return true;
        }

        // The rivals as the Rivals app ranks them (RivalRanking), their ids by place.
        private static bool ReadRanking()
        {
            if (RivalRanking.Read(HoldingsRanked) < 2)
            {
                return false;
            }
            HoldingsIds.Clear();
            HoldingsPlace.Clear();
            for (int i = 0; i < HoldingsRanked.Count; i++)
            {
                HoldingsIds.Add(HoldingsRanked[i].id);
                HoldingsPlace[HoldingsRanked[i].id] = i;
            }
            return true;
        }

        // The buildings the plan may move: a rival's, or for sale and nobody's. Everything else, and what is kept, is left out of the book altogether.
        private static void ReadBook(GameInstance save, HashSet<string> kept)
        {
            HoldingsBook book = HoldingsBookKept;
            HoldingsDistrict.Clear();
            HoldingsRegs.Clear();
            book.Reset(HoldingsIds.Count, 1);
            List<BuildingRegistration> all = save.BuildingRegistrations;
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                Building building = reg != null && reg.HasValidAddress ? GameRegistrations.BuildingOf(reg) : null;
                if (building == null || reg.BuildingOwnedByPlayer || reg.RentedByPlayer || building.SpecialService != null || building.IsHamptonsHouse()
                    || GameDlc.Gated(building) || GoingPublic.Mine(reg) || GoingPublic.Foreign(reg) || RivalsHelper.IgnoredAddresses.Contains(reg.Address))
                {
                    continue;
                }
                if (kept != null && kept.Count > 0 && kept.Contains(Place(reg.StreetName, reg.StreetNumber)))
                {
                    continue;   // a rival's head office, or the building of a block the agency manages: it stays as it is
                }
                int owner;
                int listed = 0;
                if (!string.IsNullOrEmpty(reg.buildingOwnerRivalId))
                {
                    if (!HoldingsPlace.TryGetValue(reg.buildingOwnerRivalId, out owner))
                    {
                        continue;   // a rival that is out of the ranking: the game sells its buildings itself
                    }
                }
                else if (reg.IsOnSale())
                {
                    owner = HoldingsBook.Market;
                    listed = reg.lastDayOnSale;
                }
                else
                {
                    continue;
                }
                long value;
                try
                {
                    value = (long)building.GetMarketValue();
                }
                catch (Exception)
                {
                    continue;   // a building the game cannot value is left alone
                }
                string district = building.Neighbourhood ?? string.Empty;
                if (!HoldingsDistrict.TryGetValue(district, out int d))
                {
                    d = HoldingsDistrict.Count;
                    HoldingsDistrict[district] = d;
                }
                book.Add(owner, value, d, listed);
                HoldingsRegs.Add(reg);
            }
            book.Districts = Math.Max(1, HoldingsDistrict.Count);
        }

        // The plan's moves made in the game.
        private static void Apply(GameInstance save)
        {
            HoldingsBook book = HoldingsBookKept;
            for (int i = 0; i < book.Moves.Count; i++)
            {
                HoldingsMove move = book.Moves[i];
                BuildingRegistration reg = HoldingsRegs[move.Building];
                if (move.To == HoldingsBook.Market)
                {
                    RealEstateHelper.SetBuildingForSale(reg);   // the game's own: the owner blanked, the listing added at the building's market value, the day noted
                    continue;
                }
                reg.buildingOwnerRivalId = HoldingsIds[move.To];
                if (move.From == HoldingsBook.Market)
                {
                    for (int s = save.buildingsForSale.Count - 1; s >= 0; s--)
                    {
                        if (save.buildingsForSale[s] != null && save.buildingsForSale[s].address == reg.Address)
                        {
                            save.buildingsForSale.RemoveAt(s);
                        }
                    }
                }
            }
        }

        // The map's for sale marks follow the list of buildings for sale: once, after the day's moves.
        private static void RefreshMapMarks()
        {
            UIs ui = InstanceBehavior<UIs>.Instance;
            if (ui != null && ui.mapFilters != null)
            {
                MapPlaces.Refilter(ui.mapFilters);
            }
        }
    }
}
