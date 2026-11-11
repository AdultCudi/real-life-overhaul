using RealLifeOverhaul.Ui;

/// <summary>
/// Samples of the Stocks desk's sheets for the market's extras (Scripts/Ui/Stocks/StocksBuild.Extras.cs): units of the index
/// fund, a block trade, the founder's split and the player's bank on its way to the market. Plain figures in the game's
/// words, so the previews and the screen's checks draw every sheet the extras add.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] StockExtrasStates = { "sheet_fund", "sheet_block", "sheet_split", "sheet_bank" };

    public static bool StocksExtras(StocksModel m, UiWords t, string state)
    {
        string back = t("reallife_ui_prompt_back", "Back");
        switch (state)
        {
            case "sheet_fund":
                m.Page = StocksPage.Portfolio;
                Portfolio(m, t);
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_portfolio", "Portfolio"), Title = t("reallife_stx_fund", "Index fund"), Emblem = UiIcon.ChartLineUp, Back = back,
                    Name = "City Index Fund", Big = "$104.20", Sub = "CIF  " + t("reallife_stx_fund_sub", "Every listed company, by the index's weights"),
                    SideLabels = new[]
                    {
                        t("reallife_stx_fund_units", "Units held"), t("reallife_stk_worth", "Worth"), t("reallife_stx_cost", "Cost"), t("reallife_stx_fund_paid", "Paid to you"),
                        t("reallife_stx_fund_fee", "Fee a year"), t("reallife_stk_cash", "Cash")
                    },
                    SideValues = new[] { "1,200", "$124,800", "$118,300", "$2,140", "0.30%", "$86,000" },
                    Fields = new[]
                    {
                        SPick(0, t("reallife_stx_deal", "Deal"), t("reallife_mkt_buy", "Buy")), SStep(1, t("reallife_stx_units", "Units"), "250", true),
                        SFact(t("reallife_stx_fund_price", "Price a unit"), "$104.41"), SFact(t("reallife_stk_sh_fee", "Commission"), "$26"), SFact(t("reallife_stx_total", "In all"), "$26,129"),
                        SNote(t("reallife_stx_fund_about", "A unit is dealt at what its slice of every company costs at that company's own offer or bid, so it is never cheaper than its parts."))
                    },
                    Confirm = t("reallife_stx_fund_buy", "Buy units")
                };
                return true;
            case "sheet_block":
                m.Page = StocksPage.Portfolio;
                Portfolio(m, t);
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_portfolio", "Portfolio"), Title = t("reallife_stx_block", "Block trade"), Emblem = UiIcon.Receipt, Back = back,
                    Name = "Luthor Everything Co.", Big = "$42.10", Sub = "LEC  " + t("reallife_stx_block_holding", "A rival's holding"),
                    SideLabels = new[] { t("reallife_stx_block_held", "You hold"), t("reallife_stk_cash", "Cash") },
                    SideValues = new[] { "120,000  (4.80%)", "$9,400,000" },
                    Fields = new[]
                    {
                        SPick(0, t("reallife_mkt_col_company", "Company"), "Luthor Everything Co."), SPick(1, t("reallife_stx_deal", "Deal"), t("reallife_mkt_buy", "Buy")),
                        SStep(2, t("reallife_mkt_col_shares", "Shares"), "150,000", true), SFact(t("reallife_stx_block_price", "Price a share"), "$44.86"),
                        SFact(t("reallife_stx_block_moves", "Its size moves the price"), "6.48%", -1), SFact(t("reallife_stk_sh_fee", "Commission"), "$6,729"),
                        SFact(t("reallife_stx_total", "In all"), "$6,735,729"), SFact(t("reallife_stx_block_after", "You would hold"), "10.80% / 25.00%"),
                        SNote(t("reallife_stx_block_about", "A holding is told to the market at every 5% it passes. A block is no way round the most one holder may have."))
                    },
                    Confirm = t("reallife_stx_block_buy", "Buy the block")
                };
                return true;
            case "sheet_split":
                m.Page = StocksPage.Listing;
                Listing(m, t);
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_stk_nav_listing", "Listing"), Title = t("reallife_stx_split", "Split the shares"), Emblem = UiIcon.ChartLineUp, Back = back,
                    Name = "Greenmarket Foods", Big = "$212.40", Sub = "GMF",
                    SideLabels = new[] { t("reallife_stx_split_yours", "Your shares"), t("reallife_stx_split_all", "Shares in issue"), t("reallife_stx_split_stake", "Your part") },
                    SideValues = new[] { "800,000", "1,000,000", "80.00%" },
                    Fields = new[]
                    {
                        SPick(0, t("reallife_stx_split_ratio", "New shares for old"), "2 for 1"), SFact(t("reallife_stx_split_price", "A share after"), "$106.20"),
                        SFact(t("reallife_stx_split_mine", "Your shares after"), "1,600,000"), SFact(t("reallife_stx_split_notice", "Market days of notice"), "5"),
                        SNote(t("reallife_stx_split_about", "Every holding, order and price changes in the same moment. Nobody's part of the company or its worth changes."))
                    },
                    Confirm = t("reallife_stx_split_ask", "Ask the board")
                };
                return true;
            case "sheet_bank":
                m.Page = StocksPage.Listing;
                Listing(m, t);
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_stk_nav_listing", "Listing"), Title = t("reallife_stx_bank", "List your bank"), Emblem = UiIcon.Bank, Back = back,
                    Big = "$14,200,000", Sub = t("reallife_stx_bank_worth", "Worth on its own books"),
                    Fields = new[]
                    {
                        SPick(0, t("reallife_stx_bank_float", "Part of the bank offered"), "20.00%"), SFact(t("reallife_stx_bank_retainer", "The advisers' retainer"), "$71,000"),
                        SFact(t("reallife_stx_bank_days", "Market days to the pricing"), "5"),
                        SNote(t("reallife_stx_bank_about", "The offer's money goes into the bank's capital, not your cash. Savers' deposits are no part of its worth."))
                    },
                    Confirm = t("reallife_lst_engage", "Engage advisers")
                };
                return true;
        }
        return false;
    }
}
