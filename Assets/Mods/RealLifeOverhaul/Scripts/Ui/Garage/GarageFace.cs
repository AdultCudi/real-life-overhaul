using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Draws the Garage's list window (the custom shop, the workshop, "My vehicles", a stand's rack) as the garage strip while
    /// the interface and its switch are on. The picker offers every list to this first: it takes the Garage's and leaves
    /// the rest to the picker. The list's window stays off while the strip shows, the list reads through its own entries
    /// and picks and closes through its own calls, so each owner's rules stay its own. The strip stands on a canvas of the
    /// kit's just over the one the window stands on; it is given back, the window as before, when the interface or its switch
    /// goes off or the screen faults (the screen then stays off for the session). Cost: nothing while no Garage list is open.
    /// </summary>
    public sealed class GarageFace : ITravelFace
    {
        public const string Id = "garage_shop";
        private const string WindowName = "RealLifeOverhaul Garage";

        public static readonly GarageFace Instance = new GarageFace();

        private readonly GarageShopScreen _screen = new GarageShopScreen();
        private TravelList _list;
        private RectTransform _rect;
        private Transform _over;
        private UiHost _host;
        private bool _broken;
        private int _taken;
        private string _state = "waiting for a list";

        /// <summary>What the strip is doing, for the interface's line at load.</summary>
        public static string State => Instance._list != null ? "drawing the Garage's list" : Instance._state + (Instance._taken > 0 ? ", " + Instance._taken + " list(s) drawn" : string.Empty);

        /// <summary>The list on the strip now, for the tests; null when none.</summary>
        public static TravelList Showing => Instance._list;

        /// <summary>The screen the strip runs, for the tests.</summary>
        internal static GarageShopScreen Screen => Instance._screen;

        /// <summary>True for the Garage's list window, which every owner in the Garage shares.</summary>
        public static bool Owns(TravelList list) => list != null && list.Name == WindowName && GarageList.Kind != GarageListKind.None;

        public bool Take(TravelList list)
        {
            if (_broken || !UiDirector.On || !UiScreenSwitches.On(Id) || !Owns(list) || list.Window == null)
            {
                return false;
            }
            if (!Stand(list.Window.parent))
            {
                return false;   // no canvas to stand on: the picker or the window as before
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

        /// <summary>The window again for a list the strip draws (its switch went off, the interface went off).</summary>
        public void Give(string why)
        {
            TravelList list = _list;
            if (list != null)
            {
                list.Release();   // the list tells Gone, and its window shows as it would have
            }
            Drop();
            _state = "the window as before (" + why + ")";
        }

        /// <summary>A new city: a strip that faulted in the last one is tried again.</summary>
        public void Rearm()
        {
            _broken = false;
        }

        // The strip's canvas just over the one the window stands on, made again when the window moved to another.
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
            _rect = UiOverCanvas.Make(parent, "RealLife Interface Garage");
            if (_rect == null)
            {
                return false;
            }
            _over = parent;
            _host = _rect.gameObject.AddComponent<UiHost>();
            _host.Failed = Failed;
            return true;
        }

        // A fault in the strip's screen (its host counted it): the picker or the window for the rest of the session.
        private void Failed(Exception e)
        {
            UiDirector.Logger?.Warn("UI: " + UiText.T("reallife_ui_note_fallback") + " (the garage strip, " + (e != null ? e.GetType().Name : "?") + ")");
            _broken = true;
            Give("a fault");
            _state = "the window as before after a fault of the strip";
        }

        private void Drop()
        {
            if (_rect != null)
            {
                UnityEngine.Object.Destroy(_rect.gameObject);
            }
            _rect = null;
            _host = null;
            _over = null;
        }
    }
}
