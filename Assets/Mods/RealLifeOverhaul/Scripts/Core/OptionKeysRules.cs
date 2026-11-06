using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>Where the mod keeps one value on this computer, and where it was kept before; <see cref="OptionKeysRules"/> moves it.</summary>
    public interface IOptionStore
    {
        bool Has(string key);

        /// <summary>The value under <paramref name="from"/> written under <paramref name="to"/>, of the same kind (a number or a text); the old one stays.</summary>
        void Copy(string from, string to);

        /// <summary>A number written under the key.</summary>
        void Mark(string key);
    }

    /// <summary>What one start brought over: how many values, from which of the older ids.</summary>
    public sealed class OptionMove
    {
        public readonly List<string> Sources = new List<string>(4);   // in the order they were asked
        public readonly List<int> Taken = new List<int>(4);           // beside each source, how many came from it
        public readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);   // ids already looked at in this start
        public bool Ran;              // the move was due at this start (it runs once per computer)
        public bool Finished;         // the marker was written: no later start moves anything

        public int Total
        {
            get
            {
                int total = 0;
                foreach (int n in Taken)
                {
                    total += n;
                }
                return total;
            }
        }

        public void Begin(List<string> sources)
        {
            Sources.Clear();
            Taken.Clear();
            Seen.Clear();
            Sources.AddRange(sources);
            for (int i = 0; i < sources.Count; i++)
            {
                Taken.Add(0);
            }
            Ran = true;
            Finished = false;
        }
    }

    /// <summary>
    /// The mod's options are kept in the game's PlayerPrefs, in the game's own format "m:" + id + ":" + option. The game names
    /// each copy of a mod differently (the Workshop copy by its item number, a ModsLocal copy by its folder's full path), so a
    /// player who moved between the Workshop and the installer, or had both, lost every choice. Every option is therefore kept
    /// under one id of the mod's own, whatever the game calls the copy; once per computer the values an older version kept under
    /// a copy's id are brought over, the running copy's first, and left where they were for an older RLO to read.
    /// Game-free, checked by EconomyCheck (OptionKeysChecks.cs). COST: nothing a frame; the move runs at one start only.
    /// </summary>
    public static class OptionKeysRules
    {
        public const string FixedId = "RealLifeOverhaul";      // the mod's name (its manifest and assembly): the same for every copy
        public const string WorkshopId = "3816536560";         // the id the game gives the Steam Workshop copy: its item number
        public const string LocalFolder = "RealLifeOverhaul";  // the folder in ModsLocal that the installer and the zip fill
        public const string MovedId = "options_moved";         // written once the move ran: a reset after it is never undone by it

        /// <summary>The key an option of the mod is kept under, in the game's own format, so the game's Mods tab reads the same value.</summary>
        public static string Key(string id) => "m:" + FixedId + ":" + id;

        /// <summary>The key an older version kept an option under for a copy the game called <paramref name="modId"/>. Only read.</summary>
        public static string OldKey(string modId, string id) => "m:" + modId + ":" + id;

        /// <summary>
        /// The older ids to look under, in order: the copy now running (what it kept is what the player last saw in it), then the
        /// Workshop copy, then the ModsLocal folder as the game writes it and with forward slashes. None twice, never the fixed id.
        /// </summary>
        public static List<string> Sources(string running, string localFolder)
        {
            var list = new List<string>(4);
            Add(list, running);
            Add(list, WorkshopId);
            if (!string.IsNullOrEmpty(localFolder))
            {
                string folder = localFolder.TrimEnd('/', '\\');
                Add(list, folder.Replace('/', '\\'));
                Add(list, folder.Replace('\\', '/'));
            }
            return list;
        }

        // The registry's value names do not tell case apart, so two ids that differ only in case are one place.
        private static void Add(List<string> list, string id)
        {
            if (string.IsNullOrEmpty(id) || string.Equals(id, FixedId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            foreach (string had in list)
            {
                if (string.Equals(had, id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            list.Add(id);
        }

        /// <summary>True while the move has not run on this computer.</summary>
        public static bool Due(IOptionStore store) => !store.Has(Key(MovedId));

        /// <summary>
        /// Each id with no value under the fixed id takes the first older one found, in the order of <see cref="OptionMove.Sources"/>.
        /// An id is looked at once a start, so a second pass over the page's rows counts nothing twice. Returns how many were taken.
        /// </summary>
        public static int Move(IOptionStore store, IEnumerable<string> ids, OptionMove move)
        {
            int taken = 0;
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id) || !move.Seen.Add(id))
                {
                    continue;
                }
                string to = Key(id);
                if (store.Has(to))
                {
                    continue;   // chosen under the fixed id already: an older copy's value never overrides it
                }
                for (int i = 0; i < move.Sources.Count; i++)
                {
                    string from = OldKey(move.Sources[i], id);
                    if (store.Has(from))
                    {
                        store.Copy(from, to);
                        move.Taken[i]++;
                        taken++;
                        break;
                    }
                }
            }
            return taken;
        }

        /// <summary>The move is done for good on this computer.</summary>
        public static void Finish(IOptionStore store, OptionMove move)
        {
            store.Mark(Key(MovedId));
            move.Finished = true;
        }

        /// <summary>Which older ids gave values, and how many each: "3816536560 (12), C:\...\RealLifeOverhaul (3)"; empty when none.</summary>
        public static string From(OptionMove move)
        {
            var parts = new List<string>(move.Sources.Count);
            for (int i = 0; i < move.Sources.Count; i++)
            {
                if (move.Taken[i] > 0)
                {
                    parts.Add(move.Sources[i] + " (" + move.Taken[i] + ")");
                }
            }
            return string.Join(", ", parts);
        }
    }
}
