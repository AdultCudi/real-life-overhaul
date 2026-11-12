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

    // The call of a repair, and an applicant for an empty unit: the two decisions of Property, read from the sample world.
    private static void EstateDecide(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w, string state)
    {
        m.Shape = EstateShape.Decide;
        m.Card = true;
        var d = new EstateDecision();
        m.Decision = d;
        if (state == "call")
        {
            EBuilding b = w.Buildings[0];
            EUnit u = b.Units[2];
            m.Tab = 2;
            d.Kicker = Fx(W(t, "reallife_ui_estate_dec_call_kicker", "Repair call, {place}"), "place", UnitLabel(t, b, 2));
            d.Title = Capital(FaultPhraseOf(t, u.Call));
            d.Body = Fx(W(t, "reallife_ui_estate_dec_call_body", "{name} reported it. How do you answer?"), "name", u.Tenant.Name.Split(' ')[0]);
            d.Portrait = u.Tenant.Name;
            d.PortraitLines = new[] { JobLine(t, u.Tenant.Job), u.Tenant.Age.ToString(), StateWord(t, u, out UiRole tone) };
            d.FactLabels = new[] { W(t, "reallife_ui_estate_ch_contractor", "A contractor"), W(t, "reallife_ui_estate_ch_yourself", "Yourself, materials"),
                W(t, "reallife_ui_estate_fig_reported", "Reported"), W(t, "reallife_ui_estate_fig_grace", "The tenant minds after") };
            d.FactValues = new[] { Usd(u.CallCost), Usd(u.CallCost * 35 / 100), Fx(W(t, "reallife_ui_estate_days_ago", "{days} days ago"), "days", (Today - u.CallDay).ToString()),
                Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "3") };
            d.Choices = new[]
            {
                Choice(runs, W(t, "reallife_estate_btn_contractor", "Send a contractor"), new[] { Said(W(t, "reallife_ui_estate_dec_next_morning", "Put right the next morning")) }),
                Choice(runs, W(t, "reallife_estate_btn_yourself", "Do it yourself"), new[] { Said(Fx(W(t, "reallife_ui_estate_call_minutes", "Doing it yourself takes about {minutes} minutes at the building's door."), "minutes", "120")) })
            };
            d.Start = 1;
            d.Buttons = new[] { NotNow(t) };
            return;
        }
        // The applicant: a person who wants the empty unit, as the live page says it, by what they are and how they have paid before.
        EBuilding shop = w.Buildings[5];
        EUnit empty = shop.Units[0];
        EApplicant a = empty.Applicants[0];
        m.Tab = 1;
        d.Kicker = Fx(W(t, "reallife_ui_estate_dec_app_kicker", "Applicant, {district}"), "district", shop.District);
        d.Title = UnitLabel(t, shop, 0);
        d.Body = Fx(W(t, "reallife_ui_estate_dec_app_body", "Their offer stands until day {day}, for a lease of {days} days. Do you let the unit to them?"), "day", a.Lapse.ToString(), "days", a.Term.ToString());
        d.Portrait = TenantWords(t, shop.Use, a.Kind);
        d.PortraitLines = new[] { RecordWord(t, a.Record) };
        d.FactLabels = new[] { W(t, "reallife_ui_estate_fig_their_offer", "Their offer a day"), W(t, "reallife_ui_estate_ch_market_rent", "The market's rent"),
            W(t, "reallife_ui_estate_fig_of_market", "Of the market's rent"), W(t, "reallife_ui_estate_fig_deposit", "Deposit") };
        d.FactValues = new[] { Usd(a.Offer), Usd(empty.MarketDay), Percent(t, (a.Offer * 1000 / empty.MarketDay + 5) / 10), Usd(a.Offer * 6) };
        d.Choices = new[]
        {
            Choice(runs, W(t, "reallife_estate_btn_accept", "Accept"), new[]
            {
                Said(W(t, "reallife_ui_estate_sh_pays", "Pays on its day") + " " + Percent(t, 100 - (100 - a.Record) / 4), 1), Said(W(t, "reallife_ui_estate_sh_catches", "Catches up when late") + " " + Percent(t, CatchUp(a.Record)), 1),
                Said(W(t, "reallife_ui_estate_col_record", "Record") + " " + Percent(t, a.Record))
            }),
            Choice(runs, W(t, "reallife_estate_btn_refuse", "Refuse"), new[]
            {
                Said(Fx(W(t, "reallife_estate_state_empty", "Empty {days} days"), "days", empty.VoidDays.ToString())),
                Said(Fx(W(t, "reallife_ui_estate_dec_app_others", "{count} others asked for it"), "count", Math.Max(0, empty.Applicants.Count - 1).ToString()))
            })
        };
        d.Start = 1;
        d.Buttons = new[] { NotNow(t) };
    }

    // An offer on a car of the showroom: the buyer at the left, the deal's figures, the counter to set and what each answer does. The first offer of the sample world.
    private static void DealerDecide(UiWords t, EstateModel m, List<Func<bool>> runs, RealLifeOverhaul.Economy.Dealership.DealerState s)
    {
        RealLifeOverhaul.Economy.Dealership.DealerYard yard = s.Yards[0];
        RealLifeOverhaul.Economy.Dealership.DealerOffer deal = yard.Deals[0];
        RealLifeOverhaul.Economy.Dealership.StockCar car = yard.Car(deal.Car);
        int offer = RealLifeOverhaul.Economy.Dealership.DealerRules.Price(yard, deal, car), margin = offer - car.Cost;
        m.Shape = EstateShape.Decide;
        m.Card = true;
        var d = new EstateDecision();
        m.Decision = d;
        d.Kicker = Fx(W(t, "reallife_ui_dealer_dec_deal_kicker", "Offer, {showroom}"), "showroom", yard.Name);
        d.Title = car.Type;
        d.Body = Fx(W(t, "reallife_ui_dealer_dec_deal_offers", "{name} made an offer for your {car}."), "name", deal.Customer, "car", car.Type)
            + " " + Fx(W(t, deal.Looks > 1 ? "reallife_ui_dealer_deal_looked" : "reallife_ui_dealer_deal_looked_once", "They looked at the car {times} times before they made it."), "times", deal.Looks.ToString());
        d.Portrait = deal.Customer;
        d.PortraitLines = new[] { DProfile(t, deal.Profile), W(t, "reallife_dealer_state_waiting", "Waiting") };
        d.FactLabels = new[] { W(t, "reallife_ibox_offer", "Offer"), W(t, "reallife_ibox_asking", "Asking"), W(t, "reallife_ui_dealer_l_cost", "Cost, all in"), W(t, "reallife_ui_dealer_l_margin_offer", "Margin at the offer") };
        d.FactValues = new[] { Usd(offer), Usd(car.Asking), Usd(car.Cost), (margin >= 0 ? "+" : "-") + Usd(Math.Abs(margin)) };
        d.FactInks = new[] { UiRole.None, UiRole.None, UiRole.None, margin >= 0 ? UiRole.OkText : UiRole.DangerText };
        int steps = RealLifeOverhaul.Economy.Dealership.DealerRules.CounterSteps(deal, car.Asking), at = Math.Max(0, steps - 2);
        int counter = RealLifeOverhaul.Economy.Dealership.DealerRules.CounterAt(deal, car.Asking, at), rivalAt;
        int cheapest = RealLifeOverhaul.Economy.Dealership.RivalRules.Cheapest(s, car.Type, out rivalAt);
        d.Steps = new[]
        {
            CounterStep(runs, W(t, "reallife_ui_dealer_your_counter", "Your counter"), Usd(counter),
                cheapest > 0 && rivalAt >= 0 ? Fx(W(t, "reallife_ui_dealer_rival_asks", "{name} asks {price} for the same model."), "name", RivalName(rivalAt), "price", Usd(cheapest)) : null)
        };
        var accept = new List<UiConsequence>();
        if (deal.WantsFinance)
        {
            accept.Add(Said(Fx(W(t, "reallife_dealer_deal_finance", "They would put down {deposit}% and pay over {weeks} weeks. Their rating is {grade}."), "deposit", deal.Deposit.ToString(),
                "weeks", ((deal.Term + RealLifeOverhaul.Economy.Dealership.DealerCatalogue.WeekDays - 1) / RealLifeOverhaul.Economy.Dealership.DealerCatalogue.WeekDays).ToString(), "grade", GradeLetter(deal.Score))));
        }
        var choices = new List<EstateChoice>
        {
            Choice(runs, W(t, "reallife_dealer_button_accept", "Accept"), accept.ToArray()),
            Choice(runs, W(t, "reallife_dealer_button_counter", "Counter"), new[] { Said(Fx(W(t, "reallife_ui_dealer_counters_left", "{left} counters left"), "left", (RealLifeOverhaul.Economy.Dealership.DealerCatalogue.CountersMost - deal.Counters).ToString())) })
        };
        if (deal.WantsDrive && !deal.Drove)
        {
            choices.Add(Choice(runs, W(t, "reallife_dealer_button_drive", "Test drive"), new[] { Said(W(t, "reallife_dealer_deal_wants_drive", "They want to drive it first.")) }));
        }
        choices.Add(Choice(runs, W(t, "reallife_dealer_button_decline", "Decline"), new[] { Said(Fx(W(t, "reallife_ui_dealer_dec_leaves", "{name} walks away"), "name", deal.Customer), -1) }));
        d.Choices = choices.ToArray();
        d.Start = choices.Count > 2 ? 2 : 1;   // the test drive the buyer wants commits to nothing, else the counter
        d.Buttons = new[] { NotNow(t) };
    }
}
