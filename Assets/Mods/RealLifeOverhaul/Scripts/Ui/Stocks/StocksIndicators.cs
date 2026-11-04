namespace RealLifeOverhaul.Ui
{
    /// <summary>Every indicator the Stocks chart can draw: four lines over the price and five panes under it.</summary>
    public enum StocksInd : byte
    {
        Sma20 = 0,
        Sma50,
        Ema20,
        Bollinger,
        Rsi,
        Macd,
        Stochastic,
        Atr,
        Obv,
        Count
    }

    /// <summary>
    /// The indicators as the desk names them: one table of words and colours, so a chip, the picker, the legend line over the
    /// plot and the signals all say the same thing in the same colour. Game-free; the market's own numbers come from the
    /// build. Cost: nothing.
    /// </summary>
    public static class StocksIndicators
    {
        public static readonly string[] Keys =
        {
            "reallife_stk_ind_sma20", "reallife_stk_ind_sma50", "reallife_stk_ind_ema20", "reallife_stk_ind_bollinger", "reallife_stk_ind_rsi",
            "reallife_stk_ind_macd", "reallife_stk_ind_stoch", "reallife_stk_ind_atr", "reallife_stk_ind_obv"
        };

        public static readonly string[] English =
        {
            "SMA 20", "SMA 50", "EMA 20", "Bollinger 20, 2", "RSI 14", "MACD 12 26 9", "Stochastic 14 3", "ATR 14", "OBV"
        };

        public static readonly string[] HintKeys =
        {
            "reallife_stk_ind_sma20_hint", "reallife_stk_ind_sma50_hint", "reallife_stk_ind_ema20_hint", "reallife_stk_ind_bollinger_hint", "reallife_stk_ind_rsi_hint",
            "reallife_stk_ind_macd_hint", "reallife_stk_ind_stoch_hint", "reallife_stk_ind_atr_hint", "reallife_stk_ind_obv_hint"
        };

        public static readonly string[] HintEnglish =
        {
            "Average close of 20 days", "Average close of 50 days", "Average weighted to recent days", "Average with a band of two deviations", "Strength, 0 to 100",
            "Trend against its signal", "Close within its range, 0 to 100", "Average daily range", "Volume that follows the price"
        };

        /// <summary>The colour an indicator keeps on its chip, its legend and its line.</summary>
        public static UiRole Tint(StocksInd ind)
        {
            switch (ind)
            {
                case StocksInd.Sma20: return UiRole.Chart1;
                case StocksInd.Sma50: return UiRole.Chart2;
                case StocksInd.Ema20: return UiRole.Chart4;
                case StocksInd.Bollinger: return UiRole.Chart7;
                case StocksInd.Rsi: return UiRole.Chart5;
                case StocksInd.Macd: return UiRole.Chart1;
                case StocksInd.Stochastic: return UiRole.Chart5;
                case StocksInd.Atr: return UiRole.Chart6;
                default: return UiRole.Chart8;
            }
        }

        /// <summary>The colour of an indicator's second line: MACD's signal, the stochastic's average.</summary>
        public static UiRole Second(StocksInd ind) => UiRole.Chart2;

        /// <summary>Whether an indicator has a pane of its own under the price, rather than a line over it.</summary>
        public static bool IsPane(StocksInd ind) => ind >= StocksInd.Rsi;
    }
}
