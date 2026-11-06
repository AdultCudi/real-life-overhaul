using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The director's part for the decorative copies other features ask for (<see cref="BuildingCopies"/>): one change a
    /// frame (a look hidden or shown, a copy taken away, a copy stood), each only while its place is out of the camera's view
    /// with the city map shut; behind the loading screen, every change at once.
    /// </summary>
    public sealed partial class NewBuildingsDirector
    {
        // One frame of the copies: the city's designs measured a millisecond a frame until they are known, then one change
        // whose place is out of view. True while more waits.
        private bool Copies()
        {
            if (!BuildingCopies.Pending)
            {
                return false;
            }
            if (MadeGame.Loading)
            {
                return true;
            }
            if ((BuildingCopies.Waiting.Count > 0 || BuildingCopies.MeasureWanted) && !CopyGame.Ready)
            {
                if (!CopyGame.Measuring && !CopyGame.BeginMeasuring())
                {
                    return true;   // the city's list is not there yet
                }
                CopyGame.MeasureSlice();
                return true;
            }
            BuildingCopies.MeasureWanted = false;
            Change(true);
            return BuildingCopies.Pending;
        }

        // In the game's late call after its own start of the city, the loading screen still up: every copy asked for during
        // the load stands now, and every look asked to change changes, with nobody to see it.
        private void CopiesNow()
        {
            if (!BuildingCopies.Pending)
            {
                return;
            }
            if (BuildingCopies.Waiting.Count > 0 && !CopyGame.Ready && (CopyGame.Measuring || CopyGame.BeginMeasuring()))
            {
                while (CopyGame.Measuring)
                {
                    CopyGame.MeasureSlice();
                }
            }
            int changes = 0;
            while (Change(false))
            {
                changes++;
            }
            Logger?.Info("Copies: " + changes + " change(s) asked for during the load made behind the loading screen.");
        }

        // One change, the first whose place is out of view when that matters; false when none could be made now.
        private bool Change(bool outOfViewOnly)
        {
            List<CityBuildingController> show = BuildingCopies.ToShow, hide = BuildingCopies.ToHide;
            for (int i = 0; i < show.Count; i++)
            {
                if (!outOfViewOnly || show[i] == null || !CopyGame.InView(CopyGame.BoxOf(show[i])))
                {
                    CityBuildingController c = show[i];
                    show.RemoveAt(i);
                    CopyGame.ShowLook(c);
                    return true;
                }
            }
            for (int i = 0; i < hide.Count; i++)
            {
                if (!outOfViewOnly || hide[i] == null || !CopyGame.InView(CopyGame.BoxOf(hide[i])))
                {
                    CityBuildingController c = hide[i];
                    hide.RemoveAt(i);
                    CopyGame.HideLook(c);
                    return true;
                }
            }
            List<CopyHandle> leaving = BuildingCopies.Leaving;
            for (int i = 0; i < leaving.Count; i++)
            {
                CopyHandle h = leaving[i];
                if (!outOfViewOnly || h.Look == null || !CopyGame.InView(h.Box))
                {
                    bool hid = h.HidOwn;
                    BuildingCopies.Remove(h);   // out of the list of those leaving, and the look it hid shown again
                    Logger?.Info("Copies: " + h.Wears + " for " + (h.Owner ?? "a feature") + " taken away out of view" + (hid ? ", the building's own look shown again." : "."));
                    return true;
                }
            }
            if (!CopyGame.Ready)
            {
                return false;
            }
            List<CopyHandle> waiting = BuildingCopies.Waiting;
            for (int i = 0; i < waiting.Count; i++)
            {
                CopyHandle h = waiting[i];
                if (!Plan(h))
                {
                    waiting.RemoveAt(i);
                    return true;
                }
                if (!outOfViewOnly || !CopyGame.InView(h.Box))
                {
                    waiting.RemoveAt(i);
                    Stand(h, outOfViewOnly ? "out of view" : "behind the loading screen");
                    return true;
                }
            }
            return false;
        }

        // The design a copy wears and where it stands, worked out once the city's designs are known; false (the handle done,
        // with why) when it cannot stand.
        private bool Plan(CopyHandle h)
        {
            if (h.Planned >= 0)
            {
                return true;
            }
            IList<DesignEntry> designs = CopyGame.Catalogue;
            float slack = h.Over != null ? NewBuildingsRules.Gap * 2f : 0f;   // a building's new look may be a little bigger than the old
            int design;
            string why = null;
            if (h.Design != null)
            {
                design = h.Size != null ? CopyGame.Find(h.Design, h.Size) : CopyGame.Find(h.Design);
                why = design < 0 ? "no design " + h.Design + " can be copied (unknown, of a kind never copied, or of a DLC)"
                    : designs[design].Batched ? "the design " + h.Design + " is drawn only in static batches: a copy would draw at the original's place"
                    : !NewBuildingsRules.Copyable(designs[design]) ? "the design " + h.Design + " has a part of another place under it, or its door stands more than "
                                                                   + NewBuildingsRules.DoorBackMost.ToString("F0", Plain) + " m behind its front" : null;
            }
            else
            {
                design = NewBuildingsRules.PickFor(designs, h.Lot.Width + slack, h.Lot.Depth + slack, h.Other, h.Key);
                why = design < 0 ? "no design that can be copied fits " + h.Lot.Width.ToString("F0", Plain) + " by " + h.Lot.Depth.ToString("F0", Plain) + " m" : null;
            }
            Vector3 at = h.At;
            float yaw = h.Yaw;
            if (why == null && h.OverLot)
            {
                DesignEntry d = designs[design];
                if (!NewBuildingsRules.Fits(d, h.Lot.Width + slack, h.Lot.Depth + slack))
                {
                    why = "the design " + d.Name + " does not fit " + (h.Over != null ? "the building's own footprint" : "the plot");
                }
                else
                {
                    float x, z;
                    if (h.AlignDoor)
                    {
                        h.DoorMissed = NewBuildingsRules.AlignDoor(d, h.Lot, h.DoorAcross, NewBuildingsRules.Gap, out x, out z, out yaw);
                    }
                    else
                    {
                        NewBuildingsRules.Place(d, h.Lot, out x, out z, out yaw);
                    }
                    at = new Vector3(x, h.DoorGround - d.DoorY, z);
                }
            }
            if (why != null)
            {
                h.Done = true;
                h.Why = why;
                Logger?.Info("Copies: " + (h.Owner ?? "a feature") + " asked for " + (h.Design ?? "a design that fits") + ": not made: " + why + ".");
                return false;
            }
            DesignEntry chosen = designs[design];
            h.Planned = design;
            h.Wears = chosen.Name;
            h.PlannedAt = at;
            h.PlannedYaw = yaw;
            if (h.OverLot)
            {
                h.Box = CopyGame.BoxOf(h.Lot, h.DoorGround, Mathf.Max(chosen.Height, h.OwnHeight));
            }
            else
            {
                float width = 2f * Mathf.Max(Mathf.Abs(chosen.MinX), Mathf.Abs(chosen.MaxX)), depth = 2f * Mathf.Max(Mathf.Abs(chosen.MinZ), Mathf.Abs(chosen.MaxZ));
                h.Box = CopyGame.BoxOf(new MadeLot(at.x, at.z, yaw, width, depth), at.y, chosen.Height);
            }
            return true;
        }

        // A copy stands; over one of the game's buildings, that building's own look is hidden in the same frame and the copy
        // hands it its hovers and clicks.
        private void Stand(CopyHandle h, string when)
        {
            CopyLook look = CopyGame.MakeLook(h.Planned, h.PlannedAt, h.PlannedYaw, "RealLife Copy " + h.Wears);
            h.Done = true;
            if (look == null)
            {
                h.Why = "the game's building of the design " + h.Wears + " is gone";
                Logger?.Info("Copies: " + (h.Owner ?? "a feature") + " asked for " + h.Wears + ": not made: " + h.Why + ".");
                return;
            }
            h.Look = look;
            if (h.Over != null)
            {
                h.HidOwn = h.HideOwn && CopyGame.HideLook(h.Over);   // one hidden already is not this copy's to show again
                CopyGame.Lend(look, h.Over);
            }
            BuildingCopies.Standing.Add(h);
            MadeBuildings.Changed();   // the minimap draws the copies too
            if (Meta.Dev.Verbose)
            {
                bool linked = look.FadeLinked(out int parts);
                string where = h.Over != null ? "over " + h.Over.name + " (its door within " + h.DoorMissed.ToString("F1", Plain) + " m of the building's"
                                                + (h.HidOwn ? ", the building's own look hidden)" : ")")
                    : h.OverLot ? "on a plot of " + h.Lot.Width.ToString("F0", Plain) + " by " + h.Lot.Depth.ToString("F0", Plain) + " m" : "at a place";
                Logger?.Info("Construction check (look): " + h.Wears + " for " + (h.Owner ?? "a feature") + " " + where + " at (" + h.PlannedAt.x.ToString("F1", Plain) + ", "
                             + h.PlannedAt.z.ToString("F1", Plain) + "), " + when + ", " + parts + " part(s) the game's fade finds: " + (linked ? "PASS" : "FAIL, its fade is not linked"));
            }
        }
    }
}
