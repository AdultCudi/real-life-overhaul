using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// "Uptown: the look census" (Options, Test tools; the owner, 27 Oct 2026, after a player's screenshots of plain walls, gaps and
    /// buildings turned off their street): every building and filler of both districts and the avenue's south row read as the load
    /// laid them (LookCensus: the ends nothing stands against and what shows there, the gaps, the yards, the doors against the
    /// streets), and in the game itself every renderer with no material or a shader that does not draw, a piece with nothing drawn,
    /// the window overlays laid against those wanted and the backdrop's boxes still standing along the avenue's south pavement.
    /// One "Uptown check (look census)" line with every fault in the whole band and those within a stone's throw of the player first.
    /// COST: nothing per frame; one pass over the pieces and their renderers when pressed.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private const float NearMetres = 120f;   // m round the player the test names the faults of first

        /// <summary>The look census: the line, a notice of how it stands, PASS when no fault was found.</summary>
        public static void TestLook()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                LookReport r = LookNow(out string faults, out int renderers);
                Vector3 at = PlayerHelper.PlayerController != null ? PlayerHelper.GetCityPosition() : Vector3.zero;
                var south = new List<GroundRect>(4);
                SouthRules.Hides(south);
                int boxes = GroundGame.StillShown(south);
                string why = LookWhy(r, faults, boxes);
                bool pass = why == null;
                var c = CultureInfo.InvariantCulture;
                int near = 0;
                for (int i = 0; i < r.Faults.Count; i++)
                {
                    near += Mathf.Abs(r.Faults[i].X - at.x) <= NearMetres && Mathf.Abs(r.Faults[i].Z - at.z) <= NearMetres ? 1 : 0;
                }
                string line = "Uptown check (look census): " + r.Describe() + "; " + renderers + " drawn parts looked at, " + SideWalls.Count + " window overlays laid (" + SideWalls.Describe() + "), "
                              + boxes + " of the backdrop's boxes still standing along the avenue's south pavement; " + near + " of the first " + r.Faults.Count + " faults within " + NearMetres.ToString("0", c)
                              + " m of the player at (" + at.x.ToString("F0", c) + ", " + at.z.ToString("F0", c) + ")" + (r.Faults.Count > 0 ? ": " + LookCensus.List(r) : string.Empty)
                              + (faults != null ? "; " + faults : string.Empty) + ": " + (pass ? "PASS" : "FAIL (" + why + ")");
                if (pass)
                {
                    Logger?.Info(line);
                }
                else
                {
                    Logger?.Warn(line);
                }
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.LookDone, new Dictionary<string, string>
                {
                    { "buildings", (r.Buildings + r.Fillers + r.Behind).ToString(c) }, { "blank", r.LeftBlank.ToString(c) }, { "gaps", r.Gaps.ToString(c) }, { "faults", (r.FaultCount + (faults != null ? 1 : 0) + boxes).ToString(c) }
                }, 16f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Why the census fails, or null: a blank wall to a road, a door off its street, a gap as wide as a space, a yard a road looks into, a bad
        // renderer, windows wanted and not laid, a backdrop's box left along the avenue.
        internal static string LookWhy(LookReport r, string faults, int boxes)
        {
            if (r.Buildings == 0)
            {
                return "no building stands";
            }
            if (r.LeftBlankRoad > 0)
            {
                return r.LeftBlankRoad + " open ends on a road still show a wall with no window";
            }
            if (r.Facing > 0)
            {
                return r.Facing + " doors face off their street";
            }
            if (r.WidestGap >= SpacesRules.SpaceWidth)
            {
                return "a gap of " + r.WidestGap.ToString("0.0", CultureInfo.InvariantCulture) + " m, a whole space or more";
            }
            if (faults != null)
            {
                return "a part of a building that does not draw";
            }
            if (SideWalls.Count * 10 < r.Dressed * 9)
            {
                return "windows laid on " + SideWalls.Count + " side walls, " + r.Dressed + " wanted";
            }
            if (boxes > 0)
            {
                return boxes + " of the backdrop's boxes stand along the avenue's south pavement";
            }
            return null;
        }
    }
}
