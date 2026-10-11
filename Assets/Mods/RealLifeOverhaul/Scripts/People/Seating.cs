using System;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Characters;
using Controllers;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Sitting down, for everybody of the mod's and for the player: one piece, used wherever somebody sits on something the
    /// GAME made (the furniture a player has put in a home, the benches of its streets).
    ///
    /// The game seats its own people with one call, <c>ThirdPersonCharacter.SitOnChair(seat)</c>: its own sitting pose, made
    /// for its own seats; the navigation agent switched off (a seat is not on the mesh); the body put on the seat's sitting
    /// position and turned as it is; and the seat told that it is taken, so the game offers it to nobody else and seats no
    /// customer on it. It takes them off again with <c>ThirdPersonCharacter.Reset()</c>. Both are plain methods of the
    /// character every person of the mod's is, so this uses exactly those, and adds what they leave open: where the
    /// character stood (they stand up there, on the mesh, not wherever the agent lands) and what "free" means.
    ///
    ///   a person of the mod's   <see cref="CityPerson.Sit"/> / <see cref="CityPerson.Rise"/> (the family's partner, a
    ///                           companion on an outing, anybody from a cast): rising is also done by every way a
    ///                           person is moved, played or put away, so nobody is ever left sitting.
    ///   the player              <see cref="PlayerSeat"/>: the same two calls, with the game's own navigation blocker.
    ///   finding a seat          <see cref="Free(ItemController)"/> for a piece of furniture (the game's
    ///                           <c>SeatController</c>: every chair, armchair, sofa, stool and bench it has);
    ///                           <see cref="Bench"/> for the nearest street bench (<c>OutsideBenchController</c>) with its
    ///                           two places, the player's and the one the game seats a pedestrian on.
    ///
    /// The bus shelter's bench is the mod's own prop and keeps its own clips (<see cref="TransitSeat"/>: sit_bench,
    /// sit_wait, sit_bench_up are made for that seat); the game's pose would not fit it, and the mod's clips do not fit
    /// the game's seats, whose heights differ.
    ///
    /// Nothing here runs by itself: a seat is looked for when somebody asks (a physics query of a few metres, never per
    /// frame) and nothing is allocated but that query's fixed buffer.
    /// </summary>
    public static class Seating
    {
        private const float StandOff = 2.4f;   // metres from where somebody stands to a seat they may take

        private static readonly Collider[] Near = new Collider[32];
        private static FieldInfo _benchSitter;   // OutsideBenchController._npc: the pedestrian the game has put on a bench
        private static bool _benchSitterLooked;

        /// <summary>True if this piece of the game's furniture is something to sit on.</summary>
        public static bool IsSeat(ItemController item)
        {
            return item is SeatController;
        }

        /// <summary>
        /// A sitting position of a piece of furniture that nobody is using, or null: the item must be a seat, be in the
        /// room, and have nobody on any of its places (so never the sofa the player is sitting on).
        /// </summary>
        public static Transform Free(ItemController item)
        {
            var seat = item as SeatController;
            if (seat == null || !seat.isActiveAndEnabled || seat.Occupied)
            {
                return null;
            }
            return seat.SittingPosition;
        }

        /// <summary>True if somebody standing here is near enough to a seat to sit down on it without being seen to jump.</summary>
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

        /// <summary>
        /// The nearest of the game's street benches within so many metres of a point, or null. Looked for with one physics
        /// query; call it on a slow timer, never per frame.
        /// </summary>
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
        /// True if the bench's second place (the one the game seats a pedestrian on) is there and empty. The game keeps who
        /// sits there to itself, so that is read from its own field; if the field cannot be read the place counts as taken.
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
                _benchSitter = typeof(OutsideBenchController).GetField("_npc", BindingFlags.Instance | BindingFlags.NonPublic);
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

        /// <summary>One line per step, with "Developer diagnostics" on.</summary>
        public static void Check(IModLogger logger, string what, bool pass)
        {
            if (Meta.Dev.Verbose)
            {
                logger?.Info("People check (seat): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
        }
    }

    /// <summary>
    /// The player on one of the game's seats, outside the game's own activities (which run the clock and their own screen):
    /// seated and stood up by the game's own two calls, kept from walking meanwhile by the game's own navigation blocker,
    /// and stood up where they stood before. <see cref="Stand"/> is safe at any time and is what every way out calls.
    /// </summary>
    public sealed class PlayerSeat
    {
        private const NavigationBlocker Blocker = NavigationBlocker.RestActivity;   // (as TransitSeat: what the game's own benches set)

        private PlayerController _player;
        private Vector3 _stoodAt;
        private Quaternion _stoodFacing;

        public bool Seated => _player != null;

        /// <summary>Sits the player down. False, and nothing changed, if they cannot be seated now.</summary>
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
