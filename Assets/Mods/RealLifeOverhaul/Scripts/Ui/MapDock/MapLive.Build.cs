using RealLifeOverhaul.Districts;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the live dock says now, put together from the game's state: the dock's page (the filters, the picked place's card or the district view), the route strip when
    /// Directions is chosen, the layers panel beside a card or a view, and the map's buttons. Laid out when the game's state changed, never a frame.
    /// </summary>
    internal sealed partial class MapLive
    {
        private readonly MapDistrictModel _districtModel = new MapDistrictModel();
        private MapDistrictModel _district;   // non-null while the dock holds the district view
        private int _districtLayer = -1;      // the layer the dock last followed

        private static DistrictView DistrictOwner => DistrictView.Instance;

        private void Build()
        {
            _model.Filters = null;
            _model.Place = null;
            _model.District = null;
            _model.Route = null;
            _model.Layers = null;
            _district = null;
            if (_picker != null)
            {
                BuildPicker();
                _model.Filters = _panel;
                BuildButtons();
                return;
            }
            if (_subway)
            {
                BuildSubway();
                _model.Filters = _panel;
                BuildButtons();
                return;
            }
            FollowDistrict();
            BuildFilters();   // always: the layers panel and the counts read the lines, and the picked block reads the game's card
            if (_dock == Dock.District && DistrictOwner != null && DistrictOwner.KitFill(_districtModel))
            {
                _district = _districtModel;
                _model.District = _district;
                BuildLayers();
                _model.Layers = _layers;
            }
            else if (_dock == Dock.Place && BuildPlace())
            {
                _model.Place = _place;
                BuildLayers();
                _model.Layers = _layers;
                if (_routeShown)
                {
                    BuildRoute();
                    _model.Route = _route;
                }
            }
            else
            {
                _dock = Dock.Filters;
                _routeShown = false;
                _model.Filters = _panel;
            }
            BuildButtons();
        }

        // The page the dock holds follows the district view: the view's page when a layer is ticked, the filters when it is not or the player went back to them.
        private void FollowDistrict()
        {
            DistrictView view = DistrictOwner;
            int layer = view != null && UiScreenSwitches.On(MapKits.DistrictId) ? view.Layer : -1;
            if (layer == _districtLayer)
            {
                return;
            }
            if (layer >= 0)
            {
                _dock = Dock.District;
                _districtModel.Offset = 0f;
                _districtModel.Reveal = -1;
            }
            else if (_dock == Dock.District)
            {
                _dock = Dock.Filters;
            }
            _districtLayer = layer;
        }

        /// <summary>The district view switched off with its page, as its close button did.</summary>
        private void DistrictOff() => DistrictOwner?.KitClose();

        public bool DistrictLayer(int index) => DistrictOwner != null && DistrictOwner.KitPick(index);

        public bool DistrictRow(int index) => DistrictOwner != null && DistrictOwner.KitKeep(index);

        public bool DistrictShow() => DistrictOwner != null && DistrictOwner.KitShow();

        // Back to the filters with the layer still on the map: the rows of the filters show it ticked, and ticking another brings the view's page back.
        public bool DistrictFilters()
        {
            _dock = Dock.Filters;
            return true;
        }
    }
}
