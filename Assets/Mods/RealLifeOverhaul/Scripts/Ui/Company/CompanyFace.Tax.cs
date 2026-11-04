using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        private const float LicenceRow = 56f, LineRow = 48f, StatementRow = 28f;

        // Tax and licences: what needs the owner first (the tax set aside), then the licences with who handles each and what to do, and what is coming up; at the right
        // the quarter so far, the returns paid, the tools (the return, the accountant, cash off the books) and the property tax. A sub page is a card in the whole room.
        private static void TaxPage(UiScene s, UiAt at, CompanyFacts f, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            CompanyTaxFacts t = f.Tax;
            if (t.PartOff)
            {
                OffPage(s, at, w, t.OffTitle, t.OffBody, x, y, cw, bottom, keys);
                return;
            }
            if (v.Sub[CompanyView.Tax] != CompanyView.SubMain && t.Card != null)
            {
                // A licence, the accountant, the return or cash off the books opens at the right of the licences: the list stays where it was and the card is its detail.
                float gapD = UiTokens.S6;
                if (cw >= 1000f && t.Licences.Length > 0)
                {
                    float lwD = UiDraw.SnapDown((cw - gapD) * 0.44f), rwD = cw - gapD - lwD;
                    Licences(s, at.InRegion(RegionMain), lay, v, w, t, x, y, lwD, bottom, keys, true);
                    CardPage(s, at.InRegion(RegionSide), t.Card, v, w, lay, x + lwD + gapD, y, rwD, bottom, keys);
                    return;
                }
                CardPage(s, at, t.Card, v, w, lay, x, y, cw, bottom, keys);
                return;
            }
            float cy = y;
            if (t.HasLead)
            {
                cy += TaxLead(s, at.InRegion(RegionMain), lay, v, t, x, cy, cw, keys) + UiTokens.S5;
            }
            float gap = UiTokens.S6;
            float lw = UiDraw.SnapDown((cw - gap) * 0.6f), rw = cw - gap - lw;
            bool paidLeft = Licences(s, at.InRegion(RegionMain), lay, v, w, t, x, cy, lw, bottom, keys);
            Statement(s, at.InRegion(RegionSide), lay, v, w, t, x + lw + gap, cy, rw, bottom, keys, paidLeft);
        }

        private static float TaxLead(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyTaxFacts t, float x, float y, float cw, IUiKeys keys)
        {
            float pad = UiTokens.S5, face = 56f;
            float bw = 0f;
            for (int i = 0; i < t.LeadButtons.Length; i++)
            {
                bw += UiDraw.ButtonWidth(t.LeadButtons[i].Kind, t.LeadButtons[i].Label, t.LeadButtons[i].Icon, t.LeadButtons[i].Key, keys) + (i > 0 ? UiTokens.S3 : 0f);
            }
            bw = Math.Min(bw, UiDraw.SnapDown(cw * 0.45f));
            float tx = x + pad + face + UiTokens.S4, tw = cw - pad * 2f - face - UiTokens.S4 - bw - UiTokens.S5;
            float kh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), th = UiDraw.Snap(UiTextRules.WrappedHeight(t.LeadTitle ?? string.Empty, UiStyle.Title, tw, 2));
            float bh = string.IsNullOrEmpty(t.LeadBody) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(t.LeadBody, UiStyle.Body, tw, 3));
            float h = pad * 2f + Math.Max(face, kh + UiTokens.S1 + th + (bh > 0f ? UiTokens.S1 + bh : 0f));
            int card = UiDraw.Box(s, at, x, y, cw, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt inside = at.In(card);
            UiDraw.Portrait(s, inside, x + pad, y + Mid(h, face), face, t.LeadAsker ?? string.Empty, t.LeadHue, UiRole.Warn);
            float cy = y + pad;
            UiDraw.TextMiddle(s, inside, tx, cy, kh, tw, UiStyle.SectionHeader, UiRole.Text2, t.LeadKicker ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            cy += kh + UiTokens.S1;
            UiDraw.Text(s, inside, tx, cy, tw, UiStyle.Title, UiRole.Text, t.LeadTitle ?? string.Empty, UiFlags.Wrap | UiFlags.Truncate, 2);
            cy += th;
            if (bh > 0f)
            {
                UiDraw.Text(s, inside, tx, cy + UiTokens.S1, tw, UiStyle.Body, UiRole.Text2, t.LeadBody, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
            float bx = x + cw - pad - bw;
            for (int i = 0; i < t.LeadButtons.Length; i++)
            {
                CompanyButton b = t.LeadButtons[i];
                float w1 = UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys);
                if (bx + w1 > x + cw - pad)
                {
                    break;
                }
                Button(s, inside, lay, v, ref b, bx, y + Mid(h, UiTokens.Control), w1, keys);
                bx += w1 + UiTokens.S3;
            }
            return h;
        }

        // The licences: what, where, who handles it, how it stands, when, and the button; then what is coming up, and the returns paid when the column has the room (true when drawn here).
        private static bool Licences(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyTaxFacts t, float x, float y, float width, float bottom, IUiKeys keys,
            bool compact = false)
        {
            float cy = Section(s, at, lay, x, y, width, t.LicencesTitle, t.LicencesCount);
            int comingNeed = t.ComingUp.Length > 0 ? (int)(UiTokens.PairRow + UiTokens.S1 + Math.Min(t.ComingUp.Length, 3) * DoneRow + UiTokens.S4) : 0;
            int fit = (int)Math.Max(1f, Math.Floor((bottom - cy - comingNeed) / LicenceRow));
            if (t.Licences.Length == 0)
            {
                cy = Sentence(s, at, t.LicencesEmpty, x, cy, width, UiRole.Text2, 3);
            }
            // Beside a card (compact) a row is the licence, where, and how it stands: the day and the button are on the card.
            bool showManager = !compact && width >= 640f;
            float managerW = showManager ? Math.Min(UiDraw.SnapDown(width * 0.2f), 168f) : 0f;
            float stateW = Math.Min(UiDraw.SnapDown(width * (compact ? 0.3f : 0.2f)), 176f), dayW = compact ? 0f : 76f;
            int rows = Math.Min(t.Licences.Length, fit);
            // The button's column is as wide as the widest button of the rows in view, so that the manager, the state and the day stand in the same place in every row.
            float actCol = 0f;
            for (int i = 0; i < rows; i++)
            {
                if (!compact && t.Licences[i].HasAction)
                {
                    CompanyButton a = t.Licences[i].Action;
                    actCol = Math.Max(actCol, Math.Min(UiDraw.SnapDown(width * 0.2f), UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys)));
                }
            }
            for (int i = 0; i < rows; i++)
            {
                ref CompanyLicence l = ref t.Licences[i];
                float actW = 0f;
                if (!compact && l.HasAction)
                {
                    actW = Math.Min(UiDraw.SnapDown(width * 0.2f), UiDraw.ButtonWidth(l.Action.Kind, l.Action.Label, l.Action.Icon, l.Action.Key, keys));
                }
                bool open = compact && string.Equals(l.Id, v.Picked, StringComparison.Ordinal);
                int face = UiDraw.Box(s, at, x, cy, width, LicenceRow, open ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, open ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                float size = UiTokens.IconTile;
                int tile = UiDraw.Tile(s, inside, x + UiTokens.S1, cy + Mid(LicenceRow, size), size, UiRole.None, false);
                float g = UiTokens.S6;
                UiDraw.Icon(s, inside.In(tile), x + UiTokens.S1 + (size - g) / 2f, cy + (LicenceRow - g) / 2f, g, l.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                float right = x + width - UiTokens.S2;
                float rx = right - actCol - (actCol > 0f ? UiTokens.S3 : 0f) - dayW;
                float sx = rx - UiTokens.S2 - stateW;
                float mx = sx - UiTokens.S3 - managerW;
                float tx = x + UiTokens.S1 + size + UiTokens.S3, tw = (showManager ? mx : sx) - UiTokens.S3 - tx;
                float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                float top = cy + UiDraw.SnapDown((LicenceRow - th - sh) / 2f);
                UiDraw.TextMiddle(s, inside, tx, top, th, tw, UiStyle.Title, UiRole.Text, l.Name ?? string.Empty, UiFlags.Truncate);
                UiDraw.TextMiddle(s, inside, tx, top + th, sh, tw, UiStyle.Small, open ? UiRole.Text : UiRole.Text2, l.Place ?? string.Empty, UiFlags.Truncate);
                if (showManager && !string.IsNullOrEmpty(l.Manager))
                {
                    float ps = UiTokens.AvatarSmall - UiTokens.S2;
                    UiDraw.Portrait(s, inside, mx, cy + Mid(LicenceRow, ps), ps, l.Manager, l.ManagerHue, UiRole.None);
                    UiDraw.TextMiddle(s, inside, mx + ps + UiTokens.S2, cy, LicenceRow, managerW - ps - UiTokens.S2, UiStyle.Small, UiRole.Text2, l.Manager, UiFlags.Truncate);
                }
                UiDraw.Chip(s, inside, sx, cy + (LicenceRow - UiTokens.Badge - UiTokens.S1) / 2f, l.State ?? string.Empty, l.StateTone, stateW);
                if (dayW > 0f)
                {
                    UiDraw.TextMiddle(s, inside, rx, cy, LicenceRow, dayW, UiStyle.Small, UiRole.Text2, l.Day ?? string.Empty, UiFlags.Right | UiFlags.Truncate);
                }
                if (l.Open.Do != CompanyDo.None)
                {
                    int index = lay.Add(new CompanyButton { Label = l.Name, Act = l.Open });
                    UiDraw.Node(s, at, x, cy, width - actCol - (actCol > 0f ? UiTokens.S4 : 0f), LicenceRow, Act, index, face);
                }
                if (actW > 0f)
                {
                    CompanyButton b = l.Action;
                    Button(s, inside, lay, v, ref b, right - actW, cy + Mid(LicenceRow, UiTokens.Control), actW, keys);
                }
                cy += LicenceRow;
            }
            if (rows < t.Licences.Length)
            {
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, width, UiStyle.Small, UiRole.Muted, w.F("reallife_co_and_more", "and {count} more", "count", (t.Licences.Length - rows).ToString()),
                    UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            cy += UiTokens.S4;
            if (t.ComingUp.Length > 0 && bottom - cy >= UiTokens.PairRow + UiTokens.S1 + DoneRow)
            {
                cy = Section(s, at, lay, x, cy, width, w.T("reallife_co_coming_up", "Coming up"));
                int n = (int)Math.Max(1f, Math.Floor((bottom - cy) / DoneRow));
                for (int i = 0; i < Math.Min(t.ComingUp.Length, n); i++)
                {
                    cy += DoneLine(s, at, lay, ref t.ComingUp[i], x, cy, width, DoneRow);
                }
                cy += UiTokens.S4;
            }
            cy = PaidList(s, at, lay, t, x, cy, width, bottom, out bool drawn);
            if (t.Extra.Length > 0 && bottom - cy >= UiTokens.PairRow + LineRow)
            {
                cy = Section(s, at, lay, x, cy, width, t.ExtraTitle);
                for (int i = 0; i < t.Extra.Length && cy + LineRow <= bottom; i++)
                {
                    cy += LineItem(s, at, lay, ref t.Extra[i], x, cy, width);
                }
            }
            return drawn;
        }

        // The returns paid this year (or the sentence that none is settled yet), when they fit under what is already in the column: the y after them, or y when they do not fit.
        private static float PaidList(UiScene s, UiAt at, CompanyLayout lay, CompanyTaxFacts t, float x, float y, float width, float bottom, out bool drawn)
        {
            float cy = y;
            drawn = false;
            if (t.Paid.Length > 0 && bottom - cy >= UiTokens.PairRow + LineRow * t.Paid.Length)
            {
                cy = Section(s, at, lay, x, cy, width, t.PaidTitle);
                for (int i = 0; i < t.Paid.Length && cy + LineRow <= bottom; i++)
                {
                    cy += LineItem(s, at, lay, ref t.Paid[i], x, cy, width);
                }
                drawn = true;
                return cy + UiTokens.S3;
            }
            if (t.Paid.Length == 0 && !string.IsNullOrEmpty(t.PaidEmpty) && bottom - cy >= UiTokens.PairRow * 2f)
            {
                cy = Section(s, at, lay, x, cy, width, t.PaidTitle);
                drawn = true;
                return Sentence(s, at, t.PaidEmpty, x, cy, width, UiRole.Muted, 2) + UiTokens.S3;
            }
            return y;
        }

        // The right column: the quarter so far, the reserve's controls, the returns paid, the tools, the property tax.
        private static void Statement(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyWords w, CompanyTaxFacts t, float x, float y, float width, float bottom, IUiKeys keys, bool paidLeft)
        {
            float cy = Section(s, at, lay, x, y, width, t.QuarterTitle, t.QuarterRange);
            for (int i = 0; i < t.Quarter.Length && cy + StatementRow <= bottom; i++)
            {
                ref CompanyStatementLine q = ref t.Quarter[i];
                Pair(s, at, x, cy, StatementRow, width, q.Strong ? UiStyle.BodyStrong : UiStyle.Body, q.Strong ? UiRole.Text : UiRole.Text2, q.Label, q.Strong ? UiStyle.BodyStrong : UiStyle.Body,
                    q.Tone == UiRole.None ? UiRole.Text : q.Tone, q.Value);
                cy += StatementRow;
            }
            if (t.QuarterLen > 0 && cy + UiTokens.PairRow + UiTokens.S3 <= bottom)
            {
                // How far through the quarter it is: the figures above are of this much of it.
                Pair(s, at, x, cy, UiTokens.PairRow, width, UiStyle.Body, UiRole.Text2, t.QuarterDayText ?? string.Empty, UiStyle.Body, UiRole.Text, null);
                UiDraw.Meter(s, at, x, cy + UiTokens.PairRow + UiTokens.S1, width, UiTokens.S1, Math.Max(0, Math.Min(100, t.QuarterDay * 100 / Math.Max(1, t.QuarterLen))), UiRole.Accent);
                cy += UiTokens.PairRow + UiTokens.S3 + UiTokens.S2;
            }
            for (int i = 0; i < t.ReserveControls.Length && cy + SwitchRow <= bottom; i++)
            {
                cy = Block(s, at, lay, v, w, t.ReserveControls[i], x, cy, width, bottom, keys);
            }
            cy += UiTokens.S3;
            if (!paidLeft)
            {
                cy = PaidList(s, at, lay, t, x, cy, width, bottom, out bool _);
            }
            if (t.Tools.Length > 0 && bottom - cy >= UiTokens.PairRow + LineRow)
            {
                cy = Section(s, at, lay, x, cy, width, t.ToolsTitle);
                for (int i = 0; i < t.Tools.Length && cy + LineRow <= bottom; i++)
                {
                    cy += LineItem(s, at, lay, ref t.Tools[i], x, cy, width);
                }
                cy += UiTokens.S3;
            }
            if (t.Terms.Length > 0 && bottom - cy >= UiTokens.PairRow + LineRow)
            {
                cy = Section(s, at, lay, x, cy, width, t.TermsTitle);
                for (int i = 0; i < t.Terms.Length && cy + LineRow <= bottom; i++)
                {
                    cy += LineItem(s, at, lay, ref t.Terms[i], x, cy, width);
                }
                cy += UiTokens.S3;
            }
            if (t.Property.Length > 0 && bottom - cy >= UiTokens.PairRow + StatementRow)
            {
                cy = Section(s, at, lay, x, cy, width, t.PropertyTitle);
                for (int i = 0; i < t.Property.Length && cy + StatementRow <= bottom; i++)
                {
                    Pair(s, at, x, cy, StatementRow, width, UiStyle.Body, UiRole.Text2, t.Property[i].Label, UiStyle.Body, UiRole.Text, t.Property[i].Value);
                    cy += StatementRow;
                }
            }
            if (!string.IsNullOrEmpty(t.PropertyNote) && bottom - cy >= UiTokens.S8)
            {
                Sentence(s, at, t.PropertyNote, x, cy + UiTokens.S1, width, UiRole.Text2, 3, UiStyle.Small);
            }
        }

        // A compact line: its words at the left, a chip, its figure at the right; a target when it opens something.
        private static float LineItem(UiScene s, UiAt at, CompanyLayout lay, ref CompanyItem it, float x, float y, float width)
        {
            bool press = it.Press && it.Act.Do != CompanyDo.None;
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, width + UiTokens.S4, LineRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float fw = string.IsNullOrEmpty(it.Figure) ? 0f : Math.Min(UiDraw.SnapDown(width * 0.4f), UiDraw.Snap(UiTextRules.Width(it.Figure, UiStyle.BodyStrong) + UiTokens.S1));
            float cw2 = string.IsNullOrEmpty(it.Chip) ? 0f : Math.Min(UiDraw.SnapDown(width * 0.34f), UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(it.Chip,
                UiStyle.SmallStrong) + UiTokens.S1));
            float tw = width - fw - (fw > 0f ? UiTokens.S3 : 0f) - cw2 - (cw2 > 0f ? UiTokens.S3 : 0f);
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            bool two = !string.IsNullOrEmpty(it.Sub);
            float top = y + UiDraw.SnapDown((LineRow - th - (two ? sh : 0f)) / 2f);
            UiDraw.TextMiddle(s, inside, x, top, th, tw, UiStyle.Body, UiRole.Text, it.Title ?? string.Empty, UiFlags.Truncate);
            if (two)
            {
                UiDraw.TextMiddle(s, inside, x, top + th, sh, tw, UiStyle.Small, UiRole.Text2, it.Sub, UiFlags.Truncate);
            }
            if (cw2 > 0f)
            {
                UiDraw.Chip(s, inside, x + tw + UiTokens.S3, y + (LineRow - UiTokens.Badge - UiTokens.S1) / 2f, it.Chip, it.ChipTone, cw2);
            }
            if (fw > 0f)
            {
                UiDraw.TextMiddle(s, inside, x + width - fw, y, LineRow, fw, UiStyle.BodyStrong, it.FigureTone == UiRole.None ? UiRole.Text : it.FigureTone, it.Figure,
                    UiFlags.Right | UiFlags.Truncate);
            }
            if (press)
            {
                int index = lay.Add(new CompanyButton { Label = it.Title, Act = it.Act });
                UiDraw.Node(s, at, x - UiTokens.S2, y, width + UiTokens.S4, LineRow, Item, index, face);
            }
            return LineRow;
        }
    }
}
