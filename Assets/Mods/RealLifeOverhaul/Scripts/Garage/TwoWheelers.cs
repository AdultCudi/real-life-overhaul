using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.MiniMenu;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Bicycles and motorbikes. Each one is, to the game and in the save, an ordinary electric scooter:
    /// made the way the game makes one (<c>VehicleHelper.CreateAndSpawnVehicle</c> with a new
    /// <c>VehicleInstance</c> of the scooter's type), ridden with the scooter's own controller, saved in
    /// the save's own vehicle list. Which scooters are really something else is one line in modData
    /// (<see cref="GarageState.Bikes"/>); <see cref="BikeMount"/> gives each its body, its numbers and its
    /// rider when the game spawns it. Loaded without the mod they are scooters again, and the game clears
    /// them away as it does its own. An owned one that went that way is stood back where it was left.
    ///
    /// Bicycles are hired at bike-share docks and bought at racks outside supermarkets; motorbikes and
    /// helmets are bought at stands outside car dealerships (<see cref="BikePlaces"/>). A bicycle is
    /// pedalled: holding the throttle costs energy through the game's own energy system, a tired rider
    /// has half the push, letting go coasts. A motorbike needs a motorcycle licence and a helmet by law: the dealer
    /// says so at the sale if there is no licence (<see cref="GarageBridges.HasMotorcycleLicence"/>), and riding
    /// without one is for the roadside checks of Licences to find, as with a car.
    ///
    /// What the player sees is the game's own interface. At a dock, a rack or a stand the game's panel over a
    /// place (GameUi.WorldPanel, the box it shows at a petrol pump) names the place and has one button on the
    /// game's interact key: hire, return, or browse. Browsing opens the game's list (<see cref="GarageList"/>)
    /// with what is for sale. A two-wheeler just handed over or asked for is marked with the game's own
    /// destination marker (GameUi.GuideMark). No key is read here.
    /// </summary>
    public sealed class TwoWheelers
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
        private bool _offBare;          // with the option off, the last walk over the player's scooters left none dressed

        // the stand's list of what is for sale
        private readonly List<Action> _acts = new List<Action>(8);
        private readonly Action<int> _onPick;
        private readonly Action _onListClosed;
        private BikePlace _panelPlace;
        private string _listClosing;

        // the game's panel at a dock, a rack or a stand
        private readonly WorldPanel _world = new WorldPanel();
        private readonly Action _onUse;
        private BikePlace _promptPlace;
        private int _promptState;       // 1 hire, 2 return, 3 browse
        private int _worldState;        // 0 not up, 1 up, 2 refused by the game's own panel
        private int _keyFrame = -1;
        private readonly GuideMark _mark = new GuideMark();

        // the marker that points at a two-wheeler just handed over or asked for
        private float _pointUntil;         // unscaled time; 0 = not pointing (the only thing the idle frame tests)
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
        }

        /// <summary>
        /// True while the game's panel at a dock, a rack or a stand is up with its button on the game's interact key,
        /// and in the frame that button ran: the mod's own interact key does nothing else then.
        /// </summary>
        public bool TakesInteractKey => _world.Showing || _keyFrame == Time.frameCount;

        public BikePlaces Places => _places;

        public BikeMount Riding => _riding;

        public bool PanelOpen => GarageList.OpenFor(this);

        // ---------------------------------------------------------------- every frame: who is riding what

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
                }
                _riding = selected != null ? selected.GetComponent<BikeMount>() : null;
                _tired = false;
                if (_riding != null)
                {
                    _riding.Effort = 1f;
                    _riding.Mounted(gm.playerController);
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

        // The rider has come off. A knockdown if the Ragdoll feature offers one; otherwise the bike stops and they are on their feet.
        private void Fall(BikeMount mount)
        {
            float speed = mount.Fell;
            mount.Fell = 0f;
            ScooterController scooter = mount.Scooter;
            scooter.Reset();
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

        // ---------------------------------------------------------------- finding one

        private const float PointSeconds = 20f;
        private const float PointNear = 3f;
        private const float BringFreeWithin = 25f;

        /// <summary>
        /// Marks a two-wheeler with the game's own destination marker for a while: until the player is within three
        /// metres of it, gets on it, goes indoors, or the time is up. While a job or an outing has the game's marker
        /// it is left to them, and asked for again four times a second (the game's own vehicle marker on the bike and
        /// on the map is there either way).
        /// </summary>
        public void PointTo(BikeMount mount, Vector3 at, BikeDef def, float seconds = PointSeconds)
        {
            _pointMount = mount;
            _pointAt = mount != null ? mount.transform.position : at;
            _pointLabel = GarageText.T("reallife_bike_marker", "name", GarageText.T(def.Name));
            _pointUntil = Time.unscaledTime + seconds;
            bool shown = _mark.Show(_pointAt + Vector3.up * 1.4f, _pointLabel);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (marker): '" + _pointLabel + "' at " + _pointAt.ToString("F1") + " is " + (shown ? "on the game's destination marker: PASS" : "NOT marked yet (the game's marker is on a job or an outing): FAIL")
                             + ".");
            }
        }

        // Four times a second while pointing, and only then: a few comparisons.
        private void Point(GameManager gm)
        {
            bool tracked = !ReferenceEquals(_pointMount, null);
            if (_pointMount != null && (_pointMount.transform.position - _pointAt).sqrMagnitude > 1f)
            {
                _pointAt = _pointMount.transform.position;
            }
            PlayerController player = gm.playerController;
            if (Time.unscaledTime >= _pointUntil || player == null || BuildingManager.IsInsideBuilding
                || (tracked && (_pointMount == null || _pointMount.Ridden))
                || CitySpots.Flat(player.transform.position - _pointAt) < PointNear * PointNear)
            {
                StopPointing();
                return;
            }
            _mark.Show(_pointAt + Vector3.up * 1.4f, _pointLabel);
        }

        private void StopPointing()
        {
            if (_pointUntil <= 0f)
            {
                return;
            }
            _pointUntil = 0f;
            _pointMount = null;
            _mark.Hide();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (marker): taken off (reached, ridden, indoors, or its time was up).");
            }
        }

        /// <summary>The scooter that carries a two-wheeler, if the game has it in the world now.</summary>
        public static ScooterController Find(string id)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is ScooterController scooter && scooter.vehicleInstance != null && scooter.vehicleInstance.id == id)
                {
                    return scooter;
                }
            }
            return null;
        }

        /// <summary>Where a two-wheeler is: where it stands now if it is in the world, otherwise where it was last left.</summary>
        public static Vector3 Where(BikeRecord record)
        {
            ScooterController scooter = Find(record.Id);
            return scooter != null ? scooter.transform.position : record.Position;
        }

        // "ahead of you", "to your left"...: which way a place lies from the player, as the camera sees it.
        private static string Direction(Vector3 to, out int metres)
        {
            PlayerController player = PlayerHelper.PlayerController;
            metres = 0;
            if (player == null)
            {
                return GarageText.T("reallife_bike_dir_ahead");
            }
            Vector3 way = to - player.transform.position;
            way.y = 0f;
            metres = Mathf.RoundToInt(way.magnitude);
            Camera camera = GameManager.GetMainCamera();
            Vector3 ahead = camera != null ? camera.transform.forward : player.transform.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.01f)
            {
                ahead = player.transform.forward;
            }
            if (way.sqrMagnitude < 0.01f)
            {
                return GarageText.T("reallife_bike_dir_ahead");
            }
            float angle = Vector3.SignedAngle(ahead, way, Vector3.up);   // to the right is positive
            return GarageText.T(Mathf.Abs(angle) <= 45f ? "reallife_bike_dir_ahead"
                : Mathf.Abs(angle) >= 135f ? "reallife_bike_dir_behind"
                : angle > 0f ? "reallife_bike_dir_right" : "reallife_bike_dir_left");
        }

        // The toast that says where it is: its name, how far and which way.
        private static void SayWhere(NotificationType type, string key, BikeDef def, Vector3 at, float seconds = 10f)
        {
            string direction = Direction(at, out int metres);
            Notifications.Show(type, key, new Dictionary<string, string>(3)
            {
                { "name", def.Name.GetLocalization() }, { "metres", metres.ToString() }, { "direction", direction }
            }, seconds);
        }

        /// <summary>"Show me" of the My vehicles panel: the marker points at it for half a minute, and a toast says how far and which way.</summary>
        public void ShowMe(string id)
        {
            BikeRecord record = GarageState.Bike(id);
            if (record == null)
            {
                return;
            }
            BikeDef def = BikeCatalogue.Of(record.Kind);
            ScooterController scooter = Find(id);
            BikeMount mount = scooter != null ? scooter.GetComponent<BikeMount>() : null;
            Vector3 at = scooter != null ? scooter.transform.position : record.Position;
            PointTo(mount, at, def, 30f);
            SayWhere(NotificationType.Info, BuildingManager.IsInsideBuilding ? "reallife_bike_where_indoors" : "reallife_bike_where", def, at);
            Logger?.Info("Two-wheelers: shown " + (mount != null ? mount.Report() : def.Kind + ", not in the world now; last left at " + record.Position.ToString("F1") + "."));
        }

        /// <summary>
        /// "Bring it here" of the My vehicles panel: the two-wheeler is stood in front of the player, who must be
        /// on foot and out of doors. Free from within 25 m (it only has to be wheeled over), otherwise a small fee.
        /// One the game no longer has in the world is made again, the same sort and colour.
        /// </summary>
        public void BringHere(string id)
        {
            BikeRecord record = GarageState.Bike(id);
            TestRefusal why = TestRefusals.Street();
            var gm = InstanceBehavior<GameManager>.Instance;
            if (why == TestRefusal.None && (record == null || !(Settings.Garage && Settings.TwoWheelers)))
            {
                why = TestRefusal.FeatureOff;
            }
            if (why == TestRefusal.None && gm.selectedVehicle != null)
            {
                why = TestRefusal.InVehicle;
            }
            if (why != TestRefusal.None)
            {
                Cannot(why, null);
                return;
            }
            Transform player = gm.playerController.transform;
            if (!BikePlaces.SpotAhead(player, out Vector3 spot, out Quaternion facing))
            {
                Cannot(TestRefusal.NoRoom, null);
                return;
            }
            BikeDef def = BikeCatalogue.Of(record.Kind);
            ScooterController scooter = Find(id);
            BikeMount mount = scooter != null ? scooter.GetComponent<BikeMount>() : null;
            Vector3 was = scooter != null ? scooter.transform.position : record.Position;
            int fee = BringFee(was, player.position);
            if (fee > 0 && !Crime.Pay(fee, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(fee) } }, 7f);
                return;
            }
            if (mount != null)
            {
                // It keeps the height above the ground it stood at (the game has it at rest, and at rest it does not settle).
                float lift = 0.02f;
                if (BikePlaces.StandHeight(was, out float under, out _) && was.y - under > -0.1f && was.y - under < 0.4f
                    && Vector3.Angle(scooter.transform.up, Vector3.up) < 20f)
                {
                    lift = Mathf.Max(0f, was.y - under);
                }
                spot.y += lift;
                Place(scooter, spot, facing);
                record.Position = spot;
                record.Heading = facing.eulerAngles.y;
                Watch(mount, spot, facing);
                Logger?.Info("Two-wheelers: brought to the player from " + was.ToString("F1") + " (" + (fee > 0 ? "fee " + fee : "free") + "): " + mount.Report());
            }
            else
            {
                // Not in the world: the game took its scooter away, or never spawned it. A new one of the same sort, then the old entry goes.
                mount = Spawn(record.Kind, spot, facing, record.Owned, record.Colour);
                if (mount == null)
                {
                    if (fee > 0)
                    {
                        Crime.Give(fee, "reallife_transaction_bike");
                    }
                    Cannot(TestRefusal.NothingFits, "the scooter could not be made or dressed");
                    return;
                }
                mount.Record.Day = record.Day;
                Remove(id);
                Logger?.Info("Two-wheelers: it was not in the world (last left at " + was.ToString("F1") + "); made again in front of the player (" + (fee > 0 ? "fee " + fee : "free") + ").");
            }
            PointTo(mount, spot, def);
            SayWhere(NotificationType.Success, "reallife_bike_brought", def, spot);
        }

        // "Bring it here" is something the player asks for, not a test button: its refusals are said in its own words.
        private static void Cannot(TestRefusal why, string detail)
        {
            Logger?.Info("Two-wheelers: \"Bring it here\" was not done (" + why + (detail != null ? "; " + detail : string.Empty) + ").");
            string key;
            switch (why)
            {
                case TestRefusal.Indoors: key = "reallife_bike_bring_indoors"; break;
                case TestRefusal.TrafficOff: key = "reallife_bike_bring_paused"; break;
                case TestRefusal.FeatureOff: key = "reallife_bike_bring_off"; break;
                case TestRefusal.InVehicle: key = "reallife_bike_bring_in_vehicle"; break;
                case TestRefusal.NoRoom: key = "reallife_bike_bring_no_room"; break;
                case TestRefusal.NotLoaded: key = "reallife_bike_bring_loading"; break;
                default: key = "reallife_bike_bring_failed"; break;
            }
            Notifications.Show(NotificationType.Warning, key, null, 8f);
        }

        /// <summary>What "Bring it here" costs from where a two-wheeler stands.</summary>
        public static int BringFee(Vector3 bike, Vector3 player)
        {
            return CitySpots.Flat(bike - player) < BringFreeWithin * BringFreeWithin ? 0 : BikeCatalogue.BringFee;
        }

        // Stands a scooter somewhere else, at rest, the way the game leaves one: its body, its transform, its place in the save.
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

        // A second look at one just made or moved, two seconds on: the log line that says whether it stayed.
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
                Logger?.Warn("Two-wheelers: the two-wheeler stood at " + _watchSpot.ToString("F1") + " two seconds ago is no longer in the world: the game removed it.");
                return;
            }
            if (mount.Ridden)
            {
                _watch = null;   // somebody is on it: where it is now is their doing
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
            Logger?.Warn("Two-wheelers: two seconds on it is NOT where it was put (" + _watchSpot.ToString("F2") + "): " + mount.Report());
            if (_watchRetried)
            {
                _watch = null;
                return;
            }
            // Once: stood back where it was meant to be and held there, as the game holds a scooter nobody is on (getting on lets it go).
            _watchRetried = true;
            Place(mount.Scooter, _watchSpot + Vector3.up * 0.05f, _watchFacing);
            mount.Scooter.SetFreeze(true);
            _watchAt = Time.time + 2f;
            Logger?.Warn("Two-wheelers: stood back where it was put and held there.");
        }

        // ---------------------------------------------------------------- four times a second: the game's panel at a place

        private const float PanelUp = 1.9f;   // metres over the place the panel stands

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

        // The game's panel over the place: written when the place or what it offers changes, asked for while the player stands there.
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
                string title = GarageText.T(place.Kind == BikePlaceKind.Dock ? "reallife_bike_place_dock" : place.Kind == BikePlaceKind.BicycleShop ? "reallife_bike_place_rack" : "reallife_bike_place_stand");
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
                    _world.Lines(title, GarageText.T(place.Kind == BikePlaceKind.BicycleShop ? "reallife_bike_panel_bicycles" : "reallife_bike_panel_motorbikes"), GarageText.T("reallife_bike_line_sale"), WorldPanel.Green());
                    _world.Buttons("reallife_bike_button_browse", _onUse, true, null, null, false);
                }
            }
            // Over the place; over the player's own head while the place is behind the camera.
            Vector3 over = place.Position + Vector3.up * PanelUp;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = player + Vector3.up * 2.1f;
            }
            _world.Position = over;
            int shown = _world.Show() ? 1 : 2;
            if (shown != _worldState)
            {
                _worldState = shown;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (place panel): at a " + place.Kind + ", " + Mathf.Sqrt(CitySpots.Flat(place.Position - player)).ToString("F1") + " m from it (offered within " + BikePlaces.Reach.ToString("F1") + " m): "
                                 + (shown == 1 ? "the game's own panel is up with '" + (state == 1 ? "Hire" : state == 2 ? "Return" : "Browse") + "' on the game's interact key: PASS"
                                     : "FAIL (the game's panel is showing another place; it is tried again while the player stands here)") + ".");
                }
            }
        }

        // The panel's button, on the game's interact key or a click.
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

        // ---------------------------------------------------------------- once a second

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

                // Scooters the game has spawned that are ours: dressed when the option is on, undressed when it is off. Off, once
                // a walk has left none dressed, the game's later scooters have nothing of the mod's on them: no walk after that.
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
                            _selected = null;   // the player is already on it (a save made in the saddle): seat them on the next frame
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
                        dressed = true;   // (ridden: undressed at a later second)
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

        // ---------------------------------------------------------------- making and removing

        /// <summary>A new two-wheeler at a place, made the way the game makes a scooter. Null if it could not be made.</summary>
        public BikeMount Spawn(BikeKind kind, Vector3 position, Quaternion rotation, bool owned, int colour = -1)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null || VehicleTypeHelper.GetVehicleType(ScooterType) == null)
            {
                return null;
            }
            // To the game a parked vehicle of the player's is in a public space and is billed for it by the hour, and ticketed
            // where that is not allowed: both under the name "electric scooter". A bicycle on the pavement owes nobody anything.
            var instance = new VehicleInstance(ScooterType) { parkingState = ParkingState.Undefined };
            var scooter = VehicleHelper.CreateAndSpawnVehicle(instance, position, rotation) as ScooterController;
            if (scooter == null)
            {
                // Not what was expected of the scooter's prefab: nothing of ours is left behind.
                save.VehicleInstances.Remove(instance);
                Logger?.Warn("Two-wheelers: the game's scooter prefab has no ScooterController; no two-wheeler was made.");
                return null;
            }
            var record = new BikeRecord
            {
                Id = instance.id, Kind = kind, Owned = owned, Position = position, Heading = rotation.eulerAngles.y, Day = TimeHelper.CurrentDay, Colour = colour
            };
            GarageState.Bikes[instance.id] = record;
            GarageState.Touch();
            if (scooter.vehicleCollider != null)
            {
                scooter.vehicleCollider.gameObject.layer = LayerHelper.InteractiveItemsLayerIndex;   // as the game leaves a parked scooter: one that can be walked up to and used
            }
            BikeMount mount = BikeMount.Attach(scooter, record);
            if (mount != null)
            {
                Logger?.Info("Two-wheelers: made" + (owned ? " (owned): " : " (hired): ") + mount.Report());
                Watch(mount, position, rotation);
            }
            else
            {
                Logger?.Info("Two-wheelers: " + kind + (owned ? " (owned)" : " (hired)") + " made at " + position.ToString("F2") + ", but it could not be dressed: it is a scooter.");
            }
            return mount;
        }

        /// <summary>Takes a two-wheeler out of the world and the save, the game's own way.</summary>
        public void Remove(string id)
        {
            GameInstance save = SaveGameManager.Current;
            GarageState.Bikes.Remove(id);
            GarageState.Touch();
            if (save == null || save.VehicleInstances == null)
            {
                return;
            }
            VehicleInstance instance = null;
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                if (save.VehicleInstances[i] != null && save.VehicleInstances[i].id == id)
                {
                    instance = save.VehicleInstances[i];
                    break;
                }
            }
            if (instance == null)
            {
                return;
            }
            VehicleController controller = VehicleHelper.GetVehicleController(instance);
            if (controller != null && save.ActiveVehicleId == id)
            {
                controller.ExitVehicle();
            }
            OverlayGuard.Release(controller);
            instance.Delete(controller);
        }

        // An owned two-wheeler whose scooter the game took away (the save was played without the mod): back where it stood.
        private void RestoreLost(GameInstance save)
        {
            _scratch.Clear();
            foreach (KeyValuePair<string, BikeRecord> pair in GarageState.Bikes)
            {
                if (pair.Value.Owned && !Has(save, pair.Key))
                {
                    _scratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < _scratch.Count && i < 6; i++)
            {
                BikeRecord old = GarageState.Bikes[_scratch[i]];
                GarageState.Bikes.Remove(_scratch[i]);
                if (old.Position.sqrMagnitude < 1f)
                {
                    continue;   // never placed
                }
                BikeMount mount = Spawn(old.Kind, old.Position, Quaternion.Euler(0f, old.Heading, 0f), true, old.Colour);
                if (mount != null)
                {
                    WarnIfWalled(mount);
                }
                Logger?.Info("Two-wheelers: an owned " + old.Kind + " was missing from the save and " + (mount != null ? "has been stood back where it was left." : "could not be put back."));
            }
            _scratch.Clear();
        }

        // An owned one found standing behind a building's wall when a save loads (a version of the mod that handed bikes over
        // to the side of the stand could put one behind the shopfront): said once per load, with the way out.
        private void WarnIfWalled(BikeMount mount)
        {
            Vector3 at = mount.transform.position;
            if (mount.Record == null || !mount.Record.Owned || BuildingManager.IsInsideBuilding || !BikePlaces.Walled(at))
            {
                return;
            }
            Logger?.Warn("Two-wheelers: an owned one stands behind a building's wall, where it cannot be walked up to: " + mount.Report());
            Notifications.Show(NotificationType.Warning, "reallife_bike_walled", new Dictionary<string, string>(2)
            {
                { "name", mount.Def.Name.GetLocalization() }, { "place", BikePlaces.Whereabouts(at) }
            }, 14f);
        }

        // A hired bicycle is the player's until the end of the day after it was taken. Then it is collected, with a late fee.
        private void CollectHires(GameInstance save, int day)
        {
            _scratch.Clear();
            foreach (KeyValuePair<string, BikeRecord> pair in GarageState.Bikes)
            {
                if (!pair.Value.Owned && day > pair.Value.Day + 1 && save.ActiveVehicleId != pair.Key)
                {
                    _scratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                Remove(_scratch[i]);
                Crime.Pay(BikeCatalogue.LateFee, "reallife_transaction_bike");
                Notifications.Show(NotificationType.Warning, "reallife_bike_collected",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(BikeCatalogue.LateFee) } }, 9f);
            }
            _scratch.Clear();
        }

        private static bool Has(GameInstance save, string id)
        {
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                if (save.VehicleInstances[i] != null && save.VehicleInstances[i].id == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Where every two-wheeler stands now, written into its record; called while the game saves.</summary>
        public void RememberPositions()
        {
            // The game takes a scooter away four hours after it was last looked at, and tests that the moment it
            // spawns one from a save. Ours are saved as "in view": with the mod they stay; without it the game
            // starts its own clock the first time one goes out of sight, and clears it away as it would any scooter.
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.VehicleInstances != null)
            {
                for (int i = 0; i < save.VehicleInstances.Count; i++)
                {
                    VehicleInstance instance = save.VehicleInstances[i];
                    if (instance != null && GarageState.Bike(instance.id) != null)
                    {
                        instance.lastSeen = null;
                    }
                }
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is ScooterController scooter && scooter.vehicleInstance != null)
                {
                    BikeRecord record = GarageState.Bike(scooter.vehicleInstance.id);
                    if (record != null)
                    {
                        record.Position = scooter.transform.position;
                        record.Heading = scooter.transform.eulerAngles.y;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- at a place

        private void Hire(BikePlace place)
        {
            if (!BikePlaces.HandOverSpot(place, out Vector3 spot))
            {
                Notifications.Show(NotificationType.Warning, "reallife_bike_no_room", null, 7f);
                return;
            }
            if (!Crime.Pay(BikeCatalogue.DockFee, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(BikeCatalogue.DockFee) } }, 7f);
                return;
            }
            BikeMount mount = Spawn(BikeKind.CityBicycle, spot, place.Rotation, false, 0x1E88E5);
            if (mount == null)
            {
                Crime.Give(BikeCatalogue.DockFee, "reallife_transaction_bike");   // nothing was handed over
                return;
            }
            PointTo(mount, spot, mount.Def);
            SayWhere(NotificationType.Success, "reallife_bike_hired_where", mount.Def, spot);
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

        // What a rack or a stand sells, in the game's list: a name, what it does and costs, and a button.
        private void OpenPanel(BikePlace place)
        {
            float began = Time.realtimeSinceStartup;
            TravelList list = GarageList.Take(this, false);
            if (list == null)
            {
                return;   // (the game's window could not be copied: one "Game UI:" line says why)
            }
            bool motor = place.Kind == BikePlaceKind.MotorbikeStand;
            _panelPlace = place;
            _acts.Clear();
            list.Begin(GarageText.T(motor ? "reallife_bike_panel_motorbikes" : "reallife_bike_panel_bicycles"), GarageText.T(motor ? "reallife_bike_list_info_motor" : "reallife_bike_list_info_bicycle"));
            foreach (BikeDef def in BikeCatalogue.All)
            {
                if (def.Motor != motor)
                {
                    continue;
                }
                BikeDef model = def;
                string facts = def.Motor
                    ? GarageText.T("reallife_bike_facts_motor", "seconds", def.Seconds.ToString("F1"), "top", def.TopKmh.ToString("F0"))
                    : GarageText.T("reallife_bike_facts_bicycle", "speed", def.CruiseKmh.ToString("F0"));
                Row(list, GarageText.T(def.Name), GarageText.T("reallife_bike_list_price", "price", GarageText.Money(def.Cost)) + " " + facts, "reallife_bike_button_buy", true, () => Buy(model));
            }
            if (motor)
            {
                Row(list, GarageText.T("reallife_bike_helmet"), GarageState.Helmet ? GarageText.T("reallife_bike_list_helmet_owned")
                        : GarageText.T("reallife_bike_list_price", "price", GarageText.Money(BikeCatalogue.HelmetPrice)) + " " + GarageText.T("reallife_bike_helmet_note"),
                    "reallife_bike_button_buy", !GarageState.Helmet, BuyHelmet);
            }
            // One of the player's own of this sort standing by: it can be sold here.
            BikeMount near = OwnedNear(place.Position, motor, 12f);
            if (near != null)
            {
                int offer = Mathf.RoundToInt(near.Def.Cost * GarageCatalogue.Resale);
                string id = near.Record.Id;
                Row(list, GarageText.T(near.Def.Name), GarageText.T("reallife_bike_sell", "name", GarageText.T(near.Def.Name), "price", GarageText.Money(offer)), "reallife_bike_button_sell", true, () => Sell(id, offer));
            }
            _listClosing = null;
            list.Open(_onPick, _onListClosed);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (stand list): the game's list is open at a " + place.Kind + " with " + _acts.Count + " entries" + (near != null ? " (one of them sells the player's own standing by)" : string.Empty) + "; "
                             + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
        }

        private void Row(TravelList list, string name, string detail, string button, bool enabled, Action act)
        {
            _acts.Add(act);
            list.Add(_acts.Count - 1, name, detail, button, enabled);
        }

        private void Picked(int id)
        {
            try
            {
                if (id >= 0 && id < _acts.Count && _acts[id] != null)
                {
                    _acts[id]();
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: that did not work. " + e);
                ClosePanel();
            }
        }

        private void ListClosed()
        {
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (stand list): closed (" + (_listClosing ?? "cancelled by the player, or another list of the Garage was opened") + ").");
            }
            _listClosing = null;
            _panelPlace = null;
            _acts.Clear();
        }

        public void ClosePanel()
        {
            if (PanelOpen)
            {
                GarageList.Close();
            }
            _panelPlace = null;
        }

        private void Buy(BikeDef def)
        {
            BikePlace place = _panelPlace;
            if (place == null)
            {
                return;
            }
            if (!BikePlaces.HandOverSpot(place, out Vector3 spot))
            {
                Notifications.Show(NotificationType.Warning, "reallife_bike_no_room", null, 7f);
                return;
            }
            int cost = def.Cost;
            if (!Crime.Pay(cost, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(cost) } }, 7f);
                return;
            }
            BikeMount mount = Spawn(def.Kind, spot, place.Rotation, true);
            if (mount == null)
            {
                Crime.Give(cost, "reallife_transaction_bike");
                return;
            }
            _listClosing = "a " + def.Kind + " was bought";
            ClosePanel();
            PointTo(mount, spot, def);
            SayWhere(NotificationType.Success, def.Motor && !GarageState.Helmet ? "reallife_bike_bought_where_no_helmet" : "reallife_bike_bought_where", def, spot, 12f);
            if (def.Motor && !GarageBridges.Licensed())
            {
                Notifications.Show(NotificationType.Warning, "reallife_bike_no_licence", null, 12f);   // the sale is made; the dealer says what riding it needs
            }
        }

        private void BuyHelmet()
        {
            if (GarageState.Helmet)
            {
                return;
            }
            if (!Crime.Pay(BikeCatalogue.HelmetPrice, "reallife_transaction_bike"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(BikeCatalogue.HelmetPrice) } }, 7f);
                return;
            }
            GarageState.Helmet = true;
            GarageState.Touch();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (choice): a helmet was bought for " + BikeCatalogue.HelmetPrice + ".");
            }
            BikePlace place = _panelPlace;
            if (place != null)
            {
                OpenPanel(place);   // the entry now says it is owned
            }
        }

        private void Sell(string id, int offer)
        {
            BikeRecord record = GarageState.Bike(id);
            if (record == null || !record.Owned)
            {
                return;
            }
            Remove(id);
            Crime.Give(offer, "reallife_transaction_bike_sale");
            Notifications.Show(NotificationType.Success, "reallife_bike_sold", new Dictionary<string, string>(1) { { "amount", GarageText.Money(offer) } }, 7f);
            _listClosing = "a two-wheeler was sold";
            ClosePanel();
        }

        private static BikeMount OwnedNear(Vector3 from, bool motor, float within)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                BikeMount mount = all[i] != null ? all[i].GetComponent<BikeMount>() : null;
                if (mount != null && mount.Record != null && mount.Record.Owned && mount.Def.Motor == motor && !mount.Ridden
                    && (mount.transform.position - from).sqrMagnitude < within * within)
                {
                    return mount;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the test buttons

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
            // 2.2 m in front of the player, on the ground under that point, side on (so the player sees what it is), in the open.
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

        /// <summary>Everything this has on screen, off: the game's panel at a place, the stand's list, the marker. The two-wheelers stay.</summary>
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
