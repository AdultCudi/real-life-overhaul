using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The interior designer on the kit, after Cities: Skylines II's build tools: the status bar's app row given over to a tool row (the one in hand
    /// cyan), with undo, redo, the floors and the walls after it, the business's cost, customers, score and requirements as an info panel docked at the
    /// top left, the game's own control hints as key chips at the top right, the money with Apply and Close at the bar's right end, and the chosen
    /// tool's panel rising over the bar (the furniture catalogue, the walls and floors, the item lists, the time of day, the blueprint). The room and its
    /// overlays stay the game's, in full view between them. Draws only; every press comes back as a command. Commands 9560 to 9599 (checked free).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static partial class UiDesignerPanels
    {
        public const int ToolCommand = 9560, StepCommand = 9561, ApplyCommand = 9562, CloseCommand = 9563, FoldCommand = 9564, ShowCommand = 9565, HowCommand = 9566,
            CardCommand = 9568, SearchCommand = 9569, FavouritesCommand = 9570, StarCommand = 9571, PlaceCommand = 9572, ClearCommand = 9573,
            MaterialTabCommand = 9574, MaterialCardCommand = 9575, MaterialSearchCommand = 9576, ShadeCommand = 9577, LayCommand = 9578, ItemCommand = 9579,
            TimeSliderCommand = 9580, TimeLessCommand = 9581, TimeMoreCommand = 9582, NameCommand = 9583, SaveCommand = 9584, TypeOptionCommand = 9585,
            TypeContinueCommand = 9586, TypeCancelCommand = 9587, TypeFieldCommand = 9588, TabStripCommand = 9589;

        /// <summary>Every command the designer answers, for the layout checks.</summary>
        public static readonly int[] Commands =
        {
            ToolCommand, StepCommand, ApplyCommand, CloseCommand, FoldCommand, ShowCommand, HowCommand, CardCommand, SearchCommand, FavouritesCommand,
            StarCommand, PlaceCommand, ClearCommand, MaterialTabCommand, MaterialCardCommand, MaterialSearchCommand, ShadeCommand, LayCommand, ItemCommand,
            TimeSliderCommand, TimeLessCommand, TimeMoreCommand, NameCommand, SaveCommand, TypeOptionCommand, TypeContinueCommand, TypeCancelCommand, TypeFieldCommand, TabStripCommand
        };

        public const float InfoWidth = 440f;       // px: the info panel, as every info panel of the kit
        public const float Margin = UiTokens.S4;   // px: from the screen's edges
        public const float PanelGap = UiTokens.S3; // px: between the rising panel and the bar
        public const float StepSize = 44f;         // px: undo, redo, the floors and the walls on the bar
        private const float BarTilePx = UiFrame.Toolbar;       // px: a tool's tile on the bar, as every tile of the bar
        private const float PillGlyph = 28f;
        private const byte InfoRegion = 150, PanelRegion = 151, DetailRegion = 152, BarRegion = 153, EndRegion = 154;

        private static float Lh(UiStyle style) => UiDraw.Snap(UiTextRules.LineHeight(style));

        /// <summary>The designer as one scene over the room: the pieces, the key chips and the status bar with the tool row standing on it.</summary>
        public static void Lay(UiScene s, UiDesignerModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("designer", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            PanelBox(m, frame, out float px, out float py, out float pw, out float ph);
            Info(s, m, frame, py);
            switch (m.Panel)
            {
                case UiDesignerPanelKind.Furniture:
                    Catalogue(s, UiAt.Screen.InRegion(PanelRegion), m.Catalogue, px, py, pw, ph, keys, RowsFor(frame.Height));
                    break;
                case UiDesignerPanelKind.Materials:
                    Materials(s, UiAt.Screen.InRegion(PanelRegion), m.Materials, px, py, pw, ph, keys, RowsFor(frame.Height));
                    break;
                case UiDesignerPanelKind.Items:
                    Items(s, UiAt.Screen.InRegion(PanelRegion), m.Items, px, py, pw, ph);
                    break;
                case UiDesignerPanelKind.Time:
                    Time(s, UiAt.Screen.InRegion(PanelRegion), m.Time, px, py, pw, ph);
                    break;
                case UiDesignerPanelKind.Blueprint:
                    Blueprint(s, UiAt.Screen.InRegion(PanelRegion), m.Blueprint, px, py, pw, ph, keys);
                    break;
                case UiDesignerPanelKind.BusinessType:
                    BusinessType(s, UiAt.Screen.InRegion(PanelRegion), m.BusinessType, px, py, pw, ph, keys);
                    break;
                case UiDesignerPanelKind.Note:
                    Note(s, UiAt.Screen.InRegion(PanelRegion), m.Note, px, py, pw, ph);
                    break;
            }
            Chips(s, m, frame, keys);
            Bar(s, m, frame, keys);
        }

        // ------------------------------------------------------------------ the rising panel's place

        /// <summary>The rising panel's size by what it holds.</summary>
        public static void PanelSize(UiDesignerModel m, UiFrame frame, out float w, out float h)
        {
            switch (m.Panel)
            {
                case UiDesignerPanelKind.Furniture:
                    w = Math.Min(CatalogueWidth, UiDraw.SnapDown(frame.Width - Margin * 2f));
                    h = GridPanelHeight(m.Catalogue.CardCount, w, CatalogueDetailNeed(m.Catalogue), RowsFor(frame.Height));
                    break;
                case UiDesignerPanelKind.Materials:
                    w = Math.Min(CatalogueWidth, UiDraw.SnapDown(frame.Width - Margin * 2f));
                    h = GridPanelHeight(m.Materials.CardCount, w, MaterialDetailNeed(m.Materials), RowsFor(frame.Height));
                    break;
                case UiDesignerPanelKind.Items:
                    w = ItemsWidth;
                    h = ItemsHeight(m.Items);
                    break;
                case UiDesignerPanelKind.Time:
                    w = TimeWidth;
                    h = TimeHeight(m.Time);
                    break;
                case UiDesignerPanelKind.Blueprint:
                    w = BlueprintWidth;
                    h = BlueprintHeight(m.Blueprint);
                    break;
                case UiDesignerPanelKind.BusinessType:
                    w = TypeWidth;
                    h = TypeHeight(m.BusinessType);
                    break;
                case UiDesignerPanelKind.Note:
                    w = NoteWidth;
                    h = NoteHeight(m.Note);
                    break;
                default:
                    w = h = 0f;
                    break;
            }
        }

        /// <summary>The rising panel's box: centred on the screen over the bar, a step above it; 0 wide while none rises.</summary>
        public static void PanelBox(UiDesignerModel m, UiFrame frame, out float x, out float y, out float w, out float h)
        {
            PanelSize(m, frame, out w, out h);
            x = UiDraw.SnapDown((frame.Width - w) / 2f);
            y = frame.BandY - PanelGap - h;
            if (w <= 0f)
            {
                x = 0f;
                y = frame.BandY;
            }
        }

        // ------------------------------------------------------------------ the key chips

        // The game's control hints as chips at the top right, over the room's corner, never on the bar.
        private static void Chips(UiScene s, UiDesignerModel m, UiFrame frame, IUiKeys keys)
        {
            if (m.PromptCount == 0 || keys == null || frame.Pointer)
            {
                return;   // while the mouse leads the bar names no key; the face lets the keys lead while the game has hints to show
            }
            UiDraw.KeyChips(s, frame.Width - Margin, Margin, keys, UiDraw.KeyChipsRoom(frame));
        }

        // ------------------------------------------------------------------ the bar's tools

        private struct BarPlan
        {
            public float ToolsX, Gap, StepsX, StepSize;
            public float MoneyX, MoneyW, CloseX, CloseW, ApplyX, ApplyW;
            public int ToolsShown, StepsShown;
            public bool Under;   // the money pill keeps its second line
        }

        private static float CloseSize => UiTokens.Control + UiTokens.S2;

        // Where the bar's pieces stand at this width: the right end first (Apply, Close, the money), then the tools and their steps in what is left
        // between the time pill and the money, the tiles' gap a step tighter when they would not fit.
        private static BarPlan Plan(UiDesignerModel m, UiFrame f, IUiKeys keys)
        {
            var p = new BarPlan { Under = !string.IsNullOrEmpty(m.OnApply) };
            for (int i = 0; i < m.ToolOff.Length; i++)
            {
                p.ToolsShown += m.ToolOff[i] ? 0 : 1;
            }
            for (int i = 0; i < (int)UiDesignerStep.Count; i++)
            {
                p.StepsShown += m.StepShown[i] ? 1 : 0;
            }
            float edge = Margin;
            float apply = UiDraw.ButtonWidth(UiButtonKind.Primary, m.ApplyLabel ?? string.Empty, UiIcon.None, UiAction.Count, keys);
            p.ApplyW = Math.Max(UiDraw.Snap(apply), UiTokens.S16 + UiTokens.S6);
            p.CloseW = CloseSize;
            p.ApplyX = f.Width - edge - p.ApplyW;
            p.CloseX = p.ApplyX - UiTokens.S2 - p.CloseW;
            p.MoneyW = MoneyWidth(m, p.Under);
            p.MoneyX = p.CloseX - UiTokens.S2 - p.MoneyW;
            UiPillPlan pills = UiDraw.PlanPills(m.Bar, f.Width);
            float zoneLeft = edge + pills.Time + UiTokens.S6, zoneRight = p.MoneyX - UiTokens.S6;
            float steps = StepsWidth(p.StepsShown, StepSize, UiTokens.S2);
            p.Gap = UiTokens.S2;
            p.StepSize = StepSize;
            float tools = ToolsWidth(p.ToolsShown, p.Gap);
            if (tools + steps + UiTokens.S4 > zoneRight - zoneLeft)
            {
                p.Gap = UiTokens.S1;
                steps = StepsWidth(p.StepsShown, StepSize, UiTokens.S1);
                tools = ToolsWidth(p.ToolsShown, p.Gap);
            }
            if (tools + steps + UiTokens.S4 > zoneRight - zoneLeft && p.Under)
            {
                p.Under = false;   // the money keeps its figure alone; its words under it stay in the info panel's total
                p.MoneyW = MoneyWidth(m, false);
                p.MoneyX = p.CloseX - UiTokens.S2 - p.MoneyW;
                zoneRight = p.MoneyX - UiTokens.S6;
            }
            float group = tools + (p.StepsShown > 0 ? UiTokens.S4 + steps : 0f);
            p.ToolsX = UiDraw.SnapDown(Math.Max(zoneLeft, Math.Min(f.Width / 2f - group / 2f, zoneRight - group)));
            p.StepsX = p.ToolsX + tools + UiTokens.S4;
            return p;
        }

        private static float ToolsWidth(int shown, float gap) => shown == 0 ? 0f : shown * BarTilePx + (shown - 1) * gap;

        private static float StepsWidth(int shown, float size, float gap) => shown == 0 ? 0f : UiTokens.S4 + shown * size + (shown - 1) * gap;

        private static float MoneyWidth(UiDesignerModel m, bool under)
        {
            if (string.IsNullOrEmpty(m.Balance))
            {
                return 0f;
            }
            float words = UiTextRules.Width(m.Balance, UiStyle.Number) + UiTokens.S1;
            if (under && !string.IsNullOrEmpty(m.OnApply))
            {
                words = Math.Max(words, (m.OnApplyTrend != 0 ? UiTokens.IconSmall + UiTokens.S1 : 0f) + UiTextRules.Width(m.OnApply, UiStyle.Small) + UiTokens.S1);
            }
            return UiDraw.Snap(UiTokens.S3 + PillGlyph + UiTokens.S3 + words + UiTokens.S3);
        }

        /// <summary>The width the bar's middle needs for the tool row and its steps, for the status bar to keep its pills clear of.</summary>
        public static float RowWidth(UiDesignerModel m)
        {
            int tools = 0, steps = 0;
            for (int i = 0; i < m.ToolOff.Length; i++)
            {
                tools += m.ToolOff[i] ? 0 : 1;
            }
            for (int i = 0; i < (int)UiDesignerStep.Count; i++)
            {
                steps += m.StepShown[i] ? 1 : 0;
            }
            return ToolsWidth(tools, UiTokens.S1) + (steps > 0 ? StepsWidth(steps, StepSize, UiTokens.S1) : 0f);
        }

        // The status bar with this model's time on it, then what stands on the bar after it: the tools, their steps and the right end.
        private static void Bar(UiScene s, UiDesignerModel m, UiFrame frame, IUiKeys keys)
        {
            BarPlan p = Plan(m, frame, keys);
            m.Bar.Middle = RowWidth(m);
            Func<UiStatusModel> before = UiChrome.Bar;
            UiChrome.Bar = () => m.Bar;
            int band = s.ItemCount;   // the status bar's own band is the first box it draws
            try
            {
                UiDraw.StatusBar(s, frame, keys, false);
            }
            finally
            {
                UiChrome.Bar = before;
            }
            UiAt onBand = UiAt.Screen.In(band);
            Tools(s, m, frame, p, onBand);
            Steps(s, m, frame, p, onBand);
            End(s, m, frame, p, keys, onBand);
        }

        // The tool tiles on the bar's centre line, as the app row's: the one in hand on the cyan, each its own glyph.
        private static void Tools(UiScene s, UiDesignerModel m, UiFrame frame, BarPlan p, UiAt band)
        {
            UiAt row = band.InRegion(BarRegion);
            float ty = UiDraw.BarMiddle(frame) - BarTilePx / 2f, tx = p.ToolsX;
            int first = s.NodeCount;
            for (int i = 0; i < m.ToolOff.Length; i++)
            {
                if (m.ToolOff[i])
                {
                    continue;
                }
                bool chosen = i == m.Tools.Tool;
                int face = UiDraw.Tile(s, row, tx, ty, BarTilePx, UiRole.None, chosen);
                s.Items[face].Flags |= UiFlags.Chrome | UiFlags.Middle;
                float g = UiDraw.BarGlyph;
                UiDraw.Icon(s, row.In(face), tx + (BarTilePx - g) / 2f, ty + (BarTilePx - g) / 2f, g, m.Tools.Tools[i].Icon, chosen ? UiRole.OnAccent : UiRole.None,
                    UiFlags.Middle | UiFlags.Chrome, true);
                int node = UiDraw.Node(s, row, tx, ty, BarTilePx, BarTilePx, ToolCommand, i, face);
                if (chosen || (m.Tools.Tool < 0 && s.NodeCount == first + 1))
                {
                    s.FocusFirst = node;
                }
                tx += BarTilePx + p.Gap;
            }
            Chain(s, first);
        }

        // The steps after the tools behind a rule: undo, redo, a floor down, a floor up and the walls' look.
        private static void Steps(UiScene s, UiDesignerModel m, UiFrame frame, BarPlan p, UiAt band)
        {
            if (p.StepsShown == 0)
            {
                return;
            }
            UiAt row = band.InRegion(BarRegion);
            float mid = UiDraw.BarMiddle(frame), size = p.StepSize;
            float gap = p.Gap == UiTokens.S1 ? UiTokens.S1 : UiTokens.S2;
            UiDraw.Box(s, row, p.StepsX, mid - UiTokens.S5, UiTokens.Hairline, UiTokens.S5 * 2f, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor | UiFlags.Chrome | UiFlags.Middle);
            float sx = p.StepsX + UiTokens.S4;
            int first = s.NodeCount;
            for (int i = 0; i < (int)UiDesignerStep.Count; i++)
            {
                if (!m.StepShown[i])
                {
                    continue;
                }
                int drawn = s.ItemCount;
                int node = UiDraw.IconButton(s, row, sx, mid - size / 2f, size, m.StepIcons[i], UiRole.None, StepCommand, i, false, UiAction.Count, false, true);
                for (int k = drawn; k < s.ItemCount; k++)
                {
                    s.Items[k].Flags |= UiFlags.Chrome | UiFlags.Middle;
                }
                if (m.StepOff[i])
                {
                    UiDraw.Dim(s, drawn, node);
                }
                sx += size + gap;
            }
            Chain(s, first);
        }

        // One row for the d-pad: Left and Right walk the tiles and the steps.
        private static void Chain(UiScene s, int first)
        {
            for (int n = first; n < s.NodeCount; n++)
            {
                s.Nodes[n].Left = n > first ? n - 1 : -1;
                s.Nodes[n].Right = n < s.NodeCount - 1 ? n + 1 : -1;
            }
        }

        // The bar's right end: the money after this edit with what it adds up to under it, Close (discards, asking), Apply (asks the game's question).
        private static void End(UiScene s, UiDesignerModel m, UiFrame frame, BarPlan p, IUiKeys keys, UiAt band)
        {
            UiAt at = band.InRegion(EndRegion);
            float mid = UiDraw.BarMiddle(frame), py = mid - UiDraw.BarPill / 2f;
            if (p.MoneyW > 0f)
            {
                int face = UiDraw.Box(s, at, p.MoneyX, py, p.MoneyW, UiDraw.BarPill, UiRole.Pill, UiRole.None, UiTokens.RadiusLarge, UiFlags.Chrome | UiFlags.Middle);
                UiAt inside = at.In(face);
                UiDraw.Icon(s, inside, p.MoneyX + UiTokens.S3, py + (UiDraw.BarPill - PillGlyph) / 2f, PillGlyph, UiIcon.Wallet, UiRole.None, UiFlags.Middle | UiFlags.Chrome);
                float cx = p.MoneyX + UiTokens.S3 + PillGlyph + UiTokens.S3, cw = p.MoneyX + p.MoneyW - UiTokens.S3 - cx;
                bool under = p.Under && !string.IsNullOrEmpty(m.OnApply);
                float th = UiTextRules.LineHeight(UiStyle.Number), uh = under ? UiTextRules.LineHeight(UiStyle.Small) : 0f;
                float ty = py + (UiDraw.BarPill - th - uh) / 2f;
                UiDraw.Text(s, inside, cx, ty, cw, UiStyle.Number, UiRole.Text, m.Balance, UiFlags.Middle | UiFlags.Truncate | UiFlags.Chrome);
                if (under)
                {
                    float ux = cx;
                    if (m.OnApplyTrend != 0)
                    {
                        UiDraw.Icon(s, inside, ux, ty + th + (uh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.OnApplyTrend > 0 ? UiIcon.TrendUp : UiIcon.TrendDown,
                            m.OnApplyTrend > 0 ? UiRole.Ok : UiRole.Danger, UiFlags.Middle | UiFlags.Chrome);
                        ux += UiTokens.IconSmall + UiTokens.S1;
                    }
                    UiDraw.Text(s, inside, ux, ty + th, cx + cw - ux, UiStyle.Small, m.OnApplyTrend > 0 ? UiRole.OkText : m.OnApplyTrend < 0 ? UiRole.DangerText : UiRole.Muted,
                        m.OnApply, UiFlags.Middle | UiFlags.Truncate | UiFlags.Chrome);
                }
            }
            float by = mid - UiTokens.Control / 2f;
            int close = UiDraw.Box(s, at, p.CloseX, by - UiTokens.S1, p.CloseW, UiTokens.Control + UiTokens.S2, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, UiFlags.Chrome | UiFlags.Middle);
            UiDraw.Icon(s, at.In(close), p.CloseX + (p.CloseW - UiTokens.Icon) / 2f, by - UiTokens.S1 + (UiTokens.Control + UiTokens.S2 - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Close,
                UiRole.Danger, UiFlags.Middle | UiFlags.Chrome | UiFlags.Centre);
            int closeNode = UiDraw.Node(s, at, p.CloseX, by - UiTokens.S1, p.CloseW, UiTokens.Control + UiTokens.S2, CloseCommand, 0, close);
            int drawn2 = s.ItemCount;
            int apply = UiDraw.Button(s, at, p.ApplyX, by - UiTokens.S1, p.ApplyW, UiTokens.Control + UiTokens.S2, UiButtonKind.Primary, m.ApplyLabel ?? string.Empty, UiIcon.None,
                UiAction.Count, keys,
                ApplyCommand, 0, m.ApplyOff);
            for (int k = drawn2; k < s.ItemCount; k++)
            {
                s.Items[k].Flags |= UiFlags.Chrome;
            }
            s.Nodes[apply].Left = closeNode;
            s.Nodes[closeNode].Right = apply;
        }

        // ------------------------------------------------------------------ the info panel

        // A section's band: a darker strip across the panel, its label in capitals with a figure at its right.
        private static float SectionHeight(int rows) => UiTokens.S2 * 2f + UiTokens.PairRow * (1 + rows);

        /// <summary>The info panel's height as it stands in full (folded: its head and three readings).</summary>
        public static float InfoHeight(UiDesignerInfo i, bool folded)
        {
            float h = UiFrame.Strip;
            if (folded)
            {
                int extra = FoldedRows(i) - 1;
                return UiDraw.Snap(h + FoldedHead + (extra > 0 ? UiTokens.S2 * 2f + UiTokens.PairRow * extra : 0f) + UiTokens.S2);
            }
            h += HeadHeight(i);
            h += SectionHeight(i.CostCount) + UiTokens.S1;
            if (i.CapacityShown || i.ScoreShown)
            {
                h += ReadingsHeight(i) + UiTokens.S1;
            }
            if (i.RequireShown)
            {
                h += SectionHeight(i.RequireCount) + UiTokens.S1;
            }
            return UiDraw.Snap(h + UiTokens.S2);
        }

        private const float FoldedHead = 56f;
        private static float HeadHeight(UiDesignerInfo i) => 72f;

        private static int FoldedRows(UiDesignerInfo i) => 1 + (i.ScoreShown ? 1 : 0) + (i.RequireShown ? 1 : 0);

        private static float ReadingsHeight(UiDesignerInfo i)
        {
            int blocks = (i.CapacityShown ? 1 : 0) + (i.ScoreShown ? 1 : 0);
            return UiTokens.S2 * 2f + blocks * UiTokens.PairRow * 2f + (blocks > 1 ? UiTokens.S3 : 0f);
        }

        // The info panel at the top left. It stands folded when the player folded it, or when its full height would run into the panel rising over
        // the bar (their boxes meet where the catalogue is wider than the room to the right of it).
        private static void Info(UiScene s, UiDesignerModel m, UiFrame frame, float panelTop)
        {
            UiDesignerInfo i = m.Info;
            bool folded = Folds(m, frame, panelTop);
            float x = Margin, y = Margin, h = InfoHeight(i, folded);
            UiAt column = UiAt.Screen.InRegion(InfoRegion);
            int panel = UiDraw.Box(s, column, x, y, InfoWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = column.In(panel);
            UiDraw.TitleStrip(s, on, x, y, InfoWidth, UiIcon.Sofa, i.Title, false);
            float right = x + InfoWidth - UiTokens.S3;
            if (i.CanFold)
            {
                float b = UiTokens.MinTarget;
                UiDraw.IconButton(s, on, right - b, y + (UiFrame.Strip - b) / 2f, b, folded ? UiIcon.Down : UiIcon.Up, UiRole.None, FoldCommand, 0, false, UiAction.Count, false, true);
                right -= b + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(i.FloorWords))
            {
                float fw = Math.Min(UiDraw.Snap(UiTextRules.Width(i.FloorWords, UiStyle.Small) + UiTokens.S1), InfoWidth * 0.25f);
                UiDraw.TextMiddle(s, on, right - fw, y, UiFrame.Strip, fw, UiStyle.Small, UiRole.Text2, i.FloorWords, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
            float cy = y + UiFrame.Strip;
            if (folded)
            {
                InfoFolded(s, on, i, x, cy);
                return;
            }
            cy = Head(s, on, i, x, cy);
            cy = Cost(s, on, i, x, cy);
            cy = Readings(s, on, i, x, cy);
            Require(s, on, i, x, cy);
        }

        // Folded when the player folded it, or when its full height would run into the rising panel.
        private static bool Folds(UiDesignerModel m, UiFrame frame, float panelTop) =>
            m.Info.Folded || (m.Panel != UiDesignerPanelKind.None && Margin + InfoHeight(m.Info, false) + UiTokens.S3 > panelTop && PanelMeets(m, frame));

        /// <summary>
        /// The boxes of the layout that hold the mouse off the room under them, for the face to put a clear shield under: the info panel, the rising panel when one stands,
        /// and the bar across the foot. Four numbers each (x, y, width, height in px); how many boxes.
        /// </summary>
        public static int Blocks(UiDesignerModel m, UiFrame frame, float[] into)
        {
            PanelBox(m, frame, out float px, out float py, out float pw, out float ph);
            into[0] = Margin;
            into[1] = Margin;
            into[2] = InfoWidth;
            into[3] = InfoHeight(m.Info, Folds(m, frame, py));
            int n = 1;
            if (pw > 0f)
            {
                into[n * 4] = px;
                into[n * 4 + 1] = py;
                into[n * 4 + 2] = pw;
                into[n * 4 + 3] = ph;
                n++;
            }
            into[n * 4] = 0f;
            into[n * 4 + 1] = frame.BandY;
            into[n * 4 + 2] = frame.Width;
            into[n * 4 + 3] = Math.Max(0f, frame.Height - frame.BandY);
            return n + 1;
        }

        private static bool PanelMeets(UiDesignerModel m, UiFrame frame)
        {
            PanelBox(m, frame, out float px, out _, out float pw, out _);
            return pw > 0f && px < Margin + InfoWidth;
        }

        // The business: its initials on a tile, its name and what it is and where.
        private static float Head(UiScene s, UiAt on, UiDesignerInfo i, float x, float y)
        {
            float h = HeadHeight(i), tile = 48f, tx = x + UiTokens.S4, ty = y + (h - tile) / 2f;
            int mark = UiDraw.Box(s, on, tx, ty, tile, tile, i.MarkTone == UiRole.None ? UiRole.Accent : i.MarkTone, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, on.In(mark), tx, ty, tile, tile, UiStyle.Hero, UiRole.OnAccent, i.Mark ?? string.Empty, UiFlags.Centre | UiFlags.Truncate);
            float nx = tx + tile + UiTokens.S3, nw = x + InfoWidth - UiTokens.S4 - nx;
            float nh = Lh(UiStyle.Hero), fh = Lh(UiStyle.Body);
            bool facts = !string.IsNullOrEmpty(i.Facts);
            float block = nh + (facts ? UiTokens.S1 + fh : 0f), by = y + (h - block) / 2f;
            UiDraw.Text(s, on, nx, by, nw, UiStyle.Hero, UiRole.Text, i.Name, UiFlags.Truncate | UiFlags.Middle);
            if (facts)
            {
                UiDraw.Text(s, on, nx, by + nh + UiTokens.S1, nw, UiStyle.Body, UiRole.Text2, i.Facts, UiFlags.Truncate | UiFlags.Middle);
            }
            return y + h;
        }

        // What this edit costs: a line a cause and the total on apply under a rule, in a band of its own.
        private static float Cost(UiScene s, UiAt on, UiDesignerInfo i, float x, float y)
        {
            float h = SectionHeight(i.CostCount);
            UiDraw.Band(s, on, x, y, InfoWidth, h);
            float ix = x + UiTokens.S4, iw = InfoWidth - UiTokens.S4 * 2f, ry = y + UiTokens.S2;
            ry += UiDraw.SectionRow(s, on, ix, ry, iw, i.EditTitle, null, false, -1, 0);
            for (int k = 0; k < i.CostCount; k++)
            {
                ref UiDesignerCostLine line = ref i.Cost[k];
                if (line.Total)
                {
                    UiDraw.Box(s, on, ix, ry, iw, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                    float vw = Math.Min(UiDraw.Snap(UiTextRules.Width(line.Value, UiStyle.BodyStrong) + UiTokens.S1), iw * 0.5f);
                    UiDraw.TextMiddle(s, on, ix, ry, UiTokens.PairRow, iw - vw - UiTokens.S3, UiStyle.BodyStrong, UiRole.Text, line.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.TextMiddle(s, on, ix + iw - vw, ry, UiTokens.PairRow, vw, UiStyle.BodyStrong, line.Ink == UiRole.None ? UiRole.Text : line.Ink, line.Value,
                        UiFlags.Right | UiFlags.Truncate);
                    ry += UiTokens.PairRow;
                    continue;
                }
                ry += UiDraw.InfoRow(s, on, ix, ry, iw, line.Label, line.Value, false, -1, 0, line.Ink == UiRole.None ? UiRole.Text : line.Ink);
            }
            return y + h + UiTokens.S1;
        }

        // The customers an hour and the interior score, each a meter with its figure and what it changed since the designer opened.
        private static float Readings(UiScene s, UiAt on, UiDesignerInfo i, float x, float y)
        {
            if (!i.CapacityShown && !i.ScoreShown)
            {
                return y;
            }
            float h = ReadingsHeight(i);
            UiDraw.Band(s, on, x, y, InfoWidth, h);
            float ix = x + UiTokens.S4, iw = InfoWidth - UiTokens.S4 * 2f, ry = y + UiTokens.S2;
            if (i.CapacityShown)
            {
                ry += Reading(s, on, ix, ry, iw, i.CapacityTitle, i.CapacityChange, i.CapacityChangeInk, i.CapacityFill, i.CapacityTone, i.CapacityFigure, i.CapacityOf);
                ry += i.ScoreShown ? UiTokens.S3 : 0f;
            }
            if (i.ScoreShown)
            {
                ry += Reading(s, on, ix, ry, iw, i.ScoreTitle, i.ScoreChange, i.ScoreChangeInk, i.ScoreFill, i.ScoreTone, i.ScoreFigure, i.ScoreWants);
            }
            return y + h + UiTokens.S1;
        }

        // A reading: its label in capitals with its change at the right, then its meter, its figure and the words after it.
        private static float Reading(UiScene s, UiAt on, float x, float y, float w, string title, string change, UiRole changeInk, int fill, UiRole tone, string figure, string words)
        {
            float h = UiTokens.PairRow;
            float cw = string.IsNullOrEmpty(change) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(change, UiStyle.BodyStrong) + UiTokens.S1), w * 0.3f);
            UiDraw.TextMiddle(s, on, x, y, h, w - cw - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            if (cw > 0f)
            {
                UiDraw.TextMiddle(s, on, x + w - cw, y, h, cw, UiStyle.BodyStrong, changeInk, change, UiFlags.Right | UiFlags.Truncate);
            }
            float ry = y + h;
            float fw = Math.Min(UiDraw.Snap(UiTextRules.Width(figure, UiStyle.BodyStrong) + UiTokens.S1), w * 0.25f);
            float ow = string.IsNullOrEmpty(words) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(words, UiStyle.Body) + UiTokens.S1), w * 0.5f);
            float tail = fw + (ow > 0f ? UiTokens.S2 + ow : 0f);
            float mw = w - tail - UiTokens.S3;
            UiDraw.Meter(s, on, x, ry + (h - UiTokens.Segment) / 2f, mw, UiTokens.Segment, fill, tone);
            float tx = x + mw + UiTokens.S3;
            UiDraw.TextMiddle(s, on, tx, ry, h, fw, UiStyle.BodyStrong, UiRole.Text, figure, UiFlags.Truncate);
            if (ow > 0f)
            {
                UiDraw.TextMiddle(s, on, tx + fw + UiTokens.S2, ry, h, ow, UiStyle.Body, UiRole.Text2, words, UiFlags.Truncate);
            }
            return h * 2f;
        }

        // What the business needs to open: a check line each, the count after its words, "Show" and "How" at the right of one still to meet.
        private static void Require(UiScene s, UiAt on, UiDesignerInfo i, float x, float y)
        {
            if (!i.RequireShown)
            {
                return;
            }
            float h = SectionHeight(i.RequireCount);
            UiDraw.Band(s, on, x, y, InfoWidth, h);
            float ix = x + UiTokens.S4, iw = InfoWidth - UiTokens.S4 * 2f, ry = y + UiTokens.S2;
            float fw = Math.Min(UiDraw.Snap(UiTextRules.Width(i.RequireFigure, UiStyle.BodyStrong) + UiTokens.S1), iw * 0.3f);
            UiDraw.TextMiddle(s, on, ix, ry, UiTokens.PairRow, iw - fw - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text, i.RequireTitle, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, on, ix + iw - fw, ry, UiTokens.PairRow, fw, UiStyle.BodyStrong, i.RequireInk, i.RequireFigure, UiFlags.Right | UiFlags.Truncate);
            ry += UiTokens.PairRow;
            int shown = 0;
            for (int k = 0; k < i.RequireCount; k++)
            {
                RequireLine(s, on, i, ref i.Require[k], k, ix, ry, iw, ref shown);
                ry += UiTokens.PairRow;
            }
        }

        // MostLinks lines at most carry links, the first still to meet: a long list never gives the panel more targets than it can hold.
        private const int MostLinks = 3;

        private static void RequireLine(UiScene s, UiAt on, UiDesignerInfo info, ref UiDesignerRequirement r, int index, float x, float y, float w, ref int linked)
        {
            float h = UiTokens.PairRow;
            UiDraw.Icon(s, on, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, r.Met ? UiIcon.Check : UiIcon.Ring, r.Met ? UiRole.None : UiRole.Muted, UiFlags.Middle, r.Met);
            float tx = x + UiTokens.Icon + UiTokens.S2, right = x + w;
            bool links = !r.Met && linked < MostLinks && (r.CanShow || r.CanHelp);
            if (links)
            {
                linked++;
                if (r.CanHelp)
                {
                    float lw = LinkWidth(info.HowLabel);
                    UiDraw.LinkValue(s, on, right - lw, y, h, lw, info.HowLabel, UiStyle.Body, HowCommand, index);
                    right -= lw + UiTokens.S3;
                }
                if (r.CanShow)
                {
                    float lw = LinkWidth(info.ShowLabel);
                    UiDraw.LinkValue(s, on, right - lw, y, h, lw, info.ShowLabel, UiStyle.Body, ShowCommand, index);
                    right -= lw + UiTokens.S3;
                }
            }
            float count = string.IsNullOrEmpty(r.Count) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Count, UiStyle.Body) + UiTokens.S1);
            float room = right - tx - (count > 0f ? UiTokens.S2 + count : 0f);
            float nw = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Title, UiStyle.Body) + UiTokens.S1), Math.Max(0f, room));
            UiDraw.TextMiddle(s, on, tx, y, h, nw, UiStyle.Body, r.Met ? UiRole.Text : UiRole.Text, r.Title, UiFlags.Truncate);
            if (count > 0f)
            {
                UiDraw.TextMiddle(s, on, tx + nw + UiTokens.S2, y, h, count, UiStyle.Body, UiRole.Muted, r.Count, UiFlags.Truncate);
            }
        }

        // A link's width for its words: the magnifier, the words, and a hair either side.
        private static float LinkWidth(string words) => UiDraw.Snap(UiTextRules.Width(words ?? string.Empty, UiStyle.Body) + UiDraw.LinkGap + UiTokens.IconSmall + UiTokens.S1 * 2f);

        // The folded panel: the business on one line with what this edit comes to, then the score and what is still needed, a line each.
        private static void InfoFolded(UiScene s, UiAt on, UiDesignerInfo i, float x, float y)
        {
            float tile = 32f, tx = x + UiTokens.S4, ty = y + (FoldedHead - tile) / 2f;
            int mark = UiDraw.Box(s, on, tx, ty, tile, tile, i.MarkTone == UiRole.None ? UiRole.Accent : i.MarkTone, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, on.In(mark), tx, ty, tile, tile, UiStyle.BodyStrong, UiRole.OnAccent, i.Mark ?? string.Empty, UiFlags.Centre | UiFlags.Truncate);
            float nx = tx + tile + UiTokens.S3, total = 0f;
            string figure = null;
            for (int k = 0; k < i.CostCount; k++)
            {
                if (i.Cost[k].Total)
                {
                    figure = i.Cost[k].Value;
                    total = UiDraw.Snap(UiTextRules.Width(figure, UiStyle.BodyStrong) + UiTokens.S1);
                }
            }
            float right = x + InfoWidth - UiTokens.S4, nw = right - nx - (total > 0f ? total + UiTokens.S3 : 0f);
            UiDraw.TextMiddle(s, on, nx, y, FoldedHead, nw, UiStyle.BodyStrong, UiRole.Text, i.Name, UiFlags.Truncate);
            if (total > 0f)
            {
                UiDraw.TextMiddle(s, on, right - total, y, FoldedHead, total, UiStyle.BodyStrong, UiRole.Text, figure, UiFlags.Right | UiFlags.Truncate);
            }
            float ry = y + FoldedHead + UiTokens.S2, iw = InfoWidth - UiTokens.S4 * 2f, ix = x + UiTokens.S4;
            if (FoldedRows(i) > 1)
            {
                UiDraw.Band(s, on, x, ry - UiTokens.S2, InfoWidth, UiTokens.S2 + UiTokens.PairRow * (FoldedRows(i) - 1) + UiTokens.S2);
            }
            if (i.ScoreShown)
            {
                string line = i.ScoreFigure + (string.IsNullOrEmpty(i.ScoreChange) ? string.Empty : "  " + i.ScoreChange);
                ry += UiDraw.InfoRow(s, on, ix, ry, iw, i.ScoreTitle, line, false, -1, 0, string.IsNullOrEmpty(i.ScoreChange) ? UiRole.Text : i.ScoreChangeInk);
            }
            if (i.RequireShown)
            {
                UiDraw.InfoRow(s, on, ix, ry, iw, i.RequireTitle, i.RequireFigure, false, -1, 0, i.RequireInk);
            }
        }
    }
}
