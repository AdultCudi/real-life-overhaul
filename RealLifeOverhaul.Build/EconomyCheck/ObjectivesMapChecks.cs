using System;
using RealLifeOverhaul.Ui;

// The HUD with the city map's dock up (Ui/UiHud, UiMapFilters.DockBox): the left column with the objectives, folded and open, the info
// panel, the notice cards, the world prompt, the activity's strip and the key chips all stand clear of the dock's panel and its rail,
// at 1080, 720 and 4K and at the game's zoom, in three languages. The owner's report (5 Nov): the objectives covered the dock's title,
// its rail and its filters.
internal static partial class Program
{
    private static void ObjectivesMapChecks()
    {
        int cases = 0, meets = 0, columns = 0, beside = 0;
        string fault = null;
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            UiWords t = UiWords(lang);
            foreach (int[] screen in MinimapScreens)
            {
                foreach (float zoom in MinimapZooms)
                {
                    float canvasW = 3840f / zoom, canvasH = 2160f / zoom;
                    float scale = UiHudRules.Scale(canvasW);
                    var frame = new UiFrame { Width = canvasW / scale, Height = canvasH / scale, Band = UiFrame.BandHeight, Pointer = false };
                    UiMapFilters.DockBox(frame.Height, out float dx, out float dy, out float dw, out float dh);
                    for (int variant = 0; variant < 3; variant++)
                    {
                        foreach (bool folded in new[] { true, false })
                        {
                            UiHudModel m = MinimapHud(t, variant, zoom);
                            m.Objectives.Collapsed = folded;
                            m.AvoidX = dx;
                            m.AvoidY = dy;
                            m.AvoidW = dw;
                            m.AvoidH = dh;
                            m.MapDock = true;
                            if (frame.Width < UiMapFilters.DockRight + UiHudRules.Margin * 2f + UiActivity.StripWidth)
                            {
                                m.Activity = null;   // the strip is 1020 px at the least: where it cannot stand beside the dock it stands where it was (the minimap's own rule), a bottom centre piece and not the left column's
                            }
                            if (frame.Width < UiMapFilters.DockRight + UiTokens.S2 * 2f + Math.Max(UiInfoPanel.Width, UiObjectives.Width) + UiHudRules.Margin * 2f + UiHudNotice.Width)
                            {
                                m.NoticeCount = 0;   // dock, column and a 560 px card side by side do not fit: the card stands over the column there, for its few seconds
                            }
                            cases++;
                            string at = lang + " " + screen[0] + "x" + screen[1] + " zoom " + zoom + " variant " + variant + (folded ? " folded" : " open");
                            UiHud.LeftColumn(m, out float cx, out float cy, out float cw, out float ch);
                            columns++;
                            if (UiHudRules.Overlaps(cx, cy, cw, ch, dx, dy, dw, dh))
                            {
                                meets++;
                                fault ??= "the left column at " + cx + "," + cy + " " + cw + " x " + ch + " meets the dock at " + dx + "," + dy + " " + dw + " x " + dh + " (" + at + ")";
                            }
                            if (cx + cw > frame.Width)
                            {
                                meets++;
                                fault ??= "the left column runs off the screen's right edge (" + at + ")";
                            }
                            beside += cx > UiHud.Left ? 1 : 0;
                            var s = new UiScene();
                            UiSamples.Hud(s, frame, true, MinimapKeys(), t, m, MinimapBar(t));
                            string meet = MinimapMeets(s, dx, dy, dw, dh);
                            if (meet != null)
                            {
                                meets++;
                                fault ??= meet + " (" + at + ")";
                            }
                            string cards = MinimapCards(t, frame, dx, dy, dw, dh);
                            if (cards != null)
                            {
                                meets++;
                                fault ??= cards + " (" + at + ")";
                            }
                        }
                    }
                }
            }
        }
        Check(meets == 0, "objectives and map: with the city map's dock up, the left column (objectives folded and open, with an info panel), the notice cards, the key chips, a world prompt, the activity's strip and the toasts as cards never meet the dock's panel or its rail, at 1080, 720 and 4K, at the game's zoom, in English, Portuguese and Chinese ("
                          + cases + " layouts)" + (fault == null ? string.Empty : ": " + fault));
        Check(columns > 0 && beside == columns, "objectives and map: every layout put the left column beside the dock (" + beside + " of " + columns + ")");

