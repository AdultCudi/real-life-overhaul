using System;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's ragdolls: at most six with physics on their bones, reused, and up to eight more lying still with their
    /// physics taken off, so nobody vanishes in view to make room. Every ragdoll ends through here, whatever the cause.
    /// Cost: while anyone is down, a few flags per body each frame; with nobody down the component is off.
    /// </summary>
    public class RagdollSystem : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ragdoll", "RagdollSystem", typeof(RagdollSystem));

        public const int MaxActive = 6;   // falling bodies at once: 11 rigidbodies and 10 joints each, for a second or two
        public const int MaxStill = 8;    // bodies lying with nothing on their bones, each one drawn character
        public const int MaxBodies = MaxActive + MaxStill;

        private const float SeenWithin = 150f;   // metres: further off, or behind the camera, a body is not being looked at

        public static IModLogger Logger;
        public static RagdollSystem Instance { get; private set; }

        /// <summary>Takes one lying body away, true if one went: out of view if it can, and only so when the argument is true.</summary>
        public static Func<bool, bool> MakeRoom;

        private bool _stillLogged;

        private readonly Ragdoll[] _ragdolls = new Ragdoll[MaxBodies];
        private float _nextYield;   // when bodies at rest are next asked whether a vehicle is on top of them
        private bool _failed;       // off for the session after a fault
        private int _refusals;      // characters in a row that could not be made a ragdoll

        public bool Working => !_failed;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < MaxBodies; i++)
            {
                _ragdolls[i] = new Ragdoll();
            }
            enabled = false;
        }

        private void OnDestroy()
        {
            EndAll();
            MakeRoom = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>True if a ragdoll could be started on this character now. Changes nothing.</summary>
        public bool CanBegin(BaseHuman character)
        {
            if (_failed || !Settings.Ragdolls || !Ragdoll.CanTake(character) || Find(character) != null)
            {
                return false;
            }
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i].Active && _ragdolls[i].Resting && !_ragdolls[i].Still)
                {
                    return true;   // one at rest can give up its physics
                }
            }
            return Count(still: false) < MaxActive;
        }

        private int Count(bool still)
        {
            int count = 0;
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i].Active && _ragdolls[i].Still == still)
                {
                    count++;
                }
            }
            return count;
        }

        // The body that has lain at rest longest keeps its pose and gives up its physics. False if all are still falling.
        private bool SettleOldest()
        {
            Ragdoll oldest = null;
            for (int i = 0; i < MaxBodies; i++)
            {
                Ragdoll ragdoll = _ragdolls[i];
                if (ragdoll.Active && ragdoll.Resting && !ragdoll.Still && (oldest == null || ragdoll.StartedAt < oldest.StartedAt))
                {
                    oldest = ragdoll;
                }
            }
            if (oldest == null)
            {
                return false;
            }
            oldest.Settle();
            if (!_stillLogged)
            {
                _stillLogged = true;
                Logger?.Info($"Ragdoll: all {MaxActive} are in use; the body that has lain longest keeps its pose without physics (up to {MaxStill} such, the oldest taken away out of view). (Logged once.)");
            }
            return true;
        }

        /// <summary>True if the camera could be looking at this place: in front of it and not far off.</summary>
        public static bool InView(Vector3 position)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Transform eye = camera.transform;
            Vector3 offset = position - eye.position;
            float distance = offset.magnitude;
            // Within the picture's widest half angle and a little over: bodies are two metres long.
            return distance < SeenWithin && (distance < 6f || Vector3.Dot(offset, eye.forward) > distance * 0.45f);
        }

        /// <summary>Throws a character down; null, with it as it was, if there is no room, no known skeleton or no ground.</summary>
        public Ragdoll Begin(BaseHuman character, Vector3 velocity)
        {
            if (_failed || !Settings.Ragdolls || !Ragdoll.CanTake(character) || Find(character) != null)
            {
                return null;
            }
            try
            {
                if (Count(still: false) >= MaxActive && !SettleOldest())
                {
                    return null;   // all six are still falling
                }
                Ragdoll free = FindFree();
                if (free == null && MakeRoom != null && MakeRoom(false))
                {
                    free = FindFree();
                }
                if (free == null)
                {
                    return null;
                }
                int layer = RagdollLayers.Choose(character.transform.position, out bool blocksTraffic);
                if (layer < 0)
                {
                    return null;   // nothing to land on just here
                }
                if (!free.Begin(character, velocity, layer, blocksTraffic, Time.time))
                {
                    // Three full characters in a row with unexpected bones: give up rather than fail on everyone hit.
                    // A plain standing figure that will not do is its caller's to give up on.
                    if (character is ThirdPersonCharacter && ++_refusals >= 3)
                    {
                        _failed = true;
                        Logger?.Warn("Ragdoll: characters here cannot be made ragdolls (bones not as expected); switched off for this session.");
                    }
                    return null;
                }
                _refusals = 0;
                enabled = true;
                return free;
            }
            catch (Exception e)
            {
                Fail(e);
                return null;
            }
        }

        /// <summary>Ends a ragdoll; see <see cref="Ragdoll.End"/>. Does nothing if it has already ended.</summary>
        public void End(Ragdoll ragdoll, bool stand, Vector3 place, bool restoreMovement)
        {
            ragdoll?.End(stand, place, restoreMovement);
        }

        /// <summary>True while physics has this character's body: nothing else may move or animate it.</summary>
        public static bool Holds(BaseHuman character)
        {
            return Instance != null && Instance.Find(character) != null;
        }

        /// <summary>Puts a character back on its feet where it was hit, if it is a ragdoll, for whoever is about to reuse it.</summary>
        public static void Release(BaseHuman character)
        {
            Ragdoll ragdoll = Instance != null ? Instance.Find(character) : null;
            ragdoll?.End(false, default, true);
        }

        private Ragdoll Find(BaseHuman character)
        {
            if (character == null)
            {
                return null;   // a destroyed character would otherwise equal another destroyed one
            }
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i].Active && _ragdolls[i].Character == character)
                {
                    return _ragdolls[i];
                }
            }
            return null;
        }

        /// <summary>Where one of the bodies lies, for whoever must drive round it. False if that place is empty.</summary>
        public bool TryGetBody(int index, out Vector3 position)
        {
            Ragdoll ragdoll = _ragdolls[index];
            bool down = ragdoll != null && ragdoll.Active && !ragdoll.Astray;
            position = down ? ragdoll.Position : default;
            return down;
        }

        private Ragdoll FindFree()
        {
            for (int i = 0; i < MaxBodies; i++)
            {
                if (!_ragdolls[i].Active)
                {
                    return _ragdolls[i];
                }
            }
            return null;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Ragdoll: RagdollSystem.Update");

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            try
            {
                bool any = false;
                float now = Time.time;
                bool yield = now >= _nextYield;
                if (yield)
                {
                    _nextYield = now + 0.5f;
                }
                for (int i = 0; i < MaxBodies; i++)
                {
                    Ragdoll ragdoll = _ragdolls[i];
                    if (!ragdoll.Active)
                    {
                        continue;
                    }
                    if (ragdoll.Lost)
                    {
                        // Destroyed, or back in the game's hands: strip it and touch nothing else.
                        ragdoll.End(false, default, false);
                        continue;
                    }
                    any = true;
                    ragdoll.KeepLimp();
                    ragdoll.Tick(now);
                    if (yield)
                    {
                        ragdoll.YieldToVehicles();
                    }
                }
                if (!any)
                {
                    enabled = false;
                }
                else if (yield && MakeRoom != null && Count(still: true) > MaxStill)
                {
                    MakeRoom(true);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // Ragdolls are an extra: after a fault everybody gets up and there are no more until the next load.
        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Ragdoll: switched off for this session after the error above.");
            EndAll();
        }

        private void EndAll()
        {
            for (int i = 0; i < MaxBodies; i++)
            {
                if (_ragdolls[i] != null)
                {
                    _ragdolls[i].End(false, default, true);
                }
            }
            enabled = false;
        }
    }

    /// <summary>
    /// Which physics layer a ragdoll's parts go on: one that collides with the ground, and the game's Human layer if it
    /// can, so traffic brakes for a body as for anyone on foot. The project's collision matrix is asked once per kind
    /// of ground and the answer kept.
    /// </summary>
    internal static class RagdollLayers
    {
        private const int Unknown = 0;
        private const int None = -1;

        private static readonly int[] Chosen = new int[32];   // per ground layer: 0 not asked yet, -1 none, else layer + 1
        private static readonly int[] Surfaces = new int[3];  // the game's road and ground layers, -1 for any it lacks
        private static int _human = -2;
        private static int _surfaceMask;
        private static int _anythingMask;
        private static bool _warned;

        /// <summary>The layer for a ragdoll here, or -1 if nothing would stop it falling; blocksTraffic if traffic brakes for it.</summary>
        public static int Choose(Vector3 feet, out bool blocksTraffic)
        {
            if (_human == -2)
            {
                _human = LayerMask.NameToLayer("Human");
                Surfaces[0] = LayerMask.NameToLayer("Roads");
                Surfaces[1] = LayerMask.NameToLayer("Ground");
                Surfaces[2] = LayerMask.NameToLayer("GroundUnplacable");
                _surfaceMask = LayerMask.GetMask("Roads", "Ground", "GroundUnplacable");
                // Failing those: anything solid that is not a vehicle, a person, interface or one of the game's invisible helpers.
                _anythingMask = ~(TrafficTools.VehicleMask | LayerMask.GetMask("Human", "Player", "UI", "Ignore Raycast", "Trigger",
                    "Guiders", "WallPlacementLayer", "AutoParkSpots", "ParkingArea"));
            }
            blocksTraffic = false;
            Vector3 from = feet + Vector3.up * 0.6f;
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 6f, _surfaceMask, QueryTriggerInteraction.Ignore)
                && !Physics.Raycast(from, Vector3.down, out hit, 6f, _anythingMask, QueryTriggerInteraction.Ignore))
            {
                return None;
            }
            int ground = hit.collider.gameObject.layer;
            if (Chosen[ground] == Unknown)
            {
                Chosen[ground] = Pick(ground);
                RagdollSystem.Logger?.Info(Chosen[ground] == None
                    ? $"Ragdoll: no layer collides with the ground layer '{LayerMask.LayerToName(ground)}'; no ragdolls there."
                    : $"Ragdoll: parts go on layer '{LayerMask.LayerToName(Chosen[ground] - 1)}' over ground layer '{LayerMask.LayerToName(ground)}'.");
            }
            if (Chosen[ground] == None)
            {
                return None;
            }
            int layer = Chosen[ground] - 1;
            blocksTraffic = layer == _human;
            if (!blocksTraffic && !_warned)
            {
                _warned = true;
                RagdollSystem.Logger?.Warn("Ragdoll: the Human layer does not collide with the ground here, so traffic will not stop for a body. (Logged once.)");
            }
            return layer;
        }

        // Human, then Default, then any other layer. First one that lands on road and pavement alike (a body thrown
        // off the kerb crosses from one to the other); failing that, one that lands on what is underfoot.
        private static int Pick(int ground)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool everywhere = pass == 0;
                if (_human >= 0 && Lands(_human, ground, everywhere))
                {
                    return _human + 1;
                }
                if (Lands(0, ground, everywhere))
                {
                    return 1;   // Default
                }
                for (int layer = 1; layer < 32; layer++)
                {
                    if (layer != ground && !string.IsNullOrEmpty(LayerMask.LayerToName(layer)) && Lands(layer, ground, everywhere))
                    {
                        return layer + 1;
                    }
                }
            }
            return None;
        }

        private static bool Lands(int layer, int ground, bool everywhere)
        {
            if (Physics.GetIgnoreLayerCollision(layer, ground))
            {
                return false;
            }
            for (int i = 0; everywhere && i < Surfaces.Length; i++)
            {
                if (Surfaces[i] >= 0 && Physics.GetIgnoreLayerCollision(layer, Surfaces[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
