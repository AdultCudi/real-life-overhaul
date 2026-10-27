using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Options > Tests > Check the moods' definitions. Checks, against the game's own register of mood types, that every mood
    /// the mod can add (its own, family's and travel's) has a definition there, and that the game can read every entry of the
    /// open save's mood list (an entry with no definition makes PlayerController.Awake throw on the next load). Each is one line
    /// in Player.log, "Moods check (...): PASS" or FAIL and why, and the last line says PASS only if both did. Costs nothing
    /// unless it is run; it is one pass over a few dozen entries.
    /// </summary>
    public static class MoodsTest
    {
        private const int Named = 4;   // missing types named in the log line at most

        /// <summary>Runs the check once; a press with no save open says so in a notice.</summary>
        public static void Run()
        {
            try
            {
                if (SaveGameManager.Current == null)
                {
                    Say(NotificationType.Warning, "reallife_moods_test_none", null);
                    return;
                }
                var names = new List<string>();
                int missing = ModState.MissingFromRegister(names, out int total);
                int faults = 0;
                if (missing < 0)
                {
                    Line("definitions", "the game's register of mood types could not be reached, so none of the " + total + " moods can be added", false);
                    faults++;
                }
                else
                {
                    string said = (total - missing) + " of " + total + " moods have a definition in the game's register";
                    if (missing > 0)
                    {
                        said += "; missing: " + string.Join(", ", names.GetRange(0, Math.Min(Named, names.Count))) + (names.Count > Named ? ", ..." : string.Empty);
                    }
                    faults += Line("definitions", said, missing == 0);
                }
                int bad = ModState.UnreadableInList(out int entries);
                faults += bad < 0
                    ? Line("list", "the save's mood list could not be read against the register", false)
                    : Line("list", entries + " entr" + (entries == 1 ? "y" : "ies") + " in the save's mood list, " + bad + " the game cannot read", bad == 0);
                Meta.Dev.Always(ModState.Logger, "Moods check (all): " + (faults == 0 ? "PASS" : "FAIL, " + faults + " problem(s)"));
                if (faults == 0)
                {
                    Say(NotificationType.Success, "reallife_moods_test_pass", new Dictionary<string, string>
                    {
                        { "moods", total.ToString(CultureInfo.InvariantCulture) },
                        { "entries", entries.ToString(CultureInfo.InvariantCulture) }
                    });
                }
                else
                {
                    Say(NotificationType.Error, "reallife_moods_test_fail", new Dictionary<string, string> { { "faults", faults.ToString(CultureInfo.InvariantCulture) } });
                }
            }
            catch (Exception e)
            {
                ModState.Logger?.Error(e);
            }
        }

        private static int Line(string what, string said, bool pass)
        {
            Meta.Dev.Always(ModState.Logger, "Moods check (" + what + "): " + said + ": " + (pass ? "PASS" : "FAIL"));
            return pass ? 0 : 1;
        }

        private static void Say(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 8f, null, null, true, false);
        }
    }
}