        var strip = new UiHudModel { Objectives = UiSamples.PiecesGoals(UiWords("en"), false), MapDock = true };
        UiMapFilters.DockBox(1080f, out strip.AvoidX, out strip.AvoidY, out strip.AvoidW, out strip.AvoidH);
        UiHud.LeftColumn(strip, out _, out float top, out _, out _);
        Check(top >= UiMapChrome.RouteTop, "objectives and map: the column stands under the route strip's row (" + top + " of " + UiMapChrome.RouteTop + "), which shows only while Directions is chosen");
        ObjectivesMapDragChecks();
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string box = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Host", "UiMinimapBox.cs"));
        string live = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "HudScreens", "HudLive.cs"));
        Check(box.Contains("UiMapFilters.DockBox(") && box.Contains("MapKits.DockShowing") && live.Contains("UiMinimapBox.Read(") && live.Contains("m.MapDock"),
            "objectives and map: the HUD reads the dock through the one box every piece keeps clear of (UiMinimapBox), on the slow tick, and says so to the layout");
        var none = new UiHudModel { Objectives = UiSamples.PiecesGoals(UiWords("en"), false) };
        UiHud.LeftColumn(none, out float nx, out float ny, out _, out _);
        Check(nx == UiHud.Left && ny == UiTokens.S4, "objectives and map: with the map shut the column is where it always was");
    }

    // The dock dragged (UiWindowDrag): its avoid box moves with it, and the objectives panel beside it follows, under the route strip's row of wherever the dock's top is, and the
    // route tag over the map keeps off the dock and the objectives both. The strip stands at the top beside the dock's own slot, so a column that is never above RouteTop never covers it.
    private static void ObjectivesMapDragChecks()
    {
        UiWords t = UiWords("en");
        float height = 1080f, width = 1920f;
        UiMapFilters.DockBox(height, out float bx, out float by, out float bw, out float bh);
        int cases = 0, covered = 0, offscreen = 0, followed = 0, stripCovered = 0, tagCovered = 0;
        string fault = null;
        foreach (float[] move in new[] { new[] { 0f, 0f }, new[] { 40f, 30f }, new[] { 120f, 200f }, new[] { 300f, 0f }, new[] { 600f, 120f }, new[] { 900f, 60f }, new[] { width - bx - bw - 8f, 40f } })
        {
            var m = new UiHudModel { Objectives = UiSamples.PiecesGoals(t, false), MapDock = true };
            m.AvoidX = bx + move[0];
            m.AvoidY = by + move[1];
            m.AvoidW = bw;
            m.AvoidH = Math.Min(bh, height - UiFrame.BandHeight - m.AvoidY - UiTokens.S4);
            UiHud.LeftColumn(m, out float cx, out float cy, out float cw, out float ch);
            cases++;
            string at = "dock at " + m.AvoidX + "," + m.AvoidY;
            if (UiHudRules.Overlaps(cx, cy, cw, ch, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH))
            {
                covered++;
                fault ??= "the column meets the dragged dock (" + at + ")";
            }
            if (cx + cw > width)
            {
                offscreen++;
                fault ??= "the column runs off the screen (" + at + ")";
            }
            bool pushed = cx > UiHud.Left;
            followed += !pushed || (cx >= m.AvoidX + m.AvoidW && cy >= m.AvoidY + UiMapChrome.RouteTop - 0.01f) ? 0 : 1;
            // the strip over the map: its row at the very top, beside the dock's own slot
            float stripX = UiMapFilters.DockRight + UiTokens.S6;
            if (UiHudRules.Overlaps(cx, cy, cw, ch, stripX, UiTokens.S4, Math.Max(1f, width - stripX - UiTokens.S4), UiMapChrome.RouteTop - UiTokens.S4 - UiTokens.S2))
            {
                stripCovered++;
                fault ??= "the column covers the route strip's row (" + at + ")";
            }
            // the route tag: kept beside its point off the dock and the column together, as HudHost does it
            float ax = m.AvoidX, ay = m.AvoidY, aw = m.AvoidW, ah = m.AvoidH;
            if (pushed)
            {
                aw = Math.Max(aw, cx + cw + UiTokens.S2 - ax);
                ah = Math.Max(ah, cy + ch + UiTokens.S2 - ay);
            }
            UiMapChrome.RouteTagSize("2 h 5 min", "450 m", out float tw, out float th);
            for (float px = 60f; px < width; px += 220f)
            {
                for (float py = 80f; py < height - UiFrame.BandHeight; py += 140f)
                {
                    UiHudRules.Beside(px, py, true, tw, th, width, height - UiFrame.BandHeight, ax, ay, aw, ah, out float tx, out float ty);
                    UiHudRules.Clear(ref tx, ref ty, tw, th, width, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH);
                    bool meetsDock = UiHudRules.Overlaps(tx, ty, tw, th, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH);
                    bool meetsColumn = pushed && UiHudRules.Overlaps(tx, ty, tw, th, cx, cy, cw, ch);
                    if ((meetsDock || meetsColumn) && tx >= UiHudRules.Margin - 0.01f && tx + tw <= width - UiHudRules.Margin + 0.01f)
                    {
                        tagCovered++;
                        fault ??= "the route tag at " + tx + "," + ty + " meets the " + (meetsDock ? "dock" : "column") + " (" + at + ", point " + px + "," + py + ")";
                    }
                }
            }
        }
        string box = ModSource(@"Ui\Host\UiMinimapBox.cs"), kits = ModSource(@"Ui\MapDock\MapKits.cs");
        Check(box.Contains("MapKits.DockMove(out float dx, out float dy);") && box.IndexOf("x += dx;", StringComparison.Ordinal) > box.IndexOf("DockBox(", StringComparison.Ordinal)
              && kits.Contains("if (Filters.Showing)") && kits.Contains("UiWindowPlaces.Moved(MapFiltersGame.FiltersWindow"),
            "window drag: the HUD's avoid box (UiMinimapBox, which the objectives panel and the route tag keep clear of) is the dock's box moved by where the dock was dragged to, while the filters' dock is up");
        string host = ModSource(@"Ui\Hud\HudHost.cs"), live = ModSource(@"Ui\HudScreens\HudLive.cs"), hud = ModSource(@"Ui\UiHud.cs");
        Check(covered == 0 && offscreen == 0 && followed == 0 && stripCovered == 0 && tagCovered == 0,
            "objectives and map: with the dock dragged to " + cases + " places, the objectives panel beside it follows (right of the dock, under the strip's row from the dock's top), never meets the dock, leaves the screen, covers the route strip's row, or leaves the route tag room beside it"
            + (fault == null ? string.Empty : ": " + fault));
        Check(host.Contains("if (own && hud.MapDock)") && host.Contains("ax = hud.AvoidX;") && host.Contains("_leftX + _leftW + UiTokens.S2 - ax") && hud.Contains("y = m.AvoidY + UiMapChrome.RouteTop;") && live.Contains("m.AvoidY + UiMapChrome.RouteTop"),
            "objectives and map: the HUD's followers (the route tag, the district tags) keep off the dock where it stands now and the objectives beside it, and the column's height is fitted from where it starts");
    }
}
