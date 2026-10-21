using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the market director: the market in a Going Public session, the host's tape to its guests and a guest's trades to the host.</summary>
    public sealed partial class MarketDirector
    {
        // The host: what its guests have not had of the tape, with where the market stands; null if nothing has moved.
        private string HostChanges()
        {
            if (!Running)
            {
                _shared.Length = 0;
                return null;
            }
            if (_engine.Midway)
            {
                return null;   // a moment half played: its lines and check go with the next update
            }
            string lines = _shared.Length > 0 ? _shared.ToString() : null;
            _shared.Length = 0;
            return MarketShare.Changes(lines, _engine.State, ref _checkedAt);
        }

        // A guest, once a second: its trades since the last look, no more lines than a host reads with what already waits.
        private void SendTrades()
        {
            if (!_following || _guestTrades.Empty)
            {
                return;
            }
            int room = GuestTrades.LinesMost - SharedCity.Waiting('M');
            int sent = room > 0 ? _guestTrades.Send(_askTrade, room) : 0;
            if (Verbose && sent > 0)
            {
                Logger?.Info("Stock market check (a guest): " + sent + " trade line(s) for the host, " + _guestTrades.Lines + " waiting for the next second, at most "
                             + GuestTrades.LinesMost + " a request: PASS");
            }
        }

        // The host: a guest's trades, put on the tape for every market at the next moment.
        private void HostRequest(string text, string who)
        {
            if (!Running || string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] lines = text.Split('\n');
            int done = 0;
            for (int i = 0; i < lines.Length && i < GuestTrades.LinesMost; i++)
            {
                if (GuestTrades.Read(lines[i], out int stock, out int micros, out long shares) && _engine.GuestTrade(stock, micros, shares))
                {
                    done++;
                }
            }
            if (Verbose)
            {
                Logger?.Info("Stock market check (the host): " + done + " trade(s) of " + who + " put on the tape for the next moment: PASS");
            }
        }

        // A guest: the host's market whole, which becomes this machine's with the player's own in it, or its tape's new lines.
        private bool ApplyHost(string text, bool whole)
        {
            if (!Running)
            {
                return false;
            }
            _engine.Finish();
            if (!whole)
            {
                if (!_following || !MarketShare.Follow(_engine.Tape, text, _checks, ref _hostFrontier))
                {
                    return false;
                }
                _engine.Follow(_hostFrontier);
                return true;
            }
            var checks = new List<long>(4);
            MarketState state = MarketShare.Adopt(_engine.State, text, checks, HostTape(text), out MarketTape tape, out long frontier);
            if (state == null)
            {
                return false;
            }
            if (_engine.Tape.HasPending)
            {
                MarketTapeFile.Append(_engine.Tape.Id, _engine.Tape.TakePending());   // this machine's own tape, as it was
            }
            ulong was = _engine.State.Id;
            _engine = new MarketEngine(state, tape, _world);
            _engine.BotTold = _botTold;
            HookBonds();
            HookInvest();
            HookDerivs();
            _engine.Follow(frontier);
            _engine.TradeSink = _tradeSink;
            state.Following = true;   // the player's own company is set aside meanwhile (MarketShare.Adopt)
            _hostFrontier = frontier;
            MarketTapeFile.Rewrite(state.Id, tape.Whole());
            _frontierWritten = tape.Frontier;
            _following = true;
            _checks.Clear();
            _checks.AddRange(checks);
            Behind = false;
            _notes.Clear();
            Moved();
            Logger?.Info("Stock market: the host's market is this one now (" + state.Stocks.Count + " companies, day " + state.Day + " phase " + state.Phase + ", its tape to "
                         + tape.FrontierDay + "/" + tape.FrontierPhase + (was != state.Id ? "; this save's own market was another playthrough's" : string.Empty) + "), with this player's "
                         + state.Positions.Count + " holding(s) and " + state.Orders.Count + " order(s).");
            return true;
        }

        // The tape a guest already has of the host's market, so its past is kept; null for a market it never followed.
        private MarketTape HostTape(string text)
        {
            int at = text.IndexOf("~h:", StringComparison.Ordinal);
            if (at < 0 || text.Length < at + 19
                || !ulong.TryParse(text.Substring(at + 3, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong id))
            {
                return null;
            }
            if (_engine.Tape.Id == id)
            {
                return _engine.Tape;
            }
            return System.IO.File.Exists(MarketTapeFile.PathOf(id)) ? MarketTapeFile.Read(id) : null;
        }

        // A guest at a moment the host checked: the same prices, or the market is asked for again.
        private void Checked()
        {
            while (_checks.Count >= 2 && _checks[0] <= _engine.State.Now)
            {
                long at = _checks[0], sum = _checks[1];
                _checks.RemoveRange(0, 2);
                if (at != _engine.State.Now)
                {
                    continue;   // passed in a jump of the clock: the next one will do
                }
                long mine = MarketShare.Checksum(_engine.State);
                if (mine != sum)
                {
                    Logger?.Warn("Stock market: at " + at / 100 + "/" + at % 100 + " this guest's market is not the host's (" + mine + " against " + sum + "); it is asked for again.");
                    SharedCity.AskAgain('M');
                }
                else if (Verbose)
                {
                    Logger?.Info("Stock market check (a guest, " + at / 100 + "/" + at % 100 + "): the same prices as the host's: PASS");
                }
            }
        }

        // A guest no longer follows: the market goes on from here alone, with secrets of its own past the host's last moment.
        private void Unfollow()
        {
            _following = false;
            _checks.Clear();
            _guestTrades.Clear();   // not sent: the host is no longer followed
            if (_engine != null)
            {
                _engine.Finish();   // a moment half played is played out as the guest's
                _engine.TradeSink = null;
                _engine.State.Following = false;
                Logger?.Info("Stock market: the host's market is no longer followed; it goes on alone from day " + _engine.State.Day + " phase " + _engine.State.Phase + ".");
                OwnAlone();
            }
        }
    }
}
