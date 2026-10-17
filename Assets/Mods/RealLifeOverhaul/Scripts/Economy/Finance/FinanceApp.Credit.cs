using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Credit tab: the score, what it is made of and the file's last events; a factor picked says how it stands.</summary>
    public sealed partial class FinanceApp
    {
        private static readonly string[] FactorAbout =
        {
            "reallife_tab_factor_about_payments", "reallife_tab_factor_about_owed", "reallife_tab_factor_about_length", "reallife_tab_factor_about_legal", "reallife_tab_factor_about_applications"
        };

        private int _factor = -1;   // the factor picked; -1 for none

        private string WriteCredit(InfoBox box, CreditScore score)
        {
            _spec = FinanceTables.Credit;
            string kindFactor = FinanceText.T(FinanceTables.CreditKinds[FinanceTables.Factor]), kindEvent = FinanceText.T(FinanceTables.CreditKinds[FinanceTables.Event]);
            for (int f = 0; f < (int)CreditFactor.Count; f++)
            {
                float most = CreditCatalogue.PointsOf((CreditFactor)f);
                int now = Mathf.RoundToInt(score.Factor[f] * most), weight = CreditCatalogue.Weights[f];
                TableRow row = Row("factor:" + f);
                row.Pick = true;
                row.Index = f;
                row.Kind = FinanceTables.Factor;
                row.Set(0, FinanceText.T(CreditCatalogue.FactorKeys[f]));
                row.Set(1, kindFactor);
                row.Set(2, weight + "%", weight);
                // Ordered by the points it costs: the factor that holds the rating back most comes first.
                row.Set(5, FinanceText.T("reallife_tab_points_of", "now", now.ToString(), "most", Mathf.RoundToInt(most).ToString()), now - most,
                    score.Factor[f] < 0.4f ? TableRow.Red : score.Factor[f] < 0.65f ? TableRow.Yellow : TableRow.Plain);
            }
            List<CreditEvent> events = CreditLedger.File.Events;
            int shown = 0;
            for (int i = events.Count - 1; i >= 0 && shown < CreditCatalogue.LedgerShown; i--, shown++)
            {
                CreditEvent e = events[i];
                TableRow row = Row("event:" + i);
                row.Kind = FinanceTables.Event;
                row.Set(0, FinanceText.Event(e));
                row.Set(1, kindEvent);
                row.Set(3, FinanceText.Day(e.Day), e.Day);
                string amount = FinanceText.EventAmount(e);
                if (!string.IsNullOrEmpty(amount))
                {
                    row.Set(4, amount, e.Amount);
                }
                row.Set(5, FinanceText.Effect(e.Effect), e.Effect, e.Effect < 0 ? TableRow.Red : e.Effect > 0 ? TableRow.Green : TableRow.Plain);
            }

            if (_factor >= 0 && _factor < (int)CreditFactor.Count)
            {
                WriteFactor(box, score, (CreditFactor)_factor);
                return "factor:" + _factor;
            }
            box.Title(FinanceText.T("reallife_econ_rating_value", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)));
            box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
            box.Line(FinanceText.T(score.Lends ? "reallife_econ_credit_threshold_ok" : "reallife_econ_credit_threshold_no", "threshold", CreditCatalogue.LendingThreshold.ToString()),
                score.Lends ? CellTone.Green : CellTone.Red);
            TipLines(box, score);
            box.Headline("reallife_econ_credit_factors");
            for (int f = 0; f < (int)CreditFactor.Count; f++)
            {
                float part = score.Factor[f];
                box.RowWords(FinanceText.FactorName((CreditFactor)f), FinanceText.Stands(score, (CreditFactor)f),
                    part >= 0.85f ? CellTone.Green : part >= 0.4f ? CellTone.Plain : CellTone.Yellow);
            }
            if (shown == 0)
            {
                box.Note(FinanceText.T("reallife_econ_row_no_events"));
            }
            WorstLine(box, score);
            Meta.Guide.Point(box, GuideTitle);
            return null;
        }

        // What would raise the rating most, under a headline; nothing if nothing would.
        private void TipLines(InfoBox box, CreditScore score)
        {
            int tips = CreditScoring.Tips(score, _tips);
            if (tips > 0)
            {
                box.Headline("reallife_econ_credit_raise");
                for (int i = 0; i < tips; i++)
                {
                    box.Line(FinanceText.Tip(score, _tips[i]));
                }
            }
        }

        private void WriteFactor(InfoBox box, CreditScore score, CreditFactor factor)
        {
            float most = CreditCatalogue.PointsOf(factor);
            int now = Mathf.RoundToInt(score.Factor[(int)factor] * most);
            float part = score.Factor[(int)factor];
            CellTone tone = part >= 0.85f ? CellTone.Green : part >= 0.4f ? CellTone.Plain : CellTone.Yellow;
            box.Title(FinanceText.T(CreditCatalogue.FactorKeys[(int)factor]));
            box.Chip(FinanceText.Word(part), tone);
            box.Note(FinanceText.T(FactorAbout[(int)factor]));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_tab_factor_stands", FinanceText.Stands(score, factor), tone);
            box.Meter("reallife_tab_factor_points", part, FinanceText.T("reallife_tab_points_of", "now", now.ToString(), "most", Mathf.RoundToInt(most).ToString()), tone);
            box.Row("reallife_tab_factor_weight", CreditCatalogue.Weights[(int)factor] + "%");
            box.Headline("reallife_tab_factor_moves");
            box.Line(FinanceText.Tip(score, factor));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        // The debt that hurts the rating most now: the one furthest behind, else a limit more than half used.
        private static void WorstLine(InfoBox box, CreditScore score)
        {
            CreditFile file = CreditLedger.File;
            string name = null;
            int misses = 0;
            float used = 0.5f;
            bool behind = false;
            for (int i = 0; i < file.Loans.Count; i++)
            {
                LoanMark m = file.Loans[i];
                if (m.Ladder.Misses > misses && m.Ref is Loan)
                {
                    misses = m.Ladder.Misses;
                    name = new Debt(m).Name;
                    behind = true;
                }
            }
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (a.Closed || a.Test)
                {
                    continue;
                }
                if (a.Ladder.Misses > misses)
                {
                    misses = a.Ladder.Misses;
                    name = CreditDesk.Name(a);
                    behind = true;
                }
                else if (!behind && ProductCatalogue.Revolving(a.Kind) && a.Agreed > 0 && a.Owed / (float)a.Agreed > used)
                {
                    used = a.Owed / (float)a.Agreed;
                    name = CreditDesk.Name(a);
                }
            }
            if (name == null && !score.Held)
            {
                return;
            }
            box.Headline("reallife_econ_credit_worst");
            if (name != null)
            {
                box.Line(behind
                    ? FinanceText.T("reallife_econ_credit_worst_behind", "account", name, "count", misses.ToString())
                    : FinanceText.T("reallife_econ_credit_worst_used", "account", name, "share", Mathf.RoundToInt(used * 100f) + "%"), behind ? CellTone.Red : CellTone.Yellow);
            }
            if (score.Held)
            {
                box.Line(FinanceText.T("reallife_econ_credit_held", "score", score.Ceiling.ToString()), CellTone.Yellow);
            }
        }
    }
}
