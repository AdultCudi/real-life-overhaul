using System;
using System.Collections.Generic;
using Buildings;
using Helpers;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A district building's record and registration in the game's lookups by address before the city loads (<see cref="EarlyRecordsRules"/>). The game
    /// empties and refills both lookups when a save is chosen, then builds the city's scenes, then starts every mod's city part in turn; a mod that looks a
    /// building up by its address in its own start, before this mod's city part has made anything, found nothing. These are made in the district save's early
    /// step instead (after the save is read, before the scenes): the record by <see cref="MakeRecord"/> and the registration by <see cref="MakeRegistration"/>
    /// (the save's own, kept by the district save, so the player's rented unit is the very object a lookup returns), entered into the two lookups alone. The
    /// registration of a building the save never had joins the save's list only when its make claims it, as it always did; the special services' lookup, the
    /// copy, the ground and the city's list stay with the make behind the loading screen. Nothing of it is saved. Nothing here runs a frame.
    /// </summary>
    internal static partial class MadeGame
    {
        private sealed class EarlyRecord
        {
            public Building Record;
            public BuildingRegistration Registration;
            public bool Fresh;        // the registration is new at this load: it joins the save's list when the building is made
            public string Size;       // the structure the record was made with
            public int Version;
            public string Type;
        }

        private static readonly EarlyLedger<EarlyRecord> Early = new EarlyLedger<EarlyRecord>();
        private static readonly HashSet<Building> EarlyHeld = new HashSet<Building>();
        private static readonly List<EarlyRecord> Leftover = new List<EarlyRecord>(8);

        /// <summary>Records made early that no make has claimed yet.</summary>
        public static int EarlyWaiting => Early.Count;

        /// <summary>
        /// Makes the record and registration of every declaration that gets one early (<see cref="EarlyRecordsRules.Wants"/>) and enters them into the game's two
        /// lookups; the mod's streets among the game's with them. A declaration the make would refuse, or at whose address the game already has a building, gets
        /// none (the make says why at the load). Returns how many were made; <paramref name="found"/> is how many of them the game's own lookups, asked by address, then give back as made.
        /// </summary>
        public static int RegisterEarly(IList<MadeSpec> specs, out int found)
        {
            found = 0;
            DropEarly("a new load began");
            if (specs == null || specs.Count == 0)
            {
                return 0;
            }
            AddStreets();
            int made = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                MadeSpec spec = specs[i];
                if (!EarlyRecordsRules.Wants(spec)
                    || NewBuildingsRules.Refusal(spec, StructureTakes, DistrictListed, true) != null || GameHas(spec.Street, spec.Number))
                {
                    continue;
                }
                BuildingRegistration registration = MakeRegistration(spec, out bool fresh, false);
                Building record = MakeRecord(spec, 0, registration, fresh, out _);
                if (record == null)
                {
                    continue;
                }
                if (!EnterLookups(record, registration, out string why))
                {
                    Logger?.Warn("Made buildings: the district's records could not be put in the game's lookups at the load's first step (" + why + "); they are made with the buildings.");
                    Forsake(registration, fresh);
                    Records.Remove(record);
                    UnityEngine.Object.Destroy(record);
                    break;
                }
                Early.Add(spec.Id, new EarlyRecord { Record = record, Registration = registration, Fresh = fresh, Size = spec.Size, Version = spec.Version, Type = spec.Type });
                EarlyHeld.Add(record);
                made++;
                Address address = new Address(spec.Street, spec.Number);
                found += BuildingHelper.GetBuilding(address) == record && BuildingHelper.GetBuildingRegistration(address) == registration ? 1 : 0;
            }
            return made;
        }

        /// <summary>
        /// The make of a declaration claims its record: true with the record and registration it is to stand with, its floor area set to its design's where the
        /// declaration says so, a registration new at this load joining the save's list as <see cref="MakeRegistration"/> adds one. False when none was made early,
        /// or the declaration names another structure now (then what was made is taken back and the make makes its own).
        /// </summary>
        public static bool TakeEarly(MadeSpec spec, int designArea, out BuildingRegistration registration, out Building record, out bool fresh)
        {
            registration = null;
            record = null;
            fresh = false;
            if (spec == null || !Early.Take(spec.Id, out EarlyRecord e))
            {
                return false;
            }
            EarlyHeld.Remove(e.Record);
            if (!EarlyRecordsRules.Fits(spec, e.Size, e.Version, e.Type))
            {
                Withdraw(e);
                return false;
            }
            if (spec.DesignArea && designArea > 0)
            {
                e.Record.totalSqm = designArea;   // as its size's area before the design was chosen: a district's building is as large as it looks
            }
            if (e.Fresh)
            {
                SaveGameManager.Current?.BuildingRegistrations?.Add(e.Registration);   // as BuildingHelper.GetBuildingRegistration adds one it makes, now that it stands
            }
            registration = e.Registration;
            record = e.Record;
            fresh = e.Fresh;
            return true;
        }

        /// <summary>
        /// Takes back every record no make claimed (a declaration that stood down, a district that did not stand, a city part that never started): out of the
        /// game's lookups, a registration of the save's list that was new out again, the record destroyed. Returns how many.
        /// </summary>
        public static int DropEarly(string why)
        {
            Leftover.Clear();
            int n = Early.Drain(Leftover);
            for (int i = 0; i < Leftover.Count; i++)
            {
                EarlyHeld.Remove(Leftover[i].Record);
                Withdraw(Leftover[i]);
            }
            Leftover.Clear();
            EarlyHeld.Clear();
            if (n > 0)
            {
                Logger?.Info("Made buildings: " + n + " record(s) made at the load's first step were taken back out of the game's lookups: " + why + ".");
            }
            return n;
        }

        // One early record out of the game's lookups and destroyed, its new registration out of the save's list.
        private static void Withdraw(EarlyRecord e)
        {
            if (e.Record == null)
            {
                return;
            }
            Leave(e.Record);
            Forsake(e.Registration, e.Fresh);
            Records.Remove(e.Record);
            UnityEngine.Object.Destroy(e.Record);
        }

        /// <summary>True for a record this mod made at the first step and no make has claimed yet: the game's lookup holding it is the mod's own, not another's.</summary>
        private static bool IsEarly(Building record) => record != null && EarlyHeld.Contains(record);
    }
}
