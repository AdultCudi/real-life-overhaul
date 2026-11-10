using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: the player's firms that listed on their own and the city's startups that listed (MarketEngine.Firms,
    /// <see cref="ListingFirm"/>). The tech sector asks for them on its game day or a press; each change marks the market changed, so a Going Public
    /// host's guests are given it whole with the next update. A guest follows the host's market and changes none of it.
    /// </summary>
    public sealed partial class MarketDirector
    {
        /// <summary>True while a firm can be put on the market from this machine: the market runs, plays alone, and the player has a save to own it.</summary>
        public bool FirmsOpen => Running && !_following && !GoingPublic.IsGuest && _engine.State.Me != 0UL && !_engine.State.Following;

        /// <summary>A firm's stock (still listed unless <paramref name="live"/> is false), or null for none.</summary>
        public Stock FirmStock(string place, bool live = true)
        {
            return Running ? _engine.FirmByPlace(place, live) : null;
        }

        /// <summary>The player's firms listed now.</summary>
        public int ListedFirms(List<Stock> into)
        {
            into.Clear();
            return Running ? _engine.MineFirms(into) : 0;
        }

        /// <summary>A firm on the market with its own price, shares and lock-up. Nothing is paid or raised here; null, with the reason, when it cannot be.</summary>
        public Stock ListFirm(in FirmListing r, out FirmWhy why)
        {
            why = FirmWhy.Off;
            if (!FirmsOpen)
            {
                return null;
            }
            FinishMoment();
            Stock s = _engine.ListFirm(r, out why);
            if (s != null && why == FirmWhy.None)
            {
                Logger?.Info("Stock market: " + s.Name + " (" + s.Ticker + "), a firm of the player's, is on the market at " + s.Last + " cents with " + s.Shares + " shares (" + s.Own.Founder
                             + " the founder's, " + s.Own.Float + " the public's), the founder locked up to day " + s.Own.LockDay + ".");
                Changes();
            }
            return s;
        }

        /// <summary>A startup of the city that listed, as a company of the market's: on the host's market or a lone player's only.</summary>
        public Stock ListStartup(in StartupListing r, out FirmWhy why)
        {
            why = FirmWhy.Off;
            if (!Running || _following || GoingPublic.IsGuest || _engine.State.Following)
            {
                return null;
            }
            FinishMoment();
            Stock s = _engine.ListStartup(r, out why);
            if (s != null && why == FirmWhy.None)
            {
                Logger?.Info("Stock market: " + s.Name + " (" + s.Ticker + "), a startup of the city, is on the market at " + s.Last + " cents with " + s.Shares + " shares.");
                Changes();
            }
            return s;
        }

        /// <summary>A firm's stock leaves the market, each share paid out at so many cents; the firm's investors are paid by the tech sector, not here.</summary>
        public bool EndFirm(int stockId, GoneReason reason, int cents)
        {
            if (!FirmsOpen)
            {
                return false;
            }
            FinishMoment();
            bool done = _engine.EndFirm(stockId, reason, cents);
            if (done)
            {
                Changes();
            }
            return done;
        }

        /// <summary>The player's firms on the market that the tech sector no longer lists leave it. Returns how many.</summary>
        public int SweepFirms(IList<string> places)
        {
            if (!FirmsOpen)
            {
                return 0;
            }
            FinishMoment();
            int n = _engine.SweepFirms(places);
            if (n > 0)
            {
                Changes();
            }
            return n;
        }

        /// <summary>Shares of a firm that are neither the founder's nor the public's made or cancelled (under nothing).</summary>
        public bool GrowFirm(int stockId, long shares)
        {
            if (!FirmsOpen)
            {
                return false;
            }
            FinishMoment();
            bool done = _engine.GrowFirm(stockId, shares);
            if (done)
            {
                Changes();
            }
            return done;
        }

        /// <summary>The founder's unvested shares of a firm, as of a game day, cancelled; returns how many.</summary>
        public long CancelUnvested(int stockId, int day)
        {
            if (!FirmsOpen)
            {
                return 0L;
            }
            FinishMoment();
            long taken = _engine.CancelUnvested(stockId, day);
            if (taken > 0)
            {
                Changes();
            }
            return taken;
        }

        // The market changed outside its tape: the app shows it, and a Going Public host's guests are given it whole.
        private void Changes()
        {
            Moved();
            SharedCity.Changed('M');
        }

        private void HookFirms()
        {
            _engine.FirmDealt = FirmDealtHeard;
            _engine.CompanyDealt = CompanyDealtHeard;
            _engine.CompanyTaken = CompanyTakenHeard;
        }

        // The founder dealt in the whole company's shares on this machine, or paid for a larger share of it: the tax return follows.
        private void CompanyDealtHeard(string name, long shares, int cents, long money, long fee, long held)
        {
            Action<string, long, int, long, long, long> told = MarketBridges.CompanyDealt;
            if (told == null)
            {
                return;
            }
            try
            {
                told(name, shares, cents, money, fee, held);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A bid for the whole company won on this machine: the tax return notes the founder's gain.
        private void CompanyTakenHeard(string name, long founder, long total, int cents, long money)
        {
            Action<string, long, long, int, long> told = MarketBridges.CompanyTaken;
            if (told == null)
            {
                return;
            }
            try
            {
                told(name, founder, total, cents, money);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The founder dealt in a firm's shares on this machine: the tech sector follows its cap table.
        private void FirmDealtHeard(int stockId, long shares, int cents, long money, long fee)
        {
            Action<int, long, int, long, long> told = MarketBridges.FirmDealt;
            if (told == null)
            {
                return;
            }
            try
            {
                told(stockId, shares, cents, money, fee);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
