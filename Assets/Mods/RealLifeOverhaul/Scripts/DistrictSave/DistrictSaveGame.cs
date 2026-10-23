using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The one part of the district save that touches the game's records: at a save, every record of the save that names
    /// one of the mod's places is taken out of the game's lists (a whole record, or an element of a record of the game's
    /// own) or its field is written as a stand-in, each with what puts it back where it stood; at a load, what the mod's
    /// section kept goes back. Every field of the save that can name a place is handled here, by its path in the save
    /// ("save path:" comments; ContractCheck holds them against the game's assemblies and against SavePlaces.txt). The
    /// parts: DistrictSaveGame.Records (whole records), .Elements (elements of the game's records), .StandIns (fields),
    /// .Restore (the load). COST: nothing per frame; at a save one pass over each list, at a load one step a record.
    /// </summary>
    internal static partial class DistrictSaveGame
    {
        /// <summary>What one save took out of the game's lists and stood in, and how each goes back.</summary>
        internal sealed class Strip
        {
            public readonly List<Action> PutBacks = new List<Action>(24);
            public readonly List<StoredRecord> Records = new List<StoredRecord>(16);
            public readonly List<object> Payload = new List<object>(16);
            public readonly HashSet<string> Employees = new HashSet<string>(StringComparer.Ordinal);      // ids of employees and candidates that left
            public readonly HashSet<string> HrPlans = new HashSet<string>(StringComparer.Ordinal);        // ids of HR managers' plans that left
            public readonly HashSet<string> Offers = new HashSet<string>(StringComparer.Ordinal);         // ids of insurance offers that left
            public readonly HashSet<string> Negotiations = new HashSet<string>(StringComparer.Ordinal);   // ids of salary negotiations that left
            public readonly HashSet<PlaceKey> Foreign = new HashSet<PlaceKey>();                          // another player's businesses taken out
            public readonly Dictionary<string, PlaceKey> EmployeePlaces = new Dictionary<string, PlaceKey>(StringComparer.Ordinal);       // where each who left works
            public readonly Dictionary<string, StoredRecord> PlanPlaces = new Dictionary<string, StoredRecord>(StringComparer.Ordinal);   // each HR plan that left
            public readonly Dictionary<string, StoredRecord> TalkPlaces = new Dictionary<string, StoredRecord>(StringComparer.Ordinal);   // each offer or negotiation that left
            public readonly List<string> Faults = new List<string>(0);                                    // steps that failed, for the check line
            public int Left;        // whole records and elements taken out
            public int StoodIn;     // fields written as a stand-in
            public int NotKept;     // of those, another player's: put back after the write, never kept

            /// <summary>The record's place in the payload.</summary>
            public int Slot(object item)
            {
                Payload.Add(item);
                return Payload.Count - 1;
            }
        }

        /// <summary>
        /// Takes every record that names one of the mod's places out of the save, and writes every such field as its
        /// stand-in. The order matters only where one kind leaves because another did (an employee's plan, a message about
        /// an employee): those come after.
        /// </summary>
        public static Strip TakeOut(GameInstance save, PlaceSet places)
        {
            var s = new Strip();
            Step(s, "the businesses", () => Registrations(save, places, s));
            Step(s, "their staff and candidates", () => People(save, places, s));
            Step(s, "the offices' plans", () => Plans(save, places, s));
            Step(s, "the records that name them", () => Records(save, places, s));
            Step(s, "the game's records' elements", () => Elements(save, places, s));
            Step(s, "the stand-ins", () => StandIns(save, places, s));
            return s;
        }

        // One step of the strip: one that fails is named, and the others go on.
        private static void Step(Strip s, string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                s.Faults.Add(what + ": " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>Puts everything one save took out back where it stood, the last taken first; each step on its own.</summary>
        public static int PutBack(Strip s, Action<Exception> fault)
        {
            int failed = 0;
            for (int i = s.PutBacks.Count - 1; i >= 0; i--)
            {
                try
                {
                    s.PutBacks[i]();
                }
                catch (Exception e)
                {
                    failed++;
                    fault?.Invoke(e);
                }
            }
            s.PutBacks.Clear();
            return failed;
        }

        // ---------------------------------------------------------------- the shared moves

        /// <summary>
        /// Takes the items <paramref name="mine"/> picks out of a list and keeps each with its places, unless
        /// <paramref name="foreign"/> says it is another player's: then it is put back after the write and never kept.
        /// </summary>
        private static List<Taken<T>> Leave<T>(Strip s, List<T> list, SaveKind kind, Predicate<T> mine, Action<StoredRecord, T> places,
                                               Predicate<T> foreign = null)
        {
            if (list == null || list.Count == 0)
            {
                return null;
            }
            var taken = new List<Taken<T>>(2);
            if (DistrictSaveRules.TakeOut(list, mine, taken) == 0)
            {
                return null;
            }
            s.PutBacks.Add(() => DistrictSaveRules.PutBack(list, taken));
            for (int i = 0; i < taken.Count; i++)
            {
                s.Left++;
                if (foreign != null && foreign(taken[i].Item))
                {
                    s.NotKept++;
                    continue;
                }
                var record = new StoredRecord(kind, taken[i].Index, null);
                places?.Invoke(record, taken[i].Item);
                record.Slot = s.Slot(taken[i].Item);
                s.Records.Add(record);
            }
            return taken;
        }

        /// <summary>As <see cref="Leave{T}"/>, for the elements of one record of the game's own that stays: kept under its key.</summary>
        private static void LeaveFrom<T>(Strip s, List<T> list, string parent, SaveKind kind, Predicate<T> mine, Action<StoredRecord, T> places)
        {
            if (list == null || list.Count == 0)
            {
                return;
            }
            var taken = new List<Taken<T>>(1);
            if (DistrictSaveRules.TakeOut(list, mine, taken) == 0)
            {
                return;
            }
            s.PutBacks.Add(() => DistrictSaveRules.PutBack(list, taken));
            for (int i = 0; i < taken.Count; i++)
            {
                s.Left++;
                var record = new StoredRecord(kind, taken[i].Index, parent);
                places?.Invoke(record, taken[i].Item);
                record.Slot = s.Slot(taken[i].Item);
                s.Records.Add(record);
            }
        }

        /// <summary>True for an address of the mod's places.</summary>
        private static bool Names(PlaceSet places, Address a)
        {
            return a != null && places.Names(a.streetName, a.streetNumber);
        }

        /// <summary>Adds an address to the record's places if it is one of the mod's.</summary>
        private static void Mark(StoredRecord r, PlaceSet places, Address a)
        {
            if (a != null && places.Names(a.streetName, a.streetNumber))
            {
                r.Addresses.Add(new PlaceKey(a.streetName, a.streetNumber));
            }
        }

        /// <summary>Adds a street and number to the record's places if they are one of the mod's.</summary>
        private static void Mark(StoredRecord r, PlaceSet places, string street, int number)
        {
            if (places.Names(street, number))
            {
                r.Addresses.Add(new PlaceKey(street, number));
            }
        }

        /// <summary>Adds a district to the record's places if it is one of the mod's.</summary>
        private static void MarkDistrict(StoredRecord r, PlaceSet places, string district)
        {
            if (places.NamesDistrict(district))
            {
                r.Districts.Add(district);
            }
        }

        /// <summary>An address as the store's parent key for the elements of a registration of the game's.</summary>
        private static string KeyOf(string street, int number)
        {
            return DistrictSaveRules.Place(street, number);
        }

        /// <summary>True for an id in a set of ids that left.</summary>
        private static bool In(HashSet<string> ids, string id)
        {
            return !string.IsNullOrEmpty(id) && ids.Contains(id);
        }
    }
}
