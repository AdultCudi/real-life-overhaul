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
        private const float Side = 320f, Choice = 48f;

        public static void Lay(UiScene s, UiAt at, WorkRow r, List<WorkAct> acts, UiActionModel[] choices, WorkAmount[] amounts, string why, float x, float y, float w, float bottom, IUiKeys keys)
        {
            // the card is as high as what it says: the seller at the left, the offer at the right, the choices at the foot; the seller's other things stand under it
            float cw = w, cx = x, cy = y;
            float inner = cw - Side - UiTokens.S8 * 2f;
            float ch = Math.Min(bottom - y, Need(r, choices.Length, amounts.Length > 0, why, inner));
            int card = UiDraw.Box(s, at, cx, cy, cw, ch, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Decor);
            UiAt on = at.In(card);
            UiDraw.Box(s, on, cx, cy, Side, ch, UiRole.Band, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor);
            // the seller at the left
            float face = UiTokens.AvatarHero;
            string seller = r.Seller ?? r.Person ?? string.Empty;
            UiDraw.Portrait(s, on, cx + UiDraw.SnapDown((Side - face) / 2f), cy + UiTokens.S8, face, seller, UiColour.HueOf(seller), UiRole.None);
            float ny = cy + UiTokens.S8 + face + UiTokens.S4;
            UiDraw.Text(s, on, cx + UiTokens.S4, ny, Side - UiTokens.S4 * 2f, UiStyle.Title, UiRole.Text, seller, UiFlags.Centre | UiFlags.Truncate);
            ny += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S1;
            UiDraw.Text(s, on, cx + UiTokens.S4, ny, Side - UiTokens.S4 * 2f, UiStyle.Small, UiRole.Text2, r.Line, UiFlags.Centre | UiFlags.Truncate);
            ny += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S3;
            if (!string.IsNullOrEmpty(r.Status))
            {
                float chip = Math.Min(Side - UiTokens.S4 * 2f, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Status, UiStyle.SmallStrong) + UiTokens.S1));
                UiDraw.Chip(s, on, cx + UiDraw.SnapDown((Side - chip) / 2f), ny, r.Status, r.Tone, chip);
                ny += UiTokens.Badge + UiTokens.S1;
            }
            // what is known of the seller: their stars, what they have sold and what else they list
            ny += UiTokens.S3;
            for (int i = 0; i < r.Sections.Count && ny + WorkPanel.Height(r.Sections[i], Side - UiTokens.S4 * 2f) <= cy + ch - UiTokens.S3; i++)
            {
                ny += WorkPanel.Section(s, on, r.Sections[i], cx + UiTokens.S4, ny, Side - UiTokens.S4 * 2f, amounts, keys) + UiTokens.S2;
            }
            // the card's words at the right
            float tx = UiDraw.Snap(cx + Side + UiTokens.S8), ty = cy + UiTokens.S8;
            UiDraw.Text(s, on, tx, ty, inner, UiStyle.SectionHeader, UiRole.Text2, r.Kicker, UiFlags.Label | UiFlags.Truncate);
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            UiDraw.Text(s, on, tx, ty, inner, UiStyle.Hero, UiRole.Text, r.Name, UiFlags.Truncate);
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)) + UiTokens.S4;
            if (!string.IsNullOrEmpty(r.Sentence))
            {
                int id = UiDraw.Text(s, on, tx, ty, inner, UiStyle.Body, UiRole.Text2, r.Sentence, UiFlags.Wrap | UiFlags.Truncate, 3);
                ty = UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S4;
            }
            if (r.Figures.Count > 0)
            {
                float slot = UiDraw.SnapDown(inner / r.Figures.Count);
                float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
                for (int i = 0; i < r.Figures.Count; i++)
                {
                    UiDraw.Text(s, on, tx + i * slot, ty, slot - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text2, r.Figures[i].Key, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Text(s, on, tx + i * slot, ty + lh + UiTokens.S1, slot - UiTokens.S3, UiStyle.Number, UiRole.Text, r.Figures[i].Value, UiFlags.Truncate);
                }
                ty += lh + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number)) + UiTokens.S5;
            }
            if (amounts.Length > 0)
            {
                string label = r.Hint;
                UiDraw.Text(s, on, tx, ty, inner, UiStyle.SectionHeader, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                WorkAmount a = amounts[0];
                UiDraw.Stepper(s, on, tx, ty, UiDraw.SnapDown(Math.Min(inner, 320f)), a.Figure, a.CanLess, a.CanMore, WorkCmd.Less, WorkCmd.Type, WorkCmd.More, 0, false);
                ty += UiTokens.Control + UiTokens.S5;
            }
            // the choices follow the offer: a button and what choosing it does, in a column of one width
            float bw = 0f;
            for (int i = 0; i < choices.Length; i++)
            {
                bw = Math.Max(bw, UiDraw.ButtonWidth(choices[i].Kind, choices[i].Label, choices[i].Icon, choices[i].Key, keys));
            }
            bw = Math.Min(inner * 0.5f, bw);
            for (int i = 0; i < choices.Length; i++)
            {
                UiActionModel c = choices[i];
                UiDraw.Button(s, on, tx, ty, bw, UiTokens.Control, c.Kind, c.Label, c.Icon, c.Key, keys, c.Command, c.Arg, c.Disabled);
                string note = i < acts.Count ? acts[i].Note : null;
                if (!string.IsNullOrEmpty(note))
                {
                    float nx = tx + bw + UiTokens.S4;
                    UiDraw.TextMiddle(s, on, nx, ty, UiTokens.Control, tx + inner - nx, UiStyle.Small, UiRole.Text2, note, UiFlags.Truncate);
                }
                ty += Choice + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(why))
            {
                UiDraw.Text(s, on, tx, ty, inner, UiStyle.Small, UiRole.WarnText, why, UiFlags.Truncate);
            }
            WorkForm.Strip(s, at, r.Bands, x, cy + ch + UiTokens.S4, w, bottom, keys);
        }

        // The card's height: the same steps the layout takes, down to the last choice and a line for what stops one.
        private static float Need(WorkRow r, int choices, bool offer, string why, float inner)
        {
            float h = UiTokens.S8;
            h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)) + UiTokens.S4;
            if (!string.IsNullOrEmpty(r.Sentence))
            {
                h += UiTextRules.WrappedHeight(r.Sentence, UiStyle.Body, inner, 3) + UiTokens.S4;
            }
            if (r.Figures.Count > 0)
            {
                h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number)) + UiTokens.S5;
            }
            if (offer)
            {
                h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiTokens.Control + UiTokens.S5;
            }
            h += choices * (Choice + UiTokens.S2);
            h += string.IsNullOrEmpty(why) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float side = UiTokens.S8 + UiTokens.AvatarHero + UiTokens.S4 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small))
                + UiTokens.S3 + UiTokens.Badge + UiTokens.S8;
            for (int i = 0; i < r.Sections.Count; i++)
            {
                side += WorkPanel.Height(r.Sections[i], Side - UiTokens.S4 * 2f) + UiTokens.S2;
            }
            return UiDraw.Snap(Math.Max(side, h + UiTokens.S6));
        }
    }
}
