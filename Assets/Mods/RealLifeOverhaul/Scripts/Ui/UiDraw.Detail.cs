using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A status's tone: how loud its chip is.</summary>
    public enum UiTone : byte
    {
        Quiet = 0,
        Ok,
        Warn,
        Danger
    }

    /// <summary>An action a detail offers: its words, its kind, its icon, the key that runs it and its command.</summary>
    public struct UiActionModel
    {
        public string Label;
        public UiButtonKind Kind;
        public UiIcon Icon;
        public UiAction Key;     // the action whose key runs it from anywhere on the screen, or Count for none
        public int Command;
        public int Arg;
        public bool Disabled;
    }

    /// <summary>The kit's detail: its hero (avatar, name, role, status and actions), a status chip, a thread's bubbles and the composer.</summary>
    public static partial class UiDraw
    {
        /// <summary>
        /// A detail's hero: the avatar in its accent ring, the name, the role and the status chip, and the actions at the
        /// right, the primary last, on the panel's own flat surface. Every action runs the template's one command with its
        /// own argument, so a screen cannot hand a button a number its press never answers. Returns the hero's height.
        /// </summary>
        public static float Hero(UiScene s, UiAt at, float x, float y, float w, string name, float hue, string role, string status, UiTone tone,
            UiActionModel[] actions, int actionCount, IUiKeys keys, int command)
        {
            const float pad = UiTokens.S6, size = UiTokens.AvatarHero - UiTokens.S4;
            float h = pad + size + UiTokens.S5;
            // The ring says their state, as inZOI's: no state, no ring.
            UiRole ring = tone == UiTone.Ok ? UiRole.Ok : tone == UiTone.Warn ? UiRole.Warn : tone == UiTone.Danger ? UiRole.Danger : UiRole.None;
            Portrait(s, at, x + pad, y + pad, size, name, hue, ring);
            // The actions first, from the right, so the name knows its room.
            float ax = x + w - UiTokens.S5;   // the actions end where the columns under them end
            float ay = y + pad + (size - UiTokens.Control) / 2f;
            for (int i = actionCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                ax -= bw;
                Button(s, at, ax, ay, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, command, a.Arg, a.Disabled);
                ax -= UiTokens.S3;
            }
            float tx = x + pad + size + UiTokens.S6;
            float tw = Math.Max(UiTokens.S16, ax - UiTokens.S3 - tx);
            float nameH = UiTextRules.LineHeight(UiStyle.Hero);
            float block = nameH + UiTokens.S2 + UiTokens.S8;
            float top = y + pad + (size - block) / 2f;
            Text(s, at, tx, top, tw, UiStyle.Hero, UiRole.Text, name, UiFlags.Truncate | UiFlags.Middle);
            float sy = top + nameH + UiTokens.S2;
            float chipW = string.IsNullOrEmpty(status) ? 0f : StatusChipWidth(status);
            float rw = Math.Min(UiTextRules.Width(role, UiStyle.Body) + UiTokens.S1, tw - (chipW > 0f ? chipW + UiTokens.S3 : 0f));
            TextMiddle(s, at, tx, sy, UiTokens.S8, rw, UiStyle.Body, UiRole.Text2, role, UiFlags.Truncate);
            if (chipW > 0f)
            {
                StatusChip(s, at, tx + rw + UiTokens.S3, sy, status, tone);
            }
            return h;
        }

        /// <summary>A status chip's width before it is drawn.</summary>
        public static float StatusChipWidth(string status) => Snap(UiTokens.S3 + UiTokens.StatusDot + UiTokens.S2 + UiTextRules.Width(status, UiStyle.SmallStrong) + UiTokens.S3);

        /// <summary>A status in a chip: a dot and the words in the tone's colour; returns its width.</summary>
        public static float StatusChip(UiScene s, UiAt at, float x, float y, string status, UiTone tone)
        {
            UiRole fill, ink;
            switch (tone)
            {
                case UiTone.Ok: fill = UiRole.OkSoft; ink = UiRole.OkText; break;
                case UiTone.Warn: fill = UiRole.WarnSoft; ink = UiRole.WarnText; break;
                case UiTone.Danger: fill = UiRole.DangerSoft; ink = UiRole.DangerText; break;
                default: fill = UiRole.Notice; ink = UiRole.NoticeText; break;
            }
            float w = StatusChipWidth(status), h = UiTokens.S8;
            int chip = Box(s, at, x, y, w, h, fill, UiRole.None, UiTokens.RadiusMedium, UiFlags.Middle | UiFlags.Centre);
            UiAt inside = at.In(chip);
            Circle(s, inside, x + UiTokens.S3, y + (h - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, UiTokens.StatusDot, ink, UiFlags.Middle | UiFlags.Centre);
            TextMiddle(s, inside, x + UiTokens.S3 + UiTokens.StatusDot + UiTokens.S2, y, h, w - UiTokens.S3 * 2f - UiTokens.StatusDot - UiTokens.S2 + UiTokens.S1,
                UiStyle.SmallStrong, ink, status, UiFlags.Centre);
            return w;
        }

        /// <summary>A message's height in a thread of a width, before it is drawn (a thread lays out from the bottom).</summary>
        public static float BubbleHeight(ref UiMessageModel m, float w)
        {
            if (m.Table != null)
            {
                return TableBubbleHeight(ref m, w);
            }
            UiStyle style = m.From == UiFrom.System ? UiStyle.Small : UiStyle.Body;
            float most = (m.From == UiFrom.System ? 0.9f : 0.72f) * w - UiTokens.S4 * 2f;
            int lines = UiTextRules.Lines(m.Text, style, most, 12, out float _);
            float timeH = string.IsNullOrEmpty(m.Time) ? 0f : UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small);
            return Snap(UiTokens.S3 * 2f + Math.Max(1, lines) * UiTextRules.LineHeight(style) + timeH);
        }

        /// <summary>
        /// A message: theirs at the left on the bubble's fill, the player's at the right on the accent, the system's
        /// centred and quiet; its time under its words. Returns its height.
        /// </summary>
        public static float Bubble(UiScene s, UiAt at, float x, float y, float w, ref UiMessageModel m)
        {
            if (m.Table != null)
            {
                return TableBubble(s, at, x, y, w, ref m);
            }
            bool sys = m.From == UiFrom.System, me = m.From == UiFrom.Me;
            UiStyle style = sys ? UiStyle.Small : UiStyle.Body;
            float most = (sys ? 0.9f : 0.72f) * w - UiTokens.S4 * 2f;
            UiTextRules.Lines(m.Text, style, most, 12, out float widest);
            float tw = Math.Max(widest, UiTextRules.Width(m.Time, UiStyle.Small)) + UiTokens.S1;
            float bw = Math.Min(Snap(tw + UiTokens.S4 * 2f), Snap(most + UiTokens.S4 * 2f));
            float bh = BubbleHeight(ref m, w);
            float bx = sys ? x + (w - bw) / 2f : me ? x + w - bw : x;
            UiFlags place = sys ? UiFlags.Centre : me ? UiFlags.Right : UiFlags.None;
            // Mine in the cyan at a fifth, theirs on the slate: a solid cyan block would shout over the thread and read as chosen.
            int face = Box(s, at, bx, y, bw, bh, sys ? UiRole.None : me ? UiRole.RowSel : UiRole.Bubble, sys ? UiRole.Line : UiRole.None, UiTokens.RadiusLarge, place);
            UiAt inside = at.In(face);
            UiRole ink = sys ? UiRole.Text2 : UiRole.Text;
            float inner = bw - UiTokens.S4 * 2f;
            int words = Text(s, inside, bx + UiTokens.S4, y + UiTokens.S3, inner, style, ink, m.Text, UiFlags.Wrap | UiFlags.Truncate | (sys ? UiFlags.Centre : UiFlags.None), 12);
            if (!string.IsNullOrEmpty(m.Time))
            {
                int t = Text(s, inside, bx + UiTokens.S4, s.Items[words].Y + s.Items[words].H + UiTokens.S1, inner, UiStyle.Small, me ? UiRole.Text : UiRole.Text2, m.Time,
                    sys ? UiFlags.Centre | UiFlags.Middle : me ? UiFlags.Right | UiFlags.Middle : UiFlags.Middle);
                s.Items[t].Alpha = 1f;
            }
            return bh;
        }

        /// <summary>The composer: what is being written (or its placeholder) and Send, filled once there is something to send. Returns the input's focus target.</summary>
        public static int Composer(UiScene s, UiAt at, float x, float y, float w, string placeholder, string draft, string send, int inputCommand, int sendCommand)
        {
            float h = UiTokens.Control + UiTokens.S2;
            int face = Box(s, at, x, y, w, h, UiRole.Bubble, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            bool ready = !string.IsNullOrEmpty(draft);
            float bw = ButtonWidth(UiButtonKind.Primary, send, UiIcon.Send, UiAction.Count, null);
            float bx = x + w - UiTokens.S2 - bw;
            Button(s, inside, bx, y + UiTokens.S2, bw, UiTokens.ControlSmall, ready ? UiButtonKind.Primary : UiButtonKind.Quiet, send, UiIcon.Send, UiAction.Count, null, sendCommand, 0, !ready);
            TextMiddle(s, inside, x + UiTokens.S5, y, h, bx - x - UiTokens.S5 - UiTokens.S3, UiStyle.Body, ready ? UiRole.Text : UiRole.Text2, ready ? draft : placeholder,
                ready ? UiFlags.Truncate : UiFlags.Truncate | UiFlags.Label);
            return Node(s, at, x, y, bx - x - UiTokens.S2, h, inputCommand, 0, face);
        }
    }
}
