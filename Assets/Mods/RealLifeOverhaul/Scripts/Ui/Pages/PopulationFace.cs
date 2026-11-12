using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The city's people as an app window of the kit: no sidebar, because every page is one step from the one before. A
    /// place or a household or a person stands as the profile's head (a person's face, a place's glyph on a tile) with its
    /// key figures, the city as its figures across with the way to who contributes beside them. Under it the rows a press
    /// opens, as the big table draws them (a tile for a place, a face for a person), and the facts on a band beside them.
    /// Commands: <see cref="UiAppWindow.ActionCommand"/> and the big table's rows (<see cref="UiDataTable.First"/> with
    /// <see cref="UiDataTable.Row"/>). Game-free. Cost: a layout a move.
    /// </summary>
    public static class PopulationFace
    {
        public const int RowCommand = UiDataTable.First + UiDataTable.Row;
        private const float FactsLeast = 320f, FactsMost = 440f;   // px: the facts' band
        private const float FactsShare = 0.32f;                    // of the page's width

        public static void Lay(UiScene s, PopulationModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("population", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, null, UiIcon.People, null, 0, false, keys, out float x, out float y, out float w, out float bottom);
            int page = s.NodeCount;
            if (m.Gone)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.People, m.GoneTitle, m.GoneBody);
            }
            else
            {
                y = Head(s, at, m, x, y, w, keys);
                Body(s, at, m, x, y, w, bottom - y, keys);
                UiPageFence.Lay(s, page, x, x + w);
                int first = s.FindNode(RowCommand, 0);
                s.FocusFirst = first >= 0 ? first : s.FocusFirst;   // the keys start on what the page leads to, not on its one action
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The head: a profile with the action at its right, or the city's figures with the action beside them.
        private static float Head(UiScene s, UiAt at, PopulationModel m, float x, float y, float w, IUiKeys keys)
        {
            UiActionModel[] actions = m.HasAction ? new[] { m.Action } : new UiActionModel[0];
            if (m.Head != null)
            {
                return UiAppWindow.Profile(s, at, m.Head, actions, x, y, w, keys);
            }
            float bw = m.HasAction ? UiDraw.ButtonWidth(m.Action.Kind, m.Action.Label, m.Action.Icon, m.Action.Key, keys) : 0f;
            float fw = bw > 0f ? w - bw - UiTokens.S6 : w;
            float under = UiAppWindow.Figures(s, at, m.Figures, x, y, fw);
            if (bw > 0f)
            {
                float band = under - UiTokens.S5 - y;
                UiDraw.Button(s, at, x + w - bw, y + UiDraw.SnapDown((band - UiTokens.Control) / 2f), bw, UiTokens.Control, m.Action.Kind, m.Action.Label, m.Action.Icon,
                    m.Action.Key, keys, UiAppWindow.ActionCommand, 0);
            }
            return under;
        }

        // The rows at the left, filling the room, and the facts at the right on their band.
        private static void Body(UiScene s, UiAt at, PopulationModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float facts = m.Sections.Length > 0 ? UiDraw.SnapDown(Math.Max(FactsLeast, Math.Min(FactsMost, w * FactsShare))) : 0f;
            float gap = facts > 0f ? UiTokens.S6 : 0f;
            float lw = w - facts - gap;
            UiDataTable.Lay(s, at, m.List, x, y, lw, h, keys);
            float fy = y;
            for (int i = 0; i < m.Sections.Length; i++)
            {
                fy = Section(s, at, ref m.Sections[i], x + lw + gap, fy, facts);
            }
        }

        // A section on its band: its label in capitals, then its rows.
        private static float Section(UiScene s, UiAt at, ref PopulationSection section, float x, float y, float w)
        {
            int lines = section.Lines == null ? 0 : section.Lines.Length;
            float h = UiTokens.S2 * 2f + UiTokens.PairRow * (1 + lines);
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, ry = y + UiTokens.S2;
            ry += UiDraw.SectionRow(s, at, ix, ry, iw, section.Title, null, false, -1, 0);
            for (int i = 0; i < lines; i++)
            {
                ry += UiDraw.InfoRow(s, at, ix, ry, iw, section.Lines[i].Key, section.Lines[i].Value);
            }
            return y + h + UiTokens.S2;
        }
    }
}
