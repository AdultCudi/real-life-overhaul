using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Nav;
using RealLifeOverhaul.Ui;

// The minimap's card and the kit's HUD (Navigation/NavPlaceRules, Ui/UiHud, Ui/UiNotices, Ui/UiActivity): the card stands above the kit's
// bottom bar in both of its places, clear of the game's panels and the kit's left column; its canvas is drawn over the kit's HUD and under
// the windows that stand with the HUD up; and with a notice, an objective, a prompt, the activity's strip and the cards of the game's
// toasts all up, no piece of the HUD meets the card, at 1080, 720 and 4K, at the game's zoom, in English, Portuguese and Chinese.
internal static partial class Program
{
    private static void MinimapChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        MinimapOrderChecks();
        MinimapRuleChecks();
        MinimapPieceChecks();
        MinimapSourceChecks();
        Console.WriteLine("  Minimap and HUD: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // The game's canvases the kit stands over, from the game's own scene (docs/game-ui.md, the census): the orders are the game's, the
    // kit's are worked out from them as the host works them out (UiHostRules.Above).
    private const int GamePlayerHud = 2, GameSmartphone = 2, GameNotifications = 21, GameConfirm = 21;
    private static readonly int[] GameWindowsWithHud = { 19, 20, 21 };   // items list and tutorial, help and options, the confirm boxes, text input, tooltips

    private static void MinimapOrderChecks()
    {
        int hud = UiHostRules.Above(GamePlayerHud);
        int phone = UiHostRules.Above(GameSmartphone);
        int hudOverPhone = UiHostRules.OverPhone(hud, phone);
        int notices = UiHostRules.Above(GameNotifications);
        int box = UiHostRules.Above(GameConfirm) + 1;
        int[] hudCanvases = { hud, hudOverPhone };   // the bar, the status bar, the objectives, the prompts and the HUD's notices are all on this one canvas
        bool over = true, under = true;
        string fault = null;
        foreach (int h in hudCanvases)
        {
            int minimap = UiHostRules.MinimapOrder(h);
            over &= minimap > h;
            foreach (int w in GameWindowsWithHud)
            {
                under &= minimap < w;
                fault = minimap < w ? fault : "the game's window at order " + w + " is under the card at " + minimap;
            }
            under &= minimap < box;
        }
        Check(over, "minimap: its canvas is drawn over the kit HUD's (the HUD at " + hud + " or " + hudOverPhone + " over the phone, the card at " + UiHostRules.MinimapOrder(hud) + " or " + UiHostRules.MinimapOrder(hudOverPhone) + "), so the bar never covers it");
        Check(under, "minimap: its canvas is under every window that stands with the HUD up: the game's dialogs at 19 to 21 and the kit's question and decision cards at " + box + (fault == null ? string.Empty : " (" + fault + ")"));
        Check(notices > UiHostRules.MinimapOrder(hudOverPhone) && notices < box,
            "minimap: the kit's notice cards (" + notices + ", one over the game's notifications) are the one HUD piece over the card by order, and keep off it by their place (the next checks)");
        bool modal = true;
        foreach (int h in hudCanvases)
        {
            modal &= UiHostRules.ModalOrder(h) > UiHostRules.MinimapOrder(h) && UiHostRules.ModalOrder(h) < GameWindowsWithHud[0];
        }
        Check(modal, "minimap: the HUD's own modal piece (a picker, All apps while walking) is a window: its canvas is over the card's and under the game's dialogs");
        Check(UiHostRules.MinimapOrder(hud) != hud && UiHostRules.MinimapOrder(hudOverPhone) != hudOverPhone,
            "minimap: the card never shares an order with the HUD it is drawn over (two canvases of one order draw in the order they were last switched on)");
    }

    // The card's place in the HUD's units (3840 x 2160 at the game's default zoom): above the bar in the column and beside it, under the
    // game's tasks, off the game's hints, label and item panel, off the kit's left column; and the same answer when nothing changed.
    private static void MinimapRuleChecks()
    {
        const float screen = 2160f, band = 88f * 2f;
        NavSpace free = NavSpace.Free(screen);
        string fault = null;
        int column = 0, beside = 0, hidden = 0;
        foreach (float barTop in new[] { 0f, band, band + 40f })
        {
            foreach (float hints in new[] { -1f, 120f, 300f })
            {
                foreach (float columnBottom in new[] { float.NaN, 1500f, 900f, 400f, 120f, -300f })
                {
                    NavSpace s = free;
                    s.BarTop = barTop;
                    s.HintsTop = hints;
                    s.ColumnRight = 800f;
                    s.HasColumn = !float.IsNaN(columnBottom);
                    s.ColumnBottom = columnBottom;
                    s.LabelsLeft = 1300f;
                    s.LabelsTop = 100f;
                    foreach (bool lines in new[] { false, true })
                    {
                        foreach (int percent in new[] { 50, 100 })
                        {
                            float width = NavPlaceRules.Width(percent);
                            bool fits = NavPlaceRules.Place(s, width, NavPlaceRules.Wanted(width, true), NavPlaceRules.Frame(lines), false, out float x, out float bottom, out float map, out bool inColumn);
                            if (!fits)
                            {
                                hidden++;
                                continue;
                            }
                            column += inColumn ? 1 : 0;
                            beside += inColumn ? 0 : 1;
                            string at = " (bar " + barTop + ", hints " + hints + ", column to " + columnBottom + ", " + percent + "%, lines " + lines + ", " + (inColumn ? "column" : "beside") + ")";
                            if (barTop > 0f && bottom < barTop && fault == null)
                            {
                                fault = "the card's foot " + bottom + " is under the bar's top " + barTop + at;
                            }
                            if (inColumn && hints >= 0f && bottom < hints && fault == null)
                            {
                                fault = "the card's foot " + bottom + " is under the game's hints at " + hints + at;
                            }
                            float top = bottom + NavPlaceRules.Frame(lines) + map;
                            if (inColumn && s.HasColumn && top > columnBottom + 0.01f && fault == null)
                            {
                                fault = "the card's top " + top + " runs up into the kit's column that ends at " + columnBottom + at;
                            }
                            if (!inColumn && s.HasColumn && x < s.ColumnRight + NavPlaceRules.Gap && fault == null)
                            {
                                fault = "the card beside the column stands under the kit's column at x " + x + at;
                            }
                            if (top > screen - NavPlaceRules.TasksTop + 0.01f && fault == null)
                            {
                                fault = "the card's top " + top + " is up in the top bar's room" + at;
                            }
                        }
                    }
                }
            }
        }
        Check(fault == null, "minimap: the card's foot is above the kit bar's top in the column and beside it, over the game's hints, under the kit's left column or beside it, and below the top bar" + (fault == null ? string.Empty : " (" + fault + ")"));
        Check(column > 0 && beside > 0, "minimap: the sweep meets both of the card's places (the column " + column + ", beside it " + beside + ", hidden " + hidden + ")");
        NavSpace tiny = NavSpace.Free(700f);
        tiny.BarTop = 176f;
        float tw = NavPlaceRules.Width(100);
        Check(!NavPlaceRules.Place(tiny, tw, NavPlaceRules.Wanted(tw, true), NavPlaceRules.Frame(true), false, out _, out _, out _, out _), "minimap: a screen with no room for a map above the bar and under the top bar hides the card");

        NavSpace tasks = free;
        tasks.BarTop = band;
        tasks.TasksBottom = 1200f;
        float w = NavPlaceRules.Width(100);
        NavPlaceRules.Place(tasks, w, NavPlaceRules.Wanted(w, true), NavPlaceRules.Frame(false), false, out _, out float b, out float m, out bool col);
        Check(col && b + NavPlaceRules.Frame(false) + m <= 1200f - NavPlaceRules.Gap + 0.01f, "minimap: the game's own task panel is still respected, the card standing under it");

        NavSpace label = free;
        label.BarTop = band;
        label.HasColumn = true;
        label.ColumnBottom = 60f;
        label.ColumnRight = 700f;
        label.LabelsLeft = 900f;
        label.LabelsTop = 300f;
        NavPlaceRules.Place(label, w, NavPlaceRules.Wanted(w, true), NavPlaceRules.Frame(false), false, out float lx, out float lb, out _, out bool lcol);
        Check(!lcol && lx >= 700f + NavPlaceRules.Gap && lb >= 300f + NavPlaceRules.Gap, "minimap: beside the column it stands clear of the kit's column, and over the game's address label where they meet");

        NavSpace taller = free;
        taller.BarTop = band;
        NavPlaceRules.Place(taller, w, NavPlaceRules.Wanted(w, true), NavPlaceRules.Frame(false), false, out _, out float low, out _, out _);
        taller.BarTop = band + 60f;
        NavPlaceRules.Place(taller, w, NavPlaceRules.Wanted(w, true), NavPlaceRules.Frame(false), false, out _, out float high, out _, out _);
        Check(high - low == 60f, "minimap: a taller bar lifts the card by as much (the card follows the bar's laid height, nothing fixed)");
    }

    // The three screens, the game's zoom, three languages: the card as the host reads it (canvas units to the screen's pixels to the HUD's
    // px, floor and ceil as UiMinimapBox reads it), then every piece of the HUD laid out with it up, and none of them meets it.
    private static readonly int[][] MinimapScreens = { new[] { 1920, 1080 }, new[] { 1280, 720 }, new[] { 3840, 2160 } };
    private static readonly float[] MinimapZooms = { 1f, 1.25f, 1.5f, 2f };

    private static void MinimapPieceChecks()
    {
        int cases = 0, meets = 0, beside = 0, inColumn = 0, noRoom = 0, pieces = 0;
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
                    for (int variant = 0; variant < 3; variant++)
                    {
                        UiHudModel m = MinimapHud(t, variant, zoom);
                        UiHud.LeftColumn(m, false, out float cx, out float cy, out float cw, out float ch);
                        NavSpace space = NavSpace.Free(canvasH);
                        space.BarTop = frame.Band * scale;
                        space.HintsTop = variant == 2 ? 150f : -1f;   // the game's hints panel is up in one of the three
                        space.ColumnRight = cw > 0f ? (cx + cw) * scale : 0f;
                        space.HasColumn = cw > 0f;
                        space.ColumnBottom = canvasH - (cy + ch) * scale;
                        space.LabelsLeft = canvasW * 0.4f;
                        space.LabelsTop = 100f;
                        // The key chips stand fixed at the bar's right end: the card is placed over them, as the host reads them once it has laid the HUD out.
                        var bare = new UiScene();
                        UiSamples.Hud(bare, frame, true, MinimapKeys(), t, m, MinimapBar(t));
                        if (UiHud.Chips(bare, m, frame, MinimapKeys(), out float kx, out float ky, out float kw, out float kh))
                        {
                            space.ChipsLeft = kx * scale;
                            space.ChipsTop = canvasH - ky * scale;
                        }
                        foreach (bool lines in new[] { false, true })
                        {
                            float width = NavPlaceRules.Width(100);
                            if (!NavPlaceRules.Place(space, width, NavPlaceRules.Wanted(width, true), NavPlaceRules.Frame(lines), false, out float x, out float bottom, out float map, out bool column))
                            {
                                noRoom++;
                                continue;
                            }
                            cases++;
                            inColumn += column ? 1 : 0;
                            beside += column ? 0 : 1;
                            // The card in canvas units from the screen's foot, to the screen's pixels, to the HUD's px from its top left.
                            float high = bottom + NavPlaceRules.Frame(lines) + map;
                            float pixel = screen[1] / canvasH;
                            ToHudPx(x * pixel, bottom * pixel, (x + width) * pixel, high * pixel, screen[0], screen[1], frame, out float ax, out float ay, out float aw, out float ah);
                            m.AvoidX = ax;
                            m.AvoidY = ay;
                            m.AvoidW = aw;
                            m.AvoidH = ah;
                            string at = lang + " " + screen[0] + "x" + screen[1] + " zoom " + zoom + " " + (column ? "column" : "beside") + " variant " + variant + (lines ? " with lines" : string.Empty);
                            var s = new UiScene();
                            UiSamples.Hud(s, frame, true, MinimapKeys(), t, m, MinimapBar(t));
                            pieces += s.ItemCount;
                            string meet = MinimapMeets(s, ax, ay, aw, ah);
                            // The kit's bar is the one thing the card stands over: its top is under the card's foot.
                            if (meet != null)
                            {
                                meets++;
                                fault ??= meet + " (" + at + ")";
                            }
                            if (ay + ah > frame.BandY + 0.01f)
                            {
                                meets++;
                                fault ??= "the card's foot is under the bar's top (" + at + ")";
                            }
                            string cards = MinimapCards(t, frame, ax, ay, aw, ah);
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
        Check(meets == 0, "minimap: with the objectives, an info panel, the notice cards, the key chips, a world prompt, the activity's strip and the toasts as cards all up, no piece of the HUD or its bar meets the card, at 1080, 720 and 4K, at the game's zoom, in English, Portuguese and Chinese ("
                          + cases + " layouts, " + pieces + " pieces looked at)" + (fault == null ? string.Empty : ": " + fault));
        Check(inColumn > 0 && beside > 0, "minimap: those layouts took the card to both of its places (the column " + inColumn + ", beside it " + beside + ", no room " + noRoom + ")");
        MinimapAvoidChecks();
    }

    // Variants: 0 two groups of objectives (a short column: the card has the room under it), 1 every group (it stands beside the
    // column), 2 an info panel and more than the screen holds, with the game's hints up. All carry the activity's strip, the notice cards, the chips and a world prompt.
    private static UiHudModel MinimapHud(UiWords t, int variant, float zoom)
    {
        UiObjectivesModel goals = UiSamples.PiecesGoals(t, variant == 2);
        if (variant == 0)
        {
            goals.Groups = new[] { goals.Groups[0], goals.Groups[1] };   // a short column: the card has the room under it
        }
        UiHudNoticeModel[] notes = UiSamples.PiecesNotes(t, false);
        var m = new UiHudModel
        {
            Objectives = goals,
            Info = variant == 0 ? null : UiSamples.HudBusiness(t),
            Notices = notes,
            NoticeCount = zoom >= 1.5f ? 1 : 2,   // a goal reached and the day's summary; from a zoom of 150% the HUD is 1280 px wide and a second card would not clear the card beside the column
            Prompt = UiSamples.HudDoor(t, MinimapKeys()),
            PointX = 700f,
            PointY = 600f,
            Activity = UiSamples.HudActivity(t, UiActivityState.Running, MinimapKeys()),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Pick1, Label = t("input_key_throttle", "Throttle") }, new UiPrompt { Action = UiAction.Pick2, Label = t("input_key_brakes", "Brakes") },
                new UiPrompt { Action = UiAction.Pick3, Label = t("input_key_handbrake", "Handbrake") }, new UiPrompt { Action = UiAction.Back, Label = t("input_key_menu", "Menu") },
            },
            PromptCount = 4,
        };
        return m;
    }

    private static IUiKeys MinimapKeys() => UiDefaultKeys.Keyboard;

    private static UiStatusModel MinimapBar(UiWords t) => UiSamples.PiecesBar(false);

    // A screen's pixels from the bottom left to the HUD layout's px from the top left, whole px (UiMinimapBox.Read).
    private static void ToHudPx(float left, float lowY, float right, float highY, int screenW, int screenH, UiFrame frame, out float x, out float y, out float w, out float h)
    {
        UiHudRules.ToLayout(left, highY, screenW, screenH, frame.Width, frame.Height, out float l, out float top);
        UiHudRules.ToLayout(right, lowY, screenW, screenH, frame.Width, frame.Height, out float r, out float bottom);
        x = (float)Math.Floor(l);
        y = (float)Math.Floor(top);
        w = (float)Math.Ceiling(r - l);
        h = (float)Math.Ceiling(bottom - top);
    }

    // The first piece of the scene that shares any area with the card, or null. A box, a word or a glyph of the HUD, the bar included.
    private static string MinimapMeets(UiScene s, float x, float y, float w, float h)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            UiItem it = s.Items[i];
            if (it.W > 0f && it.H > 0f && UiHudRules.Overlaps(it.X, it.Y, it.W, it.H, x, y, w, h))
            {
                return "a " + it.Kind + (string.IsNullOrEmpty(it.Text) ? string.Empty : " '" + it.Text + "'") + " at " + it.X.ToString("0") + "," + it.Y.ToString("0") + " " + it.W.ToString("0") + " x " + it.H.ToString("0")
                       + " meets the card at " + x + "," + y + " " + w + " x " + h;
            }
        }
        return null;
    }

    // The toasts as cards (the game's stack at the top right, a few at once, tall ones too) on their own host.
    private static string MinimapCards(UiWords t, UiFrame frame, float x, float y, float w, float h)
    {
        foreach (float top in new[] { UiTokens.S4, 120f, 360f })
        {
            var cards = new UiNoticeCardsModel { Top = top, AvoidX = x, AvoidY = y, AvoidW = w, AvoidH = h, Cards = new UiNotice[UiNotices.MostCards], CardCount = UiNotices.MostCards };
            for (int i = 0; i < UiNotices.MostCards; i++)
            {
                cards.Cards[i] = new UiNotice
                {
                    Icon = UiIcon.Bell, Header = t("reallife_ui_notice_other", "Notice"), Age = "Day 412, 14:32",
                    Body = "A long sentence about what just happened to the business, so that the card takes its three lines of the body and stands tall in the stack."
                };
            }
            var s = new UiScene();
            UiNotices.LayCards(s, cards, frame, true, MinimapKeys());
            string meet = MinimapMeets(s, x, y, w, h);
            if (meet != null)
            {
                return "the toast cards at top " + top + ": " + meet;
            }
        }
        return null;
    }

    // The same rule shown to the pieces one by one: a piece that stands over the card moves off it, to the side with room, and one that
    // does not meet it stays where it was.
    private static void MinimapAvoidChecks()
    {
        float ax = 100f, ay = 100f;
        UiHudRules.Clear(ref ax, ref ay, 200f, 50f, 1920f, 0f, 0f, 0f, 0f);
        float bx = 900f, by = 100f;
        UiHudRules.Clear(ref bx, ref by, 200f, 50f, 1920f, 0f, 500f, 300f, 300f);
        Check(ax == 100f && ay == 100f && bx == 900f && by == 100f, "minimap: a piece with no card, or clear of it, stays where it stands");
        float rx = 100f, ry = 400f;
        UiHudRules.Clear(ref rx, ref ry, 300f, 60f, 1920f, 0f, 380f, 380f, 600f);
        Check(rx >= 380f + UiHudRules.Margin && rx + 300f <= 1920f - UiHudRules.Margin && ry == 400f, "minimap: a piece over the card's right edge moves to its right, a margin from it");
        float lx = 1500f, ly = 400f;
        UiHudRules.Clear(ref lx, ref ly, 300f, 60f, 1920f, 1200f, 380f, 600f, 600f);
        Check(lx + 300f <= 1200f - UiHudRules.Margin + 0.01f && ly == 400f, "minimap: a piece with no room on the card's right moves to its left");
        float ux = 100f, uy = 700f;
        UiHudRules.Clear(ref ux, ref uy, 1800f, 60f, 1920f, 200f, 640f, 600f, 300f);
        Check(ux == 100f && uy + 60f <= 640f - UiHudRules.Margin + 0.01f, "minimap: a piece too wide for either side stands just above the card");
        float sx = 100f, sy = 60f;
        UiHudRules.Clear(ref sx, ref sy, 1800f, 60f, 1920f, 200f, 40f, 600f, 300f);
        Check(sx == 100f && sy == 60f, "minimap: a piece with no side and no room above stays where it was rather than leaving the screen");

        var model = new UiHudModel { AvoidX = 0f, AvoidY = 700f, AvoidW = 380f, AvoidH = 300f, Objectives = UiSamples.PiecesGoals(UiWords("en"), true) };
        UiHud.LeftColumn(model, true, out float cx, out _, out float cw, out _);
        UiHud.LeftColumn(model, false, out float px, out _, out _, out _);
        Check(px == UiHud.Left && cw > 0f && cx >= model.AvoidX + model.AvoidW,
            "minimap: the left column reads as it stands when the minimap is placed under it, and moves to the card's right where the card has grown up into it");
    }

    // The promises the code makes in its source: the card is read in one place and placed on the slow tick only, with no per-frame work.
    private static void MinimapSourceChecks()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string hud = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Navigation", "NavHud.cs"));
        string director = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Navigation", "NavDirector.cs"));
        string make = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Navigation", "NavHud.Make.cs"));
        Check(!hud.Contains("void Update(") && !hud.Contains("void LateUpdate(") && director.Contains("_hud.Place(Settings.MinimapSize, wantMap, lines)"),
            "minimap: the card is placed on the director's slow tick (four times a second) and has no Update of its own");
        Check(hud.Contains("Sort(up)") && hud.Contains("overrideSorting") && make.Contains("AddComponent<GraphicRaycaster>()") && make.Contains("_canvas = panel.AddComponent<Canvas>()"),
            "minimap: the card has a canvas of its own with a raycaster, sorted over the kit's HUD on the slow tick");
        Check(!hud.Contains("TasksTop = ") && !hud.Contains("Column = 740f"), "minimap: the card's place is worked out in one rules class (NavPlaceRules), not twice");
    }
}
