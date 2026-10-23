using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Floors;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>
    /// The architects' practice: a place on a floor of a building the mod holds (the floors core), never a building of its
    /// own, furnished as a city office with the game's office designs first. Its desk is a few steps into the room from where
    /// the player comes in (the lift stands there), on the walkable floor once the furniture stands; the practice's panel is
    /// over it (TradesDirector). Nothing is saved: the floor is asked again at each load. No Update: the core's hooks only.
    /// </summary>
    public sealed class TradeOffice : FloorPlace
    {
        public const string PlaceId = "trades_architects";   // spare floors go by it, the same on every machine
        public const string FeatureId = "buildtrades";
        private const float DeskOut = 3.6f;    // metres into the room from where the player comes in
        private const float DeskFind = 2.5f;   // metres round that the walkable floor is looked for
        private const int DeskLooks = 6;       // seconds the desk's place is looked at again while the fit-out settles

        private static TradeOffice _on;        // the office the player is on now, or null
        private Vector3 _desk, _face;
        private bool _hasDesk;
        private int _looks;

        public TradeOffice() : base(PlaceId, FeatureId, "reallife_career_architects_name")
        {
            Fit = FitKind.CivicOffice;
            Designs.Add("GraphicDesigner");
            Designs.Add("LawFirm");
            Designs.Add("Headquarters");
            LogName = "the architects' practice";
        }

        /// <summary>True while the player is on the practice's floor.</summary>
        public static bool Here => _on != null && FloorsDirector.Visit != null && ReferenceEquals(FloorsDirector.Visit.Place, _on);

        /// <summary>The practice's desk and the way someone at it faces; false while the player is not on its floor.</summary>
        public static bool Desk(out Vector3 at, out Vector3 face)
        {
            at = default;
            face = Vector3.forward;
            if (!Here || !_on._hasDesk)
            {
                return false;
            }
            at = _on._desk;
            face = _on._face;
            return true;
        }

        public override void Arrive(FloorVisit visit)
        {
            _on = this;
            _looks = DeskLooks;
            Place(visit);
        }

        public override void Shown(FloorVisit visit)
        {
            _looks = DeskLooks;
            Place(visit);
        }

        public override void Tick(FloorVisit visit)
        {
            if (_looks > 0)
            {
                _looks--;
                Place(visit);   // the fit-out cuts its furniture out of the walkable floor over the first seconds
            }
        }

        public override void Leave(FloorVisit visit)
        {
            if (ReferenceEquals(_on, this))
            {
                _on = null;
            }
            _hasDesk = false;
        }

        // A few steps in from the lift, on the walkable floor: one sample of the navigation mesh, once a second for a few seconds.
        private void Place(FloorVisit visit)
        {
            Vector3 face = visit.Turn * Vector3.forward;
            face.y = 0f;
            _face = face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward;
            Vector3 wanted = visit.Spawn + _face * DeskOut;
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, DeskFind, NavMesh.AllAreas))
            {
                bool moved = !_hasDesk || (hit.position - _desk).sqrMagnitude > 0.04f;
                _desk = hit.position;
                _hasDesk = true;
                if (moved && Meta.Dev.Verbose)
                {
                    TradesDirector.Logger?.Info("Trades check (practice desk): on floor " + visit.Floor + " of " + visit.Address + ", "
                                                + (hit.position - visit.Spawn).magnitude.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                                + " m from the lift on the walkable floor: PASS.");
                }
            }
            else if (!_hasDesk)
            {
                _desk = visit.Spawn;
                _hasDesk = true;
                TradesDirector.Logger?.Info("Trades check (practice desk): no walkable floor within " + DeskFind + " m of " + DeskOut
                                            + " m in from the lift; the desk is where the player came in: FAIL.");
            }
        }
    }
}
