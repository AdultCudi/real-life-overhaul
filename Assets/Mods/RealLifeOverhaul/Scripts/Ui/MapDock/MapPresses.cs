namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a press on the city map's panels asks of the game and the mod, one method for each kind of target the panels draw. The live screen
    /// implements it over the game's widgets and the mod's own state; the checks implement it with a recorder that names the call it got, so a target that
    /// reaches the wrong kind or the wrong argument is found (the owner's rule for every page: an action arrives as the kind that was drawn). Each answers
    /// true when it did something, false when it can do nothing now (the host plays the error).
    /// </summary>
    public interface IMapActions
    {
        // the filters' dock
        bool Rail(int index);
        bool ShowAll();
        bool Search();
        bool Result(int index);
        bool Chip(int index);
        bool Group(int row);
        bool GroupAll(int row);
        bool Line(int row);
        bool Locate(int row);
        bool Station(int row);
        bool Choice(int row);
        bool Footer();
        bool PickedLocate();
        bool PickedDestination();
        bool PickedClear();
        bool PickedMore();

        // the place card
        bool PlaceTab(int index);
        bool Directions();
        bool Save();
        bool PlaceDestination();
        bool PlaceRent();
        bool PlaceAllReviews();
        bool PlaceReview(int index);

        // the strip, the buttons and the layers
        bool RouteOption(int index);
        bool Go();
        bool North();
        bool Centre();
        bool ZoomIn();
        bool ZoomOut();
        bool Tilt();
        bool Layer(int index);
        bool HideAll();

        // the district view
        bool DistrictLayer(int layer);
        bool DistrictRow(int row);
        bool DistrictShow();
        bool DistrictFilters();
    }

    /// <summary>
    /// Sends a press of a target of the city map's panels to the kind of call it is, by its command number alone: each of the 5000 block's numbers is one kind, and a
    /// number from another part of the kit (the app window's, the decision card's) is nobody's here, so it does nothing. Game-free. Cost: a switch.
    /// </summary>
    public static class MapPresses
    {
        /// <summary>The call a command makes, or false when the command is not one of the map's.</summary>
        public static bool Do(int command, int arg, IMapActions a)
        {
            switch (command)
            {
                case UiMapFilters.RailCommand: return a.Rail(arg);
                case UiMapFilters.ShowAllCommand: return a.ShowAll();
                case UiMapFilters.SearchCommand: return a.Search();
                case UiMapFilters.ResultCommand: return a.Result(arg);
                case UiMapFilters.ChipCommand: return a.Chip(arg);
                case UiMapFilters.GroupCommand: return a.Group(arg);
                case UiMapFilters.GroupAllCommand: return a.GroupAll(arg);
                case UiMapFilters.LineCommand: return a.Line(arg);
                case UiMapFilters.LocateCommand: return a.Locate(arg);
                case UiMapFilters.StationCommand: return a.Station(arg);
                case UiMapFilters.ChoiceCommand: return a.Choice(arg);
                case UiMapFilters.FooterCommand: return a.Footer();
                case UiMapFilters.PickedLocateCommand: return a.PickedLocate();
                case UiMapFilters.PickedDestinationCommand: return a.PickedDestination();
                case UiMapFilters.PickedClearCommand: return a.PickedClear();
                case UiMapFilters.PickedMoreCommand: return a.PickedMore();
                case UiMapPlace.TabCommand: return a.PlaceTab(arg);
                case UiMapPlace.DirectionsCommand: return a.Directions();
                case UiMapPlace.SaveCommand: return a.Save();
                case UiMapPlace.DestinationCommand: return a.PlaceDestination();
                case UiMapPlace.RentCommand: return a.PlaceRent();
                case UiMapPlace.AllCommand: return a.PlaceAllReviews();
                case UiMapPlace.ReviewCommand: return a.PlaceReview(arg);
                case UiMapChrome.OptionCommand: return a.RouteOption(arg);
                case UiMapChrome.GoCommand: return a.Go();
                case UiMapChrome.NorthCommand: return a.North();
                case UiMapChrome.CentreCommand: return a.Centre();
                case UiMapChrome.ZoomInCommand: return a.ZoomIn();
                case UiMapChrome.ZoomOutCommand: return a.ZoomOut();
                case UiMapChrome.TiltCommand: return a.Tilt();
                case UiMapChrome.LayerCommand: return a.Layer(arg);
                case UiMapChrome.HideAllCommand: return a.HideAll();
                case UiMapDistrict.LayerCommand: return a.DistrictLayer(arg);
                case UiMapDistrict.RowCommand: return a.DistrictRow(arg);
                case UiMapDistrict.ShowCommand: return a.DistrictShow();
                case UiMapDistrict.FiltersCommand: return a.DistrictFilters();
                default: return false;
            }
        }

        /// <summary>Every command the panels draw, in one list, for the checks: each must be taken by <see cref="Do"/>.</summary>
        public static readonly int[] Commands =
        {
            UiMapFilters.RailCommand, UiMapFilters.ShowAllCommand, UiMapFilters.SearchCommand, UiMapFilters.ResultCommand, UiMapFilters.ChipCommand, UiMapFilters.GroupCommand,
            UiMapFilters.GroupAllCommand, UiMapFilters.LineCommand, UiMapFilters.LocateCommand, UiMapFilters.StationCommand, UiMapFilters.ChoiceCommand, UiMapFilters.FooterCommand,
            UiMapFilters.PickedLocateCommand, UiMapFilters.PickedDestinationCommand, UiMapFilters.PickedClearCommand, UiMapFilters.PickedMoreCommand,
            UiMapPlace.TabCommand, UiMapPlace.DirectionsCommand, UiMapPlace.SaveCommand, UiMapPlace.DestinationCommand, UiMapPlace.RentCommand, UiMapPlace.AllCommand, UiMapPlace.ReviewCommand,
            UiMapChrome.OptionCommand, UiMapChrome.GoCommand, UiMapChrome.NorthCommand, UiMapChrome.CentreCommand, UiMapChrome.ZoomInCommand, UiMapChrome.ZoomOutCommand,
            UiMapChrome.TiltCommand, UiMapChrome.LayerCommand, UiMapChrome.HideAllCommand,
            UiMapDistrict.LayerCommand, UiMapDistrict.RowCommand, UiMapDistrict.ShowCommand, UiMapDistrict.FiltersCommand,
        };
    }
}
