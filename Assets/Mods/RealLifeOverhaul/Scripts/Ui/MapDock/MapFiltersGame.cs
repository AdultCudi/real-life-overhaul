using Helpers;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The city map's left dock as the host binds it: the game's filter window (CityMapFilters, which the game switches on while its map is open) and, in subway mode,
    /// its list of stations (CityMapSubwayStations). Both stay active underneath; only their own leaves are hidden, the kit mirrors their lines and presses their own
    /// widgets. The panels are pointer driven, as the game's are: the stick and the movement keys pan and turn the map, so the kit takes no key from it, and Esc is the
    /// game's own close.
    /// </summary>
    internal sealed class MapFiltersGame : IUiGameScreen, IUiGameWay, IUiGameDraggable
    {
        private readonly bool _subway;

        public MapFiltersGame(bool subway)
        {
            _subway = subway;
        }

        public string Id => _subway ? MapKits.SubwayId : MapKits.FiltersId;

        // CityMapFilters is the game's draggable window; its stations' list is not one.
        public string WindowId => _subway ? null : "CityMapFilters";

        public string Name => _subway ? "the city map's subway list" : "the city map's filters";

        public string Title => UiText.T(_subway ? "common_subwaystation" : "reallife_map_filters_title");

        public UiIcon Icon => _subway ? UiIcon.Train : UiIcon.MapTrifold;

        public UiGameKind Kind => UiGameKind.Panel;

        public bool Keys => false;

        public bool Exclusive => false;

        public bool Blocks => false;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null)
            {
                return null;
            }
            return _subway ? uis.cityMapSubwayStations : (Component)uis.mapFilters;
        }

        public string Check(Component found)
        {
            if (_subway)
            {
                return GameContract.MapStationEntries.Field == null ? "its stations' list is not there" : null;
            }
            return GameContract.MapFilterEntries.Field == null ? "its table of lines is not there"
                : GameContract.MapFilterAll.Field == null ? "its master switch is not there"
                : GameContract.MapLineLabel.Field == null || GameContract.MapLineFocus.Field == null || GameContract.MapLineIcon.Field == null ? "a line's parts are not there"
                : GameContract.MapGroupLabel.Field == null || GameContract.MapGroupLines.Field == null ? "a group's parts are not there"
                : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform panel = found.transform;
            for (int i = 0; i < panel.childCount; i++)
            {
                conceal.Add(panel.GetChild(i));
            }
            conceal.Add(panel.GetComponent<Graphic>());
            if (!_subway)
            {
                // The game's building box would stand beside the dock's picked block for the same building. Alpha and raycast only: the box stays active, so its
                // buttons (set destination, taxi) and its options panel keep answering the dock, and the host gives it back when it hides.
                conceal.Add(CardPanel());
            }
        }

        private static Transform CardPanel()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            System.Reflection.FieldInfo field = GameContract.ResumePanel.Field;
            return uis != null && uis.buildingResume != null && field != null ? field.GetValue(uis.buildingResume) as Transform : null;
        }

        public IUiScreen Make(Component found) => new MapLive(found, _subway);
    }
}
