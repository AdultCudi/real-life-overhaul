using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    /// <summary>The states of the interior designer the previews and the checks draw.</summary>
    public static readonly string[] DesignerStates =
    {
        "furniture", "furniture_needed", "furniture_empty", "floors", "walls", "palette", "producer", "queue", "security", "time", "blueprint", "blueprint_bad", "business_type",
        "folded", "hand", "second_floor"
    };

    /// <summary>The keys the designer's chips are named with while the keys lead: the game's own hint keys on the number actions.</summary>
    internal sealed class DesignerKeys : IUiKeys
    {
        private static readonly string[] Names = { "R", "Shift", "Del", "Ctrl Z", "Drag", "Esc" };
        private readonly IUiKeys _inner;

        public DesignerKeys(IUiKeys inner)
        {
            _inner = inner;
        }

        public UiPad Pad => _inner.Pad;

        public string Name(UiAction action)
        {
            int at = action - UiAction.Pick1;
            return at >= 0 && at < Names.Length ? Names[at] : _inner.Name(action);
        }

        public UiIcon Glyph(UiAction action)
        {
            int at = action - UiAction.Pick1;
            return at >= 0 && at < Names.Length ? UiIcon.None : _inner.Glyph(action);
        }
    }

    private static string Of(UiWords t, string key, string english) => t(key, english);

    private static string DesignerFill(string text, params string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return text;
    }

    /// <summary>The designer as the kit draws it for a state: the info panel, the bar with the tool row, and the chosen tool's panel.</summary>
    public static UiDesignerModel Designer(UiWords t, string state)
    {
        var m = new UiDesignerModel();
        int tool = state == "furniture" || state == "furniture_needed" || state == "furniture_empty" || state == "folded" || state == "second_floor" ? (int)UiDesignerTool.Furniture
            : state == "floors" ? (int)UiDesignerTool.Floor : state == "walls" ? (int)UiDesignerTool.Wall : state == "palette" ? (int)UiDesignerTool.Palette
            : state == "producer" ? (int)UiDesignerTool.Producer : state == "queue" ? (int)UiDesignerTool.Queue : state == "security" ? (int)UiDesignerTool.Security
            : state == "time" ? (int)UiDesignerTool.TimeOfDay : state.StartsWith("blueprint", StringComparison.Ordinal) || state == "business_type" ? (int)UiDesignerTool.SaveBlueprint
            : (int)UiDesignerTool.Hand;
        DesignerBar(t, m, tool, state == "second_floor");
        DesignerInfo(t, m, state == "folded", state == "second_floor");
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Pick1, Label = "Rotate" }, new UiPrompt { Action = UiAction.Pick2, Label = "Free place" },   // the game's own hint lines, in its own words
            new UiPrompt { Action = UiAction.Pick3, Label = "Sell" }, new UiPrompt { Action = UiAction.Pick4, Label = "Undo" }
        };
        m.PromptCount = m.Prompts.Length;
        switch (state)
        {
            case "furniture":
            case "folded":
            case "second_floor":
                m.Panel = UiDesignerPanelKind.Furniture;
                DesignerCatalogue(t, m.Catalogue, false, false);
                break;
            case "furniture_needed":
                m.Panel = UiDesignerPanelKind.Furniture;
                DesignerCatalogue(t, m.Catalogue, true, false);
                break;
            case "furniture_empty":
                m.Panel = UiDesignerPanelKind.Furniture;
                DesignerCatalogue(t, m.Catalogue, false, true);
                break;
            case "floors":
            case "walls":
                m.Panel = UiDesignerPanelKind.Materials;
                DesignerMaterials(t, m.Materials, state == "walls");
                break;
            case "producer":
            case "queue":
            case "security":
                m.Panel = UiDesignerPanelKind.Items;
                DesignerItems(t, m.Items, state);
                break;
            case "time":
                m.Panel = UiDesignerPanelKind.Time;
                DesignerTime(t, m.Time);
                break;
            case "blueprint":
            case "blueprint_bad":
                m.Panel = UiDesignerPanelKind.Blueprint;
                DesignerBlueprint(t, m.Blueprint, state == "blueprint_bad");
                break;
            case "business_type":
                m.Panel = UiDesignerPanelKind.BusinessType;
                DesignerType(t, m.BusinessType);
                break;
            case "palette":
                m.Panel = UiDesignerPanelKind.Note;
                m.Note = new UiDesignerNote
                {
                    Title = t(DesignerGlyphs.ToolKeys[(int)UiDesignerTool.Palette], DesignerGlyphs.ToolEnglish[(int)UiDesignerTool.Palette]), Icon = DesignerGlyphs.ToolIcons[(int)UiDesignerTool.Palette],
                    Body = Of(t, "reallife_ui_designer_note_palette", "Click a piece of furniture that has colours to choose them in the room.")
                };
                break;
        }
        return m;
    }

    private static void DesignerBar(UiWords t, UiDesignerModel m, int tool, bool floors)
    {
        m.Tools.Tools = new UiTool[(int)UiDesignerTool.Count];
        for (int i = 0; i < m.Tools.Tools.Length; i++)
        {
            m.Tools.Tools[i] = new UiTool { Icon = DesignerGlyphs.ToolIcons[i], Name = t(DesignerGlyphs.ToolKeys[i], DesignerGlyphs.ToolEnglish[i]) };
        }
        m.Tools.Tool = tool == (int)UiDesignerTool.Hand ? (int)UiDesignerTool.Hand : tool;
        for (int i = 0; i < (int)UiDesignerStep.Count; i++)
        {
            m.StepIcons[i] = DesignerGlyphs.StepIcons[i];
            m.StepNames[i] = t(DesignerGlyphs.StepKeys[i], DesignerGlyphs.StepEnglish[i]);
            m.StepShown[i] = i < (int)UiDesignerStep.FloorDown || floors || i == (int)UiDesignerStep.Walls && true;
        }
        m.StepShown[(int)UiDesignerStep.FloorDown] = floors;
        m.StepShown[(int)UiDesignerStep.FloorUp] = floors;
        m.StepOff[(int)UiDesignerStep.Redo] = true;
        m.StepIcons[(int)UiDesignerStep.Walls] = DesignerGlyphs.WallsIcon(0);
        m.Bar = new UiStatusModel { Time = "08:45", Day = "Monday (Day 2)", DayWidest = "Wednesday (Day 4)", Paused = true, Speed = 1, TimeLocked = true };
        m.Balance = "$1.9M";
        m.OnApply = DesignerFill(Of(t, "reallife_ui_designer_on_apply", "{amount} on apply"), "amount", "-$15,140");
        m.OnApplyTrend = -1;
        m.ApplyLabel = t("common_apply", "Apply");
        m.CloseLabel = t("common_close", "Close");
    }

    private static void DesignerInfo(UiWords t, UiDesignerModel m, bool folded, bool floors)
    {
        UiDesignerInfo i = m.Info;
        i.Title = t("interior_designer", "Interior designer");
        i.FloorWords = floors ? DesignerFill(Of(t, "reallife_ui_designer_floor", "Floor {n}"), "n", "2") : null;
        i.Mark = "BC";
        i.MarkTone = UiRole.Chart7;
        i.Name = "Bloom & Co";
        i.Facts = "Florist, 214 5th Avenue, Garment District";
        i.Folded = folded;
        i.EditTitle = Of(t, "reallife_ui_designer_edit", "This edit");
        string[] labels =
        {
            DesignerFill(Of(t, "reallife_ui_designer_cost_furniture", "Furniture bought ({n})"), "n", "9"), DesignerFill(Of(t, "reallife_ui_designer_cost_floor", "Floor, {area} m2"), "area", "64"),
            DesignerFill(Of(t, "reallife_ui_designer_cost_sold", "Sold back ({n})"), "n", "2"), Of(t, "reallife_ui_designer_cost_total", "Total on apply")
        };
        string[] values = { "$14,860", "$1,180", "-$900", "$15,140" };
        for (int k = 0; k < 4; k++)
        {
            i.Cost[k] = new UiDesignerCostLine { Label = labels[k], Value = values[k], Ink = k == 2 ? UiRole.OkText : UiRole.Text, Total = k == 3 };
        }
        i.CostCount = 4;
        i.CapacityShown = true;
        i.CapacityTitle = t("bizman_customers_capacity", "Customer capacity");
        i.CapacityFigure = "56";
        i.CapacityOf = DesignerFill(Of(t, "reallife_ui_designer_of", "of {n}"), "n", "80");
        i.CapacityFill = 70;
        i.CapacityChange = "+8";
        i.CapacityTone = UiRole.Accent;
        i.ScoreShown = true;
        i.ScoreTitle = Of(t, "reallife_ui_designer_score", "Interior score");
        i.ScoreFigure = "74%";
        i.ScoreWants = DesignerFill(Of(t, "reallife_ui_designer_wants", "district wants {n}%"), "n", "70");
        i.ScoreFill = 74;
        i.ScoreChange = "+3%";
        i.ScoreTone = UiRole.Ok;
        i.RequireShown = true;
        i.RequireTitle = DesignerFill(Of(t, "reallife_ui_designer_needs", "{type} needs"), "type", "Florist");
        string pair = Of(t, "reallife_ui_designer_count_of", "{have} of {need}");
        i.RequireFigure = DesignerFill(pair, "have", "4", "need", "6");
        i.ShowLabel = Of(t, "reallife_ui_designer_show", "Show");
        i.HowLabel = Of(t, "reallife_ui_designer_how", "How");
        string[] titles = { "Cash register", "Flower stands", "Lighting", "Security camera", "Storage shelf", "Restroom for staff" };
        string[] counts = { "2", "6", null, null, DesignerFill(pair, "have", "0", "need", "1"), null };
        for (int k = 0; k < 6; k++)
        {
            i.Require[k] = new UiDesignerRequirement { Title = titles[k], Met = k < 4, Count = counts[k], CanShow = k == 4, CanHelp = k >= 4 };
        }
        i.RequireCount = 6;
    }

    private static readonly string[] CardNames =
    {
        "Flower stand", "Tiered flower shelf", "Storage shelf", "Long counter", "Chiller display", "Stock room fridge", "Potted palm", "Brass pendant light", "Lounge armchair", "Low oak table",
        "Espresso station", "Window screen", "Cash register", "Bench seat", "Floor lamp", "Wall clock", "Coat stand", "Ceiling fan"
    };

    private static readonly string[] CardPrices = { "$1,240", "$1,860", "$980", "$2,400", "$3,150", "$2,720", "$140", "$310", "$560", "$290", "$1,950", "$1,100", "$420", "$380", "$210", "$90", "$120", "$260" };
    private static readonly string[] CardKinds = { "holds 24", "holds 36", "holds 48", null, "holds 24", "holds 60", null, "ceiling", null, null, null, "wall", null, null, null, "wall", null, "ceiling" };

    // What a piece is for the room, in words: what it holds, or that it hangs from the ceiling or the wall.
    private static string KindOf(UiWords t, string kind)
    {
        if (kind == null)
        {
            return null;
        }
        if (kind.StartsWith("holds", StringComparison.Ordinal))
        {
            return DesignerFill(Of(t, "reallife_ui_designer_holds", "holds {n}"), "n", kind.Substring(6));
        }
        return kind == "ceiling" ? Of(t, "reallife_ui_designer_ceiling", "ceiling") : Of(t, "reallife_ui_designer_wall", "wall");
    }

    private static void DesignerCatalogue(UiWords t, UiDesignerCatalogue c, bool needed, bool empty)
    {
        string[] tabs = { t("interior_designer_furniture_panel_category_allitems", "All Items"), t("interior_designer_furniture_panel_category_decoration", "Decoration"),
            t("interior_designer_furniture_panel_category_lights", "Lights"), t("interior_designer_furniture_panel_category_security", "Security"),
            t("interior_designer_furniture_panel_category_tablesandchairs", "Tables & Chairs"), t("interior_designer_furniture_panel_category_warehouseandfactory", "Warehouse & Factory"),
            t("interior_designer_furniture_panel_category_cinemaandtheater", "Cinema & Theater"), t("interior_designer_furniture_panel_category_outdoor", "Outdoor") };
        string[] counts = { "212", "46", "22", "9", "24", "38", "12", "20" };
        for (int k = 0; k < tabs.Length; k++)
        {
            c.Tabs[k] = tabs[k];
            c.TabCounts[k] = counts[k];
        }
        c.TabCount = tabs.Length;
        c.Tab = 0;
        c.SearchPlaceholder = t("common_search", "Search");
        c.FavouritesLabel = t("interior_designer_furniture_panel_category_favorites", "Favorites");
        c.NeededLabel = Of(t, "reallife_ui_designer_needed", "NEEDED");
        c.ColoursLabel = Of(t, "reallife_ui_designer_colours", "Colours");
        c.PlaceLabel = Of(t, "reallife_ui_designer_place", "Place");
        c.FavouriteLabel = Of(t, "reallife_ui_designer_favourite", "Favourite");
        c.ClearLabel = Of(t, "reallife_ui_designer_clear", "Clear");
        c.NoneTitle = Of(t, "reallife_ui_designer_none_title", "Nothing here");
        c.NoneBody = Of(t, "reallife_ui_designer_none_body", "No furniture matches. Clear the search or pick another category.");
        c.Filtered = needed;
        c.FilterNote = DesignerFill(Of(t, "reallife_ui_designer_showing", "Showing what {need} needs"), "need", "Storage shelf");
        int n = empty ? 0 : needed ? 3 : CardNames.Length;
        c.Cards = new UiDesignerCard[n];
        for (int k = 0; k < n; k++)
        {
            int at = needed ? new[] { 2, 4, 5 }[k] : k;
            c.Cards[k] = new UiDesignerCard
            {
                Index = at, Name = CardNames[at], Price = CardPrices[at], Kind = KindOf(t, CardKinds[at]),
                Picture = "designer:sample:" + at, Needed = at == 0 || at == 2, Favourite = at % 3 == 0
            };
        }
        c.CardCount = n;
        c.Chosen = n > 0 ? 0 : -1;
        if (n == 0)
        {
            return;
        }
        c.DetailName = c.Cards[0].Name;
        c.DetailKind = needed ? "Storage, holds 48" : "Decoration, " + DesignerFill(Of(t, "reallife_ui_designer_holds", "holds {n}"), "n", "24");
        string[] lab = { t("common_price", "Price"), t("bizman_customers_capacity", "Customer capacity"), Of(t, "reallife_ui_designer_fills", "Needed for") };
        string[] val = { c.Cards[0].Price, "+4", needed ? "Storage shelf, " + DesignerFill(Of(t, "reallife_ui_designer_count_of", "{have} of {need}"), "have", "0", "need", "1") : "Flower stands, " + DesignerFill(Of(t, "reallife_ui_designer_count_of", "{have} of {need}"), "have", "4", "need", "6") };
        UiRole[] ink = { UiRole.Text, UiRole.OkText, UiRole.WarnText };
        for (int k = 0; k < 3; k++)
        {
            c.DetailLabels[k] = lab[k];
            c.DetailValues[k] = val[k];
            c.DetailInks[k] = ink[k];
        }
        c.DetailCount = 3;
        int[] rgb = { 0x9B6A3C, 0xD8C3A5, 0x2E3B4A, 0xF2F2F0, 0x4F8A5B };
        for (int k = 0; k < rgb.Length; k++)
        {
            c.Swatches[k] = UiDraw.SwatchKey(rgb[k]);
        }
        c.SwatchCount = rgb.Length;
    }

    private static readonly string[] MaterialNames =
    {
        "Chequered tile", "Terracotta brick", "Blue lattice", "Leopard carpet", "Large white tile", "Oak herringbone", "Walnut plank", "Whitewashed board", "Painted white", "Sky blue paint",
        "Cyan resin", "Sandstone slab", "Woven dark tile", "Sand screed"
    };

    private static readonly int[] MaterialPrices = { 28, 30, 34, 36, 30, 32, 35, 35, 18, 18, 22, 48, 50, 26 };

    private static void DesignerMaterials(UiWords t, UiDesignerMaterials c, bool walls)
    {
        c.Tabs[0] = t("interior_designer_floor", "Floor");
        c.Tabs[1] = t("interior_designer_wall", "Wall");
        c.TabCounts[0] = "48";
        c.TabCounts[1] = "41";
        c.Tab = walls ? 1 : 0;
        c.SearchPlaceholder = t("common_search", "Search");
        c.NoneTitle = Of(t, "reallife_ui_designer_none_title", "Nothing here");
        c.NoneBody = Of(t, "reallife_ui_designer_none_materials", "No material matches. Clear the search.");
        c.PickBody = walls ? Of(t, "reallife_ui_designer_pick_wall", "Pick a paint, then click the walls in the room to paint them.")
            : Of(t, "reallife_ui_designer_pick_floor", "Pick a floor, then click or drag over the room to lay it.");
        c.Cards = new UiDesignerMaterial[MaterialNames.Length];
        for (int k = 0; k < MaterialNames.Length; k++)
        {
            c.Cards[k] = new UiDesignerMaterial { Index = k, Name = MaterialNames[k], Price = "$" + MaterialPrices[k], Picture = "designer:sample:material:" + k, HasShades = k == 5 };
        }
        c.CardCount = MaterialNames.Length;
        c.Chosen = 5;
        c.DetailName = MaterialNames[5];
        c.DetailKind = t(walls ? "interior_designer_wall" : "interior_designer_floor", walls ? "Wall" : "Floor") + ", " + DesignerFill(Of(t, "reallife_ui_designer_shades", "{n} shades"), "n", "4");
        c.DetailLabels[0] = Of(t, "reallife_ui_designer_price_piece", "Price a piece");
        c.DetailValues[0] = "$32";
        c.DetailLabels[1] = Of(t, "reallife_ui_designer_score_if", "Interior score if laid");
        c.DetailValues[1] = "+2%";
        c.DetailInks[1] = UiRole.OkText;
        c.DetailCount = 2;
        c.ShadeLabel = Of(t, "reallife_ui_designer_shade", "Shade");
        int[] rgb = { 0xE8C27A, 0x9C6B43, 0xC9A57C, 0xD9CBB5 };
        for (int k = 0; k < rgb.Length; k++)
        {
            c.Shades[k] = UiDraw.SwatchKey(rgb[k]);
        }
        c.ShadeCount = rgb.Length;
        c.ShadeChosen = 0;
        if (!walls)
        {
            c.LayLabel = Of(t, "reallife_ui_designer_lay", "Lay the whole floor");
            c.LayFigure = "$1,638";
        }
    }

    private static void DesignerItems(UiWords t, UiDesignerItems m, string state)
    {
        string[] names = { "Flower stand", "Tiered flower shelf", "Chiller display", "Stock room fridge", "Bouquet counter", "Tool shelf" };
        m.Title = state == "producer" ? t("interior_designer_producer_panel", "Product Selector") : state == "queue" ? t("interior_designer_queue_designer", "Customer Queue Designer")
            : t("interior_designer_security_coverage", "Security Coverage");
        m.Icon = state == "producer" ? UiIcon.Factory : state == "queue" ? UiIcon.People : UiIcon.ShieldCheck;
        m.Note = state == "producer" ? Of(t, "reallife_ui_designer_note_producer", "Pick a shelf, then choose what it shows in the room.")
            : state == "queue" ? Of(t, "reallife_ui_designer_note_queue", "Pick a counter, then draw where its customers wait.")
            : Of(t, "reallife_ui_designer_note_security", "A camera that sees a shelf secures it. Move a camera to cover more.");
        m.NoteInk = UiRole.Text2;
        m.EmptyTitle = Of(t, "reallife_ui_designer_none_title", "Nothing here");
        m.EmptyBody = t("interior_designer_no_producers_in_building", "There are no shelves in this building.");
        m.Count = state == "queue" ? 2 : names.Length;
        m.Items = new UiDesignerItem[m.Count];
        for (int k = 0; k < m.Count; k++)
        {
            string line = state == "producer" ? (k < 4 ? "Roses" : Of(t, "reallife_ui_designer_not_set", "Not set")) : state == "queue" ? null
                : k < 4 ? Of(t, "reallife_ui_designer_secured", "Secured") : Of(t, "reallife_ui_designer_not_covered", "Not covered");
            m.Items[k] = new UiDesignerItem
            {
                Index = k, Name = names[k], Line = line, Picture = "designer:sample:" + k,
                Glyph = state == "security" ? (k < 4 ? UiIcon.Check : UiIcon.Warning) : UiIcon.None, GlyphTone = k < 4 ? UiRole.Ok : UiRole.Warn, Chosen = k == 1
            };
        }
        if (state == "security")
        {
            m.Meter = 64;
            m.MeterWords = DesignerFill(t("interior_designer_security_rating", "Security Rating {amount}"), "amount", "64%");
            m.MeterTone = UiRole.Warn;
        }
    }

    private static void DesignerTime(UiWords t, UiDesignerTime d)
    {
        d.Title = Of(t, "reallife_ui_designer_time_title", "Time of day");
        d.Sub = Of(t, "reallife_ui_designer_time_sub", "The light in the room");
        d.Time = "14:30";
        d.Clock = DesignerFill(Of(t, "reallife_ui_designer_clock", "The game clock stays at {time}"), "time", "08:45");
        d.Share = (14.5f * 60f - 300f) / 1140f;
        d.Figure = "14:30";
        var marks = new int[8];
        int n = DesignerRules.Marks(300f, 1440f, marks);
        d.Marks = new string[n];
        d.MarkShares = new float[n];
        for (int k = 0; k < n; k++)
        {
            d.Marks[k] = DesignerRules.Clock(marks[k]);
            d.MarkShares[k] = DesignerRules.Share(marks[k], 300f, 1440f);
        }
        d.LessLabel = "-10";
        d.MoreLabel = "+10";
        d.Note = Of(t, "reallife_ui_designer_time_note", "Move the slider to see the room in morning, evening or night light before you decide on lamps and windows. Nothing here is saved.");
    }

    private static void DesignerBlueprint(UiWords t, UiDesignerBlueprint b, bool bad)
    {
        b.Title = Of(t, "reallife_ui_designer_bp_title", "Save blueprint");
        b.Sub = "Bloom & Co, 214 5th Avenue";
        b.NameLabel = Of(t, "reallife_ui_designer_bp_name", "Blueprint name");
        b.Name = bad ? "Bloom & Co v1.2" : "Bloom & Co florist, third layout";
        b.NamePlaceholder = Of(t, "reallife_ui_designer_bp_placeholder", "Name this layout");
        b.NameBad = bad;
        b.NameHint = bad ? Of(t, "reallife_ui_designer_bp_bad", "A dot or a slash cannot be in the name.") : Of(t, "reallife_ui_designer_bp_hint", "Letters, numbers and spaces. No dots or slashes.");
        b.FactsTitle = Of(t, "reallife_ui_designer_bp_saved", "What is saved");
        b.FactLabels[0] = Of(t, "reallife_ui_designer_bp_business", "Business");
        b.FactValues[0] = "Florist";
        b.FactLabels[1] = Of(t, "reallife_ui_designer_bp_floor", "Floor");
        b.FactValues[1] = DesignerFill(Of(t, "reallife_ui_designer_bp_area", "{n} m2"), "n", "64");
        b.FactLabels[2] = Of(t, "reallife_ui_designer_bp_furniture", "Furniture and fittings");
        b.FactValues[2] = DesignerFill(Of(t, "reallife_ui_designer_bp_items", "{n} items"), "n", "61");
        b.FactLabels[3] = Of(t, "reallife_ui_designer_bp_mods", "Mods it needs");
        b.FactValues[3] = DesignerFill(Of(t, "reallife_ui_designer_bp_mods_n", "{n} active mods"), "n", "4");
        b.FactCount = 4;
        b.CheckWords[0] = Of(t, "reallife_ui_designer_bp_free", "The name is free");
        b.CheckSide[0] = Of(t, "reallife_ui_designer_bp_free_side", "Your library has no blueprint called this");
        b.CheckMet[0] = !bad;
        b.CheckWords[1] = Of(t, "reallife_ui_designer_bp_gifts", "No special gifts in the room");
        b.CheckSide[1] = Of(t, "reallife_ui_designer_bp_gifts_side", "Gifts cannot go in a blueprint");
        b.CheckMet[1] = true;
        b.CheckCount = 2;
        b.Foot = Of(t, "reallife_ui_designer_bp_foot", "Saved to your library, under Blueprints in the pause menu");
        b.SaveLabel = Of(t, "reallife_ui_designer_bp_save", "Save blueprint");
        b.SaveOff = bad;
    }

    private static void DesignerType(UiWords t, UiDesignerBusinessType b)
    {
        b.Title = t("dropdown_selector_header", "Select an option");
        b.Body = t("blueprint_creator_select_business_type", "This blueprint doesn't have a business type set up yet. Please select a business type to continue.");
        b.Options = new[] { "Bakery", "Barber", "Bookshop", "Cafe", "Clothing store", "Florist", "Grocery", "Restaurant" };
        b.Count = b.Options.Length;
        b.Chosen = 5;
        b.ContinueLabel = t("common_ok", "OK");
        b.CancelLabel = t("common_cancel", "Cancel");
        b.Open = false;
    }

    /// <summary>The designer's panels over the room, with its own bar.</summary>
    public static void DesignerScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state)
    {
        UiDesignerModel m = Designer(t, state);
        UiDesignerPanels.Lay(s, m, f, dark, new DesignerKeys(k));
    }
}
