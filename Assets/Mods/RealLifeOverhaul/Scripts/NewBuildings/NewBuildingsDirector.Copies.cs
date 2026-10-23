using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>The director's part for the decorative copies other features ask for (<see cref="BuildingCopies"/>): one made a frame.</summary>
    public sealed partial class NewBuildingsDirector
    {
        // One frame of the copies asked for: the city's designs measured a millisecond a frame until they are known, then
        // one copy a frame. True while more is to be done.
        private bool Copies()
        {
            List<CopyHandle> waiting = BuildingCopies.Waiting;
            if (waiting.Count == 0)
            {
                return false;
            }
            if (MadeGame.Loading)
            {
                return true;
            }
            if (!CopyGame.Ready)
            {
                if (!CopyGame.Measuring && !CopyGame.BeginMeasuring())
                {
                    return true;   // the city's list is not there yet
                }
                CopyGame.MeasureSlice();
                return true;
            }
            CopyHandle handle = waiting[0];
            waiting.RemoveAt(0);
            MakeCopy(handle);
            return waiting.Count > 0;
        }

        private void MakeCopy(CopyHandle handle)
        {
            IList<DesignEntry> designs = CopyGame.Catalogue;
            int design = handle.Size != null ? CopyGame.Find(handle.Design, handle.Size) : CopyGame.Find(handle.Design);
            string why = design < 0 ? "no design " + handle.Design + " can be copied (unknown, of a kind never copied, or of a DLC)"
                : designs[design].Batched ? "the design " + handle.Design + " is drawn only in static batches: a copy would draw at the original's place" : null;
            Vector3 at = handle.At;
            float yaw = handle.Yaw;
            if (why == null && handle.OverLot)
            {
                DesignEntry d = designs[design];
                if (!NewBuildingsRules.Fits(d, handle.Lot.Width + NewBuildingsRules.Gap * 2f, handle.Lot.Depth + NewBuildingsRules.Gap * 2f))
                {
                    why = "the design " + handle.Design + " does not fit the building's own footprint";
                }
                else
                {
                    NewBuildingsRules.Place(d, handle.Lot, out float x, out float z, out yaw);
                    at = new Vector3(x, handle.DoorGround - d.DoorY, z);
                }
            }
            CopyLook look = why == null ? CopyGame.MakeLook(design, at, yaw, "RealLife Copy " + handle.Design) : null;
            if (look == null)
            {
                handle.Done = true;
                handle.Why = why ?? "the game's building of the design " + handle.Design + " is gone";
                Logger?.Info("Copies: " + (handle.Owner ?? "a feature") + " asked for " + handle.Design + ": not made: " + handle.Why + ".");
                return;
            }
            handle.Look = look;
            handle.Done = true;
            BuildingCopies.Standing.Add(handle);
            MadeBuildings.Changed();   // the minimap draws the copies too
            if (Meta.Dev.Verbose)
            {
                bool linked = look.FadeLinked(out int parts);
                Logger?.Info("Construction check (look): " + handle.Design + " for " + (handle.Owner ?? "a feature") + " at (" + at.x.ToString("F1", Plain) + ", " + at.z.ToString("F1", Plain)
                             + "), " + parts + " part(s) the game's fade finds: " + (linked ? "PASS" : "FAIL, its fade is not linked"));
            }
        }
    }
}
