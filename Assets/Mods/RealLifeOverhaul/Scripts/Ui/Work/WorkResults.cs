using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A transcript page as its mockup draws it: the student's head with the average, the credits and the class rank; the transcript at the left; at
    /// the right the average by quarter as a chart, what comes up (exams) and the class by average. Every part is the kit's own. Game-free.
    /// </summary>
    public static class WorkResults
    {
        public static void Lay(UiScene s, UiAt at, UiAppModel m, UiChartModel chart, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Profile != null)
            {
                y = UiAppWindow.Profile(s, at, m.Profile, null, x, y, w, keys);
            }
            float lw = UiDraw.SnapDown(w * 0.6f), rw = w - lw - UiTokens.S6, rx = x + lw + UiTokens.S6;
            UiAppWindow.Table(s, at, m.TableTitle, m.Columns, m.GradeColumn, m.Rows, UiAppWindow.RowCommand, x, y, lw, bottom - y);
            float ry = y;
            if (chart != null)
            {
                float ch = Math.Min(UiDraw.SnapDown((bottom - y) * 0.45f), UiTokens.S16 * 14f);
                UiChart.Lay(s, at, chart, rx, ry, rw, ch);
                ry += ch + UiTokens.S4;
            }
            if (m.Figures.Length > 0)
            {
                ry = UiAppWindow.Figures(s, at, m.Figures, rx, ry, rw);
            }
            if (m.MoreRows.Length > 0 && bottom - ry > UiTokens.S16 * 3f)
            {
                UiAppWindow.Table(s, at, m.MoreTitle, m.MoreColumns, m.MoreGrade, m.MoreRows, UiAppWindow.MoreCommand, rx, ry, rw, bottom - ry);
            }
        }
    }
}
