using RealLifeOverhaul.Ui;

/// <summary>
/// The world's smaller windows over the running game, with the words the game and the mod write there: the private driver
/// beside the phone with nothing hired, two cars to call under a contract, and a car on its way or waiting; the items list
/// of a store's goods, of an order's goods with amounts and add buttons, one search with results and one with none; a
/// sign's look with its neon and its bulbs and with its kind's options open; the preview terminal at its first choice, with
/// all four made and with the design's options open; the preview's bar; the colour picker's frame round the game's field.
/// The game's own words come through their keys; names a game gives (a firm, a car, a store) stay as they are.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>What the private driver shows: nothing hired, the cars to call, or a car on its way or waiting.</summary>
    public enum DriverState
    {
        Nothing,
        Cars,
        Coming,
        Waiting
    }

    private const string Firm = "Metro Chauffeurs";

    // ------------------------------------------------------------------------------------------------- the private driver

    /// <summary>The private driver's panel in one of its states.</summary>
    public static UiToolPanelModel WorldDriver(UiWords t, DriverState state)
    {
        var m = new UiToolPanelModel { Title = t("ba:private_driver", "Private driver"), Icon = UiIcon.Car, Close = false };
        var lines = new System.Collections.Generic.List<UiToolLine>();
        switch (state)
        {
            case DriverState.Nothing:
                m.State = t("reallife_ui_world_driver_state_none", "Nothing hired");
                lines.Add(new UiToolLine
                {
                    Kind = UiToolKind.Text,
                    Label = t("reallife_ui_world_driver_none", "You have no private driver yet. Sign a contract at {business}, then leave your cars in its garage and they will show here.")
                        .Replace("{business}", Firm),
                });
                lines.Add(new UiToolLine { Kind = UiToolKind.Link, Label = t("reallife_ui_world_driver_locate", "Locate {business}").Replace("{business}", Firm) });
                break;
            case DriverState.Cars:
                m.State = t("reallife_ui_world_driver_state_cars", "Your cars");
                lines.Add(new UiToolLine
                {
                    Kind = UiToolKind.Entry, Icon = UiIcon.Person, Label = t("ba:private_driver_type_premium", "Premium Driver"),
                    Value = t("reallife_ui_world_driver_contract", "{business}, {cars} of {most} cars, {fee} a day").Replace("{business}", Firm).Replace("{cars}", "2")
                        .Replace("{most}", "5").Replace("{fee}", "$420"),
                });
                lines.Add(Car(t, "Falcon GT", true));
                lines.Add(Car(t, "Harbor SUV", false));
                lines.Add(new UiToolLine { Kind = UiToolKind.Foot, Label = t("reallife_ui_world_driver_fee_due", "Paid when you first call today"), Value = "$420" });
                break;
            default:
                bool waiting = state == DriverState.Waiting;
                m.State = waiting ? t("reallife_ui_world_driver_state_waiting", "Waiting") : t("reallife_ui_world_driver_state_coming", "Car on its way");
                lines.Add(new UiToolLine
                {
                    Kind = UiToolKind.Entry, Icon = UiIcon.Car, Label = "Falcon GT",
                    Value = waiting ? t("reallife_ui_world_driver_waiting_line", "Waiting for you") : t("reallife_ui_world_driver_coming_line", "On its way to you"),
                });
                lines.Add(new UiToolLine
                {
                    Kind = UiToolKind.Text,
                    Label = waiting ? t("ba:private_driver_waiting", "Your private driver is waiting for you in {vehicleName}").Replace("{vehicleName}", "Falcon GT")
                        : t("reallife_ui_world_driver_coming", "Your driver is bringing the {vehicle}. Dismiss them if you no longer need the car.").Replace("{vehicle}", "Falcon GT"),
                });
                lines.Add(new UiToolLine
                {
                    Kind = UiToolKind.Buttons, Icon = UiIcon.Close, Label = t("reallife_ui_world_driver_dismiss", "Dismiss the {vehicle}").Replace("{vehicle}", "Falcon GT"),
                });
                break;
        }
        m.Lines = lines.ToArray();
        m.LineCount = m.Lines.Length;
        return m;
    }

    private static UiToolLine Car(UiWords t, string name, bool main) => new UiToolLine
    {
        Kind = UiToolKind.Entry, Icon = UiIcon.Car, Label = name, Value = t("reallife_ui_world_driver_ready", "In the garage, ready"),
        Action = t("reallife_ui_world_driver_call", "Call"), Main = main,
    };

    /// <summary>The private driver beside the phone at the screen's right, over the HUD.</summary>
    public static void WorldDriverScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, DriverState state) =>
        WorldTool(s, f, dark, k, t, WorldDriver(t, state), (w, h) => (f.Width - UiTokens.S8 - UiPhone.Width - UiTokens.S3 - w, UiTokens.S16 + UiTokens.S8));

    // ------------------------------------------------------------------------------------------------- the items list

    private static readonly string[] Goods =
    {
        "Flower stand", "Storage shelf", "Brass pendant light", "Potted palm", "Oak counter", "Bar stool", "Cash register", "Coat rack", "Display table", "Wall mirror",
        "Floor lamp", "Bench", "Shoe rack", "Plant pot"
    };

    private static readonly string[] Prices = { "$1,240", "$980", "$310", "$140", "$2,150", "$95", "$1,600", "$60", "$720", "$180", "$120", "$260", "$85", "$40" };

    /// <summary>The items list: a store's goods to look at, or an order's with amounts and add buttons, searched or not.</summary>
    public static UiItemsListModel WorldItems(UiWords t, bool order, string search = null, bool none = false)
    {
        var m = new UiItemsListModel
        {
            Title = order
                ? t("bizman_store_furniture_delivery_title", "{businessName} Inventory ({currentAmount}/{maxAmount} items selected)").Replace("{businessName}", "Fiorello Furniture")
                    .Replace("{currentAmount}", "14").Replace("{maxAmount}", "100")
                : t("bizman_store_inventory_title", "{businessName} Inventory").Replace("{businessName}", "Fiorello Furniture"),
            Placeholder = t("common_search", "Search"), Search = search,
        };
        var rows = new System.Collections.Generic.List<UiGoodsRow>();
        for (int i = 0; i < Goods.Length && !none; i++)
        {
            if (search != null && Goods[i].IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            var r = new UiGoodsRow { Name = Goods[i], Icon = UiIcon.Package, Sub = Prices[i] };
            if (order)
            {
                r.Sub = t("reallife_ui_world_items_each", "{price} each").Replace("{price}", Prices[i]);
                int[] amounts = { 4, 2, 6, 2 };
                if (i < amounts.Length)
                {
                    r.Amount = amounts[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                    r.CanLess = true;
                    r.CanMore = true;
                }
                else
                {
                    r.Add = i == 9 ? UiGoodsAdd.Full : UiGoodsAdd.Add;
                }
            }
            rows.Add(r);
        }
        m.Rows = rows.ToArray();
        m.RowCount = m.Rows.Length;
        bool searching = !string.IsNullOrEmpty(search);
        m.Count = searching ? t("reallife_ui_world_items_count", "{shown} of {all}").Replace("{shown}", m.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{all}", Goods.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null;
        m.EmptyTitle = searching ? t("reallife_ui_world_items_none_found", "Nothing matches") : t("reallife_ui_world_items_empty", "Nothing to list");
        m.EmptyBody = searching ? t("reallife_ui_world_items_none_found_body", "No goods match {search}. Try a shorter word.").Replace("{search}", search)
            : t("reallife_ui_world_items_empty_body", "There are no goods in this list yet.");
        m.ClearLabel = searching ? t("reallife_ui_world_items_clear", "Clear the search") : null;
        return m;
    }

    /// <summary>The items list centred over the world, its prompts on the bar.</summary>
    public static void WorldItemsScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiItemsListModel m)
    {
        var prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("common_close", "Close") },
        };
        WithBar(Over(HudBar(BarWords, false, false, false)), () => UiItemsList.Lay(s, m, f, dark, k, prompts, prompts.Length));
    }

    // ------------------------------------------------------------------------------------------------- a sign's look

    private static readonly int[] SignColours = { 0xF4F1E8, 0xFF3B5C, 0xFF8A3D, 0xFFD23F, 0x3DDC97, 0x2EC4F1, 0x5B6CFF, 0xB15BFF, 0xFF5BC8, 0x1E2A38 };

    /// <summary>A sign's look: its kind, its neon's colours and its bulbs', or its kind's options open.</summary>
    public static UiToolPanelModel WorldSign(UiWords t, bool bulbs, bool open)
    {
        var keys = new string[SignColours.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = UiDraw.SwatchKey(SignColours[i]);
        }
        string type = t("sign_type", "Type {number}");
        var lines = new System.Collections.Generic.List<UiToolLine>
        {
            new UiToolLine { Kind = UiToolKind.Choice, Label = t("sign_appearance_sign_type", "Sign Type"), Value = type.Replace("{number}", bulbs ? "5" : "3") },
            new UiToolLine
            {
                Kind = UiToolKind.Swatches, Label = t("sign_appearance_sign_light_color", "Neon Color"), Swatches = keys, SwatchCount = keys.Length, SwatchChosen = 5, CanAdd = true,
            },
        };
        if (bulbs)
        {
            lines.Add(new UiToolLine
            {
                Kind = UiToolKind.Swatches, Label = t("sign_appearance_sign_bulb_color", "Bulb Color"), Swatches = keys, SwatchCount = 6, SwatchChosen = 0, CanAdd = true,
            });
        }
        var m = new UiToolPanelModel { Title = t("sign_appearance", "Sign Appearance"), Icon = UiIcon.PaintBrush, Lines = lines.ToArray(), LineCount = lines.Count };
        if (open)
        {
            m.Open = 0;
            m.Options = new string[8];
            for (int i = 0; i < m.Options.Length; i++)
            {
                m.Options[i] = type.Replace("{number}", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            m.OptionCount = m.Options.Length;
            m.OptionChosen = 2;
        }
        return m;
    }

    /// <summary>A sign's look where the game's window stands, at the left over the street.</summary>
    public static void WorldSignScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool bulbs, bool open) =>
        WorldTool(s, f, dark, k, t, WorldSign(t, bulbs, open), (w, h) => (UiTokens.S16 * 2f, UiTokens.S16 * 2f));

    // ------------------------------------------------------------------------------------------------- the preview terminal

    private static readonly string[] TerminalLabels =
    {
        "reallife_ui_world_terminal_building", "Building", "reallife_ui_world_terminal_size", "Size", "reallife_ui_world_terminal_business", "Business",
        "reallife_ui_world_terminal_design", "Design"
    };

    /// <summary>The preview terminal at its first choice, with all four made, or with the design's options open.</summary>
    public static UiToolPanelModel WorldTerminal(UiWords t, int made, bool open)
    {
        string[] values = { "Retail", "Medium", "Clothing Store", "Boutique, warm wood" };
        string[] holders =
        {
            t("preview_terminal_select_building_type", "Select building type"), t("preview_terminal_select_building_size", "Select building size"),
            t("preview_terminal_select_business_type", "Select business type"), t("preview_terminal_select_design", "Select design")
        };
        var lines = new System.Collections.Generic.List<UiToolLine>
        {
            new UiToolLine { Kind = UiToolKind.Text, Label = t("reallife_ui_world_terminal_note", "Choose a building and a design to walk through it before you buy.") },
        };
        int shown = System.Math.Min(4, made + 1);
        for (int i = 0; i < shown; i++)
        {
            lines.Add(new UiToolLine { Kind = UiToolKind.Choice, Label = t(TerminalLabels[i * 2], TerminalLabels[i * 2 + 1]), Value = i < made ? values[i] : holders[i] });
        }
        lines.Add(new UiToolLine
        {
            Kind = UiToolKind.Buttons, Icon = UiIcon.Eye, Label = t("preview_terminal_preview_button", "Preview Design"), Disabled = made < 4,
            Value = t("reallife_ui_world_terminal_blueprints", "Blueprints"),
        });
        var m = new UiToolPanelModel { Title = t("ba:itemname_previewterminal", "Preview Terminal"), Icon = UiIcon.Eye, Lines = lines.ToArray(), LineCount = lines.Count };
        if (open)
        {
            m.Open = shown;   // the design's choice, the last
            m.Options = new[] { "Boutique, warm wood", "Boutique, white", "Outlet", "Flagship", "Corner shop", "Gallery", "Minimal", "Loft" };
            m.OptionCount = m.Options.Length;
            m.OptionChosen = 0;
        }
        return m;
    }

    /// <summary>The preview terminal centred over the paused world.</summary>
    public static void WorldTerminalScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, int made, bool open) =>
        WorldTool(s, f, dark, k, t, WorldTerminal(t, made, open), (w, h) => ((f.Width - w) / 2f, System.Math.Max(UiTokens.S8, (f.BandY - h) / 2f)));

    // ------------------------------------------------------------------------------------------------- the preview's bar

    /// <summary>The preview's bar: what is previewed, the walls' state, Toggle walls beside Cancel preview on the game's Cancel key.</summary>
    public static UiToolPanelModel WorldPreview(UiWords t, IUiKeys k, bool design)
    {
        var lines = new[]
        {
            new UiToolLine
            {
                Kind = UiToolKind.Buttons, Label = t("buildingpreview_cancel_preview", "Cancel preview"), Value = t("common_toggle_walls", "Toggle walls"),
                Key = new UiGameKey { Name = k != null && k.Glyph(UiAction.Back) == UiIcon.None ? k.Name(UiAction.Back) : null, Glyph = k != null ? k.Glyph(UiAction.Back) : UiIcon.None },
            },
        };
        return new UiToolPanelModel
        {
            Title = design ? "Clothing Store, Boutique warm wood (Retail, Medium)" : "551 Seventh Avenue (Retail, Medium)", Icon = UiIcon.Eye, Close = false,
            State = design ? t("reallife_ui_world_preview_walls_cut", "Walls cut away") : t("reallife_ui_world_preview_walls_up", "Walls up"), Lines = lines, LineCount = lines.Length,
        };
    }

    /// <summary>The preview's bar at the top of the screen, the HUD hidden as the game hides it in a preview.</summary>
    public static void WorldPreviewScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool design)
    {
        UiToolPanelModel m = WorldPreview(t, k, design);
        const float w = 480f;
        UiToolPanel.Lay(s, m, f, dark, k, "building preview", UiDraw.SnapDown((f.Width - w) / 2f), UiTokens.S8, w);
    }

    // ------------------------------------------------------------------------------------------------- the colour picker

    /// <summary>The colour picker's frame round the game's colour field, which the kit never draws.</summary>
    public static void WorldColourScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var lines = new[]
        {
            new UiToolLine { Kind = UiToolKind.Buttons, Icon = UiIcon.Plus, Label = t("common_add", "Add"), Value = t("common_cancel", "Cancel") },
        };
        var m = new UiToolPanelModel { Title = t("custom_color_picker_headline", "Choose a custom color"), Icon = UiIcon.PaintBrush, Lines = lines, LineCount = lines.Length };
        const float fw = 300f, fh = 340f;
        s.Begin("colour picker", f.Width, f.Height, dark);
        s.Pointer = f.Pointer;
        s.Floats = true;
        UiToolPanel.Around(s, UiAt.Screen, m, UiDraw.SnapDown((f.Width - fw) / 2f), UiDraw.SnapDown((f.BandY - fh) / 2f), fw, fh, k);
    }

    // ------------------------------------------------------------------------------------------------- shared

    // A small panel over the HUD at its place, the HUD's bar under it.
    private static void WorldTool(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiToolPanelModel m, System.Func<float, float, (float, float)> place)
    {
        var hud = new UiHudModel();
        Walking(hud, t);
        WithBar(HudBar(BarWords, false, false, false), () =>
        {
            UiHud.Lay(s, hud, f, dark, k);
            float w = UiToolPanel.Width, h = UiToolPanel.Height(m, w);
            (float x, float y) = place(w, h);
            UiToolPanel.Draw(s, UiAt.Screen, m, UiDraw.SnapDown(x), UiDraw.SnapDown(y), w, k);
        });
    }
}
