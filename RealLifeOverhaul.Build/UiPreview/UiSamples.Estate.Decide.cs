using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The decisions of both apps as the approved desk session's card: one question, the facts that decide it, the choices, and the way out at the foot.
internal static partial class UiSamples
{
    private static EstateChoice Choice(List<Func<bool>> runs, string name, UiConsequence[] lines, bool disabled = false)
    {
        runs.Add(Yes);
        return new EstateChoice { Name = name, Lines = lines, Run = disabled ? -1 : runs.Count - 1, Disabled = disabled };
    }

    private static UiConsequence Said(string text, int tone = 0, int change = 0) => new UiConsequence { Text = text, Tone = tone, Change = change };

    private static UiActionModel NotNow(UiWords t) => new UiActionModel
    {
        Label = W(t, "reallife_ui_estate_not_now", "Not now"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = EstateFace.BackCommand, Arg = 0
    };

    private static UiDecisionStep CounterStep(List<Func<bool>> runs, string label, string value, string hint)
    {
        runs.Add(Yes);
        runs.Add(Yes);
        return new UiDecisionStep { Label = label, Value = value, Hint = hint, CanLess = true, CanMore = true, Less = EstateFace.StepLess, More = EstateFace.StepMore, Arg = runs.Count - 2 };
    }

    private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    // The call of a repair, and an applicant for an empty unit: the two decisions of Property.
    private static void EstateDecide(UiWords t, EstateModel m, List<Func<bool>> runs, string state)
    {
        m.Shape = EstateShape.Decide;
        m.Card = true;
        var d = new EstateDecision();
        m.Decision = d;
        if (state == "call")
        {
            m.Tab = 2;
            d.Kicker = Fx(W(t, "reallife_ui_estate_dec_call_kicker", "Repair call, {place}"), "place", "41 E 37th Street, flat 4A");
            d.Title = Capital(W(t, "reallife_estate_fault_1", "the heating"));
            d.Body = Fx(W(t, "reallife_ui_estate_dec_call_body", "{name} reported it {when}. How do you answer?"), "name", "Sofia Marchetti",
                "when", Fx(W(t, "reallife_ui_estate_days_ago", "{days} days ago"), "days", "5"));
            d.Portrait = "Sofia Marchetti";
            d.PortraitLines = new[] { "Nurse, Murray Hill Clinic", "34", Fx(W(t, "reallife_ui_estate_late_days", "Late {days} days"), "days", "6") };
            d.FactLabels = new[] { W(t, "reallife_ui_estate_ch_contractor", "A contractor"), W(t, "reallife_ui_estate_ch_yourself", "Yourself, materials"),
                W(t, "reallife_ui_estate_fig_reported", "Reported"), W(t, "reallife_ui_estate_fig_grace", "The tenant minds after") };
            d.FactValues = new[] { "$340", "$119", Fx(W(t, "reallife_ui_estate_days_ago", "{days} days ago"), "days", "5"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "3") };
            d.Choices = new[]
            {
                Choice(runs, W(t, "reallife_estate_btn_contractor", "Send a contractor"),
                    new[] { Said(Fx(W(t, "reallife_ui_estate_dec_costs", "Costs {amount}"), "amount", "$340"), UiConsequence.Loss) }),
                Choice(runs, W(t, "reallife_estate_btn_yourself", "Do it yourself"),
                    new[] { Said(Fx(W(t, "reallife_ui_estate_dec_materials", "Materials {amount}"), "amount", "$119"), UiConsequence.Loss),
                        Said(Fx(W(t, "reallife_ui_estate_call_minutes", "Doing it yourself takes about {minutes} minutes at the building's door."), "minutes", "120")) })
            };
            d.Start = 1;
            d.Buttons = new[] { NotNow(t) };
            return;
        }
        // The applicant: a person who wants the empty unit.
        m.Tab = 1;
        d.Kicker = Fx(W(t, "reallife_ui_estate_dec_app_kicker", "Applicant, {district}"), "district", "Uptown");
        d.Title = "1 Dock Street, shop unit";
        d.Body = Fx(W(t, "reallife_ui_estate_dec_app_body", "Their offer stands until day {day}, for a lease of {days} days. Do you let the unit to them?"), "day", "421", "days", "150");
        d.Portrait = "Kofi Mensah";
        d.PortraitLines = new[] { W(t, "reallife_estate_tenant_shops_2", "A baker"), W(t, "reallife_estate_record_good", "Good") };
        d.FactLabels = new[] { W(t, "reallife_ui_estate_fig_their_offer", "Their offer a day"), W(t, "reallife_ui_estate_ch_market_rent", "The market's rent"),
            W(t, "reallife_ui_estate_fig_of_market", "Of the market's rent"), W(t, "reallife_ui_estate_fig_deposit", "Deposit") };
        d.FactValues = new[] { "$133", "$137", "97%", "$798" };
        d.Choices = new[]
        {
            Choice(runs, W(t, "reallife_estate_btn_accept", "Accept"), new[]
            {
                Said(W(t, "reallife_ui_estate_sh_pays", "Pays on its day") + " 91%", 1), Said(W(t, "reallife_ui_estate_sh_catches", "Catches up when late") + " 60%", 1),
                Said(W(t, "reallife_ui_estate_col_record", "Record") + " 62%")
            }),
            Choice(runs, W(t, "reallife_estate_btn_refuse", "Refuse"), new[]
            {
                Said(Fx(W(t, "reallife_estate_state_empty", "Empty {days} days"), "days", "12")), Said(Fx(W(t, "reallife_ui_estate_dec_app_others", "{count} others asked for it"), "count", "2"))
            })
        };
        d.Start = 1;
        d.Buttons = new[] { NotNow(t) };
    }

    // An offer on a car of the showroom: the buyer at the left, the deal's figures, the counter to set and what each answer does.
    private static void DealerDecide(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        m.Shape = EstateShape.Decide;
        m.Card = true;
        var d = new EstateDecision();
        m.Decision = d;
        d.Kicker = Fx(W(t, "reallife_ui_dealer_dec_deal_kicker", "Offer, {showroom}"), "showroom", "Pioneer Motors");
        d.Title = "Meridian GT coupe";
        d.Body = Fx(W(t, "reallife_ui_dealer_dec_deal_offers", "{name} offers {offer} for your {car}, asking {asking}."), "name", "Marcus Bell", "offer", "$82,000", "car", "Meridian GT coupe", "asking", "$84,500")
            + " " + W(t, "reallife_ui_dealer_deal_looked", "They looked at the car {times} times before they made it.").Replace("{times}", "2");
        d.Portrait = "Marcus Bell";
        d.PortraitLines = new[] { W(t, "reallife_dealer_profile_bargain", "A bargain hunter"), W(t, "reallife_dealer_state_waiting", "Waiting") };
        d.FactLabels = new[] { W(t, "reallife_ibox_offer", "Offer"), W(t, "reallife_ibox_asking", "Asking"), W(t, "reallife_ui_dealer_l_cost", "Cost, all in"), W(t, "reallife_ui_dealer_l_margin_offer", "Margin at the offer") };
        d.FactValues = new[] { "$82,000", "$84,500", "$72,300", "+$9,700" };
        d.FactInks = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.OkText };
        d.Steps = new[]
        {
            CounterStep(runs, W(t, "reallife_ui_dealer_your_counter", "Your counter"), "$83,200",
                Fx(W(t, "reallife_ui_dealer_rival_asks", "{name} asks {price} for the same model."), "name", "Halden Motors", "price", "$86,900"))
        };
        d.Choices = new[]
        {
            Choice(runs, W(t, "reallife_dealer_button_accept", "Accept"), new[]
            {
                Said(Fx(W(t, "reallife_ui_dealer_dec_margin", "A margin of {margin}"), "margin", "$9,700"), 1),
                Said(Fx(W(t, "reallife_dealer_deal_finance", "They would put down {deposit}% and pay over {weeks} weeks. Their rating is {grade}."), "deposit", "20", "weeks", "8", "grade", "B"))
            }),
            Choice(runs, W(t, "reallife_dealer_button_counter", "Counter"), new[] { Said(Fx(W(t, "reallife_ui_dealer_counters_left", "{left} counters left"), "left", "2")) }),
            Choice(runs, W(t, "reallife_dealer_button_drive", "Test drive"), new UiConsequence[0]),
            Choice(runs, W(t, "reallife_dealer_button_decline", "Decline"), new[] { Said(Fx(W(t, "reallife_ui_dealer_dec_leaves", "{name} walks away"), "name", "Marcus Bell"), -1) })
        };
        d.Start = 1;
        d.Buttons = new[] { NotNow(t) };
    }
}
