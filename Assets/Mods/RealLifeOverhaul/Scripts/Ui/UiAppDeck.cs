using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The player's own choice of apps for the RLO interface: which apps stand on the status bar's row and in what order
    /// (the bar), and which are favourites in All apps and in what order. Two lists, as the player asked for two things: the
    /// bar is a few apps within one press at all times and is limited by the strip's room, a favourite is a mark that costs
    /// nothing and has no limit but the apps the player has. The bar is null until the player first edits it, and then the
    /// row is the phone's first home page exactly as before. Ids that are not among the apps now (a feature off, another mod
    /// gone, not unlocked yet) keep their place and are skipped, so they return where they were. Stored as one line,
    /// "1|bar=Persona,Contacts|fav=BizMan", through <see cref="Load"/> and <see cref="Save"/> (the game side keeps it with
    /// the game's settings on this computer, as the windows' places); a line of another version or a damaged one reads as
    /// the default. Game-free; cost: a few list walks on a press, nothing a frame.
    /// </summary>
    public sealed class UiAppDeck
    {
        public const string Version = "1";
        public const int BarMost = 16;       // apps the bar can hold at its widest; the strip's room cuts it shorter (UiDraw.PlanPills)
        private const int BarKept = 32;      // ids kept in the bar's line, absent apps among them
        private const int FavKept = 64;
        private const int IdMost = 64;
        private const char Parts = '|', Sign = '=', Each = ',';

        /// <summary>Where the line comes from and goes to; null offline (the deck then lives in memory).</summary>
        public static Func<string> Load;
        public static Action<string> Save;

        private static UiAppDeck _current;

        private List<string> _bar;   // null: the default row
        private readonly List<string> _fav = new List<string>(8);

        /// <summary>Moves with every change, so what shows the deck lays itself out again.</summary>
        public int Revision { get; private set; }

        /// <summary>The player's deck, read once.</summary>
        public static UiAppDeck Current => _current ?? (_current = Read(Fetch()));

        /// <summary>Forgets what was read, so the next ask reads the line again (a check's, or the options' reset).</summary>
        public static void Forget() => _current = null;

        public bool Custom => _bar != null;
        public int BarCount => _bar == null ? 0 : _bar.Count;
        public int FavCount => _fav.Count;
        public string BarAt(int i) => _bar[i];
        public string FavAt(int i) => _fav[i];

        public bool InBar(string id) => _bar != null && _bar.IndexOf(id) >= 0;
        public bool IsFav(string id) => _fav.IndexOf(id) >= 0;

        /// <summary>The line to keep.</summary>
        public string Write()
        {
            var line = new StringBuilder(32 + 24 * (BarCount + _fav.Count));
            line.Append(Version);
            if (_bar != null)
            {
                line.Append(Parts).Append("bar").Append(Sign).Append(string.Join(Each.ToString(), _bar.ToArray()));
            }
            if (_fav.Count > 0)
            {
                line.Append(Parts).Append("fav").Append(Sign).Append(string.Join(Each.ToString(), _fav.ToArray()));
            }
            return line.ToString();
        }

        /// <summary>A deck from its line; the default one for no line, another version's or a damaged one.</summary>
        public static UiAppDeck Read(string line)
        {
            var deck = new UiAppDeck();
            if (string.IsNullOrEmpty(line))
            {
                return deck;
            }
            string[] parts = line.Split(Parts);
            if (parts[0] != Version)
            {
                return deck;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                int sign = parts[i].IndexOf(Sign);
                if (sign <= 0)
                {
                    continue;
                }
                string name = parts[i].Substring(0, sign);
                if (name == "bar")
                {
                    deck._bar = Ids(parts[i].Substring(sign + 1), BarKept);
                }
                else if (name == "fav")
                {
                    deck._fav.AddRange(Ids(parts[i].Substring(sign + 1), FavKept));
                }
            }
            return deck;
        }

        // The ids of a part, a clean unique list: nothing empty, nothing too long, none twice, no more than the most.
        private static List<string> Ids(string text, int most)
        {
            var ids = new List<string>(8);
            string[] each = text.Split(Each);
            for (int i = 0; i < each.Length && ids.Count < most; i++)
            {
                if (each[i].Length > 0 && each[i].Length <= IdMost && ids.IndexOf(each[i]) < 0)
                {
                    ids.Add(each[i]);
                }
            }
            return ids;
        }

        private static bool Usable(string id) => !string.IsNullOrEmpty(id) && id.Length <= IdMost && id.IndexOf(Parts) < 0 && id.IndexOf(Sign) < 0 && id.IndexOf(Each) < 0;

        private static string Fetch()
        {
            try
            {
                return Load != null ? Load() : null;
            }
            catch (Exception)
            {
                return null;   // an unreadable store is the default deck, never a fault
            }
        }

        /// <summary>Keeps the deck after a change (not while a drag is still moving it).</summary>
        public void Commit()
        {
            try
            {
                Save?.Invoke(Write());
            }
            catch (Exception)
            {
                // The deck stands for this session; the next change tries the store again.
            }
        }

        /// <summary>Puts the deck back as a line left it (a drag cancelled).</summary>
        public void Restore(string line)
        {
            UiAppDeck was = Read(line);
            _bar = was._bar;
            _fav.Clear();
            _fav.AddRange(was._fav);
            Revision++;
        }

        // ------------------------------------------------------------------------------------------------ the apps now

        /// <summary>
        /// Sets each app's flags and the row's order from the deck. The default deck leaves the row as the phone's first home
        /// page gives it (each app's DefaultRow); a chosen bar is its ids in order, those that are not among the apps skipped.
        /// <paramref name="row"/> receives the apps' indexes in the order the bar shows them.
        /// </summary>
        public void Apply(List<UiShellApp> apps, List<int> row)
        {
            row.Clear();
            if (_bar == null)
            {
                for (int i = 0; i < apps.Count; i++)
                {
                    UiShellApp a = apps[i];
                    a.InRow = a.DefaultRow;
                    a.Favourite = IsFav(a.Id);
                    apps[i] = a;
                    if (a.InRow)
                    {
                        row.Add(i);
                    }
                }
                return;
            }
            for (int b = 0; b < _bar.Count && row.Count < BarMost; b++)
            {
                int at = IndexOf(apps, _bar[b]);
                if (at >= 0)
                {
                    row.Add(at);
                }
            }
            for (int i = 0; i < apps.Count; i++)
            {
                UiShellApp a = apps[i];
                a.InRow = row.Contains(i);
                a.Favourite = IsFav(a.Id);
                apps[i] = a;
            }
        }

        /// <summary>The favourites among the apps, in the player's order.</summary>
        public void Favourites(List<UiShellApp> apps, List<int> into)
        {
            into.Clear();
            for (int f = 0; f < _fav.Count; f++)
            {
                int at = IndexOf(apps, _fav[f]);
                if (at >= 0)
                {
                    into.Add(at);
                }
            }
        }

        private static int IndexOf(List<UiShellApp> apps, string id)
        {
            for (int i = 0; i < apps.Count; i++)
            {
                if (string.Equals(apps[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------------------------------------------- the bar

        // The bar the player edits: the default row's ids first time, so a change starts from what they see.
        private void Own(List<UiShellApp> apps)
        {
            if (_bar != null)
            {
                return;
            }
            _bar = new List<string>(BarMost);
            for (int i = 0; i < apps.Count; i++)
            {
                if (apps[i].DefaultRow)
                {
                    _bar.Add(apps[i].Id);
                }
            }
        }

        /// <summary>An app onto the bar's end; false when it is there already, has no usable id, or the bar is full.</summary>
        public bool PutOnBar(string id, List<UiShellApp> apps)
        {
            if (!Usable(id) || IndexOf(apps, id) < 0)
            {
                return false;
            }
            Own(apps);
            if (_bar.IndexOf(id) >= 0 || Present(_bar, apps) >= BarMost)
            {
                return false;
            }
            if (_bar.Count >= BarKept)
            {
                DropAbsent(apps);   // an old bar full of apps that are gone makes room for one that is here
            }
            if (_bar.Count >= BarKept)
            {
                return false;
            }
            _bar.Add(id);
            Revision++;
            return true;
        }

        /// <summary>An app off the bar; false when it was not on it.</summary>
        public bool TakeOffBar(string id, List<UiShellApp> apps)
        {
            Own(apps);
            if (!_bar.Remove(id))
            {
                return false;
            }
            Revision++;
            return true;
        }

        /// <summary>An app one place along the bar (-1 towards the left), past apps that are not here; false at its end or when it is not on the bar.</summary>
        public bool MoveBar(string id, int step, List<UiShellApp> apps)
        {
            Own(apps);
            return Shift(_bar, id, step, apps);
        }

        /// <summary>An app to the place of the n-th app of the bar that is here (a drag's drop); false when nothing moved.</summary>
        public bool MoveBarTo(string id, int place, List<UiShellApp> apps)
        {
            Own(apps);
            return PlaceAt(_bar, id, place, apps);
        }

        /// <summary>The default row again, favourites kept.</summary>
        public bool ResetBar()
        {
            if (_bar == null)
            {
                return false;
            }
            _bar = null;
            Revision++;
            return true;
        }

        // -------------------------------------------------------------------------------------------------- favourites

        /// <summary>Marks or unmarks an app as a favourite; a new one stands last. False for an id that cannot be kept or too many.</summary>
        public bool ToggleFav(string id)
        {
            if (!Usable(id))
            {
                return false;
            }
            if (!_fav.Remove(id))
            {
                if (_fav.Count >= FavKept)
                {
                    return false;
                }
                _fav.Add(id);
            }
            Revision++;
            return true;
        }

        public bool MoveFav(string id, int step, List<UiShellApp> apps) => Shift(_fav, id, step, apps);

        public bool MoveFavTo(string id, int place, List<UiShellApp> apps) => PlaceAt(_fav, id, place, apps);

        // ------------------------------------------------------------------------------------------------------ moving

        private bool Shift(List<string> list, string id, int step, List<UiShellApp> apps)
        {
            int at = list.IndexOf(id);
            if (at < 0 || step == 0)
            {
                return false;
            }
            int dir = step < 0 ? -1 : 1, to = at + dir;
            while (to >= 0 && to < list.Count && IndexOf(apps, list[to]) < 0)
            {
                to += dir;   // an app that is gone keeps its place: step over it
            }
            if (to < 0 || to >= list.Count)
            {
                return false;
            }
            list.RemoveAt(at);
            list.Insert(to, id);
            Revision++;
            return true;
        }

        private bool PlaceAt(List<string> list, string id, int place, List<UiShellApp> apps)
        {
            int at = list.IndexOf(id);
            if (at < 0 || place < 0)
            {
                return false;
            }
            int seen = -1, to = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (i == at || IndexOf(apps, list[i]) >= 0)
                {
                    seen++;
                    if (seen == place)
                    {
                        to = i;
                        break;
                    }
                }
            }
            if (to < 0 || to == at)
            {
                return false;
            }
            list.RemoveAt(at);
            list.Insert(to, id);
            Revision++;
            return true;
        }

        private static int Present(List<string> list, List<UiShellApp> apps)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                n += IndexOf(apps, list[i]) >= 0 ? 1 : 0;
            }
            return n;
        }

        private void DropAbsent(List<UiShellApp> apps)
        {
            for (int i = _bar.Count - 1; i >= 0 && _bar.Count >= BarKept; i--)
            {
                if (IndexOf(apps, _bar[i]) < 0)
                {
                    _bar.RemoveAt(i);
                }
            }
        }
    }
}
