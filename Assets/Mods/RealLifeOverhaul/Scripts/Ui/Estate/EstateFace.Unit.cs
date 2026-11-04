using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class EstateFace
    {
        private const byte RegionNeed = 6, RegionTenant = 7;

        // One flat, after the mockup: its picture tile, rent and place, the next inspection, its facts, the tenant with the four things
        // they feel, the year's money; and at the right what needs the landlord, the rent record, the works and the building's other flats.
        // What does not fit under the window's foot is left out, the lowest first.
        private static void UnitPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            EstateUnitPage u = m.Unit;
            UiAt head = at.InRegion(RegionHead);
            y = TopBar(s, head, m.Back, new UiActionModel[0], x, y, w, keys);
            float lw = UiDraw.SnapDown(w * 0.44f), rx = x + lw + UiTokens.S6, rw = x + w - rx;
            // A flat's page is short, and the floor under it is the window's: each column spreads what it has over its own height by widening the gaps between
            // its sections (by at most 40 px each), so neither stands with a hole under it. The first pass measures into a scene of its own.
            var trial = new UiScene();
            float leftMore = 0f, rightMore = 0f;
            _breathe = 0f;
            _breaks = 0;
            float leftEnd = Left(trial, UiAt.Screen, UiAt.Screen, UiAt.Screen, u, x, y, lw, bottom, keys);
            int leftBreaks = _breaks;
            _breaks = 0;
            float rightEnd = Right(trial, UiAt.Screen, UiAt.Screen, u, rx, y, rw, bottom, keys);
            int rightBreaks = _breaks;
            if (leftBreaks > 0)
            {
                leftMore = Math.Max(0f, Math.Min(UiTokens.S10, UiDraw.SnapDown((bottom - UiTokens.S2 - leftEnd) / leftBreaks)));
            }
            if (rightBreaks > 0)
            {
                rightMore = Math.Max(0f, Math.Min(UiTokens.S10, UiDraw.SnapDown((bottom - UiTokens.S2 - rightEnd) / rightBreaks)));
            }
            _breathe = leftMore;
            Left(s, at.InRegion(RegionLeft), at.InRegion(RegionTenant), at.InRegion(RegionChart), u, x, y, lw, bottom, keys);
            _breathe = rightMore;
            Right(s, at.InRegion(RegionNeed), at.InRegion(RegionRight), u, rx, y, rw, bottom, keys);
            _breathe = 0f;
        }

        [ThreadStatic] private static float _breathe;   // the extra a gap between a column's sections takes, set by UnitPage for the pass being drawn
        [ThreadStatic] private static int _breaks;      // how many such gaps the pass met

        private static float Gap(float basic)
        {
            _breaks++;
            return basic + _breathe;
        }

        private static float Left(UiScene s, UiAt at, UiAt tenant, UiAt chart, EstateUnitPage u, float x, float y, float w, float bottom, IUiKeys keys)
        {
            // The unit's head: its use's tile, its address and what it rents for, the district and the way to find it. No empty banner: the game has no photo here.
            int head = UiDraw.Tile(s, at, x, y, TileBig, UiRole.None, false);
            UiDraw.Icon(s, at.In(head), x + (TileBig - UiTokens.S8) / 2f, y + (TileBig - UiTokens.S8) / 2f, UiTokens.S8, u.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + TileBig + UiTokens.S4, tw = w - TileBig - UiTokens.S4;
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float rentW = string.IsNullOrEmpty(u.Rent) ? 0f : UiDraw.Snap(UiTextRules.Width(u.Rent, UiStyle.Number) + UiTokens.S1);
            UiDraw.Text(s, at, tx, y + UiTokens.S1, tw - rentW - UiTokens.S3, UiStyle.Display, UiRole.Text, u.Title, UiFlags.Truncate | UiFlags.Middle);
            if (rentW > 0f)
            {
                UiDraw.Text(s, at, x + w - rentW, y, rentW, UiStyle.Number, UiRole.Text, u.Rent, UiFlags.Right | UiFlags.Middle);
                UiDraw.Text(s, at, x + w - rentW, y + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number)), rentW, UiStyle.Small, UiRole.Text2,
                    u.RentPer, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            }
            y += UiTokens.S1 + nh + UiTokens.S3;
            float locW = string.IsNullOrEmpty(u.Locate) ? 0f : Math.Min(UiDraw.SnapDown(tw * 0.5f), UiDraw.Snap(UiTextRules.Width(u.Locate, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
            float distW = UiDraw.Snap(UiTextRules.Width(u.District ?? string.Empty, UiStyle.Body) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, tx, y, UiTokens.PairRow, Math.Min(distW, tw - locW - UiTokens.S3), UiStyle.Body, UiRole.Text2, u.District, UiFlags.Truncate);
            if (locW > 0f)
            {
                UiDraw.LinkRow(s, at, tx + Math.Min(distW, tw - locW) + UiTokens.S3, y - UiDraw.SnapDown((UiTokens.Control - UiTokens.PairRow) / 2f), locW, UiTokens.Control,
                    u.Locate, ActionCommand, u.LocateRun);
            }
            y += UiTokens.PairRow + UiTokens.S4;
            if (!string.IsNullOrEmpty(u.NextLabel))
            {
                float lw = UiDraw.Snap(UiTextRules.Width(u.NextLabel, UiStyle.Body) + UiTokens.S2), vw = UiDraw.Snap(UiTextRules.Width(u.NextValue ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, lw, UiStyle.Body, UiRole.Text2, u.NextLabel, UiFlags.Label | UiFlags.Truncate);
                if (u.NextFill >= 0)
                {
                    UiDraw.Meter(s, at, x + lw + UiTokens.S2, y + (UiTokens.PairRow - UiTokens.S1) / 2f, w - lw - vw - UiTokens.S4, UiTokens.S1, u.NextFill, UiRole.Accent);
                }
                UiDraw.TextMiddle(s, at, x + w - vw, y, UiTokens.PairRow, vw, UiStyle.BodyStrong, UiRole.Text, u.NextValue, UiFlags.Right);
                y += UiTokens.PairRow + UiTokens.S1;
            }
            int facts = Math.Min(u.FactLabels.Length, u.FactValues.Length);
            if (facts > 0)
            {
                // The facts as pairs of a name and its value on a band of their own, two across, so none is a value standing as a name.
                float bandPad = UiTokens.S3, half = UiDraw.SnapDown((w - bandPad * 2f - UiTokens.S6) / 2f), bandH = ((facts + 1) / 2) * UiTokens.PairRow + bandPad * 2f;
                int band = UiDraw.Box(s, at, x, y, w, bandH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                for (int i = 0; i < facts; i++)
                {
                    float fx = x + bandPad + (i % 2) * (half + UiTokens.S6), fy = y + bandPad + (i / 2) * UiTokens.PairRow;
                    UiDraw.InfoRow(s, at.In(band), fx, fy, half, u.FactLabels[i], u.FactValues[i]);
                }
                y += bandH + Gap(UiTokens.S3);
            }
            if (u.Strip.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, u.Strip, x, y, w) - UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(u.TenantName) && y + UiTokens.S16 * 2f < bottom)
            {
                y = Tenant(s, tenant, u, x, y, w, bottom);
            }
            if (u.ShareLabels.Length > 0 && y + SharesHeight(u.ShareLabels, u.ShareValues, u.ShareTitle, w) <= bottom)
            {
                y = Shares(s, at, u.ShareTitle, u.ShareLabels, u.ShareValues, x, y, w);
            }
            if (u.Empty != null || u.Others.Length > 0)
            {
                if (u.Empty != null && u.Works.Length > 0 && y + RowHeight * 2f < bottom)
                {
                    y = Section(s, at, u.WorksTitle, u.WorksCount, x, y, w);
                    for (int i = 0; i < u.Works.Length && y + RowHeight <= bottom; i++)
                    {
                        Row(s, at, u.Works[i], x, y, w);
                        y += RowHeight;
                    }
                    y += Gap(UiTokens.S3);
                }
                if (u.Others.Length > 0 && y + RowHeight * 2f < bottom)
                {
                    y = Section(s, at, u.OthersTitle, u.OthersCount, x, y, w);
                    for (int i = 0; i < u.Others.Length && y + RowHeight <= bottom; i++)
                    {
                        Row(s, at, u.Others[i], x, y, w);
                        y += RowHeight;
                    }
                    y += Gap(UiTokens.S3);
                }
            }
            if (u.YearLabels.Length > 0 && y + UiTokens.PairRow * (u.YearLabels.Length + 2) <= bottom)
            {
                UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text2, u.YearTitle, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                for (int i = 0; i < u.YearLabels.Length && i < u.YearValues.Length; i++)
                {
                    y += UiDraw.InfoRow(s, at, x, y, w, u.YearLabels[i], u.YearValues[i]);
                }
                UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                y += UiTokens.S1;
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w * 0.5f, UiStyle.BodyStrong, UiRole.Text, u.YearKeptLabel, UiFlags.Label | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + w * 0.5f, y, UiTokens.PairRow, w * 0.5f, UiStyle.BodyStrong, UiRole.OkText, u.YearKept, UiFlags.Right | UiFlags.Truncate);
                y += UiTokens.PairRow + UiTokens.S3;
            }
            return y;
        }

        private static float Tenant(UiScene s, UiAt at, EstateUnitPage u, float x, float y, float w, float bottom)
        {
            float hh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float olw = string.IsNullOrEmpty(u.TenantOpen) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(u.TenantOpen, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
            UiDraw.Text(s, at, x, y, w - olw - (olw > 0f ? UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text2, u.TenantTitle, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            if (olw > 0f)
            {
                UiDraw.LinkRow(s, at, x + w - olw, y - UiDraw.SnapDown((UiTokens.Control - hh) / 2f), olw, UiTokens.Control, u.TenantOpen, ActionCommand, u.TenantOpenRun);
            }
            y += hh + UiTokens.S3;
            float face = UiTokens.AvatarHero;
            UiDraw.Portrait(s, at, x, y, face, u.TenantName, u.TenantHue, u.TenantRing);
            float tx = x + face + UiTokens.S4;
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number));
            float chip = string.IsNullOrEmpty(u.TenantChip) ? 0f : UiFactCard.ChipWidth(u.TenantChip);
            float nameW = Math.Min(x + w - tx - (chip > 0f ? chip + UiTokens.S3 : 0f), UiDraw.Snap(UiTextRules.Width(u.TenantName, UiStyle.Number) + UiTokens.S1));
            UiDraw.Text(s, at, tx, y + UiTokens.S1, nameW, UiStyle.Number, UiRole.Text, u.TenantName, UiFlags.Truncate | UiFlags.Middle);
            if (chip > 0f)
            {
                UiDraw.Chip(s, at, tx + nameW + UiTokens.S3, y + UiTokens.S1 + UiDraw.SnapDown((nameH - UiTokens.Badge - UiTokens.S1) / 2f), u.TenantChip, u.TenantChipTone, chip);
            }
            UiDraw.Text(s, at, tx, y + UiTokens.S1 + nameH + UiTokens.S1, x + w - tx, UiStyle.Body, UiRole.Text2, u.TenantLine, UiFlags.Truncate | UiFlags.Middle);
            y += face + UiTokens.S3;
            int n = Math.Min(u.MeterLabels.Length, u.MeterValues.Length);
            float lw2 = UiDraw.LabelColumn(w, u.MeterLabels);
            for (int i = 0; i < n && y + UiTokens.PairRow <= bottom; i++)
            {
                UiDraw.GradeBar(s, at, x, y, w, u.MeterLabels[i], u.MeterValues[i], lw2, "%");
                y += UiTokens.PairRow;
            }
            return y + Gap(UiTokens.S3);
        }

        private static float Right(UiScene s, UiAt need, UiAt at, EstateUnitPage u, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (u.Empty != null)
            {
                y += UiFactCard.Lay(s, at, u.Empty, x, y, w, bottom - y, keys, true) + Gap(UiTokens.S3);
            }
            else if (!string.IsNullOrEmpty(u.NeedHead))
            {
                y = Need(s, need, u, x, y, w, keys) + Gap(UiTokens.S4);
            }
            if (u.Applicants.Length > 0 && y + RowHeight * 2f < bottom)
            {
                y = Section(s, at, u.ApplicantsTitle, u.ApplicantsCount, x, y, w);
                for (int i = 0; i < u.Applicants.Length && y + RowHeight <= bottom; i++)
                {
                    Row(s, at, u.Applicants[i], x, y, w);
                    y += RowHeight;
                }
                y += Gap(UiTokens.S3);
            }
            if (u.Record.Length > 0 && y + UiTokens.PairRow * 3 < bottom)
            {
                y = Section(s, at, u.RecordTitle, u.RecordNote, x, y, w);
                for (int i = 0; i < u.Record.Length && y + UiTokens.S10 <= bottom - UiTokens.S16; i++)
                {
                    y = RecordLine(s, at, u.Record[i], x, y, w);
                }
                y += Gap(UiTokens.S3);
            }
            if (u.Empty == null && u.Works.Length > 0 && y + RowHeight * 2f < bottom)
            {
                y = Section(s, at, u.WorksTitle, u.WorksCount, x, y, w);
                for (int i = 0; i < u.Works.Length && y + RowHeight <= bottom; i++)
                {
                    Row(s, at, u.Works[i], x, y, w);
                    y += RowHeight;
                }
                y += Gap(UiTokens.S2);
            }
            if (!string.IsNullOrEmpty(u.BuildingLink) && y + UiTokens.Control <= bottom)
            {
                UiDraw.LinkRow(s, at, x, y, w, UiTokens.Control, u.BuildingLink, ActionCommand, u.BuildingRun);
                y += UiTokens.Control;
            }
            return y;
        }

        // What needs the landlord: a kicker in capitals, the headline, what to know, the buttons and a quiet link at the right.
        private static float Need(UiScene s, UiAt at, EstateUnitPage u, float x, float y, float w, IUiKeys keys)
        {
            float pad = UiTokens.S4, iw = w - pad * 2f;
            float kick = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), head = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float body = string.IsNullOrEmpty(u.NeedBody) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(u.NeedBody, UiStyle.Body, iw, 3)) + UiTokens.S1;
            float buttons = u.NeedActions.Length > 0 ? UiTokens.S3 + UiTokens.Control : 0f;
            float h = UiDraw.Snap(pad + kick + UiTokens.S1 + head + UiTokens.S1 + body + buttons + pad);
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(box);
            float cy = y + pad;
            UiDraw.Text(s, on, x + pad, cy, iw, UiStyle.SectionHeader, UiRole.Text2, u.NeedKicker, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            cy += kick + UiTokens.S1;
            UiDraw.Text(s, on, x + pad, cy, iw, UiStyle.Title, UiRole.Text, u.NeedHead, UiFlags.Truncate | UiFlags.Middle);
            cy += head + UiTokens.S1;
            if (body > 0f)
            {
                UiDraw.Text(s, on, x + pad, cy, iw, UiStyle.Body, UiRole.Text2, u.NeedBody, UiFlags.Label | UiFlags.Wrap, 3);
                cy += body;
            }
            if (u.NeedActions.Length > 0)
            {
                cy += UiTokens.S3;
                float bx = x + pad;
                float linkW = string.IsNullOrEmpty(u.NeedLink) ? 0f : UiDraw.ButtonWidth(UiButtonKind.Destructive, u.NeedLink, UiIcon.None, UiAction.Count, keys) + UiTokens.S3;
                for (int i = 0; i < u.NeedActions.Length; i++)
                {
                    ref UiActionModel a = ref u.NeedActions[i];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    if (bx + bw > x + w - pad - linkW)
                    {
                        break;
                    }
                    UiDraw.Button(s, on, bx, cy, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                    bx += bw + UiTokens.S3;
                }
                if (linkW > 0f)
                {
                    // The one thing here that cannot be taken back is a button of its own look, never a word among the others.
                    UiDraw.Button(s, on, x + w - pad - linkW + UiTokens.S3, cy, linkW - UiTokens.S3, UiTokens.Control, UiButtonKind.Destructive,
                        u.NeedLink, UiIcon.None, UiAction.Count, keys, ActionCommand, u.NeedLinkRun, false);
                }
            }
            return y + h;
        }

        // A section's label in capitals with a note or a count at the right. Returns the y under it.
        private static float Section(UiScene s, UiAt at, string title, string note, float x, float y, float w)
        {
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float nw = string.IsNullOrEmpty(note) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.4f), UiDraw.Snap(UiTextRules.Width(note, UiStyle.Small) + UiTokens.S1));
            UiDraw.Text(s, at, x, y, w - nw - (nw > 0f ? UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            if (nw > 0f)
            {
                UiDraw.Text(s, at, x + w - nw, y, nw, UiStyle.Small, UiRole.Muted, note, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            }
            return y + lh + UiTokens.S2;
        }

        // A month of the record: its due day, how it ended, its chip and its sum. A hairline between the lines.
        private static float RecordLine(UiScene s, UiAt at, EstateRecordRow r, float x, float y, float w)
        {
            float h = UiTokens.S10;
            float dueW = UiDraw.SnapDown(w * 0.22f), amountW = UiDraw.SnapDown(w * 0.16f), chip = string.IsNullOrEmpty(r.Chip) ? 0f : UiFactCard.ChipWidth(r.Chip);
            UiDraw.TextMiddle(s, at, x, y, h, dueW, UiStyle.Small, UiRole.Muted, r.Due, UiFlags.Truncate);
            float right = x + w;
            UiDraw.TextMiddle(s, at, right - amountW, y, h, amountW, UiStyle.BodyStrong, UiRole.Text, r.Amount, UiFlags.Right | UiFlags.Truncate);
            right -= amountW + UiTokens.S3;
            if (chip > 0f)
            {
                UiDraw.Chip(s, at, right - chip, y + UiDraw.SnapDown((h - UiTokens.Badge - UiTokens.S1) / 2f), r.Chip, r.ChipTone, chip);
                right -= chip + UiTokens.S3;
            }
            UiDraw.TextMiddle(s, at, x + dueW, y, h, Math.Max(UiTokens.S8, right - x - dueW), UiStyle.Body, UiRole.Text2, r.What, UiFlags.Truncate);
            UiDraw.Box(s, at, x, y + h, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            return y + h;
        }
    }
}
