using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Makes a thing standing on a pavement (a cart) solid to everyone on foot, the way the bus stops are
    /// (Transport/BusStopDressing): the game moves the player and its pedestrians on the navigation mesh and
    /// colliders stop nobody, so what must not be walked through is cut out of the mesh by a carving
    /// <c>NavMeshObstacle</c>. One box, on the thing itself, so it goes where the thing goes and is gone with it.
    ///
    /// The box is the thing's own length, and as deep as leaves the places people must stand at beside it (a vendor
    /// behind a counter, a customer in front) on the mesh: a cut is wider than its box by the walker's radius, so
    /// the box is that much shallower than the gap to the nearest such place. Where there is no room for any box
    /// the thing stays what it was before: an obstacle walkers steer round, which cuts nothing.
    ///
    /// Cutting is work for the engine each time it changes, so <see cref="Solid"/> is called by whoever owns the
    /// thing only while the player is on foot near it, and the cut is made once (the box only cuts when it has stood
    /// still). <see cref="Verify"/> asks the mesh afterwards whether the middle was cut and the standing places were
    /// not, and takes the cut back if a place was lost, so nobody is ever shut in or out.
    /// </summary>
    public sealed class SolidFootprint
    {
        /// <summary>Metres from the thing within which the player on foot makes it solid, and the distance it is kept to (no flicker at the edge).</summary>
        public const float Within = 60f;
        public const float Keep = 90f;

        private const float Margin = 0.05f;        // metres between the cut's edge and a place somebody stands at
        private const float LeastHalfDepth = 0.05f;
        private static float _radius = -1f;

        private NavMeshObstacle _block;
        private Transform _thing;
        private float _halfDepth;
        private float _halfLength;
        private float _steerHalfDepth;
        private bool _canCarve;
        private bool _solid;
        private bool _refused;       // the mesh lost a standing place when it was cut: not cut again for this placing
        private float _verifyAt = -1f;

        /// <summary>True while the box cuts the navigation mesh.</summary>
        public bool IsSolid => _solid;

        /// <summary>True from the moment it was made solid until <see cref="Verify"/> has looked.</summary>
        public bool VerifyDue => _verifyAt >= 0f;

        /// <summary>The walker's radius the box was sized for, and the half depth it came to (for the log).</summary>
        public float Radius => WalkerRadius();
        public float HalfDepth => _canCarve ? _halfDepth : _steerHalfDepth;

        /// <summary>
        /// The radius the navigation mesh was made for (the player's kind of walker), read once from the player's
        /// own walker; half a metre until there is one.
        /// </summary>
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

        /// <summary>The city is loading: the radius is read again.</summary>
        public static void Reset()
        {
            _radius = -1f;
        }

        /// <summary>
        /// Fits the box to a thing that has just been put down: its length along its own X, and along its own Z
        /// no deeper than leaves a place <paramref name="keepClear"/> metres from its middle on the mesh. Not solid
        /// yet: walkers steer round it and nothing is cut until <see cref="Solid"/>. The component is the thing's own
        /// and is used again the next time the same object is put out.
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
            _steerHalfDepth = Mathf.Min(halfDepthMost, 0.28f);   // while it only steers walkers round: narrower than a counter, so its two places can be reached
            _block.center = new Vector3(0f, 1f, 0f);
            _block.size = new Vector3(halfLength * 2f, 2f, _steerHalfDepth * 2f);
        }

        /// <summary>
        /// Cuts the mesh round the thing, or lets it close again. One change of the engine's mesh each way, so it is
        /// asked for only when the answer differs. Returns true if the state changed.
        /// </summary>
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
            SessionWatch.Mark(on ? "a cart made solid (the navigation mesh is cut round it)" : "a cart no longer solid (the navigation mesh closes)");
            _verifyAt = on ? now + 1.6f : -1f;   // the cut is made once the box has stood still for half a second
            return true;
        }

        /// <summary>
        /// Once, a moment after the cut: is the thing's middle off the mesh, and are the two places beside it (in its
        /// own space: behind and in front) still on it and joined by a way round? If a place was lost the cut is taken
        /// back and not made again for this placing. False until the moment has come; then true, with the findings.
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
                SessionWatch.Mark("a cart's cut taken back (a standing place was lost)");
            }
            return true;
        }

        /// <summary>The thing is going back on its shelf: nothing is left cutting when the same object is next put out.</summary>
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
