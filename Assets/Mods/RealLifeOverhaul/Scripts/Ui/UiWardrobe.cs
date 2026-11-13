using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A slider of the side panel's: its words, the figure it holds and how far along it stands.</summary>
    public struct UiSliderRow
    {
        public string Label;
        public string Figure;
        public float Share;
        public bool Disabled;
    }

    /// <summary>What the clothes shop, the hairdresser and the surgeon show beside the game's 3D preview.</summary>
    public sealed class UiWardrobeModel
    {
        public string Title;                                   // the category shown, or the screen's own title
        public UiIcon Icon = UiIcon.Person;
        public string Shop;                                    // the shop's name and street under the strip, or null
        public UiEdgeTab[] Tabs = new UiEdgeTab[0];
        public int TabCount, Tab = -1;
        public string[] Subs = new string[0];                  // the category's own kinds (T-shirts, jackets), or none
        public int SubCount, Sub = -1;
        public string[] Filters = new string[0];               // "All 48", "Owned 6": the grid's filters, or none
        public int FilterCount, Filter = -1;
        public bool Searching;                                 // a search field over the grid
        public string Search, SearchHint;
        public UiCatalogueModel Grid = new UiCatalogueModel(); // the pieces: a tile each, the chosen one's foot in the cyan
        public UiSliderRow[] Sliders = new UiSliderRow[0];     // the surgeon's and the body's sliders, or none
        public int SliderCount;
        public int Held = -1;                                  // the slider the keys hold, or -1
        public string ChosenName, ChosenDetail;                // the piece chosen, and what it is with its price
        public string ColourLabel;
        public string[] Swatches = new string[0];
        public int SwatchCount, Swatch = -1;
        public UiActionModel[] Actions = new UiActionModel[0]; // the screen's own buttons, the main one last
        public string Leave;                                   // the way out: the game asks first when something is not kept
        public string BasketLabel, BasketTotalLabel, BasketTotal;
        public UiInfoRow[] Basket = new UiInfoRow[0];          // the pieces tried on that cost money, or none
        public string EmptyTitle, EmptyBody;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The clothes shop, the hairdresser and the plastic surgeon (the game's character customisers): a catalogue panel
    /// docked at the left beside the game's 3D preview, its categories as round tabs down its edge with the game's own
    /// pictures, the category's kinds as chips, the pieces as a grid of tiles (the game's picture, the mod's name, price
    /// and Owned or RLO), the chosen piece with its colours as a swatch row, the surgeon's sliders, and the screen's
    /// buttons with Save as the main one. What the basket holds stands at the right. Commands from
    /// <see cref="TabCommand"/>. Game-free. Cost: a layout a change, the grid's rows in view only.
    /// </summary>
    public static class UiWardrobe
    {
        public const int TabCommand = 640, SubCommand = 641, GridCommand = 642, SwatchCommand = 644, SliderCommand = 645, ActionCommand = 646,
            FilterCommand = 648, SearchCommand = 649;                                  // GridCommand + UiCatalogue.Card is a tile
        public static readonly int[] Commands = { TabCommand, SubCommand, GridCommand + UiCatalogue.Card, SwatchCommand, SliderCommand, ActionCommand, FilterCommand, SearchCommand };
        public const float Width = 520f;                       // px: the surgeon's sliders, the preview keeps the rest
        public const float GridWidth = 760f;                   // px: four tiles across with their names and prices whole
        public const float LeastTile = 160f;                   // px: a tile's narrowest, a garment's name and its price on it

        /// <summary>The panel's width: wide enough for four named tiles where there is a grid, the sliders' own where not.</summary>
        public static float WidthOf(UiWardrobeModel m) => m.Grid.CardCount > 0 || m.SliderCount == 0 ? GridWidth : Width;
        private const float BasketW = 320f;

        public static void Lay(UiScene s, UiWardrobeModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("wardrobe", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float x = UiTokens.S4, y = UiTokens.S4;
            if (m.TabCount > 0)
            {
                UiCataloguePanel.Tabs(s, x, y, m.Tabs, m.TabCount, m.Tab, TabCommand);
                x += UiCataloguePanel.Beside(m.TabCount);
            }
            float h = UiDraw.SnapDown(frame.BandY - UiTokens.S4 * 2f);
            Draw(s, m, x, y, WidthOf(m), h, keys);
            if (m.Basket.Length > 0)
            {
                Basket(s, m, frame.Width - UiTokens.S4 - BasketW, y);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        public static void Draw(UiScene s, UiWardrobeModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, m.Icon, m.Title, !string.IsNullOrEmpty(m.Leave));
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            if (!string.IsNullOrEmpty(m.Shop))
            {
                UiDraw.TextMiddle(s, on, ix, cy, UiTokens.PairRow + UiTokens.S2, iw, UiStyle.Small, UiRole.Text2, m.Shop, UiFlags.Truncate);
                cy += UiTokens.PairRow + UiTokens.S2;
            }
            cy += UiTokens.S3;
            if (m.FilterCount > 0 || m.Searching)
            {
                cy = Tools(s, on, m, ix, cy, iw) + UiTokens.S3;
            }
            if (m.SubCount > 0)
            {
                cy = Pills(s, on, m.Subs, m.SubCount, m.Sub, SubCommand, ix, cy, iw) + UiTokens.S3;
            }
            // From the foot up: the buttons, the chosen piece's band, the sliders; the grid takes what is left.
            float buttons = Buttons(null, on, m, ix, 0f, iw, keys);
            float band = ChosenHeight(m, iw);
            float sliders = SlidersHeight(m);
            bool grid = m.Grid.CardCount > 0 || m.SliderCount == 0;
            // The panel ends under its buttons, its grid as tall as its rows, rather than standing the screen's height; a grid of
            // more rows than the screen holds scrolls in what is left.
            float gridNeed = grid ? UiCatalogue.GridHeight(m.Grid, iw - UiTokens.S3, LeastTile) + UiTokens.S3 : 0f;
            float under = cy + gridNeed + sliders - (sliders > 0f ? UiTokens.S2 : 0f) + (band > 0f ? UiTokens.S3 + band : 0f) + UiTokens.S3 + buttons + UiTokens.S4;
            h = Math.Min(h, UiDraw.Snap(under - y));
            s.Items[panel].H = h;
            float foot = y + h - UiTokens.S4 - buttons;
            float gridBottom = foot - UiTokens.S3 - (band > 0f ? band + UiTokens.S3 : 0f) - (sliders > 0f ? sliders + UiTokens.S3 : 0f);
            if (grid)
            {
                m.Grid.EmptyTitle = m.EmptyTitle;
                m.Grid.EmptyBody = m.EmptyBody;
                m.Grid.EmptyIcon = m.Icon;
                int clips = s.ClipCount;
                UiCatalogue.LayGrid(s, on, m.Grid, ix, cy, iw - UiTokens.S3, Math.Max(UiTokens.S16, gridBottom - cy), LeastTile, keys, out _, GridCommand);
                if (s.ClipCount > clips)
                {
                    s.Clips[clips].Fills = true;   // a short kind's few pieces stand at the grid's top; the room under them is the grid's own
                }
                cy = gridBottom + UiTokens.S3;
            }
            if (sliders > 0f)
            {
                for (int i = 0; i < m.SliderCount; i++)
                {
                    ref UiSliderRow r = ref m.Sliders[i];
                    UiDraw.Text(s, on, ix, cy, iw, UiStyle.Small, UiRole.Text2, r.Label, UiFlags.Truncate | UiFlags.Label);
                    UiDraw.Slider(s, on, ix, cy + SliderLabel, iw, r.Figure, r.Share, SliderCommand, i, r.Disabled, i == m.Held);
                    cy += SliderRowH;
                }
                cy += UiTokens.S3 - UiTokens.S2;
            }
            if (band > 0f)
            {
                Chosen(s, on, m, x, cy, w);
            }
            Buttons(s, on, m, ix, foot, iw, keys);
        }

        // A slider's words over it, rounded up to the grid so the slider stands on it.
        private static float SliderLabel => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S1);

        private static float SliderRowH => SliderLabel + UiTokens.Control + UiTokens.S2;

        private static float SlidersHeight(UiWardrobeModel m) => m.SliderCount > 0 ? m.SliderCount * SliderRowH : 0f;

        private static float ChosenHeight(UiWardrobeModel m, float iw)
        {
            bool named = !string.IsNullOrEmpty(m.ChosenName);
            if (!named && m.SwatchCount == 0)
            {
                return 0f;
            }
            float h = UiTokens.S3 * 2f + NameBlock(m);
            if (m.SwatchCount > 0)
            {
                h += (named ? UiTokens.S3 : 0f) + UiTokens.PairRow + UiTokens.S1 + UiDraw.SwatchesHeight(iw, m.SwatchCount);
            }
            return UiDraw.Snap(h);
        }

        // The chosen piece's name over what it is and costs, rounded up to the grid so its colours stand on it.
        private static float NameBlock(UiWardrobeModel m) => string.IsNullOrEmpty(m.ChosenName) ? 0f
            : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title) + (string.IsNullOrEmpty(m.ChosenDetail) ? 0f : UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)));

        // The chosen piece on a band: its name over what it is and costs, then its colours with the colour's name.
        private static void Chosen(UiScene s, UiAt at, UiWardrobeModel m, float x, float y, float w)
        {
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            float h = ChosenHeight(m, iw);
            UiDraw.Band(s, at, x, y, w, h);
            float cy = y + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.ChosenName))
            {
                UiDraw.Text(s, at, ix, cy, iw, UiStyle.Title, UiRole.Text, m.ChosenName, UiFlags.Truncate);
                if (!string.IsNullOrEmpty(m.ChosenDetail))
                {
                    UiDraw.Text(s, at, ix, cy + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, iw, UiStyle.Small, UiRole.Text2, m.ChosenDetail, UiFlags.Truncate | UiFlags.Middle);
                }
                cy += NameBlock(m) + (m.SwatchCount > 0 ? UiTokens.S3 : 0f);
            }
            if (m.SwatchCount > 0)
            {
                cy += UiDraw.SectionRow(s, at, ix, cy, iw, m.ColourLabel, null, false, -1, 0) + UiTokens.S1;
                UiDraw.Swatches(s, at, ix, cy, iw, m.Swatches, m.SwatchCount, m.Swatch, SwatchCommand);
            }
        }

        // The filters as pills with their counts and the search field beside them.
        private static float Tools(UiScene s, UiAt at, UiWardrobeModel m, float x, float y, float w)
        {
            float fieldW = m.Searching ? UiDraw.Snap(w * 0.4f) : 0f;
            float pillsW = w - (fieldW > 0f ? fieldW + UiTokens.S3 : 0f);
            float ph = m.FilterCount > 0 ? Pills(s, at, m.Filters, m.FilterCount, m.Filter, FilterCommand, x, y, pillsW) - y : 0f;
            if (m.Searching)
            {
                UiDraw.TextField(s, at, x + w - fieldW, y, fieldW, m.Search, m.SearchHint, SearchCommand, 0, false);
            }
            return y + Math.Max(ph, m.Searching ? UiTokens.Control : 0f);
        }

        // Pills that wrap, the chosen one filled cyan with dark words, as the game's chosen tab; each a target with its index.
        private static float Pills(UiScene s, UiAt at, string[] labels, int count, int chosen, int command, float x, float y, float w)
        {
            float cx = x, cy = y, h = UiTokens.Chip;
            for (int i = 0; i < count && i < labels.Length; i++)
            {
                float pw = Math.Min(w, UiDraw.Snap(UiTextRules.Width(labels[i], UiStyle.Nav) + UiTokens.S4 * 2f));
                if (cx > x && cx + pw > x + w)
                {
                    cx = x;
                    cy += h + UiTokens.S2;
                }
                bool on = i == chosen;
                int face = UiDraw.Box(s, at, cx, cy, pw, h, on ? UiRole.Accent : UiRole.Field, UiRole.None, h / 2f, on ? UiFlags.Selected : UiFlags.None);
                UiDraw.TextMiddle(s, at.In(face), cx + UiTokens.S4, cy, h, pw - UiTokens.S4 * 2f + UiTokens.S1, UiStyle.Nav, on ? UiRole.OnAccent : UiRole.Text2, labels[i],
                    UiFlags.Label | UiFlags.Truncate);
                UiDraw.Node(s, at, cx, cy, pw, h, command, i, face);
                cx += pw + UiTokens.S2;
            }
            return count > 0 ? cy + h : y;
        }

        // The buttons, laid out (or measured, with no scene) from a top left; returns their height. All on one row when they fit
        // beside the way out; else the main one alone across the foot and the rest on the row above it, or two a row (one a
        // row where a pair's words do not fit half the width) when they do not fit one.
        private static float Buttons(UiScene s, UiAt at, UiWardrobeModel m, float x, float y, float w, IUiKeys keys)
        {
            int count = m.Actions.Length;
            bool leave = !string.IsNullOrEmpty(m.Leave);
            if (count == 0 || Across(m, keys, count, leave) <= w)
            {
                if (s != null)
                {
                    Row(s, at, m, x, y, w, keys, count, leave);
                }
                return UiTokens.Control;
            }
            int rows = 0, others = (leave ? 1 : 0) + count - 1;
            if (Across(m, keys, count - 1, leave) <= w)
            {
                if (s != null)
                {
                    Row(s, at, m, x, y, w, keys, count - 1, leave);
                }
                rows = 1;
            }
            else
            {
                float half = UiDraw.SnapDown((w - UiTokens.S3) / 2f);
                for (int j = 0; j < others; rows++)
                {
                    bool pair = j + 1 < others && Natural(m, keys, j, leave) <= half && Natural(m, keys, j + 1, leave) <= half;
                    float ry = y + rows * (UiTokens.Control + UiTokens.S2);
                    if (s != null)
                    {
                        Other(s, at, m, keys, j, leave, x, ry, pair ? half : w);
                        if (pair)
                        {
                            Other(s, at, m, keys, j + 1, leave, x + half + UiTokens.S3, ry, half);
                        }
                    }
                    j += pair ? 2 : 1;
                }
            }
            if (s != null)
            {
                ref UiActionModel main = ref m.Actions[count - 1];
                UiDraw.Button(s, at, x, y + rows * (UiTokens.Control + UiTokens.S2), w, UiTokens.Control, main.Kind, main.Label, main.Icon, main.Key, keys, ActionCommand, count - 1,
                    main.Disabled);
            }
            rows++;
            return rows * UiTokens.Control + (rows - 1) * UiTokens.S2;
        }

        // The width the way out and the first actions take on one row, with their gaps.
        private static float Across(UiWardrobeModel m, IUiKeys keys, int count, bool leave)
        {
            float need = leave ? Natural(m, keys, 0, true) + UiTokens.S3 : 0f;
            for (int i = 0; i < count; i++)
            {
                need += UiDraw.ButtonWidth(m.Actions[i].Kind, m.Actions[i].Label, m.Actions[i].Icon, m.Actions[i].Key, keys) + UiTokens.S3;
            }
            return need - UiTokens.S3;
        }

        // A button's own width among the rest above the main one: the way out first, then the actions in their order.
        private static float Natural(UiWardrobeModel m, IUiKeys keys, int j, bool leave)
        {
            if (leave && j == 0)
            {
                return UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Leave, UiIcon.None, UiAction.Back, keys);
            }
            ref UiActionModel a = ref m.Actions[leave ? j - 1 : j];
            return UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
        }

        private static void Other(UiScene s, UiAt at, UiWardrobeModel m, IUiKeys keys, int j, bool leave, float x, float y, float w)
        {
            if (leave && j == 0)
            {
                UiDraw.Button(s, at, x, y, w, UiTokens.Control, UiButtonKind.Secondary, m.Leave, UiIcon.None, UiAction.Back, keys, UiDraw.CloseCommand, 0);
                return;
            }
            int i = leave ? j - 1 : j;
            ref UiActionModel a = ref m.Actions[i];
            UiDraw.Button(s, at, x, y, w, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
        }

        // One row: the way out at the left, the first actions at the right in their order, the main one last.
        private static void Row(UiScene s, UiAt at, UiWardrobeModel m, float x, float y, float w, IUiKeys keys, int count, bool leave)
        {
            float right = x + w;
            for (int i = count - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            if (leave)
            {
                Other(s, at, m, keys, 0, true, x, y, Natural(m, keys, 0, true));
            }
        }

        // What the basket holds: a small panel at the top right, a row a piece and what it all costs.
        private static void Basket(UiScene s, UiWardrobeModel m, float x, float y)
        {
            float iw = BasketW - UiTokens.S4 * 2f;
            float h = UiTokens.S3 * 2f + UiTokens.PairRow * (1 + m.Basket.Length) + UiTokens.S2 + UiTokens.PairRow;
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, BasketW, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = y + UiTokens.S3;
            cy += UiDraw.SectionRow(s, on, x + UiTokens.S4, cy, iw, m.BasketLabel, null, false, -1, 0);
            for (int i = 0; i < m.Basket.Length; i++)
            {
                cy += UiDraw.InfoRow(s, on, x + UiTokens.S4, cy, iw, m.Basket[i].Label, m.Basket[i].Value, false, -1, 0, UiRole.Text);
            }
            UiDraw.Box(s, on, x + UiTokens.S4, cy + UiTokens.S1, iw, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            UiDraw.InfoRow(s, on, x + UiTokens.S4, cy + UiTokens.S2, iw, m.BasketTotalLabel, m.BasketTotal, false, -1, 0, UiRole.Text);
        }
    }
}
