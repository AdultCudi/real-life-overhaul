using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the time of day tool shows: the light's hour on its slider, its ends, and the two ten minute steps.</summary>
    public sealed class UiTimeOfDayModel
    {
        public string Title;          // "Time of day"
        public string Clock;          // "14:20": the hour the light is set to
        public float Share;           // 0 to 1 along the slider, from its first hour to its last
        public bool Held;             // the keys hold the slider
        public string From, To;       // "05:00", "24:00"
        public string Earlier, Later; // "10 min earlier", "10 min later"
        public float FaceX, FaceW;    // laid out: the slider's face, for a press or a drag on it to read its share
        public bool Bar;              // the status bar drawn under it (the samples); in the game the HUD has it
    }

    /// <summary>
    /// The game's time of day tool (InGameTimeOfDayPanel, opened from the console to light a room at an hour) as a small
    /// panel at the left above the status bar: its title strip with Close, the hour on the kit's slider, the slider's two
    /// ends, and the ten minute steps either way. The world and the bar stay in view, as the tool is for looking at the
    /// light. Commands: <see cref="SliderCommand"/> (arg 0), <see cref="StepCommand"/> (-1 or 1) and the strip's close
    /// (UiDraw.CloseCommand). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiTimeOfDay
    {
        public const int SliderCommand = 251, StepCommand = 252;
        public const float Width = 400f;   // px

        public static void Lay(UiScene s, UiTimeOfDayModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("time of day", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float h = Height();
            float x = UiTokens.S8, y = frame.BandY - UiTokens.S8 - h;
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, UiIcon.Sun, m.Title, true) + UiTokens.S4;
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            UiDraw.Slider(s, on, ix, cy, iw, m.Clock, m.Share, SliderCommand, 0, false, m.Held);
            m.FaceX = ix;
            m.FaceW = iw;
            cy += UiTokens.Control;
            float ew = UiDraw.SnapDown(iw / 2f);
            UiDraw.Text(s, on, ix, cy, ew, UiStyle.Small, UiRole.Text2, m.From, UiFlags.Truncate);
            UiDraw.Text(s, on, ix + iw - ew, cy, ew, UiStyle.Small, UiRole.Text2, m.To, UiFlags.Right | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S4;
            float bw = UiDraw.SnapDown((iw - UiTokens.S3) / 2f);
            int earlier = UiDraw.Button(s, on, ix, cy, bw, UiTokens.Control, UiButtonKind.Secondary, m.Earlier, UiIcon.Minus, UiAction.Count, keys, StepCommand, -1);
            int later = UiDraw.Button(s, on, ix + iw - bw, cy, bw, UiTokens.Control, UiButtonKind.Secondary, m.Later, UiIcon.Plus, UiAction.Count, keys, StepCommand, 1);
            s.Nodes[earlier].Right = later;
            s.Nodes[later].Left = earlier;
            if (m.Bar)
            {
                UiDraw.PromptBar(s, frame, keys);
            }
        }

        /// <summary>The panel's height: its strip, the slider, its ends and the steps.</summary>
        public static float Height() =>
            UiDraw.Snap(UiFrame.Strip + UiTokens.S4 + UiTokens.Control + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S4 + UiTokens.Control + UiTokens.S4);

        /// <summary>The share a point across the screen stands at on the slider laid last.</summary>
        public static float Share(UiTimeOfDayModel m, float x) => m == null || m.FaceW <= 0f ? 0f : UiDraw.SliderShare(m.FaceW, m.Clock, Math.Max(0f, Math.Min(1f, (x - m.FaceX) / m.FaceW)));
    }
}
