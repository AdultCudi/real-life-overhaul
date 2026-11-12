using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the vehicle purchase shows: the vehicle, its price and specs, its colours, delivery, and the game's buttons.</summary>
    public sealed class UiVehicleBuyModel
    {
        public string Title;                                   // the strip: what is being bought ("Vehicle", "Boat")
        public UiIcon Icon = UiIcon.Car;
        public string Name, Facts, Price;                      // the vehicle's name, a line of what it is, its price as the game writes it
        public string SpecsLabel;
        public UiInfoRow[] Specs = new UiInfoRow[0];
        public string ColourLabel, ColourName;
        public string[] Swatches = new string[0];
        public int Colour = -1;
        public bool Delivery;                                  // the dealer delivers: the switch and the warehouse
        public string DeliveryWords, DeliveryDetail, DeliveryState;
        public bool DeliveryOn;
        public string Warehouse;                               // the warehouse chosen, or the game's words for none
        public bool WarehouseChosen;
        public string Buy, Cancel;
        public bool BuyEnabled, CancelEnabled;
        public string Lot;                                     // "3 of 12 on the lot", or null when the lot is not walked
        public string LotPrevious, LotNext;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The vehicle purchase (the game's PurchaseVehicleUI): an info panel docked at the top left beside the game's showcase
    /// camera, as a game's vehicle panel stands beside the car: its strip, the vehicle's name with what it is and its price
    /// as the large figure, its specs as rows, its colours as a swatch row that recolours the car, delivery as a switch
    /// with the warehouse beside it, and Cancel and Buy with their keys. With the lot walked, the cars on it step at the
    /// panel's foot. Commands from <see cref="SwatchCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiVehicleBuy
    {
        public const int SwatchCommand = 610, DeliveryCommand = 611, WarehouseCommand = 612, BuyCommand = 613, CancelCommand = 614, LotCommand = 615;
        public static readonly int[] Commands = { SwatchCommand, DeliveryCommand, WarehouseCommand, BuyCommand, CancelCommand, LotCommand };
        public const float Width = UiInfoPanel.Width;
        private const float SwitchW = 96f;

        public static float Height(UiVehicleBuyModel m)
        {
            float iw = Width - UiTokens.S4 * 2f;
            float h = UiFrame.Strip + HeadH(m);
            if (m.Specs.Length > 0)
            {
                h += UiTokens.S2 * 2f + UiTokens.PairRow * (1 + m.Specs.Length) + UiTokens.S1;
            }
            if (m.Swatches.Length > 0)
            {
                h += UiTokens.S2 * 2f + UiTokens.PairRow + UiTokens.S2 + UiDraw.SwatchesHeight(iw, m.Swatches.Length) + UiTokens.S1;
            }
            if (m.Delivery)
            {
                h += DeliveryH();
            }
            if (!string.IsNullOrEmpty(m.Lot))
            {
                h += UiTokens.Control + UiTokens.S2 * 2f;
            }
            return UiDraw.Snap(h + UiTokens.Control + UiTokens.S4 * 2f);
        }

        // The name, what it is and the price, rounded up to the grid so the bands under it stand on it.
        private static float HeadH(UiVehicleBuyModel m) => UiDraw.Snap(UiTokens.S4 * 2f + UiTextRules.LineHeight(UiStyle.Hero)
            + (string.IsNullOrEmpty(m.Facts) ? 0f : UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2 + UiTextRules.LineHeight(UiStyle.Display));

        private static float DeliveryH() => UiTokens.S2 * 2f + UiTokens.Control + UiTokens.S2 + UiTokens.Control + UiTokens.S1;

        public static void Lay(UiScene s, UiVehicleBuyModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("vehicle_purchase", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, m, UiTokens.S4, UiTokens.S4, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        public static float Draw(UiScene s, UiVehicleBuyModel m, float x, float y, IUiKeys keys)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, m.Icon, m.Title, m.CancelEnabled);
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            // The vehicle's name, what it is, and its price as the one large figure.
            float ty = cy + UiTokens.S4;
            UiDraw.Text(s, on, ix, ty, iw, UiStyle.Hero, UiRole.Text, m.Name, UiFlags.Truncate);
            ty += UiTextRules.LineHeight(UiStyle.Hero);
            if (!string.IsNullOrEmpty(m.Facts))
            {
                UiDraw.Text(s, on, ix, ty + UiTokens.S1, iw, UiStyle.Small, UiRole.Text2, m.Facts, UiFlags.Truncate | UiFlags.Middle);   // under the name by its line, not a grid step
                ty += UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small);
            }
            UiDraw.Text(s, on, ix, ty + UiTokens.S2, iw, UiStyle.Display, UiRole.Text, m.Price, UiFlags.Truncate | UiFlags.Middle);
            cy += HeadH(m);
            if (m.Specs.Length > 0)
            {
                float sh = UiTokens.S2 * 2f + UiTokens.PairRow * (1 + m.Specs.Length);
                UiDraw.Band(s, on, x, cy, Width, sh);
                float ry = cy + UiTokens.S2;
                ry += UiDraw.SectionRow(s, on, ix, ry, iw, m.SpecsLabel, null, false, -1, 0);
                for (int i = 0; i < m.Specs.Length; i++)
                {
                    ry += UiDraw.InfoRow(s, on, ix, ry, iw, m.Specs[i].Label, m.Specs[i].Value, false, -1, 0, m.Specs[i].Ink == UiRole.None ? UiRole.Text : m.Specs[i].Ink);
                }
                cy += sh + UiTokens.S1;
            }
            if (m.Swatches.Length > 0)
            {
                float swh = UiDraw.SwatchesHeight(iw, m.Swatches.Length);
                float sh = UiTokens.S2 * 2f + UiTokens.PairRow + UiTokens.S2 + swh;
                UiDraw.Band(s, on, x, cy, Width, sh);
                float ry = cy + UiTokens.S2;
                ry += UiDraw.SectionRow(s, on, ix, ry, iw, m.ColourLabel, m.ColourName, false, -1, 0);
                UiDraw.Swatches(s, on, ix, ry + UiTokens.S2, iw, m.Swatches, m.Swatches.Length, m.Colour, SwatchCommand);
                cy += sh + UiTokens.S1;
            }
            if (m.Delivery)
            {
                float dh = DeliveryH() - UiTokens.S1;
                UiDraw.Band(s, on, x, cy, Width, dh);
                float ry = cy + UiTokens.S2;
                bool detail = !string.IsNullOrEmpty(m.DeliveryDetail);
                float block = UiTextRules.LineHeight(UiStyle.Body) + (detail ? UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small) : 0f), by = ry + (UiTokens.Control - block) / 2f;
                float lw = iw - SwitchW - UiTokens.S3;
                UiDraw.Text(s, on, ix, by, lw, UiStyle.Body, UiRole.Text, m.DeliveryWords, UiFlags.Truncate | UiFlags.Label | UiFlags.Middle);   // the pair centred on the switch
                if (detail)
                {
                    UiDraw.Text(s, on, ix, by + UiTextRules.LineHeight(UiStyle.Body) + UiTokens.S1, lw, UiStyle.Small, UiRole.Text2, m.DeliveryDetail, UiFlags.Truncate | UiFlags.Middle);
                }
                UiDraw.Switch(s, on, ix + iw - SwitchW, ry, SwitchW, m.DeliveryOn, m.DeliveryState, DeliveryCommand, 0, false);
                ry += UiTokens.Control + UiTokens.S2;
                UiDraw.Choice(s, on, ix, ry, iw, m.Warehouse, WarehouseCommand, 0, false);
                cy += dh + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.Lot))
            {
                cy = Lot(s, on, m, x, cy);
            }
            // Cancel and Buy with their keys, Buy the one primary and the wider.
            float by2 = cy + UiTokens.S4;
            float cw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Cancel, UiIcon.None, UiAction.Back, keys);
            UiDraw.Button(s, on, ix, by2, cw, UiTokens.Control, UiButtonKind.Secondary, m.Cancel, UiIcon.None, UiAction.Back, keys, CancelCommand, 0, !m.CancelEnabled);
            UiDraw.Button(s, on, ix + cw + UiTokens.S3, by2, iw - cw - UiTokens.S3, UiTokens.Control, UiButtonKind.Primary, m.Buy, UiIcon.None, UiAction.Alt, keys, BuyCommand, 0, !m.BuyEnabled);
            return y + h;
        }

        // The cars on the lot: the one before and the one after either side of where this one stands among them.
        private static float Lot(UiScene s, UiAt at, UiVehicleBuyModel m, float x, float y)
        {
            UiDraw.Box(s, at, x, y, Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float b = UiTokens.Control, ry = y + UiTokens.S2;
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            UiDraw.IconButton(s, at, ix, ry, b, UiIcon.Back, UiRole.Text, LotCommand, -1, false, UiAction.TabPrev);
            UiDraw.IconButton(s, at, ix + iw - b, ry, b, UiIcon.Next, UiRole.Text, LotCommand, 1, false, UiAction.TabNext);
            UiDraw.TextMiddle(s, at, ix + b + UiTokens.S3, ry, b, iw - (b + UiTokens.S3) * 2f, UiStyle.Body, UiRole.Text2, m.Lot, UiFlags.Centre | UiFlags.Truncate);
            return y + b + UiTokens.S2 * 2f;
        }
    }
}
