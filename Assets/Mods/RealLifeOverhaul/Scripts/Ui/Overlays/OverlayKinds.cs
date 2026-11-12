using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Which of the game's cards over a thing a kind belongs to.</summary>
    public enum OverlayForm : byte
    {
        Hover,     // the small card the pointer calls up (SimpleOverlay)
        Click,     // the large card a click opens (DetailedOverlay)
        Hint,      // what the click will do, which the game decides (CtaManager's behaviours)
        Marker,    // a sprite the game stands over the world or the map
        Keeper     // a class that decides or holds, with nothing of its own to draw
    }

    /// <summary>One kind of the game's overlays, whether the kit draws it and why.</summary>
    public struct OverlayKind
    {
        public string Name;       // the game's class
        public OverlayForm Form;
        public bool Drawn;        // on the kit; false is the game's own
        public string Why;        // plain words for the table in the report and the check that every row says
    }

    /// <summary>
    /// The decision for every one of the game's overlay kinds (Player.HUD.ItemInfoOverlays and the markers beside them):
    /// drawn on the kit, or left the game's with the reason a player would accept. One table, so the report, the options page and
    /// the checks read the same words. Game-free.
    /// </summary>
    public static class OverlayKinds
    {
        public static readonly OverlayKind[] All =
        {
            new OverlayKind { Name = "StockOverlay", Form = OverlayForm.Hover, Drawn = true,
                Why = "frequent and simple: the stock as a figure with its meter, what it holds, the price and margin, the next delivery" },
            new OverlayKind { Name = "EmployeeNameOverlay", Form = OverlayForm.Hover, Drawn = true, Why = "a name over a station: a face with its mood ring, who is on shift and until when" },
            new OverlayKind { Name = "CtaOverlay", Form = OverlayForm.Hover, Drawn = true, Why = "one line of the game's own words about the click, shown as a hint under the figures" },
            new OverlayKind { Name = "PriceOverlay", Form = OverlayForm.Hover, Drawn = true, Why = "a price is one figure, with the customers it brings in an hour beside it" },
            new OverlayKind { Name = "RequirementsOverlay", Form = OverlayForm.Hover, Drawn = true, Why = "the game's own requirement lines, each a tick or an open ring" },
            new OverlayKind { Name = "SecurityOverlay", Form = OverlayForm.Hover, Drawn = true, Why = "one state word in its colour" },
            new OverlayKind { Name = "InfoOverlay", Form = OverlayForm.Hover, Drawn = true, Why = "one sentence of the game's own" },
            new OverlayKind { Name = "MachineRecipe (a flag the game has no class for)", Form = OverlayForm.Hover, Drawn = true,
                Why = "the card reads the recipe, its rate and the pallets itself, for the factory machines only" },
            new OverlayKind { Name = "CustomerCapacitySimpleOverlay", Form = OverlayForm.Hover, Drawn = true, Why = "a count of customers an hour" },
            new OverlayKind { Name = "ButtonOverlay", Form = OverlayForm.Click, Drawn = true, Why = "work, order, accept a delivery, take a drink: buttons pressed through the game's own" },
            new OverlayKind { Name = "VehicleOverlay", Form = OverlayForm.Click, Drawn = true, Why = "four buttons the game decides, pressed through its own" },
            new OverlayKind { Name = "StorageShelfOverlay", Form = OverlayForm.Click, Drawn = true, Why = "manage the storage and add items to it: two buttons pressed through the game's own" },
            new OverlayKind { Name = "JobBoardOverlay", Form = OverlayForm.Click, Drawn = true, Why = "view the candidates and change the board's text: two buttons pressed through the game's own" },
            new OverlayKind { Name = "SellerStandOverlay", Form = OverlayForm.Click, Drawn = true, Why = "the street cart: what it sells and each price, a button for each" },
            new OverlayKind { Name = "CustomizableButtonsOverlay", Form = OverlayForm.Click, Drawn = true,
                Why = "the buttons the game makes for a computer, a golf tee, a court or a locker, pressed through its own" },
            new OverlayKind { Name = "DecorativeItemHolderOverlay", Form = OverlayForm.Click, Drawn = true,
                Why = "the fridge and the wardrobe: each thing to take, the game's three buttons and the mod's Cook and Family buttons, all pressed through their own" },
            new OverlayKind { Name = "MachineInfoOverlay", Form = OverlayForm.Click, Drawn = true, Why = "one sentence, red when the pallets are full" },
            new OverlayKind { Name = "DropdownOverlay", Form = OverlayForm.Click, Drawn = true,
                Why = "what a shelf holds: the game's list opened as pages of options, each chosen through the game's own drop-down" },
            new OverlayKind { Name = "MachineOverlay", Form = OverlayForm.Click, Drawn = true,
                Why = "the machine's two choices, the machines it needs and the ingredients the pallets lack, chosen through the game's own drop-downs" },
            new OverlayKind { Name = "RadioOverlay", Form = OverlayForm.Click, Drawn = true, Why = "the station and the song, a volume in steps of a tenth and the game's own two buttons" },
            new OverlayKind { Name = "EmployeeOverlay", Form = OverlayForm.Click, Drawn = true,
                Why = "a person: the info panel docked at the left with their shift, mood, skill, pay, and the game's own buttons for their page and their schedule" },
            new OverlayKind { Name = "TextInputOverlay", Form = OverlayForm.Click, Drawn = false,
                Why = "the game never shows it: its IsValid and ShouldShow are both false, so there is nothing on screen to draw" },
            new OverlayKind { Name = "EditableWorkstationHeader", Form = OverlayForm.Click, Drawn = true,
                Why = "a workstation's name is renamed through the game's own text field, which the card focuses and mirrors while it is typed in" },
            new OverlayKind { Name = "SimpleOverlay", Form = OverlayForm.Keeper, Drawn = false,
                Why = "the game's box stays alive and hidden by leaf: OverlayManager, Going Public's patch on StockOverlay.ShouldShow and every other mod keep reading it" },
            new OverlayKind { Name = "DetailedOverlay", Form = OverlayForm.Keeper, Drawn = false,
                Why = "kept alive and hidden by leaf as above: the game's click away from the box and its navigation block still run on it" },
            new OverlayKind { Name = "OverlayManager", Form = OverlayForm.Keeper, Drawn = false, Why = "it alone decides what shows and where: the kit reads what it set" },
            new OverlayKind { Name = "OverlayHelper", Form = OverlayForm.Keeper, Drawn = false, Why = "the game's choice of the thing a box is about and its header words, read as they are" },
            new OverlayKind { Name = "OverlayBase", Form = OverlayForm.Keeper, Drawn = false, Why = "the game's own positioning of its box, which the card follows" },
            new OverlayKind { Name = "IOverlay", Form = OverlayForm.Keeper, Drawn = false, Why = "the base of every kind above: it holds which are shown" },
            new OverlayKind { Name = "RequirementOverlayEntry", Form = OverlayForm.Keeper, Drawn = false, Why = "a requirement line's holder, read by RequirementsOverlay's card" },
            new OverlayKind { Name = "CtaManager", Form = OverlayForm.Hint, Drawn = false,
                Why = "the game's own click words and click action: Empire Motors adds behaviours to it, so it stays the game's and the hint reads its words" },
            new OverlayKind { Name = "ShelfCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a shelf, which the hint line shows" },
            new OverlayKind { Name = "DecorativeItemHolderCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a fridge or a wardrobe" },
            new OverlayKind { Name = "PlayerActivityCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a seat, a bed or a desk" },
            new OverlayKind { Name = "ManageCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a thing the player manages" },
            new OverlayKind { Name = "CashRegisterCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a till" },
            new OverlayKind { Name = "VehicleCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a vehicle" },
            new OverlayKind { Name = "SellerStandCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a street cart" },
            new OverlayKind { Name = "TaxiCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a taxi stand" },
            new OverlayKind { Name = "DeliveryJobCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of the delivery job" },
            new OverlayKind { Name = "TicketBoothCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a ticket booth" },
            new OverlayKind { Name = "TicketKioskCtaBehavior", Form = OverlayForm.Hint, Drawn = false, Why = "decides the click words of a ticket kiosk" },
            new OverlayKind { Name = "ItemWarningIconManager", Form = OverlayForm.Marker, Drawn = false,
                Why = "the low stock and warning icons are the game's own sprites in the world: re-skinning its images is forbidden and a flat glyph reads no better at play distance" },
            new OverlayKind { Name = "DirectionGuider", Form = OverlayForm.Marker, Drawn = false,
                Why = "the guiders are the game's markers on the street and the city map, drawn by its PointOfInterest sprites: they stay as the game draws them" },
            new OverlayKind { Name = "PointOfInterest", Form = OverlayForm.Marker, Drawn = false,
                Why = "the map's own markers, which other mods read and place; the kit's map panels already draw the places they open" },
        };

        /// <summary>How many kinds the kit draws.</summary>
        public static int DrawnCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < All.Length; i++)
                {
                    n += All[i].Drawn ? 1 : 0;
                }
                return n;
            }
        }
    }
}
