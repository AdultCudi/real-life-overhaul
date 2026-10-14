using System;
using BAModAPI;
using BigAmbitions.InputSystem;
using BigAmbitions.SoundSystem;
using UI.Notification;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Car Showroom's own camera, and choosing the bay a car stands on, inside one of the player's showrooms.
    ///
    /// LOOKING ROUND A CAR ("Look round it" on the car's page of the app): the game's own camera for looking at a car
    /// (the one its vehicle dealer films a car bought with) goes round the car, close: the game's own camera controls
    /// turn it (the right button held and dragged, as the game's preview of a building turns; the rotate keys a quarter
    /// turn at a time, as its placement camera turns) and the wheel brings it nearer or takes it back, between a car's
    /// length and a few metres, tilted from a little over the bonnet to well above the roof, and always inside the
    /// showroom's walls and under its ceiling (ShowroomFloorRules). The game's cancel key leaves, as it leaves the game's
    /// own placement. The keys are in the game's panel of control hints (ShowroomHints). What another author's showroom
    /// mod offered was a view from the driver's seat while driving; a car on show is not driven, so the showroom's camera
    /// is one for looking a car over, and the driving one stays the chase camera's and that mod's (Camera/EmpireView).
    ///
    /// CHOOSING A BAY ("Choose its bay", or the interact key while looking round it): as the game places an item, the
    /// game is paused and the car picked follows the pointer from bay to bay, the car standing on the bay pointed at
    /// outlined as the one that will make way; a left click puts it there (the two change places), the game's key for
    /// turning an item (or a short right click) turns it round, and the cancel key puts everything back. The floor is
    /// seen from above for it, with the same controls. The choice is the player's for good: it is kept in the save
    /// (Floor/ShowroomBays) and every later layout of the showroom keeps it (DealerGame.Park).
    ///
    /// Out of either, every time and by every way (the cancel key, the phone opened, leaving the showroom, the city
    /// unloading, a fault): the game's camera, walk, pointer and clock are given back as they were (ShowroomGame).
    ///
    /// It never clashes with the mod's other cameras: the third-person camera and the chase camera do nothing while it is
    /// up (ThirdPersonCamera.IsUiOpen counts it), it is never up while driving (when Empire Motors' view and the chase
    /// camera are), and it is one of the game's own cameras at the game's own priority, so the mod's watch for another
    /// author's camera (Camera/CameraYield) sees nothing foreign.
    ///
    /// COST: nothing at all while it is not up (the component is off). While it is up, in the frame: the controls read,
    /// the camera's place worked out and written, and while a bay is chosen the pointer's ray onto the floor's plane (no
    /// physics) and the nearest of at most twelve bays. It runs before the game's own key handling, so the cancel key it
    /// takes does not also open the game's menu (as Core/GameUi/TravelList does).
    ///
    /// Going Public: local only. It works only in a showroom of the player's own, and moves only the player's own cars.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class ShowroomView : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("economy_dealer", "ShowroomView", typeof(ShowroomView));   // its Update (Core/Guard)

        private const float DragShare = 0.01f;   // of the screen's height: a right click that moved less than this is a click (as the game's placement tells them apart)

        public static IModLogger Logger;
        public static ShowroomView Instance { get; private set; }

        private enum Mode { None, Look, Place }

        private static Mode _mode;

        /// <summary>True while the showroom's camera is up (looking round a car, or choosing a bay): the mod's own cameras stand aside.</summary>
        public static bool Active => _mode != Mode.None;

        private DealerYard _yard;
        private StockCar _car;
        private VehicleController _controller;
        private Buildings.Building _building;
        private bool _fromLook;               // the bay is chosen from looking round the car: back to it after
        private Vector3 _aim, _aimWanted;
        private float _yaw, _pitch, _distance, _yawWanted, _pitchWanted, _distanceWanted;
        private float _least, _most, _pitchLeast, _pitchMost;
        private bool _bounded;
        private float _minX, _maxX, _minZ, _maxZ, _floorY;
        private Vector2 _rightFrom, _rightLast;
        private bool _dragging, _dragged;
        private int _bay = -1;                // the bay pointed at
        private bool _turned;
        private int _from;                    // the bay the car stood on
        private StockCar _other;              // the car that would make way
        private Vector3 _lastPointer = new Vector3(-1f, -1f, 0f);
        private bool _told;                   // the keys have been said in a notice this session

        private void Awake()
        {
            Instance = this;
            enabled = false;
            ScenePrewarm.Add("showroom: the game's panel of control hints being found", ShowroomHints.Prewarm);
        }

        private void OnDestroy()
        {
            try
            {
                Stop(false);
                ShowroomHints.Forget();
                ShowroomGame.Forget();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _mode = Mode.None;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- starting

        /// <summary>Looks round a car of the showroom the player stands in. False, with the game's notice saying why, if it cannot be.</summary>
        public bool Look(int carId)
        {
            if (!Pick(carId))
            {
                return false;
            }
            try
            {
                ShowroomGame.CloseMenu();
                if (!StartLook(true))
                {
                    Stop(false);
                    Notifications.ShowError("reallife_showroom_refuse_camera");
                    return false;
                }
                Tell();
                Logger?.Info("Car Showroom: looking round the " + _car.Type + " on bay " + (_car.Spot + 1) + " of '" + _yard.Name + "'.");
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Stop(false);
                return false;
            }
        }

        /// <summary>Chooses the bay of a car of the showroom the player stands in. False, with the game's notice saying why, if it cannot be.</summary>
        public bool Place(int carId)
        {
            if (!Pick(carId))
            {
                return false;
            }
            try
            {
                ShowroomGame.CloseMenu();
                _fromLook = false;
                if (!StartPlace())
                {
                    Stop(false);
                    Notifications.ShowError("reallife_showroom_refuse_camera");
                    return false;
                }
                Tell();
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Stop(false);
                return false;
            }
        }

        // The car asked for, if it can be looked at or moved now: on the floor of the showroom the player stands in.
        private bool Pick(int carId)
        {
            DealerDirector director = DealerDirector.Instance;
            DealerState state = director != null ? director.State : null;
            DealerYard y = null;
            StockCar car = state != null ? state.Car(carId, out y) : null;
            if (car == null)
            {
                Notifications.ShowError("reallife_dealer_refuse_gone");
                return false;
            }
            if (y != director.YardInside())
            {
                Notifications.ShowError("reallife_showroom_refuse_inside");
                return false;
            }
            VehicleController controller = DealerGame.Controller(car.VehicleId);
            if (controller == null || car.Spot < 0 || car.OutUntil > ShowroomGame.Hour)
            {
                Notifications.ShowError("reallife_showroom_refuse_away");
                return false;
            }
            _yard = y;
            _car = car;
            _controller = controller;
            _building = ShowroomGame.Inside;
            return true;
        }

        private bool StartLook(bool fresh)
        {
            ShowroomGame.Body(_controller, out Vector3 middle, out float length);
            ShowroomFloorRules.Reach(length, out _least, out _most);
            _pitchLeast = FloorCatalogue.PitchLeast;
            _pitchMost = FloorCatalogue.PitchMost;
            _bounded = ShowroomGame.FloorBounds(out _minX, out _maxX, out _minZ, out _maxZ, out _floorY);
            _aimWanted = middle;
            float carYaw = _controller.transform.eulerAngles.y;
            _yawWanted = carYaw + FloorCatalogue.YawStart;
            _pitchWanted = FloorCatalogue.PitchStart;
            _distanceWanted = Mathf.Clamp(length * 0.5f + FloorCatalogue.FarStart, _least, _most);
            if (fresh)
            {
                Snap();
            }
            _mode = Mode.Look;
            if (!ShowroomGame.TakeCamera(_aim, Place(_aim, _yaw, _pitch, _distance)))
            {
                return false;
            }
            ShowroomHints.Show(true, false);
            enabled = true;
            return true;
        }

        private bool StartPlace()
        {
            if (!ShowroomGame.FloorMiddle(_yard, out Vector3 middle))
            {
                return false;
            }
            float longer = 0f;
            for (int s = 0; s < _yard.Spots.Count; s++)
            {
                longer = Mathf.Max(longer, Mathf.Abs(_yard.Spots[s].X / 100f - middle.x) * 2f, Mathf.Abs(_yard.Spots[s].Z / 100f - middle.z) * 2f);
            }
            longer += DealerCatalogue.SpotDepth / 100f;
            ShowroomFloorRules.FloorReach(longer, out _least, out _most);
            _pitchLeast = FloorCatalogue.FloorPitchLeast;
            _pitchMost = FloorCatalogue.FloorPitchMost;
            _bounded = false;
            _floorY = middle.y;
            bool wasUp = _mode != Mode.None;
            _aimWanted = middle;
            _yawWanted = wasUp ? _yaw : ShowroomGame.CameraYaw(middle);
            _pitchWanted = FloorCatalogue.FloorPitchStart;
            _distanceWanted = Mathf.Clamp(longer * FloorCatalogue.FloorStart, _least, _most);
            if (!wasUp)
            {
                Snap();
            }
            _mode = Mode.Place;
            if (!ShowroomGame.TakeCamera(_aim, Place(_aim, _yaw, _pitch, _distance)))
            {
                return false;
            }
            DealerDirector director = DealerDirector.Instance;
            _turned = director != null && director.Bays.TurnedOf(_yard.Place, _car.VehicleId);
            _from = _car.Spot;
            _bay = -1;
            _other = null;
            _lastPointer = new Vector3(-1f, -1f, 0f);
            ShowroomGame.Pause();
            ShowroomGame.Sound(SoundType.ObjectPickup, _controller.transform.position);
            ShowroomHints.Show(false, true);
            enabled = true;
            Logger?.Info("Car Showroom: choosing the bay of the " + _car.Type + " (now on bay " + (_from + 1) + " of " + _yard.Spots.Count + ") in '" + _yard.Name + "'.");
            return true;
        }

        // The camera starts where the live one is, near enough, and eases from there.
        private void Snap()
        {
            _aim = _aimWanted;
            _yaw = ShowroomGame.CameraYaw(_aimWanted);
            _pitch = Mathf.Clamp(_pitchWanted + 15f, _pitchLeast, _pitchMost);
            _distance = _most;
        }

        // Once a session, when the game's panel will not show the keys: a notice says them.
        private void Tell()
        {
            if (_told || ShowroomHints.Shown)
            {
                return;
            }
            _told = true;
            Notifications.Show(NotificationType.Info, "reallife_showroom_note_keys", null, 10f);
        }

        // ---------------------------------------------------------------- stopping

        // Back to the game: the bay's car put back unless it was put down, the camera, the walk, the pointer and the clock.
        private void Stop(bool keepLook)
        {
            Mode was = _mode;
            if (was == Mode.Place)
            {
                ShowroomGame.Outline(null);
                ShowroomGame.Unpause();
                DealerDirector.Instance?.ParkAgain(_yard);   // (after a bay was put down this is the layout it made)
            }
            if (keepLook && _car != null && _controller != null && StartLook(false))
            {
                return;   // back to looking round the car, from where the camera is
            }
            _mode = Mode.None;
            ShowroomHints.Show(false, false);
            ShowroomGame.GiveCamera();
            _yard = null;
            _car = null;
            _controller = null;
            _other = null;
            _dragging = false;
            enabled = false;
        }

        // ---------------------------------------------------------------- the frame

        private static readonly int HotUpdate = HotSpots.Slot("Economy: ShowroomView.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                try
                {
                    Stop(false);
                }
                catch (Exception second)
                {
                    Logger?.Error(second);
                }
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_mode == Mode.None)
            {
                enabled = false;
                return;
            }
            if (UI.Load.LoadScene.isLoading || ShowroomGame.Inside != _building || _building == null || _controller == null || UI.Smartphone.FullMenu.IsOpen
                || CityMap.IsOpen || HudConfirm.isOpen)
            {
                Stop(false);   // left the showroom, a screen of the game's opened, or the car went
                return;
            }
            if (PlayerAction.Cancel.Pressed())
            {
                PlayerAction.Cancel.Reset();   // taken, so the game's own handling does not open its menu too
                if (_mode == Mode.Place && _fromLook)
                {
                    Stop(true);
                }
                else
                {
                    Stop(false);
                }
                return;
            }
            float dt = Time.unscaledDeltaTime;
            Turn();
            if (_mode == Mode.Look && PlayerAction.Interact.Pressed())
            {
                PlayerAction.Interact.Reset();
                _fromLook = true;
                if (!StartPlace())
                {
                    Stop(false);
                    return;
                }
            }
            else if (_mode == Mode.Place)
            {
                Choose();
                if (_mode != Mode.Place)
                {
                    return;
                }
            }
            Follow(dt);
        }

        // The game's own camera controls: the right button dragged, the rotate keys a quarter turn, the wheel.
        private void Turn()
        {
            Vector2 pointer = InputActionHelper.GetCursorPosition();
            if (InputActionHelper.WasRightClickPressedThisFrame())
            {
                _dragging = true;
                _dragged = false;
                _rightFrom = pointer;
                _rightLast = pointer;
            }
            if (_dragging && InputActionHelper.IsRightClickBeingPressedThisFrame())
            {
                Vector2 moved = pointer - _rightLast;
                _rightLast = pointer;
                if (!_dragged && (pointer - _rightFrom).sqrMagnitude > Screen.height * DragShare * Screen.height * DragShare)
                {
                    _dragged = true;
                }
                if (_dragged)
                {
                    float sign = CameraControllers.PedestrianCam.invertRotation ? -1f : 1f;
                    _yawWanted += moved.x * FloorCatalogue.DragDegrees * sign;
                    _pitchWanted = Mathf.Clamp(_pitchWanted - moved.y * FloorCatalogue.DragDegrees, _pitchLeast, _pitchMost);
                }
            }
            if (_dragging && InputActionHelper.WasRightClickReleasedThisFrame())
            {
                _dragging = false;
                if (!_dragged && _mode == Mode.Place)
                {
                    TurnCar();   // a short right click turns the item, as in the game's placement
                }
            }
            if (PlayerAction.RotateLeft.Pressed())
            {
                _yawWanted = ShowroomFloorRules.Step(_yawWanted, CameraControllers.PedestrianCam.invertRotation ? 1 : -1);
            }
            if (PlayerAction.RotateRight.Pressed())
            {
                _yawWanted = ShowroomFloorRules.Step(_yawWanted, CameraControllers.PedestrianCam.invertRotation ? -1 : 1);
            }
            float notches = 0f;
            EventSystem events = EventSystem.current;
            if (events == null || !events.IsPointerOverGameObject())
            {
                notches = Input.mouseScrollDelta.y;
            }
            notches += PlayerAction.Zoom.Value() * 0.5f;
            if (notches != 0f)
            {
                _distanceWanted = ShowroomFloorRules.Zoom(_distanceWanted, notches, _least, _most);
            }
        }

        // The camera eased towards what the controls ask, kept inside the showroom round a car, and written.
        private void Follow(float dt)
        {
            _aim = Vector3.Lerp(_aim, _aimWanted, 1f - Mathf.Exp(-FloorCatalogue.Ease * dt));
            _yaw = ShowroomFloorRules.EaseAngle(_yaw, _yawWanted, dt);
            _pitch = ShowroomFloorRules.Ease(_pitch, _pitchWanted, dt);
            _distance = ShowroomFloorRules.Ease(_distance, _distanceWanted, dt);
            ShowroomGame.MoveCamera(_aim, Place(_aim, _yaw, _pitch, _distance));
        }

        private Vector3 Place(Vector3 aim, float yaw, float pitch, float distance)
        {
            ShowroomFloorRules.Orbit(yaw, pitch, distance, out float x, out float y, out float z);
            float px = aim.x + x, py = aim.y + y, pz = aim.z + z;
            if (_mode == Mode.Look && _bounded)
            {
                ShowroomFloorRules.KeepInside(aim.x, aim.z, ref px, ref py, ref pz, _floorY, _minX, _maxX, _minZ, _maxZ);
            }
            return new Vector3(px, py, pz);
        }

        // ---------------------------------------------------------------- choosing the bay

        private void Choose()
        {
            Vector3 pointer = Input.mousePosition;
            if (pointer != _lastPointer)
            {
                _lastPointer = pointer;
                if (ShowroomGame.Pointed(_floorY, out Vector3 at))
                {
                    int bay = ShowroomFloorRules.Nearest(_yard.Spots, Mathf.RoundToInt(at.x * 100f), Mathf.RoundToInt(at.z * 100f));
                    if (bay != _bay && bay >= 0)
                    {
                        Point(bay);
                    }
                }
            }
            if (PlayerAction.RotateItem.Pressed())
            {
                TurnCar();
            }
            EventSystem events = EventSystem.current;
            if (_bay >= 0 && InputActionHelper.WasLeftClickPressedThisFrame() && (events == null || !events.IsPointerOverGameObject()))
            {
                PutDown();
            }
        }

        // The car shown on the bay pointed at, and the car there shown on the car's own bay, outlined.
        private void Point(int bay)
        {
            DealerDirector director = DealerDirector.Instance;
            if (_other != null)
            {
                Show(_other, _other.Spot, director != null && director.Bays.TurnedOf(_yard.Place, _other.VehicleId));   // back where it stands
            }
            _bay = bay;
            _other = null;
            for (int c = 0; c < _yard.Cars.Count; c++)
            {
                if (_yard.Cars[c] != _car && _yard.Cars[c].Spot == bay)
                {
                    _other = _yard.Cars[c];
                    break;
                }
            }
            Show(_car, bay, _turned);
            VehicleController other = null;
            if (_other != null && _from >= 0)
            {
                Show(_other, _from, director != null && director.Bays.TurnedOf(_yard.Place, _other.VehicleId));
                other = DealerGame.Controller(_other.VehicleId);
            }
            ShowroomGame.Outline(other);
        }

        private void Show(StockCar car, int bay, bool turned)
        {
            if (bay < 0 || bay >= _yard.Spots.Count)
            {
                return;
            }
            VehicleController c = car == _car ? _controller : DealerGame.Controller(car.VehicleId);
            LotSpot s = _yard.Spots[bay];
            ShowroomGame.ShowAt(c, DealerGame.At(s), DealerGame.Facing(s, turned));
        }

        private void TurnCar()
        {
            _turned = !_turned;
            if (_bay >= 0)
            {
                Show(_car, _bay, _turned);
            }
            else
            {
                Show(_car, _from, _turned);
            }
            ShowroomGame.Sound(SoundType.ObjectRotate, _controller.transform.position);
        }

        private void PutDown()
        {
            DealerDirector director = DealerDirector.Instance;
            int bay = _bay;
            bool put = director != null && director.PutOnBay(_car.Id, bay, _turned);
            ShowroomGame.Outline(null);
            if (put)
            {
                ShowroomGame.Sound(SoundType.ObjectPutDown, _controller.transform.position);
                ShowroomFloor.Instance?.CarsMoved();
            }
            Stop(_fromLook);
        }
    }
}
