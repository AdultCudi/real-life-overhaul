using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// The director's part for collecting what was bought (docs/features/hustles.md, "Collecting"): the seller's door found and
    /// measured once, the pickup point always a point once the city has the building, the route that ends on it, the parcel
    /// stood there for the player to see, a word to a player who comes by car, what lapses and what a load mends. Once a second
    /// the few doors in play are measured against the player; nothing per frame.
    /// </summary>
    public sealed partial class HustleDirector
    {
        private const int LooksASecond = 2;        // doors first looked up in the city's list a second: many new doors are spread over seconds

        /// <summary>A seller's, client's or repair shop's door: its building, its pickup point, the parcel stood there.</summary>
        private sealed class Door : PickupEntry<CityBuildingController>
        {
            public bool Has;            // a point was found
            public bool Refined;        // ...and it is the final one: the best kind, or found with the player near the door
            public int Kind;            // HustleGame.SpotFront to SpotDoor
            public Vector3 Front;       // the pickup point
            public Vector3 DoorAt;      // the door itself, for the parcel to face
            public Transform Parcel;    // the parcel stood at the point, or null
            public bool ParcelTried;    // a parcel was asked for and the props had none: not asked again until the point moves
        }

        private static readonly Func<string, int, CityBuildingController> FindBuilding = HustleGame.Building;

        private PickupBook<Door, CityBuildingController> _doors;
        private JobProps _props;
        private readonly List<int> _told = new List<int>(4);   // things whose buyer was told to get out of the car, until they leave the place
        private int _looks;

        private void MakeDoors()
        {
            _doors = new PickupBook<Door, CityBuildingController>(PickupRules.DoorsKept, Unstage);
            _props = new JobProps(transform);
        }

        // ---------------------------------------------------------------- a door's point

        // A door's pavement, measured once and remembered: a building is asked for in the city's list again every few seconds while the
        // city has none (a district standing late, a load not done), and its point measured once the player is near it (far off, the
        // walking mesh there may not be ready); a point found by a wider search is measured again once the player is at the door.
        private Door Spot(string street, int number, Vector3 near, bool now)
        {
            Door d = _doors.Find(street, number);
            if (d == null)
            {
                if (!now && _looks <= 0)
                {
                    return null;
                }
                _looks--;
                d = _doors.Add(street, number);
            }
            bool had = d.Building != null;
            if (!d.Seek(Time.unscaledTime, now, FindBuilding))
            {
                return d;
            }
            if (!had && d.Looks > 1 && Verbose)
            {
                Logger?.Info("Hustles check (pickup): the city had a building at " + street + " " + number + " on the look number " + d.Looks + " (it was not in its list at first): PASS");
            }
            float away = JobSpots.Flat(d.Building.transform.position, near);
            if (!d.Has)
            {
                if (now || away <= PickupRules.MeasureWithin)
                {
                    Measure(d, away <= PickupRules.RefineWithin);
                }
            }
            else if (!d.Refined && (now || away <= PickupRules.RefineWithin))
            {
                Measure(d, away <= PickupRules.RefineWithin);
            }
            return d;
        }

        private void Measure(Door d, bool close)
        {
            if (!HustleGame.PickupSpot(d.Building, out Vector3 spot, out Vector3 door, out int kind))
            {
                return;   // the building has no door that stands: asked again on the next look
            }
            if (d.Has && (spot - d.Front).sqrMagnitude > 0.09f)
            {
                Unstage(d);   // the point moved: the parcel is stood again at the new one
            }
            bool first = !d.Has;
            d.Has = true;
            d.Front = spot;
            d.DoorAt = door;
            d.Kind = kind;
            d.Refined = kind == HustleGame.SpotFront || close;
            if (first || d.Refined)
            {
                if (Verbose)
                {
                    Logger?.Info("Hustles check (pickup point): " + d.Street + " " + d.Number + " " + (d.Refined ? "settled" : "found, to be measured again at the door") + " as "
                                 + (kind == HustleGame.SpotFront ? "the pavement at the door" : kind == HustleGame.SpotWide ? "the pavement within 14 m of the door"
                                     : kind == HustleGame.SpotMesh ? "the walking mesh within 8 m of the door" : "the ground under the door") + ", "
                                 + JobSpots.Flat(spot, door).ToString("F1", CultureInfo.InvariantCulture) + " m from the door: PASS");
                }
            }
        }

        private bool FrontOf(string street, int number, Vector3 near, bool now, out Vector3 front)
        {
            front = default;
            Door d = Spot(street, number, near, now);
            if (d == null || !d.Has)
            {
                return false;
            }
            front = d.Front;
            return true;
        }

        /// <summary>The pickup point at an address's door, measured now if need be, for the tests, the route and the hired van; false if the city has no building there.</summary>
        internal bool DoorAt(string street, int number, out Vector3 front)
        {
            PlayerController player = HustleGame.Player;
            return FrontOf(street, number, player != null ? player.transform.position : Vector3.zero, true, out front);
        }

        private bool Near(string street, int number, Vector3 here)
        {
            if (string.IsNullOrEmpty(street) || !FrontOf(street, number, here, false, out Vector3 front))
            {
                return false;
            }
            Vector3 d = front - here;
            float rise = d.y;
            d.y = 0f;
            if (!PickupRules.Reaches(d.magnitude, rise))
            {
                return false;
            }
            _atFront = front;
            return true;
        }

        // ---------------------------------------------------------------- the route

        /// <summary>The game's destination marker on a pickup: the address kept in the save, the marker on the pickup point. False, the player told, if the city has no building there.</summary>
        internal bool Route(string street, int number)
        {
            bool have = false, marked = false;
            Vector3 spot = default;
            try
            {
                have = DoorAt(street, number, out spot);
                marked = HustleGame.Route(street, number, have, spot);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (!marked)
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_way_missing", "place", HustleText.Place(street, number), 10f);
            }
            string ends = have ? "the pickup point" : "the building";
            Logger?.Info("Hustles: the route to " + street + " " + number + (marked ? " ends on " + ends : " could not be marked: the city has no building there") + ".");
            return marked;
        }

        // The route the game kept in the save across a load stands on the building: moved onto the pickup point once, never when the
        // player has set another destination.
        private void Retarget(Vector3 here)
        {
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                if (o.State != OwnedState.Waiting)
                {
                    continue;
                }
                Door d = _doors.Find(o.Street, o.Number);
                int at = HustleGame.RouteState(o.Street, o.Number, d != null && d.Has ? d.Front : here, out Vector3 end);
                if (at == 0)
                {
                    continue;
                }
                if (at == 2 && (d == null || !d.Has))
                {
                    d = Spot(o.Street, o.Number, here, true);   // the player is on the way: the point is measured now, from where the walking mesh may not be ready
                }
                if (d == null || !d.Has)
                {
                    continue;
                }
                if (at == 2 || JobSpots.Flat(end, d.Front) > PickupRules.RouteEnd)
                {
                    HustleGame.Route(o.Street, o.Number, true, d.Front);
                    if (Verbose)
                    {
                        Logger?.Info("Hustles check (route): the route to " + o.Street + " " + o.Number + " moved onto the pickup point: PASS");
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the parcel

        // A parcel at each door with something waiting, while the player is within sight of it; taken away when they go, or it is collected.
        private void Stage(Vector3 here)
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                Door d = _doors[i];
                if (d.Parcel != null && (!WaitingAt(d.Street, d.Number) || JobSpots.Flat(d.Front, here) > PickupRules.UnstageBeyond))
                {
                    Unstage(d);
                }
            }
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                if (o.State != OwnedState.Waiting)
                {
                    continue;
                }
                Door d = Spot(o.Street, o.Number, here, false);
                if (d != null && d.Has && d.Parcel == null && !d.ParcelTried && JobSpots.Flat(d.Front, here) <= PickupRules.StageWithin)
                {
                    Stand(d, o);
                }
            }
        }

        private bool WaitingAt(string street, int number)
        {
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                if (o.State == OwnedState.Waiting && o.Number == number && string.Equals(o.Street, street, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ParcelFor(Owned o)
        {
            ThingDef def = o.Car ? null : HustleCatalogue.Thing(o.Thing);
            return def == null ? "parcel_s" : def.Kind == ThingKind.Furniture ? "parcel_l" : def.Kind == ThingKind.Electronics ? "parcel_m" : "parcel_s";
        }

        private void Stand(Door d, Owned o)
        {
            d.ParcelTried = true;
            Vector3 facing = d.DoorAt - d.Front;
            d.Parcel = _props?.Stand(ParcelFor(o), d.Front, facing);
            if (d.Parcel == null)
            {
                Logger?.Warn("Hustles check (pickup parcel): no parcel could be made at " + d.Street + " " + d.Number + "; the door's panel still offers the pickup: FAIL");
            }
            else if (Verbose)
            {
                float metres = JobSpots.Flat(d.Front, HustleGame.Player.transform.position);
                Logger?.Info("Hustles check (pickup parcel): a parcel stands at " + d.Street + " " + d.Number + ", " + metres.ToString("F0", CultureInfo.InvariantCulture)
                             + " m from the player: PASS");
            }
        }

        private void Unstage(Door d)
        {
            if (d == null)
            {
                return;
            }
            if (d.Parcel != null)
            {
                _props?.Return(d.Parcel);
            }
            d.Parcel = null;
            d.ParcelTried = false;
        }

        private void UnstageAll()
        {
            _doors?.Clear();
            _told.Clear();
        }

        // ---------------------------------------------------------------- a player in a vehicle

        // Come by car to the point and nothing shows: the panel is for a player on foot, so they are told once, until they leave.
        private void Nudge(Vector3 here)
        {
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                if (o.State != OwnedState.Waiting)
                {
                    continue;
                }
                Door d = Spot(o.Street, o.Number, here, false);
                float away = d != null && d.Has ? JobSpots.Flat(d.Front, here) : float.MaxValue;
                if (away > PickupRules.NudgeWithin)
                {
                    _told.Remove(o.Id);
                    continue;
                }
                if (!_told.Contains(o.Id) && PickupRules.Why(true, d.Building != null) == PickupWhy.GetOut)
                {
                    _told.Add(o.Id);
                    HustleText.Note(NotificationType.Info, "reallife_hustle_note_get_out", "place", HustleText.Place(o.Street, o.Number), "thing", HustleText.Thing(o.Thing, o.Car), 10f);
                    Logger?.Info("Hustles: the player came to " + o.Street + " " + o.Number + " in a vehicle; told to get out to collect the " + o.Thing + ".");
                }
            }
        }

        // ---------------------------------------------------------------- on load and by the hour

        // The waiting things of a save made right: a hold set for one from before things lapsed, a refund for one with no address.
        private int Mend()
        {
            var gone = new List<Owned>(2);
            int waiting = 0;
            for (int i = 0; i < _state.Items.Count; i++)
            {
                waiting += _state.Items[i].State == OwnedState.Waiting ? 1 : 0;
            }
            int changed = PickupRules.Repair(_state, HustleGame.Day, gone);
            for (int i = 0; i < gone.Count; i++)
            {
                Refund(gone[i].Thing, gone[i].Car, PickupRules.Refund(gone[i]), "had no address to be collected at");
            }
            if (waiting > 0)
            {
                Logger?.Info("Hustles check (pickups on load): " + waiting + " thing(s) waiting at a seller's door, " + changed + " made right, " + gone.Count
                             + " refunded; each is held to its last day: PASS");
            }
            return changed;
        }

        private void Refund(string thing, bool car, int amount, string why)
        {
            HustleGame.Refund(amount, "reallife_transaction_hustle_refund");
            HustleText.Note(NotificationType.Warning, amount > 0 ? "reallife_hustle_note_refunded" : "reallife_hustle_note_lapsed_free", "thing", HustleText.Thing(thing, car),
                "amount", HustleText.Money(amount), 10f);
            Logger?.Info("Hustles check (pickup refund): the " + thing + " " + why + "; " + amount + " returned: PASS");
        }
    }
}
