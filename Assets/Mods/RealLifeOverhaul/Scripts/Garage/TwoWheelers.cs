using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Bicycles and motorbikes. Each is the game's own electric scooter, made, ridden and saved as the game does one, and
    /// dressed by <see cref="BikeMount"/>; which ones is a line in modData. Without the mod they are scooters again. A
    /// reference comparison a frame, the place panel four times a second, the walk over the scooters once a second.
    /// </summary>
    public sealed partial class TwoWheelers
    {
        public const string ScooterType = "ba:vehicletype_electricscooter";

        public static IModLogger Logger;

        private readonly Transform _owner;
        private readonly BikePlaces _places;
        private readonly BikeNames _names = new BikeNames();
        private readonly List<string> _scratch = new List<string>(8);

        private VehicleController _selected;
        private BikeMount _riding;
        private bool _restored;
        private bool _tired;
        private int _day = -1;
        private int _helmetFineDay = -1;
        private float _nextPoliceLook;
        private bool _failed;
        private bool _offBare;          // with the option off, the last walk over the scooters left none dressed

        // the stand's list of what is for sale
        private readonly List<Action> _acts = new List<Action>(8);
        private readonly Action<int> _onPick;
        private readonly Action _onListClosed;
        private BikePlace _panelPlace;
        private string _listClosing;

        // the game's panel at a dock, a rack or a stand
        private readonly WorldPanel _world = new WorldPanel { Name = "bike place" };
        private readonly Action _onUse;
        private BikePlace _promptPlace;
        private int _promptState;       // 1 hire, 2 return, 3 browse
        private int _worldState;        // 0 not up, 1 up, 2 refused by the game's own panel
        private int _keyFrame = -1;
        private readonly GuideMark _mark = new GuideMark("bikes and scooters");

        // the marker that points at a two-wheeler just handed over or asked for
        private float _pointUntil;         // unscaled time; 0: not pointing
        private BikeMount _pointMount;
        private Vector3 _pointAt;
        private string _pointLabel;

        // the look at a two-wheeler shortly after it was made: has it settled where it was put?
        private BikeMount _watch;
        private float _watchAt;
        private Vector3 _watchSpot;
        private Quaternion _watchFacing;
        private bool _watchRetried;

        public TwoWheelers(Transform owner)
        {
            _owner = owner;
            _places = new BikePlaces(owner);
            _onUse = UseClicked;
            _onPick = Picked;
            _onListClosed = ListClosed;
            PanelPlaces.Set("bike place", BikesNear);   // somebody waiting for the player is stood clear of them
        }

        // The docks, racks and stands near a point, with the reach of their panel.
        private int BikesNear(Vector3 around, float within, float[] x, float[] z, float[] reach, int count)
        {
            IReadOnlyList<BikePlace> all = _places.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                {
                    count = PanelPlaces.Add(around, within, all[i].Position, BikePlaces.Reach, x, z, reach, count);
                }
            }
            return count;
        }

        /// <summary>The director whose life keeps the marker: destroyed or stopped by the guard, the marker goes.</summary>
        internal void KeptBy(Behaviour host)
        {
            _mark.Keep(host);
        }

        /// <summary>True while the panel at a place is up with its button on the interact key, and in the frame it ran.</summary>
        public bool TakesInteractKey => _world.Showing || _keyFrame == Time.frameCount;

        public BikePlaces Places => _places;

        public BikeMount Riding => _riding;

        public bool PanelOpen => GarageList.OpenFor(this);

        /// <summary>Each frame: who is riding what.</summary>
        public void Frame(GameManager gm)
        {
            if (_failed)
            {
                return;
            }
            VehicleController selected = gm.selectedVehicle;
            if (selected != _selected)
            {
                _selected = selected;
                if (_riding != null)
                {
                    _riding.Dismounted();
                    Unpin(_riding, gm, "left");   // every way off (the game's own, a fall, the dock's return) comes through here
                }
                _riding = selected != null ? selected.GetComponent<BikeMount>() : null;
                _tired = false;
                if (_riding != null)
                {
                    _riding.Effort = 1f;
                    _riding.Mounted(gm.playerController);
                    Unpin(_riding, gm, "got on");
                }
                _names.Ride(_riding);
                return;
            }
            if (_riding == null)
            {
                return;
            }
            _names.Frame();
            if (_riding.Fell > 0f)
            {
                Fall(_riding);
            }
        }

        // A knockdown if the Ragdoll feature offers one; otherwise the bike stops and they are on their feet.
        private void Fall(BikeMount mount)
        {
            float speed = mount.Fell;
            mount.Fell = 0f;
            ScooterController scooter = mount.Scooter;
            scooter.Reset();
            mount.BarsOff();   // before the game hands the rider back to physics
            scooter.ExitVehicle();
            bool played = false;
            try
            {
                played = GarageBridges.RiderFalls != null && GarageBridges.RiderFalls(speed);
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the knockdown bridge failed. " + e.Message);
            }
            if (speed > 9f)
            {
                ModState.AddModifier(ModState.MoodDown);
            }
            Logger?.Info("Two-wheelers: the rider came off a " + mount.Def.Kind + " at " + (speed * 3.6f).ToString("F0") + " km/h" + (played ? " (knockdown played)." : " (stopped and stood up)."));
            Notifications.Show(NotificationType.Warning, "reallife_bike_fell", null, 6f);
        }
        // Stands a scooter somewhere else at rest, the way the game leaves one: body, transform and place in the save.
        private static void Place(ScooterController scooter, Vector3 position, Quaternion rotation)
        {
            Rigidbody body = scooter.GetComponent<Rigidbody>();
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = position;
                body.rotation = rotation;
            }
            scooter.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            scooter.SavePosition();
            scooter.UpdateNavMeshTargets();
        }

        // As high as it stood over the ground (at rest it does not settle), or just off it if it was lying down.
        private static float Lift(ScooterController scooter, Vector3 was)
        {
            if (BikePlaces.StandHeight(was, out float under, out _) && was.y - under > -0.1f && was.y - under < 0.4f
                && Vector3.Angle(scooter.transform.up, Vector3.up) < 20f)
            {
                return Mathf.Max(0f, was.y - under);
            }
            return 0.02f;
        }

        // As a bike is got off and on: the player's own standing in something solid is moved to a clear place.
        private void Unpin(BikeMount mount, GameManager gm, string when)
        {
            try
            {
                if (mount == null || mount.Scooter == null || mount.Record == null || GarageState.Bike(mount.Record.Id) != mount.Record)
                {
                    return;   // gone, or being handed back at a dock
                }
                Transform bike = mount.transform;
                Transform player = gm.playerController != null ? gm.playerController.transform : null;
                bool pinned = mount.Pinned(bike.position, bike.rotation, BikePlaces.PinMask, player);
                string what = pinned && BikeMount.PinnedBy != null ? BikeMount.PinnedBy.name + " (" + LayerMask.LayerToName(BikeMount.PinnedBy.gameObject.layer) + ")" : null;
                bool mine = pinned && GoingPublic.MineVehicle(mount.Scooter);
                if (!BikeClearRules.Move(pinned, mine, BuildingManager.IsInsideBuilding))
                {
                    if (pinned)
                    {
                        Logger?.Info("Two-wheelers: clear-place check (" + when + "): it stands in " + what + "; not moved (" + (mine ? "the player is indoors" : "not the player's own") + ").");
                    }
                    else if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Two-wheelers: clear-place check (" + when + "): nothing solid in its frame or its bars at "
                                     + bike.position.ToString("F1", CultureInfo.InvariantCulture) + ". PASS");
                    }
                    return;
                }
                Vector3 was = bike.position;
                if (!ClearSpot(mount, player, out Vector3 spot, out Quaternion facing))
                {
                    Logger?.Warn("Two-wheelers: clear-place check (" + when + "): it stands in " + what + " and nothing within "
                                 + BikeClearRules.MostShift.ToString("F1", CultureInfo.InvariantCulture)
                                 + " m is clear; it stays where it is (\"Bring it here\" stands it in front of the player): " + mount.Report());
                    return;
                }
                Place(mount.Scooter, spot, facing);
                mount.Record.Position = spot;
                mount.Record.Heading = facing.eulerAngles.y;
                if (!mount.Ridden)
                {
                    Watch(mount, spot, facing);
                }
                Logger?.Info("Two-wheelers: clear-place check (" + when + "): it stood in " + what + "; moved "
                             + Mathf.Sqrt(CitySpots.Flat(spot - was)).ToString("F2", CultureInfo.InvariantCulture) + " m to clear ground: " + mount.Report());
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the clear-place check could not be made; the bike stays where it is. " + e.Message);
            }
        }

        // The nearest of BikeClearRules' places where the bike stands clear, pavement first, no wall between.
        private static bool ClearSpot(BikeMount mount, Transform player, out Vector3 spot, out Quaternion facing)
        {
            Transform bike = mount.transform;
            Vector3 from = bike.position;
            facing = Quaternion.Euler(0f, bike.eulerAngles.y, 0f);
            float lift = Lift(mount.Scooter, from);
            float level = BikePlaces.StandHeight(from, out float under, out _) ? under : from.y;
            int room = BikePlaces.RoomMask;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < BikeClearRules.Places; i++)
                {
                    BikeClearRules.Place(i, out float across, out float along);
                    Vector3 at = from + facing * new Vector3(across, 0f, along);
                    if (!BikePlaces.StandHeight(at, out float height, out bool pavement) || (pass == 0 && !pavement)
                        || Mathf.Abs(height - level) > (pass == 0 ? 0.6f : 0.3f))
                    {
                        continue;
                    }
                    spot = new Vector3(at.x, height + lift, at.z);
                    if (!mount.Pinned(spot, facing, room, player) && BikePlaces.Unwalled(from, spot, 1.2f))
                    {
                        return true;
                    }
                }
            }
            spot = default;
            return false;
        }

        // A second look two seconds on at one just made or moved: the log line says whether it stayed.
        private void Watch(BikeMount mount, Vector3 spot, Quaternion facing)
        {
            _watch = mount;
            _watchSpot = spot;
            _watchFacing = facing;
            _watchAt = Time.time + 2f;
            _watchRetried = false;
        }

        private void Settled()
        {
            BikeMount mount = _watch;
            _watchAt = 0f;
            if (mount == null)
            {
                _watch = null;
                Logger?.Warn("Two-wheelers: the two-wheeler stood at " + _watchSpot.ToString("F1", CultureInfo.InvariantCulture) + " two seconds ago is no longer in the world: the game removed it.");
                return;
            }
            if (mount.Ridden)
            {
                _watch = null;   // where it is now is the rider's doing
                return;
            }
            Transform bike = mount.transform;
            Vector3 at = bike.position;
            bool astray = at.y < _watchSpot.y - 1f || at.y > _watchSpot.y + 1.5f || CitySpots.Flat(at - _watchSpot) > 3f * 3f
                          || Vector3.Angle(bike.up, Vector3.up) > 50f;
            if (!astray)
            {
                _watch = null;
                Logger?.Info("Two-wheelers: two seconds on it stands where it was put: " + mount.Report());
                return;
            }
            Logger?.Warn("Two-wheelers: two seconds on it is NOT where it was put (" + _watchSpot.ToString("F2", CultureInfo.InvariantCulture) + "): " + mount.Report());
            if (_watchRetried)
            {
                _watch = null;
                return;
            }
            // Once: stood back and held, as the game holds a scooter nobody is on.
            _watchRetried = true;
            Place(mount.Scooter, _watchSpot + Vector3.up * 0.05f, _watchFacing);
            mount.Scooter.SetFreeze(true);
            _watchAt = Time.time + 2f;
            Logger?.Warn("Two-wheelers: stood back where it was put and held there.");
        }

        private const float PanelUp = 1.9f;   // metres over the place the panel stands

        /// <summary>Four times a second: the marker, the names and the game's panel at a place.</summary>
        public void Quick(GameManager gm)
        {
            if (_failed)
            {
                return;
            }
            if (_pointUntil > 0f)
            {
                Point(gm);
            }
            _names.Quick();
            if (!_places.Found || gm.playerController == null)
            {
                return;
            }
            Vector3 at = gm.playerController.transform.position;
            if (PanelOpen)
            {
                string why = !Settings.TwoWheelers || _panelPlace == null ? "two-wheelers were switched off" : gm.selectedVehicle != null ? "the player got on something"
                    : (at - _panelPlace.Position).sqrMagnitude > 7f * 7f ? "the player walked away"
                    : GarageList.MenuUp ? "another screen came up" : null;
                if (why != null)
                {
                    _listClosing = why;
                    ClosePanel();
                }
                SetPanel(null, 0, at);
                return;
            }
            BikePlace place = Settings.TwoWheelers && !BuildingManager.IsInsideBuilding && !ThirdPersonCamera.IsUiOpen() && !JobDirector.TakesInteractKey && !TransitDirector.TakesInteractKey
                ? _places.Near(at)
                : null;
            int state = 0;
            if (place != null)
            {
                if (place.Kind == BikePlaceKind.Dock)
                {
                    state = _riding != null ? (_riding.Record != null && !_riding.Record.Owned && Mathf.Abs(_riding.Speed) < 1.5f ? 2 : 0)
                        : gm.selectedVehicle == null ? 1 : 0;
                }
                else
                {
                    state = gm.selectedVehicle == null ? 3 : 0;
                }
            }
            SetPanel(state != 0 ? place : null, state, at);
        }

        // Written when the place or what it offers changes, asked for while the player stands there.
        private void SetPanel(BikePlace place, int state, Vector3 player)
        {
            if (place == null)
            {
                if (_promptPlace != null || _worldState != 0)
                {
                    bool was = _worldState == 1;
                    _promptPlace = null;
                    _promptState = 0;
                    _worldState = 0;
                    _world.Hide();
                    if (was && Meta.Dev.Verbose)
                    {
                        Logger?.Info("Garage check (place panel): closed (its button was used, the player walked away, or another screen is open).");
                    }
                }
                return;
            }
            if (place != _promptPlace || state != _promptState)
            {
                _promptPlace = place;
                _promptState = state;
                string title = GarageText.T(place.Kind == BikePlaceKind.Dock ? "reallife_bike_place_dock"
                    : place.Kind == BikePlaceKind.BicycleShop ? "reallife_bike_place_rack" : "reallife_bike_place_stand");
                if (state == 1)
                {
                    _world.Lines(title, GarageText.T("reallife_bike_line_hire"), GarageText.Money(BikeCatalogue.DockFee), WorldPanel.White());
                    _world.Buttons("reallife_bike_button_hire", _onUse, true, null, null, false);
                }
                else if (state == 2)
                {
                    _world.Lines(title, GarageText.T("reallife_bike_line_return"), null, WorldPanel.White());
                    _world.Buttons("reallife_bike_button_return", _onUse, true, null, null, false);
                }
                else
                {
                    _world.Lines(title, GarageText.T(place.Kind == BikePlaceKind.BicycleShop ? "reallife_bike_panel_bicycles" : "reallife_bike_panel_motorbikes"),
                        GarageText.T("reallife_bike_line_sale"), WorldPanel.Green());
                    _world.Buttons("reallife_bike_button_browse", _onUse, true, null, null, false);
                }
            }
            // Over the place; over the player's head while the place is behind the camera.
            Vector3 over = place.Position + Vector3.up * PanelUp;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = player + Vector3.up * 2.1f;
            }
            _world.For(place.Position);
            _world.Position = over;
            int shown = _world.Show() ? 1 : 2;
            if (shown != _worldState)
            {
                _worldState = shown;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (place panel): at a " + place.Kind + ", " + Mathf.Sqrt(CitySpots.Flat(place.Position - player)).ToString("F1", CultureInfo.InvariantCulture)
                                 + " m from it (offered within " + BikePlaces.Reach.ToString("F1", CultureInfo.InvariantCulture) + " m): "
                                 + (shown == 1 ? "the game's own panel is up with '" + (state == 1 ? "Hire" : state == 2 ? "Return" : "Browse") + "' on the game's interact key: PASS"
                                     : "FAIL (the game's panel is showing another place; it is tried again while the player stands here)") + ".");
                }
            }
        }

        private void UseClicked()
        {
            BikePlace place = _promptPlace;
            int state = _promptState;
            _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
            if (place == null || PanelOpen)
            {
                return;
            }
            try
            {
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (choice): '" + (state == 1 ? "Hire" : state == 2 ? "Return" : "Browse") + "' at a " + place.Kind + ".");
                }
                switch (state)
                {
                    case 1: Hire(place); break;
                    case 2: Return(); break;
                    case 3: OpenPanel(place); break;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: that did not work. " + e);
            }
            SetPanel(null, 0, default);
        }

        /// <summary>Once a second: the places, dressing the scooters that are ours, the hires and the ride.</summary>
        public void Slow(GameManager gm)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                bool on = Settings.Garage && Settings.TwoWheelers;
                GameInstance save = SaveGameManager.Current;
                if (on && !_places.Found && InstanceBehavior<CityManager>.Instance != null && TrafficTools.Street)
                {
                    _places.Find();
                }
                if (gm.playerController != null)
                {
                    _places.Tick(gm.playerController.transform.position, on && !BuildingManager.IsInsideBuilding);
                }

                // Ours dressed with the option on, undressed with it off; off, no walk once one has left none dressed.
                List<VehicleController> all = on || !_offBare ? VehicleHelper.AllPlayerVehicles : null;
                bool dressed = false;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    var scooter = all[i] as ScooterController;
                    if (scooter == null || scooter.vehicleInstance == null)
                    {
                        continue;
                    }
                    BikeRecord record = GarageState.Bike(scooter.vehicleInstance.id);
                    if (record == null)
                    {
                        continue;
                    }
                    BikeMount mount = scooter.GetComponent<BikeMount>();
                    if (on && mount == null)
                    {
                        scooter.vehicleInstance.parkingState = ParkingState.Undefined;   // see Spawn
                        BikeMount attached = BikeMount.Attach(scooter, record);
                        if (attached != null && gm.selectedVehicle == scooter)
                        {
                            _selected = null;   // a save made in the saddle: seated on the next frame
                        }
                        else if (attached != null)
                        {
                            WarnIfWalled(attached);
                        }
                    }
                    else if (!on && mount != null && !mount.Ridden)
                    {
                        mount.Detach();
                    }
                    else if (!on && mount != null)
                    {
                        dressed = true;   // ridden: undressed at a later second
                    }
                }
                if (all != null)
                {
                    _offBare = !on && !dressed;
                }
                if (!on || save == null)
                {
                    return;
                }
                if (_watchAt > 0f && Time.time >= _watchAt)
                {
                    Settled();
                }

                if (!_restored && !BuildingManager.IsInsideBuilding && TrafficTools.Street)
                {
                    _restored = true;
                    RestoreLost(save);
                }
                int day = TimeHelper.CurrentDay;
                if (day != _day)
                {
                    _day = day;
                    CollectHires(save, day);
                }
                if (_riding != null && _riding.Ridden)
                {
                    Ride(save);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Warn("Two-wheelers: switched off after something unexpected; bicycles and motorbikes already out stay as they are. " + e);
                _riding?.Dismounted();   // nothing of a ride is left switched on
                _riding = null;
                _names.Dispose();
            }
        }

        // The rider's second: a bicycle's effort and energy, a motorbike's helmet.
        private void Ride(GameInstance save)
        {
            BikeMount mount = _riding;
            if (mount.Def.Bicycle)
            {
                if (mount.Pedalling)
                {
                    float room = (save.Energy - BikeCatalogue.EnergyFloor) / 1.9f;   // as the jobs spend it: never down to where the game takes over
                    float spend = Mathf.Min(mount.Def.EnergyPerSecond, room);
                    if (spend > 0f)
                    {
                        EnergyHelper.SpentEnergyOnce(spend);
                    }
                }
                bool tired = save.Energy <= BikeCatalogue.EnergyFloor + 1.5f;
                if (tired != _tired)
                {
                    _tired = tired;
                    mount.Effort = tired ? 0.5f : 1f;
                    if (tired)
                    {
                        Notifications.Show(NotificationType.Warning, "reallife_bike_tired", null, 7f);
                    }
                }
                return;
            }
            if (!GarageState.Helmet && Settings.GaragePolice && Settings.Police && _helmetFineDay != TimeHelper.CurrentDay
                && Mathf.Abs(mount.Speed) > 5f && Time.time >= _nextPoliceLook)
            {
                _nextPoliceLook = Time.time + 30f;
                if (UnityEngine.Random.value < 0.10f)
                {
                    _helmetFineDay = TimeHelper.CurrentDay;
                    GarageDirector.Notice("helmet", BikeCatalogue.HelmetFine, "reallife_bike_fine_helmet");
                }
            }
        }

        private void Return()
        {
            BikeMount mount = _riding;
            if (mount == null || mount.Record == null || mount.Record.Owned)
            {
                return;
            }
            string id = mount.Record.Id;
            mount.Scooter.ExitVehicle();
            Remove(id);
            Notifications.Show(NotificationType.Success, "reallife_bike_returned", null, 6f);
        }

        /// <summary>The test button: a new one of this kind in front of the player.</summary>
        public void Test(BikeKind kind)
        {
            TestRefusal why = TestRefusals.Street();
            if (why == TestRefusal.None && !(Settings.Garage && Settings.TwoWheelers))
            {
                why = TestRefusal.FeatureOff;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            if (why == TestRefusal.None && gm.selectedVehicle != null)
            {
                why = TestRefusal.InVehicle;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Two-wheeler (" + kind + ")", why);
                return;
            }
            Transform player = gm.playerController.transform;
            if (!BikePlaces.SpotAhead(player, out Vector3 spot, out Quaternion facing))
            {
                TestRefusals.Tell(Logger, "Two-wheeler (" + kind + ")", TestRefusal.NoRoom);
                return;
            }
            BikeMount mount = Spawn(kind, spot, facing, true);
            if (mount == null)
            {
                TestRefusals.Tell(Logger, "Two-wheeler (" + kind + ")", TestRefusal.NothingFits, 0, "the scooter could not be made or dressed");
            }
            else
            {
                PointTo(mount, spot, mount.Def);
            }
        }

        /// <summary>Everything this has on screen off: the panel at a place, the stand's list, the marker. The two-wheelers stay.</summary>
        public void HideUi()
        {
            ClosePanel();
            StopPointing();
            SetPanel(null, 0, default);
        }

        public void Dispose()
        {
            HideUi();
            _watch = null;
            _watchAt = 0f;
            if (_riding != null)
            {
                _riding.Dismounted();
                _riding = null;
            }
            _names.Dispose();
            _places.Dispose();
            BikeBody.Dispose();
        }
    }
}
