using System;
using System.Collections.Generic;
using BAModAPI;
using Cinemachine;
using Helpers;
using Localizor;
using TMPro;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The custom shop. It is at every repair bay the game has: the bays of its auto repair shops and truck
    /// garages (the game's own <c>GasStationTrigger</c>s marked as repair stations, found once per city
    /// load). Drive a car of your own into a bay, stop, and the Garage key opens the workshop: the mod's own
    /// camera on a turntable round the car (drag to turn, wheel to move in and out), a panel of what can be
    /// done to it, every choice shown on the car as it is picked, what it costs, what it does to the car's
    /// figures, and one bill at the end. Leaving without paying puts the car back as it came in.
    ///
    /// Work is done at once. The game has no notion of a car that is away being worked on: a car is either
    /// in the save's vehicle list and in the world, or it is not the player's. Keeping one back would mean
    /// hiding or removing a vehicle the base game then cannot account for in a save made meanwhile.
    /// </summary>
    public sealed class GarageShop
    {
        private const int CameraPriority = 200;        // over the mod's chase camera (100) and the game's own (1)
        private const float BayStandingSpeed = 1.5f;   // m/s, as the game's vehicles report their speed (rounded): standing, or rolling to a stop

        private static readonly string[] WheelIds = { "wheel_5spoke", "wheel_mesh", "wheel_steel" };

        private static readonly string[] WheelNames =
        {
            "reallife_garage_wheels_5spoke", "reallife_garage_wheels_mesh", "reallife_garage_wheels_steel"
        };

        private enum Tab { Paint, Finish, Lights, Body, Parts }

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
        private Tab _tab;

        // the view
        private CinemachineVirtualCamera _vcam;
        private float _yaw, _pitch, _distance, _minDistance, _maxDistance;
        private Vector3 _focus;

        // the panel
        private GameObject _panel;
        private GameObject _dragArea;
        private Transform _content;
        private TextMeshProUGUI _money, _figures, _total, _customPrice;
        private TextMeshProUGUI _payLabel;
        private Image _customSwatch;
        private float _h, _s, _v;

        private bool _promptOn;
        private bool _open;

        public GarageShop(Transform owner)
        {
            _owner = owner;
        }

        public bool IsOpen => _open;

        public bool PromptOn => _promptOn;

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

        /// <summary>Looks whether the player has stopped in a bay (shows the prompt), and whether an open shop may stay open.</summary>
        public void Tick(GameManager gm)
        {
            if (_open)
            {
                // The car gone, the option off, the driver out of the seat, the player walked off (the test button opens it on foot).
                if (_car == null || !Settings.Garage || (_held != null && gm.selectedVehicle != _car) || LoadScene.isLoading
                    || (_held == null && gm.playerController != null && (gm.playerController.transform.position - _car.transform.position).sqrMagnitude > 30f * 30f))
                {
                    Close(false);
                }
                else if (_money != null)
                {
                    RefreshMoney();
                }
                SetPrompt(false);
                return;
            }
            VehicleController vehicle = gm.selectedVehicle;
            bool here = false;
            if (Settings.Garage && vehicle is CarController && vehicle.CurrentSpeed <= BayStandingSpeed && !ThirdPersonCamera.IsUiOpen())
            {
                if (!_baysFound)
                {
                    FindBays();
                }
                here = _bays.Count > 0 && BayOf(vehicle) != null && Refusal(vehicle) == null;
            }
            SetPrompt(here);
        }

        private void SetPrompt(bool on)
        {
            if (on == _promptOn)
            {
                return;
            }
            _promptOn = on;
            if (on || GarageUi.Exists)
            {
                GarageDirector.Prompt(this, on ? GarageText.T("reallife_garage_prompt_shop") : null);
            }
        }

        /// <summary>The Garage key, while the prompt is up.</summary>
        public void Use(GameManager gm)
        {
            if (_promptOn && !_open && gm.selectedVehicle != null)
            {
                Open(gm.selectedVehicle);
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
                _tab = Tab.Paint;
                Color.RGBToHSV(_cart.Paint >= 0 ? GarageCar.FromRgb(_cart.Paint) : _fitted.StockColour(), out _h, out _s, out _v);

                // Seated: the pedals are taken off the keys and the car is held where it stands, so nothing drives it from under the view.
                var driven = gm != null && gm.selectedVehicle == car ? car as CarController : null;
                if (driven != null && driven.vehicleController != null && driven.vehicleController.input != null)
                {
                    _held = driven.vehicleController;
                    _autoInput = _held.input.autoSetInput;
                    _held.input.autoSetInput = false;
                    _held.input.Throttle = 0f;
                    _held.input.Brakes = 1f;
                    _held.input.Steering = 0f;
                    car.SetFreeze(true);
                    _frozen = true;
                }

                View(gm);
                Build();
                Preview();
                SetPrompt(false);
                Logger?.Info("Garage: custom shop opened on " + car.name + " (" + _kind + ").");
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
            if (!_open && _panel == null)
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
                        _held.input.Brakes = 0f;
                        _held.input.autoSetInput = _autoInput;
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
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
                GarageUi.GiveCursor();
            }
            if (_dragArea != null)
            {
                UnityEngine.Object.Destroy(_dragArea);
                _dragArea = null;
            }
            _content = null;
            _money = _figures = _total = _customPrice = _payLabel = null;
            _customSwatch = null;
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
            _vcam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * _distance, rotation);
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

        // ---------------------------------------------------------------- the panel

        private void Build()
        {
            GarageUi ui = GarageUi.Instance;

            RectTransform drag = ui.Box("Shop View", ui.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            GarageUi.Stretch(drag, 0f);
            var area = drag.gameObject.AddComponent<GarageDragArea>();
            area.Dragged = Turn;
            area.Scrolled = Zoom;
            _dragArea = drag.gameObject;

            RectTransform panel = ui.Box("Shop", ui.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -45f), new Vector2(560f, 780f), GarageUi.Plate);
            _panel = panel.gameObject;
            GarageUi.TakeCursor();   // given back in Close, which looks for the panel
            string name = _car.vehicleType != null ? _car.vehicleType.vehicleTypeName.GetLocalization() : _car.name;
            ui.TextAt(panel, 16f, 10f, 528f, 32f, 26f, Color.white, GarageText.T("reallife_customshop_title", "car", name));
            _money = ui.TextAt(panel, 16f, 44f, 528f, 24f, 18f, GarageUi.Dim, string.Empty);

            Tabs(panel);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(panel, false);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -118f);
            rect.sizeDelta = new Vector2(540f, 420f);
            _content = content.transform;

            _figures = ui.TextAt(panel, 16f, 546f, 528f, 72f, 17f, GarageUi.Dim, string.Empty, TextAlignmentOptions.TopLeft);
            _total = ui.TextAt(panel, 16f, 624f, 528f, 26f, 20f, Color.white, string.Empty);
            Button pay = ui.ButtonAt(panel, 16f, 660f, 256f, 44f, GarageUi.Go, string.Empty, Pay, 20f);
            _payLabel = pay.GetComponentInChildren<TextMeshProUGUI>();
            ui.ButtonAt(panel, 282f, 660f, 126f, 44f, GarageUi.Stop, GarageText.T("reallife_garage_leave"), () => Close(false));
            ui.ButtonAt(panel, 418f, 660f, 126f, 44f, GarageUi.Row, GarageText.T("reallife_garage_stock"), Stock);
            ui.ButtonAt(panel, 16f, 716f, 256f, 36f, GarageUi.Row, GarageText.T("reallife_garage_my_vehicles"), () => GarageSummary.Toggle());
            GarageUi.Wrap(ui.TextAt(panel, 282f, 714f, 262f, 44f, 14f, GarageUi.Dim, GarageText.T("reallife_garage_work_note"), TextAlignmentOptions.TopLeft));
            Fill();
            Refresh();
        }

        private void Tabs(Transform panel)
        {
            GarageUi ui = GarageUi.Instance;
            var holder = new GameObject("Tabs", typeof(RectTransform));
            holder.transform.SetParent(panel, false);
            var rect = (RectTransform)holder.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -74f);
            rect.sizeDelta = new Vector2(540f, 36f);
            string[] names =
            {
                GarageText.T("reallife_garage_tab_paint"), GarageText.T("reallife_garage_tab_finish"), GarageText.T("reallife_garage_tab_lights"),
                GarageText.T("reallife_garage_tab_body"), GarageText.T("reallife_garage_tab_parts")
            };
            var buttons = new Image[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var tab = (Tab)i;
                Button button = ui.ButtonAt(rect, 6f + i * 106f, 0f, 102f, 34f, tab == _tab ? GarageUi.Chosen : GarageUi.Row, names[i], null, 17f);
                buttons[i] = button.GetComponent<Image>();
                button.onClick.AddListener(() =>
                {
                    _tab = tab;
                    for (int n = 0; n < buttons.Length; n++)
                    {
                        buttons[n].color = n == (int)tab ? GarageUi.Chosen : GarageUi.Row;
                    }
                    Fill();
                });
            }
        }

        // The middle of the panel, for the tab that is open. Rebuilt on a click, never otherwise.
        private void Fill()
        {
            if (_content == null)
            {
                return;
            }
            GarageUi.Empty(_content);
            _customPrice = null;
            _customSwatch = null;
            switch (_tab)
            {
                case Tab.Paint: FillPaint(); break;
                case Tab.Finish: FillFinish(); break;
                case Tab.Lights: FillLights(); break;
                case Tab.Body: FillBody(); break;
                default: FillParts(); break;
            }
        }

        private void FillPaint()
        {
            GarageUi ui = GarageUi.Instance;
            if (!_fitted.Paintable)
            {
                ui.TextAt(_content, 6f, 6f, 528f, 60f, 18f, GarageUi.Dim, GarageText.T("reallife_garage_no_paint"));
                return;
            }
            int[] palette = GarageCatalogue.Palette;
            for (int i = 0; i < palette.Length; i++)
            {
                int colour = palette[i];
                float x = 6f + i % 7 * 76f;
                float y = i / 7 * 44f;
                RectTransform frame = ui.At("Swatch", _content, x, y, 70f, 38f, colour == _cart.Paint ? Color.white : GarageUi.Row);
                Button button = ui.ButtonAt(frame, 4f, 4f, 62f, 30f, GarageCar.FromRgb(colour), null, () => PickPaint(colour));
                button.name = "Colour";
            }
            ui.ButtonAt(_content, 6f, 272f, 250f, 32f, _cart.Paint < 0 ? GarageUi.Chosen : GarageUi.Row, GarageText.T("reallife_garage_paint_factory"), () => PickPaint(-1), 17f);
            ui.TextAt(_content, 266f, 276f, 270f, 26f, 16f, GarageUi.Dim,
                GarageText.T("reallife_garage_paint_prices", "palette", GarageText.Money(GarageCatalogue.PaintPrice(_kind, false)),
                    "custom", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true))));

            ui.TextAt(_content, 6f, 312f, 150f, 22f, 16f, GarageUi.Dim, GarageText.T("reallife_garage_paint_hue"));
            ui.TextAt(_content, 6f, 340f, 150f, 22f, 16f, GarageUi.Dim, GarageText.T("reallife_garage_paint_saturation"));
            ui.TextAt(_content, 6f, 368f, 150f, 22f, 16f, GarageUi.Dim, GarageText.T("reallife_garage_paint_brightness"));
            ui.SliderAt(_content, 150f, 314f, 280f, _h, value => Mix(value, _s, _v));
            ui.SliderAt(_content, 150f, 342f, 280f, _s, value => Mix(_h, value, _v));
            ui.SliderAt(_content, 150f, 370f, 280f, _v, value => Mix(_h, _s, value));
            RectTransform swatch = ui.At("Mixed", _content, 444f, 312f, 90f, 76f, Color.HSVToRGB(_h, _s, _v));
            _customSwatch = swatch.GetComponent<Image>();
            _customSwatch.raycastTarget = false;
            _customPrice = ui.TextAt(_content, 6f, 396f, 528f, 22f, 15f, GarageUi.Dim, GarageText.T("reallife_garage_paint_mix_note"));
        }

        private void PickPaint(int colour)
        {
            _cart.Paint = colour;
            if (colour >= 0)
            {
                Color.RGBToHSV(GarageCar.FromRgb(colour), out _h, out _s, out _v);
            }
            Preview();
            Fill();
            Refresh();
        }

        // A slider of the colour mixer moved: the car is repainted, the panel is not rebuilt (the slider is being held).
        private void Mix(float h, float s, float v)
        {
            _h = h;
            _s = s;
            _v = v;
            Color colour = Color.HSVToRGB(h, s, v);
            _cart.Paint = GarageCar.ToRgb(colour);
            if (_customSwatch != null)
            {
                _customSwatch.color = colour;
            }
            Preview();
            Refresh();
        }

        private void FillFinish()
        {
            GarageUi ui = GarageUi.Instance;
            if (!_fitted.Paintable)
            {
                ui.TextAt(_content, 6f, 6f, 528f, 60f, 18f, GarageUi.Dim, GarageText.T("reallife_garage_no_paint"));
                return;
            }
            for (int i = 0; i <= (int)PaintFinish.Pearl; i++)
            {
                var finish = (PaintFinish)i;
                string price = finish == _saved.Finish ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.FinishPrice(finish, _kind));
                Option(i * 46f, GarageText.T(GarageCatalogue.FinishName(finish)), price, finish == _cart.Finish, () =>
                {
                    _cart.Finish = finish;
                    Changed();
                });
            }
            GarageUi.Wrap(ui.TextAt(_content, 6f, 240f, 528f, 60f, 15f, GarageUi.Dim, GarageText.T("reallife_garage_finish_note"), TextAlignmentOptions.TopLeft));
        }

        private void FillLights()
        {
            GarageUi ui = GarageUi.Instance;
            ui.TextAt(_content, 6f, 0f, 528f, 24f, 18f, Color.white, GarageText.T("reallife_garage_lamps_header"));
            for (int i = 0; i <= (int)LampTint.Yellow; i++)
            {
                var tint = (LampTint)i;
                string price = tint == _saved.Lamps ? GarageText.T("reallife_garage_fitted")
                    : GarageText.Money(tint == LampTint.Stock ? GarageCatalogue.RemovalPrice : GarageCatalogue.LampPrice);
                Option(28f + i * 42f, GarageText.T(GarageCatalogue.LampName(tint)), price, tint == _cart.Lamps, () =>
                {
                    _cart.Lamps = tint;
                    Changed();
                }, 38f);
            }
            ui.TextAt(_content, 6f, 206f, 528f, 24f, 18f, Color.white,
                GarageText.T("reallife_garage_glow_header", "price", GarageText.Money(GarageCatalogue.GlowPrice)));
            ui.ButtonAt(_content, 6f, 236f, 110f, 38f, _cart.Glow < 0 ? GarageUi.Chosen : GarageUi.Row, GarageText.T("reallife_customshop_none"), () =>
            {
                _cart.Glow = -1;
                Changed();
            }, 17f);
            int[] colours = GarageCatalogue.GlowColours;
            for (int i = 0; i < colours.Length; i++)
            {
                int colour = colours[i];
                RectTransform frame = ui.At("Glow", _content, 124f + i * 58f, 236f, 54f, 38f, colour == _cart.Glow ? Color.white : GarageUi.Row);
                ui.ButtonAt(frame, 4f, 4f, 46f, 30f, GarageCar.FromRgb(colour), null, () =>
                {
                    _cart.Glow = colour;
                    Changed();
                });
            }
            GarageUi.Wrap(ui.TextAt(_content, 6f, 284f, 528f, 60f, 15f, GarageUi.Dim, GarageText.T("reallife_garage_glow_note"), TextAlignmentOptions.TopLeft));
        }

        private void FillBody()
        {
            GarageUi ui = GarageUi.Instance;
            string type = _car.vehicleType != null ? _car.vehicleType.vehicleTypeName : null;
            float y = 0f;
            y = BodyRow(y, "reallife_garage_body_spoiler", GarageCatalogue.SpoilerPrice, GarageFit.Spoiler(type, _kind), _cart.Spoiler, _saved.Spoiler, () => _cart.Spoiler = !_cart.Spoiler);
            y = BodyRow(y, "reallife_garage_body_rack", GarageCatalogue.RackPrice, GarageFit.Rack(type, _kind), _cart.Rack, _saved.Rack, () => _cart.Rack = !_cart.Rack);
            y = BodyRow(y, "reallife_garage_body_bar", GarageCatalogue.BarPrice, GarageFit.Bar(type, _kind), _cart.Bar, _saved.Bar, () => _cart.Bar = !_cart.Bar);

            y += 10f;
            ui.TextAt(_content, 6f, y, 528f, 24f, 18f, Color.white, GarageText.T("reallife_garage_wheels_header"));
            y += 28f;
            bool any = false;
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
                    Option(y, GarageText.T("reallife_garage_wheels_own"), string.Empty, _cart.Wheels.Length == 0, () =>
                    {
                        _cart.Wheels = string.Empty;
                        Changed();
                    }, 38f);
                    y += 42f;
                }
                string price = id == _saved.Wheels ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.WheelsPrice);
                Option(y, GarageText.T(WheelNames[i]), price, _cart.Wheels == id, () =>
                {
                    _cart.Wheels = id;
                    Changed();
                }, 38f);
                y += 42f;
            }
            if (!any)
            {
                GarageUi.Wrap(ui.TextAt(_content, 6f, y, 528f, 70f, 15f, GarageUi.Dim, GarageText.T("reallife_garage_wheels_none"), TextAlignmentOptions.TopLeft));
            }
        }

        private float BodyRow(float y, string nameKey, int price, bool fits, bool inCart, bool fitted, Action toggle)
        {
            if (!fits)
            {
                GarageUi.Instance.At("Row", _content, 6f, y, 528f, 40f, new Color(0.09f, 0.10f, 0.12f, 0.9f)).GetComponent<Image>().raycastTarget = false;
                GarageUi.Instance.TextAt(_content, 18f, y + 9f, 510f, 24f, 17f, GarageUi.Dim,
                    GarageText.T("reallife_garage_body_no_fit", "part", GarageText.T(nameKey)));
                return y + 46f;
            }
            string cost = fitted == inCart ? (fitted ? GarageText.T("reallife_garage_fitted") : GarageText.Money(price))
                : GarageText.Money(inCart ? price : GarageCatalogue.RemovalPrice);
            Option(y, GarageText.T(nameKey), cost, inCart, () =>
            {
                toggle();
                Changed();
            });
            return y + 46f;
        }

        private void FillParts()
        {
            GarageUi ui = GarageUi.Instance;
            if (!Settings.GarageTuning)
            {
                ui.TextAt(_content, 6f, 6f, 528f, 60f, 18f, GarageUi.Dim, GarageText.T("reallife_garage_parts_off"));
                return;
            }
            for (int s = 0; s < GarageCatalogue.Slots; s++)
            {
                int slot = s;
                float y = s * 52f;
                PartDef part = GarageCatalogue.Part(_cart.Parts[slot]);
                ui.At("Row", _content, 6f, y, 528f, 48f, part != null ? new Color(0.12f, 0.24f, 0.36f, 0.95f) : GarageUi.Row).GetComponent<Image>().raycastTarget = false;
                ui.TextAt(_content, 14f, y + 4f, 118f, 22f, 17f, Color.white, GarageText.T(GarageCatalogue.SlotName((PartSlot)slot)));
                ui.ButtonAt(_content, 134f, y + 6f, 34f, 36f, GarageUi.Plate, "<", () => Cycle(slot, -1), 20f);
                ui.TextAt(_content, 174f, y + 3f, 258f, 22f, 17f, Color.white, part != null ? GarageText.T(part.Name) : GarageText.T("reallife_garage_part_stock"));
                ui.TextAt(_content, 174f, y + 25f, 258f, 20f, 13f, GarageUi.Dim, part != null ? Effects(part) : string.Empty);
                ui.ButtonAt(_content, 436f, y + 6f, 34f, 36f, GarageUi.Plate, ">", () => Cycle(slot, 1), 20f);
                string price = _cart.Parts[slot] == _saved.Parts[slot]
                    ? (part != null ? GarageText.T("reallife_garage_fitted") : string.Empty)
                    : GarageText.Money(part != null ? GarageCatalogue.PartPrice(part, _kind) : GarageCatalogue.RemovalPrice);
                ui.TextAt(_content, 472f, y + 13f, 60f, 22f, 15f, GarageUi.Dim, price, TextAlignmentOptions.Right);
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
            Changed();
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

        // One row of a list: a name on the left, a price on the right, lit when it is the one chosen.
        private void Option(float y, string name, string price, bool chosen, Action pick, float height = 42f)
        {
            GarageUi ui = GarageUi.Instance;
            Button button = ui.ButtonAt(_content, 6f, y, 528f, height, chosen ? GarageUi.Chosen : GarageUi.Row, null, () => pick());
            ui.TextAt(button.transform, 12f, (height - 24f) * 0.5f, 360f, 24f, 18f, Color.white, name);
            ui.TextAt(button.transform, 376f, (height - 24f) * 0.5f, 140f, 24f, 17f, GarageUi.Dim, price, TextAlignmentOptions.Right);
        }

        private void Changed()
        {
            Preview();
            Fill();
            Refresh();
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

        private void RefreshMoney()
        {
            GameInstance save = SaveGameManager.Current;
            if (_money != null && save != null)
            {
                _money.SetText(GarageText.T("reallife_garage_money", "amount", GarageText.Money(save.Money)));
            }
        }

        // The bottom of the panel: the bill, and what the cart does to the car's figures.
        private void Refresh()
        {
            if (_panel == null)
            {
                return;
            }
            RefreshMoney();
            int bill = Bill();
            _total.SetText(GarageText.T("reallife_customshop_total", "amount", GarageText.Money(bill)));
            _payLabel.SetText(bill > 0 ? GarageText.T("reallife_garage_pay", "amount", GarageText.Money(bill)) : GarageText.T("reallife_customshop_done"));
            if (_customPrice != null)
            {
                _customPrice.SetText(_cart.Paint >= 0 && !GarageCatalogue.InPalette(_cart.Paint)
                    ? GarageText.T("reallife_garage_paint_mixed", "amount", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true)))
                    : GarageText.T("reallife_garage_paint_mix_note"));
            }

            var car = _car as CarController;
            GarageFigures before = GarageTuning.Figures(car, _saved);
            GarageFigures after = GarageTuning.Figures(car, _cart);
            GarageModifiers m = _cart.Modifiers();
            var text = new System.Text.StringBuilder(220);
            if (before.To100 > 0f && after.To100 > 0f)
            {
                text.Append(GarageText.T("reallife_garage_fig_to100", "from", before.To100.ToString("F1"), "to", after.To100.ToString("F1"))).Append('\n');
            }
            if (before.TopKmh > 0f && after.TopKmh > 0f)
            {
                text.Append(GarageText.T("reallife_garage_fig_top", "from", Mathf.RoundToInt(before.TopKmh).ToString(), "to", Mathf.RoundToInt(after.TopKmh).ToString())).Append('\n');
            }
            if (before.StopMetres > 0f && after.StopMetres > 0f)
            {
                text.Append(GarageText.T("reallife_garage_fig_stop", "from", Mathf.RoundToInt(before.StopMetres).ToString(), "to", Mathf.RoundToInt(after.StopMetres).ToString())).Append('\n');
            }
            if (m.Wear > 0f || m.Insurance > 0f || m.Loudness >= 2)
            {
                text.Append(GarageText.T("reallife_garage_fig_costs", "wear", Mathf.RoundToInt(m.Wear * 100f).ToString(), "insurance", Mathf.RoundToInt(m.Insurance * 100f).ToString()));
                if (m.Loudness >= 2)
                {
                    text.Append(' ').Append(GarageText.T("reallife_garage_fig_illegal"));
                }
            }
            else if (before.To100 < 0f)
            {
                text.Append(GarageText.T("reallife_garage_fig_none"));
            }
            _figures.SetText(text.ToString());
        }

        private void Stock()
        {
            int spent = _cart.Spent;
            _cart.Take(new GarageBuild());
            _cart.Spent = spent;
            Changed();
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
}
