using System.Collections.Generic;
using Buildings.Outdoors;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>Staying a station: a precinct whose unit has been let moves, and its doors are kept open.</summary>
    internal sealed partial class Precincts
    {
        /// <summary>Every few seconds: moves precincts whose units were let and keeps the doors open, leaving <paramref name="inside"/> alone.</summary>
        public void Keep(Precinct inside)
        {
            if (++_nextLook < 5)
            {
                return;
            }
            _nextLook = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                Precinct precinct = _all[i];
                if (precinct != inside && !Custody.Active && (precinct.WalkIn ? !Vacant(precinct.Building) : !precinct.Stays && SpecialBuildings.Players(precinct.Building)))
                {
                    Move(precinct);
                }
                Doors(precinct, CanEnter(precinct));
            }
        }

        private void Move(Precinct precinct)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null)
            {
                return;
            }
            Vector4 sum = Vector4.zero;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (Usable(buildings[i], out Vector3 door) && buildings[i].building.Neighbourhood == precinct.District)
                {
                    sum += new Vector4(door.x, door.y, door.z, 1f);
                }
            }
            Vector3 middle = sum.w > 0f ? new Vector3(sum.x, sum.y, sum.z) / sum.w : precinct.Door;
            Doors(precinct, false);
            bool players = SpecialBuildings.Players(precinct.Building);   // off the player's building to any other, empty or not
            if (!Best(buildings, precinct.District, middle, precinct.Building, out Candidate best) || (!best.WalkIn && !players))
            {
                // Nowhere empty to go: it stays, with its desk at the door, and is not looked at again for it.
                if (precinct.WalkIn)
                {
                    _logger?.Info("Law: " + precinct.Name + ": its unit has been let and the district has no other empty one; it stays, with the front desk at its door.");
                }
                precinct.WalkIn = false;
                precinct.Stays = players;
                return;
            }
            if (_frontage.Dressed == precinct)
            {
                _frontage.Undress();
            }
            precinct.Building = best.Building;
            precinct.Door = best.Door;
            precinct.Address = best.Address;
            precinct.WalkIn = best.WalkIn;
            precinct.Score = best.Score;
            Mark(precinct);
            Remember();
            string address = best.Building.building.Address.ToFormattedString();
            _logger?.Info("Law: " + precinct.Name + " has moved to " + address + " (its old unit was let); frontage " + precinct.Score + ".");
            if (SaveGameManager.Current != null)
            {
                var data = new Dictionary<string, string>(2) { { "precinct", precinct.Name }, { "address", address } };
                Notifications.Show(NotificationType.Info, "reallife_law_precinct_moved", data, 12f);
            }
        }

        // The game's own flag: with it set, the door lets the player in whatever the registration says. Put back as it was.
        private static void Doors(Precinct precinct, bool open)
        {
            if (precinct.DoorsOpen == open || precinct.Building == null || precinct.Building.entranceDoors == null)
            {
                return;
            }
            BuildingEntranceDoor[] doors = precinct.Building.entranceDoors;
            if (open)
            {
                precinct.DoorWas = new bool[doors.Length];
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        precinct.DoorWas[i] = doors[i].alwaysOpen;
                        doors[i].alwaysOpen = true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        doors[i].alwaysOpen = precinct.DoorWas != null && i < precinct.DoorWas.Length && precinct.DoorWas[i];
                    }
                }
                precinct.DoorWas = null;
            }
            precinct.DoorsOpen = open;
        }
    }
}
