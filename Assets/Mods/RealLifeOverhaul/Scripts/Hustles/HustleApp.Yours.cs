using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The Yours tab: the player's things, where they are, what they cost and are worth, and what to do with each.</summary>
    public sealed partial class HustleApp
    {
        public const string ItemRow = "item:";

        private TableSpec WriteYours(InfoBox box, HustleState state)
        {
            int pickedId = Picked(_picked, ItemRow);
            Owned picked = null;
            int cost = 0, worth = 0;
            for (int i = 0; i < state.Items.Count; i++)
            {
                Owned o = state.Items[i];
                int w = _director.WorthOf(o);
                TableRow row = Row(ItemRow + o.Id);
                ThingDef def = o.Car ? null : HustleCatalogue.Thing(o.Thing);
                row.Kind = o.Car ? (int)ThingKind.Car : def != null ? (int)def.Kind : 0;
                row.Set(0, HustleText.Thing(o.Thing, o.Car));
                row.Set(1, HustleText.Kind(o.Thing, o.Car));
                row.Set(2, HustleText.Condition(o.Condition), o.Condition);
                Money(row, 3, o.Paid + o.Spent);
                Money(row, 4, w, w >= o.Paid + o.Spent ? TableRow.Green : TableRow.Red);
                if (o.State == OwnedState.Listed)
                {
                    Money(row, 5, o.Asking);
                }
                else
                {
                    row.Set(5, string.Empty, 0);
                }
                row.Set(6, WhereOf(o));
                row.Set(7, StateOf(o), o.State == OwnedState.Waiting ? TableRow.Yellow : o.State == OwnedState.Listed ? TableRow.Green : TableRow.Plain);
                cost += o.Paid + o.Spent;
                worth += w;
                picked = o.Id == pickedId ? o : picked;
            }
            if (picked != null)
            {
                WriteItem(box, state, picked);
                return HustleColumns.Yours;
            }
            _picked = null;
            box.Title(HustleText.T("reallife_hustle_yours_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_hustle_row_cost", HustleText.Money(cost));
            box.Row("reallife_hustle_row_worth", HustleText.Money(worth), worth >= cost ? CellTone.Green : CellTone.Red);
            box.Row("reallife_hustle_row_profit", HustleText.Money(state.Profit), state.Profit >= 0 ? CellTone.Green : CellTone.Red);
            box.Row("reallife_hustle_row_xp", state.RepairXp.ToString(CultureInfo.InvariantCulture));
            box.Note(HustleText.T("reallife_hustle_yours_about"));
            Meta.Guide.Point(box, GuideTitle);
            return HustleColumns.Yours;
        }

        private static string WhereOf(Owned o)
        {
            switch (o.State)
            {
                case OwnedState.Waiting: return Where(o.Street, o.Number);
                case OwnedState.Delivering: return HustleText.T("reallife_hustle_where_on_way", "day", o.Due.ToString(CultureInfo.InvariantCulture));
                default:
                    return o.Plan == OwnedPlan.Shop ? Where(o.Street, o.Number) : HustleText.T(o.Car ? "reallife_hustle_where_your_car" : "reallife_hustle_where_yours");
            }
        }

        private static string StateOf(Owned o)
        {
            switch (o.State)
            {
                case OwnedState.Waiting: return HustleText.T("reallife_hustle_state_to_collect_by", "day", o.Until.ToString(CultureInfo.InvariantCulture));
                case OwnedState.Delivering: return HustleText.T("reallife_hustle_state_delivering");
                case OwnedState.Listed: return HustleText.T("reallife_hustle_state_listed");
            }
            switch (o.Plan)
            {
                case OwnedPlan.FixAtHome: return HustleText.T("reallife_hustle_state_to_fix");
                case OwnedPlan.CleanAtHome: return HustleText.T("reallife_hustle_state_to_clean");
                case OwnedPlan.Shop: return HustleText.T("reallife_hustle_state_to_shop");
                default: return HustleText.T("reallife_hustle_state_kept");
            }
        }

        private void WriteItem(InfoBox box, HustleState state, Owned o)
        {
            int level = HustleGame.Level;
            int worth = _director.WorthOf(o);
            int id = o.Id;
            string thing = HustleText.Thing(o.Thing, o.Car);
            box.Title(thing);
            box.Subtitle(HustleText.T("reallife_hustle_item_from", "seller", o.Seller, "day", o.Bought.ToString(CultureInfo.InvariantCulture)));
            box.Chip(StateOf(o), o.State == OwnedState.Listed ? CellTone.Green : o.State == OwnedState.Waiting ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_hustle_row_paid", HustleText.Money(o.Paid));
            if (o.Spent > 0)
            {
                box.Row("reallife_hustle_row_spent", HustleText.Money(o.Spent));
            }
            box.Row("reallife_hustle_row_worth_now", HustleText.Money(worth), worth >= o.Paid + o.Spent ? CellTone.Green : CellTone.Red);
            box.Row("reallife_hustle_row_condition", HustleText.Condition(o.Condition));
            if (o.Test)
            {
                box.Line(HustleText.T("reallife_hustle_item_test"), CellTone.Yellow);
            }
            switch (o.State)
            {
                case OwnedState.Waiting:
                    bool stands = HustleGame.Stands(o.Street, o.Number);
                    string held = o.Car ? "reallife_hustle_item_collect_car_by" : "reallife_hustle_item_collect_by";
                    box.Line(HustleText.T(stands ? held : "reallife_hustle_item_collect_missing", "place", Where(o.Street, o.Number), "day", o.Until.ToString(CultureInfo.InvariantCulture)),
                        stands ? CellTone.Plain : CellTone.Yellow);
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_hustle_button_way", () => _director.ShowWay(o.Street, o.Number));
                    break;
                case OwnedState.Delivering:
                    box.Line(HustleText.T("reallife_hustle_item_delivering", "day", o.Due.ToString(CultureInfo.InvariantCulture)));
                    break;
                case OwnedState.Listed:
                    WriteListed(box, o, worth);
                    break;
                default:
                    WriteKept(box, o, worth, level);
                    break;
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteKept(InfoBox box, Owned o, int worth, int level)
        {
            int id = o.Id;
            string thing = HustleText.Thing(o.Thing, o.Car);
            box.Section("reallife_hustle_sec_work");
            if (o.Car)
            {
                box.Note(HustleText.T("reallife_hustle_item_car_work"));
            }
            else if (o.Plan != OwnedPlan.None)
            {
                box.Line(HustleText.T(o.Plan == OwnedPlan.FixAtHome ? "reallife_hustle_item_plan_fix" : o.Plan == OwnedPlan.CleanAtHome ? "reallife_hustle_item_plan_clean"
                    : "reallife_hustle_item_plan_shop", "place", Where(o.Street, o.Number)));
                Transform plan = box.ButtonRow();
                if (o.Plan == OwnedPlan.Shop)
                {
                    InfoBox.Button(plan, InfoBox.Positive, "reallife_hustle_button_way", () => _director.ShowWay(o.Street, o.Number));
                }
                InfoBox.Button(plan, InfoBox.Negative, "reallife_hustle_button_cancel_plan", () => _director.CancelPlan(id));
            }
            else
            {
                ThingDef def = HustleCatalogue.Thing(o.Thing);
                bool fix = HustleRules.CanFix(o), clean = HustleRules.CanClean(o), shop = HustleRules.CanShop(o);
                if (fix)
                {
                    box.Line(HustleText.T("reallife_hustle_item_fix", "parts", HustleText.Money(HustleRules.PartsCost(def, level)), "chance",
                        (HustleRules.FixChance(_director.State.RepairXp, o.Condition) / 10).ToString(CultureInfo.InvariantCulture), "most", def.FixMost.ToString(CultureInfo.InvariantCulture)));
                }
                if (shop)
                {
                    box.Line(HustleText.T("reallife_hustle_item_shop", "fee", HustleText.Money(HustleRules.ShopFee(def, o.Condition, level)), "percent",
                        HustleCatalogue.ShopCondition.ToString(CultureInfo.InvariantCulture)));
                }
                if (clean)
                {
                    box.Line(HustleText.T("reallife_hustle_item_clean", "gain", def.CleanGain.ToString(CultureInfo.InvariantCulture)));
                }
                if (!fix && !clean && !shop)
                {
                    box.Line(HustleText.T("reallife_hustle_item_nothing"));
                }
                Transform work = box.ButtonRow();
                if (fix)
                {
                    InfoBox.Button(work, InfoBox.Positive, "reallife_hustle_button_plan_fix", () => _director.PlanAtHome(id, true));
                }
                if (shop)
                {
                    InfoBox.Button(work, InfoBox.Negative, "reallife_hustle_button_plan_shop", () => _director.PlanShop(id));
                }
                if (clean)
                {
                    InfoBox.Button(work, InfoBox.Additional, "reallife_hustle_button_plan_clean", () => _director.PlanAtHome(id, false));
                }
            }
            box.Section("reallife_hustle_sec_sell");
            int step = Choice(HustleRules.AskStepAtWorth);
            int asking = HustleRules.AskAt(worth, step);
            Choose(box, HustleRules.AskSteps, step, i => HustleText.T("reallife_hustle_ask_at", "price", HustleText.Money(HustleRules.AskAt(worth, i))), SetChoice);
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_hustle_button_list", () => _director.List(id, asking));
        }

        private void WriteListed(InfoBox box, Owned o, int worth)
        {
            int id = o.Id;
            box.Section("reallife_hustle_sec_sell");
            box.Line(HustleText.T("reallife_hustle_item_listed", "asking", HustleText.Money(o.Asking), "days",
                Mathf.Max(0, HustleGame.Day - o.ListedDay).ToString(CultureInfo.InvariantCulture)));
            box.Note(HustleText.T(o.Car ? "reallife_hustle_item_listed_car" : "reallife_hustle_item_listed_note"));
            int step = Choice(HustleRules.AskStepOf(worth, o.Asking));
            int asking = HustleRules.AskAt(worth, step);
            Choose(box, HustleRules.AskSteps, step, i => HustleText.T("reallife_hustle_ask_at", "price", HustleText.Money(HustleRules.AskAt(worth, i))), SetChoice);
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_hustle_button_reprice", () => _director.List(id, asking));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_hustle_button_unlist", () => _director.Unlist(id));
        }
    }
}
