using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Rivals;
using BigAmbitions.Tags;
using Buildings;
using Helpers;
using Streets;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything the held addresses read from or do to the game (<see cref="HeldUnits"/>), and the only file that writes
    /// RivalsHelper.IgnoredAddresses or BuildingHelper.AllNeighbourhoodBuildings. Both are runtime lists, never saved:
    /// the ignore list is the game's list of addresses its rivals and new businesses leave alone (an entry goes on it only
    /// by <see cref="HeldUnitsRules.MayList"/>, and only the mod's own comes off); the district lists are where the game's
    /// night of letting (CompetitionHelper.RunDaily, the one reader in the game) finds its units, and a held empty unit is
    /// out of its list from the night's first hour event to its day event (<see cref="Out"/>, <see cref="Back"/>), put
    /// back in the order it had. A reader of the district lists in the mod runs on its own timer, never inside the night,
    /// and sees them whole. COST: a registration look an address on a change and once a second; the night's two steps a
    /// pass over the held addresses; nothing per frame.
    /// </summary>
    internal static class HeldUnitsGame
    {
        private const string Empty = "ba:businesstype_empty";
        private const string GlobalDistrict = "ba:neighborhood_global";

        // The district lists as they were before the night took the held units out, by the game's own list: put back as
        // they were. Copies kept for the next night.
        private static readonly Dictionary<List<Building>, List<Building>> Was = new Dictionary<List<Building>, List<Building>>(8);
        private static readonly Stack<List<Building>> Spare = new Stack<List<Building>>(8);

        /// <summary>True from <see cref="Out"/> until <see cref="Back"/>.</summary>
        public static bool IsOut => Was.Count > 0;

        /// <summary>The game's address of a held spot, made once.</summary>
        public static Address AddressOf(HeldSpot spot)
        {
            if (spot.Place is Address made)
            {
                return made;
            }
            var address = new Address(spot.Street, spot.Number);
            spot.Place = address;
            return address;
        }

        /// <summary>
        /// The registration of one of the game's buildings, or null for an address the game has no building for (a building
        /// the mod made, which no saved registration stands for). The game's own lookup adds a registration to the save
        /// for a building that has none, as it does in its own passes; for no building it is never asked.
        /// </summary>
        public static BuildingRegistration Registration(Address address, out Building building)
        {
            building = BuildingHelper.GetBuilding(address);
            return building != null ? BuildingHelper.GetBuildingRegistration(address) : null;
        }

        /// <summary>True if a held address may be on the game's ignore list now (<see cref="HeldUnitsRules.MayList"/>).</summary>
        public static bool MayList(HeldSpot spot, GameInstance save)
        {
            Address address = AddressOf(spot);
            BuildingRegistration reg = Registration(address, out _);
            return HeldUnitsRules.MayList(spot.Holders > 0, reg != null && !string.IsNullOrEmpty(reg.buildingOwnerRivalId),
                reg != null && !string.IsNullOrEmpty(reg.businessOwnerRivalId), ForSale(save, address), GoingPublic.IsGuest);
        }

        private static bool ForSale(GameInstance save, Address address)
        {
            List<BuildingForSale> all = save != null ? save.buildingsForSale : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].address == address)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// On or off the game's ignore list by <see cref="HeldUnitsRules.IgnoreChange"/>: only an entry the mod added is
        /// ever taken off. Returns whether the entry there is the mod's now.
        /// </summary>
        public static bool Ignore(HeldSpot spot, bool want)
        {
            List<Address> list = RivalsHelper.IgnoredAddresses;
            if (list == null)
            {
                return false;
            }
            Address address = AddressOf(spot);
            int at = list.IndexOf(address);
            bool ours = spot.Ours;
            int change = HeldUnitsRules.IgnoreChange(want, at >= 0, ref ours);
            if (change > 0)
            {
                list.Add(address);
            }
            else if (change < 0)
            {
                list.RemoveAt(at);
            }
            return ours;
        }

        /// <summary>True if the game's ignore list has the address, whoever put it there.</summary>
        public static bool Listed(HeldSpot spot)
        {
            List<Address> list = RivalsHelper.IgnoredAddresses;
            return list != null && list.Contains(AddressOf(spot));
        }

        /// <summary>
        /// The held units that stand empty out of their districts' lists (<see cref="HeldUnitsRules.OutOfPass"/>), for the
        /// game's night of letting; returns how many. Nothing if they are out already.
        /// </summary>
        public static int Out(List<HeldSpot> spots)
        {
            if (IsOut)
            {
                return 0;
            }
            int taken = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                HeldSpot spot = spots[i];
                if (spot.Holders <= 0)
                {
                    continue;
                }
                BuildingRegistration reg = Registration(AddressOf(spot), out Building building);
                if (reg == null || !HeldUnitsRules.OutOfPass(true, string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty,
                        reg.RentedByPlayer || reg.BuildingOwnedByPlayer, building.SpecialService != null)
                    || string.IsNullOrEmpty(building.Neighbourhood) || !BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(building.Neighbourhood, out List<Building> list)
                    || list == null)
                {
                    continue;
                }
                if (!Was.ContainsKey(list))
                {
                    List<Building> copy = Spare.Count > 0 ? Spare.Pop() : new List<Building>(list.Count);
                    copy.Clear();
                    copy.AddRange(list);
                    Was[list] = copy;
                }
                if (list.Remove(building))
                {
                    taken++;
                }
            }
            return taken;
        }

        /// <summary>Every district list the night took units out of, as it was; returns how many lists. Safe to ask any time.</summary>
        public static int Back()
        {
            if (Was.Count == 0)
            {
                return 0;
            }
            int lists = 0;
            foreach (KeyValuePair<List<Building>, List<Building>> pair in Was)
            {
                pair.Key.Clear();
                pair.Key.AddRange(pair.Value);   // a list the game has since made again is no longer read: putting it back is harmless
                pair.Value.Clear();
                Spare.Push(pair.Value);
                lists++;
            }
            Was.Clear();
            return lists;
        }

        /// <summary>
        /// The game's own choice of empty units to let (CompetitionHelper.GetEmptyBuildings, on each district's units of the
        /// types it lists for business, as its night does), run with the held units out as at night: every held address it
        /// would offer, into <paramref name="offered"/>. Returns the units it would offer in all, or -1 with
        /// <paramref name="why"/> if the game's choice cannot be read.
        /// </summary>
        public static int Offered(HeldLedger ledger, List<HeldSpot> spots, List<string> offered, out int districts, out string why)
        {
            offered.Clear();
            districts = 0;
            why = null;
            MethodInfo choose = GameContract.EmptyUnits.Method;
            if (choose == null)
            {
                why = "the game's choice of empty units (CompetitionHelper.GetEmptyBuildings) is not in this build";
                return -1;
            }
            bool took = !IsOut;
            if (took)
            {
                Out(spots);
            }
            int total = 0;
            try
            {
                var business = new List<Building>(256);
                foreach (KeyValuePair<string, List<Building>> pair in BuildingHelper.AllNeighbourhoodBuildings)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Key == GlobalDistrict || pair.Value == null || pair.Value.Count == 0)
                    {
                        continue;
                    }
                    business.Clear();
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        Building b = pair.Value[i];
                        if (b != null && BuildingTypeHelper.GetData(b).HasTag(TagRef.Buildingtypetag.showinbusinesslist))
                        {
                            business.Add(b);
                        }
                    }
                    districts++;
                    var empty = choose.Invoke(null, new object[] { business }) as List<Building>;
                    for (int i = 0; empty != null && i < empty.Count; i++)
                    {
                        Building b = empty[i];
                        total++;
                        if (b != null && ledger.Holds(b.StreetName, b.StreetNumber))
                        {
                            offered.Add(b.Address.ToFormattedString() + " (" + b.Neighbourhood + ")");
                        }
                    }
                }
            }
            finally
            {
                if (took)
                {
                    Back();
                }
            }
            return total;
        }
    }
}
