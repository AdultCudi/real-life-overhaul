using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A requirement of a post, a course or a deal, met or not.</summary>
    public struct SplitNeed
    {
        public string Words;
        public bool Met;
    }

    /// <summary>What the split page shows beside its table: the chosen row's name, state, sentence, facts, requirements and people. Game-free.</summary>
    public sealed class SplitDetail
    {
        public UiIcon Icon = UiIcon.Briefcase;
        public string Title, Status, Sentence, NeedsTitle, PeopleTitle, PeopleCount;
        public UiRole Tone = UiRole.Ok;
        public readonly List<KeyValuePair<string, string>> Facts = new List<KeyValuePair<string, string>>(6);
        public readonly List<SplitNeed> Needs = new List<SplitNeed>(6);
        public UiPersonRow[] People = new UiPersonRow[0];
    }

    /// <summary>
    /// The page the mockups give a list of things to choose from: the filter chips above, the table at the left and the chosen row's detail
    /// beside it, the facts as rows, the requirements as check lines, the people it names with their faces, and its buttons at the foot.
    /// Every part is the kit's own. Game-free.
    /// </summary>
    public static class WorkSplit
    {
        private const float DetailShare = 0.36f;

        public static void Lay(UiScene s, UiAt at, UiAppModel m, SplitDetail d, int chosen, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float cx = x;
            for (int i = 0; i < m.Actions.Length; i++)
            {
                UiActionModel a = m.Actions[i];
                if (a.Kind != UiButtonKind.Chip)
                {
                    continue;
                }
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                UiDraw.Button(s, at, cx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                cx += bw + UiTokens.S3;
            }
            if (cx > x)
            {
                y += UiTokens.Control + UiTokens.S4;
            }
            float dw = d != null ? UiDraw.SnapDown(w * DetailShare) : 0f, tw = w - dw - (d != null ? UiTokens.S6 : 0f);
            UiAppWindow.Table(s, at, null, m.Columns, m.GradeColumn, m.Rows, UiAppWindow.RowCommand, x, y, tw, bottom - y);
            if (d == null)
            {
                return;
            }
            float dx = x + tw + UiTokens.S6;
            UiDraw.Box(s, at, dx, y, dw, bottom - y, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float ix = dx + UiTokens.S4, iw = dw - UiTokens.S4 * 2f, iy = y + UiTokens.S4;
            int tile = UiDraw.Tile(s, at, ix, iy, UiTokens.IconTile, UiRole.None, false);
            UiDraw.Icon(s, at.In(tile), ix + UiDraw.SnapDown((UiTokens.IconTile - UiTokens.Icon) / 2f),
                iy + UiDraw.SnapDown((UiTokens.IconTile - UiTokens.Icon) / 2f), UiTokens.Icon, d.Icon, UiRole.None, UiFlags.Middle, true);
            float nx = ix + UiTokens.IconTile + UiTokens.S3, nw = iw - UiTokens.IconTile - UiTokens.S3;
            UiDraw.TextMiddle(s, at, nx, iy, UiTokens.IconTile / 2f + UiTokens.S1, nw, UiStyle.Title, UiRole.Text,
                UiTextRules.Cut(d.Title ?? string.Empty, UiStyle.Title, nw), UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(d.Status))
            {
                UiDraw.Chip(s, at, nx, iy + UiTokens.IconTile / 2f + UiTokens.S2, d.Status, d.Tone, nw);
            }
            iy += UiTokens.IconTile + UiTokens.S3;
            float footer = bottom - UiTokens.Control - UiTokens.S4;
            if (!string.IsNullOrEmpty(d.Sentence))
            {
                int id = UiDraw.Text(s, at, ix, iy, iw, UiStyle.Body, UiRole.Text2, d.Sentence, UiFlags.Wrap | UiFlags.Truncate, 4);
                iy = UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S3;
            }
            for (int i = 0; i < d.Facts.Count && iy + UiTokens.PairRow < footer - UiTokens.S16 * 2f; i++)
            {
                iy += UiDraw.InfoRow(s, at, ix, iy, iw, d.Facts[i].Key, d.Facts[i].Value) + UiTokens.S1;
            }
            if (d.Needs.Count > 0)
            {
                iy += UiDraw.SectionHeader(s, at, ix, iy, iw, d.NeedsTitle ?? string.Empty);
                for (int i = 0; i < d.Needs.Count && iy + UiTokens.PairRow < footer; i++)
                {
                    iy += UiDraw.CheckLine(s, at, ix, iy, iw, d.Needs[i].Words, d.Needs[i].Met) + UiTokens.S1;
                }
            }
            if (d.People.Length > 0 && footer - iy > UiTokens.S16 * 3f)
            {
                UiAppWindow.People(s, at, d.PeopleTitle, d.PeopleCount, d.People, null, null, UiRole.Warn, null, ix, iy, iw, footer - iy - UiTokens.S2);
            }
            float right = ix + iw;
            for (int k = 0; k < m.Actions.Length; k++)
            {
                UiActionModel a = m.Actions[k];
                if (a.Kind == UiButtonKind.Chip)
                {
                    continue;
                }
                float bw = Math.Min(iw, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                right -= bw;
                UiDraw.Button(s, at, right, footer, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
        }
    }
}
