using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>Who has one of the city's buildings, as the office supply count reads its registration.</summary>
    public enum LetState
    {
        ToLet,      // standing empty and to let: nobody's, no business, nothing left in it
        Business,   // a business of the game's own (the city's AI)
        Rival,      // a rival's
        Players,    // the player's, or in a Going Public session another player's (rented or owned)
        Other       // not to let, or a layout or furniture left in it
    }

    /// <summary>
    /// The city's buildings of one kind counted by who has them, and the empty ones the mod's features hold: how many
    /// stood empty before the mod held any and how many are left for the player, in all and by district, so the log
    /// shows whether a player can still find one to rent. Game-free.
    /// </summary>
    public sealed class OfficeTally
    {
        private readonly int[] _states = new int[5];
        private readonly SortedDictionary<string, int> _held = new SortedDictionary<string, int>(StringComparer.Ordinal);       // empty ones held, by feature
        private readonly SortedDictionary<string, int> _heldBusy = new SortedDictionary<string, int>(StringComparer.Ordinal);   // others held, by feature
        private readonly SortedDictionary<string, int[]> _districts = new SortedDictionary<string, int[]>(StringComparer.Ordinal);   // empty, left for the player

        public int Count { get; private set; }

        /// <summary>Standing empty and to let, held by the mod or not: the supply before the mod held any.</summary>
        public int Empty => _states[(int)LetState.ToLet];

        public int Held { get; private set; }

        public int Free => Empty - Held;

        public int Of(LetState state) => _states[(int)state];

        public static LetState Classify(bool rentedByPlayer, bool ownedByPlayer, string rivalId, string businessType, bool forRent, bool layout, int items)
        {
            if (rentedByPlayer || ownedByPlayer)
            {
                return LetState.Players;
            }
            if (!string.IsNullOrEmpty(rivalId))
            {
                return LetState.Rival;
            }
            if (!string.IsNullOrEmpty(businessType) && businessType != "ba:businesstype_empty")
            {
                return LetState.Business;
            }
            return forRent && !layout && items == 0 ? LetState.ToLet : LetState.Other;
        }

        /// <summary>The feature a book id belongs to, in the log's words.</summary>
        public static string Feature(string holder)
        {
            if (string.IsNullOrEmpty(holder))
            {
                return null;
            }
            if (holder.StartsWith("techhq_", StringComparison.Ordinal))
            {
                return "the tech companies' towers";
            }
            if (holder.StartsWith("police_", StringComparison.Ordinal))
            {
                return "the police stations";
            }
            if (holder.StartsWith("hotel_", StringComparison.Ordinal))
            {
                return "the hotels";
            }
            switch (holder)
            {
                case "utilities_office": return "City Utilities";
                case "campus": return "the university";
                case "courthouse": return "the courthouse";
                default: return holder;
            }
        }

        /// <summary>Counts one building; <paramref name="holder"/> is the book id of the feature holding it, or null.</summary>
        public void Add(string district, LetState state, string holder)
        {
            Count++;
            _states[(int)state]++;
            string feature = Feature(holder);
            if (feature != null)
            {
                SortedDictionary<string, int> into = state == LetState.ToLet ? _held : _heldBusy;
                into.TryGetValue(feature, out int n);
                into[feature] = n + 1;
            }
            if (state != LetState.ToLet)
            {
                return;
            }
            Held += feature != null ? 1 : 0;
            string d = string.IsNullOrEmpty(district) ? "?" : district;
            if (!_districts.TryGetValue(d, out int[] row))
            {
                row = new int[2];
                _districts[d] = row;
            }
            row[0]++;
            row[1] += feature != null ? 0 : 1;
        }

        public void District(string district, out int empty, out int free)
        {
            empty = free = 0;
            if (district != null && _districts.TryGetValue(district, out int[] row))
            {
                empty = row[0];
                free = row[1];
            }
        }

        /// <summary>The log's line for the kind counted ("office", "retail", "warehouse").</summary>
        public string Line(string kind)
        {
            var b = new StringBuilder(512);
            b.Append("Office supply: ").Append(Count).Append(' ').Append(kind).Append(" buildings of the city's own; ").Append(Empty)
             .Append(" stood empty and to let before the mod held any; the mod holds ").Append(Held).Append(" of them");
            Features(b, _held);
            b.Append(", and ").Append(Free).Append(Free == 1 ? " is" : " are").Append(" left for the player; ")
             .Append(Of(LetState.Business)).Append(" a business of the game's, ").Append(Of(LetState.Rival)).Append(" a rival's, ")
             .Append(Of(LetState.Players)).Append(" the player's (or another player's in a session), ").Append(Of(LetState.Other)).Append(" not to let or with something left in");
            if (_heldBusy.Count > 0)
            {
                b.Append("; held by the mod with a business or a player in: ");
                bool first = true;
                foreach (KeyValuePair<string, int> f in _heldBusy)
                {
                    b.Append(first ? string.Empty : ", ").Append(f.Key).Append(' ').Append(f.Value);
                    first = false;
                }
            }
            if (_districts.Count > 0)
            {
                b.Append(". By district, empty and to let / left for the player:");
                bool first = true;
                foreach (KeyValuePair<string, int[]> d in _districts)
                {
                    b.Append(first ? " " : ", ").Append(d.Key).Append(' ').Append(d.Value[0]).Append('/').Append(d.Value[1]);
                    first = false;
                }
            }
            return b.Append('.').ToString();
        }

        private static void Features(StringBuilder b, SortedDictionary<string, int> features)
        {
            if (features.Count == 0)
            {
                return;
            }
            b.Append(" (");
            bool first = true;
            foreach (KeyValuePair<string, int> f in features)
            {
                b.Append(first ? string.Empty : ", ").Append(f.Key).Append(' ').Append(f.Value);
                first = false;
            }
            b.Append(')');
        }
    }
}
