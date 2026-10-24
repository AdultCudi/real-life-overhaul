using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The director's money: costs, payments, the year's figures and the hotels' and car hire's own tax year.</summary>
    public sealed partial class EstateDirector
    {
        // The game's tax year turns on each day that is a multiple of its length (TaxHelper), and the hotels' and car
        // hire's year, which no daily statement holds, is taxed as the game taxes a business. After a jump over a turn,
        // everything counted so far is settled at the first pass.
        private void TradingYear(int day)
        {
            int year = Math.Max(1, _climate.DaysPerYear);
            if (_state.TaxFrom <= 0)
            {
                _state.TaxFrom = EstateRules.TaxYearFirst(day, year);
            }
            int last = EstateRules.TaxYearLast(_state.TaxFrom, year);
            if (day < last)
            {
                return;
            }
            long sales = EstateGame.BusinessSales(last - year + 1, last);
            int profit = _state.TaxTakings - _state.TaxCosts;
            int tax = EstateRules.TradingTax(_state.TaxTakings, _state.TaxCosts, sales, _climate.TaxPercent);
            if (tax > 0)
            {
                Cost(tax, EstateGame.TradingTaxKey, EstateText.T("reallife_estate_trading_year", "day", last.ToString()), null,
                    "tax on the year's hotels and car hire: " + _climate.TaxPercent + "% of " + _state.TaxTakings + " taken less " + _state.TaxCosts + " spent", true, false);
                Tell(NotificationType.Info, "reallife_estate_note_trading_tax", Data("amount", EstateText.Money(tax), "profit", EstateText.Money(profit), "rate", _climate.TaxPercent + "%"),
                    TabHotel, null);
            }
            if (Verbose)
            {
                Logger?.Info("Estate check (the tax year to day " + last + "): the hotels and car hire took " + _state.TaxTakings + " and spent " + _state.TaxCosts + "; the businesses' statements "
                             + sales + "; the game's threshold " + EstateCatalogue.TaxThreshold + "; tax " + tax + " at " + _climate.TaxPercent + "%: PASS");
            }
            _state.TaxTakings = _state.TaxCosts = 0;
            _state.TaxFrom = day / year * year + 1;
        }

        // The hotels' and car hire's money: counted for their own tax (TradingYear), never among the game's deductions.
        private static bool Trading(string key)
        {
            return key == EstateGame.HotelTakingsKey || key == EstateGame.HotelCostsKey || key == EstateGame.HireRentKey || key == EstateGame.HireCostsKey;
        }

        // The year's figures start again with the game's tax year (TradingYear's).
        private void NewYear(int day)
        {
            _state.YearFrom = EstateRules.TaxYearFirst(day, _climate.DaysPerYear);
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                h.RentYear = h.CostYear = h.CallsYear = h.FeesYear = h.FinesYear = 0;
            }
            for (int i = 0; i < _state.Hotels.Count; i++)
            {
                _state.Hotels[i].TakingsYear = _state.Hotels[i].CostsYear = 0;
            }
            _state.Hire.TakingsYear = _state.Hire.CostsYear = 0;
        }

        /// <summary>A cost, deductible unless it is trading (TradingYear); unless 'force', false with nothing taken if cash is short.</summary>
        internal bool Cost(int amount, string key, string name, string place, string how, bool force = false, bool deductible = true)
        {
            bool trading = Trading(key);
            bool taken = EstateGame.Take(amount, key, name, place, deductible && !trading, force);
            if (taken && trading && deductible && amount > 0 && _state != null)
            {
                _state.TaxCosts = (int)Math.Min(int.MaxValue / 4, (long)_state.TaxCosts + amount);
            }
            if (Verbose && amount > 0)
            {
                Logger?.Info("Estate check (transaction, " + key + ", " + name + "): " + amount + (taken ? " taken" : " could not be taken: the cash does not cover it") + "; " + how + ": PASS");
            }
            return taken;
        }

        /// <summary>Money paid to the player; the hotels' and car hire's takings are counted for their own tax.</summary>
        internal void Pay(int amount, string key, string name, string place, string how)
        {
            EstateGame.Give(amount, key, name, place);
            if (Trading(key) && amount > 0 && _state != null)
            {
                _state.TaxTakings = (int)Math.Min(int.MaxValue / 4, (long)_state.TaxTakings + amount);
            }
            if (Verbose && amount > 0)
            {
                Logger?.Info("Estate check (transaction, " + key + ", " + name + "): " + amount + " paid to the player; " + how + ": PASS");
            }
        }
    }
}
