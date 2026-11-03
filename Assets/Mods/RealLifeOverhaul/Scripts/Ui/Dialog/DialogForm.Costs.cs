using System.Collections.Generic;
using System.Globalization;
using AI.Employees.SalaryNegotiation;
using Buildings;
using Buildings.BuildingTypes.Special.PrivateDriverService;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Dialog;
using UnityEngine;
using Vehicles;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a game form's confirm pays and costs, under it in the conversation, from the same figures the game's own
    /// form shows and its dialog then uses: a wholesale contract's first delivery day and its fee on each delivery, an
    /// importer's partnership with the agent's discount, a delivery's goods and slot and its total paid on delivery, a
    /// vehicle paid now or on delivery, a move or an installation and its total paid on that day (a design's discontinued
    /// items left out), a tow, a recruitment campaign's candidates and its price now, a campaign's or a driver's price a
    /// day, an insurer's plan priced next, an offer a day for each employee, a wage and its signing bonus against the
    /// candidate's ask, and an investment from the account today. A choice the confirm needs and the player has not made
    /// says so in the game's own words for it (Select business), in place of any cost. Only the game's own forms are
    /// told here (another mod's form keeps what its dialog told); the bank's loan form tells its own (DialogForm.Loan).
    /// A wage or an offer typed in a Contacts thread is told the same way (Offers). Costs nothing a frame; a little
    /// arithmetic when the form is read.
    /// </summary>
    internal sealed partial class DialogForm
    {
        private void Costs(Transform form, DialogEntry entry, DialogController talk, int heading)
        {
            if (form == null || entry == null || _loanForm != null)
            {
                return;
            }
            if (form.TryGetComponent(out DeliveryContractSettings wholesale))
            {
                // A wholesale store's delivery contract: the goods are set in BizMan; the store's fee comes with each delivery.
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, 1))
                {
                    return;
                }
                Building store = talk != null && talk.contact != null ? BuildingHelper.GetBuilding(talk.contact.Address) : null;
                var settings = store != null && store.SpecialService != null ? store.SpecialService.settings as WholesaleStoreSettings : null;
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_supplied", "business", Chosen(heading, 0), "day", Day(DeliveryHelper.GetNextDeliveryDay())), 0);
                if (settings != null)
                {
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_each_delivery", "fee", Money(settings.deliveryFee)), -1, -1);
                }
                return;
            }
            if (form.TryGetComponent(out ImportPartnershipSettings import))
            {
                // An importer's partnership: the agent buys from the next delivery day, at the discount the game works out from their skill.
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, 1))
                {
                    return;
                }
                EmployeeInstance agent = import.selectedEmployeeInstance;
                string name = agent != null && agent.characterData != null && !string.IsNullOrEmpty(agent.characterData.name) ? agent.characterData.name : Chosen(heading, 0);
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_agent", "buyer", name, "day", Day(DeliveryHelper.GetNextDeliveryDay())), 0);
                if (agent != null && !string.IsNullOrEmpty(agent.id) && EmployeeHelper.GetEmployeeById(agent.id, false) != null)
                {
                    float off = 1f - new ImportPartnership { employeeInstanceId = agent.id }.GetDiscount;
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_discount", "discount", (off * 100f).ToString("0.#", CultureInfo.CurrentCulture)), 1);
                }
                return;
            }
            if (form.TryGetComponent(out DeliveryContractSettingsBase delivery))
            {
                // Furniture or food to a place in a slot: paid when it is delivered.
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, 2))
                {
                    return;
                }
                int count = delivery.itemsToDeliver != null ? delivery.TotalItemsToDeliverAmount : 0;
                if (count <= 0)
                {
                    Line(entry, UiText.T("reallife_ui_dialog_cost_no_goods"), 0);
                    return;
                }
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_items", "count", count.ToString(CultureInfo.CurrentCulture), "slot", Chosen(heading, 1)), 0);
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_on_delivery", "amount", Money(delivery.TotalPrice)), -1, -1);
                return;
            }
            if (form.TryGetComponent(out VehicleContractSettings vehicle))
            {
                // A vehicle bought here is paid now; one ordered to an address is paid as it arrives.
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, vehicle.isDelivery ? 2 : 1))
                {
                    return;
                }
                Line(entry, vehicle.isDelivery ? "reallife_ui_dialog_cost_on_delivery" : "reallife_ui_dialog_cost_now", Total(heading), -1, -1);
                return;
            }
            if (form.TryGetComponent(out MovingServiceContractSettings moving))
            {
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, 3))
                {
                    return;
                }
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_move", "from", Chosen(heading, 0), "to", Chosen(heading, 1), "day", Day(moving.selectedDay)), 0);
                Line(entry, "reallife_ui_dialog_cost_on_the_day", Total(heading), -1, -1);
                return;
            }
            if (form.TryGetComponent(out InteriorInstallationFirmDesignSettings design))
            {
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, 3))
                {
                    return;
                }
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_refit", "place", Chosen(heading, 0), "day", Day(design.selectedDay)), 0);
                if (design.hasDiscontinuedItems)
                {
                    Line(entry, UiText.T("reallife_ui_dialog_cost_discontinued"), -1);
                }
                Line(entry, "reallife_ui_dialog_cost_on_the_day", Total(heading), -1, -1);
                return;
            }
            if (form.TryGetComponent(out AutoTowServiceSettings tow))
            {
                entry.NoCosts(DialogButton.Confirm);
                TowDestinationData where = string.IsNullOrEmpty(tow.optionSelected) ? null : TowDestinationHelper.GetData(tow.optionSelected);
                if (where != null)
                {
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_now", "amount", Money(where.servicePrice)), -1, -1);
                }
                return;
            }
            if (form.TryGetComponent(out RecruitmentSettings recruitment))
            {
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, 2))
                {
                    return;
                }
                if (recruitment.candidatesAmountSlider != null && recruitment.deadlineSlider != null)
                {
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_candidates", "count", Mathf.RoundToInt(recruitment.candidatesAmountSlider.value).ToString(CultureInfo.CurrentCulture),
                        "days", Mathf.RoundToInt(recruitment.deadlineSlider.value).ToString(CultureInfo.CurrentCulture)), 1);
                }
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_now", "amount", Money(recruitment.totalPrice)), -1, -1);
                return;
            }
            if (form.TryGetComponent(out MarketingCampaignSettings marketing))
            {
                entry.NoCosts(DialogButton.Confirm);
                if (Unmade(entry, heading, 1))
                {
                    return;
                }
                if (marketing.marketingTypeNamesSelected == null || marketing.marketingTypeNamesSelected.Count == 0)
                {
                    Line(entry, UiText.T("reallife_ui_dialog_cost_no_campaign"), 0);
                    return;
                }
                Line(entry, "reallife_ui_dialog_cost_a_day", Total(heading), -1, -1);
                return;
            }
            if (form.TryGetComponent(out PrivateDriverContractSettings _))
            {
                entry.NoCosts(DialogButton.Confirm);
                if (!Unmade(entry, heading, 1))
                {
                    Line(entry, "reallife_ui_dialog_cost_a_day", Total(heading), -1, -1);
                }
                return;
            }
            if (form.TryGetComponent(out HealthInsurancePartnershipSettings _))
            {
                entry.NoCosts(DialogButton.Confirm);
                if (!Unmade(entry, heading, 2))
                {
                    Line(entry, UiText.T("reallife_ui_dialog_cost_insurer_next"), 0);
                }
                return;
            }
            if (Offers(form, entry))
            {
                return;
            }
            if (form.TryGetComponent(out BankInvestmentSettings investment))
            {
                entry.NoCosts(DialogButton.Confirm);
                Invest(investment, entry, heading);
            }
        }

        // The bank's investment: no fund offered here, the cash under the bank's least (the form keeps each sum to the cash),
        // the fund still to choose, the least and the most while no sum is typed, a sum under the least, or the sum leaving the
        // account today and the daily auto-invest after it, by the same figures BankDialog.InvestmentRequest reads.
        private void Invest(BankInvestmentSettings investment, DialogEntry entry, int heading)
        {
            int fund = -1;
            for (int i = heading; i < _rows.Count && fund < 0; i++)
            {
                fund = _rows[i].Kind == UiFormKind.Choice ? i : -1;
            }
            int funds = fund >= 0 ? _widgets[fund].Options : 0;
            float cash = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
            const float least = BankInvestmentSettings.MinimumAmount;
            float sum = Sum(investment.amountField), daily = Sum(investment.autoInvestField);
            DialogRules.InvestNote note = DialogRules.Invest(funds, cash, sum, least);
            if (note == DialogRules.InvestNote.NoFund)
            {
                Line(entry, UiText.T("reallife_ui_dialog_cost_invest_nofund"), -1);
                return;
            }
            if (note == DialogRules.InvestNote.Short)
            {
                Line(entry, UiText.Fill("reallife_ui_dialog_cost_invest_short", "cash", Money(Mathf.Max(0f, cash)), "least", Money(least)), -1);
                return;
            }
            if (Unmade(entry, heading, 1))
            {
                return;
            }
            switch (note)
            {
                case DialogRules.InvestNote.Range:
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_invest_range", "least", Money(least), "cash", Money(cash)), 0);
                    break;
                case DialogRules.InvestNote.Least:
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_invest_least", "least", Money(least)), -1);
                    break;
                default:
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_invest", "amount", Money(sum)), -1, -1);
                    if (daily > 0f)
                    {
                        Line(entry, UiText.Fill("reallife_ui_dialog_cost_invest_daily", "amount", Money(daily)), -1, -1);
                    }
                    break;
            }
        }

        // A figure typed into one of the game's number fields, as the game reads it back (its raw value); 0 when none.
        private static float Sum(global::UI.Components.InputField field)
        {
            string raw = field != null ? field.GetRawValue() : null;
            return !string.IsNullOrEmpty(raw) && float.TryParse(raw, out float sum) && sum > 0f ? sum : 0f;
        }

        /// <summary>
        /// What an offer typed into the game's form pays, under its confirm: a wage and its signing bonus beside what the
        /// candidate asked last, or an insurer's price a day for each employee. The conversation's form and a Contacts
        /// thread's both tell it. True when the form is one of the two.
        /// </summary>
        internal static bool Offers(Transform form, DialogEntry entry)
        {
            if (form == null || entry == null)
            {
                return false;
            }
            if (form.TryGetComponent(out SalaryOfferSettings salary))
            {
                entry.NoCosts(DialogButton.Confirm);
                var offer = default(CandidateSalaryOffer);
                if (salary.GetAmounts(ref offer))
                {
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_wage", "wage", Money(offer.hourlyWage), "bonus", Money(offer.signingBonus)), -1, -1);
                }
                if (Asked(out CandidateSalaryOffer ask))
                {
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_asked", "wage", Money(ask.hourlyWage), "bonus", Money(ask.signingBonus)), 0);
                }
                return true;
            }
            if (form.TryGetComponent(out PlayerOfferSettings proposal))
            {
                entry.NoCosts(DialogButton.Confirm);
                float amount = 0f;
                if (proposal.GetAmount(ref amount) && amount > 0f)
                {
                    Line(entry, UiText.Fill("reallife_ui_dialog_cost_per_employee", "amount", Money(amount)), -1, -1);
                }
                return true;
            }
            return false;
        }

        // A line under the confirm: for the player (+1), against them (-1) or neither, and which way their money goes.
        private static void Line(DialogEntry entry, string text, int tone, int change = 0) => entry.Cost(DialogButton.Confirm, text, tone, change);

        // A line of a figure the game's form shows (its total), when it shows one.
        private static void Line(DialogEntry entry, string key, string amount, int tone, int change)
        {
            if (!string.IsNullOrEmpty(amount))
            {
                entry.Cost(DialogButton.Confirm, UiText.Fill(key, "amount", amount), tone, change);
            }
        }

        /// <summary>
        /// True when one of the form's first <paramref name="most"/> choices is not made yet; the line under the confirm then
        /// says so in the game's own words for it (its placeholder: Select business), or names it.
        /// </summary>
        private bool Unmade(DialogEntry entry, int heading, int most)
        {
            int seen = 0;
            for (int i = heading; i < _rows.Count && seen < most; i++)
            {
                UiFormRow r = _rows[i];
                if (r.Kind != UiFormKind.Choice)
                {
                    continue;
                }
                seen++;
                if (!string.IsNullOrEmpty(r.Value))
                {
                    continue;
                }
                string words = !string.IsNullOrEmpty(r.Hint) ? r.Hint
                    : !string.IsNullOrEmpty(r.Label) ? UiText.Fill("reallife_ui_dialog_cost_unchosen", "what", r.Label) : null;
                if (words != null)
                {
                    entry.Cost(DialogButton.Confirm, words, 0);
                }
                return true;
            }
            return false;
        }

        // The words the form's nth choice shows (a business, a slot), as the game lists it.
        private string Chosen(int heading, int nth)
        {
            for (int i = heading; i < _rows.Count; i++)
            {
                if (_rows[i].Kind == UiFormKind.Choice && nth-- == 0)
                {
                    return _rows[i].Value ?? string.Empty;
                }
            }
            return string.Empty;
        }

        // The form's total as the game writes it: its last strong figure.
        private string Total(int heading)
        {
            for (int i = _rows.Count - 1; i >= heading; i--)
            {
                if (_rows[i].Kind == UiFormKind.Figure && _rows[i].Strong && !string.IsNullOrEmpty(_rows[i].Value))
                {
                    return _rows[i].Value;
                }
            }
            return string.Empty;
        }

        // What the candidate asked last in the salary talk the game runs now.
        private static bool Asked(out CandidateSalaryOffer ask)
        {
            ask = default;
            List<CandidateSalaryOffer> offers = CandidateSalaryNegotiationDialog.negotiation != null ? CandidateSalaryNegotiationDialog.negotiation.offers : null;
            for (int i = offers != null ? offers.Count - 1 : -1; i >= 0; i--)
            {
                if (offers[i].fromCandidate)
                {
                    ask = offers[i];
                    return true;
                }
            }
            return false;
        }

        private static string Day(int day) => day.ToString(CultureInfo.CurrentCulture);
    }
}
