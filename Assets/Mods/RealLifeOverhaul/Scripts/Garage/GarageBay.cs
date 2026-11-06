using System;
using System.Globalization;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Elements;
using UI.MiniMenu;
using UI.Overlays;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own panel at a filling station or a repair bay, with one more line and button in a bay. It derives from
    /// the game's GasStationOverlay and implements IOverlay again, so the box's questions are answered here while the
    /// repair, the wash and the refuelling stay the game's. Asked when the game shows its panel, never per frame.
    /// </summary>
    public sealed class RepairBayOverlay : GasStationOverlay, IOverlay
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "RepairBayOverlay", typeof(RepairBayOverlay));   // the game's calls of it (Core/Guard)

        private int _frame = -1;
        private bool _atBay;
        private ButtonInfo[] _own;

        // The game's own buttons, asked for once a frame at most: the box asks for the lines and the buttons in turn.
        private ButtonInfo[] Own()
        {
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _own = base.GetButtons();
                _atBay = _own != null && _own.Length > 0 && _own[0] != null && _own[0].name == "RepairVehicle";   // seated in a bay of the vehicle's kind
            }
            return _own;
        }

        public new ButtonInfo[] GetButtons()
        {
            if (FrameGuard.Off)
            {
                return base.GetButtons();   // (the Garage switched off after its faults: the game's own buttons)
            }
            try
            {
                ButtonInfo[] own = Own();
                return _atBay ? GarageBay.With(own) : own;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (ButtonInfo[])null);
            }
        }

        public new LabelInfo GetSecondLineLeftLabel()
        {
            if (FrameGuard.Off)
            {
                return Guard.Blank();
            }
            try
            {
                Own();
                return _atBay ? GarageBay.Line(false) : null;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }

        public new LabelInfo GetSecondLineRightLabel()
        {
            if (FrameGuard.Off)
            {
                return Guard.Blank();
            }
            try
            {
                Own();
                return _atBay ? GarageBay.Line(true) : null;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }
    }

    /// <summary>
    /// The repair bay's side of car ownership and of the custom shop: the bay's panel gets a second line (what is due
    /// and its cost) and a "Garage" button on the game's confirm key, which opens the workshop's list (the service, the
    /// custom shop, "My vehicles"). Cost: three compares on the Garage's quarter-second timer and car ownership's own
    /// <see cref="Tick"/> unless the list is open (one is the wash's, <see cref="GarageWash"/>).
    /// </summary>
    public static class GarageBay
    {
        private const string ButtonKey = "reallife_garage_button_workshop";
        private const int RowService = 1;
        private const int RowShop = 2;
        private const int RowVehicles = 3;

        public static IModLogger Logger;

        private static readonly object Owner = new object();
        private static readonly Action OnButton = OpenClicked;
        private static readonly Action<int> OnPick = Picked;
        private static readonly Action OnClosed = Closed;
        private static RepairBayOverlay _ours;
        private static GasStationOverlay _theirs;
        private static ButtonInfo _button;
        private static ButtonInfo _buttonOff;
        private static VehicleController _car;      // the car the list was opened for
        private static string _closing;
        private static string _left;
        private static string _right;
        private static int _lineFrame = -1;
        private static bool _told;
        private static bool _failed;

        public static bool Installed => _ours != null;

        public static bool ListOpen => GarageList.OpenFor(Owner);

        /// <summary>Four times a second: the panel in the game's field, a stopped wash ended, the list closed when due.</summary>
        public static void Tick(GameManager gm)
        {
            if (_ours == null && !_failed)
            {
                Install();
            }
            if (_ours != null)
            {
                GarageWash.Watch(gm);
            }
            if (!ListOpen)
            {
                return;
            }
            string why = _car == null || gm.selectedVehicle != _car ? "the player left the car"
                : _car.CurrentSpeed > 1.5f ? "the car moved off"
                : !Up() ? "the car left the bay"
                : GarageList.MenuUp ? "another screen came up" : null;
            if (why != null)
            {
                CloseList(why);
            }
        }

        // True while the game's box is up with the bay's panel.
        private static bool Up()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return OverlayUI.IsVisible && uis != null && uis.overlayUI != null && uis.overlayUI.GetCurrentOverlayType == typeof(RepairBayOverlay);
        }

        private static void Install()
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                OverlayUI box = uis != null ? uis.overlayUI : null;
                if (box == null || box.gasStation == null || OverlayUI.IsVisible)
                {
                    return;   // not there yet, or a panel is showing: the object in use is not changed under it
                }
                _theirs = box.gasStation;
                _ours = new RepairBayOverlay();
                box.gasStation = _ours;
                _button = new ButtonInfo("RealLifeWorkshop", ButtonKey, "gray", Guard.Wrap(OnButton), PlayerAction.Confirm, true);
                _buttonOff = new ButtonInfo("RealLifeWorkshop", ButtonKey, "gray", Guard.Wrap(OnButton), PlayerAction.Confirm, false);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (bay): the game's panel for filling stations and repair bays is extended (the game's own object is kept to be put back): PASS.");
                }
            }
            catch (Exception e)
            {
                _failed = true;
                _ours = null;
                Logger?.Error(e);
                Logger?.Warn("Garage: the game's repair-bay panel could not be extended, so the workshop has no button there this session. \"My vehicles\" and the Garage contact work as they do.");
            }
        }

        /// <summary>The game's own object back in its field and the list closed, when the Garage goes or switches itself off.</summary>
        public static void Remove()
        {
            try
            {
                CloseList("the Garage is going");
                UIs uis = InstanceBehavior<UIs>.Instance;
                OverlayUI box = uis != null ? uis.overlayUI : null;
                if (box != null && _ours != null && _theirs != null && ReferenceEquals(box.gasStation, _ours))
                {
                    if (Up())
                    {
                        OverlayUI.Hide(_ours);   // the game shows its own again the next time a bay or a pump is driven into
                    }
                    box.gasStation = _theirs;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _ours = null;
            _theirs = null;
            _button = null;
            _buttonOff = null;
            _car = null;
            _told = false;
            _failed = false;
            GarageWash.Forget();
        }

        /// <summary>The game's buttons in a bay, and the workshop's after them if the workshop takes this car.</summary>
        internal static ButtonInfo[] With(ButtonInfo[] own)
        {
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                VehicleController car = gm != null ? gm.selectedVehicle : null;
                GarageWash.Ready(car);   // before "Wash" can be pressed: a car without the wash's parts gets stand-ins
                if (_button == null || car == null || !Takes(car))
                {
                    return own;
                }
                var all = new ButtonInfo[own.Length + 1];
                Array.Copy(own, all, own.Length);
                all[own.Length] = Mathf.Approximately(car.CurrentSpeed, 0f) ? _button : _buttonOff;   // as the game's own: only standing still
                if (!_told && Meta.Dev.Verbose)
                {
                    _told = true;
                    Logger?.Info("Garage check (bay): in a repair bay with " + car.name + ": the game's panel has its " + own.Length
                                 + " button(s) and 'Garage' on the game's confirm key"
                                 + (_left != null ? ", second line '" + _left + (_right != null ? " " + _right : string.Empty) + "'" : string.Empty) + ": PASS.");
                }
                return all;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return own;
            }
        }

        /// <summary>The panel's second line in a bay: what is due on the left, what it costs on the right. Null leaves it out.</summary>
        internal static LabelInfo Line(bool right)
        {
            try
            {
                if (_lineFrame != Time.frameCount)
                {
                    _lineFrame = Time.frameCount;
                    _left = null;
                    _right = null;
                    var gm = InstanceBehavior<GameManager>.Instance;
                    CarOwnership owner = CarOwnership.Instance;
                    if (owner != null && gm != null && gm.selectedVehicle != null)
                    {
                        owner.BayLine(gm.selectedVehicle, out _left, out _right);
                    }
                }
                string words = right ? _right : _left;
                return words != null ? new LabelInfo(words, right ? WorldPanel.Yellow() : WorldPanel.White(), false) : null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        // A car the service or the custom shop takes.
        private static bool Takes(VehicleController car)
        {
            CarOwnership owner = CarOwnership.Instance;
            return (owner != null && owner.Looks(car)) || (GarageDirector.Working && Settings.Garage && GarageShop.Refusal(car) == null);
        }

        /// <summary>Has the game write its panel again if the bay's panel is up (after a service).</summary>
        public static void Refresh()
        {
            try
            {
                _told = false;
                if (_ours != null && Up())
                {
                    OverlayUI.Show(_ours);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void OpenClicked()
        {
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                VehicleController car = gm != null ? gm.selectedVehicle : null;
                if (car == null || ListOpen || !Takes(car) || (GarageDirector.Working && GarageDirector.Instance.Shop.IsOpen))
                {
                    return;
                }
                float began = Time.realtimeSinceStartup;
                TravelList list = GarageList.Take(Owner, false);
                if (list == null)
                {
                    // The game's window could not be copied: the button does the one thing that is due.
                    Logger?.Info("Garage check (workshop): the game's list could not be made; the service is ordered directly if anything is due: FAIL.");
                    CarOwnership.Instance?.OrderService(car);
                    return;
                }
                _car = car;
                int entries = Fill(list, car);
                _closing = null;
                list.Open(OnPick, OnClosed);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (workshop): the game's list is open for " + car.name + " with " + entries + " entries ("
                                 + (CarOwnership.Instance != null ? CarOwnership.Instance.DueWords(car) : "car ownership is off")
                                 + "); " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                CloseList("an error");
            }
        }

        private static int Fill(TravelList list, VehicleController car)
        {
            string name = car.vehicleType != null ? car.vehicleType.vehicleTypeName.GetLocalization() : car.name;
            list.Begin(GarageText.T("reallife_garage_workshop_title", "car", name), GarageText.T("reallife_garage_workshop_info"));
            int entries = 0;
            CarOwnership owner = CarOwnership.Instance;
            if (owner != null && owner.Looks(car))
            {
                owner.ListEntry(list, RowService, car);
                entries++;
            }
            if (GarageDirector.Working && Settings.Garage && GarageShop.Refusal(car) == null)
            {
                list.Add(RowShop, GarageText.T("reallife_garage_workshop_shop"), GarageText.T("reallife_garage_workshop_shop_detail"), "reallife_garage_list_open", true);
                entries++;
            }
            if (GarageDirector.Working && Settings.Garage)
            {
                list.Add(RowVehicles, GarageText.T("reallife_garage_my_vehicles"), GarageText.T("reallife_garage_workshop_vehicles_detail"), "reallife_garage_list_open", true);
                entries++;
            }
            return entries;
        }

        private static void Picked(int id)
        {
            try
            {
                VehicleController car = _car;
                if (car == null)
                {
                    CloseList("the car is gone");
                    return;
                }
                if (id == RowService)
                {
                    bool ordered = CarOwnership.Instance != null && CarOwnership.Instance.OrderService(car);
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Garage check (choice): the service, from the workshop's list: "
                                     + (ordered ? "ordered and paid: PASS" : "not ordered (nothing is due, the money is short, or the car left the bay)") + ".");
                    }
                    if (ordered)
                    {
                        CloseList("the service was ordered");
                    }
                }
                else if (id == RowShop)
                {
                    CloseList("the custom shop was chosen");
                    bool opened = GarageDirector.Working && GarageDirector.Instance.Shop.Open(car);
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Garage check (choice): the custom shop, from the workshop's list: " + (opened ? "opened: PASS" : "NOT opened: FAIL") + ".");
                    }
                }
                else if (id == RowVehicles)
                {
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Garage check (choice): 'My vehicles', from the workshop's list.");
                    }
                    GarageSummary.Open();   // takes the window over: this list's closing runs first
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                CloseList("an error");
            }
        }

        /// <summary>Closes the workshop's list if it is open, saying why in the log.</summary>
        public static void CloseList(string why)
        {
            if (ListOpen)
            {
                _closing = why;
                GarageList.Close();
            }
        }

        private static void Closed()
        {
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (workshop): closed (" + (_closing ?? "cancelled by the player, or another list of the Garage was opened") + ").");
            }
            _closing = null;
            _car = null;
        }
    }

    /// <summary>
    /// The one window the Garage's lists share, a copy of the game's subway station list (<see cref="TravelList"/>) made
    /// when first wanted: taking it closes whichever list had it.
    /// </summary>
    public static class GarageList
    {
        // Where the window's left edge stands when aside, and its width, in the HUD's units (3,840 across).
        private const float AsideLeft = 780f;
        private const float Width = 1100f;

        private static TravelList _list;
        private static object _owner;
        private static bool _failed;

        public static bool IsOpen => _list != null && _list.IsOpen;

        /// <summary>True while a game screen is up that a list must not stand under: the phone's menu, the map, the pause menu, a question.</summary>
        public static bool MenuUp => FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen || Scenes.MainMenu.Options.IsVisible;

        /// <summary>True while the window is open and it is this owner's list that is in it.</summary>
        public static bool OpenFor(object owner)
        {
            return IsOpen && ReferenceEquals(_owner, owner);
        }

        /// <summary>The window for this owner to fill, aside (left of the middle) or in the middle; null if it could not be copied.</summary>
        public static TravelList Take(object owner, bool aside)
        {
            if (_failed)
            {
                return null;
            }
            if (_list == null || !_list.Alive)
            {
                _list = TravelList.Make("RealLifeOverhaul Garage");
                if (_list == null)
                {
                    _failed = true;
                    return null;
                }
                RectTransform made = _list.Window;
                made.sizeDelta = new Vector2(Width, made.sizeDelta.y);   // an entry says what a part does and costs: wider than a station's name
                _list.EntryShape(150f, true);
            }
            if (_list.IsOpen && !ReferenceEquals(_owner, owner))
            {
                _list.Close();
            }
            _owner = owner;
            RectTransform window = _list.Window;
            // Aside: clear of the game's task panel (740 wide at the left), phone (628 at the right) and item panel.
            Vector2 at = aside ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            window.anchorMin = window.anchorMax = window.pivot = at;
            window.anchoredPosition = new Vector2(aside ? AsideLeft : 0f, 60f);   // 60 up: the item panel is on screen while a car is sat in
            return _list;
        }

        public static void Close()
        {
            if (IsOpen)
            {
                _list.Close();
            }
        }

        /// <summary>The window gone with the Garage (the game's interface is rebuilt with the city).</summary>
        public static void Destroy()
        {
            try
            {
                _list?.Destroy();
            }
            catch (Exception e)
            {
                GarageDirector.Logger?.Error(e);
            }
            _list = null;
            _owner = null;
            _failed = false;
        }
    }
}
