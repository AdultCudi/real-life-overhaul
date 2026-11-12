using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where a step of a track stands: done, the current one, the one after it, later on, or not open yet.</summary>
    public enum UiStepState : byte
    {
        Done = 0,
        Now,
        Next,
        Locked,
        Later
    }

    /// <summary>A step of a track: its glyph on a tile, its name, a note under it (when, what it needs) and where it stands.</summary>
    public struct UiTrackStep
    {
        public UiIcon Icon;
        public string Label;
        public string Note;
        public UiStepState State;
    }

    /// <summary>A short line of steps (a career's ladder, a game's milestones, a building's stages) with the current one marked.</summary>
    public sealed class UiTrackModel
    {
        public string Title;
        public string Line;          // where it stands in words ("Shift lead for 64 days")
        public UiTrackStep[] Steps = new UiTrackStep[0];
        public string[] StateWords = new string[5];    // the chip's words by UiStepState ("Done", "Now", "Next", "Locked", "Later")
    }

    /// <summary>
    /// A track, a block of an app window's page or an info panel: a short line of steps (a career's ladder, a game's
    /// milestones, a building's stages), each its glyph on a tile with its name, a note under it and its state in a chip,
    /// joined by a rule that runs cyan up to the current step, whose tile is filled cyan as a game marks the one in hand.
    /// A state is told by the chip and the rule, never by recolouring a glyph. Each step is a target (what it needs, when
    /// it comes). Commands from <see cref="First"/>: <see cref="Step"/> (the step's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiTrack
    {
        public const int First = 150;
        public const int Step = 0;
        private const float Joint = 2f;    // px: the rule between two steps

        /// <summary>The height a track takes: its title line, the tiles, each step's name, note and chip.</summary>
        public static float Height(UiTrackModel m) => UiDraw.Snap(TitleH(m) + UiTokens.IconTile + UiTokens.S2 + UiTextRules.LineHeight(UiStyle.Title) + UiTextRules.LineHeight(UiStyle.Small)
                                                                   + UiTokens.S2 + UiTokens.Badge + UiTokens.S1);

        private static float TitleH(UiTrackModel m) => string.IsNullOrEmpty(m.Title) ? 0f : UiTokens.PairRow + UiTokens.S3;

        /// <summary>Lays a track out across a width. Returns its height.</summary>
        public static float Lay(UiScene s, UiAt at, UiTrackModel m, float x, float y, float w, int command = First)
        {
            float ty = y;
            if (!string.IsNullOrEmpty(m.Title))
            {
                float lw = string.IsNullOrEmpty(m.Line) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(m.Line, UiStyle.Body) + UiTokens.S1));
                UiDraw.TextMiddle(s, at, x, ty, UiTokens.PairRow, w - lw - UiTokens.S4, UiStyle.SectionHeader, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
                if (lw > 0f)
                {
                    UiDraw.TextMiddle(s, at, x + w - lw, ty, UiTokens.PairRow, lw, UiStyle.Body, UiRole.Text2, m.Line, UiFlags.Right | UiFlags.Truncate);
                }
                ty += TitleH(m);
            }
            int n = m.Steps.Length;
            if (n == 0)
            {
                return ty - y;
            }
            float slot = w / n, tile = UiTokens.IconTile;
            int now = -1;
            for (int i = 0; i < n; i++)
            {
                now = m.Steps[i].State == UiStepState.Now ? i : now;
            }
            // The rule first, under the tiles: cyan from the first step up to the current one, the track's grey after it.
            for (int i = 1; i < n; i++)
            {
                float x0 = x + slot * (i - 0.5f) + tile / 2f, x1 = x + slot * (i + 0.5f) - tile / 2f;
                bool reached = m.Steps[i].State == UiStepState.Done || m.Steps[i].State == UiStepState.Now;
                UiDraw.Box(s, at, x0 + UiTokens.S1, ty + (tile - Joint) / 2f, Math.Max(0f, x1 - x0 - UiTokens.S2), Joint, reached ? UiRole.Accent : UiRole.Track, UiRole.None, Joint / 2f,
                    UiFlags.Decor);
            }
            float h = Height(m) - TitleH(m);
            for (int i = 0; i < n; i++)
            {
                ref UiTrackStep st = ref m.Steps[i];
                float sx = x + slot * i;
                int face = UiDraw.Box(s, at, sx, ty, slot, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge, UiFlags.Share);
                UiAt inside = at.In(face);
                float tx = sx + (slot - tile) / 2f;
                bool current = st.State == UiStepState.Now;
                int t = UiDraw.Tile(s, inside, tx, ty, tile, UiRole.None, current);
                s.Items[t].Flags |= UiFlags.Share;
                float g = UiTokens.S6;
                UiDraw.Icon(s, inside.In(t), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, st.Icon, current ? UiRole.OnAccent : UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                float ly = ty + tile + UiTokens.S2, lw = slot - UiTokens.S2;
                UiDraw.Text(s, inside, sx + UiTokens.S1, ly, lw, UiStyle.Title, UiRole.Text, st.Label, UiFlags.Centre | UiFlags.Truncate | UiFlags.Middle);
                ly += UiTextRules.LineHeight(UiStyle.Title);
                UiDraw.Text(s, inside, sx + UiTokens.S1, ly, lw, UiStyle.Small, UiRole.Text2, st.Note, UiFlags.Centre | UiFlags.Truncate | UiFlags.Middle);
                ly += UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S2;
                string words = m.StateWords != null && (int)st.State < m.StateWords.Length ? m.StateWords[(int)st.State] : null;
                if (!string.IsNullOrEmpty(words))
                {
                    float cw = Math.Min(UiDraw.SnapDown(lw), UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1));
                    UiDraw.Chip(s, inside, sx + (slot - cw) / 2f, ly, words, Tone(st.State), cw);
                    s.Items[s.ItemCount - 3].Flags |= UiFlags.Share | UiFlags.Middle;   // the chip's face, centred under its step
                }
                UiDraw.Node(s, at, sx, ty, slot, h, command + Step, i, face);
            }
            return Height(m);
        }

        private static UiRole Tone(UiStepState state) => state == UiStepState.Done ? UiRole.Ok : state == UiStepState.Now ? UiRole.Accent : UiRole.Muted;
    }
}
