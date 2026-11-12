using System;
using RealLifeOverhaul.Nav;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The label on the route's line over the open city map ("14 min" over "1.2 km"): a piece of the kit the HUD's host keeps over the middle of the line as the map's camera moves.
    /// Navigation hands it over when the route or the map changes (never a frame's work here), and takes it back when the route is gone or the map has shut. Switched on with the map's
    /// filters; after a fault it stays away for the session and the route goes on without a label.
    /// </summary>
    public static class MapRouteTag
    {
        private static readonly Action<NavRouteLabel> Handler = Show;   // one delegate, so the once a second Install allocates nothing
        private static HudFollower _tag;
        private static UnityEngine.Vector3 _at;
        private static bool _broken;

        /// <summary>Navigation's label goes to this while the map's panels are on the kit.</summary>
        public static void On()
        {
            NavDirector.RouteLabelTo = _broken ? null : Handler;
        }

        /// <summary>The label taken away, and Navigation's hand-over with it.</summary>
        public static void Off()
        {
            NavDirector.RouteLabelTo = null;
            Clear();
        }

        /// <summary>The label taken off the map (a new city, a switch turned off).</summary>
        public static void Clear()
        {
            HudFollower tag = _tag;
            _tag = null;
            if (tag != null)
            {
                HudHost.Instance?.Unfollow(tag);
            }
        }

        private static void Show(NavRouteLabel label)
        {
            try
            {
                Clear();
                HudHost hud = HudHost.Instance;
                if (!label.On || hud == null)
                {
                    return;
                }
                _at = label.At;
                bool onFoot = label.OnFoot;
                string minutes = label.Minutes, distance = label.Distance;
                UiMapChrome.RouteTagSize(minutes, distance, out float w, out float h);
                _tag = hud.Follow(w, h, s => UiMapChrome.RouteTag(s, UiAt.Screen, 0f, 0f, w, h, onFoot, minutes, distance), () => _at, 0f, 0f, UiMapFilters.DockRight, 10000f);
            }
            catch (Exception e)
            {
                _broken = true;
                NavDirector.RouteLabelTo = null;
                UiDirector.Logger?.Error(e);
                UiDirector.Logger?.Warn("UI: the label on the city map's route could not be drawn after the error above; the route goes on without it.");
            }
        }
    }
}
