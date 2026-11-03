using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Every list window of the mod's (TravelList, and DeskList which stands on it: the station desk, custody, the
    /// driving school and its theory test, the hotel and holidays, cooking, the dispatch board, design stages, the tender
    /// bill, the garage, the bus stops, the utilities desk) drawn as the kit's picker while the interface and its switch
    /// are on. The list tells its face at each opening (TravelList.Face); the face takes it, so the game's window stays off
    /// and the picker reads the list's own entries, picks through the list's own pick (a greyed entry refused as its
    /// greyed button is) and closes through its own close. A locked list (custody, a test) has no close and no Back: Escape
    /// opens the game's menu as it did over the window. The scenes that are conversations (a hearing, a fame or an office
    /// scene, an interview, a venture's or a pitch's meeting) are the conversations' own and are left to them. The picker
    /// stands on a canvas of the kit's just over the canvas the window stands on, so it follows a list the time skip's
    /// screen holds (custody) and comes and goes with the list. Cost: nothing while no list is open; while one is, a
    /// number compared a frame and a layout a change.
    /// </summary>
    public sealed class WorldPicker : ITravelFace
    {
        /// <summary>The scene windows the conversations draw (Ui/Dialog), by their objects' names: never taken here.</summary>
        private static readonly string[] Scenes =
        {
            "RealLifeOverhaul Civic Office", "RealLifeOverhaul Ventures", "RealLifeOverhaul Standing", "RealLifeOverhaul Justice", "RealLifeOverhaul Interview",
            "RealLifeOverhaul Venture",
        };

        public static readonly WorldPicker Instance = new WorldPicker();

        private readonly WorldPickerScreen _screen = new WorldPickerScreen();
        private TravelList _list;
        private RectTransform _rect;
        private Transform _over;     // the canvas the window stood on when the picker's canvas was made over it
        private UiHost _host;
        private WorldPickerFollow _follow;
        private int _taken;
        private string _state = "waiting for a list";

        /// <summary>What the picker is doing, for the interface's line at load.</summary>
        public static string State => Instance._list != null ? "drawing '" + Instance._list.Name + "'"
            : Instance._state + (Instance._taken > 0 ? ", " + Instance._taken + " list(s) drawn" : string.Empty);

        /// <summary>The list on the picker now, for the tests; null when none.</summary>
        public static TravelList Showing => Instance._list;

        /// <summary>The screen the picker runs, for the tests.</summary>
        internal static WorldPickerScreen Screen => Instance._screen;

        /// <summary>True for a scene window the conversations draw, which the picker leaves to them.</summary>
        public static bool Theirs(string windowName) => windowName != null && Array.IndexOf(Scenes, windowName) >= 0;

        public bool Take(TravelList list)
        {
            if (!UiDirector.On || !UiScreenSwitches.On(WorldKits.PickerId) || list == null || list.Window == null || Theirs(list.Name))
            {
                return false;
            }
            if (!Stand(list.Window.parent))
            {
                return false;   // no canvas to stand on: the window as before
            }
            _list = list;
            _taken++;
            _screen.Show(list);
            _host.Bind(_screen, UiGame.Instance, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight, _screen.Title, _screen.Icon);
            _host.View.Scale = UiTokens.CanvasPerPx;
            _rect.gameObject.SetActive(true);
            _rect.SetAsLastSibling();
            _host.Show();
            return true;
        }

        public void Gone(TravelList list)
        {
            if (!ReferenceEquals(list, _list))
            {
                return;
            }
            _list = null;
            _screen.Show(null);
            if (_host != null)
            {
                _host.Hide();
            }
            if (_rect != null)
            {
                _rect.gameObject.SetActive(false);
            }
        }

        /// <summary>The window again for a list the picker draws (its switch went off, the interface went off), and no list taken after.</summary>
        public static void Give(string why)
        {
            if (ReferenceEquals(TravelList.Face, Instance))
            {
                TravelList.Face = null;
            }
            TravelList list = Instance._list;
            if (list != null)
            {
                list.Release();   // the list tells Gone, and its window shows as it would have
            }
            Instance.Drop();
            Instance._state = "the windows as before (" + why + ")";
        }

        // The picker's canvas just over the one the window stands on, made again when the window moved (MoveTo).
        private bool Stand(Transform parent)
        {
            if (_rect != null && _over == parent && _over != null)
            {
                return true;
            }
            Drop();
            if (parent == null)
            {
                return false;
            }
            _rect = UiOverCanvas.Make(parent, "RealLife Interface Picker");
            if (_rect == null)
            {
                return false;
            }
            _over = parent;
            _host = _rect.gameObject.AddComponent<UiHost>();
            _host.Failed = Failed;
            _follow = _rect.gameObject.AddComponent<WorldPickerFollow>();
            _follow.Picker = this;
            return true;
        }

        // A fault in the picker's screen (its host counted it): the window for the rest of the session.
        private void Failed(Exception e)
        {
            UiDirector.Logger?.Warn("UI: " + UiText.T("reallife_ui_note_fallback") + " (the picker, " + (e != null ? e.GetType().Name : "?") + ")");
            Give("a fault");
            _state = "the windows as before after a fault of the picker";
        }

        private void Drop()
        {
            if (_rect != null)
            {
                UnityEngine.Object.Destroy(_rect.gameObject);
            }
            _rect = null;
            _host = null;
            _follow = null;
            _over = null;
        }

        // A frame while the picker shows: the window moved to another canvas (the time skip's) and the picker goes with it.
        internal void Follow()
        {
            TravelList list = _list;
            if (list == null)
            {
                return;
            }
            if (!list.IsOpen || !list.Alive)
            {
                Gone(list);
                return;
            }
            Transform parent = list.Window != null ? list.Window.parent : null;
            if (parent != null && parent != _over)
            {
                _list = null;
                if (Take(list))
                {
                    _taken--;   // the same list on its new canvas
                }
                else
                {
                    list.Release();
                }
            }
        }
    }

    /// <summary>On the picker's canvas, so it runs only while the picker shows: the list's window followed as it moves. Cost: two reference comparisons a frame.</summary>
    public sealed class WorldPickerFollow : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "WorldPickerFollow", typeof(WorldPickerFollow));
        private static readonly int HotUpdate = HotSpots.Slot("Ui: WorldPickerFollow.Update");

        internal WorldPicker Picker;

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                Picker?.Follow();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                WorldPicker.Give("a fault");
            }
        }
    }
}
