using System;
using BAModAPI;
using Helpers;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player on foot hit by a vehicle: shaken under 15 km/h, injured to 35 km/h, taken to hospital above that, and
    /// killed only by Permadeath's rules. They lie as a ragdoll for a few seconds with input blocked, then stand and the
    /// consequence follows. The ragdoll is optional: if the player is not exactly as expected only the consequence
    /// applies, and every way out runs the same restore.
    /// Cost: on foot outdoors, one small overlap query ten times a second; while down, a few flags per frame.
    /// </summary>
    public class PlayerKnockdown : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ragdoll", "PlayerKnockdown", typeof(PlayerKnockdown));

        private const float SenseInterval = 0.1f;       // seconds
        private const float SenseRadius = 0.55f;        // around the chest: the vehicle has to be touching
        private const float MinSpeed = 2f;              // m/s; slower than this is a bump, not a hit
        private const float InjurySpeed = 15f / 3.6f;   // m/s
        private const float HospitalSpeed = 35f / 3.6f; // m/s
        private const float DownAtLeast = 2.4f;         // seconds on the ground
        private const float DownAtMost = 5f;            // seconds
        private const float PathHalfWidth = 1.1f;       // metres either side of the vehicle's line of travel
        private const float Immunity = 8f;              // seconds after a hit before another counts

        // Outside the game's own blockers, so it can never be mistaken for, or cancel, one of them.
        private const NavigationBlocker Blocker = (NavigationBlocker)7301;

        private enum Harm { Shaken, Injured, Hospital }

        private static readonly Collider[] Hits = new Collider[8];

        public static IModLogger Logger;

        private int _trafficMask;
        private float _nextSense;
        private float _immuneUntil;
        private bool _failed;

        // While down.
        private bool _down;
        private Ragdoll _ragdoll;
        private int _serial;
        private PlayerController _player;
        private bool _blocked;
        private Harm _harm;
        private bool _nearMiss;         // Permadeath's first warning goes with this hit's hospital stay
        private float _upAt;
        private float _upLatest;
        private Vector3 _origin;        // where they stood when hit: known good ground

        private void Awake()
        {
            _trafficMask = LayerMask.GetMask("AiVehicles");
        }

        private void OnDestroy()
        {
            Restore(stand: false);
        }

        private static readonly int HotUpdate = HotSpots.Slot("Ragdoll: PlayerKnockdown.Update");

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
            float now = Time.time;
            if (_down)
            {
                try
                {
                    TickDown(now);
                }
                catch (Exception e)
                {
                    Fail(e);
                }
                return;
            }
            if (now < _nextSense)
            {
                return;
            }
            _nextSense = now + SenseInterval;
            if (_failed || now < _immuneUntil || !Settings.PlayerKnockdown || !Settings.Ragdolls || _trafficMask == 0
                || !ModState.Loaded || !TrafficTools.Ready || PlayerHelper.IsUsingVehicle)
            {
                return;
            }
            try
            {
                Sense(now);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The player comes first: back on their feet whatever went wrong, and no more of this until the next load.
        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Player knock-down: switched off for this session after the error above.");
            Restore(stand: false);
        }

        private void Sense(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            PlayerController player = gm != null ? gm.playerController : null;
            // Scooters and hand trucks count as vehicles; a player hidden or busy (getting into a taxi, an activity) is left alone.
            if (player == null || gm.selectedVehicle != null || !player.gameObject.activeInHierarchy || player.NavigationDisabled)
            {
                return;
            }
            Vector3 chest = player.transform.position + Vector3.up * 0.9f;
            int count = Physics.OverlapSphereNonAlloc(chest, SenseRadius, Hits, _trafficMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody vehicle = Hits[i].attachedRigidbody;
                Hits[i] = null;
                if (vehicle == null || vehicle.isKinematic)
                {
                    continue;   // parked, pinned at a scene
                }
                Vector3 velocity = vehicle.velocity;
                velocity.y = 0f;
                Vector3 towards = chest - vehicle.worldCenterOfMass;
                towards.y = 0f;
                float speed = velocity.magnitude;
                // Moving, at the player and with the player in its path: walking into a passing car, or a car passing
                // close along the kerb, has not run anyone over.
                if (speed < MinSpeed)
                {
                    continue;
                }
                Vector3 heading = velocity / speed;
                float ahead = Vector3.Dot(heading, towards);
                if (ahead <= 0f || (towards - heading * ahead).sqrMagnitude > PathHalfWidth * PathHalfWidth)
                {
                    continue;
                }
                for (int j = i + 1; j < count; j++)
                {
                    Hits[j] = null;
                }
                Hit(player, velocity / speed, speed, now);
                return;
            }
        }

        private void Hit(PlayerController player, Vector3 direction, float speed, float now)
        {
            // Not while the game has the player's attention (a dialog, a fade, the hospital).
            if (LifeEvents.Instance == null || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            _immuneUntil = now + Immunity;
            _harm = speed >= HospitalSpeed ? Harm.Hospital : speed >= InjurySpeed ? Harm.Injured : Harm.Shaken;
            News.Report(NewsKind.PlayerKnocked, PlayerHelper.GetPosition());
            Logger?.Info($"Player hit by traffic at {speed * 3.6f:F0} km/h: {_harm}.");

            Permadeath.Verdict fate = _harm == Harm.Hospital ? Permadeath.Mortality.Judge(Permadeath.DeathCause.Struck, speed) : Permadeath.Verdict.None;
            _nearMiss = fate == Permadeath.Verdict.NearMiss;

            // A gentler throw than a pedestrian gets: the player should land within a few steps.
            Vector3 launch = direction * Mathf.Clamp(speed * 0.7f, 2f, 7f) + Vector3.up * Mathf.Clamp(1.5f + speed * 0.12f, 1.5f, 3.2f);
            bool fell = TryFall(player, launch, now);
            if (fate == Permadeath.Verdict.Death && Permadeath.Mortality.Die(Permadeath.DeathCause.Struck))
            {
                return;   // the fall plays into the fade; the game's death then ends it like anything that overtakes it
            }
            if (!fell)
            {
                Consequence();
            }
        }

        // Only a player who is plainly walking about is thrown down; anyone doing anything else just takes the consequence.
        private bool TryFall(PlayerController player, Vector3 launch, float now)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            ThirdPersonCharacter character = player.Character;
            if (ragdolls == null || character == null || !player.isActiveAndEnabled || player.NavigationDisabled
                || PlayerHelper.IsHoldingItem || character.isSittingOn != null || character.isKinematic
                || character.characterRigidbody == null || character.characterRigidbody.isKinematic
                || character.navmeshAgent == null || !character.navmeshAgent.enabled || !character.navmeshAgent.isOnNavMesh
                || character.capsuleCollider == null || !character.capsuleCollider.enabled
                || BuildingManager.IsInsideBuilding || !ragdolls.CanBegin(character))
            {
                return false;
            }

            // Marked down before anything changes, so whatever fails from here on is undone by Restore.
            _down = true;
            _player = player;
            _ragdoll = null;
            _blocked = false;
            _upAt = now + DownAtLeast;
            _upLatest = now + DownAtMost;

            // Input goes first, while the character can still be animated: blocking it puts away what is in their hand.
            _origin = character.transform.position;
            player.ResetNavigation();
            _blocked = true;   // before the call: if it faults half way, Restore still takes the blocker off
            player.SetNavigationBlocker(Blocker);
            Ragdoll ragdoll = ragdolls.Begin(character, launch);
            if (ragdoll == null)
            {
                Restore(stand: false);
                return false;
            }
            _ragdoll = ragdoll;
            _serial = ragdoll.Serial;
            ThirdPersonCamera.Focus = ragdoll.Hips;
            return true;
        }

        private void TickDown(float now)
        {
            bool mine = _ragdoll != null && _ragdoll.Active && _ragdoll.Serial == _serial;
            // The game has moved on: put the player back where they stood and say no more.
            bool overtaken = !mine || _player == null || !_player.isActiveAndEnabled || LoadScene.isLoading
                             || EnergyHelper.goingToHospital || GameMembers.PlayerDead || BuildingManager.IsInsideBuilding
                             || PlayerHelper.IsUsingVehicle || !TrafficTools.Ready;
            if (overtaken)
            {
                Restore(stand: false);
                return;
            }
            if (now >= _upLatest || (now >= _upAt && _ragdoll.Resting))
            {
                Restore(stand: true);
                Consequence();
            }
        }

        // Puts the player back: ragdoll off, standing on the navigation mesh, in control, the camera on them. Each step is
        // taken whether or not the one before worked.
        private void Restore(bool stand)
        {
            if (!_down)
            {
                return;
            }
            _down = false;
            PlayerController player = _player;
            Ragdoll ragdoll = _ragdoll;
            Vector3 origin = _origin;
            bool mine = ragdoll != null && ragdoll.Active && ragdoll.Serial == _serial;
            _player = null;
            _ragdoll = null;

            try
            {
                ThirdPersonCamera.Focus = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            try
            {
                if (mine)
                {
                    // Where they landed, if that is somewhere the player can walk; otherwise where they were hit.
                    Vector3 place = default;
                    bool canStand = false;
                    if (stand && !ragdoll.Astray
                        && NavMesh.SamplePosition(ragdoll.Position, out NavMeshHit hit, 4f, PlayerController.navMeshQueryFilter))
                    {
                        place = hit.position;
                        canStand = true;
                    }
                    ragdoll.End(canStand, place, true);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            try
            {
                if (player != null)
                {
                    ThirdPersonCharacter character = player.Character;
                    if (character != null && player.gameObject.activeInHierarchy && !PlayerHelper.IsUsingVehicle)
                    {
                        // Whatever happened above, the player ends up on the navigation mesh with a collider.
                        NavMeshAgent agent = character.navmeshAgent;
                        if (agent != null)
                        {
                            if (!agent.enabled)
                            {
                                agent.enabled = true;
                            }
                            if (!agent.isOnNavMesh)
                            {
                                character.transform.position = origin;
                                if (!agent.Warp(origin) || !agent.isOnNavMesh)
                                {
                                    character.WarpSafely(origin);
                                }
                            }
                        }
                        if (character.capsuleCollider != null && !character.capsuleCollider.enabled)
                        {
                            character.capsuleCollider.enabled = true;
                        }
                    }
                    player.ResetNavigation();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            try
            {
                if (_blocked && player != null)
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _blocked = false;
        }

        // What it cost them: after they are back on their feet, or at once if they never fell.
        private void Consequence()
        {
            try
            {
                LifeEvents life = LifeEvents.Instance;
                bool nearMiss = _nearMiss;
                _nearMiss = false;
                if (life == null || !LifeEvents.IsSafeForEvent())
                {
                    return;
                }
                switch (_harm)
                {
                    case Harm.Shaken:
                        ModState.AddModifier(ModState.MoodDown);
                        Notifications.Show(NotificationType.Warning, "reallife_player_hit_knocked", null, 8f);
                        break;

                    case Harm.Injured:
                        life.Injure(24, 12f, "reallife_player_hit_injured");
                        break;

                    default:
                        life.Injure(48, 25f, "reallife_player_hit_severe");
                        life.SendToHospital();
                        if (nearMiss)
                        {
                            Permadeath.Mortality.NearMiss(Permadeath.DeathCause.Struck);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
