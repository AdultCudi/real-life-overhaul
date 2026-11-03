using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The world's pieces over the streets they stand on: a building's panel docked at the top left (the player's rented
    // coffee shop by day, a rival's closed noodle bar, an empty shop to rent at night, its owners' tab) and its card at the
    // door before it is picked; the box at a tower's door with four floors, the game's own door at night, a lift indoors and
    // a bus stop with two routes; the police front desk's list of eight indoors and the holding cell's list locked beside
    // the cell; the job offer at the coffee shop's counter. The longest words: the door's box and the building's panel in
    // German, the game's words from its own German file.
    static partial void AddWorld(List<Screen> screens)
    {
        screens.Add(World("world_building_rented", "13.jpg", (s, f, d, k) => UiSamples.WorldBuildingScene(s, f, d, k, English, UiSamples.WorldPlace.Rented),
            UiInfoPanel.FooterCommand, -1, "RDDDDDDDDDLDRR"));
        screens.Add(World("world_building_rival", "02.jpg", (s, f, d, k) => UiSamples.WorldBuildingScene(s, f, d, k, English, UiSamples.WorldPlace.Rival),
            UiInfoPanel.TabCommand, 1, "RDDDDDDDDDLDR"));
        screens.Add(World("world_building_for_rent", "11.jpg", (s, f, d, k) => UiSamples.WorldBuildingScene(s, f, d, k, English, UiSamples.WorldPlace.ForRent, true),
            UiInfoPanel.FooterCommand, 0, "RDDDDDDDLDR"));
        screens.Add(World("world_building_owners", "02.jpg", (s, f, d, k) => UiSamples.WorldBuildingScene(s, f, d, k, English, UiSamples.WorldPlace.Owners),
            UiInfoPanel.TabCommand, 0, "RDDDLDRR"));
        screens.Add(World("world_building_door", "13.jpg", (s, f, d, k) => UiSamples.WorldPromptScene(s, f, d, k, English, UiSamples.WorldBuildingCard(English), 0.5f, 0.4f),
            UiDraw.MoneyPillCommand, 0, "RR"));
        screens.Add(World("world_door", "13.jpg", (s, f, d, k) => UiSamples.WorldPromptScene(s, f, d, k, English, UiSamples.WorldDoor(English, k), 0.62f, 0.58f),
            UiPromptCard.ActionCommand, 1, "DDDD"));
        screens.Add(World("world_door_night", "11.jpg", (s, f, d, k) => UiSamples.WorldPromptScene(s, f, d, k, English, UiSamples.WorldGameDoor(English, k), 0.55f, 0.5f, true),
            UiPromptCard.ActionCommand, 1, "DD"));
        screens.Add(World("world_lift", "03.jpg", (s, f, d, k) => UiSamples.WorldPromptScene(s, f, d, k, English, UiSamples.WorldLift(English, k), 0.4f, 0.45f),
            UiPromptCard.ActionCommand, 2, "DDD"));
        screens.Add(World("world_bus_stop", "02.jpg", (s, f, d, k) => UiSamples.WorldPromptScene(s, f, d, k, English, UiSamples.WorldBusStop(English, k), 0.7f, 0.62f),
            UiPromptCard.ActionCommand, 0, "DD"));
        screens.Add(new Screen { Name = "world_desk", AppName = "Front desk", Icon = UiIcon.ShieldCheck, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldDesk(English, false), false), HoverCommand = UiPicker.EntryCommand, HoverArg = 2,
            Walk = Moves("DDDDDDDDU") });
        screens.Add(new Screen { Name = "world_custody", AppName = "Holding cell", Icon = UiIcon.ShieldCheck, Backdrop = "03.jpg",
            Lay = (s, f, d, k) => UiSamples.WorldPickerScene(s, f, d, k, UiSamples.WorldCustody(English), true, true), HoverCommand = UiPicker.EntryCommand, HoverArg = 2,
            Walk = Moves("DDDU") });
        screens.Add(new Screen { Name = "world_job_offer", AppName = "Job offer", Icon = UiIcon.Briefcase, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.WorldOfferScene(s, f, d, k, English, false), HoverCommand = UiOfferCard.GoCommand, Walk = Moves("RL") });
        screens.Add(World("world_longest_door", "13.jpg", (s, f, d, k) => UiSamples.WorldPromptScene(s, f, d, k, BizGerman, UiSamples.WorldGameDoor(BizGerman, k), 0.62f, 0.58f),
            UiPromptCard.ActionCommand, 1, "DD"));
        screens.Add(World("world_longest_building", "13.jpg", (s, f, d, k) => UiSamples.WorldBuildingScene(s, f, d, k, BizGerman, UiSamples.WorldPlace.Rented),
            UiInfoPanel.FooterCommand, -1, "RDDDDDDDDDLDRR"));
        screens.Add(World("world_longest_bus_stop", "02.jpg", (s, f, d, k) => UiSamples.WorldPromptScene(s, f, d, k, BizGerman, UiSamples.WorldBusStop(BizGerman, k), 0.7f, 0.62f),
            UiPromptCard.ActionCommand, 0, "DD"));
        // The game's smaller windows: the private driver beside the phone in its four states, the items list of a store and of an
        // order, a sign's look, the preview terminal, the preview's bar and the colour picker's frame.
        screens.Add(World("world_private_driver_nothing", "02.jpg", (s, f, d, k) => UiSamples.WorldDriverScene(s, f, d, k, English, UiSamples.DriverState.Nothing),
            UiToolPanel.LineCommand, 1, "D"));
        screens.Add(World("world_private_driver", "02.jpg", (s, f, d, k) => UiSamples.WorldDriverScene(s, f, d, k, English, UiSamples.DriverState.Cars),
            UiToolPanel.LineCommand, 1, "DD"));
        screens.Add(World("world_private_driver_coming", "02.jpg", (s, f, d, k) => UiSamples.WorldDriverScene(s, f, d, k, English, UiSamples.DriverState.Coming),
            UiToolPanel.LineCommand, 2, "D"));
        screens.Add(World("world_private_driver_waiting", "02.jpg", (s, f, d, k) => UiSamples.WorldDriverScene(s, f, d, k, English, UiSamples.DriverState.Waiting),
            UiToolPanel.LineCommand, 2, "D"));
        screens.Add(new Screen { Name = "world_items_list", AppName = "Items", Icon = UiIcon.ShoppingCart, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.WorldItemsScene(s, f, d, k, English, UiSamples.WorldItems(English, false)), HoverCommand = UiItemsList.TypeCommand, HoverArg = -1,
            Walk = Moves("DDD") });
        screens.Add(new Screen { Name = "world_items_order", AppName = "Items", Icon = UiIcon.ShoppingCart, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.WorldItemsScene(s, f, d, k, English, UiSamples.WorldItems(English, true)), HoverCommand = UiItemsList.MoreCommand, HoverArg = 0,
            Walk = Moves("DDRRDD") });
        screens.Add(new Screen { Name = "world_items_none_found", AppName = "Items", Icon = UiIcon.ShoppingCart, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.WorldItemsScene(s, f, d, k, English, UiSamples.WorldItems(English, true, "zebra")), HoverCommand = UiItemsList.ClearCommand, HoverArg = 0,
            Walk = Moves("D") });
        screens.Add(World("world_sign", "11.jpg", (s, f, d, k) => UiSamples.WorldSignScene(s, f, d, k, English, true, false), UiToolPanel.SwatchCommand + 1, 3, "DDRRD"));
        screens.Add(World("world_sign_open", "11.jpg", (s, f, d, k) => UiSamples.WorldSignScene(s, f, d, k, English, false, true), UiToolPanel.OptionCommand, 4, "DD"));
        screens.Add(World("world_preview_terminal", "03.jpg", (s, f, d, k) => UiSamples.WorldTerminalScene(s, f, d, k, English, 4, false), UiToolPanel.LineCommand, 5, "DDDDR"));
        screens.Add(World("world_preview_terminal_open", "03.jpg", (s, f, d, k) => UiSamples.WorldTerminalScene(s, f, d, k, English, 3, true), UiToolPanel.OptionCommand, 1, "DD"));
        screens.Add(World("world_building_preview", "13.jpg", (s, f, d, k) => UiSamples.WorldPreviewScene(s, f, d, k, English, false), UiToolPanel.SecondCommand, 0, "R"));
        screens.Add(World("world_colour_picker", "11.jpg", (s, f, d, k) => UiSamples.WorldColourScene(s, f, d, k, English), UiToolPanel.LineCommand, 0, "L"));
    }

    // A world piece over its street, the HUD's bar under it: the mouse resting on one of its targets, the pad's walk from its first.
    private static Screen World(string name, string backdrop, LayOut lay, int hoverCommand, int hoverArg, string walk) =>
        new Screen { Name = name, AppName = "HUD", Backdrop = backdrop, Lay = lay, HoverCommand = hoverCommand, HoverArg = hoverArg, Walk = Moves(walk) };
}
