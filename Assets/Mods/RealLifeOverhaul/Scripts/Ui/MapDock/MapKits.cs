using System;
using System.Collections;
using City.CityMap;
using RealLifeOverhaul.Districts;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The city map's panels on the RLO interface: its filters (with the picked place's card, the district view, the route strip, the layers and the map's buttons) and its list
    /// of subway stations, each through the host (UiGameHost) like any game screen, each with its own switch. The game's windows stay up and live underneath, their leaves
    /// hidden; every press is the game's own widget. The district view's page is a page of the filters' dock, with its own switch (a view with that off keeps its old window).
    /// The mod's own choices of a place (MapPicker) are drawn on the HUD's modal piece while they are open, the same dock with the choice's entries for its rows.
    /// Cost: nothing while the map is shut; while it is open, a look at the game's window four times a second and a layout when it changed.
    /// </summary>
    public static class MapKits
    {
        public const string FiltersId = "map_filters", SubwayId = "map_subway", PickerId = "map_picker", DistrictId = "map_district";

        private static readonly UiGameHost Filters = new UiGameHost(new MapFiltersGame(false));
        private static readonly UiGameHost Subway = new UiGameHost(new MapFiltersGame(true));
        private static readonly UiGameHost[] All = { Filters, Subway };

        /// <summary>Once a second while the interface is on: each host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            DistrictView.KitDraws = DistrictOnKit;
            MapPicker.KitShow = PickerShow;
            MapPicker.KitHide = PickerHide;
            if (UiScreenSwitches.On(FiltersId))
            {
                MapRouteTag.On();
            }
            else
            {
                MapRouteTag.Off();
            }
            for (int i = 0; i < All.Length; i++)
            {
                if (UiScreenSwitches.On(All[i].Id))
                {
                    All[i].Install();
                }
                else
                {
                    All[i].Remove("its switch is off");
                }
            }
            MapSavedGame.Look();
        }

        /// <summary>The map's windows the game's own again.</summary>
        public static void Remove(string why)
        {
            for (int i = 0; i < All.Length; i++)
            {
                All[i].Remove(why);
            }
            MapPicker.KitShow = null;
            MapPicker.KitHide = null;
            MapRouteTag.Off();
            if (_pickerShown != null)
            {
                MapPicker open = _pickerShown;
                PickerHide(open);
                open.KitGiveBack();   // the choice goes on in the game's own window
            }
        }

        /// <summary>A new city: the windows are looked for again.</summary>
        public static void Rearm()
        {
            for (int i = 0; i < All.Length; i++)
            {
                All[i].Rearm();
            }
            MapStoreGame.Forget();
            MapSavedGame.Forget();
            MapRouteTag.Clear();
        }

        /// <summary>The windows' states for the interface's line at load.</summary>
        public static string States() => "city map filters (" + Filters.State + "), subway list (" + Subway.State + ")";

        /// <summary>True while the kit draws the map's filters, for the tests.</summary>
        internal static bool FiltersShowing => Filters.Showing;

        /// <summary>True while a docked panel of the map (filters, subway list, a choice of a place) stands on the screen: the HUD's pieces keep clear of it.</summary>
        internal static bool DockShowing => CityMap.IsOpen && (Filters.Showing || Subway.Showing || _pickerShown != null);

        /// <summary>True while the kit draws the map's subway list, for the tests.</summary>
        internal static bool SubwayShowing => Subway.Showing;

        private static MapPicker _pickerShown;
        private static MapLive _pickerScreen;

        // A choice of a place opens: the HUD's modal piece takes the dock, pointer only (the stick and the keys pan the map), and the choice's own window stays unseen.
        private static bool PickerShow(MapPicker picker)
        {
            HudHost hud = HudHost.Instance;
            if (hud == null || !UiDirector.On || !UiScreenSwitches.On(PickerId) || !UiScreenSwitches.On("hud"))
            {
                return false;
            }
            try
            {
                _pickerScreen = new MapLive(picker);
                UiHost host = hud.Modal(_pickerScreen, picker.TitleText, UiIcon.MapPin);
                host.PointerOnly = true;
                _pickerShown = picker;
                return true;
            }
            catch (Exception e)
            {
                _pickerScreen = null;
                UiDirector.Logger?.Error(e);
                UiDirector.Logger?.Warn("UI: the city map's choice of a place could not be drawn on the RLO interface (above); the game's own window is used for it.");
                return false;
            }
        }

        // The choice closed, by any means: the modal piece ends, if it is still the choice's.
        private static void PickerHide(MapPicker picker)
        {
            if (!ReferenceEquals(_pickerShown, picker))
            {
                return;
            }
            HudHost hud = HudHost.Instance;
            if (hud != null && hud.ModalHost != null && ReferenceEquals(hud.ModalHost.Screen, _pickerScreen))
            {
                hud.EndModal();
            }
            _pickerShown = null;
            _pickerScreen = null;
        }

        /// <summary>True while the kit draws a choice of a place, for the tests.</summary>
        internal static bool PickerShowing => _pickerShown != null && _pickerShown.KitOn;

        // The district view's page stands in the filters' dock only while the kit has that window and the page's own switch is on.
        private static bool DistrictOnKit() => UiScreenSwitches.On(DistrictId) && Filters.Bound;

        /// <summary>Runs something once the city map has shut (it waits at most three seconds for that), for a thing the game cannot do over the map: a call, a dialogue.</summary>
        internal static void AfterMap(Action work)
        {
            UiDirector director = UiDirector.Instance;
            if (director != null && work != null)
            {
                director.StartCoroutine(Guard.Routine(WaitShut(work)));
            }
        }

        private static IEnumerator WaitShut(Action work)
        {
            float until = Time.unscaledTime + 3f;
            while (CityMap.IsOpen && Time.unscaledTime < until)
            {
                yield return null;
            }
            if (!CityMap.IsOpen)
            {
                yield return null;
                work();
            }
        }
    }
}
