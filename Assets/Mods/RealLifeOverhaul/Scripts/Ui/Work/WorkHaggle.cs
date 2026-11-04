using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A haggle as a compact decision card, after the kit's decision card: the seller's face and what they are at the left, the thing and what is
    /// said of it at the right with its facts (their price, its market price, offers left), your offer as an amount stepper, and numbered choices
    /// each with what it comes to. Centred over the page. Every part is the kit's own. Game-free. Cost: a layout a change.
    /// </summary>
    public static class WorkHaggle
    {
        private const float Width = 920f, Side = 248f, Choice = 56f;

        public static void Lay(UiScene s, UiAt at, WorkRow r, List<WorkAct> acts, UiActionModel[] choices, WorkAmount[] amounts, string why, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float inner = Width - Side - UiTokens.S8 * 2f;
            float h = Height(r, acts, inner, why);
            float cw = Math.Min(Width, w), cx = x + UiDraw.SnapDown((w - cw) / 2f), cy = y + Math.Max(0f, UiDraw.SnapDown((bottom - y - h) / 2f));
            int card = UiDraw.Box(s, at, cx, cy, cw, Math.Min(h, bottom - y), UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Decor);
            UiAt on = at.In(card);
            UiDraw.Box(s, on, cx, cy, Side, Math.Min(h, bottom - y), UiRole.Band, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor);
            // the seller at the left
            float face = UiTokens.AvatarHero;
            string seller = r.Seller ?? r.Person ?? string.Empty;
            UiDraw.Portrait(s, on, cx + UiDraw.SnapDown((Side - face) / 2f), cy + UiTokens.S8, face, seller, UiColour.HueOf(seller), UiRole.None);
            float ny = cy + UiTokens.S8 + face + UiTokens.S4;
            UiDraw.Text(s, on, cx + UiTokens.S4, ny, Side - UiTokens.S4 * 2f, UiStyle.Title, UiRole.Text, seller, UiFlags.Centre | UiFlags.Truncate);
            ny += UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1;
            UiDraw.Text(s, on, cx + UiTokens.S4, ny, Side - UiTokens.S4 * 2f, UiStyle.Small, UiRole.Text2, r.Line, UiFlags.Centre | UiFlags.Truncate);
            ny += UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S3;
            if (!string.IsNullOrEmpty(r.Status))
            {
                float chip = Math.Min(Side - UiTokens.S4 * 2f, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Status, UiStyle.SmallStrong) + UiTokens.S1));
                UiDraw.Chip(s, on, cx + (Side - chip) / 2f, ny, r.Status, r.Tone, chip);
            }
            // the card's words at the right
            float tx = cx + Side + UiTokens.S8, ty = cy + UiTokens.S8;
            UiDraw.Text(s, on, tx, ty, inner, UiStyle.SectionHeader, UiRole.Text2, r.Kicker, UiFlags.Label | UiFlags.Truncate);
            ty += UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2;
            UiDraw.Text(s, on, tx, ty, inner, UiStyle.AppTitle, UiRole.AccentText, r.Name, UiFlags.Label | UiFlags.Truncate);
            ty += UiTextRules.LineHeight(UiStyle.AppTitle) + UiTokens.S4;
            if (!string.IsNullOrEmpty(r.Sentence))
            {
                int id = UiDraw.Text(s, on, tx, ty, inner, UiStyle.Body, UiRole.Text2, r.Sentence, UiFlags.Wrap | UiFlags.Truncate, 3);
                ty = UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S4;
            }
            if (r.Figures.Count > 0)
            {
                float slot = UiDraw.SnapDown(inner / r.Figures.Count);
                float lh = UiTextRules.LineHeight(UiStyle.SectionHeader);
                for (int i = 0; i < r.Figures.Count; i++)
                {
                    UiDraw.Text(s, on, tx + i * slot, ty, slot - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text2, r.Figures[i].Key, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Text(s, on, tx + i * slot, ty + lh + UiTokens.S1, slot - UiTokens.S3, UiStyle.Number, UiRole.Text, r.Figures[i].Value, UiFlags.Truncate);
                }
                ty += lh + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Number) + UiTokens.S5;
            }
            if (amounts.Length > 0)
            {
                string label = r.Hint;
                UiDraw.Text(s, on, tx, ty, inner, UiStyle.SectionHeader, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
                ty += UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2;
                WorkAmount a = amounts[0];
                UiDraw.Stepper(s, on, tx, ty, Math.Min(inner, 320f), a.Figure, a.CanLess, a.CanMore, WorkCmd.Less, WorkCmd.Type, WorkCmd.More, 0, false);
                ty += UiTokens.Control + UiTokens.S5;
            }
            for (int i = 0; i < choices.Length; i++)
            {
                UiActionModel c = choices[i];
                int face2 = UiDraw.Box(s, on, tx, ty, inner, Choice, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = on.In(face2);
                float bw = Math.Min(inner * 0.55f, UiDraw.ButtonWidth(c.Kind, c.Label, c.Icon, c.Key, keys));
                UiDraw.Button(s, inside, tx + UiTokens.S3, ty + UiTokens.S2, bw, UiTokens.Control, c.Kind, c.Label, c.Icon, c.Key, keys, c.Command, c.Arg, c.Disabled);
                string note = i < acts.Count ? acts[i].Note : null;
                if (!string.IsNullOrEmpty(note))
                {
                    float nx = tx + UiTokens.S3 + bw + UiTokens.S3;
                    UiDraw.TextMiddle(s, inside, nx, ty, Choice, tx + inner - UiTokens.S3 - nx, UiStyle.Small, UiRole.Text2, note, UiFlags.Truncate);
                }
                ty += Choice + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(why))
            {
                UiDraw.Text(s, on, tx, ty, inner, UiStyle.Small, UiRole.WarnText, why, UiFlags.Truncate);
            }
        }

        private static float Height(WorkRow r, List<WorkAct> acts, float inner, string why)
        {
            float h = UiTokens.S8 + UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2 + UiTextRules.LineHeight(UiStyle.AppTitle) + UiTokens.S4;
            if (!string.IsNullOrEmpty(r.Sentence))
            {
                h += UiTextRules.WrappedHeight(r.Sentence, UiStyle.Body, inner, 3) + UiTokens.S4;
            }
            if (r.Figures.Count > 0)
            {
                h += UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Number) + UiTokens.S5;
            }
            h += UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2 + UiTokens.Control + UiTokens.S5;
            h += acts.Count * (Choice + UiTokens.S2) + (string.IsNullOrEmpty(why) ? 0f : UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S2) + UiTokens.S6;
            return UiDraw.Snap(Math.Max(h, UiTokens.AvatarHero + UiTokens.S12 * 4f));
        }
    }
}
