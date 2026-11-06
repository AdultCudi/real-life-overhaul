using System;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The test of the construction objectives (Options > Tests; also in the city expansion's test run): the lines written at
    /// once and the game's objectives window looked at, holding them only while something is under way, in order and capped.
    /// It writes one "Construction check (objectives)" line ending PASS or FAIL, and a notice, and is a public static method,
    /// so a runner can call it with no press. Nothing is changed.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>Test: the objectives written now, and the game's objectives window holding them only while something is under way.</summary>
        public static bool TestTrackNow()
        {
            return TrackTest("objectives", d => d.TrackTestRun("objectives"));
        }

        private bool TrackTestRun(string test)
        {
            if (!Settings.ConstructionTrack)
            {
                TestRefusals.Tell(Logger, TrackCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(TrackCheck + test + "): the objectives are off in the options (Construction objectives on screen): FAIL.");
                return false;
            }
            _trackDirty = false;
            TrackWrite();
            int count = _trackLines.Count;
            bool ordered = true;
            for (int i = 1; i < count; i++)
            {
                ordered &= TrackRules.Compare(_trackLines[i - 1], _trackLines[i]) <= 0;
            }
            int shown = TrackRules.ShownOf(count, out int more);
            int lines = _trackFolded ? Math.Min(1, shown) + (count > 1 ? 1 : 0) : shown + (more > 0 ? 1 : 0);
            bool group = _track != null && (count == 0 ? !_track.Showing : _track.Showing && _track.Lines == lines);
            bool pass = ordered && group;
            string first = count > 0 ? ", the first '" + TrackRules.Words(_trackLines[0], _trackText, _trackDay, _trackMoney) + "'" : string.Empty;
            Logger?.Warn(TrackCheck + test + "): " + count + " matter(s) under way" + first
                         + "; " + (ordered ? "in order" : "OUT OF ORDER") + "; the game's objectives window " + (_track == null ? "has no group"
                             : _track.Showing ? "shows " + _track.Lines + " line(s) of the " + lines + " wanted" : "shows none") + (pass ? ": PASS." : ": FAIL."));
            if (pass)
            {
                Notifications.Show(NotificationType.Info, count > 0 ? "reallife_bld_test_track_shown" : "reallife_bld_test_track_none", Data("count", count.ToString()), 8f);
            }
            return pass;
        }
    }
}
