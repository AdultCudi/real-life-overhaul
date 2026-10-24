using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Roommates;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Household
{
    /// <summary>
    /// A member of the household staff seen at work in the home, as the roommate is: one character, at a place for their work
    /// (beside the cooker for the cook, clear floor for the cleaner, either for the butler), playing their work's clips; in
    /// the home only while the player is there during the visit, and never put down where the player is looking unless they
    /// have just come in. Cost: <see cref="Tick"/> once a second while the player is in the staff's home; per frame only the
    /// cast's turn and hide.
    /// </summary>
    public sealed class HouseholdAtHome
    {
        private const float JustIn = 4f;          // seconds after coming through the door in which they may be put down in view: they were there already
        private const float ClipLeast = 22f, ClipMost = 40f;   // seconds between changes of what they are doing
        private const float FromDoor = 1.8f;      // metres a place keeps from where the player came in
        private const float Beside = 2.0f;        // metres a kitchen place may be from what it is beside

        private static readonly string[] KitchenItems = { "stove", "oven", "grill", "kitchen", "sink", "fridge", "microwave" };

        private readonly IModLogger _logger;
        private readonly RoommateCast _cast;
        private CityPerson _person;
        private StaffMember _who;
        private object _building;
        private bool _inside;
        private float _cameIn;
        private Vector3 _entry;
        private bool _kitchen;         // the place they are at is beside the kitchen's things
        private float _nextClip;
        private int _turn;
        private bool _failedLogged;

        public HouseholdAtHome(Transform parent, IModLogger logger)
        {
            _logger = logger;
            _cast = new RoommateCast(parent, logger, "RealLifeOverhaul Household Staff", "Household staff");
        }

        /// <summary>True while the cast has somebody out: the director then gives it its frame.</summary>
        public bool Out => _cast.Out;

        /// <summary>The member shown now, or null.</summary>
        public StaffMember Shown => _person != null && !_person.Lost ? _who : null;

        public bool Inside => _inside;

        public void Frame(float delta)
        {
            _cast.Tick(delta);
        }

        /// <summary>A look, once a second, with the player in the staff's home: <paramref name="who"/> is the member at work now, or null.</summary>
        public void Tick(float now, StaffMember who)
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || manager.enteringBuilding || manager.exitingBuilding)
            {
                return;
            }
            Vector3 player = PlayerHelper.GetPosition();
            if (!_inside || !ReferenceEquals(_building, manager.building))
            {
                Leave();
                _inside = true;
                _building = manager.building;
                _entry = player;
                _cameIn = now;
                _failedLogged = false;
            }
            if (_person != null && _person.Lost)
            {
                _person = null;
                _who = null;
            }
            if (_person != null && !ReferenceEquals(_who, who))
            {
                Release();   // the visit is over, or somebody else's has begun
            }
            if (who == null)
            {
                return;
            }
            if (_person == null)
            {
                Arrive(now, who, manager, player);
            }
            else if (now >= _nextClip || (_person.ClipOver && _person.Playing != null))
            {
                Work(now);
            }
        }

        private void Arrive(float now, StaffMember who, BuildingManager manager, Vector3 player)
        {
            if (_cast.Failed)
            {
                return;
            }
            bool kitchenFirst = who.Kind == StaffKind.Cook || (who.Kind == StaffKind.Butler && HouseholdGame.Hour >= 15);
            bool kitchen = kitchenFirst;
            if (!Place(kitchen, manager, player, out Vector3 spot, out Vector3 face, out int type))
            {
                kitchen = !kitchenFirst;
                if (!Place(kitchen, manager, player, out spot, out face, out type))
                {
                    if (!_failedLogged)
                    {
                        _failedLogged = true;
                        _logger?.Info("Household staff: no clear floor on a navigation mesh was found in this home; " + who.Name + " works here unseen.");
                    }
                    return;
                }
            }
            if (now - _cameIn > JustIn && CitySpots.InView(spot))
            {
                return;   // nobody appears where the player is looking: tried again next second
            }
            CityPerson person = _cast.Put(HouseholdGame.Person(who), spot, face, type);
            if (person == null)
            {
                if (!_failedLogged)
                {
                    _failedLogged = true;
                    _logger?.Info("Household staff: " + who.Name + " could not be put on the floor of this home; they work here unseen.");
                }
                return;
            }
            _person = person;
            _who = who;
            _kitchen = kitchen;
            _turn = 0;
            Work(now);
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Household staff check (at home): " + who.Name + " (" + who.Kind + ") is shown " + (kitchen ? "in the kitchen" : "on the floor") + ", "
                              + Mathf.Sqrt(CitySpots.Flat(person.Position - _entry)).ToString("F1", CultureInfo.InvariantCulture) + " m from the door (wanted "
                              + FromDoor.ToString("F1", CultureInfo.InvariantCulture) + " or more), playing " + (person.Playing ?? "nothing") + ": PASS.");
            }
        }

        // Their work's clips in turn; the last of each is the game's way of standing when the mod's clips cannot be had.
        private void Work(float now)
        {
            _nextClip = now + Random.Range(ClipLeast, ClipMost);
            CityProps props = _cast.Props;
            string first, second;
            switch (_who.Kind)
            {
                case StaffKind.Cook:
                    first = _turn % 3 == 2 ? "counter_wipe" : "cook_stir";
                    second = "counter_wipe";
                    break;
                case StaffKind.Butler:
                    first = _kitchen ? (_turn % 2 == 0 ? "cook_stir" : "counter_wipe") : (_turn % 2 == 0 ? "mop_floor" : "sweep");
                    second = _kitchen ? "counter_wipe" : "mop_floor";
                    break;
                default:
                    first = _kitchen ? "counter_wipe" : (_turn % 3 == 0 ? "mop_floor" : _turn % 3 == 1 ? "sweep" : "mop_floor");
                    second = _kitchen ? "counter_wipe" : "sweep";
                    break;
            }
            _turn++;
            if (_person.Playing == first && !_person.ClipOver)
            {
                return;
            }
            _person.PutAway(props);
            if (!_person.Work(first, props, 0.4f) && !_person.Work(second, props, 0.4f))
            {
                _person.Work("arms_crossed", props, 0.4f);
            }
        }

        // A place for their work: beside the kitchen's things, or clear floor round the player; clear of the door either way.
        private bool Place(bool kitchen, BuildingManager manager, Vector3 player, out Vector3 spot, out Vector3 face, out int type)
        {
            spot = default;
            face = player;
            type = NavMeshHelperIds.Npc;
            if (kitchen)
            {
                List<ItemController> items = manager.allItemControllers;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    ItemController item = items[i];
                    if (item == null || !item.isActiveAndEnabled || string.IsNullOrEmpty(item.itemName) || !Is(item.itemName, KitchenItems))
                    {
                        continue;
                    }
                    Vector3 at = item.transform.position;
                    if (FamilyPlaces.Floor(at, player, 0.4f, 1.6f, out spot, out type) && Clear(spot) && CitySpots.Flat(spot - at) <= Beside * Beside)
                    {
                        face = at;
                        return true;
                    }
                }
                return false;
            }
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = 2.8f + ring * 1.6f;
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i + ring * 0.5f) * Mathf.PI / 4f;
                    Vector3 near = player + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (FamilyPlaces.Floor(near, player, 0.6f, 0.5f, out spot, out type) && Clear(spot))
                    {
                        face = spot + (spot - player);   // facing away into the room they are cleaning
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Clear(Vector3 spot) => CitySpots.Flat(spot - _entry) >= FromDoor * FromDoor;

        private static bool Is(string name, string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (name.IndexOf(parts[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Out of the home: the character back on the shelf. Safe at any time.</summary>
        public void Leave()
        {
            Release();
            _inside = false;
            _building = null;
        }

        private void Release()
        {
            if (_person != null && !_person.Lost)
            {
                _person.PutAway(_cast.Props);
            }
            _cast.Release();
            _person = null;
            _who = null;
        }

        public void Dispose()
        {
            Leave();
            _cast.Dispose();
        }
    }
}
