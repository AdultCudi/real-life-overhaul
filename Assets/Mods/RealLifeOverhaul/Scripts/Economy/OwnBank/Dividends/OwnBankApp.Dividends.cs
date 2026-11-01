using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Bank tab's automatic dividends (Economy/OwnBank/Dividends): a row of its list that opens a page, as the level's
    /// and the pay's do. The page chooses how often (the game's own selector) and the share of the profit kept (the game's
    /// own slider, as the pay's share), and shows the next one, the profit kept towards it, the last one and, on a line of
    /// its own, what held it back. Written on a click; dragging the slider writes only its own row.
    /// </summary>
    public sealed partial class OwnBankApp
    {
        private const string DivRow = "b|div";

        // The row under the level and the pay (OwnBankApp.Tiers writes its page when it is picked).
        private void DividendRow(OwnBank b)
        {
            TableRow div = Row(DivRow);
            div.Pick = true;
            div.Set(0, OwnBankText.T("reallife_ob_row_div"));
            div.Set(1, OwnBankText.Money(b.DivAll), b.DivAll / 100.0, TableRow.Plain);
            div.Set(2, b.DivEvery == BankDividends.Off ? OwnBankText.T("reallife_ob_note_div_off")
                : OwnBankText.Fill("reallife_ob_note_div_on", "every", Every(b.DivEvery), "share", OwnBankText.Ratio(b.DivShare)));
        }

        private static string Every(int every)
        {
            int e = Math.Max(BankDividends.Off, Math.Min(BankDividends.Count - 1, every));
            return OwnBankText.Fill(BankDividends.EveryKeys[e], "days", BankDividends.WeekDays.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // The dividends' page: how often and what share, the next one, the last one and what held it back.
        private void WriteDividends(InfoBox box, OwnBank b, BankCity c)
        {
            box.Title(OwnBankText.T("reallife_ob_div_title"));
            box.Subtitle(b.Name);
            box.Headline("reallife_ob_div_head");
            box.Selector(BankDividends.Count, b.DivEvery, Every, i => UiClone.Safe(() =>
            {
                _director.SetDividends(i, b.DivShare);
                Refresh();
            }));
            if (b.DivEvery != BankDividends.Off)
            {
                if (!box.Slider("reallife_ob_div_share", DivSteps, b.DivShare / BankDividends.ShareStep, DivWords, _divSlid))
                {
                    box.Selector(DivSteps + 1, b.DivShare / BankDividends.ShareStep, DivWords, i => UiClone.Safe(() =>
                    {
                        _divSlid(i);
                        Refresh();
                    }));
                }
                box.Row("reallife_ob_div_next", FinanceText.Day(OwnBankRules.DividendDay(b, c.MonthDays, c.YearDays)));
                box.Row("reallife_ob_div_kept", OwnBankText.Signed(b.DivKept), b.DivKept < 0 ? CellTone.Red : CellTone.Plain);
            }
            box.Section("reallife_ob_div_paid");
            if (b.DivDay > 0)
            {
                bool short_ = b.DivPaid < b.DivWanted || b.DivWhy != BankDividends.Paid;
                box.Row("reallife_ob_div_last", OwnBankText.Fill("reallife_ob_div_last_value", "amount", OwnBankText.Money(b.DivPaid), "day", FinanceText.Day(b.DivDay)),
                    short_ ? CellTone.Yellow : CellTone.Plain);
                if (short_)
                {
                    box.Line(DividendShort(b), CellTone.Yellow);   // what held it back, on its own line
                }
            }
            box.Row("reallife_ob_div_all", OwnBankText.Money(b.DivAll));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ob_box_back", Back);
            box.Note(OwnBankText.T("reallife_ob_div_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        /// <summary>What a dividend paid short or not at all came to, and why: the page's line and the director's notice.</summary>
        internal static string DividendWhy(OwnBank b)
        {
            int why = Math.Max(BankDividends.Paid, Math.Min(BankDividends.WhyMost, b.DivWhy));
            return why == BankDividends.Paid ? string.Empty : OwnBankText.T(BankDividends.WhyKeys[why]);
        }

        private static string DividendShort(OwnBank b)
        {
            return b.DivPaid > 0
                ? OwnBankText.Fill("reallife_ob_div_short_line", "amount", OwnBankText.Money(b.DivPaid), "wanted", OwnBankText.Money(b.DivWanted), "why", DividendWhy(b))
                : OwnBankText.Fill("reallife_ob_div_none_line", "why", DividendWhy(b));
        }

        private const int DivSteps = BankDividends.ShareMost / BankDividends.ShareStep;   // the slider's steps: 0% to 100% in fives

        private static readonly Func<int, string> DivWords = i => OwnBankText.Ratio(i * BankDividends.ShareStep);

        private Action<int> _divSlid;   // made once (OwnBankApp's constructor): a step dragged is the dividends' new share
    }
}
