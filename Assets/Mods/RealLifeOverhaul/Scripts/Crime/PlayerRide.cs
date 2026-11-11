using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Reads from the game the signs that something carries the player (<see cref="OffenceRules.Carried"/>), so that nobody the
    /// game shows as carried is taken for a walker in the road. The game's own cars, its taxi and private driver and the mod's bus
    /// each show at least one, and a ride of another mod shows those it borrows from them; one that shows none is taken for a walk,
    /// which the Guide's Known limits say. COST: called twice a second for a foot look, never in a frame of its own: a few field
    /// reads and, with none of them, a walk up the player's two or three parents. Nothing is allocated or searched.
    /// </summary>
    internal static class PlayerRide
    {
        /// <summary>
        /// The signs now. <paramref name="movedSqr"/> (square metres) and <paramref name="seconds"/> are the player's move since the
        /// last foot look. Every sign has a line of its own, named, so that one left out or answered with false shows at once.
        /// </summary>
        public static RideFacts Read(PlayerController player, float movedSqr, float seconds)
        {
            return new RideFacts(
                ownVehicle: PlayerHelper.IsUsingVehicle,
                taxiTrip: TaxiTrip(),
                seated: Seated(player),
                blocked: Blocked(player),
                agentOff: AgentOff(player),
                underVehicle: UnderVehicle(player.transform),
                tooFast: OffenceRules.TooFast(movedSqr, seconds));
        }

        // The game's taxi or private driver trip: the player is a passenger, hidden, until it has put them down.
        private static bool TaxiTrip()
        {
            return InstanceBehavior<TaxiSystem>.IsInitialized && TaxiSystem.IsTraveling;
        }

        private static bool Seated(PlayerController player)
        {
            return player.Character != null && player.Character.isSittingOn != null;
        }

        // The game keeps a set of reasons the player may not walk; a ride that carries them sets one, or they could walk off it.
        private static bool Blocked(PlayerController player)
        {
            return player.NavigationDisabled;
        }

        // The player's NavMeshAgent, which walks them, is switched off by every seat, car and hidden player of the game's (SitOnChair,
        // AttachToObject, ToggleVisibility), and a ride that borrows one of them does the same. With it off nobody can walk.
        private static bool AgentOff(PlayerController player)
        {
            NavMeshAgent agent = player.Character != null ? player.Character.navmeshAgent : null;
            return agent != null && !agent.enabled;
        }

        // The game parents the player under the car they drive; a ride that seats them in a traffic car does the same.
        private static bool UnderVehicle(Transform body)
        {
            for (Transform up = body.parent; up != null; up = up.parent)
            {
                if (up.TryGetComponent(out VehicleComponent _) || up.TryGetComponent(out VehicleController _))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
