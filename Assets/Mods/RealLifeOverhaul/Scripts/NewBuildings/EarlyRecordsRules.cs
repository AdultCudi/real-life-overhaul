using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The rules of a district building's record made before the city loads (docs/features/new-buildings.md, "Findable at the load's first step"). The game
    /// empties its lookups of buildings and registrations by address when a save is chosen, and the mod's own city part runs after every older mod's, so
    /// whatever looked a district building up by its address while those mods started found nothing: another mod's setup stopped on "a property is missing".
    /// The record and the registration of an ordinary district building (no special service to copy) are therefore put into the lookups in the step the
    /// district save runs when a save has been read, before the city's scenes are built; its copy of the game's building, its ground and everything else are
    /// made behind the loading screen as before, and <see cref="EarlyLedger{T}"/> hands each record to the make that stands the building, or takes back what
    /// no make claimed. Game-free. COST: a few comparisons a declaration, once a load.
    /// </summary>
    public static class EarlyRecordsRules
    {
        /// <summary>
        /// True for a declaration that gets its record early: a district's own (its registration the save's), with no service of the game's to copy (that
        /// copy, its desks and its floor are made with the building). An id is needed to hand the record back to its make.
        /// </summary>
        public static bool Wants(MadeSpec spec)
        {
            return spec != null && spec.Life && !string.IsNullOrEmpty(spec.Id) && string.IsNullOrEmpty(spec.Service);
        }

        /// <summary>
        /// True while a record made early still describes the declaration: the structure it was made with is the one the declaration names now. A declaration
        /// that took another of its lot's structures (no design of its own size fitting) gets a new record made with that one.
        /// </summary>
        public static bool Fits(MadeSpec spec, string size, int version, string type)
        {
            return spec != null && string.Equals(spec.Size, size, StringComparison.Ordinal) && spec.Version == version && string.Equals(spec.Type, type, StringComparison.Ordinal);
        }

        /// <summary>
        /// The step an early record is made in, against the other steps of a city's load, in the order the game runs them. The order is the proof the
        /// records are there before any other mod's city part: <see cref="Registered"/> comes before <see cref="FirstCityPart"/>.
        /// </summary>
        public const int Chosen = 0, Registered = 1, ScenesBuilt = 2, FirstCityPart = 3, OwnCityPart = 4, Made = 5, GameLoaded = 6;

        /// <summary>The names of the steps, for a log line and for the checks.</summary>
        public static readonly string[] Steps =
        {
            "a save is chosen (the game's lookups emptied and filled with its own buildings)",
            "the main menu's mods unload (the district save's early step: the records)",
            "the city's scenes are built",
            "the first other mod's city part starts",
            "the mod's own city part starts",
            "the made buildings stand (the copies, the ground)",
            "the game's own 'game loaded' calls"
        };
    }

    /// <summary>
    /// The records made early, by the declaration's id, until a make claims them or the load's end takes back the rest. Generic so it can be checked with
    /// any stand-in for the game's record. Game-free. COST: a dictionary look-up a declaration.
    /// </summary>
    public sealed class EarlyLedger<T> where T : class
    {
        private readonly Dictionary<string, T> _by = new Dictionary<string, T>(512, StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>(512);

        /// <summary>Records still waiting for their make.</summary>
        public int Count => _by.Count;

        /// <summary>True for an id with a record waiting.</summary>
        public bool Has(string id) => id != null && _by.ContainsKey(id);

        /// <summary>The record of that id waiting, or null.</summary>
        public T Peek(string id) => id != null && _by.TryGetValue(id, out T item) ? item : null;

        /// <summary>Adds a record under an id; the one already there (a declaration made twice) is handed back, not lost.</summary>
        public T Add(string id, T item)
        {
            if (string.IsNullOrEmpty(id) || item == null)
            {
                return null;
            }
            _by.TryGetValue(id, out T old);
            if (old == null)
            {
                _order.Add(id);
            }
            _by[id] = item;
            return old;
        }

        /// <summary>The make claims its record: it leaves the ledger, so no end of the load takes it back.</summary>
        public bool Take(string id, out T item)
        {
            item = null;
            if (id == null || !_by.TryGetValue(id, out item))
            {
                return false;
            }
            _by.Remove(id);
            _order.Remove(id);
            return true;
        }

        /// <summary>Every record no make claimed, in the order they were added, into the list (not cleared); the ledger is empty after.</summary>
        public int Drain(List<T> into)
        {
            int n = 0;
            for (int i = 0; i < _order.Count; i++)
            {
                if (_by.TryGetValue(_order[i], out T item))
                {
                    into?.Add(item);
                    n++;
                }
            }
            _by.Clear();
            _order.Clear();
            return n;
        }
    }
}
