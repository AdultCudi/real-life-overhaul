using System;
using UI.Notification;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The player's counters, from the rivals' page in the Finance app: outbid, hire away a rival's people, a piece in the
    /// paper, lobby back, and take a rival's holding over. Each is asked for with the game's own question and done here:
    /// its cost taken (RivalGame, in pieces), the rules' change made (RivalRules.Counter, Defeat), the customers written
    /// again where a rival's business closes, one notice. On a click only.
    /// </summary>
    public sealed partial class RivalDirector
    {
        /// <summary>Why a counter cannot be used against a tycoon now (None: it can), and what it would cost.</summary>
        public RivalWhy CanCounter(int tycoon, RivalCounter kind, out long cost)
        {
            cost = 0;
            if (!Running || _resting || Save == null)
            {
                return RivalWhy.Out;
            }
            if (kind == RivalCounter.Takeover)
            {
                return TakeoverQuote(tycoon, out cost, out long _);
            }
            RivalWhy why = RivalRules.CanCounter(_file, tycoon, kind, Save.Day, out RivalMove _);
            cost = RivalRules.CounterCost(kind, RivalGame.Worth(), Prices.Level);
            return why == RivalWhy.None && RivalGame.Cash < cost ? RivalWhy.Money : why;
        }

        /// <summary>What taking a tycoon's holding over would cost and bring back, and why it cannot be done now (None: it can).</summary>
        public RivalWhy TakeoverQuote(int tycoon, out long cost, out long breakUp)
        {
            cost = breakUp = 0;
            if (!Running || _resting || tycoon < 0 || tycoon >= _file.Tycoons.Length)
            {
                return RivalWhy.Out;
            }
            TycoonDef d = _file.Tycoons[tycoon].Def;
            long shares = 0, held = 0, most = 0;
            int mid = 0;
            bool listed = _file.Tycoons[tycoon].Listed && RivalBridges.Holding != null && RivalBridges.Holding(d.StockId, out shares, out held, out most, out mid);
            RivalWhy why = RivalRules.CanTakeOver(_file, tycoon, listed, held, most);
            if (listed)
            {
                RivalRules.TakeoverTerms(shares, held, mid, _file.Tycoons[tycoon].Worth, out cost, out breakUp);
            }
            return why == RivalWhy.None && RivalGame.Cash < cost ? RivalWhy.Money : why;
        }

        /// <summary>The player's holding of a tycoon's holding company and the most one holder may have, in shares; false if it is not listed here.</summary>
        public bool HoldingShares(int tycoon, out long held, out long most, out long shares, out int midCents)
        {
            held = most = shares = 0;
            midCents = 0;
            if (!Running || tycoon < 0 || tycoon >= _file.Tycoons.Length || !_file.Tycoons[tycoon].Listed || RivalBridges.Holding == null)
            {
                return false;
            }
            return RivalBridges.Holding(_file.Tycoons[tycoon].Def.StockId, out shares, out held, out most, out midCents);
        }

        /// <summary>A counter used now, after the player said yes. Null when it was done; otherwise the key of the sentence that says why not.</summary>
        public string Counter(int tycoon, RivalCounter kind)
        {
            try
            {
                RivalWhy why = CanCounter(tycoon, kind, out long cost);
                if (why != RivalWhy.None)
                {
                    return WhyKey(why);
                }
                int day = Save.Day;
                TycoonDef d = _file.Tycoons[tycoon].Def;
                if (kind == RivalCounter.Takeover)
                {
                    return TakeOver(tycoon, day);
                }
                if (!RivalGame.Take(cost, RivalGame.CounterKey, d.Holding))
                {
                    return WhyKey(RivalWhy.Money);
                }
                RivalCounterNote note = RivalRules.Counter(_file, tycoon, kind, day, cost, Prices.Level);
                if (note == null)
                {
                    RivalGame.Give(cost, RivalGame.CounterKey, d.Holding);   // (the rules said no after all: the money goes back)
                    return WhyKey(RivalWhy.Nothing);
                }
                RivalMove stopped = note.Move > 0 ? _file.Move(note.Move) : null;
                if (stopped != null && stopped.Kind == RivalMoveKind.Outlet)
                {
                    WriteFootfall(stopped);
                }
                else if (stopped != null && stopped.Kind == RivalMoveKind.Property)
                {
                    RivalBridges.Revalue?.Invoke();
                }
                RivalGame.Tell(NotificationType.Success, RivalText.CounterDoneKey(kind), RivalText.Data("rival", d.Name, "holding", d.Holding, "amount", RivalText.Money(cost)), _openPage);
                Logger?.Info("Rivals: the player's " + kind + " against " + d.Name + " for " + cost + (stopped != null ? ", stopping " + stopped.Kind + " (move " + stopped.Id + ")" : string.Empty) + ".");
                return null;
            }
            catch (Exception e)
            {
                Fault("a counter (" + kind + ")", e);
                return "reallife_rival_why_out";
            }
        }

        // A takeover: the shares the player does not hold bought at the premium through the market, the holding off it, the rival out.
        private string TakeOver(int tycoon, int day)
        {
            RivalWhy why = TakeoverQuote(tycoon, out long cost, out long breakUp);
            if (why != RivalWhy.None)
            {
                return WhyKey(why);
            }
            TycoonDef d = _file.Tycoons[tycoon].Def;
            if (RivalBridges.TakeOver == null || !RivalBridges.TakeOver(d.StockId, cost))
            {
                return WhyKey(RivalWhy.Money);
            }
            RivalRules.Defeat(_file, tycoon, day, cost, breakUp);
            Released(null);
            RivalGame.Tell(NotificationType.Success, RivalText.CounterDoneKey(RivalCounter.Takeover), RivalText.Data("rival", d.Name, "holding", d.Holding, "amount", RivalText.Money(cost),
                "back", RivalText.Money(breakUp), "days", RivalCatalogue.BreakUpDays.ToString()), _openPage);
            Logger?.Info("Rivals: the player took " + d.Holding + " over for " + cost + "; " + d.Name + " is out; its parts bring " + breakUp + " on day " + _file.Tycoons[tycoon].BreakUpDay + ".");
            return null;
        }

        private static string WhyKey(RivalWhy why)
        {
            switch (why)
            {
                case RivalWhy.Out: return "reallife_rival_why_out";
                case RivalWhy.Rest: return "reallife_rival_why_rest";
                case RivalWhy.Nothing: return "reallife_rival_why_nothing";
                case RivalWhy.NoHolding: return "reallife_rival_why_no_holding";
                case RivalWhy.Short: return "reallife_rival_why_short";
                default: return "reallife_rival_why_money";
            }
        }

        /// <summary>The rich list as a page of the City Ledger, opened now. False (with a notice) if the paper cannot show it.</summary>
        public bool ShowList()
        {
            RivalFile f = File;
            if (f == null || RivalBridges.ShowList == null)
            {
                return false;
            }
            bool shown = RivalBridges.ShowList(RivalPage.ListPage(f, RivalGame.Worth(), Save != null ? Save.Day : 0));
            if (!shown)
            {
                RivalGame.Tell(NotificationType.Info, "reallife_rival_note_list_later", null, null);
            }
            return shown;
        }
    }
}
