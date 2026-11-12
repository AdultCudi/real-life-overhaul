using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The walk over the city's premises to let, a slice of buildings at a time (FlareBridges.ToLetSlice), so no frame pays for the whole city. It is
    /// queued at start-up and by each day's round, never finished by it: a save, a day turn or a time skip only asks for a new pass, which the slices
    /// start over when they come, so a run of days pays for one walk and not one a day. COST: <see cref="Slice"/> buildings a step.
    /// </summary>
    public sealed class FlareToLetWalk
    {
        /// <summary>Buildings walked a step.</summary>
        public const int Slice = 300;

        private bool _wanted, _fresh = true;

        /// <summary>The last finished map: premises to let by the game's own district id.</summary>
        public readonly Dictionary<string, int> Map = new Dictionary<string, int>(16, StringComparer.Ordinal);

        /// <summary>A pass is wanted, or under way.</summary>
        public bool Wanted => _wanted;

        /// <summary>A pass is wanted from the start. Costs nothing now; the next slice begins it.</summary>
        public void Queue()
        {
            _wanted = true;
            _fresh = true;
        }

        /// <summary>No pass is wanted any more (Flare stands down).</summary>
        public void Drop()
        {
            _wanted = false;
            _fresh = true;
        }

        /// <summary>Drop, and the map forgotten (another save).</summary>
        public void Forget()
        {
            Drop();
            Map.Clear();
        }

        /// <summary>One slice through the bridge; true when no pass is left to walk (finished, or the bridge is not there).</summary>
        public bool Step(Func<bool, int, Dictionary<string, int>> slice)
        {
            if (!_wanted)
            {
                return true;
            }
            if (slice == null)
            {
                _wanted = false;
                return true;
            }
            Dictionary<string, int> done = slice(_fresh, Slice);
            _fresh = false;
            if (done == null)
            {
                return false;
            }
            Map.Clear();
            foreach (KeyValuePair<string, int> kv in done)
            {
                Map[kv.Key] = kv.Value;
            }
            _wanted = false;
            _fresh = true;
            return true;
        }
    }
}
