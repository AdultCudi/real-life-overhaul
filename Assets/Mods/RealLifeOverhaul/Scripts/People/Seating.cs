using System;
using System.Reflection;
using BAModAPI;
using Controllers;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Sitting on what the game made (furniture, street benches) by the game's own SitOnChair and Reset; this adds where the
    /// character stood and what "free" means. The player sits through <see cref="PlayerSeat"/>.
    /// </summary>
    public static class Seating
    {
        private const float StandOff = 2.4f;   // metres from where somebody stands to a seat they may take

        private static readonly Collider[] Near = new Collider[32];
        private static FieldInfo _benchSitter;   // OutsideBenchController._npc: the pedestrian the game has put on a bench
        private static bool _benchSitterLooked;

        public static bool IsSeat(ItemController item)
        {
            return item is SeatController;
        }

        /// <summary>A sitting position of a piece of furniture nobody uses, or null: a seat, in the room, every place empty.</summary>
        public static Transform Free(ItemController item)
        {
            var seat = item as SeatController;
            if (seat == null || !seat.isActiveAndEnabled || seat.Occupied)
            {
                return null;
            }
            return seat.SittingPosition;
        }

        /// <summary>True if somebody standing here is near enough to a seat to sit on it without being seen to jump.</summary>
        public static bool InReach(Vector3 from, Transform seat)
        {
            if (seat == null)
            {
                return false;
            }
            Vector3 offset = seat.position - from;
            float up = offset.y;
            offset.y = 0f;
            return offset.sqrMagnitude <= StandOff * StandOff && Mathf.Abs(up) < 1.5f;
        }

        /// <summary>The nearest street bench within <paramref name="within"/> metres, or null. One physics query: call it on a slow timer.</summary>
        public static OutsideBenchController Bench(Vector3 near, float within)
        {
            int found = Physics.OverlapSphereNonAlloc(near + Vector3.up * 0.5f, within, Near, ~0, QueryTriggerInteraction.Collide);
            OutsideBenchController best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < found; i++)
            {
                Collider hit = Near[i];
                Near[i] = null;
                if (hit == null)
                {
                    continue;
                }
                OutsideBenchController bench = hit.GetComponentInParent<OutsideBenchController>();
                if (bench == null || !bench.isActiveAndEnabled || bench.sittingPosition == null)
                {
                    continue;
                }
                float sqr = (bench.sittingPosition.position - near).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = bench;
                }
            }
            return best;
        }

        /// <summary>
        /// True if the bench's second place, where the game seats a pedestrian, is there and empty. Read from the game's
        /// private field; taken if that cannot be read.
        /// </summary>
        public static bool SecondPlaceFree(OutsideBenchController bench)
        {
            if (bench == null || bench.aiSittingPosition == null || bench.aiSittingPosition == bench.sittingPosition)
            {
                return false;
            }
            if (!_benchSitterLooked)
            {
                _benchSitterLooked = true;
                _benchSitter = GameContract.BenchSitter.Field;
            }
            if (_benchSitter == null)
            {
                return false;
            }
            try
            {
                var sitter = _benchSitter.GetValue(bench) as UnityEngine.Object;
                return sitter == null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>One check line per step, with Developer diagnostics on.</summary>
        public static void Check(IModLogger logger, string what, bool pass)
        {
            if (Meta.Dev.Verbose)
            {
                logger?.Info("People check (seat): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
        }
    }

    /// <summary>
    /// The player on one of the game's seats outside its own activities: seated and stood up by the game's two calls, held
    /// by its navigation blocker meanwhile, and stood up where they stood. <see cref="Stand"/> is safe at any time.
    /// </summary>
    public sealed class PlayerSeat
    {
        private const NavigationBlocker Blocker = NavigationBlocker.RestActivity;   // what the game's own benches set

        private PlayerController _player;
        private Vector3 _stoodAt;
        private Quaternion _stoodFacing;

        public bool Seated => _player != null;

        /// <summary>Sits the player down; false, with nothing changed, if they cannot be seated now.</summary>
        public bool Sit(PlayerController player, Transform seat)
        {
            if (_player != null || player == null || seat == null || player.Character == null || player.Character.navmeshAgent == null
                || !player.Character.navmeshAgent.enabled || PlayerHelper.IsUsingVehicle || PlayerHelper.IsHoldingItem || RagdollSystem.Holds(player.Character))
            {
                return false;
            }
            _stoodAt = player.Character.transform.position;
            _stoodFacing = player.Character.transform.rotation;
            _player = player;
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            player.Character.SitOnChair(seat);
            return true;
        }

        /// <summary>On their feet again where they stood, free to walk. Never throws.</summary>
        public void Stand()
        {
            PlayerController player = _player;
            if (player == null)
            {
                return;
            }
            _player = null;
            try
            {
                ThirdPersonCharacter body = player.Character;
                if (body != null && !RagdollSystem.Holds(body))
                {
                    body.transform.SetPositionAndRotation(_stoodAt, _stoodFacing);   // there first, then the agent (see CityPerson.Rise)
                    body.Reset();
                    NavMeshAgent agent = body.navmeshAgent;
                    if (agent != null && agent.enabled)
                    {
                        agent.Warp(_stoodAt);
                    }
                    body.transform.rotation = _stoodFacing;
                }
            }
            finally
            {
                player.UnsetNavigationBlocker(Blocker);
            }
        }
    }
}
