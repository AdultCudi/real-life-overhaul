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
    /// The game's own panel at a filling station or a repair bay (UI.Overlays.GasStationOverlay, shown in the game's
    /// box over a place: "Repair [F]" and "Wash [P]" in a bay), with one more line and one more button in a bay.
    ///
    /// The game keeps one object of that class in a public field (OverlayUI.gasStation) and shows it through the
    /// interface IOverlay. This class derives from the game's and implements the interface again, so that what the
    /// box asks through the interface (the second line, the buttons) is answered here, while everything the game
    /// does with its own object (which station, the repair, the wash, the refuelling) goes on in the game's code,
    /// untouched. <see cref="GarageBay"/> puts one of these in the game's field while the box is down, and puts the
    /// game's own back when the Garage goes.
    /// </summary>
    public sealed class RepairBayOverlay : GasStationOverlay, IOverlay
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "RepairBayOverlay", typeof(RepairBayOverlay));   // the game's calls of it (Core/Guard)

        private int _frame = -1;
        private bool _atBay;
        private ButtonInfo[] _own;

        // The game's own buttons, asked for once a frame at most (the box asks for the lines and the buttons in turn).
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
                return (ButtonInfo[])null;
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
    /// The repair bay's side of car ownership and of the custom shop, in the game's own interface:
    ///
    /// - the game's panel in a bay gets a second line (what is due on this car and what it costs, from
    ///   <see cref="CarOwnership"/>) and a third button, "Garage", on the game's confirm key (its interact keys
    ///   are the game's own repair and wash; the key is written on the button by the game, as on the others);
    /// - that button opens the game's list (<see cref="GarageList"/>) with what the workshop does for this car: the
    ///   service and whatever else is due, the custom shop, and "My vehicles".
    ///
    /// The cost: the Garage's quarter-second timer and car ownership's own call <see cref="Tick"/> (either keeps the
    /// panel going should the other have switched itself off), which is two compares unless the list is open. The game asks for the line and the button when it shows or refreshes its panel, never per frame.
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

        /// <summary>True while the workshop's list is open.</summary>
        public static bool ListOpen => GarageList.OpenFor(Owner);

        // ---------------------------------------------------------------- the game's panel

        /// <summary>
        /// Four times a second. Puts the extended panel in the game's field the first time the box is down, and
        /// closes the workshop's list when the car has left the bay or another screen has come up.
        /// </summary>
        public static void Tick(GameManager gm)
        {
            if (_ours == null && !_failed)
            {
                Install();
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
                Close(why);
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
                    return;   // not there yet, or the game is showing a panel: the object in use is not changed under it
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

        /// <summary>The game's own object back in its field, the list closed. When the Garage goes, or switches itself off.</summary>
        public static void Remove()
        {
            try
            {
                Close("the Garage is going");
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
        }

        /// <summary>The game's buttons in a bay, and the workshop's after them if this car is one the workshop takes.</summary>
        internal static ButtonInfo[] With(ButtonInfo[] own)
        {
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                VehicleController car = gm != null ? gm.selectedVehicle : null;
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
                    Logger?.Info("Garage check (bay): in a repair bay with " + car.name + ": the game's panel has its " + own.Length + " button(s) and 'Garage' on the game's confirm key"
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

        /// <summary>Has the game write its panel again (the second line and the buttons), if the bay's panel is up: after a service.</summary>
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

        // ---------------------------------------------------------------- the workshop's list

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
                    // The game's window could not be copied (one "Game UI:" line says why): the button does the one thing that is due.
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
                    Logger?.Info("Garage check (workshop): the game's list is open for " + car.name + " with " + entries + " entries (" + (CarOwnership.Instance != null ? CarOwnership.Instance.DueWords(car) : "car ownership is off")
                                 + "); " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Close("an error");
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
                    Close("the car is gone");
                    return;
                }
                if (id == RowService)
                {
                    bool ordered = CarOwnership.Instance != null && CarOwnership.Instance.OrderService(car);
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Garage check (choice): the service, from the workshop's list: " + (ordered ? "ordered and paid: PASS" : "not ordered (nothing is due, the money is short, or the car left the bay)") + ".");
                    }
                    if (ordered)
                    {
                        Close("the service was ordered");
                    }
                }
                else if (id == RowShop)
                {
                    Close("the custom shop was chosen");
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
                    GarageSummary.Open();   // (takes the window over: this list's closing runs first)
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Close("an error");
            }
        }

        /// <summary>Closes the workshop's list if it is open, saying why in the log.</summary>
        public static void CloseList(string why)
        {
            Close(why);
        }

        private static void Close(string why)
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
    /// The one window the Garage's lists share: a copy of the window the game lists its subway stations in
    /// (<see cref="TravelList"/>), made the first time one of them is wanted and used again by all of them (the
    /// workshop's list in a bay, the custom shop, "My vehicles", a two-wheeler stand). Only one is ever open: taking
    /// the window closes whichever had it, and that one's own closing runs.
    /// </summary>
    public static class GarageList
    {
        /// <summary>Where the window's left edge stands when it is aside, and how wide it is, in the HUD's units (3,840 across).</summary>
        public const float AsideLeft = 780f;
        public const float Width = 1100f;

        private static TravelList _list;
        private static object _owner;
        private static bool _failed;

        public static bool IsOpen => _list != null && _list.IsOpen;

        /// <summary>True while one of the game's own screens is up that a list must not stand under: the phone's menu, the map, the pause menu and its options, a question.</summary>
        public static bool MenuUp => FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen || Scenes.MainMenu.Options.IsVisible;

        /// <summary>True while the window is open and it is this owner's list that is in it.</summary>
        public static bool OpenFor(object owner)
        {
            return IsOpen && ReferenceEquals(_owner, owner);
        }

        /// <summary>
        /// The window, for this owner to fill and open. <paramref name="aside"/>: left of the middle, beside the game's
        /// task panel (the custom shop: the car is looked at, to the right of it), otherwise in the middle. Null if the
        /// game's window could not be copied (one "Game UI:" line says why).
        /// </summary>
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
            // Aside: clear of the game's task panel (740 wide at the left edge), of its phone (628 wide at the right) and of its
            // item panel (bottom middle), which stand on other canvases of the same order and so may draw over it.
            Vector2 at = aside ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            window.anchorMin = window.anchorMax = window.pivot = at;
            window.anchoredPosition = new Vector2(aside ? AsideLeft : 0f, 60f);   // (60 up: the item panel is on screen while a car is sat in)
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
