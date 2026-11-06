using System.Globalization;
using RealLifeOverhaul.Floors;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A block's show flat: a place on a floor of a building the mod holds (the floors core), never a building of its own, standing
    /// on the game's apartment structure of its design and dressed by the floors core with that design's pieces (one of the
    /// game's residential designs, staged or cut plain). The player walks in by the building's door as to any floor; where the
    /// buyers are shown round is a few steps in from the lift, on the walkable floor. Nothing of it is saved: the director asks
    /// it again at each load from the project's line. No Update: the core's hooks only.
    /// </summary>
    public sealed class ShowFlatPlace : FloorPlace
    {
        // A place of one player's save: its id sorts after every place every machine shares, so the floors those take never move for it.
        public const string PlaceId = "~construction_showflat";
        public const string FeatureId = "construction";
        private const float SpotOut = 2.8f;   // metres into the room from where the player comes in
        private const float SpotFind = 2.5f;  // metres round that the walkable floor is looked for
        private const int SpotLooks = 6;      // seconds the spot is looked at again while the pieces settle

        private readonly string _block;
        private Vector3 _spot, _face;
        private bool _hasSpot, _here;
        private int _looks;

        public ShowFlatPlace(ShowDesignView design, string block) : base(PlaceId, FeatureId, "reallife_bld_show_place")
        {
            DesignId = design.Id;
            _block = block ?? string.Empty;
            Structure = new FloorStructure(design.Size, design.Version);
            Layout = FloorLayout.Design(BuildCatalogue.UseTypes[(int)BuildUse.Homes], design.Design, design.Keeps);   // the firms' residential designs
            Check = "Show flats check (way in)";
            LayCheck = "Show flats check (dressing)";
            LogName = "the show flat";
            StructureWords = "the show flat's apartment structure";
        }

        /// <summary>The design it is dressed in (Designs' id).</summary>
        public readonly string DesignId;

        /// <summary>True while the player is on its floor.</summary>
        public bool Here => _here;

        public override string Name => ConstructionText.T("reallife_bld_show_place", "name", _block);

        /// <summary>Where the buyers are shown round and the way someone there faces; false while the player is not on its floor.</summary>
        public bool Spot(out Vector3 at, out Vector3 face)
        {
            at = _spot;
            face = _face;
            return _here && _hasSpot;
        }

        public override void Arrive(FloorVisit visit)
        {
            _here = true;
            _looks = SpotLooks;
            Place(visit);
            if (Meta.Dev.Verbose)
            {
                ConstructionDirector.Logger?.Info("Show flats check (walk in): the player is on floor " + visit.Floor + " of " + visit.Address + ", the show flat on structure "
                                                  + visit.Structure + " in " + DesignId + ", came in by " + visit.How + ": PASS.");
            }
        }

        public override void Laid(FloorVisit visit, FloorLaid laid)
        {
            if (Meta.Dev.Verbose)
            {
                ConstructionDirector.Logger?.Info("Show flats check (dressed): " + laid.Made + " piece(s) of " + DesignId + " stand, " + laid.Skipped
                                                  + " left out by the game (out of season or no prefab), " + laid.Frames + " frame(s), " + laid.Ms + " ms: "
                                                  + (laid.Made > 0 ? "PASS" : "FAIL") + ".");
            }
        }

        public override void Shown(FloorVisit visit)
        {
            _looks = SpotLooks;
            Place(visit);
        }

        public override void Tick(FloorVisit visit)
        {
            if (_looks > 0)
            {
                _looks--;
                Place(visit);   // the pieces cut themselves out of the walkable floor over the first seconds
            }
        }

        public override void Leave(FloorVisit visit)
        {
            _here = false;
            _hasSpot = false;
        }

        // A few steps in from the lift, on the walkable floor: one sample of the navigation mesh, once a second for a few seconds.
        private void Place(FloorVisit visit)
        {
            Vector3 face = visit.Turn * Vector3.forward;
            face.y = 0f;
            _face = face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward;
            Vector3 wanted = visit.Spawn + _face * SpotOut;
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, SpotFind, NavMesh.AllAreas))
            {
                bool moved = !_hasSpot || (hit.position - _spot).sqrMagnitude > 0.04f;
                _spot = hit.position;
                _hasSpot = true;
                if (moved && Meta.Dev.Verbose)
                {
                    ConstructionDirector.Logger?.Info("Show flats check (viewing spot): " + (hit.position - visit.Spawn).magnitude.ToString("0.0", CultureInfo.InvariantCulture)
                                                      + " m from the lift on the walkable floor: PASS.");
                }
            }
            else if (!_hasSpot)
            {
                _spot = visit.Spawn;
                _hasSpot = true;
                ConstructionDirector.Logger?.Info("Show flats check (viewing spot): no walkable floor within " + SpotFind + " m of " + SpotOut
                                                  + " m in from the lift; the buyers are met where the player came in: FAIL.");
            }
        }
    }
}
