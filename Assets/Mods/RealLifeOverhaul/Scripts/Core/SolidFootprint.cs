using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Makes a thing on a pavement (a cart) solid to everyone on foot: walkers move on the navigation mesh and colliders
    /// stop nobody, so one carving <c>NavMeshObstacle</c> box on the thing cuts it out. A cut is wider than its box by
    /// the walker's radius, so the box is shallow enough to leave the places people stand at beside it; with no room it
    /// only steers walkers round. The owner calls <see cref="Solid"/> only while the player is near on foot, and
    /// <see cref="Verify"/> takes the cut back if a standing place was lost.
    /// </summary>
    public sealed class SolidFootprint
    {
        public const float Within = 60f;   // metres from the thing within which the player on foot makes it solid
        public const float Keep = 90f;     // metres it is kept solid to, so it does not flicker at the edge

        private const float Margin = 0.05f;        // metres between the cut's edge and a place somebody stands at
        private const float LeastHalfDepth = 0.05f;   // metres: a shallower box cuts nothing
        private static float _radius = -1f;

        private NavMeshObstacle _block;
        private Transform _thing;
        private float _halfDepth;
        private float _halfLength;
        private float _steerHalfDepth;
        private bool _canCarve;
        private bool _solid;
        private bool _refused;       // a cut lost a standing place: not cut again for this placing
        private float _verifyAt = -1f;

        public bool IsSolid => _solid;

        /// <summary>True from the moment it was made solid until <see cref="Verify"/> has looked.</summary>
        public bool VerifyDue => _verifyAt >= 0f;

        /// <summary>The walker's radius the box was sized for, for the log.</summary>
        public float Radius => WalkerRadius();

        /// <summary>The radius the navigation mesh was made for, read once from the player's walker; 0.5 m until there is one.</summary>
        public static float WalkerRadius()
        {
            if (_radius < 0f)
            {
                PlayerController player = Helpers.PlayerHelper.PlayerController;
                NavMeshAgent agent = player != null && player.Character != null ? player.Character.navmeshAgent : null;
                if (agent == null)
                {
                    return 0.5f;
                }
                _radius = Mathf.Clamp(NavMesh.GetSettingsByID(agent.agentTypeID).agentRadius, 0.15f, 0.6f);
            }
            return _radius;
        }

        public static void Reset()
        {
            _radius = -1f;
        }

        /// <summary>
        /// Fits the box to a thing just put down: its length along its X, and along its Z no deeper than leaves a place
        /// <paramref name="keepClear"/> metres from its middle. Nothing is cut until <see cref="Solid"/>.
        /// </summary>
        public void Fit(Transform thing, float halfLength, float halfDepthMost, float keepClear)
        {
            _thing = thing;
            _solid = false;
            _refused = false;
            _verifyAt = -1f;
            if (thing == null)
            {
                _block = null;
                return;
            }
            if (!thing.TryGetComponent(out _block))
            {
                _block = thing.gameObject.AddComponent<NavMeshObstacle>();
                _block.shape = NavMeshObstacleShape.Box;
            }
            _block.carving = false;
            _block.carveOnlyStationary = true;
            float radius = WalkerRadius();
            _halfLength = halfLength;
            _halfDepth = Mathf.Min(halfDepthMost, keepClear - radius - Margin);
            _canCarve = _halfDepth >= LeastHalfDepth;
            _steerHalfDepth = Mathf.Min(halfDepthMost, 0.28f);   // metres; narrower than a counter, so its two places can be reached
            _block.center = new Vector3(0f, 1f, 0f);
            _block.size = new Vector3(halfLength * 2f, 2f, _steerHalfDepth * 2f);
        }

        /// <summary>Cuts the mesh round the thing, or lets it close; true if the state changed.</summary>
        public bool Solid(bool on, float now)
        {
            on = on && _canCarve && !_refused && _block != null;
            if (on == _solid)
            {
                return false;
            }
            _solid = on;
            if (_block != null)
            {
                _block.size = new Vector3(_halfLength * 2f, 2f, (on ? _halfDepth : _steerHalfDepth) * 2f);
                _block.carving = on;
                MeshCuts.Note(on ? "a cart or stand made solid" : "a cart or stand no longer solid");
            }
            using var piece = SessionWatch.Piece(on ? "a cart made solid (the navigation mesh is cut round it)" : "a cart no longer solid (the navigation mesh closes)");
            _verifyAt = on ? now + 1.6f : -1f;   // seconds: the cut is made once the box has stood still for half a second
            return true;
        }

        /// <summary>
        /// Once, a moment after the cut: is the middle off the mesh, and are the places behind and in front still on it
        /// and joined? If not, the cut is taken back for this placing. False until the moment has come.
        /// </summary>
        public bool Verify(float now, Vector3 behind, Vector3 front, out bool cut, out bool placesKept, out bool wayRound)
        {
            cut = placesKept = wayRound = false;
            if (_verifyAt < 0f || now < _verifyAt || _thing == null)
            {
                return false;
            }
            _verifyAt = -1f;
            cut = !OnMesh(_thing.position);
            placesKept = OnMesh(behind) && OnMesh(front);
            wayRound = placesKept && CitySpots.Reachable(front, behind);
            if (!placesKept || !wayRound)
            {
                _refused = true;
                _solid = false;
                if (_block != null)
                {
                    _block.carving = false;
                    _block.size = new Vector3(_halfLength * 2f, 2f, _steerHalfDepth * 2f);
                }
                using var piece = SessionWatch.Piece("a cart's cut taken back (a standing place was lost)");
            }
            return true;
        }

        /// <summary>The thing goes back on its shelf: nothing is left cutting.</summary>
        public void Clear()
        {
            if (_block != null)
            {
                _block.carving = false;
            }
            _block = null;
            _thing = null;
            _solid = false;
            _verifyAt = -1f;
        }

        private static bool OnMesh(Vector3 place)
        {
            return NavMesh.SamplePosition(place, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) && CitySpots.Flat(hit.position - place) < 0.06f * 0.06f;
        }
    }
}
