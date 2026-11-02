using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// Buying and what the player holds, with the data a player would meet at a cash and carry in the Garment District on a
/// Tuesday: the checkout with five goods being chosen, a long order turned to its second page, the tax office's payment,
/// the queue once the order is in, a casino boat ticket and an empty register; a car at a dealer with and without the
/// delivery to a warehouse, and a boat; the item panel with a box truck, another player's car, a hand truck, a mop, an
/// unpaid basket and a mini game; manage cargo; the clothes shop, the wardrobe at home, the hairdresser and the surgeon;
/// and the garage shop's pages. Each in French too, the longest of the mod's languages: the game's words in French, the
/// mod's new words in English until the next translation batch. Plain English, as the owner reads the previews; the labels
/// the game or the mod already has come through their keys.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] CheckoutStates = { "goods", "goods_page2", "taxes", "queue", "ticket", "empty", "goods_fr" };
    public static readonly string[] VehicleBuyStates = { "car", "car_delivery", "boat", "car_fr" };
    public static readonly string[] ItemPanelStates = { "vehicle", "vehicle_foreign", "hand_truck", "mop", "basket", "minigame", "vehicle_fr" };
    public static readonly string[] CargoStates = { "truck", "truck_unpaid", "empty", "truck_fr" };
    public static readonly string[] WardrobeStates = { "clothes", "wardrobe", "hair", "surgery", "surgery_face", "clothes_fr" };
    public static readonly string[] GarageStates = { "main", "paint", "figures" };

    private static readonly string[] Goods = { "Cupcake", "Croissant", "Ice cream", "Popcorn", "Cheese platter", "Paper bags", "Donut", "Coffee beans" };
    private static readonly string[] GoodsPictures = { "buy:cupcake", "buy:croissant", "buy:icecream", "buy:popcorn", "buy:cheese", "buy:boxes", "buy:cupcake2", "buy:brewed" };
    private static readonly float[] GoodsAspects = { 2.15f, 2.6f, 1.6f, 1f, 1.25f, 1f, 2.09f, 0.67f };
    private static readonly int[] GoodsPieces = { 1500, 500, 500, 2000, 500, 2000, 1000, 400 };
    private static readonly int[] GoodsPrices = { 1003, 710, 1055, 115, 162, 120, 640, 880 };

    private static UiWords Words(UiWords t, string state) => state.EndsWith("_fr") ? BuyFrench(t) : t;

    private static string Money(int amount) => "$" + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    private static UiPrompt[] Prompts(UiWords t) => new[]
    {
        new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
        new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
        new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") }
    };

    // ------------------------------------------------------------------------------------------------- the checkout

    /// <summary>The checkout at a cash and carry: five goods being chosen, a long order's second page, the taxes, the queue, a ticket, nothing.</summary>
    public static UiCheckoutModel Checkout(UiWords t, string state)
    {
        t = Words(t, state);
        var m = new UiCheckoutModel
        {
            Choose = true, ColumnGoods = t("reallife_ui_buy_goods", "Goods"), ColumnPrice = t("common_price", "Price"), ColumnPacks = t("common_amount", "Amount"),
            ColumnTotal = t("common_total", "Total"), PriceEach = t("reallife_ui_buy_each", "{price} a pack"), TotalLabel = t("purchaseui_totalprice", "Total Price"),
            Paused = t("reallife_ui_buy_paused", "Time is paused"), Order = t("purchaseui_place_order", "Place order"), OrderEnabled = true,
            Cancel = t("common_cancel", "Cancel"), CancelEnabled = true, Pay = t("reallife_ui_buy_pay", "Pay"), AmountHint = t("reallife_ui_buy_amount_hint", "Amount to pay"),
            OwedWords = t("reallife_ui_buy_owed", "Owed {amount}"), EmptyTitle = t("reallife_ui_buy_empty", "Nothing on the register"),
            EmptyBody = t("reallife_ui_buy_empty_body", "The goods you pick up show here with their price."), Prompts = Prompts(t), PromptCount = 3
        };
        int[] packs = { 3, 2, 1, 0, 1, 2, 1, 0 };
        int count = state == "goods_page2" ? 8 : state == "empty" ? 0 : state == "ticket" ? 1 : 5;
        int taken = 0, total = 0;
        var lines = new UiCheckoutLine[state == "taxes" ? 0 : count];
        for (int i = 0; i < lines.Length; i++)
        {
            bool ticket = state == "ticket";
            int price = ticket ? 5000 : GoodsPrices[i], n = ticket ? 1 : packs[i];
            taken += n;
            total += price * n;
            lines[i] = new UiCheckoutLine
            {
                Name = ticket ? t("casinoboat_ticket_pay_item", "Casino Boat Ticket") : Goods[i], Picture = ticket ? null : GoodsPictures[i], Aspect = GoodsAspects[i],
                Icon = ticket ? UiIcon.Tag : UiIcon.Package,
                Pack = ticket ? null : t("reallife_ui_buy_pack", "{count} pieces").Replace("{count}", GoodsPieces[i].ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
                Price = Money(price), Total = Money(n == 0 ? price : price * n), Packs = n, CanLess = n > 0, CanMore = true
            };
        }
        m.Lines = lines;
        m.Title = t("purchaseui_title_with_amounts", "Purchase ({currentAmount}/{maxAmount} items)").Replace("{currentAmount}", taken.ToString()).Replace("{maxAmount}", "10");
        m.Total = Money(total);
        m.Page = state == "goods_page2" ? 1 : 0;
        m.PageWords = t("reallife_ui_buy_page", "Page {page} of {pages}");
        m.Head = state == "taxes" || state == "ticket" ? null : new UiCheckoutHead
        {
            Mark = UiIcon.Store, Name = "NY Distro Inc", Address = "37 1st Street, Garment District"
        };
        switch (state)
        {
            case "taxes":
                m.Title = t("purchaseui_pay_taxes", "Pay taxes");
                m.Choose = false;
                m.Taxes = new[]
                {
                    new UiCheckoutTax { Name = t("taxes_pay_item", "Taxes"), Owed = "$12,480.50", Amount = "12,480.50" },
                    new UiCheckoutTax { Name = t("taxes_back_taxes_pay_item", "Back taxes"), Owed = "$3,115.00", Amount = "3,115.00" }
                };
                m.Total = "$12,480.50";
                m.Order = t("purchaseui_pay_taxes", "Pay taxes");
                break;
            case "queue":
                m.Order = null;
                m.Total = null;
                m.Queue = t("purchaseui_waiting_in_queue", "Waiting in queue...");
                m.Title = t("common_purchase", "Purchase");
                m.CancelEnabled = false;
                break;
            case "ticket":
                m.Choose = false;
                m.Title = t("common_purchase", "Purchase");
                break;
            case "empty":
                m.OrderEnabled = false;
                m.Total = Money(0);
                break;
        }
        return m;
    }

    // ------------------------------------------------------------------------------------------------- a vehicle's purchase

    /// <summary>A car at the dealer, with the delivery to a warehouse off and on, and a boat at the harbour.</summary>
    public static UiVehicleBuyModel VehicleBuy(UiWords t, string state)
    {
        t = Words(t, state);
        bool boat = state == "boat", delivery = state == "car_delivery";
        var m = new UiVehicleBuyModel
        {
            Title = t("common_vehicle", "Vehicle"), Icon = boat ? UiIcon.Globe : UiIcon.Car, Name = boat ? "Marlin 28" : "Bimmer Box Truck", Price = boat ? "$84,000" : "$38,500",
            SpecsLabel = t("purchasecar_specs", "Specs"),
            Specs = new[]
            {
                new UiInfoRow { Label = t("common_cargo_capacity", "Cargo Capacity"), Value = boat ? "0" : "8" },
                new UiInfoRow { Label = t("vehicle_tank_capacity", "Tank Capacity"), Value = boat ? "300" : "120" },
                new UiInfoRow { Label = t("vehicle_auto_park_support", "Auto Park Support"), Value = t("common_yes", "Yes") },
                new UiInfoRow { Label = t("vehicle_max_speed", "Max Speed"), Value = boat ? "60" : "110" }
            },
            ColourLabel = t("reallife_ui_buy_colour", "Colour"),
            Swatches = new[]
            {
                UiDraw.SwatchKey(0xF2F2F0), UiDraw.SwatchKey(0x1C1C20), UiDraw.SwatchKey(0x6B6F76), UiDraw.SwatchKey(0x7A2430), UiDraw.SwatchKey(0x1F2F6B),
                UiDraw.SwatchKey(0x2F5A43), UiDraw.SwatchKey(0xC9B79A), UiDraw.SwatchKey(0xD9A21B)
            },
            Colour = 4, Delivery = !boat, DeliveryOn = delivery,
            DeliveryWords = t("purchasevehicleui_delivertowarehouse", "Deliver to Warehouse\n({amount} Fee)").Replace("{amount}", "$500").Replace("\n", " "),
            Warehouse = delivery ? "Garment District Warehouse, 112 7th Avenue" : t("dialog_select_address", "Select address"), WarehouseChosen = delivery,
            Cancel = t("common_cancel", "Cancel"), CancelEnabled = true, BuyEnabled = true, Prompts = Prompts(t), PromptCount = 3
        };
        m.Buy = (delivery ? t("furniture_delivery_dialog_order", "Order") : t("common_purchase", "Purchase"));
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the item panel

    /// <summary>The item panel: a box truck, another player's car, a hand truck, a mop, an unpaid basket and a mini game.</summary>
    public static UiItemPanelModel ItemPanel(UiWords t, string state)
    {
        t = Words(t, state);
        var m = new UiItemPanelModel { Title = t("common_vehicle", "Vehicle"), Icon = UiIcon.Car };
        UiGameKey park = new UiGameKey { Name = "P" }, sleep = new UiGameKey { Name = "Z" }, sell = new UiGameKey { Name = "X" }, auto = new UiGameKey { Name = "R" };
        switch (state)
        {
            case "vehicle":
            case "vehicle_fr":
            case "vehicle_foreign":
            {
                bool foreign = state == "vehicle_foreign";
                m.Name = foreign ? "Stallion GT" : "Bimmer Box Truck";
                m.Status = "57 km/h, 5th Avenue";
                m.Picture = foreign ? "buy:bluesport" : "buy:boxtruck";
                m.Aspect = foreign ? 1.3f : 1.94f;
                m.Owner = foreign ? t("reallife_ui_buy_not_yours", "Not yours") : t("reallife_ui_buy_yours", "Yours");
                m.OwnerTone = foreign ? UiRole.Warn : UiRole.Ok;
                m.ReadingsLabel = t("reallife_ui_buy_readings", "Readings");
                m.Readings = new[]
                {
                    new UiReading { Icon = UiIcon.Drop, Label = t("itempanelui_vehicle_fuel", "Fuel"), Share = 0.64f, Value = "64%" },
                    new UiReading { Icon = UiIcon.Wrench, Label = t("itempanelui_maintenance_condition", "Condition"), Share = 0.42f, Tone = UiRole.Warn, Value = "42%" },
                    new UiReading { Icon = UiIcon.FastForward, Label = t("reallife_ui_buy_speed", "Speed"), Share = -1f, Value = "57 km/h" }
                };
                m.Facts = new[]
                {
                    new UiInfoRow { Label = t("itempanelui_vehicle_parkingzone", "Parking Zone"), Value = t("itempanelui_parkingzone_legal_payed", "LEGAL ({price})").Replace("{price}", "$25") }
                };
                if (!foreign)
                {
                    m.CargoLabel = t("common_cargo", "Cargo");
                    m.CargoValue = t("reallife_ui_buy_slots", "{used} of {most}").Replace("{used}", "6").Replace("{most}", "8");
                    m.CargoSlots = 8;
                    m.Cargo = new[]
                    {
                        new UiCatalogueCard { Picture = "buy:cupcake", Aspect = 2.15f, Figure = "\u00D73" }, new UiCatalogueCard { Picture = "buy:croissant", Aspect = 2.6f, Figure = "\u00D72" },
                        new UiCatalogueCard { Picture = "buy:icecream", Aspect = 1.6f, Figure = "\u00D71" }, new UiCatalogueCard { Picture = "buy:popcorn", Aspect = 1f, Figure = "\u00D71" },
                        new UiCatalogueCard { Picture = "buy:cheese", Aspect = 1.25f, Figure = "\u00D71" }, new UiCatalogueCard { Picture = "buy:boxes", Aspect = 1f, Figure = "\u00D72" }
                    };
                }
                var actions = new List<UiItemAction>
                {
                    new UiItemAction { Label = t("itempanelui_buttons_park_vehicle", "Park Vehicle"), Key = park, Main = true },
                    new UiItemAction { Label = t("itempanelui_buttons_auto_park", "Auto Park"), Key = auto },
                    new UiItemAction { Label = t("itempanelui_buttons_sleep", "Sleep"), Key = sleep }
                };
                if (!foreign)
                {
                    actions.Add(new UiItemAction { Label = t("itempanelui_sell", "Sell"), Key = sell, Danger = true });
                }
                m.Actions = actions.ToArray();
                break;
            }
            case "hand_truck":
                m.Title = t("reallife_ui_buy_in_hands", "In your hands");
                m.Icon = UiIcon.Package;
                m.Name = "Hand truck";
                m.Glyph = UiIcon.Truck;
                m.CargoLabel = t("common_cargo", "Cargo");
                m.CargoValue = t("reallife_ui_buy_slots", "{used} of {most}").Replace("{used}", "3").Replace("{most}", "4");
                m.CargoSlots = 4;
                m.Cargo = new[]
                {
                    new UiCatalogueCard { Picture = "buy:boxes", Aspect = 1f, Figure = "\u00D72" }, new UiCatalogueCard { Picture = "buy:cupcake", Aspect = 2.15f, Figure = "\u00D71" },
                    new UiCatalogueCard { Picture = "buy:popcorn", Aspect = 1f, Figure = "\u00D71" }
                };
                m.Actions = new[]
                {
                    new UiItemAction { Label = t("itempanelui_buttons_place_box", "Place box"), Key = park, Main = true },
                    new UiItemAction { Label = t("itempanelui_buttons_discard", "Discard"), Key = new UiGameKey { Name = "Del" }, Danger = true }
                };
                break;
            case "mop":
                m.Title = t("reallife_ui_buy_in_hands", "In your hands");
                m.Icon = UiIcon.Package;
                m.Name = "Mop";
                m.Glyph = UiIcon.Drop;
                m.Facts = new[] { new UiInfoRow { Label = t("itempanelui_maintenance_condition", "Condition"), Value = "Clean 81%" } };
                m.Actions = new[] { new UiItemAction { Label = t("itempanelui_buttons_discard", "Discard"), Key = new UiGameKey { Name = "Del" }, Danger = true } };
                break;
            case "basket":
                m.Title = t("reallife_ui_buy_in_hands", "In your hands");
                m.Icon = UiIcon.ShoppingBag;
                m.Name = "Shopping basket";
                m.Glyph = UiIcon.ShoppingBag;
                m.Facts = new[] { new UiInfoRow { Label = t("reallife_ui_buy_unpaid_total", "Unpaid"), Value = "$3,971", Ink = UiRole.WarnText } };
                m.CargoLabel = t("common_cargo", "Cargo");
                m.CargoValue = t("reallife_ui_buy_slots", "{used} of {most}").Replace("{used}", "3").Replace("{most}", "6");
                m.CargoSlots = 6;
                m.Cargo = new[]
                {
                    new UiCatalogueCard { Picture = "buy:cupcake", Aspect = 2.15f, Figure = "$3,009", State = "unpaid" },
                    new UiCatalogueCard { Picture = "buy:cupcake2", Aspect = 2.09f, Figure = "$726", State = "unpaid" },
                    new UiCatalogueCard { Picture = "buy:boxes", Aspect = 1f, Figure = "$235", State = "unpaid" }
                };
                m.Actions = new[] { new UiItemAction { Label = t("itempanelui_buttons_discard", "Discard"), Key = new UiGameKey { Name = "Del" }, Danger = true } };
                break;
            case "minigame":
                m.Title = t("reallife_ui_buy_minigame", "Playing");
                m.Icon = UiIcon.GameController;
                m.Name = "Golf";
                m.Glyph = UiIcon.GameController;
                m.Actions = new[] { new UiItemAction { Label = t("itempanelui_buttons_leave", "Leave"), Key = new UiGameKey { Name = "Esc" }, Main = true } };
                break;
        }
        return m;
    }

    // ------------------------------------------------------------------------------------------------- manage cargo

    /// <summary>Manage cargo on a box truck: six stacks with the cupcakes chosen, an unpaid stack, and an empty truck.</summary>
    public static UiCargoModel Cargo(UiWords t, string state)
    {
        t = Words(t, state);
        bool empty = state == "empty", unpaid = state == "truck_unpaid";
        var m = new UiCargoModel
        {
            Title = t("reallife_ui_buy_manage_cargo", "Manage cargo"), Paused = t("reallife_ui_buy_paused", "Time is paused"), Holder = "Bimmer Box Truck",
            HolderDetail = t("itemoverlay_contents_headline", "Contents") + ": " + (empty ? "0/8" : "6/8"), HolderPicture = "buy:boxtruck", HolderAspect = 1.94f,
            SlotsLabel = t("reallife_ui_buy_slots_used", "Slots used"),
            SlotsValue = t("reallife_ui_buy_slots", "{used} of {most}").Replace("{used}", empty ? "0" : "6").Replace("{most}", "8"), SlotsShare = empty ? 0f : 0.75f,
            SellAll = t("bizman_inventory_sell_all", "Sell All"), SellAllShown = !empty, SellAllEnabled = true, Prompts = Prompts(t), PromptCount = 3
        };
        m.Catalogue.EmptyTitle = t("reallife_ui_buy_empty_cargo", "Nothing in the cargo");
        m.Catalogue.EmptyBody = t("reallife_ui_buy_empty_cargo_body", "Goods loaded here show with their count.");
        m.Catalogue.EmptyIcon = UiIcon.Package;
        if (!empty)
        {
            int n = 6;
            m.Catalogue.Cards = new UiCatalogueCard[n];
            for (int i = 0; i < n; i++)
            {
                bool owed = unpaid && i == 2;
                m.Catalogue.Cards[i] = new UiCatalogueCard
                {
                    Picture = GoodsPictures[i], Aspect = GoodsAspects[i], Name = Goods[i],
                    Figure = t("reallife_ui_buy_count", "x{count}").Replace("{count}", (GoodsPieces[i] * (i == 0 ? 3 : 1)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
                    State = owed ? t("reallife_ui_buy_unpaid", "Unpaid") : null, StateTone = UiRole.Warn
                };
            }
            m.Catalogue.CardCount = n;
            m.Catalogue.Chosen = unpaid ? 2 : 0;
            m.Catalogue.DetailKicker = t("reallife_ui_buy_in_cargo", "In the cargo");
            m.Catalogue.Actions = unpaid
                ? new[] { new UiActionModel { Label = t("itempanelui_buttons_discard", "Discard"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count, Command = 0 } }
                : new[]
                {
                    new UiActionModel { Label = t("action_take", "Take"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Command = 0 },
                    new UiActionModel { Label = t("itempanelui_sell", "Sell"), Kind = UiButtonKind.Primary, Icon = UiIcon.Coin, Key = UiAction.Alt, Command = 1 }
                };
        }
        else
        {
            m.Catalogue.CardCount = 0;
        }
        return m;
    }

    // ------------------------------------------------------------------------------------------------- clothes, hair, surgery

    private static readonly string[] Tees = { "Red tee", "Black tee", "Orange tee", "Olive tee", "Charcoal tee", "Dark tee", "Blue hoodie", "Black jacket", "Lime crop top", "Navy blazer", "Red polo", "Red work vest" };
    private static readonly string[] TeePictures = { "buy:cl_red", "buy:cl_black", "buy:cl_orange", "buy:cl_olive", "buy:cl_char", "buy:cl_dark", "buy:cl_hoodie", "buy:cl_jacket", "buy:cl_lime", "buy:cl_blazer", "buy:cl_polo", "buy:cl_p12" };
    private static readonly float[] TeeAspects = { 1.4f, 1.33f, 1.4f, 1.51f, 1.56f, 1.59f, 1.56f, 1.44f, 2.14f, 1.12f, 1.13f, 1.28f };

    /// <summary>The clothes shop at the mod's rack, the wardrobe at home, the hairdresser, and the surgeon's body and face.</summary>
    public static UiWardrobeModel Wardrobe(UiWords t, string state)
    {
        t = Words(t, state);
        bool hair = state == "hair", surgery = state.StartsWith("surgery"), shop = state.StartsWith("clothes");
        var m = new UiWardrobeModel
        {
            Leave = t("dialog_leave_button", "Leave"), ColourLabel = t("reallife_ui_buy_colour", "Colour"),
            EmptyTitle = t("reallife_ui_buy_empty_grid", "Nothing to try on here"), EmptyBody = t("reallife_ui_buy_empty_grid_body", "Pick another kind at the left."),
            Prompts = Prompts(t), PromptCount = 3
        };
        UiIcon[] glyphs = surgery ? new[] { UiIcon.Person, UiIcon.Eye, UiIcon.SmileyHappy } : hair ? new[] { UiIcon.Person } : new[] { UiIcon.Person, UiIcon.Tag, UiIcon.ShoppingBag, UiIcon.Star };
        m.Tabs = new UiEdgeTab[glyphs.Length];
        for (int i = 0; i < glyphs.Length; i++)
        {
            m.Tabs[i] = new UiEdgeTab { Icon = glyphs[i] };
        }
        m.TabCount = glyphs.Length;
        m.Tab = surgery && state == "surgery_face" ? 1 : hair ? 0 : 1;
        if (hair)
        {
            m.Title = t("change_character_hair_title", "Change hair");
            m.Grid.Cards = new UiCatalogueCard[8];
            for (int i = 0; i < 8; i++)
            {
                m.Grid.Cards[i] = new UiCatalogueCard { Icon = UiIcon.Person, Name = t("character_customization_hair", "Hair") + " " + (i + 1) };
            }
            m.Grid.CardCount = 8;
            m.Grid.Chosen = 2;
            m.Swatches = new[] { UiDraw.SwatchKey(0x2B1B12), UiDraw.SwatchKey(0x5A3A22), UiDraw.SwatchKey(0x9A6A3A), UiDraw.SwatchKey(0xD6B370), UiDraw.SwatchKey(0x8A8A8A), UiDraw.SwatchKey(0xB33A2B) };
            m.SwatchCount = 6;
            m.Swatch = 1;
            m.Actions = new[] { new UiActionModel { Label = t("change_character_hair_save_button", "Change hair") + " $45", Kind = UiButtonKind.Primary, Key = UiAction.Alt } };
            return m;
        }
        if (surgery)
        {
            m.Title = t("plastic_surgery_title", "Plastic Surgery");
            if (state == "surgery")
            {
                m.Sliders = new[]
                {
                    new UiSliderRow { Label = t("reallife_ui_buy_strength", "Strength"), Figure = "62", Share = 0.62f },
                    new UiSliderRow { Label = t("reallife_ui_buy_weight", "Weight"), Figure = "35", Share = 0.35f },
                    new UiSliderRow { Label = t("character_customization_skin_color", "Skin color"), Figure = "40", Share = 0.4f }
                };
                m.SliderCount = 3;
                m.Held = 0;
            }
            else
            {
                m.Grid.Cards = new UiCatalogueCard[6];
                for (int i = 0; i < 6; i++)
                {
                    m.Grid.Cards[i] = new UiCatalogueCard { Icon = UiIcon.Eye, Name = t("character_customization_head", "Head") + " " + (i + 1) };
                }
                m.Grid.CardCount = 6;
                m.Grid.Chosen = 1;
                m.Swatches = new[] { UiDraw.SwatchKey(0x3B6EA8), UiDraw.SwatchKey(0x2F5A43), UiDraw.SwatchKey(0x6B4A2B), UiDraw.SwatchKey(0x8A8A8A) };
                m.SwatchCount = 4;
                m.Swatch = 0;
            }
            m.Actions = new[]
            {
                new UiActionModel { Label = t("intro_tooltip_randomize_appearance", "Randomize appearance"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                new UiActionModel { Label = t("common_revert", "Revert"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                new UiActionModel { Label = t("common_apply_changes", "Apply Changes") + " $2,400", Kind = UiButtonKind.Primary, Key = UiAction.Alt }
            };
            return m;
        }
        // The clothes: the torso's pieces, the mod's with their prices or Owned, the game's with their pictures alone.
        m.Title = t("change_character_clothes_title", "Change clothes") + ", " + t("character_customization_torso", "Torso");
        m.Subs = new[] { "T-shirts", "Hoodies", "Jackets", "Shirts" };
        m.SubCount = 4;
        m.Sub = 0;
        m.Grid.Cards = new UiCatalogueCard[Tees.Length];
        int[] prices = { 24, 28, 24, 26, 26, 30, 58, 96, 28, 140, 42, 36 };
        for (int i = 0; i < Tees.Length; i++)
        {
            bool mod = i >= 5;
            bool owned = i == 0 || i == 11 || !shop;
            m.Grid.Cards[i] = new UiCatalogueCard
            {
                Picture = TeePictures[i], Aspect = TeeAspects[i], Name = mod ? Tees[i] : null,
                Figure = mod && shop && !owned ? Money(prices[i]) : null,
                State = mod ? (owned && shop ? t("reallife_clothes_screen_owned", "Owned") : shop ? null : t("reallife_creator_tag", "RLO")) : null,
                StateTone = owned && shop ? UiRole.Ok : UiRole.Accent
            };
        }
        m.Grid.CardCount = Tees.Length;
        m.Grid.Chosen = 9;
        m.ChosenName = Tees[9];
        m.ChosenDetail = shop ? t("character_customization_torso", "Torso") + ", " + Money(140) : t("character_customization_torso", "Torso");
        m.Swatches = new[] { UiDraw.SwatchKey(0x1F2F6B), UiDraw.SwatchKey(0x1C1C20), UiDraw.SwatchKey(0x6B6F76), UiDraw.SwatchKey(0x7A2430), UiDraw.SwatchKey(0x2F5A43), UiDraw.SwatchKey(0xC9B79A) };
        m.SwatchCount = 6;
        m.Swatch = 0;
        m.Actions = new[]
        {
            new UiActionModel { Label = t("change_character_clothes_save_button", "Save clothes") + (shop ? " " + Money(140) : ""), Kind = UiButtonKind.Primary, Key = UiAction.Alt }
        };
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the garage shop

    /// <summary>The garage shop's pages: its main page, the paint and the figures.</summary>
    public static UiGarageStripModel GarageStrip(UiWords t, string state)
    {
        var m = new UiGarageStripModel
        {
            Title = t("reallife_customshop_title", "Custom shop: {car}").Replace("{car}", "Stallion GT"), Leave = t("reallife_customshop_done", "Done"),
            Info = new[] { t("reallife_garage_money", "You have {amount}").Replace("{amount}", "$48,210"), t("reallife_customshop_total", "This visit: {amount}").Replace("{amount}", "$1,840") },
            InfoCount = 2, Prompts = Prompts(t), PromptCount = 3
        };
        var cards = new List<UiGarageCard>();
        string choose = t("reallife_garage_list_choose", "Choose");
        switch (state)
        {
            case "paint":
                cards.Add(new UiGarageCard { Name = t("reallife_garage_list_back_name", "Back"), Detail = t("reallife_customshop_total", "This visit: {amount}").Replace("{amount}", "$1,840"), Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = "Factory colour", Detail = t("reallife_garage_paint_prices", "Chart colour {palette}, mixed {custom}").Replace("{palette}", "$900").Replace("{custom}", "$1,400"), Button = choose, Enabled = true });
                string[] colours = { "Racing red", "Midnight blue", "Pearl white", "Graphite", "Lime", "Copper" };
                for (int i = 0; i < colours.Length; i++)
                {
                    cards.Add(new UiGarageCard { Name = colours[i], Detail = "$900", Button = i == 1 ? t("reallife_garage_list_chosen", "Chosen") : choose, Enabled = i != 1 });
                }
                break;
            case "figures":
                cards.Add(new UiGarageCard { Name = t("reallife_garage_list_back_name", "Back"), Detail = t("reallife_customshop_total", "This visit: {amount}").Replace("{amount}", "$1,840"), Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = t("reallife_tuning_tab_figures", "Figures"), Detail = "Power 310 hp as standard, 334 hp now. 0 to 100 km/h in 5.4 s, 5.1 s with the cart.", Note = true });
                cards.Add(new UiGarageCard { Name = "Braking", Detail = "100 to 0 km/h in 36 m as standard, 34 m now.", Note = true });
                cards.Add(new UiGarageCard { Name = "Weight", Detail = "1,480 kg as standard, 1,452 kg now.", Note = true });
                break;
            default:
                cards.Add(new UiGarageCard { Name = t("reallife_garage_pay", "Pay {amount}").Replace("{amount}", "$1,840"), Detail = "Paint, wheels and a spoiler", Button = t("reallife_garage_list_pay", "Pay"), Enabled = true, Main = true });
                cards.Add(new UiGarageCard { Name = t("reallife_garage_tab_paint", "Paint"), Detail = "Midnight blue, chart colour", Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = t("reallife_garage_tab_finish", "Finish"), Detail = "Gloss", Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = t("reallife_garage_tab_lights", "Lights"), Detail = "Standard", Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = t("reallife_garage_tab_body", "Body"), Detail = "Spoiler", Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = t("reallife_garage_tab_parts", "Parts"), Detail = "Mesh wheels, sport exhaust", Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = t("reallife_tuning_tab_figures", "Figures"), Detail = t("reallife_tuning_tab_figures_detail", "The dyno sheet"), Button = choose, Enabled = true });
                cards.Add(new UiGarageCard { Name = t("reallife_garage_stock", "All stock"), Detail = t("reallife_garage_list_stock_detail", "Puts the whole car back to standard."), Button = t("reallife_garage_list_reset", "Reset"), Enabled = true });
                break;
        }
        m.Cards = cards.ToArray();
        m.CardCount = m.Cards.Length;
        return m;
    }

    // The game's words in French over the words the checks and previews were given, as a French player reads the screen:
    // the game's own keys in French, the mod's new keys in English until the next translation batch.
    private static UiWords BuyFrench(UiWords t)
    {
        var fr = new Dictionary<string, string>
        {
            { "purchaseui_title_with_amounts", "Achat ({currentAmount}/{maxAmount} articles)" }, { "purchaseui_place_order", "Passer commande" }, { "common_cancel", "Annuler" },
            { "purchaseui_totalprice", "Prix total" }, { "purchaseui_waiting_in_queue", "En file d\u2019attente..." }, { "purchaseui_pay_taxes", "Payer les imp\u00F4ts" },
            { "common_amount", "Montant" }, { "common_price", "Prix" }, { "common_total", "Total" }, { "taxes_pay_item", "Imp\u00F4ts" },
            { "taxes_back_taxes_pay_item", "Arri\u00E9r\u00E9s d'imp\u00F4ts" }, { "casinoboat_ticket_pay_item", "Billet pour le bateau-casino" },
            { "common_vehicle", "V\u00E9hicule" }, { "purchasecar_specs", "Caract\u00E9ristiques" }, { "common_cargo_capacity", "Capacit\u00E9 de chargement" },
            { "vehicle_tank_capacity", "Capacit\u00E9 du r\u00E9servoir" }, { "vehicle_auto_park_support", "Assistance au stationnement automatique" },
            { "vehicle_max_speed", "Vitesse maximale" }, { "common_yes", "Oui" }, { "common_purchase", "Acheter" }, { "furniture_delivery_dialog_order", "Commander" },
            { "purchasevehicleui_delivertowarehouse", "Livrer \u00E0 l'entrep\u00F4t\n(Frais de {amount})" },
            { "purchasevehicleui_no_warehouses_available", "Aucun entrep\u00F4t avec des emplacements libres disponible" },
            { "dialog_select_address", "S\u00E9lectionner une adresse" }, { "itempanelui_vehicle_fuel", "Carburant" }, { "itempanelui_maintenance_condition", "\u00C9tat" },
            { "itempanelui_vehicle_parkingzone", "Zone de stationnement" }, { "itempanelui_parkingzone_legal_payed", "L\u00C9GAL ({price})" },
            { "itempanelui_parkingzone_illegal", "ILL\u00C9GAL" }, { "itempanelui_buttons_park_vehicle", "Garer le v\u00E9hicule" },
            { "itempanelui_buttons_auto_park", "Se garer automatiquement" }, { "itempanelui_buttons_sleep", "Dormir" }, { "itempanelui_sell", "Vendre" },
            { "itempanelui_buttons_discard", "Jeter" }, { "itempanelui_buttons_place_box", "Placer la bo\u00EEte" }, { "itempanelui_buttons_leave", "Quitter" },
            { "common_cargo", "Cargaison" }, { "itemoverlay_contents_headline", "Contenu" }, { "bizman_inventory_sell_all", "Tout vendre" },
            { "itempanelui_buttons_place", "Placer" }, { "action_take", "Prendre" }, { "change_character_clothes_title", "Changer de v\u00EAtements" },
            { "change_character_clothes_save_button", "Enregistrer la tenue" }, { "change_character_hair_title", "Changer de coiffure" },
            { "change_character_hair_save_button", "Changer de coiffure" }, { "plastic_surgery_title", "Chirurgie esth\u00E9tique" }, { "character_customization_torso", "Torse" },
            { "character_customization_legs", "Jambes" }, { "character_customization_shoes", "Chaussures" }, { "character_customization_hair", "Cheveux" },
            { "character_customization_head", "T\u00EAte" }, { "character_customization_skin_color", "Couleur de peau" }, { "common_apply_changes", "Appliquer les modifications" },
            { "common_revert", "R\u00E9tablir" }, { "intro_tooltip_randomize_appearance", "Randomiser l'apparence" }, { "dialog_leave_button", "Quitter" },
        };
        return (key, english) => fr.TryGetValue(key, out string words) ? words : t(key, english);
    }
}
