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
        public UiPrompt[] Prompts = new UiPrompt[0];       // the game's control hints, as key chips over the bar's right end while the keys lead
        public int PromptCount;

        // Appended with the HUD's screens (3 Nov 2026).
        public UiHudNoticeModel[] Notices = new UiHudNoticeModel[0];   // notice cards at the top centre (UiHudNotice)
        public int NoticeCount;
        public UiHudPopupModel[] Popups = new UiHudPopupModel[0];      // the bar's small buttons' panels over it (UiHudPopup)
        public int PopupCount;
        public UiActivityModel ActivityClear;   // an activity strip another view draws (the activity panel's own): kept clear of, never drawn here

        // Appended with the phone's place (3 Nov 2026): the kit's phone, a view of its own at the bar's right end, as px of
        // the layout (its part on the screen); the key chips keep clear of it. 0 wide while it does not show.
        public float PhoneX, PhoneY, PhoneW, PhoneH;
    }

    /// <summary>
    /// The HUD over the running game, after Cities: Skylines II: one calm status bar along the foot (UiDraw.StatusBar: the
    /// host's app row and the pills, UiDraw.HudBar) and the pieces at the screen's edges round the city, never in its middle
    /// but for what the player is doing: the left column (an info panel docked at the top left, the objectives under it),
    /// the side panels stacked at the top right, an activity's strip above the bar at the centre, a build tool row in place
    /// of the apps with its panel rising above it, a world prompt beside its point (UiHudRules.Beside), the money's change
    /// over the money pill, and while the keys lead the game's control hints as small key chips over the bar's right end
    /// (UiDraw.KeyChips), never a strip of their own. The HUD takes no key: the game reads its keys and the kit shows which
    /// (UiGameKey); the mouse presses. Commands are each piece's own (200 to 279, the info panel's 1 to 3), the bar's pills
    /// (9020 to 9024) and the frame's app row and All apps (9010 to 9019, UiDraw.AppRow). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiHud
    {
        /// <summary>The left column's x, and the side panels' right edge from the screen's: the bar's own edge, so the corners line up.</summary>
        public const float Left = UiTokens.S4;

        /// <summary>The HUD as one scene: every piece the model holds in its place, the status bar last.</summary>
        public static void Lay(UiScene s, UiHudModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("hud", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            UiStatusModel bar = UiChrome.Bar != null ? UiChrome.Bar() : null;
            AddPrompts(s, m, bar, keys);
            Draw(s, m, frame, keys, true, bar);
            Bar(s, m, frame, keys);
        }

        /// <summary>
        /// The HUD's prompts, said before its pieces are laid so they keep clear of the chips: the game's control hints the
        /// model holds, and All apps on the pad's View while a pad leads.
        /// </summary>
        public static void AddPrompts(UiScene s, UiHudModel m, UiStatusModel bar, IUiKeys keys)
        {
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            if (UiDraw.AllAppsPrompt(bar, keys))
            {
                s.AddPrompt(UiAction.View, bar.AllAppsLabel);
            }
        }

        /// <summary>
        /// The bar and what stands on it, after the pieces: the status bar with its pills, the prompts as key chips over its
        /// right end while the keys lead, and a build tool row in place of the apps.
        /// </summary>
        public static void Bar(UiScene s, UiHudModel m, UiFrame frame, IUiKeys keys)
        {
            UiDraw.StatusBar(s, frame, keys, false);
            if (Chips(s, m, frame, keys, out float x, out float y, out float w, out float h))
            {
                UiDraw.KeyChips(s, x + w, y, keys);
            }
            OnBar(s, m, frame);
        }

        /// <summary>
        /// Where the key chips stand: ending at the bar's right edge, a step over the bar, on the phone's left while the phone
        /// stands there (open or folded), lifted over the activity's strip or a tool's panel where they would meet them.
        /// False while the mouse leads or there is nothing to say.
        /// </summary>
        public static bool Chips(UiScene s, UiHudModel m, UiFrame frame, IUiKeys keys, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            if (frame.Pointer || keys == null || s.PromptCount == 0)
            {
                return false;
            }
            w = UiDraw.KeyChipsWidth(s, keys);
            h = UiDraw.KeyChipHeight;
            x = frame.Width - Left - w;
            y = frame.BandY - UiTokens.S3 - h;
            if (m != null && m.PhoneW > 0f && m.PhoneH > 0f && UiHudRules.Overlaps(x, y, w, h, m.PhoneX, m.PhoneY, m.PhoneW, m.PhoneH))
            {
                // The phone stands at the bar's right end, over the chips' place (the owner, 3 Nov 2026: "The map, pause and
                // menu are overlapping on the phone"): the chips keep their row and end a step left of it.
                x = UiDraw.SnapDown(m.PhoneX - UiTokens.S2 - w);
            }
            UiActivityModel strip = m == null ? null : m.Activity ?? m.ActivityClear;
            if (strip != null && UiActivity.Strip(strip.State))
            {
                UiActivity.StripPlace(frame, strip, out float ax, out float ay);
                if (UiHudRules.Overlaps(x, y, w, h, ax, ay, UiActivity.StripWidth, UiActivity.Height(strip)))
                {
                    y = ay - UiTokens.S2 - h;
                }
            }
            if (m != null && UiToolRow.PanelBox(m.Tools, frame, out float tx, out float ty, out float tw, out float th) && UiHudRules.Overlaps(x, y, w, h, tx, ty, tw, th))
            {
                y = ty - UiTokens.S2 - h;
            }
            return w > 0f;
        }

        /// <summary>What stands on the bar itself, drawn after it: a build tool row in place of the apps, with its panel.</summary>
        public static void OnBar(UiScene s, UiHudModel m, UiFrame frame)
        {
            if (m.Tools != null)
            {
                UiToolRow.Draw(s, UiAt.Screen, m.Tools, frame);
            }
        }

        /// <summary>
        /// The pieces in their places, without the bar: the left column, the side panels, the strip, the money's change, and the
        /// world prompt when <paramref name="prompt"/> (the game draws it in a view that follows its point). The tool row stands
        /// on the bar and is drawn after it (<see cref="OnBar"/>).
        /// </summary>
        public static void Draw(UiScene s, UiHudModel m, UiFrame frame, IUiKeys keys, bool prompt) => Draw(s, m, frame, keys, prompt, UiChrome.Bar != null ? UiChrome.Bar() : null);

        /// <summary>The pieces in their places, with the bar's model already read; the prompts said first keep their chips clear.</summary>
        public static void Draw(UiScene s, UiHudModel m, UiFrame frame, IUiKeys keys, bool prompt, UiStatusModel bar)
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
                UiSidePanel.LayStack(s, UiAt.Screen, m.Side, m.Side.Length, frame.Width - Left, UiTokens.S4, keys);
                Join(s);
            }
            if (m.Activity != null && UiActivity.Strip(m.Activity.State))
            {
                UiActivity.StripPlace(frame, m.Activity, out float ax, out float ay);
                UiActivity.DrawStrip(s, UiAt.Screen, m.Activity, ax, ay);
            }
            if (m.NoticeCount > 0)
            {
                UiHudNotice.LayStack(s, UiAt.Screen, m.Notices, m.NoticeCount, frame.Width, UiTokens.S4);
            }
            if (m.PopupCount > 0)
            {
                UiHudPopup.LayAll(s, UiAt.Screen, m.Popups, m.PopupCount, frame.Width, frame.BandY);
            }
            bool chips = Chips(s, m, frame, keys, out float cx, out float cy, out float cw, out float ch);
            if (m.MoneyChange && bar != null && !string.IsNullOrEmpty(bar.MoneyChange) && !string.IsNullOrEmpty(bar.Money))
            {
                MoneyChangePlace(bar, frame, out float right, out float bottom);
                if (chips && right > cx && cy + ch > bottom - UiDraw.MoneyChangeHeight)
                {
                    bottom = Math.Min(bottom, cy - UiTokens.S2);   // it rises from over the chips, as it would from the bar
                }
                UiDraw.MoneyChange(s, UiAt.Screen, right, bottom, bar.MoneyChange, bar.MoneyChangeRises);
            }
            if (prompt && m.Prompt != null)
            {
                PromptPlace(m, frame, chips ? cy : frame.BandY, out float px, out float py);
                UiWorldPrompt.Lay(s, UiAt.Screen, m.Prompt, px, py);
            }
        }

        // The left column's corner and the side panels' first fold stand on one row across the top: Right and Left join them.
        private static void Join(UiScene s)
        {
            int left = s.FindNode(UiObjectives.FoldCommand, 0), right = s.FindNode(UiSidePanel.FoldCommand, 0);
            if (left >= 0 && right >= 0)
            {
                s.Nodes[left].Right = right;
                s.Nodes[right].Left = left;
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
        public static void PromptPlace(UiHudModel m, UiFrame frame, out float x, out float y) => PromptPlace(m, frame, frame.BandY, out x, out y);

        /// <summary>The world prompt's place above a floor: the bar's top, or the key chips' while they stand over it.</summary>
        public static void PromptPlace(UiHudModel m, UiFrame frame, float floor, out float x, out float y)
        {
            LeftColumn(m, out float lx, out float ly, out float lw, out float lh);
            UiHudRules.Beside(m.PointX, m.PointY, m.PointAhead, UiWorldPrompt.Width, UiWorldPrompt.Height(m.Prompt), frame.Width, floor, lx, ly, lw, lh, out x, out y);
            x = UiDraw.SnapDown(x);   // a still scene keeps the grid; the game's follower moves the box by the pixel
            y = UiDraw.SnapDown(y);
        }
    }
}
