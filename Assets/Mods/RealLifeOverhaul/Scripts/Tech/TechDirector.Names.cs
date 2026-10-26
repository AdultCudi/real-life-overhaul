using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Names that are not shared: the list of every name in use that a new name is checked against, and the repair that gives a
    /// second bearer of a name a new one (<see cref="StaffNameRules"/>). The list is made when a name is wanted and the repair
    /// runs when the people part loads and each game day, one pass over the staff, the candidates and the seekers; no frame cost.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly HashSet<string> _nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<EmployeeInstance> _namedStaff = new List<EmployeeInstance>(64);
        private readonly List<EmployeeInstance> _namedWaiting = new List<EmployeeInstance>(16);
        private readonly List<bool> _namedMine = new List<bool>(64);
        private readonly List<string> _holdNames = new List<string>(128);
        private readonly List<int> _holdKind = new List<int>(128);
        private readonly List<int> _holdAt = new List<int>(128);
        private readonly List<int> _doubled = new List<int>(8);

        private const int HoldStaff = 0, HoldPayroll = 1, HoldWaiting = 2, HoldSeeker = 3;

        /// <summary>
        /// Every name in use: the game's hired staff and waiting candidates, the mod's payroll and the seekers kept in the save. The
        /// list is cleared and filled afresh and is the director's own, so a caller adds the names it makes and does not keep it.
        /// </summary>
        internal HashSet<string> NameSet()
        {
            _nameSet.Clear();
            StaffNamesGame.Names(_nameSet);
            for (int i = 0; _talent != null && i < _talent.People.Count; i++)
            {
                AddName(_talent.People[i].Name);
            }
            for (int i = 0; _hiringOn && _hiring != null && i < _hiring.Seekers.Count; i++)
            {
                AddName(_hiring.Seekers[i].P.Name);
            }
            return _nameSet;
        }

        private void AddName(string name)
        {
            if (StaffNameRules.IsName(name))
            {
                _nameSet.Add(name.Trim());
            }
        }

        private void Hold(string name, int kind, int at)
        {
            _holdNames.Add(name);
            _holdKind.Add(kind);
            _holdAt.Add(at);
        }

        /// <summary>
        /// Gives each second bearer of a name a new one of their sex, the first keeping theirs: hired staff in the order the save
        /// lists them, then the payroll, the candidates waiting, the seekers. Only the player's own are renamed. Returns how many.
        /// </summary>
        internal int MendNames(int day, bool tell)
        {
            if (!_peopleOn || _talent == null || Save == null)
            {
                return 0;
            }
            StaffNamesGame.Hired(_namedStaff, _namedMine);
            StaffNamesGame.Candidates(_namedWaiting);
            _holdNames.Clear();
            _holdKind.Clear();
            _holdAt.Clear();
            for (int i = 0; i < _namedStaff.Count; i++)
            {
                Hold(StaffNamesGame.Name(_namedStaff[i]), HoldStaff, i);
            }
            for (int i = 0; i < _talent.People.Count; i++)
            {
                if (_talent.People[i].Payroll)
                {
                    Hold(_talent.People[i].Name, HoldPayroll, i);
                }
            }
            for (int i = 0; i < _namedWaiting.Count; i++)
            {
                Hold(StaffNamesGame.Name(_namedWaiting[i]), HoldWaiting, i);
            }
            for (int i = 0; _hiringOn && _hiring != null && i < _hiring.Seekers.Count; i++)
            {
                Hold(_hiring.Seekers[i].P.Name, HoldSeeker, i);
            }
            if (StaffNameRules.Doubles(_holdNames, _doubled) == 0)
            {
                return 0;
            }
            HashSet<string> taken = NameSet();
            int renamed = 0, staff = 0;
            for (int d = 0; d < _doubled.Count; d++)
            {
                int hold = _doubled[d], at = _holdAt[hold];
                string old = _holdNames[hold], now;
                switch (_holdKind[hold])
                {
                    case HoldStaff:
                        if (!_namedMine[at])
                        {
                            continue;   // another player's: not ours to rename
                        }
                        now = StaffNamesGame.FreshFor(taken, StaffNamesGame.Female(_namedStaff[at]));
                        StaffNamesGame.Rename(_namedStaff[at], now);
                        staff++;
                        break;
                    case HoldPayroll:
                        now = StaffNamesGame.FreshFor(taken, _talent.People[at].Female);
                        _talent.People[at].Name = now;
                        break;
                    case HoldWaiting:
                        now = StaffNamesGame.FreshFor(taken, StaffNamesGame.Female(_namedWaiting[at]));
                        StaffNamesGame.Rename(_namedWaiting[at], now);
                        break;
                    default:
                        Seeker s = _hiring.Seekers[at];
                        now = StaffNamesGame.FreshFor(taken, s.P.Female);
                        s.P.Name = now;
                        break;
                }
                renamed++;
                Logger?.Info("Tech: a second " + old + " on day " + day + " is now " + now + ".");
            }
            if (renamed > 0 && tell)
            {
                Tell(NotificationType.Info, "reallife_names_note_mended", Data("count", renamed.ToString(CultureInfo.InvariantCulture)), TabTeam, null);
            }
            if (staff > 0)
            {
                Changed();
            }
            return renamed;
        }

        /// <summary>
        /// Mends the names that are doubled now and rolls twenty new ones against every name in use. PASS when none of the twenty
        /// is in use or repeats another, and no name is doubled after the repair.
        /// </summary>
        public void TestNames()
        {
            if (!_peopleOn || _talent == null || Save == null)
            {
                Notifications.ShowError("reallife_names_test_off");
                return;
            }
            int mended = MendNames(Save.Day, true);
            var inUse = new HashSet<string>(NameSet(), StringComparer.OrdinalIgnoreCase);
            var probe = new HashSet<string>(inUse, StringComparer.OrdinalIgnoreCase);
            int clashes = 0;
            for (int i = 0; i < 20; i++)
            {
                int before = probe.Count;
                string name = StaffNamesGame.Fresh(probe, out _);
                clashes += probe.Count == before + 1 && !inUse.Contains(name.Trim()) ? 0 : 1;
            }
            int left = MendNames(Save.Day, false);
            bool ok = clashes == 0 && left == 0;
            Logger?.Info("Names check (the player's company, day " + Save.Day + "): " + inUse.Count + " name(s) in use, " + mended + " doubled name(s) mended, 20 new names rolled with "
                         + clashes + " in use already or repeated, " + left + " still doubled: " + (ok ? "PASS" : "FAIL"));
            Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_names_test_pass" : "reallife_names_test_fail",
                Data("count", mended.ToString(CultureInfo.InvariantCulture)), 10f);
        }
    }
}
