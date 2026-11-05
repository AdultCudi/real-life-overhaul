using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Services app on the kit: an app window with its four tabs across, and a page of the model's shape: a tab's rows with the card of the one
    /// picked beside them, or the sale of a business (its worth, its offers, what accepting one leaves and the price to list it at). Game-free; the
    /// model says what, this says where. Cost: a layout when the model changes.
    /// </summary>
    public static partial class ServicesFace
    {
        // The block 6700 to 6719 is this screen's: the kit's own commands repeat across parts (a kit nav is 1, a decision's choice is 1).
        public const int ActionCommand = 6700, StepLess = 6701, StepMore = 6702, BackCommand = 6703, ChipCommand = 6704, PickerCommand = 6705, OptionCommand = 6706, ViewCommand = 6707;
        public const int TableBase = UiDataTable.First;
        private const float SideWidth = 360f;
        private const byte RegionHead = 1, RegionTable = 2, RegionSide = 3, RegionLower = 4, RegionLeft = 5;

        /// <summary>Every command the screen answers, for the checks.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int>
            {
                UiAppWindow.NavCommand, ActionCommand, StepLess, StepMore, BackCommand, ChipCommand, PickerCommand, OptionCommand, ViewCommand, UiChart.First + UiChart.Point
            };
            for (int i = 0; i <= UiDataTable.MassAnswer; i++)
            {
                list.Add(TableBase + i);
            }
            list.Add(TableBase + UiDataTable.Act);
            return list.ToArray();
        }

        public static void Lay(UiScene s, ServicesModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("services", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            switch (m.Shape)
            {
                case ServicesShape.Table:
                    TablePage(s, at, m, x, y, w, bottom, keys);
                    break;
                case ServicesShape.Sell:
                    SellPage(s, at, m, x, y, w, bottom, keys);
                    break;
                default:
                    int leave = UiDraw.EmptyState(s, at, x, y, w, bottom - y, m.OffIcon, m.OffTitle, m.OffBody, m.OffAction, UiDraw.CloseCommand, keys);
                    if (leave >= 0)
                    {
                        s.FocusFirst = leave;   // a part that is off has one thing to do, and focus starts on it rather than on the window's close
                    }
                    break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        private static short ClipOf(UiScene s, short list) => list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;

        // The height a table takes with every row showing.
        private static float Natural(UiDataTableModel t) => t == null ? 0f : UiDraw.Snap(UiDataTable.Chrome(t) + t.RowCount * UiDataTable.RowHeight(t));

        private static float ChipWide(string label, IUiKeys keys) => UiDraw.ButtonWidth(UiButtonKind.Secondary, label, UiIcon.None, UiAction.Count, keys);

        // Choices over a table, in one row, or wrapping when a long translation does not fit: the chosen one filled cyan, as every filter of the kit.
        private static float ChipsRow(UiScene s, UiAt at, UiActionModel[] chips, int chosen, float x, float y, float w, float rightRoom, IUiKeys keys)
        {
            float cx = x, cy = y;
            for (int i = 0; i < chips.Length; i++)
            {
                float bw = Math.Min(w, ChipWide(chips[i].Label, keys));
                if (cx > x && cx + bw > x + w - rightRoom)
                {
                    cx = x;
                    cy += UiTokens.Control + UiTokens.S2;
                }
                UiDraw.Button(s, at, cx, cy, bw, UiTokens.Control, i == chosen ? UiButtonKind.Chosen : UiButtonKind.Secondary, chips[i].Label, UiIcon.None, UiAction.Count, keys,
                    chips[i].Command, chips[i].Arg, chips[i].Disabled);
                cx += bw + UiTokens.S2;
            }
            return cy + UiTokens.Control - y;
        }

        // ------------------------------------------------------------------------------------------------- a tab's rows

        private static void TablePage(UiScene s, UiAt at, ServicesModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Strip.Length > 0)
            {
                y += UiAppWindow.Figures(s, at.InRegion(RegionHead), m.Strip, x, y, w) + UiTokens.S3;
            }
            if (m.Chips.Length > 0)
            {
                y += ChipsRow(s, at.InRegion(RegionHead), m.Chips, m.ChipAt, x, y, w, 0f, keys) + UiTokens.S3;
            }
            bool side = m.Side != null;
            float sw = side ? Math.Min(SideWidth, UiDraw.SnapDown(w * 0.34f)) : 0f;
            float tw = UiDraw.SnapDown(w - (sw > 0f ? sw + UiTokens.S4 : 0f));
            float chartH = m.Chart != null ? Math.Min(UiTokens.S16 * 8f, UiDraw.SnapDown((bottom - y) * 0.36f)) : 0f;
            float th = Math.Max(UiTokens.S16 * 3f, bottom - y - (chartH > 0f ? chartH + UiTokens.S3 : 0f));
            if (m.Chart != null)
            {
                th = Math.Min(th, Math.Max(UiTokens.S16 * 3f, Natural(m.Table)));
                chartH = Math.Max(0f, bottom - y - th - UiTokens.S3);
                if (chartH < UiTokens.S16 * 5f)
                {
                    chartH = 0f;
                    th = bottom - y;
                }
            }
            m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, y, tw, th, keys, TableBase);
            m.TableClip = ClipOf(s, m.TableList);
            if (side)
            {
                UiFactCard.Lay(s, at.InRegion(RegionSide), m.Side, x + tw + UiTokens.S4, y, sw, bottom - y, keys, true, true);
            }
            if (chartH > 0f)
            {
                UiChart.Lay(s, at.InRegion(RegionLower), m.Chart, x, UiDraw.Snap(y + th + UiTokens.S3), tw, UiDraw.SnapDown(chartH), UiChart.First);
            }
        }

        // ------------------------------------------------------------------------------------------------- the sale of a business

        private static void SellPage(UiScene s, UiAt at, ServicesModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            ServicesSell p = m.Sell;
            float leftW = UiDraw.SnapDown((w - UiTokens.S4) * 0.34f), rx = x + leftW + UiTokens.S4, rw = w - leftW - UiTokens.S4;
            UiAt left = at.InRegion(RegionLeft);
            float ly = y;
            UiDraw.TextMiddle(s, left, x, ly, UiTokens.PairRow, leftW, UiStyle.SectionHeader, UiRole.Text2, p.PickerTitle, UiFlags.Label | UiFlags.Truncate);
            ly += UiTokens.PairRow + UiTokens.S1;
            string chosen = p.Chosen >= 0 && p.Chosen < p.Businesses.Length ? p.Businesses[p.Chosen] : string.Empty;
            int field = UiDraw.Choice(s, left, x, ly, leftW, chosen, PickerCommand, 0, p.Businesses.Length < 2);
            s.FocusFirst = field;
            float pickerY = ly + UiTokens.Control + UiTokens.S1;
            ly += UiTokens.Control + UiTokens.S3;

            float valuedH = UiFactCard.Height(p.Valued, leftW, keys, true);
            float raiseH = p.Raise != null ? UiFactCard.Height(p.Raise, leftW, keys, true) : 0f;
            float afterH = p.After != null ? UiFactCard.Height(p.After, leftW, keys, true) : 0f;
            float worthBase = UiTokens.S4 + UiTokens.PairRow + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S1 + UiTokens.PairRow + UiTokens.S3;
            float below = valuedH + UiTokens.S3 + (raiseH > 0f ? raiseH + UiTokens.S3 : 0f) + (afterH > 0f ? afterH + UiTokens.S3 : 0f);
            float spark = p.Spark != null ? Math.Min(UiTokens.S16 * 6f, UiDraw.SnapDown(bottom - ly - worthBase - below)) : 0f;
            spark = spark >= UiTokens.S16 * 3f ? spark : 0f;
            float worthH = UiDraw.Snap(worthBase + spark);
            int band = UiDraw.Box(s, left, x, ly, leftW, worthH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = left.In(band);
            float ix = x + UiTokens.S4, iw = leftW - UiTokens.S4 * 2f, iy = ly + UiTokens.S4;
            UiDraw.TextMiddle(s, on, ix, iy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text2, p.WorthLabel, UiFlags.Label | UiFlags.Truncate);
            iy += UiTokens.PairRow;
            float dh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.TextMiddle(s, on, ix, iy, dh, iw, UiStyle.Display, UiRole.Text, p.Worth, UiFlags.Truncate);
            iy += dh + UiTokens.S1;
            UiDraw.TextMiddle(s, on, ix, iy, UiTokens.PairRow, iw, UiStyle.Body, p.WorthSubInk == UiRole.None ? UiRole.Text2 : p.WorthSubInk, p.WorthSub, UiFlags.Truncate);
            iy += UiTokens.PairRow + UiTokens.S3;
            if (spark > 0f)
            {
                UiChart.Lay(s, on, p.Spark, ix - UiTokens.S4, iy - UiTokens.S3, leftW, spark, UiChart.First);
            }
            ly += worthH + UiTokens.S3;
            ly += UiFactCard.Lay(s, left, p.Valued, x, ly, leftW, bottom - ly, keys, true) + UiTokens.S3;
            if (p.Raise != null && bottom - ly >= UiTokens.S16 * 3f)
            {
                ly += UiFactCard.Lay(s, left, p.Raise, x, ly, leftW, bottom - ly, keys, true) + UiTokens.S3;
            }
            if (p.After != null && bottom - ly >= UiTokens.S16 * 3f)
            {
                UiFactCard.Lay(s, left, p.After, x, ly, leftW, bottom - ly, keys, true);
            }

            // The right: what the buyers offered as rows against the worth, then what accepting one leaves beside the price to list it at.
            UiAt head = at.InRegion(RegionHead);
            float ry = y;
            float note = string.IsNullOrEmpty(p.OffersNote) ? 0f : Math.Min(UiDraw.SnapDown(rw * 0.5f), UiDraw.Snap(UiTextRules.Width(p.OffersNote, UiStyle.Small) + UiTokens.S1));
            ry += ChipsRow(s, head, p.Views, p.View, rx, ry, rw, note + UiTokens.S3, keys) + UiTokens.S3;
            if (note > 0f)
            {
                UiDraw.TextMiddle(s, head, rx + rw - note, y, UiTokens.Control, note, UiStyle.Small, UiRole.Muted, p.OffersNote, UiFlags.Right | UiFlags.Truncate);
            }
            float cardsTop = ry;
            float lowerNeed = UiTokens.S16 * 7f;
            float th = Math.Max(UiTokens.S16 * 3f, Math.Min(Natural(p.Offers), bottom - ry - lowerNeed));
            m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), p.Offers, rx, ry, rw, th, keys, TableBase);
            m.TableClip = ClipOf(s, m.TableList);
            cardsTop = UiDraw.Snap(ry + th + UiTokens.S3);
            float cardsH = bottom - cardsTop, cw = UiDraw.SnapDown((rw - UiTokens.S4) / 2f), px = rx + cw + UiTokens.S4, pw = rw - cw - UiTokens.S4;
            UiFactCard.Lay(s, at.InRegion(RegionSide), p.Accept, rx, cardsTop, cw, cardsH, keys, true, true);
            UiAt lower = at.InRegion(RegionLower);
            float priceH = UiFactCard.Height(p.Price, pw, keys, true), footH = UiFactCard.Height(p.PriceFoot, pw, keys, true);
            float barsH = cardsH - priceH - footH - UiTokens.S3 * 2f;
            float py = cardsTop;
            UiFactCard.Lay(s, lower, p.Price, px, py, pw, priceH, keys, true);
            py += priceH + UiTokens.S3;
            if (p.PriceBars != null && barsH >= UiTokens.S16 * 3f)
            {
                UiChart.Lay(s, lower, p.PriceBars, px, py, pw, UiDraw.SnapDown(barsH), UiChart.First);
                py += UiDraw.SnapDown(barsH) + UiTokens.S3;
            }
            else
            {
                footH += Math.Max(0f, barsH);
            }
            UiFactCard.Lay(s, lower, p.PriceFoot, px, py, pw, bottom - py, keys, true, true);
            if (p.PickerOpen && p.Businesses.Length > 1)
            {
                UiDraw.ChoiceList(s, left, x, pickerY, leftW, p.Businesses, p.Businesses.Length, p.Chosen, 0f, OptionCommand);
            }
        }
    }
}
