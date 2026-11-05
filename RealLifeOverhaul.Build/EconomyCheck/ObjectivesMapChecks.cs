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
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string box = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Host", "UiMinimapBox.cs"));
        string live = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "HudScreens", "HudLive.cs"));
        Check(box.Contains("UiMapFilters.DockBox(") && box.Contains("MapKits.DockShowing") && live.Contains("UiMinimapBox.Read(") && live.Contains("m.MapDock"),
            "objectives and map: the HUD reads the dock through the one box every piece keeps clear of (UiMinimapBox), on the slow tick, and says so to the layout");
        var none = new UiHudModel { Objectives = UiSamples.PiecesGoals(UiWords("en"), false) };
        UiHud.LeftColumn(none, out float nx, out float ny, out _, out _);
        Check(nx == UiHud.Left && ny == UiTokens.S4, "objectives and map: with the map shut the column is where it always was");
    }
}
