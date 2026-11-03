using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Which way a named print went. Saved as a number.</summary>
    public enum TradeSide : byte
    {
        Buy = 0,
        Sell = 1
    }

    /// <summary>Who dealt. Saved as a number: never renumber, add at the end. News reads the same values later.</summary>
    public enum TradeWho : byte
    {
        You = 0,
        Hired = 1,      // a hired firm's id
        Rule = 2,       // a rule bot's id
        Rival = 3,      // a rival template's index
        Fund = 4,
        Investor = 5,
        Broker = 6
    }

    /// <summary>One named print: who, which way, how many, the price and when. No text, so a step can write one without building a string.</summary>
    public struct TradePrint
    {
        public int StockId;
        public TradeSide Side;
        public long Size;
        public int PriceCents;
        public int Day;
        public int Phase;
        public TradeWho Who;
        public int WhoId;
    }

    /// <summary>
    /// The named tape of trades, one ring, written when a trade happens and never from a frame. Its own line in the save
    /// (<see cref="Key"/>), versioned; a line this version cannot read is kept. Deterministic: a print follows the fill
    /// or the candle, and a flow print names its dealer from the day, the step and the company, with no new draw.
    /// Game-free. Cost: nothing a frame; a few integer writes on a fill or a notable candle.
    /// </summary>
    public sealed class TradeLog
    {
        public const string Key = "reallife:tradelog";
        public const int Version = 1;
        public const int Capacity = 96;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly TradePrint[] _ring = new TradePrint[Capacity];
        private int _count;
        private int _next;

        /// <summary>
        /// Set for the call that places a rule bot's order, so the fill is that rule's and not the player's own hand.
        /// The order has no field for it. Cleared by the caller before it returns.
        /// </summary>
        public static int RuleMark;

        public int Count => _count;

        /// <summary>The print that many places back from the newest, or a zero print past the end.</summary>
        public TradePrint Newest(int back)
        {
            if (back < 0 || back >= _count)
            {
                return new TradePrint();
            }
            int at = _next - 1 - back;
            if (at < 0)
            {
                at += Capacity;
            }
            return _ring[at];
        }

        /// <summary>The newest print in one company, or false when the ring has none.</summary>
        public bool Latest(int stockId, out TradePrint print)
        {
            for (int i = 0; i < _count; i++)
            {
                TradePrint p = Newest(i);
                if (p.StockId == stockId && p.Size > 0)
                {
                    print = p;
                    return true;
                }
            }
            print = new TradePrint();
            return false;
        }

        public void Note(TradePrint print)
        {
            if (print.Size <= 0 || print.PriceCents <= 0 || print.StockId == 0)
            {
                return;
            }
            _ring[_next] = print;
            _next++;
            if (_next >= Capacity)
            {
                _next = 0;
            }
            if (_count < Capacity)
            {
                _count++;
            }
        }

        /// <summary>
        /// A candle that moved enough to be a print, named without drawing again. Most candles are the day's flow and
        /// stay unnamed, so the ring keeps the ones a player would read.
        /// </summary>
        public void NoteFlow(int stockId, int open, int close, long volume, int day, int phase)
        {
            if (stockId <= 0 || open <= 0 || close <= 0 || volume <= 0)
            {
                return;
            }
            int move = close - open;
            if (move < 0)
            {
                move = -move;
            }
            if (move * 200 < open)
            {
                return;   // under half a percent: the candle is the flow, not a print
            }
            if ((stockId + day) % 11 != phase % 11)
            {
                return;   // a fixed share of the notable candles, the same on every machine
            }
            long size = volume / 50;
            if (size < 1)
            {
                size = 1;
            }
            int who = (stockId * 3 + day + phase) % 4;
            Note(new TradePrint
            {
                StockId = stockId,
                Side = close >= open ? TradeSide.Buy : TradeSide.Sell,
                Size = size,
                PriceCents = close,
                Day = day,
                Phase = phase,
                Who = who == 0 ? TradeWho.Fund : who == 1 ? TradeWho.Investor : who == 2 ? TradeWho.Broker : TradeWho.Rival,
                WhoId = (stockId + day) % 4
            });
        }

        /// <summary>A fill already on the account's history, named for the tape. A rule's mark wins over "you".</summary>
        public void NoteFill(Trade trade)
        {
            if (trade.Kind != TradeKind.Bought && trade.Kind != TradeKind.Sold)
            {
                return;
            }
            TradeWho who = TradeWho.You;
            int whoId = 0;
            if (trade.Bot > 0)
            {
                who = TradeWho.Hired;
                whoId = trade.Bot;
            }
            else if (RuleMark > 0)
            {
                who = TradeWho.Rule;
                whoId = RuleMark;
            }
            Note(new TradePrint
            {
                StockId = trade.StockId,
                Side = trade.Kind == TradeKind.Bought ? TradeSide.Buy : TradeSide.Sell,
                Size = trade.Qty,
                PriceCents = trade.PriceCents,
                Day = trade.Day,
                Phase = trade.Phase,
                Who = who,
                WhoId = whoId
            });
        }

        public string Write()
        {
            var t = new StringBuilder(Capacity * 24);
            t.Append('v').Append(Version).Append('|').Append(_count.ToString(Inv)).Append('|').Append(_next.ToString(Inv));
            int start = _count < Capacity ? 0 : _next;
            for (int i = 0; i < _count; i++)
            {
                TradePrint p = _ring[(start + i) % Capacity];
                t.Append(';').Append(p.StockId.ToString(Inv));
                t.Append('|').Append(((int)p.Side).ToString(Inv));
                t.Append('|').Append(p.Size.ToString(Inv));
                t.Append('|').Append(p.PriceCents.ToString(Inv));
                t.Append('|').Append(p.Day.ToString(Inv));
                t.Append('|').Append(p.Phase.ToString(Inv));
                t.Append('|').Append(((int)p.Who).ToString(Inv));
                t.Append('|').Append(p.WhoId.ToString(Inv));
            }
            return t.ToString();
        }

        /// <summary>Null when the line is not this version's: the caller keeps it.</summary>
        public static TradeLog Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || text.Length < 4 || text[0] != 'v')
            {
                return null;
            }
            int bar = text.IndexOf('|');
            if (bar < 2 || !int.TryParse(text.Substring(1, bar - 1), NumberStyles.Integer, Inv, out version) || version != Version)
            {
                return null;   // a newer line, or one that is not a tape, is kept whole
            }
            string[] parts = text.Substring(bar + 1).Split(';');
            if (parts.Length == 0)
            {
                return null;
            }
            string[] head = parts[0].Split('|');
            if (head.Length < 2 || !int.TryParse(head[0], NumberStyles.Integer, Inv, out int count) || !int.TryParse(head[1], NumberStyles.Integer, Inv, out int next))
            {
                return null;
            }
            var log = new TradeLog();
            int got = 0;
            for (int i = 1; i < parts.Length && got < Capacity; i++)
            {
                string[] c = parts[i].Split('|');
                if (c.Length < 8)
                {
                    continue;   // a record this version cannot read is left out; the rest stand
                }
                if (!int.TryParse(c[0], NumberStyles.Integer, Inv, out int stock) || !long.TryParse(c[2], NumberStyles.Integer, Inv, out long size)
                    || !int.TryParse(c[3], NumberStyles.Integer, Inv, out int price) || !int.TryParse(c[4], NumberStyles.Integer, Inv, out int day)
                    || !int.TryParse(c[5], NumberStyles.Integer, Inv, out int phase) || !int.TryParse(c[1], NumberStyles.Integer, Inv, out int side)
                    || !int.TryParse(c[6], NumberStyles.Integer, Inv, out int who) || !int.TryParse(c[7], NumberStyles.Integer, Inv, out int whoId))
                {
                    continue;
                }
                if (stock <= 0 || size <= 0 || price <= 0)
                {
                    continue;
                }
                log._ring[got] = new TradePrint
                {
                    StockId = stock,
                    Side = (TradeSide)(side == 1 ? 1 : 0),
                    Size = size,
                    PriceCents = price,
                    Day = day,
                    Phase = phase,
                    Who = (TradeWho)(who < 0 || who > (int)TradeWho.Broker ? 0 : who),
                    WhoId = whoId
                };
                got++;
            }
            log._count = got;
            log._next = got >= Capacity ? 0 : got;
            if (next < 0)
            {
                next = 0;
            }
            return log;
        }

        public static void Load(Dictionary<string, string> data, MarketState state)
        {
            if (data == null || state == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            TradeLog read = SaveLines.Checked(data, Key, Read(text, out int version), version, Version);
            if (read == null)
            {
                return;
            }
            state.Names.CopyFrom(read);
        }

        public static void Save(Dictionary<string, string> data, MarketState state)
        {
            if (data == null || state == null || SaveLines.IsKept(data, Key))
            {
                return;   // a line this version could not read stays as it was
            }
            if (state.Names.Count == 0 && !data.ContainsKey(Key))
            {
                return;
            }
            data[Key] = state.Names.Write();
        }

        public void CopyFrom(TradeLog other)
        {
            if (other == null)
            {
                return;
            }
            _count = 0;
            _next = 0;
            for (int i = other._count - 1; i >= 0; i--)
            {
                Note(other.Newest(i));
            }
        }
    }
}
