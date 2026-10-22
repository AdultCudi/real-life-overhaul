using System;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Bank tab's level and pay (Economy/OwnBank/Tiers): two rows of its list that open a page each. The level's page
    /// shows what the bank holds against its level's limits, what the next level asks for, ticked as it is met, and the
    /// button that moves it up; the pay's page chooses the owner's pay, a share of each day's profit on the game's own slider
    /// or a fixed sum, with what was paid. Written on a click; dragging the slider writes only its own row.
    /// </summary>
    public sealed partial class OwnBankApp
    {
        private const string LevelRow = "b|level", PayRow = "b|pay";

        // The two rows under the books; true with one of them picked and its page written into the box.
        private bool WriteTierRows(InfoBox box, OwnBank b, BankCity c, ref string selected)
        {
            int tier = OwnBankRules.TierOf(b);
            BankCaps caps = OwnBankRules.Caps(b, c.Level, c.Scale);
            TableRow level = Row(LevelRow);
            level.Pick = true;
            level.Set(0, OwnBankText.T("reallife_ob_row_level"));
            level.Set(1, OwnBankDirector.TierName(tier));
            level.Set(2, Holds(tier, caps));
            TableRow pay = Row(PayRow);
            pay.Pick = true;
            pay.Set(0, OwnBankText.T("reallife_ob_row_pay"));
            pay.Set(1, OwnBankText.Money(b.PaidMonth), b.PaidMonth / 100.0, TableRow.Plain);
            pay.Set(2, PayNote(b));
            if (_pick == LevelRow)
            {
                selected = LevelRow;
                WriteLevel(box, b, c, caps);
                return true;
            }
            if (_pick == PayRow)
            {
                selected = PayRow;
                WritePay(box, b);
                return true;
            }
            return false;
        }

        // What a level may hold, in a sentence.
        private static string Holds(int tier, BankCaps caps)
        {
            return tier >= BankTiers.National
                ? OwnBankText.Fill("reallife_ob_note_level_top", "branches", Whole(caps.Branches))
                : OwnBankText.Fill("reallife_ob_note_level", "branches", Whole(caps.Branches), "loans", OwnBankText.Money(caps.Loans), "deposits", OwnBankText.Money(caps.Deposits));
        }

        private static string PayNote(OwnBank b)
        {
            if (b.PayMode == BankTiers.PayFixedMode && b.PayFixed > 0)
            {
                return OwnBankText.Fill("reallife_ob_note_pay_fixed", "amount", OwnBankText.Money(b.PayFixed));
            }
            if (b.PayMode == BankTiers.PayShareMode && b.PayShare > 0)
            {
                return OwnBankText.Fill("reallife_ob_note_pay_share", "share", OwnBankText.Ratio(b.PayShare));
            }
            return OwnBankText.T("reallife_ob_note_pay_none");
        }

        // The level's page: what the bank holds against its limits, then the next level's conditions and the move up.
        private void WriteLevel(InfoBox box, OwnBank b, BankCity c, BankCaps caps)
        {
            int tier = OwnBankRules.TierOf(b);
            box.Title(OwnBankText.T("reallife_ob_level_title"));
            box.Subtitle(b.Name);
            box.Chip(OwnBankDirector.TierName(tier), tier >= BankTiers.National ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_ibox_sec_now");
            long book = b.Lent + b.InArrears;
            Held(box, "reallife_ob_meter_deposits", b.Savings, caps.Deposits);
            Held(box, "reallife_ob_meter_loans", book, caps.Loans);
            box.Row("reallife_ob_box_branches", OwnBankText.Fill("reallife_ob_figure_of", "now", Whole(b.Branches.Count), "most", Whole(caps.Branches)),
                b.Branches.Count > caps.Branches ? CellTone.Yellow : CellTone.Plain);
            bool over = (caps.Deposits != BankTiers.NoLimit && b.Savings > caps.Deposits) || (caps.Loans != BankTiers.NoLimit && book > caps.Loans) || b.Branches.Count > caps.Branches;
            if (over)
            {
                box.Note(OwnBankText.Fill("reallife_ob_box_level_over", "level", OwnBankDirector.TierName(tier)));
            }
            if (tier >= BankTiers.National)
            {
                box.Note(OwnBankText.T("reallife_ob_box_level_top"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ob_box_back", Back);
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            int next = OwnBankRules.NextTier(b), buy = EndScaleRules.Buying(Settings.EndScale);
            long least = OwnBankRules.TierCapital(next, Prices.Level, b.Closures, buy), capital = OwnBankRules.CapitalNow(b), fee = _director.MoveUpFee();
            int months = BankTiers.RecordMonths[next], held = OwnBankRules.MonthsAtTier(b, OwnBankGame.Today, c.MonthDays);
            BankCaps after = OwnBankRules.CapsOf(next, c.Level, c.Scale);
            box.SectionWords(OwnBankText.Fill("reallife_ob_sec_level_next", "level", OwnBankDirector.TierName(next)));
            box.Check(OwnBankText.Fill("reallife_ob_check_record", "months", Whole(months), "now", Whole(Math.Min(held, months))), held >= months);
            box.Check(OwnBankText.Fill("reallife_ob_check_capital", "least", OwnBankText.Money(least), "now", OwnBankText.Money(capital)), capital >= least);
            box.Check(OwnBankText.T("reallife_ob_check_regulator"), b.Stage == Regulator.Clear && !b.Run);
            box.Row("reallife_ob_box_up_fee", OwnBankText.Money(fee));
            box.Row("reallife_ob_box_up_holds", Holds(next, after));
            Transform buttons = box.ButtonRow();
            if (_director.MoveUpWhy() == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ob_box_up_button", () => AskMoveUp(b.Name, next, fee, after));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_box_back", Back);
            box.Note(OwnBankText.T("reallife_ob_box_level_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // The game's own question before the licence is paid; on yes, the bank moves up and any refusal is said.
        private void AskMoveUp(string bank, int next, long fee, BankCaps after)
        {
            string level = OwnBankDirector.TierName(next), money = OwnBankText.Money(fee), branches = Whole(after.Branches);
            LanguageChangeEventDataHolder body = next >= BankTiers.National
                ? "reallife_ob_confirm_up_body_top".Localize(new { fee = money, level, branches })
                : "reallife_ob_confirm_up_body".Localize(new { fee = money, level, branches, loans = OwnBankText.Money(after.Loans), deposits = OwnBankText.Money(after.Deposits) });
            Ask("reallife_ob_confirm_up_title".Localize(new { bank, level }), body, "reallife_ob_box_up_button", false,
                () => _director.MoveUp(fee, out string why) ? null : why);
        }

        // A meter of what the bank holds against its level's most; a plain row where the level has no limit.
        private static void Held(InfoBox box, string key, long now, long most)
        {
            if (most == BankTiers.NoLimit)
            {
                box.Row(key, OwnBankText.Fill("reallife_ob_figure_no_limit", "now", OwnBankText.Money(now)));
                return;
            }
            float share = most > 0 ? (float)Math.Min(1.0, now / (double)most) : 1f;
            box.Meter(key, share, OwnBankText.Fill("reallife_ob_figure_of", "now", OwnBankText.Money(now), "most", OwnBankText.Money(most)),
                now >= most ? CellTone.Yellow : CellTone.Plain);
        }

        // The pay's page: how the owner is paid (a share on the game's slider, or a fixed sum), and what was paid.
        private void WritePay(InfoBox box, OwnBank b)
        {
            box.Title(OwnBankText.T("reallife_ob_pay_title"));
            box.Subtitle(b.Name);
            box.Headline("reallife_ob_pay_head");
            box.Selector(2, b.PayMode == BankTiers.PayFixedMode ? 1 : 0, i => OwnBankText.T(i == 1 ? "reallife_ob_pay_mode_fixed" : "reallife_ob_pay_mode_share"),
                i => UiClone.Safe(() =>
                {
                    _director.SetPayMode(i == 1 ? BankTiers.PayFixedMode : BankTiers.PayShareMode);
                    Refresh();
                }));
            if (b.PayMode == BankTiers.PayFixedMode)
            {
                box.Row("reallife_ob_pay_fixed_row", OwnBankText.Money(b.PayFixed));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ob_pay_set", () => AskAmount("reallife_ob_ask_pay", b.PayFixed, cents =>
                {
                    string why;
                    return _director.SetPayFixed(cents, out why) ? null : why;
                }));
            }
            else if (!box.Slider("reallife_ob_pay_share", PaySteps, b.PayShare / BankTiers.PayStep, PayWords, _paySlid))
            {
                box.Selector(PaySteps + 1, b.PayShare / BankTiers.PayStep, PayWords, i => UiClone.Safe(() =>
                {
                    _paySlid(i);
                    Refresh();
                }));
            }
            box.Section("reallife_ob_pay_paid");
            box.Row("reallife_ob_pay_month", OwnBankText.Money(b.PaidMonth));
            box.Row("reallife_ob_pay_last", OwnBankText.Money(b.PaidLast));
            box.Row("reallife_ob_pay_all", OwnBankText.Money(b.PaidAll));
            if (b.Test)
            {
                box.Note(OwnBankText.T("reallife_ob_box_test"));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ob_box_back", Back);
            box.Note(OwnBankText.T("reallife_ob_pay_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        private const int PaySteps = BankTiers.PayShareMost / BankTiers.PayStep;   // the slider's steps: 0% to 100% in fives

        private static readonly Func<int, string> PayWords = i => OwnBankText.Ratio(i * BankTiers.PayStep);

        private Action<int> _paySlid;   // made once (OwnBankApp's constructor): a step dragged is the owner's new share
    }
}
