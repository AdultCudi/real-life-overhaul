using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Cinemachine;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The custom shop at every repair bay: a turntable view of the car and the game's list window with what can be done
    /// to it. Each choice shows on the car at once; leaving without paying puts it back as it came. Work is done at once,
    /// as the game has no notion of a car away being worked on. A compare four times a second while closed.
    /// </summary>
    public sealed partial class GarageShop
    {
        private const int CameraPriority = 200;        // over the mod's chase camera (100) and the game's own (1)
        private const float ViewAside = 0.21f;         // of the camera's distance: the car a sixth of the screen right of the middle (40 degree lens)

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

        private VehicleController _car;
        private GarageCar _fitted;
        private GarageBuild _saved;        // what the car came in with
        private GarageBuild _cart;         // what is on it now
        private CarKind _kind;
        private NwhVehicle _held;          // the car's physics, while its pedals are ours
        private bool _autoInput;
        private bool _frozen;
        private Page _page;

        private CinemachineVirtualCamera _vcam;
        private float _yaw, _pitch, _distance, _minDistance, _maxDistance;
        private Vector3 _focus;

        private readonly List<Action> _acts = new List<Action>(40);   // each entry's button, by the entry's id
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
        private bool _mixKept;      // the colour picker's own button was used: the colour stays in the cart
        private int _mixFrom;       // the cart's paint as the picker opened: put back unless the colour was kept

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
            _onMixing = Guard.Wrap<Color>(Mixing);   // the game's colour picker calls them
            _onMixed = Guard.Wrap<Color>(Mixed);
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
            if (Dealerships.Stock(vehicle.vehicleInstance) || Family.PartnerCar.Is(vehicle.vehicleInstance))
            {
                return "reallife_garage_refused_stock";   // the dealership prices it as it came: what was spent would go with the sale
            }
            return null;
        }

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

        /// <summary>The repair bays the shop works at, for the city map (World/MapLinks); looked for on first use. Read only.</summary>
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

        /// <summary>Four times a second: whether an open shop may stay open, and the money under its headline.</summary>
        public void Tick(GameManager gm)
        {
            if (!_open)
            {
                return;
            }
            // The test button opens it on foot: the player may walk off.
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
                _mixing = false;   // the game's colour picker was closed
                if (!_mixKept)
                {
                    _cart.Paint = _mixFrom;   // closed without its own button: as it was
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

                // The car held with both pedals up: a brake held at a standstill is reverse, where the brake is the throttle.
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
                                 + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
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
                    // Let go standing still and out of gear.
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

        // The list for the page that is open: on opening and on a click, never otherwise.
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

        // By the Cancel button or the game's cancel key (leaving without paying), or by the shop itself.
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
            if (Settings.GarageSounds)
            {
                _info.Append("\n<size=80%>").Append(GarageText.T("reallife_garage_sounds_rev")).Append("</size>");   // the turntable revs on the throttle key
            }
            return _info.ToString();
        }

        // Shown on the car; the list is written again by whoever ran the click.
        private void Changed(string what)
        {
            Preview();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (choice): " + what + "; this visit " + Bill() + ".");
            }
        }

        // Looks only, the underglow lit whatever the hour; handling waits for the bill.
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
            // Everything taken off makes the car stock: what was spent goes with the parts.
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
}
