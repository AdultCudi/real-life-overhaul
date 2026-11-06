using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul.Corners
{
    /// <summary>
    /// The check of the corner buildings standing (Options > Tests, construction: "Corner buildings: check every corner building standing
    /// now"): one "Corners check (...)" line each in Player.log, PASS or what failed: its model drawn and aligned to the corner, its
    /// building's own look hidden under it, its door at the entrance on the walking mesh, each sign saying what its registration says,
    /// and its whole-building line (Uptown's, written beside it). Asked from a test, never per frame.
    /// </summary>
    internal static partial class CornerLook
    {
        private static readonly List<CornerHandle> Checking = new List<CornerHandle>(8);

        /// <summary>Every corner building standing checked now; how many failed, -1 when none stands.</summary>
        public static int CheckAll()
        {
            All(Checking);
            if (Checking.Count == 0)
            {
                return -1;
            }
            int failed = 0;
            for (int i = 0; i < Checking.Count; i++)
            {
                failed += CheckOne(Checking[i]) ? 0 : 1;
            }
            Checking.Clear();
            return failed;
        }

        private static bool CheckOne(CornerHandle h)
        {
            string why = Judge(h, out string said);
            if (why == null)
            {
                why = Aligned(h) ?? Drawn(h) ?? SignsAgree(h);
            }
            bool whole = DistrictBuildings.CompleteNow(h.Lot);   // the whole-building line, written beside this one
            if (why == null && !whole)
            {
                why = "its whole-building line failed an item (the line above names it)";
            }
            Logger?.Info("Corners check (" + h.Id + " at " + h.Address + "): " + (why == null ? said + ", the whole-building line PASS: PASS" : why + ": FAIL") + ".");
            return why == null;
        }

        // The model where its pose puts it: its root at the street corner, turned by its quarter turn.
        private static string Aligned(CornerHandle h)
        {
            Transform t = h.Root.transform;
            float dx = t.position.x - h.Pose.X, dz = t.position.z - h.Pose.Z;
            float turn = Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y, h.Pose.Yaw));
            if (dx * dx + dz * dz > 0.01f || turn > 0.5f)
            {
                return "its model stands " + Mathf.Sqrt(dx * dx + dz * dz).ToString("F2", CultureInfo.InvariantCulture) + " m and " + turn.ToString("F1", CultureInfo.InvariantCulture)
                       + " degrees off the corner's pose";
            }
            return null;
        }

        // The model drawn now (unless the building's look is hidden on purpose: a demolition's wrap).
        private static string Drawn(CornerHandle h)
        {
            if (h.Hidden)
            {
                return null;
            }
            return h.Drawn == null || !h.Drawn.enabled || h.Drawn.forceRenderingOff || !h.Drawn.gameObject.activeInHierarchy ? "its model is not drawn" : null;
        }

        // Each sign as the registration says now: the business's name, "For lease", the number, or a blank board under the business's own sign.
        private static string SignsAgree(CornerHandle h)
        {
            BuildingRegistration reg = h.Made.Registration;
            bool business = reg != null && !string.IsNullOrEmpty(reg.BusinessName) && reg.businessTypeName != "ba:businesstype_empty";
            bool toLet = reg != null && reg.AvailableForRent;
            int number = reg != null ? reg.StreetNumber : h.Made.Spec.Number;
            string lease = CornersText.T(CornersText.Lease);
            for (int i = 0; i < h.Texts.Count; i++)
            {
                CornerText t = h.Texts[i];
                int shows = t.Covered ? CornerRules.ShowsNothing : CornerRules.Shows(t.Sign.Kind, business, toLet);
                string wanted = CornerRules.Words(t.Sign.Kind, shows, reg != null ? reg.BusinessName : null, lease, number);
                string shown = t.Text != null && t.Text.enabled ? t.Text.text : null;
                if ((wanted ?? string.Empty) != (shown ?? string.Empty))
                {
                    return "its " + t.Sign.Kind + " sign says '" + (shown ?? string.Empty) + "', its registration '" + (wanted ?? string.Empty) + "'";
                }
            }
            return null;
        }
    }
}
