using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.InputSystem;
using BigAmbitions.Items;
using Extensions;
using Helpers;
using NWH.VehiclePhysics2.Modules.Fuel;
using TMPro;
using UI;
using UI.ItemPanel;
using UI.PlayerHUD;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The item panel (the game's ItemPanelUI: what the player holds or drives, its readings, its cargo and its buttons) on
    /// the kit. Going Public copies the game's whole panel for a passenger's view, so nothing is added to the panel, not
    /// a component, a fade or a sprite: the kit stands on a canvas of its own beside it (the host's UiOverCanvas) and holds
    /// the one fade group the game already gives the panel at nothing while it draws it (the host's UiHeldGroup), giving
    /// it back the moment the kit stops. The game keeps every key of the panel (Sell, Sleep, Park, Place) and every rule:
    /// the kit presses the game's own buttons. A keeper on the interface's own object holds the group after the game's
    /// tweens each frame. Cost: one alpha read a frame; while the panel shows, a look at it four times a second.
    /// </summary>
    internal static class ItemPanelKit
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "ItemPanelKit", typeof(ItemPanelKit));
        private const string Drawn = "drawn over the game's panel";

        private static ItemPanelUI _ui;
        private static CanvasGroup _group;   // the game's own fade group on its panel, read here and held by _held
        private static UiHeldGroup _held;
        private static GameObject _root;
        private static Canvas _canvas;
        private static CanvasGroup _face;    // the kit's own fade, following the game's
        private static UiHost _host;
        private static ItemPanelKeeper _keeper;
        private static int _order, _want;   // the canvas's order as made, and as Lift wants it now
        private static float _nextLook;
        private static bool _shown, _classic, _gaveUp;
        private static string _state = "not looked at yet";

        internal static string State => _root != null ? Drawn : _state;

        /// <summary>True while the kit draws the panel, for the tests.</summary>
        internal static bool Showing => _shown;

        /// <summary>Once a second while the interface is on: the keeper put on while the switch is on, the game's panel as found while off.</summary>
        internal static void Install()
        {
            if (!UiScreenSwitches.On(BuyKits.ItemPanelId))
            {
                Remove("its switch is off");
                return;
            }
            if (_gaveUp || FrameGuard.Off)
            {
                return;
            }
            if (_root != null)
            {
                if (_ui == null || _group == null)
                {
                    Remove("the city was unloaded");
                }
                return;
            }
            if (Time.unscaledTime < _nextLook)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            ItemPanelUI ui = uis != null && uis.playerHUD != null ? uis.playerHUD.itemPanelUI : null;
            RectTransform rect = ui != null && ui.panel != null && UiDirector.Instance != null ? UiOverCanvas.Make(ui, "RealLife Interface " + BuyKits.ItemPanelId) : null;
            if (rect == null)
            {
                _state = "waiting for the game to build it";
                _nextLook = Time.unscaledTime + 2f;
                return;
            }
            _ui = ui;
            _group = ui.panel;
            _held = new UiHeldGroup(_group);
            _root = rect.gameObject;
            _canvas = _root.GetComponent<Canvas>();
            _order = _canvas.sortingOrder;
            _want = _order;
            _face = _root.AddComponent<CanvasGroup>();
            _host = _root.AddComponent<UiHost>();
            _host.Bind(new ItemPanelScreen(ui), UiGame.Instance, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight,
                UiText.T("reallife_ui_screen_item_panel"), UiIcon.Package);
            _host.PointerOnly = true;   // the game reads the panel's keys itself: the kit takes the pointer alone
            _host.View.Scale = UiTokens.CanvasPerPx;
            _host.Failed = Fallback;
            _keeper = UiDirector.Instance.gameObject.AddComponent<ItemPanelKeeper>();
            _state = Drawn;
        }

        /// <summary>The game's panel as it was: its fade and clicks given back, the kit gone.</summary>
        internal static void Remove(string why)
        {
            if (_root == null && _keeper == null)
            {
                return;
            }
            try
            {
                _held?.GiveBack(GameShows());
                if (_keeper != null)
                {
                    UnityEngine.Object.Destroy(_keeper);
                }
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                }
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
            }
            _keeper = null;
            _root = null;
            _host = null;
            _canvas = null;
            _face = null;
            _ui = null;
            _group = null;
            _held = null;
            _shown = false;
            _classic = false;
            if (UiDirector.Logger != null && why != null && Meta.Dev.Verbose)
            {
                UiDirector.Logger.Info("UI: the item panel is the game's own again (" + why + ").");
            }
        }

        /// <summary>
        /// The panel's canvas over the kit HUD's, at the minimap card's order (UiHostRules.MinimapOrder), so none of the
        /// HUD's pieces is drawn across it (the HUD lifts its own order over the phone's, which can leave the panel under it).
        /// The card and the panel never meet; the game's dialogs and the kit's windows stay over both. With the HUD not up,
        /// the order it was made at. While the kit draws manage cargo, one over that: the game's two panels are pressed
        /// side by side (a hand truck's tile moves its stack into the storage that is open), and manage cargo holds every
        /// click that falls under it. Four times a second with the panel's readings, never a frame; writes only a change.
        /// </summary>
        internal static void Lift()
        {
            if (_canvas == null)
            {
                return;
            }
            HudHost hud = HudHost.Instance;
            _want = hud != null && hud.isActiveAndEnabled ? Math.Max(_order, UiHostRules.MinimapOrder(hud.CanvasOrder)) : _order;
            int cargo = BuyKits.CargoOrder;
            if (cargo != int.MinValue)
            {
                _want = Math.Max(_want, UiHostRules.Above(cargo));
            }
            if (_shown && _canvas.sortingOrder != _want)
            {
                _canvas.sortingOrder = _want;
            }
        }

        /// <summary>A new city: the panel is looked for again.</summary>
        internal static void Rearm()
        {
            _nextLook = 0f;
            if (!_gaveUp)
            {
                _state = "not looked at yet";
            }
        }

        /// <summary>The game's own panel until it closes: another mod's button that only its view shows.</summary>
        internal static void Classic()
        {
            _classic = true;
            Keep();
        }

        // A fault in the kit's screen: the game's panel back for the session.
        private static void Fallback(Exception e)
        {
            UiDirector.Logger?.Warn("UI: " + UiText.T("reallife_ui_note_fallback") + " (the item panel, " + (e != null ? e.GetType().Name : "?") + ")");
            Remove("a fault");
            _gaveUp = true;
            _state = "the game's own after a fault of the kit's screen";
        }

        internal static void Fault(Exception e)
        {
            Guard.Fail(FrameGuard, e, null);
            Fallback(e);
        }

        // Whether the game shows its panel: open, visible and not hidden by its HidePanel.
        private static bool GameShows() => _group != null && _group.gameObject.activeInHierarchy && ItemPanelUI.IsVisible && _group.interactable;

        // Each frame after the game's tweens: the game's group held at nothing while its panel is open, and the kit's face
        // shown, faded with the game's, while the game shows the panel.
        internal static void Keep()
        {
            if (_ui == null || _group == null || _held == null)
            {
                Remove("the city was unloaded");
                return;
            }
            bool open = _group.gameObject.activeInHierarchy;
            if (!open || !ItemPanelUI.IsVisible)
            {
                _classic = false;   // the game's own view lasts until the panel closes
            }
            bool shows = GameShows() && !_classic;
            _held.Hold(open && !_classic, GameShows());
            if (shows != _shown)
            {
                _shown = shows;
                if (shows)
                {
                    _canvas.overrideSorting = true;
                    _canvas.sortingOrder = _want;
                    _root.SetActive(true);
                    _root.transform.SetAsLastSibling();
                    _host.Show();
                }
                else
                {
                    _host.Hide();
                    _root.SetActive(false);
                }
            }
            if (shows)
            {
                float f = Mathf.Clamp01(_held.GameAlpha);
                if (!Mathf.Approximately(_face.alpha, f))
                {
                    _face.alpha = f;
                }
            }
        }
    }

    /// <summary>The item panel's keeper: on the interface's own object, after every Update (the game's tweens), once a frame.</summary>
    internal sealed class ItemPanelKeeper : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "ItemPanelKeeper", typeof(ItemPanelKeeper));
        private static readonly int HotKeep = HotSpots.Slot("Ui: ItemPanelKeeper.LateUpdate");

        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotKeep);
            if (FrameGuard.Off)
            {
                enabled = false;
                ItemPanelKit.Remove("its guard is off");
                return;
            }
            try
            {
                ItemPanelKit.Keep();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                ItemPanelKit.Fault(e);
            }
        }
    }

    /// <summary>
    /// The item panel as the kit's docked card: the thing, its readings, its cargo as tiles, the game's buttons. A car the
    /// player drives is the small card (UiItemPanel.DrivingCard): its speed, its fuel with how far it goes, its condition
    /// or what it needs, the parking zone and how much it carries. A hand truck, a cart and a scooter keep the full panel:
    /// their tiles are the game's one way to take a box off them, and a tile's press is the game's own click (CargoTake).
    /// </summary>
    internal sealed class ItemPanelScreen : IUiScreen
    {
        private const float Every = 0.25f;
        private const string SpeedRow = "RealLifeOverhaul Speed";   // the mod's speed, a copy of the condition row in the vehicle panel
        private readonly ItemPanelUI _ui;
        private readonly UiItemPanelModel _m = new UiItemPanelModel();
        private readonly List<Button> _buttons = new List<Button>(10);   // the game's button an action presses; null for Manage cargo
        private VehicleController _car;                                  // the vehicle the panel shows, or null
        private bool _mine = true;                                       // the vehicle is the player's own (always, outside a Going Public session)
        private bool _cargoRow;                                          // the game's own cargo row shows on its vehicle panel ("3/10")
        private bool _needSaid;                                          // what the car needs stands on the driving card's condition line
        private int _saidCar, _saidTiles = -1;                           // the vehicle the log's line was last written for, and what it showed then
        private bool _saidCompact;
        private string _sellFor;                                         // "Sell for $2,407", made again only when the price changes
        private float _sellPrice = -1f;
        private bool _sellShort;                                         // the price made short, for the driving card
        private bool _slotButtons;                                       // a slot shows its own sell or discard, which only the game's view has
        private float _frameW, _frameH;                                  // the layout's size, last laid: the minimap's card is read into it
        private readonly List<UiItemAction> _actions = new List<UiItemAction>(10);
        private readonly List<UiReading> _readings = new List<UiReading>(6);
        private readonly List<UiInfoRow> _facts = new List<UiInfoRow>(4);
        private readonly List<Transform> _slots = new List<Transform>(12);
        private readonly List<string> _words = new List<string>(4);
        private readonly List<Transform> _parents = new List<Transform>(4);
        private readonly StringBuilder _sb = new StringBuilder(256);
        private readonly Vector3[] _corners = new Vector3[4];
        private UiCatalogueCard[] _cargo = new UiCatalogueCard[0];
        private string _status, _streetOf, _districtOf;   // the street and district the HUD shows, and the words they came from
        private string _range, _speed, _speedUnit;         // the range's and the speed's words, made again only when a figure changes
        private string _rangeShort;                        // "310 km", the range beside the fuel's figure on the driving card
        private int _rangeShown = -1, _speedShown = int.MinValue, _limitShown = int.MinValue;
        private string _seen;
        private float _next;
        private bool _dirty = true;

        public ItemPanelScreen(ItemPanelUI ui)
        {
            _ui = ui;
        }

        public string Id => BuyKits.ItemPanelId;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    string now = Read();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_seen == null)
            {
                _seen = Read();
            }
            if (frame.Width != _frameW || frame.Height != _frameH)
            {
                _frameW = frame.Width;
                _frameH = frame.Height;
                Minimap();
            }
            UiItemPanel.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            switch (command)
            {
                case UiItemPanel.ActionCommand:
                    if (arg >= 0 && arg < _buttons.Count && _buttons[arg] == null)
                    {
                        // Manage cargo: the game's own walk to the trunk and its cargo screen, as a click on the vehicle does.
                        if (_car != null)
                        {
                            _car.MoveAndManageStorage();
                        }
                        return Again(_car != null);
                    }
                    return arg >= 0 && arg < _buttons.Count && Again(UiWidgets.Press(_buttons[arg]));
                case UiItemPanel.CargoCommand:
                {
                    // A stack's tile is the game's own click on its tile: off the hand truck into the hands, or across to the storage that is open.
                    bool taken = arg >= 0 && arg < _slots.Count
                                 && CargoTake.Press(_slots[arg] != null ? _slots[arg].GetComponent<CargoItemUi>() : null, Holder(), _mine, _car != null, "the item panel");
                    Again(true);   // read again either way: a press that met a tile the game had rebuilt shows the new ones at once
                    return taken;
                }
                case UiItemPanel.MoreCommand:
                    ItemPanelKit.Classic();
                    return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private bool Again(bool done)
        {
            if (done)
            {
                _next = 0f;
                _dirty = true;
            }
            return done;
        }

        // What holds the stacks the tiles show: the vehicle in use, else the thing in the hands.
        private ICargoHolder Holder() => _car != null ? (ICargoHolder)_car.vehicleInstance : _ui.selectedItemInstance;

        // The street and district of the nearest address as the HUD shows them ("12 Main Street" in "Midtown" gives "Main
        // Street, Midtown"), made again only when the HUD's words change.
        private string Status()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            PlayerHUD hud = uis != null ? uis.playerHUD : null;
            TMP_Text near = hud != null && hud.nearestBuildingText != null ? hud.nearestBuildingText.GetComponent<TMP_Text>() : null;
            TMP_Text district = hud != null ? hud.currentNeighborhoodText : null;
            string street = near != null ? near.text : null, place = district != null ? district.text : null;
            if (!ReferenceEquals(street, _streetOf) || !ReferenceEquals(place, _districtOf))
            {
                _streetOf = street;
                _districtOf = place;
                string s = BuyKits.StreetOf(UiWidgets.Text(near)), d = UiWidgets.Text(district);
                _status = string.IsNullOrEmpty(s) ? (string.IsNullOrEmpty(d) ? null : d) : string.IsNullOrEmpty(d) ? s : s + ", " + d;
            }
            return _status;
        }

        // How far the tank still goes by the game's own reckoning of a car's fuel over a distance (what a skipped bridge
        // burns: the engine's most use an hour as the fuel module reckons it, at five and a half times idle, at the car's top
        // speed, with the mod's thirst for its kind), and the tank's share for the meter; null when the car burns no fuel or
        // the game's fuel is off. In miles with the game's imperial units.
        private string Range(VehicleController car, out float share)
        {
            share = -1f;
            var driven = car as CarController;
            FuelModule fuel = driven != null ? driven.fuelModule : null;
            NWH.VehiclePhysics2.VehicleController body = driven != null ? driven.vehicleController : null;
            GameInstance save = SaveGameManager.Current;
            if (fuel == null || !fuel.useFuel || body == null || body.powertrain == null || body.powertrain.engine == null || car.vehicleType == null
                || car.vehicleType.maxSpeed <= 0 || (save != null && save.gameVariables != null && save.gameVariables.disableVehicleFuel))
            {
                return null;
            }
            float perKm = FuelUseRules.PerKm(body.powertrain.engine.maxPower, fuel.efficiency, fuel.idleConsumption, fuel.consumptionMultiplier, car.vehicleType.maxSpeed);
            if (!(perKm > 0f) || !(fuel.amount >= 0f))
            {
                return null;
            }
            share = fuel.capacity > 0f ? Mathf.Clamp01(fuel.amount / fuel.capacity) : -1f;
            bool imperial = PlayerPrefSettings.useImperial;
            int far = Mathf.FloorToInt(fuel.amount / perKm * (imperial ? BuyKits.MilesPerKm : 1f));
            int key = far * 2 + (imperial ? 1 : 0);
            if (key != _rangeShown || _range == null)
            {
                _rangeShown = key;
                string distance = far.ToString("N0", CultureInfo.InvariantCulture), unit = imperial ? "mi" : "km";
                _range = UiText.T("reallife_ui_buy_range_value").Replace("{distance}", distance).Replace("{unit}", unit);
                _rangeShort = distance + " " + unit;
            }
            return _range;
        }

        // The mod's speed as its row in the game's panel shows it and, on a road the traffic knows, against that road's limit
        // as the police read it (Crime/RoadMap: "32 of 50 km/h", the meter the share of the limit, amber above it); the row's
        // own words off the roads or with the map not there.
        private UiReading Speed(VehicleController car, string shown)
        {
            var reading = new UiReading { Icon = UiIcon.FastForward, Label = UiText.T("reallife_ui_buy_speed"), Share = -1f, Value = shown };
            int speed = SpeedReading.Shown;
            string unit = SpeedReading.ShownUnit;
            if (speed == int.MinValue || unit == null || car == null || !TrafficTools.Street || !RoadMap.Ensure()
                || !RoadMap.NearestLane(car.transform.position, out float distance, out _, out float limitKmh) || distance > BuyKits.OnTheRoad || !(limitKmh > 0f))
            {
                return reading;
            }
            int limit = Mathf.RoundToInt(unit == "mph" ? limitKmh * BuyKits.MilesPerKm : limitKmh);
            if (speed != _speedShown || limit != _limitShown || !ReferenceEquals(unit, _speedUnit) || _speed == null)
            {
                _speedShown = speed;
                _limitShown = limit;
                _speedUnit = unit;
                _speed = UiText.T("reallife_ui_buy_speed_of").Replace("{speed}", speed.ToString(CultureInfo.InvariantCulture))
                    .Replace("{limit}", limit.ToString(CultureInfo.InvariantCulture)).Replace("{unit}", unit);
            }
            reading.Value = _speed;
            reading.Share = limit > 0 ? Mathf.Clamp01(speed / (float)limit) : -1f;
            reading.Tone = speed > limit ? UiRole.Warn : UiRole.None;
            return reading;
        }

        // The panel as the game filled it. Returns what the screen shows, to see a change by.
        private string Read()
        {
            _sb.Length = 0;
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = BuyKits.Get<VehicleController>(GameContract.PanelCar, _ui);
            ItemInstance item = _ui.selectedItemInstance;
            bool game = BuyKits.Value(GameContract.PanelMiniGame, _ui, false);
            _m.Title = game ? UiText.T("reallife_ui_buy_minigame") : car != null ? UiText.T("common_vehicle") : UiText.T("reallife_ui_buy_in_hands");
            _m.Icon = game ? UiIcon.GameController : car != null ? UiIcon.Car : UiIcon.Package;
            _m.Glyph = _m.Icon;
            _m.Name = BuyKits.Words(_ui.itemNameLabel);
            _m.Status = car != null ? Status() : null;
            bool mine = car == null || GoingPublic.MineVehicle(car);   // a vehicle of another player's (a Going Public session) is not the player's to sell: the game hides its sell too
            _m.Owner = car != null ? UiText.T(mine ? "reallife_ui_buy_yours" : "reallife_ui_buy_not_yours") : null;
            _m.OwnerTone = mine ? UiRole.Ok : UiRole.Warn;
            string type = car != null && car.vehicleType != null ? car.vehicleType.vehicleTypeName : item != null ? item.itemName : null;
            _m.Picture = game ? null : BuyPictures.Item(type);
            _m.Aspect = 1f;
            _sb.Append(_m.Title).Append('|').Append(_m.Name).Append('|').Append(_m.Status).Append('|').Append(_m.Picture).Append('|').Append(_m.Owner).Append('|');
            _car = car;
            _mine = mine;
            // Driving a car, the small card (the owner, 12 Nov 2026). The game shows a vehicle's panel only while the player
            // uses the vehicle, a hand truck, a cart and a scooter too, and those carry tiles that are its one way to take a
            // box off them: so never by "in use" alone, and never while a tile shows.
            int tiles = Stacks();
            _m.Compact = UiItemPanel.DrivingCard(car != null, car != null && car.controlledByPlayer, PlayerHelper.IsUsingVehicle,
                car != null && car.vehicleType != null && car.vehicleType.spawnInPlayerObject, car != null && car.showCargoInItemPanel, tiles);
            _sb.Append(_m.Compact ? 'c' : 'f').Append(mine ? 'm' : 'o');
            ReadVehicle(car);
            ReadMeta();
            ReadCargo(car, item);
            ReadButtons();
            Minimap();
            Said(car, tiles);
            return _sb.ToString();
        }

        // With the diagnostics on, one line when the vehicle's panel becomes the card or the full panel, or its tiles change:
        // which it is and what it shows, so a log says whether a hand truck's tiles were there to press.
        private void Said(VehicleController car, int tiles)
        {
            if (!Meta.Dev.Verbose || car == null)
            {
                return;
            }
            int id = car.GetInstanceID();
            if (id == _saidCar && _m.Compact == _saidCompact && tiles == _saidTiles)
            {
                return;
            }
            _saidCar = id;
            _saidCompact = _m.Compact;
            _saidTiles = tiles;
            bool whole = tiles == _m.Cargo.Length && (!_m.Compact || tiles == 0);   // every tile the game shows is drawn, and the card stands for none
            UiDirector.Logger?.Info("UI check (vehicle panel): " + _m.Name + " has " + (_m.Compact ? "the driving card" : "the full panel") + ": the game gives its panel "
                                    + (car.showCargoInItemPanel ? "cargo tiles" : "no cargo tiles") + ", " + tiles + " show and " + _m.Cargo.Length + " are drawn, with "
                                    + _m.Readings.Length + " readings and cargo " + (_m.CargoValue ?? "not said") + ": " + (whole ? "PASS." : "FAIL."));
        }

        // The vehicle's readings in the game's own order: fuel and condition with their meters, the parking zone as a line,
        // the mod's speed and any other row a mod copied into the panel as the words it shows. The driving card holds the
        // same readings in fewer lines: the speed first, how far the tank goes beside the fuel's figure, and what the car
        // needs in the condition's place while it needs something.
        private void ReadVehicle(VehicleController car)
        {
            _readings.Clear();
            _facts.Clear();
            _cargoRow = false;
            _needSaid = false;
            VehicleInfoPanel info = _ui.vehicleInfo;
            if (car != null && info != null && info.gameObject.activeInHierarchy)
            {
                GameObject fuel = BuyKits.Get<GameObject>(GameContract.InfoFuel, info), condition = BuyKits.Get<GameObject>(GameContract.InfoCondition, info);
                GameObject cargo = BuyKits.Get<GameObject>(GameContract.InfoCargo, info), parking = BuyKits.Get<GameObject>(GameContract.InfoParking, info);
                GameObject steering = BuyKits.Get<GameObject>(GameContract.InfoSteering, info);
                Transform rows = info.transform;
                for (int i = 0; i < rows.childCount; i++)
                {
                    GameObject row = rows.GetChild(i).gameObject;
                    if (row == cargo)
                    {
                        _cargoRow = row.activeSelf;   // said with the cargo: by the tiles' line, or on the driving card's head line
                        continue;
                    }
                    if (!row.activeSelf || row == steering)
                    {
                        continue;
                    }
                    if (row == fuel || row == condition)
                    {
                        string value = UiWidgets.Text(BuyKits.Get<TMP_Text>(row == fuel ? GameContract.InfoFuelValue : GameContract.InfoConditionValue, info));
                        float share = BuyKits.Percent(value);
                        var reading = new UiReading
                        {
                            Icon = row == fuel ? UiIcon.Drop : UiIcon.Wrench, Label = UiText.T(row == fuel ? "itempanelui_vehicle_fuel" : "itempanelui_maintenance_condition"),
                            Share = share, Tone = BuyKits.Tone(share), Value = value
                        };
                        float left = -1f;
                        string range = row == fuel ? Range(car, out left) : null;
                        if (_m.Compact && row == fuel)
                        {
                            reading.Note = range != null ? _rangeShort : null;
                        }
                        else if (_m.Compact && CarDash.Shown != null)
                        {
                            reading.Value = CarDash.Shown;
                            reading.Tone = CarDash.Urgent ? UiRole.Danger : UiRole.Warn;
                            _needSaid = true;
                        }
                        _readings.Add(reading);
                        if (!_m.Compact && range != null)
                        {
                            _readings.Add(new UiReading { Icon = UiIcon.Navigation, Label = UiText.T("reallife_ui_buy_range"), Share = left, Tone = BuyKits.Tone(left), Value = range });
                        }
                    }
                    else if (row == parking)
                    {
                        _facts.Add(new UiInfoRow { Label = UiText.T("itempanelui_vehicle_parkingzone"), Value = BuyKits.Words(BuyKits.Get<Component>(GameContract.InfoParkingValue, info)) });
                    }
                    else if (row.name.StartsWith(SpeedRow, StringComparison.Ordinal))
                    {
                        UiReading speed = Speed(car, BuyKits.WordsAt(row.transform, "Value"));
                        if (_m.Compact)
                        {
                            _readings.Insert(0, speed);   // what a driver reads most, first
                        }
                        else
                        {
                            _readings.Add(speed);
                        }
                    }
                    else if (_needSaid && row.name == CarDash.RowName)
                    {
                        continue;   // on the condition's line already
                    }
                    else if (BuyKits.AllWords(row.transform, _words) > 0)
                    {
                        _facts.Add(new UiInfoRow { Label = _words.Count > 1 ? _words[0] : string.Empty, Value = _words[_words.Count - 1] });
                    }
                }
            }
            _m.ReadingsLabel = _readings.Count > 0 ? UiText.T("reallife_ui_buy_readings") : null;
            if (_m.Readings.Length != _readings.Count)
            {
                _m.Readings = new UiReading[_readings.Count];
            }
            _readings.CopyTo(_m.Readings);
            for (int i = 0; i < _readings.Count; i++)
            {
                _sb.Append(_readings[i].Label).Append('=').Append(_readings[i].Value).Append(_readings[i].Note).Append(';');
            }
        }

        // What the game's meta line and maintenance line say (the unpaid amount, a mop's cleanliness), as lines.
        private void ReadMeta()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Transform root = pass == 0 ? _ui.metaInfo : _ui.maintenanceInfo;
                if (root == null || !root.gameObject.activeInHierarchy || BuyKits.AllWords(root, _words) == 0)
                {
                    continue;
                }
                _facts.Add(new UiInfoRow
                {
                    Label = _words.Count > 1 ? _words[0] : UiText.T(pass == 0 ? "reallife_ui_buy_unpaid_total" : "itempanelui_maintenance_condition"),
                    Value = _words[_words.Count - 1], Ink = pass == 0 ? UiRole.WarnText : UiRole.None
                });
            }
            if (_m.Facts.Length != _facts.Count)
            {
                _m.Facts = new UiInfoRow[_facts.Count];
            }
            _facts.CopyTo(_m.Facts);
            for (int i = 0; i < _facts.Count; i++)
            {
                _sb.Append(_facts[i].Label).Append('=').Append(_facts[i].Value).Append(';');
            }
        }

        // The game's tiles that show, in its order: the last ones it made, as it lets the old ones go only at the end of the
        // frame it rebuilds them in (ItemPanelUI.SetupCargoSlots), and a read in that frame would hold both.
        private int Stacks()
        {
            _slots.Clear();
            if (_ui.cargoContainer == null || !_ui.cargoContainer.activeInHierarchy)
            {
                return 0;
            }
            BuyKits.Copies(_ui.cargoSlotTemplate, _slots);
            var stacks = BuyKits.Get<System.Collections.IList>(GameContract.PanelStacks, _ui);
            if (stacks != null && _slots.Count > stacks.Count)
            {
                _slots.RemoveRange(0, _slots.Count - stacks.Count);
            }
            return _slots.Count;
        }

        // The cargo slots as tiles (Stacks, read before): each stack's icon with its count, an unpaid one with its price; the
        // empty slots are said, not drawn. A car the player drives has no tiles in the game, only its cargo row's count,
        // which the driving card says on its head line. Another player's vehicle's tiles are drawn held.
        private void ReadCargo(VehicleController car, ItemInstance item)
        {
            int most = 0, used = 0;
            bool shown = _ui.cargoContainer != null && _ui.cargoContainer.activeInHierarchy;
            if (shown || (_m.Compact && _cargoRow))
            {
                ICargoHolder holder = car != null ? car.vehicleInstance : item;
                most = car != null && car.vehicleType != null ? car.vehicleType.maxCargoCapacity : item != null && item.ItemCached != null ? item.ItemCached.cargoCapacity : 0;
                List<CargoInstance> held = holder != null ? holder.GetCargoInstances() : null;
                used = held != null ? held.Count : _slots.Count;
            }
            if (_cargo.Length != _slots.Count)
            {
                _cargo = new UiCatalogueCard[_slots.Count];
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i].GetComponent<CargoItemUi>();
                TMP_Text count = BuyKits.Get<TMP_Text>(GameContract.SlotCount, slot), price = BuyKits.Get<TMP_Text>(GameContract.SlotPrice, slot);
                bool owed = price != null && price.gameObject.activeSelf;
                string pieces = count != null && count.gameObject.activeSelf ? UiWidgets.Text(count) : null;
                _cargo[i] = new UiCatalogueCard
                {
                    Picture = BuyPictures.Item(_slots[i].name), Aspect = 1f, Icon = UiIcon.Package,
                    Name = BuyKits.Words(BuyKits.Get<Component>(GameContract.SlotName, slot)),
                    Figure = owed ? UiWidgets.Text(price) : pieces, State = owed ? UiText.T("reallife_ui_buy_unpaid") : null, StateTone = UiRole.Warn,
                    Disabled = car != null && !_mine
                };
                _sb.Append(_cargo[i].Name).Append(_cargo[i].Figure).Append(';');
            }
            _m.Cargo = _cargo;
            _m.CargoSlots = Math.Max(most, _slots.Count);
            _m.CargoLabel = _m.CargoSlots > 0 ? UiText.T("common_cargo") : null;
            _m.CargoValue = _m.CargoSlots > 0
                ? UiText.T("reallife_ui_buy_slots").Replace("{used}", used.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Replace("{most}", _m.CargoSlots.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : null;
            _sb.Append(_m.CargoValue).Append('|');
        }

        // The game's buttons that show, in its own order, the first one the panel's main; another mod's own button as More.
        private void ReadButtons()
        {
            _buttons.Clear();
            _actions.Clear();
            SlotActions();
            ManageCargo();
            Add(_ui.parkButton, null, Key(PlayerAction.SecondaryInteract), false);
            Add(_ui.placeButton, _ui.placeButtonLabel, Key(PlayerAction.SecondaryInteract), false);
            Add(_ui.leaveButton, null, default(UiGameKey), false);
            Add(_ui.autoParkButton, null, Key(PlayerAction.SpecialInteract), false);
            Add(_ui.sleepButton, null, Key(PlayerAction.Sleep), false);
            Add(_ui.grabButton, null, default(UiGameKey), false);
            Add(_ui.useMopButton, null, default(UiGameKey), false);
            Add(_ui.sellButton, null, Key(PlayerAction.Sell), true);
            SellFor();
            Add(_ui.discardButton, null, default(UiGameKey), true);
            if (_actions.Count > 0)
            {
                UiItemAction first = _actions[0];
                first.Main = !first.Danger;
                _actions[0] = first;
            }
            if (_m.Actions.Length != _actions.Count)
            {
                _m.Actions = new UiItemAction[_actions.Count];
            }
            _actions.CopyTo(_m.Actions);
            _m.More = ItemPanelRules.More(Foreign(), _slotButtons) ? UiText.T("reallife_ui_buy_more") : null;
            for (int i = 0; i < _actions.Count; i++)
            {
                _sb.Append(_actions[i].Label).Append(_actions[i].Disabled ? '-' : '+');
            }
            _sb.Append(_m.More);
        }

        private void Add(Button b, Component label, UiGameKey key, bool danger)
        {
            if (b == null || !b.gameObject.activeSelf)
            {
                return;
            }
            _buttons.Add(b);
            _actions.Add(new UiItemAction { Label = BuyKits.Words(label != null ? label : b), Key = key, Danger = danger, Disabled = !b.interactable });
        }

        // What each stack carried offers through the game's own button on its slot (Eat, Drink, Equip, Read, Place), as an
        // action of the panel named with the stack, first so that eating what is in the bag is the panel's main. The kit drew
        // the stacks as tiles that press the slot alone, so food in a bag could not be eaten at all (1.0.0 to 1.0.2). A
        // slot's own sell and discard have no place on the tile: while one shows, the panel offers the game's own view.
        // On another player's vehicle (a Going Public session) a stack's action is drawn held, as its tile is: what it
        // carries is theirs to eat or place.
        private void SlotActions()
        {
            _slotButtons = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i].GetComponent<CargoItemUi>();
                Button act = BuyKits.Get<Button>(GameContract.SlotAction, slot);
                string label = act != null && act.gameObject.activeSelf
                    ? ItemPanelRules.SlotAction(BuyKits.Words(BuyKits.Get<Component>(GameContract.SlotActionLabel, slot)), BuyKits.Words(BuyKits.Get<Component>(GameContract.SlotName, slot)))
                    : null;
                if (label != null)
                {
                    _buttons.Add(act);
                    _actions.Add(new UiItemAction { Label = label, Disabled = !act.interactable || (_car != null && !_mine) });
                }
                _slotButtons |= UiWidgets.There(BuyKits.Get<Button>(GameContract.SlotSell, slot)) || UiWidgets.There(BuyKits.Get<Button>(GameContract.SlotDiscard, slot));
            }
        }

        // Manage cargo, first and the panel's main, while the vehicle holds cargo and the player is on foot with empty hands:
        // the game offers it then by a click on the vehicle (VehicleCtaBehavior), never from its panel. Driving, the game
        // offers none, so neither does the panel. No key: F is the game's own in the world.
        private void ManageCargo()
        {
            if (_car == null || _car.vehicleType == null || _car.vehicleType.maxCargoCapacity <= 0 || _car.controlledByPlayer || PlayerHelper.IsUsingVehicle
                || PlayerHelper.IsHoldingItem)
            {
                return;
            }
            _buttons.Add(null);
            _actions.Add(new UiItemAction { Label = UiText.T("reallife_ui_buy_manage_cargo") });
        }

        // A vehicle's Sell says what it sells for, the price the game's own confirm offers (VehicleInstance.GetSellingPrice).
        private void SellFor()
        {
            int last = _actions.Count - 1;
            ItemInstance item = _ui.selectedItemInstance;
            if (((_car == null || _car.vehicleInstance == null) && item == null) || last < 0 || _buttons[last] != _ui.sellButton)
            {
                return;
            }
            float price = _car != null && _car.vehicleInstance != null ? _car.vehicleInstance.GetSellingPrice() : ItemPrice(item);
            if (price != _sellPrice || _sellFor == null || _sellShort != _m.Compact)
            {
                _sellPrice = price;
                _sellShort = _m.Compact;
                _sellFor = UiText.T("reallife_ui_buy_sell_for").Replace("{price}", _m.Compact ? price.ToShortCurrencyFormat() : price.ToCurrencyFormat());
            }
            UiItemAction a = _actions[last];
            a.Label = _sellFor;
            _actions[last] = a;
        }

        // The mod's minimap card (Navigation) where it stands, in px of the layout, so the panel stands over it wherever it
        // is and whichever HUD shows; nothing while it is hidden. With it the HUD's key chips over the bar, which the panel's
        // foot keeps over (they stand on a canvas of their own and know nothing of the panel), and the panel's canvas order
        // over the HUD's. Read with the panel, four times a second; whole px.
        private void Minimap()
        {
            UiMinimapBox.Read(_frameW, _frameH, out _m.AvoidX, out _m.AvoidY, out _m.AvoidW, out _m.AvoidH);
            if (_m.AvoidW > 0f)
            {
                _sb.Append(_m.AvoidX).Append(',').Append(_m.AvoidY).Append(',').Append(_m.AvoidW).Append(',').Append(_m.AvoidH).Append('|');
            }
            HudHost.KeysBox(_frameW, _frameH, out _m.KeysX, out _m.KeysY, out _m.KeysW, out _m.KeysH);
            Phone();
            ShellNotices.StackBox(_frameW, _frameH, out _m.NoticesX, out _m.NoticesY, out _m.NoticesW, out _m.NoticesH);
            if (_m.PhoneW > 0f)
            {
                _sb.Append(_m.PhoneX).Append(',').Append(_m.PhoneY).Append(',').Append(_m.PhoneW).Append(',').Append(_m.PhoneH).Append('|');
            }
            if (_m.NoticesW > 0f)
            {
                _sb.Append(_m.NoticesX).Append(',').Append(_m.NoticesY).Append(',').Append(_m.NoticesW).Append(',').Append(_m.NoticesH).Append('|');
            }
            ItemPanelKit.Lift();
            if (_m.KeysW > 0f)
            {
                _sb.Append(_m.KeysX).Append(',').Append(_m.KeysY).Append(',').Append(_m.KeysW).Append(',').Append(_m.KeysH).Append('|');
            }
        }

        // The kit's phone card where it stands on the screen (its part above the screen's foot, open or folded) in px of the
        // layout, as the HUD reads it for its key chips (HudLive.PhoneBox); nothing while it does not show.
        private void Phone()
        {
            RectTransform phone = PhoneKit.Rect;
            _m.PhoneX = _m.PhoneY = _m.PhoneW = _m.PhoneH = 0f;
            if (phone == null || _frameW <= 0f || _frameH <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }
            phone.GetWorldCorners(_corners);   // the phone's canvas is an overlay: world is screen pixels
            float foot = Math.Max(0f, _corners[0].y), top = Math.Min(Screen.height, _corners[2].y);
            if (top <= foot)
            {
                return;
            }
            UiHudRules.ToLayout(_corners[0].x, top, Screen.width, Screen.height, _frameW, _frameH, out float x, out float y);
            UiHudRules.ToLayout(_corners[2].x, foot, Screen.width, Screen.height, _frameW, _frameH, out float right, out float bottom);
            _m.PhoneX = Mathf.Floor(x);
            _m.PhoneY = Mathf.Floor(y);
            _m.PhoneW = Mathf.Ceil(right) - _m.PhoneX;
            _m.PhoneH = Mathf.Ceil(bottom) - _m.PhoneY;
        }

        // A thing in hand's price as the game's own Sell confirm offers it (ItemPanelUI.GetTotalSellingPrice): its own, and
        // indoors the price of what is stacked on it too.
        private static float ItemPrice(ItemInstance item)
        {
            float price = item.GetSellingPrice();
            if (!BuildingManager.IsInsideBuilding || item.stackedItems == null)
            {
                return price;
            }
            var registration = InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
            if (registration == null)
            {
                return price;
            }
            foreach (var child in item.stackedItems)
            {
                if (registration.itemInstances.TryGetValue(child.childId, out ItemInstance stacked) && stacked != item)
                {
                    price += ItemPrice(stacked);
                }
            }
            return price;
        }

        // A button another mod put among the game's (Going Public's passenger lock) that shows now: only the game's view has it.
        private bool Foreign()
        {
            _parents.Clear();
            Button[] own = { _ui.parkButton, _ui.placeButton, _ui.leaveButton, _ui.autoParkButton, _ui.sleepButton, _ui.grabButton, _ui.useMopButton, _ui.sellButton, _ui.discardButton };
            for (int i = 0; i < own.Length; i++)
            {
                if (own[i] != null && own[i].transform.parent != null && !_parents.Contains(own[i].transform.parent))
                {
                    _parents.Add(own[i].transform.parent);
                }
            }
            for (int p = 0; p < _parents.Count; p++)
            {
                for (int i = 0; i < _parents[p].childCount; i++)
                {
                    Transform c = _parents[p].GetChild(i);
                    var b = c.GetComponent<Button>();
                    if (b != null && c.gameObject.activeSelf && Array.IndexOf(own, b) < 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static UiGameKey Key(PlayerAction action) => BuyKits.Key(action);
    }
}
