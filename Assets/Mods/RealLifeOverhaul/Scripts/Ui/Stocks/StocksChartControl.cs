using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the screen holds of the chart's view and the order, so the page is built from the market and this alone: the tool in
    /// hand, the candle the pointer reads, the window zoomed to, the unfinished drawing, the picker and the big screen. A hover
    /// or a pan changes these and lays the page out again; it does not build it again.
    /// </summary>
    public sealed class StocksView
    {
        public int Tool;
        public int Focus = -1;          // a candle of the whole set the pointer reads, or -1
        public int Shown;               // candles in the window; 0 for all
        public int End = -1;            // one past the last candle in the window; -1 for the newest
        public int Tip = -1;            // the rail's button whose name shows
        public int AnchorDay;           // the first click of a two click drawing: its game day...
        public float AnchorPrice;       // ...and its price in dollars; 0 day for none
        public int Selected = -1;       // the selected drawing of this company, in the order they were made
        public bool PickOpen, Big;
    }

    /// <summary>What a change of the chart's view needs: nothing, a layout from the model already built, or the page built again from the market.</summary>
    public enum StocksChange : byte
    {
        None = 0,
        Relay,
        Rebuild
    }

    /// <summary>
    /// How the company chart's view moves: the candle the pointer reads, the window zoomed to and panned along, the picker, the big
    /// screen and the half drawn tool. A hover, a pan, a zoom, a step of the keys and a tip are a <see cref="StocksChange.Relay"/>:
    /// the page is laid out again from the model it has, so nothing is built from the market for them. Only what the model is built
    /// from (the range, a tool, an indicator, a drawing) is a <see cref="StocksChange.Rebuild"/>. Game-free. Cost: a few comparisons
    /// a pointer move.
    /// </summary>
    public sealed class StocksChartControl
    {
        public readonly StocksView View = new StocksView();
        private bool _panning;
        private float _panX;
        private int _panEnd;

        /// <summary>A new company: the view as it begins.</summary>
        public void Reset()
        {
            StocksView v = View;
            v.Tool = 0;
            v.Focus = -1;
            v.Shown = 0;
            v.End = -1;
            v.Tip = -1;
            v.AnchorDay = 0;
            v.Selected = -1;
            v.PickOpen = false;
            v.Big = false;
            _panning = false;
        }

        /// <summary>A new range: the window is all of it again, the newest at its end, nothing half drawn or selected.</summary>
        public void ResetWindow()
        {
            View.Focus = -1;
            View.Shown = 0;
            View.End = -1;
            View.AnchorDay = 0;
            View.Selected = -1;
        }

        /// <summary>Puts the view's window, reading and tip on the chart model, so a layout from it shows them.</summary>
        public void Apply(UiChartDeskModel chart)
        {
            if (chart == null)
            {
                return;
            }
            chart.Tip = View.Tip;
            chart.Tool = View.Tool;
            if (chart.Close != null && chart.Close.Length > 0)
            {
                Window(chart, View, chart.Close.Length);
            }
        }

        /// <summary>The window of the whole set the view shows: how many candles and from which, the newest at the end unless the view moved back.</summary>
        public static void Window(UiChartDeskModel m, StocksView v, int n)
        {
            int shown = v.Shown <= 0 ? n : Math.Max(Math.Min(UiChartDesk.LeastShown, n), Math.Min(v.Shown, n));
            int end = v.End < 0 ? n : Math.Max(shown, Math.Min(v.End, n));
            m.Shown = shown;
            m.First = end - shown;
            m.Focus = v.Focus >= 0 && v.Focus < n ? v.Focus : -1;
        }

        /// <summary>The candle of the whole set under a place along the plot, kept inside the window.</summary>
        public static int CellAt(UiChartDeskModel chart, float x)
        {
            return Math.Max(chart.First, Math.Min(chart.First + chart.Shown - 1, chart.First + (int)Math.Floor((x - chart.PlotX) / chart.Slot)));
        }

        /// <summary>The mouse moved over the plot: the candle under it is read. A layout again only when it is another candle.</summary>
        public StocksChange Moved(UiChartDeskModel chart, float x)
        {
            if (chart == null || chart.Slot <= 0f || chart.Close == null || chart.Close.Length == 0)
            {
                return StocksChange.None;
            }
            int at = CellAt(chart, x);
            if (at == View.Focus)
            {
                return StocksChange.None;
            }
            View.Focus = at;
            Apply(chart);
            return StocksChange.Relay;
        }

        /// <summary>The mouse rests on a target (or none): a rail button's name shows, and the plot's reading goes when the mouse leaves the plot.</summary>
        public StocksChange Pointed(UiChartDeskModel chart, int command, int arg)
        {
            int tip = command == UiChartDesk.Rail ? arg : -1;
            bool leave = command != UiChartDesk.Command && View.Focus >= 0;
            if (tip == View.Tip && !leave)
            {
                return StocksChange.None;
            }
            View.Tip = tip;
            if (leave)
            {
                View.Focus = -1;
            }
            Apply(chart);
            return StocksChange.Relay;
        }

        /// <summary>The wheel over the plot: in toward the newest candle, out toward all of them.</summary>
        public StocksChange Wheel(UiChartDeskModel chart, float notches)
        {
            if (chart == null || chart.Close == null || chart.Close.Length == 0)
            {
                return StocksChange.None;
            }
            int n = chart.Close.Length, shown = chart.Shown;
            int to = notches > 0f ? Math.Max(Math.Min(UiChartDesk.LeastShown, n), (int)(shown * 0.8f)) : Math.Min(n, (int)(shown * 1.25f) + 1);
            if (to == shown)
            {
                return StocksChange.None;
            }
            View.Shown = to >= n ? 0 : to;
            Apply(chart);
            return StocksChange.Relay;
        }

        /// <summary>A drag begins on the plot with the pointer in hand: the chart moves along with it.</summary>
        public bool PanBegin(UiChartDeskModel chart, float x)
        {
            if (View.Tool != 0 || chart == null || chart.Close == null || chart.Slot <= 0f)
            {
                return false;
            }
            _panning = true;
            _panX = x;
            _panEnd = chart.First + chart.Shown;
            return true;
        }

        public StocksChange PanMove(UiChartDeskModel chart, float x)
        {
            if (!_panning || chart == null || chart.Slot <= 0f)
            {
                return StocksChange.None;
            }
            int n = chart.Close.Length;
            int end = Math.Max(chart.Shown, Math.Min(n, _panEnd - (int)Math.Round((x - _panX) / chart.Slot)));
            int stored = end >= n ? -1 : end;
            if (stored == View.End)
            {
                return StocksChange.None;
            }
            View.End = stored;
            Apply(chart);
            return StocksChange.Relay;
        }

        public void PanEnd()
        {
            _panning = false;
        }

        /// <summary>Left or Right on the plot: the candle read moves, and the window with it when it reaches an edge.</summary>
        public StocksChange Step(UiChartDeskModel chart, int step)
        {
            if (chart == null || chart.Close == null || chart.Close.Length == 0)
            {
                return StocksChange.None;
            }
            int n = chart.Close.Length;
            int at = Math.Max(0, Math.Min(n - 1, (View.Focus < 0 ? n - 1 : View.Focus) + step));
            View.Focus = at;
            if (at < chart.First)
            {
                View.End = at + chart.Shown;
            }
            else if (at >= chart.First + chart.Shown)
            {
                View.End = at + 1;
            }
            if (View.End >= n)
            {
                View.End = -1;
            }
            Apply(chart);
            return StocksChange.Relay;
        }

        /// <summary>The button or the key of the big screen: open, or shut again.</summary>
        public StocksChange ToggleBig()
        {
            View.Big = !View.Big;
            View.PickOpen = false;
            View.Tip = -1;
            return StocksChange.Rebuild;
        }

        /// <summary>Opens or shuts the picker of indicators.</summary>
        public StocksChange TogglePicker(bool open)
        {
            View.PickOpen = open;
            return StocksChange.Rebuild;
        }

        /// <summary>Back (Escape): shuts the picker, then the big screen, then drops a half drawn line. None when there was nothing to shut.</summary>
        public StocksChange Back()
        {
            if (View.PickOpen)
            {
                View.PickOpen = false;
                return StocksChange.Rebuild;
            }
            if (View.Big)
            {
                View.Big = false;
                return StocksChange.Rebuild;
            }
            if (View.AnchorDay != 0)
            {
                View.AnchorDay = 0;
                return StocksChange.Rebuild;
            }
            return StocksChange.None;
        }
    }
}
