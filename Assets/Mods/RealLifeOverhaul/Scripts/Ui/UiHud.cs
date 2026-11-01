using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the HUD shows over the running game at once, each piece left out while null: a thing in the world as an info
    /// panel and the objectives in the left column, the side panels at the top right, an activity's strip above the bar, a
    /// build tool row in the bar, a world prompt over its point, and the money's change rising from the bar.
    /// </summary>
    public sealed class UiHudModel
    {
        public UiInfoModel Info;
        public UiObjectivesModel Objectives;
        public UiSidePanelModel[] Side = new UiSidePanelModel[0];
        public UiActivityModel Activity;                   // shown as the strip while it runs; its card is a screen of its own
        public UiToolRowModel Tools;
        public UiWorldPromptModel Prompt;                  // the box over a point; in the game a follower of its own draws it
        public float PointX, PointY;                       // where its point lands, px of the layout
        public bool PointAhead = true;                     // the point is in front of the camera
        public bool MoneyChange = true;                    // the bar's money change shows above it while it has one
        public UiPrompt[] Prompts = new UiPrompt[0];       // the game's control hints, at the right of the app row while the keys lead
        public int PromptCount;
    }

    /// <summary>
    /// The HUD over the running game, after Cities: Skylines II: one calm status bar along the foot (UiDraw.StatusBar: the
    /// host's app row and the pills, UiDraw.HudBar) and the pieces at the screen's edges round the city, never in its middle
    /// but for what the player is doing: the left column (an info panel docked at the top left, the objectives under it),
    /// the side panels stacked at the top right, an activity's strip above the bar at the centre, a build tool row in place
    /// of the apps with its panel rising above it, a world prompt beside its point (UiHudRules.Beside), and the money's
    /// change over the money pill. The HUD takes no key: the game reads its keys and the kit shows which (UiGameKey); the
    /// mouse presses. Commands are each piece's own (200 to 279, the info panel's 1 to 3), the bar's pills (9020 to 9023)
    /// and the frame's app row (9010 to 9019). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiHud
    {
        /// <summary>The bar's app row and All apps pressed on the HUD: answered by whoever owns the frame (the shell), or not at all.</summary>
        public static Func<int, int, bool> ChromePressed;

        /// <summary>The alerts pill pressed on the HUD: the notice list opened by whoever owns it, or not at all.</summary>
        public static Func<bool> AlertsPressed;

        /// <summary>How many notices wait, for the alerts pill: told by whoever owns the notice list; unset, the pill stays out.</summary>
        public static Func<int> AlertsWaiting;

        /// <summary>The left column's x, a grid step from the edge as an info panel docks.</summary>
        public const float Left = UiTokens.S2;

        /// <summary>The HUD as one scene: every piece the model holds in its place, the status bar last.</summary>
        public static void Lay(UiScene s, UiHudModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("hud", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, m, frame, keys, true);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>
        /// The pieces in their places, without the bar: the left column, the side panels, the strip, the tool row, the money's
        /// change, and the world prompt when <paramref name="prompt"/> (the game draws it in a view that follows its point).
        /// </summary>
        public static void Draw(UiScene s, UiHudModel m, UiFrame frame, IUiKeys keys, bool prompt)
        {
            float top = UiTokens.S4;
            if (m.Info != null)
            {
                top = UiInfoPanel.Draw(s, m.Info, Left, top) + UiTokens.S2;
            }
            if (m.Objectives != null)
            {
                UiObjectives.Lay(s, UiAt.Screen, m.Objectives, Left, top);
            }
            if (m.Side != null && m.Side.Length > 0)
            {
                UiSidePanel.LayStack(s, UiAt.Screen, m.Side, m.Side.Length, frame.Width - UiTokens.S2, UiTokens.S4, keys);
            }
            if (m.Activity != null && UiActivity.Strip(m.Activity.State))
            {
                UiActivity.StripPlace(frame, out float ax, out float ay);
                UiActivity.DrawStrip(s, UiAt.Screen, m.Activity, ax, ay);
            }
            if (m.Tools != null)
            {
                UiToolRow.Draw(s, UiAt.Screen, m.Tools, frame);
            }
            UiStatusModel bar = UiChrome.Bar != null ? UiChrome.Bar() : null;
            if (m.MoneyChange && bar != null && !string.IsNullOrEmpty(bar.MoneyChange) && !string.IsNullOrEmpty(bar.Money))
            {
                MoneyChangePlace(bar, frame, out float right, out float bottom);
                UiDraw.MoneyChange(s, UiAt.Screen, right, bottom, bar.MoneyChange, bar.MoneyChangeRises);
            }
            if (prompt && m.Prompt != null)
            {
                PromptPlace(m, frame, out float px, out float py);
                UiWorldPrompt.Lay(s, UiAt.Screen, m.Prompt, px, py);
            }
        }

        /// <summary>Where the money's change starts: its right edge on the money pill's, its foot a grid step over the bar.</summary>
        public static void MoneyChangePlace(UiStatusModel bar, UiFrame frame, out float right, out float bottom)
        {
            UiPillPlan plan = UiDraw.PlanPills(bar, frame.Width);
            right = plan.MoneyX + plan.Money;
            bottom = frame.BandY - UiTokens.S2;
        }

        /// <summary>The left column's box (an info panel and the objectives under it), which a world prompt keeps off; 0 wide when empty.</summary>
        public static void LeftColumn(UiHudModel m, out float x, out float y, out float w, out float h)
        {
            x = Left;
            y = UiTokens.S4;
            w = 0f;
            h = 0f;
            float bottom = y;
            if (m.Info != null)
            {
                w = UiInfoPanel.OuterWidth(m.Info);
                bottom += UiInfoPanel.Height(m.Info) + UiTokens.S2;
            }
            if (m.Objectives != null)
            {
                w = Math.Max(w, UiObjectives.Width);
                bottom += UiObjectives.Height(m.Objectives);
            }
            h = w > 0f ? bottom - y : 0f;
        }

        /// <summary>Where the world prompt stands this frame: beside its point, inside the screen above the bar, off the left column.</summary>
        public static void PromptPlace(UiHudModel m, UiFrame frame, out float x, out float y)
        {
            LeftColumn(m, out float lx, out float ly, out float lw, out float lh);
            UiHudRules.Beside(m.PointX, m.PointY, m.PointAhead, UiWorldPrompt.Width, UiWorldPrompt.Height(m.Prompt), frame.Width, frame.BandY, lx, ly, lw, lh, out x, out y);
            x = UiDraw.SnapDown(x);   // a still scene keeps the grid; the game's follower moves the box by the pixel
            y = UiDraw.SnapDown(y);
        }
    }
}
