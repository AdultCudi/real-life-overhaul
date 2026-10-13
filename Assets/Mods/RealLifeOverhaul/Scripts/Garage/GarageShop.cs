using System;
using System.Collections.Generic;
using BAModAPI;
using Cinemachine;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Load;
using UI.MiniMenu;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The custom shop. It is at every repair bay the game has: the bays of its auto repair shops and truck
    /// garages (the game's own <c>GasStationTrigger</c>s marked as repair stations, found once per city
    /// load). Drive a car of your own into a bay, stop, and the "Garage" button of the game's own panel
    /// there (<see cref="GarageBay"/>) lists the custom shop. In it: the mod's own camera on a turntable round
    /// the car (drag to turn, wheel to move in and out), and the game's list window left of the middle, the car to its right
    /// (<see cref="GarageList"/>, a copy of the window the game lists its subway stations in) with what can
    /// be done to the car, page by page: paint, finish, lights, body, parts. Every choice is shown on the
    /// car as it is picked; the lines under the headline say what the visit costs and what it does to the
    /// car's figures; the first entry pays the one bill. A colour of one's own is mixed in the game's own
    /// colour picker. Cancel (the yellow button, or the game's cancel key) leaves without paying, which
    /// puts the car back as it came in.
    ///
    /// Work is done at once. The game has no notion of a car that is away being worked on: a car is either
    /// in the save's vehicle list and in the world, or it is not the player's. Keeping one back would mean
    /// hiding or removing a vehicle the base game then cannot account for in a save made meanwhile.
    /// </summary>
    public sealed class GarageShop
    {
        private const int CameraPriority = 200;        // over the mod's chase camera (100) and the game's own (1)
        private const float ViewAside = 0.21f;         // of the camera's distance: puts the car about a sixth of the screen's width right of the middle (lens of 40 degrees)

        private static readonly string[] WheelIds = { "wheel_5spoke", "wheel_mesh", "wheel_steel" };

        private static readonly string[] WheelNames =
        {
            "reallife_garage_wheels_5spoke", "reallife_garage_wheels_mesh", "reallife_garage_wheels_steel"
        };

        private enum Page { Main, Paint, Finish, Lights, Body, Parts }

        private static readonly string[] Families =
        {
            "reallife_garage_list_family_1", "reallife_garage_list_family_2", "reallife_garage_list_family_3",
            "reallife_garage_list_family_4", "reallife_garage_list_family_5", "reallife_garage_list_family_6"
        };

        public static IModLogger Logger;

        private readonly Transform _owner;
        private readonly List<GasStationTrigger> _bays = new List<GasStationTrigger>(24);
        private bool _baysFound;

        // the session
        private VehicleController _car;
        private GarageCar _fitted;
        private GarageBuild _saved;        // what the car came in with
        private GarageBuild _cart;         // what is on it now
        private CarKind _kind;
        private NwhVehicle _held;          // the car's physics, while its pedals are ours
        private bool _autoInput;
        private bool _frozen;
        private Page _page;

        // the view
        private CinemachineVirtualCamera _vcam;
        private float _yaw, _pitch, _distance, _minDistance, _maxDistance;
        private Vector3 _focus;

        // the list
        private readonly List<Action> _acts = new List<Action>(40);   // what each entry's button does, by the entry's id
        private readonly System.Text.StringBuilder _text = new System.Text.StringBuilder(240);
        private readonly System.Text.StringBuilder _info = new System.Text.StringBuilder(320);
        private readonly Action<int> _onPick;
        private readonly Action _onListClosed;
        private readonly Action _toMain, _toPaint, _toFinish, _toLights, _toBody, _toParts, _pay, _stock, _mix;
        private readonly Action<Color> _onMixing, _onMixed;
        private GameObject _dragArea;
        private string _listClosing;
        private int _moneyShown;
        private bool _mixing;
        private bool _mixKept;      // the colour picker's own button was used: the mixed colour stays in the cart
        private int _mixFrom;       // the cart's paint when the picker was opened: put back unless the colour was kept

        private bool _open;

        public GarageShop(Transform owner)
        {
            _owner = owner;
            _onPick = Picked;
            _onListClosed = ListClosed;
            _toMain = () => To(Page.Main);
            _toPaint = () => To(Page.Paint);
            _toFinish = () => To(Page.Finish);
            _toLights = () => To(Page.Lights);
            _toBody = () => To(Page.Body);
            _toParts = () => To(Page.Parts);
            _pay = Pay;
            _stock = Stock;
            _mix = MixOpen;
            _onMixing = Mixing;
            _onMixed = Mixed;
        }

        public bool IsOpen => _open;

        public VehicleController Car => _car;

        /// <summary>What kind of car the model takes a vehicle for (Untouched: not a car the shop works on).</summary>
        public static CarKind KindOf(VehicleController vehicle)
        {
            Vehicles.VehicleTypes.VehicleType type = vehicle != null ? vehicle.vehicleType : null;
            if (type == null)
            {
                return CarKind.Untouched;
            }
            var body = vehicle.GetComponent<Rigidbody>();
            var traits = new CarTraits(type.IsMotorVehicle, type.spawnInPlayerObject, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, body != null ? body.mass : 0f);
            return CarCatalogue.For(type.vehicleTypeName, traits).Kind;
        }

        /// <summary>Why the shop will not take this vehicle, as a locale key; null if it will.</summary>
        public static string Refusal(VehicleController vehicle)
        {
            if (!(vehicle is CarController) || vehicle.vehicleInstance == null || KindOf(vehicle) == CarKind.Untouched)
            {
                return "reallife_garage_refused_not_a_car";
            }
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null || !save.VehicleInstances.Contains(vehicle.vehicleInstance)
                || vehicle.GetComponent<Vehicles.DeliveryDriverJob.DeliveryJobVehicle>() != null
                || GarageState.Bike(vehicle.vehicleInstance.id) != null)
            {
                return "reallife_garage_refused_not_yours";   // a job's van, a hire car
            }
            if (EmpireMotors.Stock(vehicle.vehicleInstance))
            {
                return "reallife_garage_refused_stock";   // Empire Motors prices it as it came, and what was spent would go with the sale (Core/EmpireMotors)
            }
            return null;
        }

        // ---------------------------------------------------------------- the bays

        private void FindBays()
        {
            _baysFound = true;
            _bays.Clear();
            var city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null)
            {
                return;
            }
            int truck = 0;
            foreach (CityBuildingController building in city.cityBuildingControllers)
            {
                GasStationController station = building != null ? building.GetComponent<GasStationController>() : null;
                if (station == null || station.repairStationTriggers == null)
                {
                    continue;
                }
                foreach (GasStationTrigger bay in station.repairStationTriggers)
                {
                    if (bay != null && bay.stationCollider != null)
                    {
                        _bays.Add(bay);
                        truck += bay.isTruckGarage ? 1 : 0;
                    }
                }
            }
            Logger?.Info("Garage: " + _bays.Count + " repair bays found for the custom shop (" + truck + " of them truck garages).");
        }

        /// <summary>The bay a vehicle stands in, or null.</summary>
        private GasStationTrigger BayOf(VehicleController vehicle)
        {
            Vector3 at = vehicle.transform.position;
            for (int i = 0; i < _bays.Count; i++)
            {
                GasStationTrigger bay = _bays[i];
                if (bay == null || bay.stationCollider == null)
                {
                    continue;
                }
                Bounds bounds = bay.stationCollider.bounds;
                if (Mathf.Abs(at.x - bounds.center.x) <= bounds.extents.x && Mathf.Abs(at.z - bounds.center.z) <= bounds.extents.z
                    && Mathf.Abs(at.y - bounds.center.y) <= bounds.extents.y + 2f)
                {
                    return bay;
                }
            }
            return null;
        }

        /// <summary>The repair bays the custom shop works at (looked for now if they have not been), for the city map (World/MapLinks). Read only.</summary>
        public IReadOnlyList<GasStationTrigger> Bays
        {
            get
            {
                if (!_baysFound)
                {
                    FindBays();
                }
                return _bays;
            }
        }

        /// <summary>The nearest bay to a place and how far it is, for the test button's refusal.</summary>
        public float NearestBay(Vector3 from)
        {
            if (!_baysFound)
            {
                FindBays();
            }
            float best = float.MaxValue;
            for (int i = 0; i < _bays.Count; i++)
            {
                if (_bays[i] != null)
                {
                    best = Mathf.Min(best, Vector3.Distance(from, _bays[i].transform.position));
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- four times a second

        /// <summary>Whether an open shop may stay open, and whether the lines under its headline still say what the player has.</summary>
        public void Tick(GameManager gm)
        {
            if (!_open)
            {
                return;
            }
            // The car gone, the option off, the driver out of the seat, the player walked off (the test button opens it on foot), another screen up.
            string why = _car == null ? "the car is gone" : !Settings.Garage ? "the Garage was switched off" : _held != null && gm.selectedVehicle != _car ? "the driver left the seat"
                : LoadScene.isLoading ? "the city is loading"
                : _held == null && gm.playerController != null && (gm.playerController.transform.position - _car.transform.position).sqrMagnitude > 30f * 30f ? "the player walked off"
                : GarageList.MenuUp ? "another screen came up" : null;
            if (why != null)
            {
                _listClosing = why;
                Close(false);
                return;
            }
            if (_mixing && !CustomColorPicker.isOpen)
            {
                _mixing = false;   // the game's colour picker has been closed: the list says what the paint is now
                if (!_mixKept)
                {
                    _cart.Paint = _mixFrom;   // closed without its button (its Cancel, a click beside it, the cancel key): as it was
                    Preview();
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (choice): mixed paint " + (_cart.Paint < 0 ? "factory" : "#" + _cart.Paint.ToString("X6")) + "; this visit " + Bill() + ".");
                }
                Fill();
                return;
            }
            GameInstance save = SaveGameManager.Current;
            if (save != null && Mathf.RoundToInt(save.Money) != _moneyShown && GarageList.OpenFor(this))
            {
                GarageList.Take(this, true).Info(Info());
            }
        }

        // ---------------------------------------------------------------- opening and closing

        public bool Open(VehicleController car)
        {
            if (_open || car == null || Refusal(car) != null)
            {
                return false;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            try
            {
                _open = true;
                _car = car;
                _kind = KindOf(car);
                _fitted = GarageCar.Of(car, true);
                GarageBuild own = GarageState.Of(car.vehicleInstance.id);
                _saved = own != null ? own.Copy() : new GarageBuild();
                _cart = _saved.Copy();
                _page = Page.Main;
                _mixing = false;

                // Seated: the pedals are taken off the keys and the car is held where it stands, so nothing drives it from under the view.
                // Held by its body alone, both pedals up: to the automatic gearbox a brake pedal held at a standstill is "reverse", and
                // in reverse the brake pedal is the throttle, so a pressed brake spun the wheels backwards for the whole visit and the
                // car shot back into whatever stood behind it the moment it was let go.
                var driven = gm != null && gm.selectedVehicle == car ? car as CarController : null;
                if (driven != null && driven.vehicleController != null && driven.vehicleController.input != null)
                {
                    _held = driven.vehicleController;
                    _autoInput = _held.input.autoSetInput;
                    _held.input.autoSetInput = false;
                    _held.input.Throttle = 0f;
                    _held.input.Brakes = 0f;
                    _held.input.Steering = 0f;
                    car.SetFreeze(true);
                    _frozen = true;
                }

                if (GarageList.Take(this, true) == null)
                {
                    Logger?.Warn("Garage: the custom shop has no window this session (the game's list could not be copied; a \"Game UI:\" line above says why).");
                    Close(false);
                    return false;
                }
                View(gm);
                DragArea();
                float began = Time.realtimeSinceStartup;
                Fill();
                Preview();
                Logger?.Info("Garage: custom shop opened on " + car.name + " (" + _kind + ").");
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (custom shop list): the game's list is open left of the middle with " + _acts.Count + " entries (pay, paint, finish, lights, body, parts, all stock); "
                                 + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: the custom shop could not be opened. " + e);
                Close(false);
                return false;
            }
        }

        /// <summary>Every way out. <paramref name="keep"/>: the cart has been paid for and is the car's build now.</summary>
        public void Close(bool keep)
        {
            if (!_open)
            {
                return;
            }
            VehicleController car = _car;
            _open = false;
            _car = null;
            try
            {
                if (_held != null)
                {
                    if (_held.input != null)
                    {
                        _held.input.Throttle = 0f;
                        _held.input.Brakes = 0f;
                        _held.input.autoSetInput = _autoInput;
                    }
                    // Let go standing still and out of gear, whatever the gearbox did meanwhile.
                    if (_held.powertrain != null && _held.powertrain.transmission != null)
                    {
                        _held.powertrain.transmission.ShiftInto(0, true);
                    }
                    Rigidbody body = _held.GetComponent<Rigidbody>();
                    if (body != null && !body.isKinematic)
                    {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    _held = null;
                }
                if (_frozen && car != null)
                {
                    car.SetFreeze(false);
                }
                _frozen = false;
                if (!keep && _fitted != null && _saved != null)
                {
                    _fitted.Show(_saved, Settings.GarageCosmetics, Settings.GarageTuning, GarageBridges.Wet());   // back as it came in
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: the car could not be handed back cleanly. " + e);
            }
            _fitted = null;
            try
            {
                if (_mixing)
                {
                    _mixing = false;
                    UIs uis = InstanceBehavior<UIs>.Instance;
                    if (CustomColorPicker.isOpen && uis != null && uis.customColorPicker != null)
                    {
                        uis.customColorPicker.Close();
                    }
                }
                if (GarageList.OpenFor(this))
                {
                    _listClosing = _listClosing ?? (keep ? "the bill was paid" : "the shop was left");
                    GarageList.Close();
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: the shop's list could not be closed cleanly. " + e);
            }
            _acts.Clear();
            if (_dragArea != null)
            {
                UnityEngine.Object.Destroy(_dragArea);
                _dragArea = null;
            }
            if (_vcam != null)
            {
                _vcam.Priority = -100;
                _vcam.gameObject.SetActive(false);   // the game's own camera has the view again
            }
            GarageState.Touch();   // whoever shows this car looks at it again
            Logger?.Info("Garage: custom shop closed (" + (keep ? "paid" : "no changes") + ").");
        }

        public void Dispose()
        {
            Close(false);
            if (_vcam != null)
            {
                UnityEngine.Object.Destroy(_vcam.gameObject);
                _vcam = null;
            }
        }

        // ---------------------------------------------------------------- the turntable view

        private void View(GameManager gm)
        {
            if (_vcam == null)
            {
                var go = new GameObject("RealLifeOverhaul Garage Camera");
                go.transform.SetParent(_owner, false);
                _vcam = go.AddComponent<CinemachineVirtualCamera>();
                if (gm != null && gm.vehicleCamera is CinemachineVirtualCamera vanilla)
                {
                    _vcam.m_Lens = vanilla.m_Lens;   // keep the game's clip planes
                }
                LensSettings lens = _vcam.m_Lens;
                lens.FieldOfView = 40f;
                _vcam.m_Lens = lens;
            }
            float length = 4.6f;
            float height = 1.5f;
            if (GarageFit.TryGet(_car.vehicleType.vehicleTypeName, out CarFit fit))
            {
                length = fit.Front - fit.Rear;
                height = fit.Top;
            }
            else if (_car.vehicleCollider != null)
            {
                Bounds bounds = _car.vehicleCollider.bounds;
                length = Mathf.Max(bounds.size.x, bounds.size.z);
                height = bounds.size.y;
            }
            _focus = new Vector3(0f, height * 0.45f, 0f);
            _minDistance = length * 0.9f;
            _maxDistance = length * 3f;
            _distance = length * 1.55f;
            _yaw = _car.transform.eulerAngles.y + 145f;   // three-quarter view from the front
            _pitch = 14f;
            _vcam.gameObject.SetActive(true);
            _vcam.Priority = CameraPriority;
            Aim();
        }

        private void Aim()
        {
            if (_vcam == null || _car == null)
            {
                return;
            }
            Vector3 target = _car.transform.TransformPoint(_focus);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            // The camera stands a little to the left of its line to the car, so the car is seen right of the middle: in the part
            // of the screen between the shop's list and the game's phone.
            _vcam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * _distance - rotation * Vector3.right * (_distance * ViewAside), rotation);
        }

        private void Turn(Vector2 pixels)
        {
            _yaw += pixels.x * 0.25f;
            _pitch = Mathf.Clamp(_pitch - pixels.y * 0.2f, 3f, 65f);
            Aim();
        }

        private void Zoom(float notches)
        {
            _distance = Mathf.Clamp(_distance - notches * 0.5f, _minDistance, _maxDistance);
            Aim();
        }

        // The part of the screen the list leaves free: an invisible plate on the HUD's canvas, behind everything else on it.
        // Dragging on it turns the turntable view, the wheel moves it in and out, and no click in the shop reaches the world behind.
        private void DragArea()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform canvas = uis != null && uis.playerHUD != null ? uis.playerHUD.transform : null;
            if (canvas == null || _dragArea != null)
            {
                return;
            }
            var made = new GameObject("RealLifeOverhaul Shop View", typeof(RectTransform));
            made.transform.SetParent(canvas, false);
            var rect = (RectTransform)made.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            made.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var area = made.AddComponent<GarageDragArea>();
            area.Dragged = Turn;
            area.Scrolled = Zoom;
            _dragArea = made;
        }

        // ---------------------------------------------------------------- the list

        // The shop's list, written for the page that is open. On opening and on a click, never otherwise.
        private void Fill()
        {
            TravelList list = GarageList.Take(this, true);
            if (list == null)
            {
                return;
            }
            _acts.Clear();
            string name = _car.vehicleType != null ? _car.vehicleType.vehicleTypeName.GetLocalization() : _car.name;
            list.Begin(GarageText.T("reallife_customshop_title", "car", name), Info());
            if (_page == Page.Main)
            {
                FillMain(list);
            }
            else
            {
                Row(list, GarageText.T("reallife_garage_list_back_name"), GarageText.T("reallife_customshop_total", "amount", GarageText.Money(Bill())), "reallife_garage_list_back", true, _toMain);
                switch (_page)
                {
                    case Page.Paint: FillPaint(list); break;
                    case Page.Finish: FillFinish(list); break;
                    case Page.Lights: FillLights(list); break;
                    case Page.Body: FillBody(list); break;
                    default: FillParts(list); break;
                }
            }
            _listClosing = null;
            list.Open(_onPick, _onListClosed);
        }

        private void Row(TravelList list, string name, string detail, string button, bool enabled, Action act)
        {
            _acts.Add(act);
            list.Add(_acts.Count - 1, name, detail, button, enabled);
        }

        // An entry's button was clicked: its action runs, and the list is written again with what is true now.
        private void Picked(int id)
        {
            try
            {
                if (!_open || id < 0 || id >= _acts.Count || _acts[id] == null)
                {
                    return;
                }
                _acts[id]();
                if (_open)
                {
                    Fill();
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: the custom shop was closed after something unexpected. " + e);
                Close(false);
            }
        }

        // The list was closed: by the Cancel button or the game's cancel key (leaving without paying), or by the shop itself.
        private void ListClosed()
        {
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (custom shop list): closed (" + (_listClosing ?? "cancelled by the player: the car goes back as it came in") + ").");
            }
            _listClosing = null;
            _acts.Clear();
            if (_open)
            {
                Close(false);
            }
        }

        private void To(Page page)
        {
            _page = page;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (custom shop list): page " + page + ", this visit " + Bill() + ".");
            }
        }

        // The lines under the headline: the money, the bill, and what the cart does to the car's figures.
        private string Info()
        {
            GameInstance save = SaveGameManager.Current;
            _moneyShown = save != null ? Mathf.RoundToInt(save.Money) : 0;
            _info.Length = 0;
            _info.Append(GarageText.T("reallife_garage_money", "amount", GarageText.Money(_moneyShown))).Append(". ")
                .Append(GarageText.T("reallife_customshop_total", "amount", GarageText.Money(Bill()))).Append('.');
            string figures = Figures();
            if (figures.Length > 0)
            {
                _info.Append("\n<size=80%>").Append(figures).Append("</size>");
            }
            return _info.ToString();
        }

        private void FillMain(TravelList list)
        {
            int bill = Bill();
            Row(list, bill > 0 ? GarageText.T("reallife_garage_pay", "amount", GarageText.Money(bill)) : GarageText.T("reallife_customshop_done"), GarageText.T("reallife_garage_work_note"),
                bill > 0 ? "reallife_garage_list_pay" : "reallife_customshop_done", true, _pay);

            bool paintable = _fitted.Paintable;
            string paint = !paintable ? GarageText.T("reallife_garage_no_paint")
                : (_cart.Paint < 0 ? GarageText.T("reallife_garage_paint_factory") : "#" + _cart.Paint.ToString("X6")) + ". "
                  + GarageText.T("reallife_garage_paint_prices", "palette", GarageText.Money(GarageCatalogue.PaintPrice(_kind, false)), "custom", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true)));
            Row(list, GarageText.T("reallife_garage_tab_paint"), paint, "reallife_garage_list_choose", paintable, _toPaint);
            Row(list, GarageText.T("reallife_garage_tab_finish"), paintable ? GarageText.T(GarageCatalogue.FinishName(_cart.Finish)) : GarageText.T("reallife_garage_no_paint"),
                "reallife_garage_list_choose", paintable, _toFinish);

            string lights = GarageText.T(GarageCatalogue.LampName(_cart.Lamps));
            if (_cart.Glow >= 0)
            {
                lights += ", " + GarageText.T("reallife_garage_summary_glow");
            }
            Row(list, GarageText.T("reallife_garage_tab_lights"), lights, "reallife_garage_list_choose", true, _toLights);

            _text.Length = 0;
            Fitted(_cart.Spoiler, "reallife_garage_body_spoiler");
            Fitted(_cart.Rack, "reallife_garage_body_rack");
            Fitted(_cart.Bar, "reallife_garage_body_bar");
            for (int i = 0; i < WheelIds.Length; i++)
            {
                Fitted(_cart.Wheels == WheelIds[i], WheelNames[i]);
            }
            Row(list, GarageText.T("reallife_garage_tab_body"), _text.Length > 0 ? _text.ToString() : GarageText.T("reallife_garage_part_stock"), "reallife_garage_list_choose", true, _toBody);

            _text.Length = 0;
            for (int s = 0; s < GarageCatalogue.Slots; s++)
            {
                PartDef part = GarageCatalogue.Part(_cart.Parts[s]);
                if (part != null)
                {
                    Fitted(true, part.Name);
                }
            }
            bool tuning = Settings.GarageTuning;
            Row(list, GarageText.T("reallife_garage_tab_parts"), !tuning ? GarageText.T("reallife_garage_parts_off") : _text.Length > 0 ? _text.ToString() : GarageText.T("reallife_garage_part_stock"),
                "reallife_garage_list_choose", tuning, _toParts);

            Row(list, GarageText.T("reallife_garage_stock"), GarageText.T("reallife_garage_list_stock_detail"), "reallife_garage_list_reset", !_cart.IsStock, _stock);
        }

        private void Fitted(bool fitted, string nameKey)
        {
            if (fitted)
            {
                if (_text.Length > 0)
                {
                    _text.Append(", ");
                }
                _text.Append(GarageText.T(nameKey));
            }
        }

        // The button's word for an option: the one in the cart cannot be chosen again.
        private static string Choose(bool chosen)
        {
            return chosen ? "reallife_garage_list_chosen" : "reallife_garage_list_choose";
        }

        private const int Shades = 7;   // the chart is six families of seven shades, light to dark (GarageCatalogue.Palette)

        // The chart, a family to an entry: its button steps through the family's shades, each shown on the car at once.
        private void FillPaint(TravelList list)
        {
            Row(list, GarageText.T("reallife_garage_paint_factory"), _saved.Paint < 0 ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.PaintPrice(_kind, false)),
                Choose(_cart.Paint < 0), _cart.Paint >= 0, () => PickPaint(-1));
            int[] palette = GarageCatalogue.Palette;
            string price = GarageText.Money(GarageCatalogue.PaintPrice(_kind, false));
            for (int f = 0; f * Shades < palette.Length && f < Families.Length; f++)
            {
                int family = f;
                int first = f * Shades;
                int count = Mathf.Min(Shades, palette.Length - first);
                int at = -1;
                for (int i = 0; i < count; i++)
                {
                    if (palette[first + i] == _cart.Paint)
                    {
                        at = i;
                    }
                }
                // The family's name is written in the shade chosen (the middle one while none is; lightened where it would not show
                // on the dark window): the car is the real sample.
                int sample = palette[first + (at >= 0 ? at : count / 2)];
                Color shown = GarageCar.FromRgb(sample);
                if (shown.grayscale < 0.22f)
                {
                    shown = Color.Lerp(shown, Color.white, 0.4f);
                }
                string detail = at >= 0
                    ? GarageText.T("reallife_garage_list_shade", "n", (at + 1).ToString(), "all", count.ToString()) + " #" + sample.ToString("X6") + ", "
                      + (sample == _saved.Paint ? GarageText.T("reallife_garage_fitted") : price) + "."
                    : GarageText.T("reallife_garage_list_shades", "all", count.ToString(), "price", price);
                int next = palette[first + (at + 1) % count];
                Row(list, "<color=#" + ColorUtility.ToHtmlStringRGB(shown) + ">" + GarageText.T(Families[family]) + "</color>", detail, "reallife_garage_list_next", true, () => PickPaint(next));
            }
            bool mixed = _cart.Paint >= 0 && !GarageCatalogue.InPalette(_cart.Paint);
            Row(list, GarageText.T("reallife_garage_list_mix"), mixed
                    ? "#" + _cart.Paint.ToString("X6") + ". " + GarageText.T("reallife_garage_paint_mixed", "amount", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true)))
                    : GarageText.T("reallife_garage_list_mix_detail", "amount", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true))),
                "reallife_garage_list_open", true, _mix);
        }

        private void PickPaint(int colour)
        {
            _cart.Paint = colour;
            Changed("paint " + (colour < 0 ? "factory" : "#" + colour.ToString("X6")));
        }

        // The game's own colour picker (Canvases/CustomColorPicker, what its interior designer mixes a colour with): the car is
        // repainted as the colour is moved; its button keeps the colour, and closing it any other way puts the paint back.
        private void MixOpen()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            CustomColorPicker picker = uis != null ? uis.customColorPicker : null;
            if (picker == null)
            {
                GameUi.UiClone.Fallback("the game's colour picker is not there, so only the chart's colours can be chosen");
                return;
            }
            _mixFrom = _cart.Paint;
            _mixKept = false;
            picker.onColorPick = _onMixed;
            picker.Open(_onMixing, _cart.Paint >= 0 ? GarageCar.FromRgb(_cart.Paint) : _fitted.StockColour());
            _mixing = true;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (custom shop list): the game's colour picker is open for a mixed colour.");
            }
        }

        private void Mixing(Color colour)
        {
            if (!_open || _cart == null)
            {
                return;
            }
            _cart.Paint = GarageCar.ToRgb(colour);
            Preview();
        }

        // The picker's own button: the colour is kept.
        private void Mixed(Color colour)
        {
            Mixing(colour);
            _mixKept = _open;
        }

        private void FillFinish(TravelList list)
        {
            for (int i = 0; i <= (int)PaintFinish.Pearl; i++)
            {
                var finish = (PaintFinish)i;
                string price = finish == _saved.Finish ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.FinishPrice(finish, _kind));
                Row(list, GarageText.T(GarageCatalogue.FinishName(finish)), i == 0 ? price + ". " + GarageText.T("reallife_garage_finish_note") : price, Choose(finish == _cart.Finish), finish != _cart.Finish, () =>
                {
                    _cart.Finish = finish;
                    Changed("finish " + finish);
                });
            }
        }

        private void FillLights(TravelList list)
        {
            for (int i = 0; i <= (int)LampTint.Yellow; i++)
            {
                var tint = (LampTint)i;
                string price = tint == _saved.Lamps ? GarageText.T("reallife_garage_fitted")
                    : GarageText.Money(tint == LampTint.Stock ? GarageCatalogue.RemovalPrice : GarageCatalogue.LampPrice);
                Row(list, GarageText.T("reallife_garage_lamps_header") + ": " + GarageText.T(GarageCatalogue.LampName(tint)), price, Choose(tint == _cart.Lamps), tint != _cart.Lamps, () =>
                {
                    _cart.Lamps = tint;
                    Changed("lamps " + tint);
                });
            }
            string glow = GarageText.T("reallife_garage_glow_header", "price", GarageText.Money(GarageCatalogue.GlowPrice));
            Row(list, GarageText.T("reallife_garage_summary_glow") + ": " + GarageText.T("reallife_customshop_none"), glow, Choose(_cart.Glow < 0), _cart.Glow >= 0, () =>
            {
                _cart.Glow = -1;
                Changed("underglow off");
            });
            int[] colours = GarageCatalogue.GlowColours;
            for (int i = 0; i < colours.Length; i++)
            {
                int colour = colours[i];
                Row(list, "<color=#" + colour.ToString("X6") + ">" + GarageText.T("reallife_garage_summary_glow") + " " + (i + 1) + "</color>", "#" + colour.ToString("X6") + ". " + GarageText.T("reallife_garage_glow_note"),
                    Choose(colour == _cart.Glow), colour != _cart.Glow, () =>
                    {
                        _cart.Glow = colour;
                        Changed("underglow #" + colour.ToString("X6"));
                    });
            }
        }

        private void FillBody(TravelList list)
        {
            string type = _car.vehicleType != null ? _car.vehicleType.vehicleTypeName : null;
            BodyRow(list, "reallife_garage_body_spoiler", GarageCatalogue.SpoilerPrice, GarageFit.Spoiler(type, _kind), _cart.Spoiler, _saved.Spoiler, () => _cart.Spoiler = !_cart.Spoiler);
            BodyRow(list, "reallife_garage_body_rack", GarageCatalogue.RackPrice, GarageFit.Rack(type, _kind), _cart.Rack, _saved.Rack, () => _cart.Rack = !_cart.Rack);
            BodyRow(list, "reallife_garage_body_bar", GarageCatalogue.BarPrice, GarageFit.Bar(type, _kind), _cart.Bar, _saved.Bar, () => _cart.Bar = !_cart.Bar);

            bool any = false;
            string header = GarageText.T("reallife_garage_wheels_header") + ": ";
            for (int i = 0; i < WheelIds.Length; i++)
            {
                string id = WheelIds[i];
                if (!PropLibrary.TryGet(id, out Mesh mesh, out Material material) || mesh == null || material == null)
                {
                    continue;   // not modelled yet: nothing stands in for a wheel
                }
                if (!any)
                {
                    any = true;
                    Row(list, header + GarageText.T("reallife_garage_wheels_own"), string.Empty, Choose(_cart.Wheels.Length == 0), _cart.Wheels.Length != 0, () =>
                    {
                        _cart.Wheels = string.Empty;
                        Changed("the car's own wheels");
                    });
                }
                string price = id == _saved.Wheels ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.WheelsPrice);
                Row(list, header + GarageText.T(WheelNames[i]), price, Choose(_cart.Wheels == id), _cart.Wheels != id, () =>
                {
                    _cart.Wheels = id;
                    Changed("wheels " + id);
                });
            }
            if (!any)
            {
                Row(list, GarageText.T("reallife_garage_wheels_header"), GarageText.T("reallife_garage_wheels_none"), "reallife_garage_list_choose", false, null);
            }
        }

        private void BodyRow(TravelList list, string nameKey, int price, bool fits, bool inCart, bool fitted, Action toggle)
        {
            if (!fits)
            {
                Row(list, GarageText.T(nameKey), GarageText.T("reallife_garage_body_no_fit", "part", GarageText.T(nameKey)), "reallife_garage_list_fit", false, null);
                return;
            }
            string cost = fitted == inCart ? (fitted ? GarageText.T("reallife_garage_fitted") : GarageText.Money(price))
                : GarageText.Money(inCart ? price : GarageCatalogue.RemovalPrice);
            Row(list, GarageText.T(nameKey), cost, inCart ? "reallife_garage_list_remove" : "reallife_garage_list_fit", true, () =>
            {
                toggle();
                Changed(nameKey + (inCart ? " off" : " on"));
            });
        }

        private void FillParts(TravelList list)
        {
            for (int s = 0; s < GarageCatalogue.Slots; s++)
            {
                int slot = s;
                PartDef part = GarageCatalogue.Part(_cart.Parts[slot]);
                string price = _cart.Parts[slot] == _saved.Parts[slot]
                    ? (part != null ? GarageText.T("reallife_garage_fitted") : string.Empty)
                    : GarageText.Money(part != null ? GarageCatalogue.PartPrice(part, _kind) : GarageCatalogue.RemovalPrice);
                string effects = part != null ? Effects(part) : string.Empty;
                string detail = price.Length > 0 && effects.Length > 0 ? price + ". " + effects : price + effects;
                Row(list, GarageText.T(GarageCatalogue.SlotName((PartSlot)slot)) + ": " + (part != null ? GarageText.T(part.Name) : GarageText.T("reallife_garage_part_stock")), detail,
                    "reallife_garage_list_next", true, () => Cycle(slot, 1));
            }
        }

        // The next part for a slot: stock, then every part of that slot this kind of car can take, and round again.
        private void Cycle(int slot, int step)
        {
            PartDef[] all = GarageCatalogue.Parts;
            int at = -1;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id == _cart.Parts[slot])
                {
                    at = i;
                    break;
                }
            }
            for (int tries = 0; tries <= all.Length; tries++)
            {
                at += step;
                if (at < -1)
                {
                    at = all.Length - 1;
                }
                else if (at >= all.Length)
                {
                    at = -1;
                }
                if (at == -1)
                {
                    _cart.Parts[slot] = null;
                    break;
                }
                PartDef part = all[at];
                if ((int)part.Slot == slot && (!part.UtilityOnly || _kind == CarKind.Utility || _kind == CarKind.Van))
                {
                    _cart.Parts[slot] = part.Id;
                    break;
                }
            }
            Changed((PartSlot)slot + " " + (_cart.Parts[slot] ?? "stock"));
        }

        /// <summary>What a part does and costs to run, in a line: "acceleration +12 %, top speed +4 %, wears 12 % faster".</summary>
        public static string Effects(PartDef part)
        {
            var text = new System.Text.StringBuilder(96);
            Effect(text, part.Accel, "reallife_garage_fx_accel");
            Effect(text, part.Top, "reallife_garage_fx_top");
            Effect(text, part.Brake, "reallife_garage_fx_brake");
            Effect(text, part.Grip, "reallife_garage_fx_grip");
            Effect(text, part.WetGrip, "reallife_garage_fx_wet");
            Effect(text, part.Stiff, "reallife_garage_fx_stiff");
            Effect(text, part.Mass, "reallife_garage_fx_mass");
            if (part.Ride != 0f)
            {
                int cm = Mathf.RoundToInt(part.Ride * 100f);
                Add(text, GarageText.T("reallife_garage_fx_ride", "n", (cm > 0 ? "+" : string.Empty) + cm));
            }
            if (part.Wear > 0f)
            {
                Add(text, GarageText.T("reallife_garage_fx_wear", "n", Mathf.RoundToInt(part.Wear * 100f).ToString()));
            }
            if (part.Insurance > 0f)
            {
                Add(text, GarageText.T("reallife_garage_fx_insurance", "n", Mathf.RoundToInt(part.Insurance * 100f).ToString()));
            }
            if (part.Loud == 1)
            {
                Add(text, GarageText.T("reallife_garage_fx_loud"));
            }
            else if (part.Loud >= 2)
            {
                Add(text, GarageText.T("reallife_garage_fx_illegal"));
            }
            return text.ToString();
        }

        private static void Effect(System.Text.StringBuilder text, float factor, string key)
        {
            if (factor != 1f)
            {
                Add(text, GarageText.T(key, "n", GarageText.Signed(factor)));
            }
        }

        private static void Add(System.Text.StringBuilder text, string piece)
        {
            if (text.Length > 0)
            {
                text.Append(", ");
            }
            text.Append(piece);
        }

        // Something in the cart changed: it is shown on the car. (The list is written again by whoever ran the click.)
        private void Changed(string what)
        {
            Preview();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (choice): " + what + "; this visit " + Bill() + ".");
            }
        }

        // The cart on the car: looks only, with the underglow lit whatever the hour. Handling waits for the bill.
        private void Preview()
        {
            if (_fitted == null)
            {
                return;
            }
            _fitted.Show(_cart, true, false, false);
            _fitted.LightGlow(true);
        }

        private int Bill()
        {
            return GarageCatalogue.Price(_saved, _cart, _kind, _cart.Paint < 0 || GarageCatalogue.InPalette(_cart.Paint));
        }

        // What the cart does to the car's figures, in up to four lines.
        private string Figures()
        {
            var car = _car as CarController;
            GarageFigures before = GarageTuning.Figures(car, _saved);
            GarageFigures after = GarageTuning.Figures(car, _cart);
            GarageModifiers m = _cart.Modifiers();
            _text.Length = 0;
            if (before.To100 > 0f && after.To100 > 0f)
            {
                _text.Append(GarageText.T("reallife_garage_fig_to100", "from", before.To100.ToString("F1"), "to", after.To100.ToString("F1"))).Append('\n');
            }
            if (before.TopKmh > 0f && after.TopKmh > 0f)
            {
                _text.Append(GarageText.T("reallife_garage_fig_top", "from", Mathf.RoundToInt(before.TopKmh).ToString(), "to", Mathf.RoundToInt(after.TopKmh).ToString())).Append('\n');
            }
            if (before.StopMetres > 0f && after.StopMetres > 0f)
            {
                _text.Append(GarageText.T("reallife_garage_fig_stop", "from", Mathf.RoundToInt(before.StopMetres).ToString(), "to", Mathf.RoundToInt(after.StopMetres).ToString())).Append('\n');
            }
            if (m.Wear > 0f || m.Insurance > 0f || m.Loudness >= 2)
            {
                _text.Append(GarageText.T("reallife_garage_fig_costs", "wear", Mathf.RoundToInt(m.Wear * 100f).ToString(), "insurance", Mathf.RoundToInt(m.Insurance * 100f).ToString()));
                if (m.Loudness >= 2)
                {
                    _text.Append(' ').Append(GarageText.T("reallife_garage_fig_illegal"));
                }
            }
            else if (before.To100 < 0f)
            {
                _text.Append(GarageText.T("reallife_garage_fig_none"));
            }
            return _text.ToString().TrimEnd('\n');
        }

        private void Stock()
        {
            int spent = _cart.Spent;
            _cart.Take(new GarageBuild());
            _cart.Spent = spent;
            Changed("all stock");
        }

        private void Pay()
        {
            VehicleController car = _car;
            if (car == null || car.vehicleInstance == null)
            {
                Close(false);
                return;
            }
            int bill = Bill();
            if (bill > 0 && !Crime.Pay(bill, "reallife_transaction_garage"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_cannot_afford", new Dictionary<string, string>(1) { { "amount", GarageText.Money(bill) } }, 7f);
                return;
            }
            // Taking everything off again makes the car stock, and what was spent on it is gone with the parts.
            _cart.Spent = _cart.IsStock ? 0 : _saved.Spent + bill;
            GarageState.Set(car.vehicleInstance.id, _cart.Copy());
            Logger?.Info("Garage: " + car.name + " paid " + bill + " for [" + _cart.Write() + "].");
            if (bill > 0)
            {
                Notifications.Show(NotificationType.Success, "reallife_garage_paid", new Dictionary<string, string>(1) { { "amount", GarageText.Money(bill) } }, 7f);
            }
            Close(true);
        }

        /// <summary>The test button: a full build picked at random and put on a car, free.</summary>
        public static GarageBuild RandomBuild(VehicleController car)
        {
            CarKind kind = KindOf(car);
            string type = car.vehicleType != null ? car.vehicleType.vehicleTypeName : null;
            var build = new GarageBuild
            {
                Paint = GarageCatalogue.Palette[UnityEngine.Random.Range(0, GarageCatalogue.Palette.Length)],
                Finish = (PaintFinish)UnityEngine.Random.Range(0, (int)PaintFinish.Pearl + 1),
                Lamps = (LampTint)UnityEngine.Random.Range(0, (int)LampTint.Yellow + 1),
                Glow = GarageCatalogue.GlowColours[UnityEngine.Random.Range(0, GarageCatalogue.GlowColours.Length)],
                Spoiler = GarageFit.Spoiler(type, kind),
                Rack = GarageFit.Rack(type, kind) && !GarageFit.Spoiler(type, kind),
                Bar = GarageFit.Bar(type, kind)
            };
            for (int slot = 0; slot < GarageCatalogue.Slots; slot++)
            {
                int picked = 0;
                foreach (PartDef part in GarageCatalogue.Parts)
                {
                    if ((int)part.Slot != slot || (part.UtilityOnly && kind != CarKind.Utility && kind != CarKind.Van))
                    {
                        continue;
                    }
                    picked++;
                    if (UnityEngine.Random.Range(0, picked) == 0)
                    {
                        build.Parts[slot] = part.Id;   // each part of the slot equally likely
                    }
                }
            }
            return build;
        }
    }

    /// <summary>
    /// The part of the screen the shop's list leaves free: dragging on it turns the turntable view, the
    /// wheel moves it in and out. An invisible plate that takes the mouse, so clicks in the shop never
    /// reach the world behind it.
    /// </summary>
    public sealed class GarageDragArea : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public Action<Vector2> Dragged;
        public Action<float> Scrolled;

        public void OnDrag(PointerEventData data)
        {
            Dragged?.Invoke(data.delta);
        }

        public void OnScroll(PointerEventData data)
        {
            Scrolled?.Invoke(data.scrollDelta.y);
        }
    }
}
