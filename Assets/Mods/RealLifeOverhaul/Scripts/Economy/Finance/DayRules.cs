using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a transaction is to the day's result the day-end panel and the top bar show.</summary>
    public enum DayPart
    {
        /// <summary>Money the day's result takes in: the game's own figure does not hold it.</summary>
        Counted = 0,
        /// <summary>Money the game's own figure holds already (a business's statement, the game's loan, wage, parking, insurance and rent lines): not counted a second time.</summary>
        Held = 1,
        /// <summary>A capital move that is no profit or loss: a loan's payout, debts consolidated, shares bought or sold, a deposit, money between the player's own accounts.</summary>
        Transfer = 2
    }

    /// <summary>The day's result in the numbers: the game's figure, what the mod's money adds to it, and how many transactions went each way.</summary>
    public struct DayFigure
    {
        public int Day;
        /// <summary>The game's figure for the day: FinancialSummary.totalProfit, after the day's statement was brought up to date.</summary>
        public float Game;
        /// <summary>The net of the transactions counted: what the mod's money adds to the game's figure.</summary>
        public float Extra;
        public int Counted, Held, Left;

        /// <summary>The game's figure with the mod's money: the day's result.</summary>
        public float Mod => Game + Extra;
        public int Total => Counted + Held + Left;
    }

    /// <summary>One day's transactions sorted into the three parts, as the game-facing walk adds them (no allocation: a struct kept by the caller).</summary>
    public struct ResultTally
    {
        public float Extra, LeftAmount;
        public int Counted, Held, Left;

        public void Add(string type, float amount, bool held)
        {
            switch (DayRules.Of(type, held))
            {
                case DayPart.Transfer:
                    Left++;
                    LeftAmount += amount;
                    break;
                case DayPart.Held:
                    Held++;
                    break;
                default:
                    Counted++;
                    Extra += amount;
                    break;
            }
        }

        public DayFigure Figure(int day, float game)
        {
            return new DayFigure { Day = day, Game = game, Extra = Extra, Counted = Counted, Held = Held, Left = Left };
        }
    }

    /// <summary>
    /// The day's result: the game's profit or loss for yesterday, which holds the businesses' statements, the homes' rent and the game's own loan, wage,
    /// parking and insurance lines, with the rest of the money the player's cash moved by that day, as the Finance app's Cash flow tab counts a day
    /// (<see cref="CashCategory"/>'s types). A transaction is one of three things: held by the game's figure already (decided by the game-facing walk,
    /// <see cref="GameBooks.Result"/>), a transfer (this table), or counted. Transfers are EconoView's Investments category and the loans' principal:
    /// the cash a loan, a credit line or a finance deal pays out, debts a consolidation pays off, shares, bonds, options and futures bought and sold, a
    /// deposit held and given back, a building, boat or vehicle bought or sold, money lent and paid back, and the capital lines of BizLines. What a
    /// loan or a share costs or pays (interest, a fee, a commission, a dividend, a payment on what is owed) is counted, as the game counts its own loan
    /// payment. Game-free; EconomyCheck runs it. Nothing here allocates once built.
    /// </summary>
    public static class DayRules
    {
        private const string Mod = "reallife_transaction_", Game = "ba:transaction_";

        /// <summary>The game's own transfers: a loan paid out or paid off, a side investment and its payout, a building, boat or vehicle
        /// bought or sold, a deposit and its return, and the game's cheat and repair lines.</summary>
        public static readonly string[] GameTransfers =
        {
            "loanpayout", "loanpayoff", "investment", "investmentpayout", "buildingbought", "buildingsold", "boatbought", "boatsold", "vehiclebought",
            "deposit", "depositreturn", "depositreturnfurniture", "cheat", "compatibilityfix"
        };

        /// <summary>The mod's transfers (its keys after "reallife_transaction_").</summary>
        public static readonly string[] ModTransfers =
        {
            // shares, bonds, options and futures, the reserves and the offers of a listed company
            "shares_bought", "shares_sold", "shares_buyback", "shares_bond_bought", "shares_bond_sold", "shares_bond_repaid", "shares_option_bought", "shares_option_sold",
            "shares_option_paid", "shares_future_margin", "shares_future_back", "shares_deposit", "shares_withdrawal", "shares_reserve_in", "shares_reserve_out",
            "shares_issue", "shares_offer", "shares_underwriting", "shares_holders", "shares_holders_back", "shares_private", "shares_proceeds", "shares_takeover",
            "shares_takeover_kept", "shares_rival_takeover", "shares_payout", "shares_shortfall", "shares_short_swing", "shares_worked_back", "shares_worked_held",
            "shares_worked_sold", "investment", "venture_invested", "venture_returned", "venture_buy_out",
            // cash a loan, a card, an overdraft or a finance deal pays out, and the debts a consolidation pays off
            "consolidation_paid_out", "consolidation_paid_off", "credit_drawn", "overdraft_cover", "finance_advance", "finance_returned", "private_loan", "private_loan_taken",
            "repossession_surplus",
            // money lent and paid back, or borrowed from a friend
            "family_loan", "social_loan", "social_loan_back", "roommate_loan", "roommate_loan_back", "loan_friend", "social_borrowed", "repaid",
            // a deposit held and given back
            "util_deposit", "util_deposit_back", "civic_office_deposit", "civic_office_deposit_back", "estate_hire_deposit",
            // a hotel or a school bought or sold
            "estate_hotel_buy", "estate_hotel_sold", "school_buy", "school_sell"
        };

        /// <summary>The keys of EconoView's Investments and Loans categories that count: what a loan, a card or a share costs or pays, not the capital.</summary>
        public static readonly string[] ModCounted =
        {
            "shares_commission", "shares_dividend", "shares_dividend_owed", "shares_dividend_tax", "shares_borrow_fee", "shares_margin_interest", "shares_bot_fee",
            "shares_bot_share", "shares_bond_paid", "venture_share",
            "arrangement", "bankruptcy", "consolidation_fee", "credit_payment", "finance_charge", "interest", "late_fee", "loan_fee", "private_loan_paid", "study_loan",
            "takings_diverted", "social_repay"
        };

        private static readonly HashSet<string> Transfers = Build();

        private static HashSet<string> Build()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < GameTransfers.Length; i++)
            {
                set.Add(Game + GameTransfers[i]);
            }
            for (int i = 0; i < ModTransfers.Length; i++)
            {
                set.Add(Mod + ModTransfers[i]);
            }
            return set;
        }

        /// <summary>True for a type that is a capital move, no profit or loss: listed here, or a capital line of the businesses the mod runs (BizLines).</summary>
        public static bool IsTransfer(string type)
        {
            return type != null && (Transfers.Contains(type) || BizLines.Of(type) == BizLine.Capital);
        }

        /// <summary>What a transaction of a type is to the day's result: a transfer is left out whoever holds it, a held one is the game's already, the rest is counted.</summary>
        public static DayPart Of(string type, bool held)
        {
            return IsTransfer(type) ? DayPart.Transfer : held ? DayPart.Held : DayPart.Counted;
        }

        /// <summary>
        /// The offline test of the table over a list of the mod's keys: every key of EconoView's Investments or Loans category that no feature of
        /// BizLines holds is in exactly one of the two lists (a transfer, or one that counts), and no key is in both. Returns what is wrong, or null.
        /// </summary>
        public static string Audit(IEnumerable<string> keys)
        {
            var wrong = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var counted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ModCounted.Length; i++)
            {
                counted.Add(Mod + ModCounted[i]);
            }
            foreach (string key in keys)
            {
                if (key == null || !key.StartsWith(Mod, StringComparison.Ordinal) || !seen.Add(key))
                {
                    continue;
                }
                int category = CashCategory.For(key);
                bool capital = category == CashCategory.Investments || category == CashCategory.Loans;
                if (capital && BizLines.Of(key) == BizLine.Foreign && !Transfers.Contains(key) && !counted.Contains(key))
                {
                    wrong.Append("not sorted: ").Append(key).Append(' ');
                }
            }
            foreach (string key in counted)
            {
                if (Transfers.Contains(key))
                {
                    wrong.Append("in both lists: ").Append(key).Append(' ');
                }
            }
            foreach (string key in Transfers)
            {
                if (key.StartsWith(Mod, StringComparison.Ordinal) && !seen.Contains(key))
                {
                    wrong.Append("no such key: ").Append(key).Append(' ');
                }
            }
            return wrong.Length == 0 ? null : wrong.ToString().TrimEnd();
        }

        /// <summary>
        /// The self-check's line for a day: "Day result check (day N): the game's figure X, with the mod's money Y (Z transaction(s) counted, W left out as
        /// transfers): PASS", or FAIL naming what is wrong.
        /// </summary>
        public static string Verdict(DayFigure figure, string problem)
        {
            var text = new StringBuilder(200);
            text.Append("Day result check (day ").Append(figure.Day).Append("): the game's figure ").Append(Whole(figure.Game)).Append(", with the mod's money ")
                .Append(Whole(figure.Mod)).Append(" (").Append(figure.Counted).Append(" transaction(s) counted, ").Append(figure.Left).Append(" left out as transfers): ");
            if (string.IsNullOrEmpty(problem))
            {
                text.Append("PASS");
            }
            else
            {
                text.Append("FAIL (").Append(problem).Append(')');
            }
            return text.ToString();
        }

        private static string Whole(float amount)
        {
            return amount.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// What is wrong with a figure, or null: the game's figure must be the sum of its parts (the day's statement was brought up to date), and no
        /// part of the day may be left unsorted or not a number.
        /// </summary>
        public static string Problem(float game, float parts, DayFigure figure)
        {
            if (float.IsNaN(figure.Extra) || float.IsInfinity(figure.Extra) || float.IsNaN(game) || float.IsInfinity(game))
            {
                return "a figure is not a number";
            }
            if (Math.Abs(game - parts) > 0.05f + Math.Abs(game) * 0.0001f)
            {
                return "the game's figure " + Whole(game) + " is not the sum of its parts, " + Whole(parts);
            }
            return null;
        }
    }
}
