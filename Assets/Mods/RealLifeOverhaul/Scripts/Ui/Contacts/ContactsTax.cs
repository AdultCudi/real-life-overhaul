using System;
using System.Collections.Generic;
using Entities;
using Helpers;
using Localizor;
using Streets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The tax office's notices in the Contacts thread as a ledger in their bubble, read from the message's own data in the
    /// order the game's own table (TaxesMessage.SetData) lays them out: the heading's words, income, deductions with their
    /// subtotal, real estate with its subtotal and the grand total, every figure from the game's own calculation and
    /// formatting (TaxCalculationHelper); a levy's seized valuables and the back taxes still owed. Cost: called on a layout
    /// of a thread that holds such a notice.
    /// </summary>
    public static class ContactsTax
    {
        /// <summary>Fills a message from the tax office's notice: its words over the ledger, the ledger, the words under it.</summary>
        public static void Fill(TextMessage msg, ref UiMessageModel m)
        {
            AdditionalMessageData data = msg.additionalData;
            var lines = new List<UiLedgerLine>(24);
            if (msg.messageKey == "ba:messagetype_taxes")
            {
                Taxes taxes = data != null ? data.taxes : null;
                if (taxes == null)
                {
                    m.Text = ContactsRules.Plain(LocalizorManager.GetLocalization(msg.messageKey, (object)msg.messageData));
                    return;
                }
                int days = SaveGameManager.Current != null ? SaveGameManager.Current.gameVariables.daysPerYear : 0;
                m.Text = ContactsRules.Plain(LocalizorManager.GetLocalization("contacts_taxes_message_heading", (object)new { startingDay = taxes.day - days + 1, endingDay = taxes.day }));
                m.TableFoot = ContactsRules.Plain(LocalizorManager.GetLocalization("contacts_taxes_message_ending",
                    (object)new { lastPayingDay = taxes.day + 20, address = TaxHelper.GetIRSAddress().ToFormattedString() }));
                Return(taxes, lines);
            }
            else
            {
                m.Text = ContactsRules.Plain(LocalizorManager.GetLocalization("contacts_taxes_repossession_message_heading"));
                m.TableFoot = ContactsRules.Plain(LocalizorManager.GetLocalization("contacts_taxes_repossession_message_ending"));
                Levy(data != null ? data.listOfLabels : null, data != null ? data.backTaxesOwed : 0f, lines);
            }
            m.Table = lines.ToArray();
        }

        // The return, section by section as the game's own table: income, deductions, real estate, the grand total.
        private static void Return(Taxes taxes, List<UiLedgerLine> lines)
        {
            TaxCalculationResult calc = TaxCalculationHelper.Calculate(taxes);
            if (taxes.businessesIncome.Count > 0 || taxes.subtotalGamblingWinnings > 0f)
            {
                Heading(lines, "contacts_taxes_income");
                foreach ((string, float) item in taxes.businessesIncome)
                {
                    if (item.Item2 != 0f)
                    {
                        Line(lines, Label(item.Item1), TaxCalculationHelper.ToCurrencyFormat(TaxCalculationHelper.RoundCurrency(item.Item2)), UiLedgerKind.Line);
                    }
                }
                if (taxes.subtotalGamblingWinnings > 0f)
                {
                    Line(lines, Label("contacts_taxes_gambling"), TaxCalculationHelper.ToCurrencyFormat(TaxCalculationHelper.RoundCurrency(taxes.subtotalGamblingWinnings)), UiLedgerKind.Line);
                }
            }
            if (taxes.deductibleExpenses.Count > 0)
            {
                Heading(lines, "contacts_taxes_tax_deductions");
                var sorted = new List<(string, float)>(taxes.deductibleExpenses);
                sorted.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.CurrentCultureIgnoreCase));   // as the game sorts them
                foreach ((string, float) item in sorted)
                {
                    Line(lines, Label(item.Item1), TaxCalculationHelper.ToCurrencyFormat(TaxCalculationHelper.RoundCurrency(item.Item2)), UiLedgerKind.Line);
                }
                Line(lines, Label("common_subtotal"), TaxCalculationHelper.ToCurrencyFormat(calc.DeductibleExpenses), UiLedgerKind.Subtotal);
            }
            if (taxes.estateTaxes.Count > 0)
            {
                Heading(lines, "contacts_taxes_real_estate");
                foreach ((string, float) item in taxes.estateTaxes)
                {
                    Line(lines, Label(item.Item1), TaxCalculationHelper.ToCurrencyFormat(TaxCalculationHelper.RoundCurrency(item.Item2)), UiLedgerKind.Line);
                }
                Line(lines, Label("common_subtotal"), TaxCalculationHelper.ToCurrencyFormat(calc.RealEstateTaxes), UiLedgerKind.Subtotal);
            }
            Heading(lines, "contacts_taxes_grand_total");
            Line(lines, Label("contacts_taxes_income"), TaxCalculationHelper.ToCurrencyFormat(calc.TotalIncome), UiLedgerKind.Line);
            Line(lines, Label("contacts_taxes_tax_deductions"), "-" + TaxCalculationHelper.ToCurrencyFormat(calc.DeductibleExpenses), UiLedgerKind.Line);
            Line(lines, Label("contacts_taxes_taxable_income"), TaxCalculationHelper.ToCurrencyFormat(calc.TaxableIncome), UiLedgerKind.Subtotal);
            Line(lines, Label("contacts_taxes_tax_rate"), taxes.taxPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%", UiLedgerKind.Line);
            Line(lines, Label("contacts_taxes_income_tax"), TaxCalculationHelper.ToCurrencyFormat(calc.IncomeTax), UiLedgerKind.Line);
            Line(lines, Label("contacts_taxes_real_estate_tax"), TaxCalculationHelper.ToCurrencyFormat(calc.RealEstateTaxes), UiLedgerKind.Line);
            Line(lines, Label("contacts_taxes_total_tax_due"), TaxCalculationHelper.ToCurrencyFormat(calc.TotalTax), UiLedgerKind.Total);
        }

        // A levy: what was seized, then the back taxes still owed.
        private static void Levy(List<string> seized, float owed, List<UiLedgerLine> lines)
        {
            if (seized != null && seized.Count > 0)
            {
                Heading(lines, "contacts_taxes_repossessed_valuables");
                foreach (string item in seized)
                {
                    Line(lines, Label(item), null, UiLedgerKind.Line);
                }
            }
            if (owed > 0f)
            {
                if (lines.Count > 0)
                {
                    lines.Add(new UiLedgerLine { Kind = UiLedgerKind.Gap });
                }
                Line(lines, ContactsRules.Plain(LocalizorManager.GetLocalization("contacts_taxes_back_taxes_still_owed",
                    (object)new { amount = TaxCalculationHelper.ToRoundedCurrencyFormat(owed) })), null, UiLedgerKind.Total);
            }
        }

        private static void Heading(List<UiLedgerLine> lines, string key) => lines.Add(new UiLedgerLine { Kind = UiLedgerKind.Heading, Label = ContactsRules.Label(Label(key)) });

        private static void Line(List<UiLedgerLine> lines, string label, string value, UiLedgerKind kind) =>
            lines.Add(new UiLedgerLine { Kind = kind, Label = ContactsRules.Label(label), Value = value });

        // A label the game keeps as a key or as words (a business's name), as its own table shows it.
        private static string Label(string label) =>
            string.IsNullOrEmpty(label) ? string.Empty : LocalizorManager.IsLocalizedKey(label) ? ContactsRules.Plain(LocalizorManager.GetLocalization(label)) : label;
    }
}
