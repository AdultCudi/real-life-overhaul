using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The player's own home screen: the apps they moved and where to, over the places of PhoneLayoutRules.Defaults. Only
    /// a move away from an app's usual place is kept, so a layout nobody touched is empty, and an app added in a later
    /// version starts in its usual folder.
    ///
    /// Written as one line, "1|RealLifeStocks=home|RealLifePlanner=life" (the version, then each move in the order made).
    /// A move of an app this version does not have is kept and used if that app comes; a part this version cannot read (a
    /// later version's folder) is kept as it was read and written back after the rest, so going back and forth between
    /// versions loses nothing.
    ///
    /// Plain strings and lists: no Unity or game types. Read once a session and written only when the player moves an app;
    /// checked offline (ContractCheck, PhoneLayoutChecks).
    /// </summary>
    public sealed class PhoneLayout
    {
        public const string Version = "1";        // the line's first part
        private const char Parts = '|', Sign = '=';
        public const int MostMoves = 64;          // moves kept: more than the mod has apps, so a line can never grow without end

        private readonly List<string> _ids = new List<string>(8);
        private readonly List<int> _places = new List<int>(8);
        private readonly List<string> _kept = new List<string>(2);   // parts read but not understood, written back as they were

        /// <summary>How many apps stand somewhere else than their usual place.</summary>
        public int Moves => _ids.Count;

        /// <summary>The parts of the line this version could not read, kept as they were.</summary>
        public int Kept => _kept.Count;

        /// <summary>Where an app stands: where the player put it, else its usual place.</summary>
        public int Place(string id)
        {
            int at = Find(id);
            return at >= 0 ? _places[at] : PhoneLayoutRules.DefaultOf(id);
        }

        /// <summary>Whether the player moved this app from its usual place.</summary>
        public bool Moved(string id)
        {
            return Find(id) >= 0;
        }

        /// <summary>
        /// Puts an app in a place. Back at its usual place the move is forgotten. False, and nothing changed, for a place this
        /// version does not have, an app with no name, or a layout holding <see cref="MostMoves"/> already.
        /// </summary>
        public bool Move(string id, int place)
        {
            if (string.IsNullOrEmpty(id) || !PhoneLayoutRules.Known(place) || id.IndexOf(Parts) >= 0 || id.IndexOf(Sign) >= 0)
            {
                return false;
            }
            int at = Find(id);
            if (place == PhoneLayoutRules.DefaultOf(id))
            {
                if (at >= 0)
                {
                    _ids.RemoveAt(at);
                    _places.RemoveAt(at);
                }
                return true;
            }
            if (at >= 0)
            {
                _places[at] = place;
                return true;
            }
            if (_ids.Count >= MostMoves)
            {
                return false;
            }
            _ids.Add(id);
            _places.Add(place);
            return true;
        }

        /// <summary>Every app back in its usual place, and nothing kept of another version's.</summary>
        public void Reset()
        {
            _ids.Clear();
            _places.Clear();
            _kept.Clear();
        }

        /// <summary>The layout as its one line.</summary>
        public string Write()
        {
            var line = new StringBuilder(16 + 32 * (_ids.Count + _kept.Count));
            line.Append(Version);
            for (int i = 0; i < _ids.Count; i++)
            {
                line.Append(Parts).Append(_ids[i]).Append(Sign).Append(PhoneLayoutRules.Token(_places[i]));
            }
            for (int i = 0; i < _kept.Count; i++)
            {
                line.Append(Parts).Append(_kept[i]);
            }
            return line.ToString();
        }

        /// <summary>
        /// A layout from its line: empty for none (every app in its usual place). A part that is no move, or names a place
        /// this version does not know, is kept as it was read; a line of another version is kept whole as its only part.
        /// </summary>
        public static PhoneLayout Read(string line)
        {
            var layout = new PhoneLayout();
            if (string.IsNullOrEmpty(line))
            {
                return layout;
            }
            string[] parts = line.Split(Parts);
            if (parts[0] != Version)
            {
                layout._kept.Add(line);   // (a later version's line: written back whole, after any move made in this one)
                return layout;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0)
                {
                    continue;
                }
                int sign = part.IndexOf(Sign);
                string id = sign > 0 ? part.Substring(0, sign) : null;
                if (id == null || !PhoneLayoutRules.FromToken(part.Substring(sign + 1), out int place) || layout.Find(id) >= 0 || layout._ids.Count >= MostMoves)
                {
                    if (layout._kept.Count < MostMoves)
                    {
                        layout._kept.Add(part);
                    }
                    continue;
                }
                if (place != PhoneLayoutRules.DefaultOf(id))
                {
                    layout._ids.Add(id);
                    layout._places.Add(place);
                }
            }
            return layout;
        }

        private int Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }
            for (int i = 0; i < _ids.Count; i++)
            {
                if (string.Equals(_ids[i], id, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
