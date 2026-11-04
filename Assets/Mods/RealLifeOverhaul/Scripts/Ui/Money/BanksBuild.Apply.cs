using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class BanksBuild
    {
        // ------------------------------------------------------------------------------------------------- Overdraft and Card

        // An overdraft or a card at this bank: the one you hold with its figures and buttons, or what the bank would open and the button that opens it.
        private static void LimitTab(MoneyModel m, BanksFacts f, BankFact bank, UtilitiesWords w, BankCards cards, BankLimit lim, bool overdraft)
        {
            string kind = overdraft ? w.T("reallife_econ_kind_overdraft", "Overdraft") : w.T("reallife_econ_kind_card", "Credit card");
            if (!overdraft)
            {
                var rows = new List<UiDataRow>(8);
                for (int i = 0; i < f.CardMoves.Length; i++)
                {
                    BankMove move = f.CardMoves[i];
                    rows.Add(BankParts.Row(new[]
                    {
                        move.Day, move.What, move.Amount < 0f ? w.Money(-move.Amount) : string.Empty, move.Amount > 0f ? w.Money(move.Amount) : string.Empty
                    }, new[] { UiRole.None, UiRole.None, UiRole.None, move.Amount > 0f ? UiRole.WarnText : UiRole.None }));
                }
                m.Table = BankParts.Table(new[] { w.T("reallife_fin_col_when", "When"), w.T("reallife_ui_bank_col_what", "What"), w.T("reallife_ui_bank_col_paid_in", "Paid in"),
                    w.T("reallife_ui_bank_col_drawn", "Drawn") }, new[] { UiCellKind.Text, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure }, rows, -1,
                    w.T("reallife_bank_row_card_quiet", "Nothing moved on the card in the last 30 days."), w.T("reallife_ui_bank_moves_body", "They are listed here as money moves."), UiIcon.IdCard);
                m.RowIds = new string[rows.Count];
            }
            if (lim.Held && lim.Account != null)
            {
                BankDebt a = lim.Account;
                float used = a.Total > 0f ? a.Owed / a.Total : 0f;
                m.Figures = new[]
                {
                    new UiFigureModel { Label = w.T("reallife_econ_offer_drawn", "Owed on it now:"), Value = w.Money(a.Owed) },
                    new UiFigureModel { Label = w.T("reallife_econ_offer_limit", "Limit:"), Value = w.Money(a.Total) },
                    new UiFigureModel { Label = w.T("reallife_ui_bank_free", "Free to draw"), Value = w.Money(a.Free) },
                    new UiFigureModel { Label = w.T("reallife_ibox_used", "Used"), Value = ((int)Math.Round(used * 100f)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%" }
                };
                DebtCard(m, a, w, cards, false);
                return;
            }
            var lines = new List<UtilitiesLine>(4);
            var actions = new List<UiActionModel>(1);
            string chip = null;
            if (lim.Elsewhere != null)
            {
                lines.Add(BankParts.Say(BankParts.Either(w, overdraft, "reallife_bank_overdraft_elsewhere", "Overdraft: held with {other}. You can hold one.", "reallife_bank_card_elsewhere",
                    "Card: held with {other}. You can hold one, so close it there for one with {bank}.",
                    "other", lim.Elsewhere, "bank", bank.Name)));
                chip = w.T("reallife_ui_bank_elsewhere", "Held elsewhere");
            }
            else if (lim.Refusal != null)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ui_bank_answer", "The bank's answer"), w.T("reallife_ui_bank_no", "No"), UiRole.DangerText));
                lines.Add(BankParts.Say(lim.Refusal, UiRole.DangerText));
                actions.Add(BankParts.Button(w.T("reallife_econ_offer_open", "Open it"), UiButtonKind.Primary, UiIcon.Check, overdraft ? MoneyAct.OpenOverdraft : MoneyAct.OpenCard, true, true));
            }
            else
            {
                lines.Add(BankParts.Pair(w.T("reallife_econ_offer_limit", "Limit:"), w.Money(lim.OfferLimit)));
                lines.Add(BankParts.Pair(w.T("reallife_econ_offer_rate", "Rate a year:"), w.F("reallife_fin_rate_year", "{rate}% a year", "rate", (lim.OfferRate / 100f).ToString("0.#",
                    System.Globalization.CultureInfo.InvariantCulture))));
                lines.Add(BankParts.Say(BankParts.Either(w, overdraft, "reallife_bank_overdraft_offer", "On offer: an overdraft of {limit} at {rate}% a year from {bank}.",
                    "reallife_bank_card_offer", "On offer: a card with a limit of {limit} at {rate}% a year from {bank}.", "bank", bank.Name,
                    "limit", w.Money(lim.OfferLimit), "rate", (lim.OfferRate / 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))));
                actions.Add(BankParts.Button(w.T("reallife_econ_offer_open", "Open it"), UiButtonKind.Primary, UiIcon.Check, overdraft ? MoneyAct.OpenOverdraft : MoneyAct.OpenCard, true));
            }
            if (!string.IsNullOrEmpty(lim.Free))
            {
                lines.Add(BankParts.Say(lim.Free));
            }
            cards.Add(new UtilitiesCard
            {
                Title = kind, Sub = bank.Name, Chip = chip, ChipTone = UiRole.Warn, Lines = lines.ToArray(), Actions = actions.ToArray()
            });
        }

        // ------------------------------------------------------------------------------------------------- Apply

        // The products this bank offers at your rating in a table, the one picked as a form, what the bank checked, and the same asked at the other banks.
        private static void ApplyTab(MoneyModel m, BanksFacts f, BankFact bank, UtilitiesWords w, BankCards cards)
        {
                        var rows = new List<UiDataRow>(7);
            var ids = new List<string>(7);
            int selected = -1;
            for (int i = 0; i < f.Products.Length; i++)
            {
                BankProduct p = f.Products[i];
                string id = "p:" + p.Kind.ToString(System.Globalization.CultureInfo.InvariantCulture);
                selected = m.View.Row == id ? rows.Count : selected;
                rows.Add(BankParts.Row(new[] { p.Name, p.Most > 0 ? w.Money(p.Most) : string.Empty, p.Rate ?? string.Empty, p.Daily ?? string.Empty, p.Fee ?? string.Empty, p.State },
                    BankParts.Tones(6, 5, p.On || p.Open ? UiRole.Ok : UiRole.Danger)));
                ids.Add(id);
            }
            m.Table = BankParts.Table(new[] { w.T("reallife_ui_bank_col_product", "Kind of credit"), w.T("reallife_ui_bank_col_most", "Most"), w.T("reallife_fin_col_rate", "Rate"),
                w.T("reallife_ui_bank_col_daily", "A day"), w.T("reallife_ui_bank_col_fee", "Fee"), w.T("reallife_fin_col_state", "State") },
                new[] { UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Chip }, rows, selected,
                w.F("reallife_bank_row_refused_all", "Nothing on offer. {bank} lends from a rating of {threshold}.", "bank", bank.Name, "threshold",
                    f.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                w.T("reallife_ui_bank_apply_empty", "Nothing is on offer."), UiIcon.Plus);
            m.RowIds = ids.ToArray();
            // The same application at each bank: the rate and the answer, as lines over the table (not a second table: its rows would be a second list for the keys to reach).
            m.Note = w.T("reallife_money_other_banks", "The same application at the other banks.");
            m.Lines = new string[f.Banks.Length];
            m.LineBody = new string[f.Banks.Length];
            m.LineOn = new bool[f.Banks.Length];
            for (int i = 0; i < f.Banks.Length; i++)
            {
                BankFact b = f.Banks[i];
                m.Lines[i] = b.Name;
                m.LineOn[i] = b.Answer;
                m.LineBody[i] = (b.Lends ? w.F("reallife_fin_rate_year", "{rate}% a year", "rate", b.Rate.ToString("0.#",
                    System.Globalization.CultureInfo.InvariantCulture)) + ", " + w.Money(b.Most) + ": " : string.Empty)
                    + (b.Answer ? w.T("reallife_ui_bank_yes", "Yes") : w.T("reallife_ui_bank_no", "No"));
            }
            if (selected < 0)
            {
                m.View.Row = null;
                var intro = new List<UtilitiesLine>(4);
                if (f.Gate != null)
                {
                    intro.Add(BankParts.Say(f.Gate, UiRole.DangerText));
                }
                intro.Add(BankParts.Say(w.F("reallife_bank_apply_intro", "What {bank} offers you today. Pick one to see what it costs.", "bank", bank.Name)));
                if (!string.IsNullOrEmpty(f.Note))
                {
                    intro.Add(BankParts.Say(f.Note));
                }
                intro.Add(BankParts.Say(w.T("reallife_bank_apply_one_desk", "The same offers are in the Finance app, and a loan here counts the same on your credit file.")));
                if (!f.Lends)
                {
                    intro.Add(BankParts.Say(w.F("reallife_bank_refused", "Credit rating {score}: {bank} and every other bank lend only from {threshold}. Raise your rating first.", "bank", bank.Name,
                        "score", f.Score.ToString(System.Globalization.CultureInfo.InvariantCulture), "threshold", f.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            UiRole.DangerText));
                    for (int i = 0; i < f.Tips.Length; i++)
                    {
                        intro.Add(BankParts.Say(f.Tips[i]));
                    }
                }
                cards.Add(new UtilitiesCard { Title = w.F("reallife_ui_bank_apply_at", "Apply at {bank}", "bank", bank.Name), Lines = intro.ToArray() });
            }
            else
            {
                QuoteCard(m, f, bank, w, cards, f.Products[Math.Max(0, selected)]);
            }
            if (f.Checks.Length > 0)
            {
                var lines = new UtilitiesLine[f.Checks.Length];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = BankParts.Check(f.Checks[i].Text, f.Checks[i].Met);
                }
                var all = new UtilitiesLine[lines.Length + 1];
                Array.Copy(lines, all, lines.Length);
                all[lines.Length] = BankParts.Pair(w.T("reallife_money_headroom", "Headroom"), f.Headroom >= 0 ? f.Headroom.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%" : "-");
                cards.Add(new UtilitiesCard { Title = w.T("reallife_ui_bank_checked", "What the bank checked"), Heading = true, Lines = all });
            }
        }

        // The form of the product picked: the amount to step and type, the terms, what it costs, and the one button.
        private static void QuoteCard(MoneyModel m, BanksFacts f, BankFact bank, UtilitiesWords w, BankCards cards, BankProduct product)
        {
            BankQuote q = f.Quote;
            var lines = new List<UtilitiesLine>(8);
            var steps = new List<UtilitiesStep>(4);
            var moves = new List<MoneyStep>(4);
            var actions = new List<UiActionModel>(2);
            int card = cards.Next;
            if (q.Refused != null)
            {
                if (!string.IsNullOrEmpty(q.Record))
                {
                    lines.Add(BankParts.Say(q.Record));
                }
                lines.Add(BankParts.Say(q.Refused, UiRole.DangerText));
                actions.Add(BankParts.Button(ApplyWords(w, product.Kind), UiButtonKind.Primary, UiIcon.Check, ApplyAct(product.Kind), true, true));
                cards.Add(new UtilitiesCard { Title = product.Name, Lines = lines.ToArray(), Actions = actions.ToArray() }, moves.ToArray());
                return;
            }
            if (q.Step3Prev != -1 || q.Step3Next != -1 || !string.IsNullOrEmpty(q.StepLabel3))
            {
                steps.Add(StepOf(q.StepLabel3, q.StepText3, q.Step3Prev, q.Step3Next, q.Step3Field, out MoneyStep step));
                moves.Add(step);
            }
            if (q.HasAmount)
            {
                steps.Add(BankParts.AmountStep(w, w.T("reallife_econ_offer_amount", "Amount"), q.Amount, q.Least, q.Most, MoneyView.FAmount, card, steps.Count, q.AmountHint, out MoneyStep amount));
                moves.Add(amount);
            }
            if (!string.IsNullOrEmpty(q.StepLabel1))
            {
                steps.Add(StepOf(q.StepLabel1, q.StepText1, q.Step1Prev, q.Step1Next, q.Step1Field, out MoneyStep deposit));
                moves.Add(deposit);
            }
            if (!string.IsNullOrEmpty(q.StepLabel2))
            {
                steps.Add(StepOf(q.StepLabel2, q.StepText2, q.Step2Prev, q.Step2Next, q.Step2Field, out MoneyStep term));
                moves.Add(term);
            }
            for (int i = 0; i < q.Labels.Length && i < q.Values.Length; i++)
            {
                lines.Add(BankParts.Pair(q.Labels[i], q.Values[i]));
            }
            if (!string.IsNullOrEmpty(q.Record))
            {
                lines.Add(BankParts.Say(q.Record));
            }
            if (q.HasApproval)
            {
                lines.Add(BankParts.Say(q.ApprovalLine, UiRole.WarnText));
                actions.Add(BankParts.Button(w.T("reallife_econ_approval_cancel", "Cancel the approval"), UiButtonKind.Destructive, UiIcon.Trash, MoneyAct.CancelApproval, false));
            }
            else
            {
                actions.Add(BankParts.Button(ApplyWords(w, product.Kind), UiButtonKind.Primary, UiIcon.Check, ApplyAct(product.Kind), true, !q.CanApply));
            }
            if (!string.IsNullOrEmpty(q.Note))
            {
                lines.Add(BankParts.Say(q.Note));
            }
            if (!string.IsNullOrEmpty(q.Note2))
            {
                lines.Add(BankParts.Say(q.Note2));
            }
            cards.Add(new UtilitiesCard
            {
                Title = product.Name, Chip = q.Rate, ChipTone = UiRole.Ok, Lines = lines.ToArray(), Steps = steps.ToArray(), Actions = actions.ToArray()
            }, moves.ToArray());
        }

        private static UtilitiesStep StepOf(string label, string text, int prev, int next, int field, out MoneyStep move)
        {
            move = new MoneyStep { Act = MoneyAct.None, Field = field, Less = prev, More = next, CanLess = prev >= 0, CanMore = next >= 0 };
            return new UtilitiesStep { Label = label, Value = text, CanLess = prev >= 0, CanMore = next >= 0 };
        }

        private static string ApplyWords(UtilitiesWords w, int kind)
        {
            switch (kind)
            {
                case 1:
                case 2: return w.T("reallife_econ_offer_open", "Open it");
                case 4:
                case 5: return w.T("reallife_econ_finance_approve", "Get approved");
                case 6: return w.T("reallife_econ_consolidate_do", "Consolidate");
                default: return w.T("reallife_money_apply", "Apply");
            }
        }

        internal static MoneyAct ApplyAct(int kind)
        {
            switch (kind)
            {
                case 1: return MoneyAct.OpenOverdraft;
                case 2: return MoneyAct.OpenCard;
                case 3: return MoneyAct.ApplyBusiness;
                case 4:
                case 5: return MoneyAct.ApproveFinance;
                case 6: return MoneyAct.Consolidate;
                default: return MoneyAct.ApplyLoan;
            }
        }

        // ------------------------------------------------------------------------------------------------- Rating

        // Your rating as this bank reads it: each factor graded with its sentence, the terms at each band, and what would raise it.
        private static void RatingTab(MoneyModel m, BanksFacts f, BankFact bank, UtilitiesWords w, BankCards cards)
        {
            int n = f.Factors.Length;
            m.Lines = new string[n];
            m.LineBody = new string[n];
            m.Marks = new int[n];
            for (int i = 0; i < n; i++)
            {
                m.Lines[i] = f.Factors[i].Name;
                m.LineBody[i] = f.Factors[i].Stands;
                m.Marks[i] = f.Factors[i].Part;
            }
            var rows = new List<UiDataRow>(f.Bands.Length);
            for (int i = 0; i < f.Bands.Length; i++)
            {
                BankBand b = f.Bands[i];
                rows.Add(BankParts.Row(b.Refused ? new[] { b.Score, b.Kind, w.T("reallife_bank_row_refused", "Nothing lent"), string.Empty, string.Empty, string.Empty, string.Empty }
                    : new[] { b.Score, b.Kind, b.Loan, b.Rate, b.Card, b.Overdraft, b.Car }, BankParts.Tones(7, 2, b.Refused ? UiRole.DangerText : b.Yours ? UiRole.OkText : UiRole.None)));
            }
            m.Table2Title = w.F("reallife_ui_bank_bands", "What {bank} gives at each rating", "bank", bank.Name);
            m.Table2 = BankParts.Table(new[] { w.T("reallife_ui_bank_col_score", "Score"), w.T("reallife_ui_bank_col_band", "Band"), w.T("reallife_ui_bank_col_loan", "Loan up to"),
                w.T("reallife_fin_col_rate", "Rate"), w.T("reallife_ui_bank_col_card", "Card"), w.T("reallife_ui_bank_col_overdraft", "Overdraft"), w.T("reallife_ui_bank_col_car", "Car finance") },
                null, rows, -1, w.T("reallife_ui_bank_none_title", "No banks in the city"), string.Empty, UiIcon.Star);
            m.RowIds2 = new string[rows.Count];
            m.Note = w.F("reallife_money_threshold", "Lending stops under {threshold}.", "threshold", f.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var lines = new List<UtilitiesLine>(6)
            {
                BankParts.Pair(w.T("reallife_ui_bank_f_rating", "Your rating"), f.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                BankParts.Say(BankParts.Either(w, f.Lends, "reallife_bank_rating_lends", "{bank} lends to you. Banks lend from {threshold}.", "reallife_bank_rating_refuses",
                    "{bank} will not lend to you. No bank lends under {threshold}.", "bank", bank.Name,
                    "threshold", f.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)), f.Lends ? UiRole.OkText : UiRole.DangerText)
            };
            if (!string.IsNullOrEmpty(f.Next))
            {
                lines.Add(BankParts.Say(f.Next));
            }
            for (int i = 0; i < f.Tips.Length; i++)
            {
                lines.Add(BankParts.Say(f.Tips[i]));
            }
            if (!string.IsNullOrEmpty(f.Note))
            {
                lines.Add(BankParts.Say(f.Note));
            }
            cards.Add(new UtilitiesCard
            {
                Title = w.F("reallife_econ_rating_value", "{score} ({grade})", "score", f.Score.ToString(System.Globalization.CultureInfo.InvariantCulture), "grade", f.Grade),
                Sub = bank.Name, Chip = f.Grade, ChipTone = f.GradeTone, Lines = lines.ToArray(),
                Actions = new[] { BankParts.Link(w.T("reallife_bank_tab_apply", "Apply"), UiIcon.Plus, Apply) }
            });
        }
    }
}
