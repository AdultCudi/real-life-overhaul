using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The set hours of part-timers (AvailabilityRules) in the company's staff day: the new hires told of theirs, the day's shifts outside them told once, and the test tool.
    /// Cost: a pass over the staff list on a day that has begun, which the staff part makes anyway.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly List<StaffFile> _hired = new List<StaffFile>(4);

        /// <summary>
        /// After the staff were read on a new day: each new hire with set hours is told of them (one by name, several as a count), and the part-timers who worked outside
        /// theirs yesterday (and so lost mood, <see cref="AvailabilityGame.Judge"/>) are told in one notice. Nothing with the option off.
        /// </summary>
        private void AvailabilityDay()
        {
            _hired.Clear();
            List<StaffFile> staff = _state.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                if (staff[i].Fresh)
                {
                    staff[i].Fresh = false;
                    if (Settings.StaffAvailability && staff[i].Window != AvailabilityKind.None)
                    {
                        _hired.Add(staff[i]);
                    }
                }
            }
            if (_hired.Count == 1)
            {
                EmployeeInstance e = CompanyGame.Employee(_hired[0].Id);
                AvailabilityWindow w = AvailabilityGame.Of(e);
                Tell(NotificationType.Info, "reallife_avail_note_hired", new Dictionary<string, string> { { "name", _hired[0].Name }, { "words", AvailabilityGame.Words(w) } },
                    TabStaff, "emp:" + _hired[0].Id);
            }
            else if (_hired.Count > 1)
            {
                Tell(NotificationType.Info, "reallife_avail_note_hired_many", new Dictionary<string, string> { { "count", _hired.Count.ToString() } }, TabStaff, null);
            }
            _hired.Clear();
            int breached = AvailabilityGame.BreachCount;
            if (breached == 1)
            {
                Tell(NotificationType.Warning, "reallife_avail_note_breach", new Dictionary<string, string>
                {
                    { "name", AvailabilityGame.BreachName }, { "hours", AvailabilityGame.BreachHours.ToString() }
                }, TabStaff, null);
            }
            else if (breached > 1)
            {
                Tell(NotificationType.Warning, "reallife_avail_note_breach_many", new Dictionary<string, string> { { "count", breached.ToString() } }, TabStaff, null);
            }
            AvailabilityGame.EndDay();
        }

        /// <summary>"Test: part-timers' set hours": how many of the staff keep them, by kind, and the hours of the first, in a notice and the log.</summary>
        public void TestAvailability()
        {
            const string test = "Part-timers' set hours";
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.Economy || !Settings.EconomyCompany || !Settings.StaffAvailability)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            int[] kinds = new int[5];
            string first = null, words = null;
            for (int i = 0; _state != null && i < _state.Staff.Count; i++)
            {
                AvailabilityWindow w = AvailabilityGame.Of(CompanyGame.Employee(_state.Staff[i].Id));
                kinds[(int)w.Kind]++;
                if (!w.None && first == null)
                {
                    first = _state.Staff[i].Name;
                    words = AvailabilityGame.Words(w);
                }
            }
            int total = kinds[1] + kinds[2] + kinds[3] + kinds[4];
            Logger?.Info("Company test: " + total + " of " + (_state != null ? _state.Staff.Count : 0) + " staff keep set hours (" + kinds[1] + " students, " + kinds[2] + " parents, "
                         + kinds[3] + " early risers, " + kinds[4] + " weekenders)" + (first != null ? "; " + first + ": " + words : string.Empty) + ".");
            if (total == 0)
            {
                TestRefusals.TellKey(Logger, test, "reallife_avail_test_none");
                return;
            }
            Notify(NotificationType.Info, "reallife_avail_test_result", new Dictionary<string, string> { { "count", total.ToString() }, { "name", first }, { "words", words } }, 12f);
        }
    }
}
