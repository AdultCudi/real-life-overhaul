using System;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // A logistics route's page as a player saw it at 1920 x 1080 (the least any screen gives): the form above the goods' table, as tall as a route with
    // trips makes it, left the table one row in the room that was left. The table keeps its rows and what stands above it scrolls.
    private static void BizOpsRouteChecks()
    {
        UiWords t = UiWords("en");
        float[] aspects = { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f };
        string fault = null;
        int most = 0, least = int.MaxValue;
        foreach (int products in new[] { 5, 12, 40 })
        {
            foreach (float aspect in aspects)
            {
                UiFrame frame = UiFrame.FullMenu(aspect, "BizMan");
                frame.Content(out float _, out float cy, out float _, out float ch);
                BizManModel m = UiSamples.OpsRoute(t, products);
                var s = new UiScene();
                BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
                UiDataTableModel table = m.Ops.Table;
                int clip = TableClip(s, table);
                if (clip < 0)
                {
                    fault ??= products + " goods at " + aspect.ToString("0.00") + ": the table is not laid";
                    continue;
                }
                int shown = s.Lists[s.Clips[clip].List].Shown;
                most = Math.Max(most, shown);
                least = Math.Min(least, shown);
                if (shown < Math.Min(products, 5))
                {
                    fault ??= products + " goods at " + aspect.ToString("0.00") + ": only " + shown + " rows are in view";
                }
                if (s.Clips[clip].Y + s.Clips[clip].H > cy + ch + 0.5f)
                {
                    fault ??= products + " goods at " + aspect.ToString("0.00") + ": the table runs past the window's foot";
                }
            }
        }
        Check(fault == null && least >= 5,
            "bizops: a route's goods table shows at least five rows at every screen with 5, 12 and 40 goods under a form as tall as a route with trips makes it (" + (fault ?? least + " to " + most + " rows") + ")");

        // Every row scrolls into view, whatever the offset it stood at.
        {
            UiFrame frame = UiFrame.FullMenu(16f / 9f, "BizMan");
            BizManModel m = UiSamples.OpsRoute(t, 12);
            string unseen = null;
            for (int row = 0; row < 12 && unseen == null; row++)
            {
                m.Ops.Table.Offset = row % 2 == 0 ? 0f : 1e6f;
                m.Ops.Table.Reveal = row;
                var s = new UiScene();
                BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
                int clip = TableClip(s, m.Ops.Table);
                UiList list = s.Lists[s.Clips[clip].List];
                unseen = row >= list.First && row < list.First + list.Shown ? null : "row " + row + " (rows " + list.First + " to " + (list.First + list.Shown - 1) + " in view)";
            }
            Check(unseen == null, "bizops: each of a route's twelve goods scrolls into view from the top or the foot of the table" + (unseen == null ? string.Empty : ": " + unseen));
        }

        // A wheel's notch moves the table by a row, from the middle of it: not to the end, and not by nothing.
        {
            UiFrame frame = UiFrame.FullMenu(16f / 9f, "BizMan");
            BizManModel m = UiSamples.OpsRoute(t, 40);
            var s = new UiScene();
            BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
            float rowH = UiDataTable.RowHeight(m.Ops.Table);
            m.Ops.Table.Offset = 10 * rowH;
            BizManPages.Lay(s = new UiScene(), m, frame, true, UiDefaultKeys.Keyboard);
            float before = m.Ops.Table.Offset;
            m.Ops.Table.Offset += BizOpsRules.TableStep(UiTokens.S16, rowH);
            BizManPages.Lay(s = new UiScene(), m, frame, true, UiDefaultKeys.Keyboard);
            float one = m.Ops.Table.Offset - before;
            m.Ops.Table.Offset += BizOpsRules.TableStep(-UiTokens.S16, rowH);
            BizManPages.Lay(s = new UiScene(), m, frame, true, UiDefaultKeys.Keyboard);
            Check(Math.Abs(one - rowH) < 0.5f && Math.Abs(m.Ops.Table.Offset - before) < 0.5f && BizOpsRules.TableStep(UiTokens.S16, 200f) == 200f,
                "bizops: a wheel notch moves a goods table by one row and the next notch back moves it back, however tall its rows are (" + one + " of " + rowH + ")");
        }

        // The form above the table scrolls as a part of its own: every target of it can be brought into its room, and the pad reaches all of them.
        {
            UiFrame frame = UiFrame.FullMenu(16f / 9f, "BizMan");
            BizManModel m = UiSamples.OpsRoute(t, 40);
            var s = new UiScene();
            BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
            short clip = m.Ops.FormClip;
            bool scrolls = clip >= 0 && s.Clips[clip].ContentHeight > s.Clips[clip].H + 0.5f && m.Ops.FormList >= 0 && m.Ops.FormListRight >= 0;
            string lost = null;
            for (int side = 0; scrolls && side < 2 && lost == null; side++)
            {
                short list = side == 0 ? m.Ops.FormList : m.Ops.FormListRight;
                int rows = s.Lists[list].Count;
                for (int row = 0; row < rows && lost == null; row++)
                {
                    m.Ops.FormReveal = row;
                    m.Ops.FormRevealList = list;
                    var r = new UiScene();
                    BizManPages.Lay(r, m, frame, true, UiDefaultKeys.Keyboard);
                    int node = -1;
                    for (int i = 0; i < r.NodeCount && node < 0; i++)
                    {
                        node = r.Nodes[i].List == list && r.Nodes[i].Index == row ? i : -1;
                    }
                    r.NodeBox(node, out float _, out float ny, out float _, out float nh);
                    ref UiClip c = ref r.Clips[m.Ops.FormClip];
                    lost = node >= 0 && ny >= c.Y - 0.5f && ny + nh <= c.Y + c.H + 0.5f ? null : "row " + row + " of column " + side;
                }
            }
            m.Ops.FormReveal = -1;
            var all = new UiScene();
            BizManPages.Lay(all, m, frame, true, UiDefaultKeys.Keyboard);
            int unreached = UiFocusRules.Unreached(all, out string first);
            Check(scrolls && lost == null && unreached == 0,
                "bizops: a route's form above its goods scrolls in the room the table leaves, each row of both its columns can be brought into view and the pad reaches every target ("
                + (scrolls ? lost ?? first ?? "all" : "it did not scroll") + ")");
        }

        // The table's wheel and the form's wheel are two regions, and neither takes the other's offset.
        {
            UiFrame frame = UiFrame.FullMenu(16f / 9f, "BizMan");
            BizManModel m = UiSamples.OpsRoute(t, 40);
            var s = new UiScene();
            BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
            int tableClip = TableClip(s, m.Ops.Table);
            Check(m.Ops.FormClip >= 0 && tableClip >= 0 && m.Ops.FormClip != tableClip && s.Clips[m.Ops.FormClip].Y + s.Clips[m.Ops.FormClip].H <= s.Clips[tableClip].Y,
                "bizops: the scrolling form stands over the goods table in a region of its own, the table's rows under it");
        }
    }

    // The scroll region of a page's big table: the list of as many rows as the table has.
    private static int TableClip(UiScene s, UiDataTableModel table)
    {
        for (int i = 0; i < s.ListCount; i++)
        {
            if (s.Lists[i].Count == table.RowCount && s.Lists[i].Clip >= 0 && Math.Abs(s.Clips[s.Lists[i].Clip].ContentHeight - table.RowCount * UiDataTable.RowHeight(table)) < 0.5f)
            {
                return s.Lists[i].Clip;
            }
        }
        return -1;
    }
}
