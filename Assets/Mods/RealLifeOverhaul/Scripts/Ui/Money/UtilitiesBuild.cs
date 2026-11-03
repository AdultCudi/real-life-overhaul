using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the player has chosen on the Utilities screen: the tab, the account, the place and its plan, the contract switch
    /// and the sheet over the page. The screen keeps it; the build reads it with the facts.
    /// </summary>
    public sealed class UtilitiesView
    {
        public int Tab;
        public int Account;
        public int Place;
        public int Option = -1;             // the plan chosen to buy at the place, an index of its options; -1 the one it has now
        public bool Contract = true;
        public UtilitiesAsk Sheet;
        public int District;                // the district a question is about
    }

    /// <summary>What is open over the page.</summary>
    public enum UtilitiesAsk : byte
    {
        None = 0,
        Found,          // the question before the licence is paid
        Build,          // before a district is dug
        Extend,         // before a district is extended to its whole
        Sell,           // before the company is sold
        DigList,        // the districts to dig in
        Settings        // prices, maintenance and marketing
    }

    /// <summary>The words a build writes with: a key's text (the English is what a check reads), names filled in, money and counts.</summary>
    public sealed class UtilitiesWords
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly UiWords _words;
        private readonly Func<float, string> _money;
        private readonly Func<CultureInfo> _culture;

        /// <summary>The words of a key, money as the game writes it, and the culture a count is written in (asked each time: the language may change).</summary>
        public UtilitiesWords(UiWords words, Func<float, string> money, Func<CultureInfo> culture)
        {
            _words = words;
            _money = money;
            _culture = culture;
        }

        private CultureInfo Culture => _culture != null ? _culture() ?? Inv : Inv;

        public string T(string key, string english) => _words(key, english);

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public string F(string key, string english, params string[] pairs)
        {
            string text = _words(key, english);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public string Money(float money) => _money(money);

        public string Cents(int cents) => _money(cents / 100f);

        public string N(int n) => n.ToString("N0", Culture);

        public string Day(int day) => F("reallife_util_day", "day {day}", "day", day.ToString(Inv));

        /// <summary>A speed with its unit: 300 Mbit, 1 Gbit.</summary>
        public string Speed(int mbps)
        {
            return mbps >= 1000 && mbps % 1000 == 0 ? F("reallife_ui_util_gbit", "{n} Gbit", "n", (mbps / 1000).ToString(Inv))
                : F("reallife_ui_util_mbit", "{n} Mbit", "n", N(mbps));
        }

        public string Percent(int thousandths) => (thousandths / 10f).ToString("0.#", Culture) + "%";
    }

    /// <summary>
    /// Writes the Utilities screen's model from the facts of the save and what the player has chosen. Game-free: the live
    /// screen and the preview's samples both come through it, so what a shot shows is what ships. Cost: a pass over a few
    /// dozen rows when the facts or the choice change; nothing while idle.
    /// </summary>
    public static partial class UtilitiesBuild
    {
        public static UtilitiesFaceModel Make(UtilitiesFacts f, UtilitiesView v, UtilitiesWords w)
        {
            var m = new UtilitiesFaceModel { Title = w.T("reallife_util_app", "Utilities"), Tab = Math.Max(0, Math.Min(2, v.Tab)) };
            m.Nav = new[]
            {
                Tab(w, "reallife_util_tab_accounts", "Accounts", UiIcon.Lightning),
                Tab(w, "reallife_util_tab_internet", "Internet", UiIcon.Globe),
                Tab(w, "reallife_util_tab_network", "Your network", UiIcon.HardDrives)
            };
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = w.T("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = w.T("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = w.T("reallife_ui_prompt_back", "Back") }
            };
            if (!f.Ready)
            {
                m.OffTitle = w.T("reallife_util_off_title", "Not running");
                m.OffBody = w.T("reallife_util_off_body", "Power and water are switched off in the options, or no save is loaded.");
                return m;
            }
            if (m.Tab == 0)
            {
                Accounts(m, f, v, w);
            }
            else if (m.Tab == 1)
            {
                Internet(m, f, v, w);
            }
            else
            {
                Network(m, f, v, w);
            }
            Sheet(m, f, v, w);
            return m;
        }

        private static UiNavEntry Tab(UtilitiesWords w, string key, string english, UiIcon icon) => new UiNavEntry { Icon = icon, Label = w.T(key, english) };

        // The icon of a service: a bolt, a drop, a globe.
        private static UiIcon ServiceIcon(int service) => service == 0 ? UiIcon.Lightning : service == 1 ? UiIcon.Drop : UiIcon.Globe;

        private static UiDataColumn Col(string title, UiCellKind kind, float width) => new UiDataColumn { Title = title, Kind = kind, Width = width };

        // ------------------------------------------------------------------------------------------------- Accounts

        private static void Accounts(UtilitiesFaceModel m, UtilitiesFacts f, UtilitiesView v, UtilitiesWords w)
        {
            UtilityAccountFact[] list = f.Accounts;
            m.Headline = w.F("reallife_util_headline", "Accounts: {count}, owed {owed}", "count", list.Length.ToString(CultureInfo.InvariantCulture), "owed", w.Money(f.Owed));
            var rows = new UiDataRow[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                UtilityAccountFact a = list[i];
                string service = ServiceName(w, a.Service);
                string state = BillWords(w, a, out UiRole tone);
                rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        a.Place, state, w.Money(a.SoFar), a.Owed > 0 ? w.Money(a.Owed) : string.Empty,
                        a.Owed > 0 && a.DueDay > 0 ? w.Day(a.DueDay) : string.Empty, w.T(a.AutoDebit ? "reallife_util_debit_on" : "reallife_util_debit_off", a.AutoDebit ? "On" : "Off")
                    },
                    Subs = new[] { w.F("reallife_ui_util_account_sub", "{service}, {provider}", "service", service, "provider", a.Provider), null, null, null, null, null },
                    Values = new[] { (int)ServiceIcon(a.Service), 0, 0, 0, 0, 0 },
                    Tones = new[] { UiRole.None, tone, UiRole.None, a.State == UtilityBillState.Overdue ? UiRole.DangerText : UiRole.None, UiRole.None, UiRole.None }
                };
            }
            m.Accounts = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w.T("reallife_util_col_place", "Place"), UiCellKind.Tile, 0f),
                    Col(w.T("reallife_ui_util_col_bill", "Bill"), UiCellKind.Chip, UiTokens.S16 * 2.5f),
                    Col(w.T("reallife_util_col_month", "This month"), UiCellKind.Money, UiTokens.S16 * 2f),
                    Col(w.T("reallife_util_col_owed", "Owed"), UiCellKind.Money, UiTokens.S16 * 2f),
                    Col(w.T("reallife_util_col_due", "Due"), UiCellKind.Date, UiTokens.S16 * 1.5f),
                    Col(w.T("reallife_util_col_debit", "Auto debit"), UiCellKind.Text, UiTokens.S16 * 2f)
                },
                Rows = rows, RowCount = rows.Length, Selected = rows.Length > 0 ? Math.Max(0, Math.Min(rows.Length - 1, v.Account)) : -1,
                EmptyTitle = w.T("reallife_ui_util_no_accounts", "No homes or businesses yet"),
                EmptyBody = w.T("reallife_util_empty", "No accounts yet: they come with the first place you rent or buy."),
                EmptyIcon = UiIcon.Lightning
            };
            if (rows.Length > 0)
            {
                m.AccountCard = AccountCard(f, list[m.Accounts.Selected], w);
            }
        }

        private static string ServiceName(UtilitiesWords w, int service)
        {
            return service == 0 ? w.T("reallife_util_power", "Power") : service == 1 ? w.T("reallife_util_water", "Water") : w.T("reallife_util_internet", "Internet");
        }

        // Paid, unpaid or overdue as a chip, or where the supply stands when it is not simply supplied.
        private static string BillWords(UtilitiesWords w, UtilityAccountFact a, out UiRole tone)
        {
            switch (a.State)
            {
                case UtilityBillState.Due: tone = UiRole.Warn; return w.T("reallife_ui_util_unpaid", "Unpaid");
                case UtilityBillState.Overdue: tone = UiRole.Danger; return w.T("reallife_ui_util_overdue", "Overdue");
                case UtilityBillState.Pending: tone = UiRole.Accent; return w.T("reallife_ui_util_connecting", "Connecting");
                case UtilityBillState.Temporary: tone = UiRole.Warn; return w.T("reallife_ui_util_temporary", "Temporary");
                case UtilityBillState.Cut: tone = UiRole.Danger; return w.T("reallife_util_state_cut", "Cut off");
                case UtilityBillState.Closed: tone = UiRole.Muted; return w.T("reallife_ui_util_closed", "Closed");
                default: tone = UiRole.Ok; return w.T("reallife_ui_util_paid", "Paid");
            }
        }

        private static UtilitiesCard AccountCard(UtilitiesFacts f, UtilityAccountFact a, UtilitiesWords w)
        {
            var card = new UtilitiesCard
            {
                Title = a.Place,
                Sub = w.F("reallife_ui_util_account_sub", "{service}, {provider}", "service", ServiceName(w, a.Service), "provider", a.Provider)
            };
            card.Chip = BillWords(w, a, out UiRole tone);
            card.ChipTone = tone;
            var lines = new System.Collections.Generic.List<UtilitiesLine>(8);
            if (!string.IsNullOrEmpty(a.StateText) && a.State != UtilityBillState.Paid && a.State != UtilityBillState.Due && a.State != UtilityBillState.Overdue)
            {
                lines.Add(new UtilitiesLine { Label = w.T("reallife_util_col_state", "State"), Value = a.StateText });
            }
            if (a.Owed > 0)
            {
                lines.Add(new UtilitiesLine
                {
                    Label = w.T("reallife_util_col_owed", "Owed"),
                    Value = w.F("reallife_util_box_owed_value", "{amount}, due on {day}", "amount", w.Money(a.Owed), "day", w.Day(a.DueDay)),
                    Ink = a.State == UtilityBillState.Overdue ? UiRole.DangerText : UiRole.WarnText
                });
            }
            else
            {
                lines.Add(new UtilitiesLine { Value = w.T("reallife_util_box_nothing_owed", "Nothing owed."), Check = true, Met = true });
            }
            if (a.Units != null)
            {
                lines.Add(new UtilitiesLine
                {
                    Label = w.T("reallife_ui_util_used", "Used this month"),
                    Value = w.F(a.Service == 0 ? "reallife_ui_util_kwh" : "reallife_ui_util_m3", a.Service == 0 ? "{n} kWh" : "{n} cubic metres", "n", a.Units)
                });
            }
            lines.Add(new UtilitiesLine { Label = w.T("reallife_util_col_month", "This month"), Value = w.Money(a.SoFar) });
            if (a.LastBill > 0)
            {
                lines.Add(new UtilitiesLine
                {
                    Label = w.T("reallife_ui_util_last_bill", "Last bill"),
                    Value = w.F("reallife_util_box_last_value", "{amount} on {day}", "amount", w.Money(a.LastBill), "day", w.Day(a.LastBillDay))
                });
            }
            if (a.Deposit > 0)
            {
                lines.Add(new UtilitiesLine
                {
                    Label = w.T("reallife_ui_util_deposit", "Deposit held"),
                    Value = w.F("reallife_util_box_deposit_value",
                        "{amount}, back after {left} more bills paid on time", "amount", w.Money(a.Deposit), "left", a.DepositLeft.ToString(CultureInfo.InvariantCulture))
                });
            }
            if (a.Offer != UtilityOffer.None)
            {
                lines.Add(new UtilitiesLine
                {
                    Value = f.ByPhone ? ApplyWords(w, a) : w.F(a.State == UtilityBillState.Temporary ? "reallife_util_box_apply_temporary" : "reallife_util_box_apply_cut",
                        a.State == UtilityBillState.Temporary ? "Temporary supply: apply in person at {office} before it ends." : "Cut off: pay what is owed, then apply in person at {office}.",
                        "office", f.Office)
                });
            }
            else if (a.Service != 2)
            {
                lines.Add(new UtilitiesLine
                {
                    Value = f.ByPhone ? w.T("reallife_util_box_office_app",
                        "Power and water accounts: no building can hold the utilities office just now, so you apply and reconnect here in the app, by phone, at the same prices.")
                        : w.F("reallife_util_box_office", "Power and water accounts: apply in person at {office}.", "office", f.Office)
                });
            }
            card.Lines = lines.ToArray();
            if (a.CanDebit)
            {
                card.SwitchLabel = w.T("reallife_util_box_debit", "Auto debit");
                card.SwitchOn = a.AutoDebit;
            }
            var actions = new System.Collections.Generic.List<UiActionModel>(2);
            if (f.ByPhone && a.Offer != UtilityOffer.None)
            {
                actions.Add(new UiActionModel
                {
                    Label = a.Offer == UtilityOffer.Reconnect ? w.T("reallife_util_box_reconnect", "Have it reconnected") : w.T("reallife_util_box_apply", "Apply for an account"),
                    Kind = a.Owed > 0 ? UiButtonKind.Secondary : UiButtonKind.Primary, Icon = UiIcon.Check, Key = a.Owed > 0 ? UiAction.Count : UiAction.Alt,
                    Command = UtilitiesFace.CardAction, Arg = 1, Disabled = a.Owed > 0
                });
            }
            if (a.Owed > 0)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_util_box_pay", "Pay now"), Kind = UiButtonKind.Primary, Icon = UiIcon.Coin, Key = UiAction.Alt,
                    Command = UtilitiesFace.CardAction, Arg = 0
                });
            }
            card.Actions = actions.ToArray();
            return card;
        }

        // What an application by phone costs, in the desk's own words.
        private static string ApplyWords(UtilitiesWords w, UtilityAccountFact a)
        {
            return a.ApplyCost > 0
                ? w.F("reallife_util_desk_apply_cost", "Deposit {deposit}, fee {fee}: {amount} now. It connects tomorrow morning.", "deposit", w.Money(a.ApplyDeposit), "fee", w.Money(a.ApplyFee),
                    "amount", w.Money(a.ApplyCost))
                : w.T("reallife_util_desk_apply_free", "Nothing to pay with your credit rating. It connects tomorrow morning.");
        }
    }
}
