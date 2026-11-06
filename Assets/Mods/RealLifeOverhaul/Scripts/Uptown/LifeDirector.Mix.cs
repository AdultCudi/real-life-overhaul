using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// "Uptown: units to rent and the business mix": the census of the mix pass's work, one "Uptown check (mix)" line in
    /// Player.log and a notice. Of its shops, offices and homes, how many stand free to rent against the third kept free;
    /// of its businesses, how many of each kind, the mod's own shops among them; whether the mix pass has run. Nothing here
    /// runs a frame.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly int[] MixUnits = new int[3], MixFree = new int[3];
        private static readonly Dictionary<string, int> MixKinds = new Dictionary<string, int>(64, StringComparer.Ordinal);
        private static readonly string[] MixWhat = { "shops", "offices", "homes" };

        /// <summary>"Uptown: units to rent and the business mix": the census line, and a notice of its shares.</summary>
        public static void TestMixCensus()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                LifeGame.MixCensus(Standing, Another, MixUnits, MixFree, MixKinds);
                string why = !_state.Mixed ? "the mix pass has not run in this save" : null;
                var shares = new System.Text.StringBuilder(160);
                for (int t = 0; t < MixWhat.Length; t++)
                {
                    why = why ?? MixRules.ShareWhy(MixWhat[t], MixUnits[t], MixFree[t], MixRules.ToLetShare);
                    shares.Append(t > 0 ? ", " : string.Empty).Append(MixWhat[t]).Append(' ').Append(MixFree[t].ToString(Plain)).Append(" of ")
                          .Append(MixUnits[t].ToString(Plain)).Append(" (").Append(OfficesRules.Share(MixFree[t], MixUnits[t]).ToString(Plain)).Append(" %)");
                }
                int own = 0;
                foreach (KeyValuePair<string, int> kind in MixKinds)
                {
                    own += kind.Key.StartsWith("reallife_", StringComparison.Ordinal) ? kind.Value : 0;
                }
                bool pass = why == null;
                string line = "Uptown check (mix): free to rent " + shares + " against " + MixRules.ToLetShare + " % kept free; " + MixKinds.Count + " kind(s) of business ("
                              + MixRules.Spread(MixKinds) + "), " + own + " of them the mod's own shops; the mix pass " + (_state.Mixed ? "has run" : "has not run")
                              + (why != null ? " (" + why + ")" : string.Empty) + ": " + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(line);
                }
                else
                {
                    Logger?.Warn(line);
                }
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.MixDone, new Dictionary<string, string>
                {
                    { "shops", OfficesRules.Share(MixFree[0], MixUnits[0]).ToString(Plain) }, { "offices", OfficesRules.Share(MixFree[1], MixUnits[1]).ToString(Plain) },
                    { "homes", OfficesRules.Share(MixFree[2], MixUnits[2]).ToString(Plain) }, { "kinds", MixKinds.Count.ToString(Plain) }
                }, 14f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
