using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>How a screen stands over the world: an app's window, a panel docked at the top left, a card or the phone.</summary>
    public enum UiFrameKind : byte
    {
        Window = 0,   // an app: a centred window with the city around it
        Dock,         // an info panel or an info view: docked at the top left, the world beside it
        Card,         // a decision: centred over the dimmed world
        Phone         // the phone, at the bottom right as the game holds it
    }

    /// <summary>
    /// Where a screen stands: the whole area the host gives it, in px, over the game's world, with the status bar the kit
    /// paints along its foot (the time, the money, the player's needs, the apps, and the keys while they lead). Nothing is
    /// full bleed: an app is a window at about 86% by 84% of the screen with the city visible round it, as Cities: Skylines
    /// II opens its large windows (design/DIRECTION_CS2.md, sections 1 and 4). The canvas scales with the screen (3840 x
    /// 2160, expanded), so the area is never narrower than 1920 px and only its height and an ultrawide's width change.
    /// Game-free. Cost: a few sums.
    /// </summary>
    public struct UiFrame
    {
        public const float ScreenHeight = 1080f;   // px: the canvas's reference height, the least any screen gives (16:9)
        public const float WindowShareX = 0.86f, WindowShareY = 0.84f;   // a window's share of the screen: the world shows round it
        public const float MostWindow = 2240f;     // px: the widest a window runs; an ultrawide centres it with more world round it
        public const float Strip = 40f;            // px: a panel's or a window's title strip
        public const float Toolbar = 56f;          // px: the status bar's row of app buttons on their tiles
        public const float StatusRow = 44f;        // px: its row of pills

        public float Width;     // px: the area's width
        public float Height;    // px: the area's whole height, the status bar included
        public float Top;       // px: kept for the host's binding: no masthead, so 0
        public float Band;      // px: the status bar's height at the foot
        public string Title;    // the window's title, written in its strip
        public UiIcon Icon;     // the strip's icon
        public UiFrameKind Kind;
        public bool Pointer;    // the mouse leads (UiFocusMode): no key caps or prompts, the mouse does it by pointing
        public bool Held;       // a layer of the kit's stands over the screen with the keys: its buttons' caps go, one key one thing

        /// <summary>No masthead: the status bar at the foot says the money, the day and the time.</summary>
        public static float MastheadHeight => 0f;

        /// <summary>The status bar's height: its row of apps and its row of pills.</summary>
        public static float BandHeight => Toolbar + StatusRow;

        /// <summary>The frame of an app's window on a screen of an aspect (16:9 is 1920 x 1080).</summary>
        public static UiFrame FullMenu(float aspect, string title = null)
        {
            float canvasW = Math.Max(3840f, 2160f * aspect), canvasH = Math.Max(2160f, 3840f / aspect);
            return new UiFrame { Width = canvasW / UiTokens.CanvasPerPx, Height = canvasH / UiTokens.CanvasPerPx, Top = 0f, Band = BandHeight, Title = title };
        }

        /// <summary>A frame of another kind on the same screen.</summary>
        public UiFrame As(UiFrameKind kind)
        {
            UiFrame f = this;
            f.Kind = kind;
            return f;
        }

        /// <summary>The top of the world's room: an overlay dims everything above the status bar.</summary>
        public float ContentTop => 0f;

        /// <summary>The y where the status bar begins.</summary>
        public float BandY => Height - Band;

        /// <summary>The window: centred over the world above the status bar, at its share of the screen.</summary>
        public void Window(out float x, out float y, out float w, out float h)
        {
            w = UiDraw.Snap(Math.Min(MostWindow, Width * WindowShareX));
            float room = BandY;
            h = Math.Min(UiDraw.Snap(Math.Min(Height, ScreenHeight * Math.Max(1f, Height / ScreenHeight)) * WindowShareY), UiDraw.SnapDown(room - UiTokens.S8 * 2f));
            x = UiDraw.SnapDown((Width - w) / 2f);
            y = UiDraw.SnapDown((room - h) / 2f);
        }

        /// <summary>The content box: the window's inside under its title strip, its padding taken off.</summary>
        public void Content(out float x, out float y, out float w, out float h)
        {
            Window(out float wx, out float wy, out float ww, out float wh);
            x = wx + UiTokens.S4;
            y = wy + Strip + UiTokens.S4;
            w = ww - UiTokens.S4 * 2f;
            h = UiDraw.SnapDown(wh - Strip - UiTokens.S4 * 2f);
        }

        /// <summary>
        /// The room a page is judged against (RLO-UI-016): the content box, no taller than a 16:9 screen's. A taller
        /// screen's extra height is room its lists take, never a hole a page of fixed parts has to fill.
        /// </summary>
        public void Room(out float x, out float y, out float w, out float h)
        {
            Content(out x, out y, out w, out h);
            h = Math.Min(h, UiDraw.SnapDown((ScreenHeight - Band) * WindowShareY / 0.92f - Strip - UiTokens.S4 * 2f));
        }
    }

    /// <summary>
    /// What the status bar says: the game file fills it from the save (the money, the day, the time, the player's needs),
    /// an offline render from its samples. Null fields leave their pill out.
    /// </summary>
    public sealed class UiStatusModel
    {
        public string Time;           // "14:32"
        public string Day;            // "Tue, Day 12"
        public bool Paused;
        public int Speed;             // 1 to 3, as the game's speed buttons, or 0 while the game has not said
        public string Season;         // "Autumn"
        public UiIcon Weather = UiIcon.CloudSun;
        public string Temperature;    // beside the weather's icon, as the game writes it; null for the season alone
        public string Place;          // the district the player is in
        public string Money;          // "$48,210"
        public int MoneyTrend;        // +1 rising today, -1 falling, 0 still
        public int Energy = -1;       // 0 to 100, or -1 to leave the meter out
        public int Hunger = -1;
        public int Happiness = -1;
        public int Alerts;            // notifications waiting
        public UiIcon[] Apps;         // the toolbar's app buttons, in order
        public UiRole[] AppTints;     // each one's category colour
        public int App = -1;          // the app that is open, filled cyan
        public int[] AppBadges;       // each one's news, as its own button counts it; null for none
        public bool AllApps;          // the All apps tile ends the row
        public bool AllAppsOpen;      // the All apps panel is up: its tile filled cyan
        public int AllAppsBadge;      // news of apps the row never lists (another mod's), on the All apps tile
        public bool AppsPress;        // the tiles are targets: true where the frame answers them (the full menu)
        public string AllAppsLabel;   // the prompt's words for the pad's View, which opens All apps from anywhere
    }

    /// <summary>The status bar's source; the game file sets it. Status is the older one line, kept for the game file's log.</summary>
    public static class UiChrome
    {
        public static Func<string> Status;
        public static Func<UiStatusModel> Bar;
    }
}
