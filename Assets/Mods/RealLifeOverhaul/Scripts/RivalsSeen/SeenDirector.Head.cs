using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The director's part for the rivals' head offices (HeadOfficeRules): each rival is given a real office building of the city once, it is their office from then on (where
    /// their week has them in office hours, so they stand at its door as the rest of the world's rivals do), it is marked on the city map with their name, and their pages say
    /// where it is and show it on the map. The choice is made on the day's pass and only for a rival who has none; the map and the pages cost nothing between passes.
    /// COST: the city's offices are read and sorted once for a rival with no head office; a lookup in a short list when a page asks.
    /// </summary>
    public sealed partial class SeenDirector
    {
        private readonly List<OfficeSite> _sites = new List<OfficeSite>(64);
        private static readonly Func<string, bool> StillOffice = key => SeenGame.City.Table.Office(key, out object _, out float _, out float _);

        // Before the day's table is made: the rivals without a head office are given one, and every rival's head office is their office for the day.
        private void HeadOffices(int day)
        {
            if (HeadOfficeRules.Needs(_file, _subjects) && SeenGame.OfficeSites(_sites))
            {
                int made = HeadOfficeRules.Choose(_file, _subjects, _sites);
                if (made > 0)
                {
                    Logger?.Info("Rivals seen: " + made + " rival(s) given a head office from " + _sites.Count + " office building(s) of the city on day " + day + ": "
                                 + _file.Heads.Count + " on record, " + HeadsOwned() + " of them in a building the rival owns.");
                }
                _sites.Clear();
            }
            HeadOfficeRules.Apply(_file, _subjects);
        }

        // After the day's table is made: a head office whose building the city no longer has as an office is forgotten, to be chosen again; the map is made again.
        private void HeadMarks(int day)
        {
            int dropped = HeadOfficeRules.Drop(_file, StillOffice, _subjects);
            if (dropped > 0)
            {
                Logger?.Info("Rivals seen: " + dropped + " head office(s) are no longer office buildings of the city or belonged to a rival gone; they are chosen again on the next pass.");
            }
            int marked = SeenMap.Set(_subjects, SeenGame.City.Table);
            if (Verbose)
            {
                Logger?.Info("Rivals seen check (head offices, day " + day + "): " + _file.Heads.Count + " on record, " + marked + " on the city map, " + SeenGame.City.Table.OfficesFound + " of "
                             + SeenGame.City.Table.Offices + " office(s) found in the city: " + (marked <= _file.Heads.Count ? "PASS" : "MORE MARKS THAN OFFICES"));
            }
        }

        private int HeadsOwned()
        {
            int n = 0;
            for (int i = 0; i < _file.Heads.Count; i++)
            {
                n += _file.Heads[i].Owned ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// A rival's head office for a page, by their key ("t" and the tycoon's index, "g" and the game's own id): its address in words, its district in words and the hours they
        /// are found there. False for somebody with none, gone, or while the feature does not run.
        /// </summary>
        public bool HeadOf(string key, out string address, out string district)
        {
            address = district = null;
            SeenFile f = File;
            SeenHead h = f != null && !string.IsNullOrEmpty(key) ? f.Head(key) : null;
            SeenSubject s = h != null ? SubjectOf(key) : null;
            if (h == null || s == null || s.Gone)
            {
                return false;
            }
            address = SeenGame.AddressText(h.Office);
            district = string.IsNullOrEmpty(h.District) ? string.Empty : SeenGame.DistrictName(h.District);
            return true;
        }

        /// <summary>Adds the building of every head office on record ("street#number") to a set, for what must leave them alone (the rivals' holdings in the property business). Nothing with none.</summary>
        public void HeadBuildings(ICollection<string> into)
        {
            SeenFile f = File;
            for (int i = 0; f != null && into != null && i < f.Heads.Count; i++)
            {
                into.Add(f.Heads[i].Office);
            }
        }

        /// <summary>A rival's head office as one line for a narrow place on a page: the address and the district; empty with none.</summary>
        public string HeadLine(string key)
        {
            return HeadOf(key, out string address, out string district) ? (district.Length > 0 ? address + ", " + district : address) : string.Empty;
        }

        /// <summary>The head office of a tycoon (by their place on the rich list) as one line; empty with none.</summary>
        public string TycoonHead(int index) => HeadLine("t" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>The head office of one of the game's rival owners (by their id) as one line; empty with none.</summary>
        public string OwnerHead(string gameId) => string.IsNullOrEmpty(gameId) ? string.Empty : HeadLine("g" + gameId);

        /// <summary>
        /// Show on map for a rival's head office: the game's own destination marker on its building, as the Careers pages' button sets one, and a notice saying where it is.
        /// True when it was shown.
        /// </summary>
        public bool ShowHead(string key)
        {
            SeenFile f = File;
            SeenHead h = f != null && !string.IsNullOrEmpty(key) ? f.Head(key) : null;
            SeenSubject s = h != null ? SubjectOf(key) : null;
            if (h == null || s == null || s.Gone || !SeenRules.Address(h.Office, out int cut, out int number) || cut <= 0)
            {
                Notifications.ShowError("reallife_seen_head_gone");
                return false;
            }
            if (!Careers.DeskGame.Route(h.Office.Substring(0, cut), number))
            {
                Notifications.ShowError("reallife_career_why_route");
                return false;
            }
            Notifications.Show(NotificationType.Success, "reallife_seen_head_shown",
                new Dictionary<string, string>(2) { { "name", s.Name }, { "address", SeenGame.AddressText(h.Office) } });
            return true;
        }

        public bool ShowTycoonHead(int index) => ShowHead("t" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public bool ShowOwnerHead(string gameId) => !string.IsNullOrEmpty(gameId) && ShowHead("g" + gameId);

        /// <summary>Options, Tests: how many rivals have a head office on record, in a notice and the log; the first of them is shown on the map.</summary>
        public void TestHead()
        {
            if (!TestReady())
            {
                return;
            }
            var names = new System.Text.StringBuilder(160);
            int count = 0;
            string first = null;
            for (int i = 0; i < _subjects.Count; i++)
            {
                SeenSubject s = _subjects[i];
                string line = s != null ? HeadLine(s.Key) : string.Empty;
                if (line.Length == 0)
                {
                    continue;
                }
                count++;
                first = first ?? s.Key;
                Logger?.Info("Rivals seen check (head office): " + s.Name + " has theirs at " + line
                             + (_file.Head(s.Key).Owned ? ", a building they own" : ", an office building fitted to their rank") + ".");
                if (count <= 3)
                {
                    names.Append(names.Length > 0 ? "; " : string.Empty).Append(s.Name).Append(": ").Append(line);
                }
            }
            SeenGame.Tell(count > 0 ? NotificationType.Info : NotificationType.Warning, count > 0 ? "reallife_seen_test_head" : "reallife_seen_test_head_none",
                new Dictionary<string, string>(2) { { "count", count.ToString(System.Globalization.CultureInfo.InvariantCulture) }, { "names", names.ToString() } });
            Logger?.Info("Rivals seen check (head offices): " + count + " of " + _subjects.Count + " rival(s) have one on record, " + SeenGame.City.Table.Offices
                         + " office(s) looked for in the city: PASS");
            if (first != null)
            {
                ShowHead(first);
            }
        }

        private SeenSubject SubjectOf(string key)
        {
            for (int i = 0; i < _subjects.Count; i++)
            {
                if (_subjects[i] != null && _subjects[i].Key == key)
                {
                    return _subjects[i];
                }
            }
            return null;
        }
    }
}
