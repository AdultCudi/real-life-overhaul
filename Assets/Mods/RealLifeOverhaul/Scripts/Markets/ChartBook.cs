using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What is drawn over the candles. Several may be on at once. Saved as bits: never renumber.</summary>
    public enum ChartLayer : byte
    {
        Sma20 = 1,
        Sma50 = 2,
        Ema20 = 4,
        Bollinger = 8
    }

    /// <summary>A pane under the candles. Two may show. Saved as a number.</summary>
    public enum ChartPane : byte
    {
        None = 0,
        Rsi = 1,
        Macd = 2,
        Stochastic = 3,
        Atr = 4,
        Obv = 5
    }

    /// <summary>A mark the player draws on one company. Saved as a number.</summary>
    public enum ChartMark : byte
    {
        Trend = 0,
        Level = 1,       // a horizontal line: support or resistance
        Fibonacci = 2,
        Path = 3,
        Rectangle = 4,
        Measure = 5,
        Text = 6
    }

    /// <summary>How a price alert fires. Saved as a number.</summary>
    public enum AlertKind : byte
    {
        Above = 0,
        Below = 1,
        Support = 2      // under a drawn level
    }

    /// <summary>One saved drawing on one company. Prices in cents, the ends as candle indexes in the frame it was drawn on.</summary>
    public struct ChartDrawing
    {
        public int StockId;
        public ChartMark Kind;
        public int Frame;
        public int FromIndex, ToIndex;
        public int FromCents, ToCents;
        public byte Magnet;
    }

    /// <summary>A price the player asked to be told about. Checked on the market step, not from a frame.</summary>
    public struct PriceAlert
    {
        public int StockId;
        public AlertKind Kind;
        public int Cents;
        public int Drawing;     // a level's place in the drawings, or -1
        public bool Hit;
        public int HitDay;
    }

    /// <summary>A plain reading of the indicators at the last candle. 1 with the move, -1 against, 0 nothing to say.</summary>
    public struct ChartSignal
    {
        public int Sma;       // price against the 50 day average
        public int Rsi;       // 1 overbought, -1 oversold
        public int Macd;      // the line against its signal
        public int Band;      // 1 outside the upper band, -1 outside the lower
    }

    /// <summary>
    /// The chart the player keeps: which lines and panes are on, the drawings of each company, and the price alerts.
    /// The mesh (the kit's chart, laid out again) changes when this or the candle changes, not every frame. Alerts are
    /// checked when a candle is written. Game-free. Cost: nothing a frame; a short walk of the alerts on a candle.
    /// </summary>
    public sealed class ChartBook
    {
        public const int DrawingsMost = 32;
        public const int AlertsMost = 16;
        public const int HitsMost = 8;

        public int Layers;                 // ChartLayer bits
        public ChartPane FirstPane, SecondPane;
        public int Kind;                   // 0 candles, 1 line, 2 area
        public int Frame;                  // ChartFrame
        public int Stamp;                  // grows when the set or a drawing changes

        public readonly List<ChartDrawing> Drawings = new List<ChartDrawing>(8);
        public readonly List<PriceAlert> Alerts = new List<PriceAlert>(4);

        private readonly int[] _hitStock = new int[HitsMost];
        private readonly int[] _hitCents = new int[HitsMost];
        private readonly int[] _hitKind = new int[HitsMost];
        private int _hits;

        public int HitCount => _hits;

        public void Touch()
        {
            Stamp++;
        }

        public bool LayerOn(ChartLayer layer) => (Layers & (int)layer) != 0;

        public void ToggleLayer(ChartLayer layer)
        {
            Layers ^= (int)layer;
            Touch();
        }

        /// <summary>Puts a pane in the first free slot, or takes it off when it is already on. At most two.</summary>
        public void TogglePane(ChartPane pane)
        {
            if (pane == ChartPane.None)
            {
                return;
            }
            if (FirstPane == pane)
            {
                FirstPane = SecondPane;
                SecondPane = ChartPane.None;
            }
            else if (SecondPane == pane)
            {
                SecondPane = ChartPane.None;
            }
            else if (FirstPane == ChartPane.None)
            {
                FirstPane = pane;
            }
            else
            {
                SecondPane = pane;   // the older second pane gives way
            }
            Touch();
        }

        public bool AddDrawing(ChartDrawing drawing)
        {
            if (drawing.StockId <= 0)
            {
                return false;
            }
            if (Drawings.Count >= DrawingsMost)
            {
                Drawings.RemoveAt(0);
            }
            Drawings.Add(drawing);
            Touch();
            return true;
        }

        public int ClearDrawings(int stockId)
        {
            int gone = 0;
            for (int i = Drawings.Count - 1; i >= 0; i--)
            {
                if (Drawings[i].StockId == stockId)
                {
                    Drawings.RemoveAt(i);
                    gone++;
                }
            }
            if (gone > 0)
            {
                Touch();
            }
            return gone;
        }

        public bool AddAlert(PriceAlert alert)
        {
            if (alert.StockId <= 0 || Alerts.Count >= AlertsMost)
            {
                return false;
            }
            alert.Drawing = alert.Kind == AlertKind.Support ? alert.Drawing : -1;
            Alerts.Add(alert);
            Touch();
            return true;
        }

        public bool RemoveAlert(int index)
        {
            if (index < 0 || index >= Alerts.Count)
            {
                return false;
            }
            Alerts.RemoveAt(index);
            Touch();
            return true;
        }

        /// <summary>A candle's close against the alerts of that company. Hits wait for the director; nothing is built here.</summary>
        public void Check(int stockId, int cents, int day)
        {
            if (stockId <= 0 || cents <= 0)
            {
                return;
            }
            for (int i = 0; i < Alerts.Count; i++)
            {
                PriceAlert a = Alerts[i];
                if (a.Hit || a.StockId != stockId)
                {
                    continue;
                }
                int level = a.Cents;
                if (a.Kind == AlertKind.Support)
                {
                    level = LevelOf(a.Drawing, stockId);
                }
                if (level <= 0)
                {
                    continue;
                }
                bool hit = a.Kind == AlertKind.Above ? cents >= level : cents <= level;
                if (!hit)
                {
                    continue;
                }
                a.Hit = true;
                a.HitDay = day;
                Alerts[i] = a;
                if (_hits < HitsMost)
                {
                    _hitStock[_hits] = stockId;
                    _hitCents[_hits] = cents;
                    _hitKind[_hits] = (int)a.Kind;
                    _hits++;
                }
            }
        }

        public bool TakeHit(out int stockId, out int cents, out int kind)
        {
            if (_hits <= 0)
            {
                stockId = 0;
                cents = 0;
                kind = 0;
                return false;
            }
            _hits--;
            stockId = _hitStock[_hits];
            cents = _hitCents[_hits];
            kind = _hitKind[_hits];
            return true;
        }

        public int LevelOf(int drawing, int stockId)
        {
            if (drawing < 0 || drawing >= Drawings.Count)
            {
                return 0;
            }
            ChartDrawing d = Drawings[drawing];
            if (d.StockId != stockId || d.Kind != ChartMark.Level)
            {
                return 0;
            }
            return d.FromCents;
        }

        /// <summary>The last candle's readings. Work arrays are the caller's, at least as long as the closes.</summary>
        public static ChartSignal Signals(double[] close, int count, double[] a, double[] b, double[] c, double[] scratch)
        {
            var signal = new ChartSignal();
            if (close == null || count < 2 || a == null)
            {
                return signal;
            }
            double last = close[count - 1];
            MarketIndicators.Sma(close, count, 50, a);
            if (!double.IsNaN(a[count - 1]) && a[count - 1] > 0.0)
            {
                signal.Sma = last >= a[count - 1] ? 1 : -1;
            }
            MarketIndicators.Rsi(close, count, 14, a);
            if (!double.IsNaN(a[count - 1]))
            {
                signal.Rsi = a[count - 1] >= 70.0 ? 1 : a[count - 1] <= 30.0 ? -1 : 0;
            }
            if (b != null && c != null && scratch != null)
            {
                MarketIndicators.Macd(close, count, 12, 26, 9, a, b, c, scratch);
                if (!double.IsNaN(a[count - 1]) && !double.IsNaN(b[count - 1]))
                {
                    signal.Macd = a[count - 1] >= b[count - 1] ? 1 : -1;
                }
                MarketIndicators.Bollinger(close, count, 20, 2.0, a, b, c);
                if (!double.IsNaN(b[count - 1]) && !double.IsNaN(c[count - 1]))
                {
                    signal.Band = last > b[count - 1] ? 1 : last < c[count - 1] ? -1 : 0;
                }
            }
            return signal;
        }
    }
}
