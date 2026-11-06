using System;
using System.Collections;
using UnityEngine;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// The lift between floors. To a floor on the structure on show: behind a quarter-second fade the floor's furniture goes,
    /// the player stands where they came in, the next floor's goes up and the screen is light again once it stands (six seconds
    /// at most). To a floor on another structure: through the game's own door again, the building read as that one's while the
    /// game loads it (the game leaves the building first, behind its own fade). On a press only.
    /// </summary>
    public sealed partial class FloorsDirector
    {
        /// <summary>The player, on a floor, to another floor of the same building. Null when under way, else why not, in words for the log.</summary>
        public static string Lift(int floor, string how)
        {
            return Instance != null ? Instance.LiftNow(floor, how) : "the floors are not running";
        }

        private string LiftNow(int floor, string how)
        {
            FloorVisit v = _visit;
            if (v == null || _broken)
            {
                return "the player is on no floor";
            }
            if (_lifting || FloorsGame.Entering || Loan.Pending || Loan.Borrowed)
            {
                return "the lift is already going";
            }
            FloorHost host = v.Host;
            FloorPlace to = host.PlaceOn(floor);
            if (to == null)
            {
                return "it has no floor " + floor;
            }
            if (floor == v.Floor)
            {
                return null;   // there already
            }
            if (!Shown(v, host, to))
            {
                host.LiftPanel.Hide("going to another floor");
                string size = Structure(host, to, out int version);
                Logger?.Info("Floors: the lift in " + host.Name + " from floor " + v.Floor + " to floor " + floor + " goes through the game's door: floor " + floor + " stands on "
                             + FloorsGame.Words(size, version) + ", this one on " + v.Structure + ".");
                return EnterNow(host, floor, how, true);
            }
            if (!isActiveAndEnabled)
            {
                return "the floors are not running";
            }
            _lifting = true;
            StartCoroutine(Guard.Routine(LiftSame(v, floor)));
            return null;
        }

        private IEnumerator LiftSame(FloorVisit v, int floor)
        {
            v.Host.LiftPanel.Hide("going to another floor");
            yield return FloorsGame.Fade(LiftFade);
            FloorPlace from = v.Place, to = from;
            int was = v.Floor;
            bool moved = false;
            try
            {
                if (ReferenceEquals(_visit, v))
                {
                    to = v.Host.PlaceOn(floor) ?? from;
                    if (!ReferenceEquals(to, from))
                    {
                        v.Stays = true;
                        from.Leave(v);
                        EndFurniture(true);
                    }
                    Loan.Moved(floor);
                    v.Floor = floor;
                    v.Part = v.Host.PartOf(floor);
                    v.Place = to;
                    v.How = "the lift";
                    FloorsGame.Warp(v.Spawn, v.Turn, to.FacesIn);
                    moved = true;
                    Logger?.Info("Floors: the player takes the lift in " + v.Host.Name + " from floor " + was + " to floor " + floor + ", " + to.Said + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            int frames = moved && !ReferenceEquals(to, from) ? Mathf.Max(0, from.ClearFrames) : 0;
            for (int i = 0; i < frames; i++)
            {
                yield return null;   // what was taken down is gone, and its cuts out of the walkable floor with it
            }
            if (moved && ReferenceEquals(_visit, v))
            {
                try
                {
                    if (ReferenceEquals(to, from))
                    {
                        to.Moved(v, was);
                    }
                    else
                    {
                        v.Built = ++_built;
                        to.Arrive(v);
                        Furnish(v);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                float until = Time.realtimeSinceStartup + LiftWaitMost;
                while (ReferenceEquals(_visit, v) && !ReadyNow(v) && Time.realtimeSinceStartup < until)
                {
                    yield return null;   // behind the dark screen the floor goes up at the hidden pace
                }
            }
            yield return FloorsGame.UnFade(LiftFade);
            _lifting = false;
            v.Host.LiftPage = 0;
            if (moved && ReferenceEquals(_visit, v))
            {
                try
                {
                    to.Shown(v);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }
    }
}
