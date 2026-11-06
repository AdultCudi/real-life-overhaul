using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The end game at scale's two tests on the options page. Each venture does its part through a bridge set by the wiring
    /// (Wiring/EndScaleLinks); one notice says what was done, or why nothing was. Nothing per frame.
    /// </summary>
    public static class EndScaleTests
    {
        /// <summary>Each venture's test (the data centres and cloud, broadband, the bank): true if it held something and was moved.</summary>
        public static Func<bool, bool> Infra, Broadband, Bank;

        /// <summary>Starts the four weeks of coming to scale now, or with <paramref name="full"/> brings every venture to the whole scale.</summary>
        public static void Phase(bool full)
        {
            if (!Settings.EndScale)
            {
                Notifications.Show(NotificationType.Warning, "reallife_endscale_test_off", null, 8f);
                return;
            }
            int moved = Run(Infra, full) + Run(Broadband, full) + Run(Bank, full);
            if (moved == 0)
            {
                Notifications.Show(NotificationType.Warning, "reallife_endscale_test_none", null, 8f);
                return;
            }
            Notifications.Show(NotificationType.Success, full ? "reallife_endscale_test_full_done" : "reallife_endscale_test_phase_done",
                new Dictionary<string, string>(2) { { "count", moved.ToString() }, { "days", EndScaleRules.PhaseDays.ToString() } }, 10f);
        }

        private static int Run(Func<bool, bool> test, bool full)
        {
            return test != null && test(full) ? 1 : 0;
        }
    }
}
