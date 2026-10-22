using System;
using System.Reflection;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One character as a ragdoll, from the hit to being put back. Begin turns its animator, pathfinding and capsule off
    /// and gives the bones RagdollRig's bodies; a body at rest is frozen and costs nothing; End takes everything off and
    /// puts back all Begin changed. Owned and reused by <see cref="RagdollSystem"/>.
    /// </summary>
    public sealed class Ragdoll
    {
        private const float MinFlight = 0.7f;       // seconds before it may count as lying still
        private const float RestHold = 0.5f;        // seconds it must stay still
        private const float MaxFlight = 8f;         // seconds: frozen wherever it is after this long
        private const float RestSpeedSqr = 0.05f;   // (m/s) squared
        private const float RestSpinSqr = 0.6f;     // (rad/s) squared
        private const float SinkLimit = 8f;         // metres below where it started: it has left the world
        private const float StrayLimit = 80f;       // metres from where it started

        // The game fades people out near vehicles; this is its own switch for that, used to show a body again.
        private static MethodInfo _showNearVehicle;
        private static bool _showSearched;
        private static readonly object[] ShowArguments = { false };

        private readonly RagdollPart[] _parts = new RagdollPart[RagdollRig.PartCount];
        private readonly Transform[] _ends = new Transform[RagdollRig.EndCount];

        private BaseHuman _character;   // a full street character, or a plainer standing figure with no agent or capsule
        private Animator _animator;
        private NavMeshAgent _agent;
        private CapsuleCollider _capsule;
        private Rigidbody _rootBody;
        private HumanHider _hider;
        private bool _characterWasOn;
        private bool _agentWasOn;
        private bool _capsuleWasOn;
        private bool _rootWasKinematic;
        private bool _hiderWasOn;
        private bool _blocksTraffic;
        private bool _solid;            // at rest with its colliders left on, for traffic to brake for
        private bool _still;
        private float _stillSince;
        private Vector3 _lastPosition;

        public bool Active { get; private set; }

        /// <summary>True once the body has come to rest and been frozen.</summary>
        public bool Resting { get; private set; }

        /// <summary>True for a body at rest that has given up its physics and only keeps its pose; still Active and Resting.</summary>
        public bool Still { get; private set; }

        /// <summary>True if the body fell out of the world or flew absurdly far: where it lies means nothing.</summary>
        public bool Astray { get; private set; }

        /// <summary>Goes up on every Begin, so a holder can tell this is no longer the ragdoll it started.</summary>
        public int Serial { get; private set; }

        public float StartedAt { get; private set; }

        /// <summary>Where the character stood when it was hit. Its root stays there until End.</summary>
        public Vector3 Origin { get; private set; }

        public BaseHuman Character => _character;

        /// <summary>The hip bone: what a camera should watch.</summary>
        public Transform Hips => Active ? _parts[RagdollRig.Hips].Bone : null;

        /// <summary>The head bone: with the hips, which way the body lies.</summary>
        public Transform Head => Active ? _parts[RagdollRig.Head].Bone : null;

        /// <summary>Where the body is now, or was when last seen.</summary>
        public Vector3 Position
        {
            get
            {
                Transform hips = _parts[RagdollRig.Hips].Bone;
                if (Active && !Astray && hips != null)
                {
                    _lastPosition = hips.position;
                }
                return _lastPosition;
            }
        }

        /// <summary>True if the game destroyed, switched off or reused the character while it was ours: it must be let go.</summary>
        public bool Lost =>
            Active && (_character == null || !_character.gameObject.activeInHierarchy
                       || (_capsule != null && _capsule.enabled) || (_agent != null && _agent.enabled));

        /// <summary>The checks Begin makes before it changes anything, for callers that must prepare the character first.</summary>
        public static bool CanTake(BaseHuman character)
        {
            if (character == null || !character.gameObject.activeInHierarchy || character.animator == null || !character.animator.enabled
                || !character.animator.isHuman || character.animator.avatar == null)
            {
                return false;
            }
            // A full character must have what it moves with; a plain standing figure has neither, and needs neither.
            return !(character is ThirdPersonCharacter full) || (full.navmeshAgent != null && full.capsuleCollider != null);
        }

        /// <summary>False, with the character as it was, if this skeleton cannot be made a ragdoll. Velocity in m/s; layer collides with the ground.</summary>
        /// <param name="blocksTraffic">Traffic brakes for that layer, so the frozen body keeps its colliders.</param>
        internal bool Begin(BaseHuman character, Vector3 velocity, int layer, bool blocksTraffic, float now)
        {
            if (Active || !CanTake(character))
            {
                return false;
            }
            _animator = character.animator;
            if (!RagdollRig.Resolve(_animator, _parts, _ends))
            {
                Forget();
                return false;
            }

            var full = character as ThirdPersonCharacter;
            _character = character;
            _agent = full != null ? full.navmeshAgent : null;
            _capsule = full != null ? full.capsuleCollider : null;
            _rootBody = full != null && full.characterRigidbody != null ? full.characterRigidbody : character.GetComponent<Rigidbody>();
            _hider = character.GetComponent<HumanHider>();
            _characterWasOn = character.enabled;
            _agentWasOn = _agent != null && _agent.enabled;
            _capsuleWasOn = _capsule != null && _capsule.enabled;
            _rootWasKinematic = _rootBody != null && _rootBody.isKinematic;
            _hiderWasOn = _hider != null && _hider.enabled;
            _blocksTraffic = blocksTraffic;
            for (int i = 0; i < RagdollRig.PartCount; i++)
            {
                Transform bone = _parts[i].Bone;
                _parts[i].LocalPosition = bone.localPosition;
                _parts[i].LocalRotation = bone.localRotation;
                _parts[i].Layer = bone.gameObject.layer;
            }
            Origin = character.transform.position;
            _lastPosition = Origin;
            StartedAt = now;
            _still = false;
            Resting = false;
            Still = false;
            Astray = false;
            Serial++;
            Active = true;   // from here on End undoes whatever has been done

            try
            {
                // Nothing may move the root while the bones are bodies: they hang from it.
                if (_agent != null && _agent.enabled)
                {
                    if (_agent.isOnNavMesh)
                    {
                        _agent.ResetPath();
                    }
                    _agent.enabled = false;
                }
                if (full != null)
                {
                    full.LookTarget = Vector3.zero;
                }
                if (_capsule != null && _capsule.enabled)
                {
                    _capsule.enabled = false;
                    TellTraffic(_capsule);
                }
                if (_rootBody != null)
                {
                    _rootBody.isKinematic = true;
                }
                if (_hider != null)
                {
                    ShowNearVehicles(_hider);
                    _hider.enabled = false;
                }
                // The character's own script would tell the stopped animator to stand each frame, and could turn the root.
                character.enabled = false;
                _animator.enabled = false;

                if (!RagdollRig.Build(_parts, _ends, layer))
                {
                    End(false, default, true);
                    return false;
                }
                Vector3 tumble = Vector3.Cross(velocity, Vector3.up) * 0.35f;   // legs taken from under them: the head goes back
                for (int i = 0; i < RagdollRig.PartCount; i++)
                {
                    _parts[i].Body.velocity = velocity;
                }
                _parts[RagdollRig.Torso].Body.angularVelocity = Vector3.ClampMagnitude(tumble, 5f);
                return true;
            }
            catch (Exception)
            {
                End(false, default, true);
                throw;
            }
        }

        /// <summary>The rest check, called each frame until the body is frozen: a few values read from two bodies.</summary>
        internal void Tick(float now)
        {
            if (!Active || Resting)
            {
                return;
            }
            Rigidbody hips = _parts[RagdollRig.Hips].Body;
            Rigidbody torso = _parts[RagdollRig.Torso].Body;
            if (hips == null || torso == null)
            {
                Freeze();
                return;
            }
            Vector3 offset = hips.position - Origin;
            if (offset.y < -SinkLimit || offset.sqrMagnitude > StrayLimit * StrayLimit)
            {
                Astray = true;
                Freeze();
                return;
            }
            float age = now - StartedAt;
            if (age < MinFlight)
            {
                return;
            }
            bool still = hips.velocity.sqrMagnitude < RestSpeedSqr && torso.velocity.sqrMagnitude < RestSpeedSqr
                         && hips.angularVelocity.sqrMagnitude < RestSpinSqr;
            if (!still)
            {
                _still = false;
            }
            else if (!_still)
            {
                _still = true;
                _stillSince = now;
            }
            if (age >= MaxFlight || (_still && now - _stillSince >= RestHold))
            {
                Freeze();
            }
        }

        /// <summary>Something switched the animator back on under us; it would pull the bones out of the bodies' hands.</summary>
        internal void KeepLimp()
        {
            if (Active && _animator != null && _animator.enabled)
            {
                _animator.enabled = false;
            }
        }

        // Holds the body as it lies, no longer simulated. It stays solid only where traffic brakes for it and no vehicle
        // is on top of it already, so nothing solid and immovable is left in the road.
        private void Freeze()
        {
            Resting = true;
            for (int i = 0; i < RagdollRig.PartCount; i++)
            {
                Rigidbody body = _parts[i].Body;
                if (body != null)
                {
                    body.isKinematic = true;
                }
            }
            _solid = _blocksTraffic && !UnderVehicle();
            if (!_solid)
            {
                GoSoft();
            }
        }

        /// <summary>
        /// A solid body at rest gives way when a traffic vehicle has ended up over it after all: a frozen body cannot move,
        /// so the vehicle would hop on it for good. One overlap test, twice a second while someone is down.
        /// </summary>
        internal void YieldToVehicles()
        {
            if (Active && Resting && _solid && UnderVehicle())
            {
                _solid = false;
                GoSoft();
                RagdollSystem.Logger?.Info("Ragdoll: a traffic vehicle is standing over a body at rest; the body no longer blocks it.");
            }
        }

        /// <summary>A body at rest gives up everything added to its bones and stays as it lies until End.</summary>
        internal void Settle()
        {
            if (!Active || !Resting || Still)
            {
                return;
            }
            Still = true;
            _solid = false;
            GoSoft();
            RagdollRig.Strip(_parts);
        }

        // True if a vehicle of the traffic system is touching the line of the body, hips to chest.
        private bool UnderVehicle()
        {
            Rigidbody hips = _parts[RagdollRig.Hips].Body;
            Rigidbody torso = _parts[RagdollRig.Torso].Body;
            int traffic = TrafficTools.TrafficMask;
            if (hips == null || torso == null || traffic == 0)
            {
                return false;
            }
            Vector3 lift = Vector3.up * 0.2f;
            return Physics.CheckCapsule(hips.position + lift, torso.position + lift, 0.4f, traffic, QueryTriggerInteraction.Ignore);
        }

        // The colliders go, and any car that was waiting for one of them is told.
        private void GoSoft()
        {
            for (int i = 0; i < RagdollRig.PartCount; i++)
            {
                Collider collider = _parts[i].Collider;
                if (collider != null && collider.enabled)
                {
                    collider.enabled = false;
                    TellTraffic(collider);
                }
            }
        }

        /// <summary>
        /// Ends the ragdoll: every added component comes off and all Begin changed goes back, in any state, and no step can
        /// stop the ones after it. <paramref name="stand"/> moves the character to <paramref name="place"/>, else it is
        /// back where it was hit; <paramref name="restoreMovement"/> is false when the caller places it or the game has it.
        /// </summary>
        internal void End(bool stand, Vector3 place, bool restoreMovement)
        {
            if (!Active)
            {
                return;
            }
            bool alive = _character != null;
            if (alive)
            {
                _lastPosition = stand ? place : Position;   // for whoever asks afterwards
            }
            Active = false;
            Resting = false;
            Still = false;

            try
            {
                // Before they are destroyed: traffic that stopped for the body is told it has gone.
                for (int i = 0; alive && i < RagdollRig.PartCount; i++)
                {
                    TellTraffic(_parts[i].Collider);
                }
            }
            catch (Exception e)
            {
                RagdollSystem.Logger?.Error(e);
            }
            try
            {
                RagdollRig.Strip(_parts);
            }
            catch (Exception e)
            {
                RagdollSystem.Logger?.Error(e);
            }
            try
            {
                if (alive)
                {
                    if (stand)
                    {
                        _character.transform.position = place;
                    }
                    for (int i = 0; i < RagdollRig.PartCount; i++)
                    {
                        Transform bone = _parts[i].Bone;
                        if (bone != null)
                        {
                            bone.gameObject.layer = _parts[i].Layer;
                            bone.localPosition = _parts[i].LocalPosition;
                            bone.localRotation = _parts[i].LocalRotation;
                        }
                    }
                    if (_animator != null)
                    {
                        _animator.enabled = true;
                    }
                    _character.enabled = _characterWasOn;
                    if (_rootBody != null)
                    {
                        _rootBody.isKinematic = _rootWasKinematic;
                    }
                    if (_hider != null)
                    {
                        _hider.enabled = _hiderWasOn;
                    }
                    if (restoreMovement)
                    {
                        if (_capsule != null)
                        {
                            _capsule.enabled = _capsuleWasOn;
                        }
                        if (_agent != null && _agentWasOn)
                        {
                            _agent.enabled = true;
                            if (stand)
                            {
                                _agent.Warp(place);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                RagdollSystem.Logger?.Error(e);
            }
            Forget();
        }

        private void Forget()
        {
            Array.Clear(_parts, 0, _parts.Length);
            Array.Clear(_ends, 0, _ends.Length);
            _character = null;
            _animator = null;
            _agent = null;
            _capsule = null;
            _rootBody = null;
            _hider = null;
        }

        // A traffic car that has stopped for a collider keeps waiting unless it is told the collider went away.
        private static void TellTraffic(Collider collider)
        {
            if (collider != null && TrafficManager.HasInstance && TrafficManager.IsInitialized)
            {
                Manager.TriggerColliderRemovedEvent(collider);
            }
        }

        // The car that hit them is right there, so the game has probably just faded this person out.
        private static void ShowNearVehicles(HumanHider hider)
        {
            if (!_showSearched)
            {
                _showSearched = true;
                _showNearVehicle = GameContract.NearItemToHide.Method;
                if (_showNearVehicle == null)
                {
                    RagdollSystem.Logger?.Warn("Ragdoll: the game's fade-near-vehicles switch was not found; a body may stay faded while a car is beside it.");
                }
            }
            if (_showNearVehicle == null || !hider.enabled)
            {
                return;
            }
            try
            {
                _showNearVehicle.Invoke(hider, ShowArguments);
            }
            catch (Exception e)
            {
                // Only how the body looks beside a car: give it up rather than lose the ragdoll over it.
                RagdollSystem.Logger?.Error(e);
                _showNearVehicle = null;
            }
        }
    }
}
