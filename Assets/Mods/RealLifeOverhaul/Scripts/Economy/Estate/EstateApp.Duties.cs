using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A landlord's duties in the app: a building's rent roll, satisfaction, calls, the city's inspection and who runs it;
    /// what a flip would bring; a tenant's satisfaction and the arrears tools on a lease; and the repair calls on the
    /// Works tab with their page. Written with the rest of a page, never on a timer.
    /// </summary>
    public sealed partial class EstateApp
    {
        private bool DutiesOn => _director.On(EstatePart.Duties);

        // What the building has cost and what selling it at its worth today would leave: the sum a flip is judged by.
        private static void WriteFlip(InfoBox box, EstateHolding h, int value, EstateClimate c)
        {
            int fee = EstateRules.AgentFee(value, c.Index), tax = EstateRules.GainsTax(value, fee, h.Paid, h.Spent, c.TaxPercent);
            long kept = (long)value - fee - tax, gain = kept - h.Paid - h.Spent;
            box.Line(EstateText.Fill(gain >= 0 ? "reallife_estate_flip_gain" : "reallife_estate_flip_loss", "paid", EstateText.Money(h.Paid), "spent", EstateText.Money(h.Spent),
                "amount", EstateText.Money(kept), "gain", EstateText.Money(Math.Abs(gain))), gain >= 0 ? CellTone.Green : CellTone.Yellow);
        }

        // A building the player lets himself: its rent roll, what is owed, how its tenants feel, calls, the inspector and the manager.
        private void WriteRentRoll(InfoBox box, EstateHolding h, EstateClimate c, int day)
        {
            if (!DutiesOn || !h.Managed)
            {
                return;
            }
            string place = h.Place;
            long roll = 0, owed = 0;
            int let = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                EstateUnit u = h.Units[i];
                if (u.Let)
                {
                    let++;
                    roll += u.Rent;
                    owed += u.Arrears;
                }
            }
            box.Headline("reallife_estate_duty_headline");
            box.Row("reallife_estate_duty_roll", EstateText.T("reallife_estate_row_a_day", "amount", EstateText.Money(roll)));
            box.Row("reallife_estate_duty_owed", EstateText.Money(owed), owed > 0 ? CellTone.Red : CellTone.Plain);
            int happy = EstateRules.BuildingHappy(h);
            if (happy >= 0)
            {
                box.Meter("reallife_estate_duty_happy", LookRules.Fill(happy, 1000), EstateText.Happy(happy), happy >= EstateCatalogue.HappyFair ? CellTone.Green
                    : happy >= EstateCatalogue.HappyPoor ? CellTone.Yellow : CellTone.Red);
            }
            else if (let == 0)
            {
                box.Line(EstateText.T("reallife_estate_duty_nobody"));
            }
            int calls = EstateRules.OpenCalls(h);
            if (calls > 0)
            {
                box.Line(EstateText.T("reallife_estate_duty_calls", "count", calls.ToString()), CellTone.Yellow);
            }
            if (EstateRules.Barred(h))
            {
                box.Line(EstateText.T("reallife_estate_duty_barred", "day", h.InspectDay.ToString()), CellTone.Red);
            }
            else if (h.OrderDay > 0)
            {
                box.Line(EstateText.T("reallife_estate_duty_order", "day", h.OrderDay.ToString(), "mark", EstateText.Level(EstateCatalogue.InspectPass)), CellTone.Red);
            }
            else if (h.InspectDay > 0)
            {
                box.Line(EstateText.T("reallife_estate_duty_inspect", "day", h.InspectDay.ToString(), "score", EstateText.Level(EstateRules.InspectScore(h, day)),
                    "mark", EstateText.Level(EstateCatalogue.InspectPass)));
            }
            box.Line(EstateText.T("reallife_estate_duty_costs", "calls", EstateText.Money(h.CallsYear), "fees", EstateText.Money(h.FeesYear), "fines", EstateText.Money(h.FinesYear)));
            box.Headline("reallife_estate_duty_who");
            if (!box.HasSelector || !box.Selector(EstateCatalogue.Managers, h.Manager, EstateText.Manager, i => _director.SetManager(place, i)))
            {
                box.Line(EstateText.Manager(h.Manager));
            }
            box.Note(EstateText.T(h.Manager == (int)EstateManager.Manager ? "reallife_estate_duty_manager_what" : h.Manager == (int)EstateManager.Agent ? "reallife_estate_duty_agent_what"
                    : "reallife_estate_duty_self_what", "share", EstateText.Share(EstateCatalogue.ManagerShare), "days", EstateCatalogue.AgentLetDays.ToString()));
        }

        // A let unit's satisfaction, what it is made of, a call, a tenant's notice to go and a payment plan.
        private void WriteTenantDuties(InfoBox box, EstateHolding h, int unit, int day, EstateClimate c)
        {
            EstateUnit u = h.Units[unit];
            if (!DutiesOn || !u.Let)
            {
                return;
            }
            box.Section("reallife_ibox_sec_feeling");
            if (u.Happy >= 0)
            {
                box.Meter("reallife_estate_duty_happy", LookRules.Fill(u.Happy, 1000), EstateText.Happy(u.Happy), u.Happy >= EstateCatalogue.HappyFair ? CellTone.Green
                    : u.Happy >= EstateCatalogue.HappyPoor ? CellTone.Yellow : CellTone.Red);
                int target = EstateRules.HappyTarget(h, unit, day, c);
                box.Line(EstateText.T(target >= u.Happy ? "reallife_estate_ten_happy_up" : "reallife_estate_ten_happy_down", "level", EstateText.Level(target),
                    "about", EstateText.Complaint(EstateRules.Grievance(h, unit, day, c))));
            }
            else
            {
                box.Line(EstateText.Happy(u.Happy));
            }
            if (u.Moans > 0)
            {
                box.Line(EstateText.T("reallife_estate_ten_moans", "count", u.Moans.ToString(), "most", EstateCatalogue.ComplaintsToCity.ToString()), CellTone.Yellow);
            }
            if (u.QuitDay > 0)
            {
                box.Line(EstateText.T("reallife_estate_ten_quitting", "day", u.QuitDay.ToString(), "level", EstateText.Level(EstateCatalogue.QuitBelow + EstateCatalogue.StayAbove)), CellTone.Red);
            }
            if (u.Call > 0)
            {
                box.Line(EstateText.T("reallife_estate_ten_call", "fault", EstateText.Fault(u.Call), "day", u.CallDay.ToString()), CellTone.Yellow);
            }
            if (u.OnPlan)
            {
                box.Line(EstateText.T("reallife_estate_ten_plan", "amount", EstateText.Money(u.PlanEach), "owed", EstateText.Money(u.Arrears)), CellTone.Green);
            }
            else if (u.Arrears > 0 && u.Reminded)
            {
                box.Line(EstateText.T("reallife_estate_ten_reminded"));
            }
        }

        // The arrears tools under a lease: a reminder, then a payment plan, before notice and the court.
        private void TenantDutyButtons(Transform buttons, EstateHolding h, int unit)
        {
            EstateUnit u = h.Units[unit];
            if (!DutiesOn || !u.Let || u.Arrears <= 0)
            {
                return;
            }
            string place = h.Place;
            if (!u.Reminded && !u.OnPlan && u.CourtDay == 0)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_remind", () => _director.Remind(place, unit));
            }
            if (EstateRules.MayOfferPlan(u))
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_plan", () => _director.OfferPlan(place, unit));
            }
        }

        private void TenantDutyNote(InfoBox box, EstateHolding h, int unit)
        {
            EstateUnit u = h.Units[unit];
            if (DutiesOn && u.Let && EstateRules.MayOfferPlan(u))
            {
                box.Note(EstateText.T("reallife_estate_ten_plan_what", "days", EstateRules.PlanDays(u.ArrearsDays).ToString(),
                    "chance", EstateText.Share(EstateRules.PlanAcceptChance(u.Record))));
            }
        }

        // The Works tab's rows for open calls, before the buildings. True with the picked one found.
        private bool CallRows(EstateState state, ref string selected, out EstateHolding picked, out int pickedUnit)
        {
            picked = null;
            pickedUnit = 0;
            if (!DutiesOn)
            {
                return false;
            }
            for (int i = 0; i < state.Holdings.Count; i++)
            {
                EstateHolding h = state.Holdings[i];
                for (int n = 0; h.Managed && n < h.Units.Count; n++)
                {
                    EstateUnit u = h.Units[n];
                    if (!u.Let || u.Call <= 0)
                    {
                        continue;
                    }
                    TableRow row;
                    if (Pick(EstateDirector.CallPick(h, n), ref selected, out row))
                    {
                        picked = h;
                        pickedUnit = n;
                    }
                    row.Kind = EstateTables.WorksCall;
                    row.NoTotal = true;   // a quote is not a repair to new
                    row.Set(0, EstateText.UnitName(h, n));
                    row.Set(1, EstateText.Level(h.Cond), h.Cond);
                    row.Set(2, h.Level.ToString(), h.Level);
                    row.Set(3, EstateText.Use(h.Use));
                    row.Set(4, EstateText.Money(u.CallCost), u.CallCost);
                    row.Set(5, EstateText.T(u.CallAnswer == (int)CallAnswer.Contractor ? "reallife_estate_row_call_booked" : u.CallAnswer == (int)CallAnswer.Yourself
                        ? "reallife_estate_row_call_yours" : "reallife_estate_row_call", "fault", EstateText.Fault(u.Call)), u.CallAnswer == (int)CallAnswer.Waiting ? TableRow.Red : TableRow.Yellow);
                    row.Set(6, EstateText.T("reallife_estate_row_since", "day", u.CallDay.ToString()), u.CallDay);
                }
            }
            return picked != null;
        }

        // One call: what it is, how long it has waited, what the tenant thinks, and the two ways to answer it.
        private void WriteCall(InfoBox box, EstateHolding h, int unit, int day)
        {
            EstateUnit u = h.Units[unit];
            string place = h.Place;
            box.Title(EstateText.UnitName(h, unit));
            box.Subtitle(EstateText.Fault(u.Call));
            box.Chip(EstateText.T(u.CallAnswer == (int)CallAnswer.Contractor ? "reallife_ibox_chip_booked" : u.CallAnswer == (int)CallAnswer.Yourself ? "reallife_ibox_chip_due"
                : "reallife_ibox_chip_open"), u.CallAnswer == (int)CallAnswer.Waiting ? CellTone.Red : CellTone.Yellow);
            box.Line(EstateText.T("reallife_estate_call_since", "day", u.CallDay.ToString(), "days", Math.Max(0, day - u.CallDay).ToString(),
                "grace", EstateCatalogue.CallGraceDays.ToString()), day - u.CallDay > EstateCatalogue.CallGraceDays ? CellTone.Yellow : CellTone.Plain);
            box.Line(EstateText.T("reallife_estate_call_tenant", "who", EstateText.Tenant(h.Use, u.Tenant), "happy", EstateText.Happy(u.Happy)));
            box.Section("reallife_ibox_sec_quote");
            int materials = EstateRules.DiyCost(u.CallCost), minutes = EstateCatalogue.CallMinutes[EstateCatalogue.Clamp(u.Call, 0, EstateCatalogue.CallKinds)];
            if (u.CallAnswer == (int)CallAnswer.Contractor)
            {
                box.Line(EstateText.T("reallife_estate_call_booked", "day", u.CallDue.ToString()), CellTone.Green);
            }
            else if (u.CallAnswer == (int)CallAnswer.Yourself)
            {
                box.Line(EstateText.T("reallife_estate_call_yours", "day", u.CallDue.ToString(), "minutes", minutes.ToString(), "amount", EstateText.Money(materials)), CellTone.Yellow);
            }
            else
            {
                box.Line(EstateText.T("reallife_estate_call_contractor", "amount", EstateText.Money(u.CallCost)));
                box.Line(EstateText.T("reallife_estate_call_yourself", "amount", EstateText.Money(materials), "minutes", minutes.ToString(), "days", EstateCatalogue.DiyDays.ToString()));
            }
            Transform buttons = box.ButtonRow();
            if (u.CallAnswer != (int)CallAnswer.Contractor)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_contractor", () => _director.SendContractor(place, unit));
            }
            if (u.CallAnswer == (int)CallAnswer.Waiting)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_yourself", () => _director.DoItYourself(place, unit));
            }
            BackButton(buttons);
            box.Note(EstateText.T("reallife_estate_call_what", "days", EstateCatalogue.CallLapseDays.ToString(), "old", EstateCatalogue.InspectCallOld.ToString()));
            Meta.Guide.Point(box, DutiesGuideTitle);
        }

        private const string DutiesGuideTitle = "reallife_meta_title_economy_estate_duties";
    }
}
