using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    public static partial class UtilitiesBuild
    {
        // ------------------------------------------------------------------------------------------------- Internet

        private static void Internet(UtilitiesFaceModel m, UtilitiesFacts f, UtilitiesView v, UtilitiesWords w)
        {
            UtilityPlaceFact[] places = f.Places;
            int connected = 0;
            var rows = new UiDataRow[places.Length];
            for (int i = 0; i < places.Length; i++)
            {
                UtilityPlaceFact p = places[i];
                connected += p.Connected && !p.Cut && !p.Pending ? 1 : 0;
                string state = PlaceState(w, p, out UiRole tone);
                rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        p.Name, p.Connected ? w.F("reallife_ui_util_provider_plan", "{provider}, {speed}", "provider", p.Provider, "speed", w.Speed(p.Mbps)) : w.T("reallife_net_none", "None"),
                        p.Connected ? w.Cents(p.PriceCents) : string.Empty, state
                    },
                    Subs = new[] { w.T(p.Home ? "reallife_ui_util_home" : "reallife_ui_util_business", p.Home ? "Home" : "Business"), null, null, null },
                    Values = new[] { (int)(p.Home ? UiIcon.Home : UiIcon.Building), 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone }
                };
            }
            m.Headline = w.F("reallife_net_headline", "Connected: {count} of {places} places", "count", connected.ToString(CultureInfo.InvariantCulture),
                "places", places.Length.ToString(CultureInfo.InvariantCulture));
            int place = places.Length > 0 ? Math.Max(0, Math.Min(places.Length - 1, v.Place)) : -1;
            m.Places = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w.T("reallife_util_col_place", "Place"), UiCellKind.Tile, 0f),
                    Col(w.T("reallife_net_col_plan", "Plan"), UiCellKind.Text, UiTokens.S16 * 3.5f),
                    Col(w.T("reallife_net_col_month", "A month"), UiCellKind.Money, UiTokens.S16 * 1.5f),
                    Col(w.T("reallife_util_col_state", "State"), UiCellKind.Chip, UiTokens.S16 * 2.5f)
                },
                Rows = rows, RowCount = rows.Length, Selected = place,
                EmptyTitle = w.T("reallife_ui_util_no_places", "No homes or businesses yet"), EmptyBody = w.T("reallife_net_empty", "No places yet: rent or buy one first."),
                EmptyIcon = UiIcon.Globe
            };
            if (place < 0)
            {
                return;
            }
            UtilityPlaceFact chosen = places[place];
            int option = ChosenOption(chosen, v);
            m.PlansTitle = w.F("reallife_ui_util_plans_at", "Plans at {place}", "place", chosen.Name);
            m.Plans = PlanTable(chosen, option, v, w);
            m.BuyCard = BuyCard(f, chosen, option, v, w);
        }

        /// <summary>The plan chosen at a place: the one asked for, else the one it has now, else the first.</summary>
        public static int ChosenOption(UtilityPlaceFact place, UtilitiesView v)
        {
            int n = place.Options.Length;
            if (n == 0)
            {
                return -1;
            }
            if (v.Option >= 0 && v.Option < n)
            {
                return v.Option;
            }
            for (int i = 0; i < n; i++)
            {
                if (place.Options[i].Current)
                {
                    return i;
                }
            }
            return 0;
        }

        // What a place's connection is doing, as a chip: working, in an outage, cut, being installed, or what it still needs.
        private static string PlaceState(UtilitiesWords w, UtilityPlaceFact p, out UiRole tone)
        {
            if (p.Connected)
            {
                if (p.Down)
                {
                    tone = UiRole.Danger;
                    return w.T("reallife_net_state_outage", "Outage");
                }
                if (p.Cut)
                {
                    tone = UiRole.Danger;
                    return w.T("reallife_util_state_cut", "Cut off");
                }
                tone = p.Pending ? UiRole.Accent : UiRole.Ok;
                return p.Pending ? w.T("reallife_ui_util_connecting", "Connecting") : w.T("reallife_ui_util_connected", "Connected");
            }
            if (p.Needs || p.Orders)
            {
                tone = UiRole.Warn;
                return w.T(p.Needs ? "reallife_net_needed" : "reallife_net_needed_orders", p.Needs ? "To open" : "For orders");
            }
            tone = UiRole.Muted;
            return w.T("reallife_ui_util_not_connected", "Not connected");
        }

        private static UiDataTableModel PlanTable(UtilityPlaceFact place, int option, UtilitiesView v, UtilitiesWords w)
        {
            UtilityOptionFact[] list = place.Options;
            var rows = new UiDataRow[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                UtilityOptionFact o = list[i];
                rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        o.ProviderName, w.Speed(o.Mbps), o.Own ? w.T("reallife_ui_util_free", "Free") : w.Cents(v.Contract ? o.ContractCents : o.MonthlyCents),
                        o.Current ? w.T("reallife_ui_util_your_plan", "Your plan") : string.Empty
                    },
                    Subs = new[] { o.Kind, null, null, null },
                    Values = new[] { (int)UiIcon.Globe, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Ok }
                };
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w.T("reallife_net_col_provider", "Provider"), UiCellKind.Tile, 0f),
                    Col(w.T("reallife_ui_util_col_speed", "Speed"), UiCellKind.Figure, UiTokens.S16 * 1.5f),
                    Col(w.T("reallife_net_col_month", "A month"), UiCellKind.Money, UiTokens.S16 * 1.5f),
                    Col(string.Empty, UiCellKind.Chip, UiTokens.S16 * 2f)
                },
                Rows = rows, RowCount = rows.Length, Selected = option,
                EmptyTitle = w.T("reallife_ui_util_no_plans", "No plans here"), EmptyBody = w.T("reallife_net_box_none_here", "No provider reaches this district."),
                EmptyIcon = UiIcon.Globe
            };
        }

        private static UtilitiesCard BuyCard(UtilitiesFacts f, UtilityPlaceFact p, int option, UtilitiesView v, UtilitiesWords w)
        {
            var card = new UtilitiesCard
            {
                Title = w.F("reallife_ui_util_place_line", "{place}, {district}", "place", p.Name, "district", p.District ?? string.Empty),
                Sub = null
            };
            var lines = new List<UtilitiesLine>(8);
            if (p.Needs)
            {
                lines.Add(new UtilitiesLine { Value = w.T("reallife_net_box_needs", "Needs a connection to open."), Check = true, Met = p.Connected && !p.Cut });
            }
            else if (p.Orders)
            {
                lines.Add(new UtilitiesLine { Value = w.T("reallife_net_box_needs_orders", "Its online shop needs a connection to take orders."), Check = true, Met = p.Connected && !p.Cut });
            }
            if (p.Grace && f.GraceLeft > 0 && (!p.Connected || p.Cut))
            {
                lines.Add(new UtilitiesLine
                {
                    Value = w.F("reallife_net_box_grace_place", "Grace period: {left} of {days} days left. It may open without a connection until the end of {day}.",
                        "left", f.GraceLeft.ToString(CultureInfo.InvariantCulture), "days", f.GraceDays.ToString(CultureInfo.InvariantCulture), "day", w.Day(f.GraceUntilDay))
                });
            }
            if (p.Connected)
            {
                lines.Add(new UtilitiesLine { Label = w.T("reallife_net_col_contract", "Contract"), Value = p.ContractText });
            }
            UtilityOptionFact o = option >= 0 ? p.Options[option] : null;
            if (o == null)
            {
                card.Lines = lines.ToArray();
                card.Actions = Connection(p, null, false, w);
                return card;
            }
            bool contract = v.Contract && !o.Own;
            bool same = p.Connected && !p.Cut && p.ProviderIndex == o.Provider;
            card.Sub = w.F("reallife_ui_util_plan_title", "{provider}, {speed}", "provider", o.ProviderName, "speed", w.Speed(o.Mbps));
            string price = o.Own ? w.T("reallife_ui_util_free", "Free") : w.Cents(contract ? o.ContractCents : o.MonthlyCents);
            lines.Add(new UtilitiesLine { Label = w.T("reallife_net_col_month", "A month"), Value = price });
            if (!same)
            {
                lines.Add(new UtilitiesLine
                {
                    Value = o.Own ? w.T("reallife_net_box_install_own", "Installation: free, by your own engineers")
                        : contract ? w.T("reallife_net_box_install_free", "Installation: free on a contract")
                        : w.F("reallife_net_box_install_fee", "Installation: {fee}", "fee", w.Money(f.InstallFee))
                });
                lines.Add(new UtilitiesLine { Value = w.F("reallife_net_box_install_day", "Installed on {day}", "day", w.Day(f.Day + f.InstallDays)) });
                if (p.Connected && p.ExitFee > 0)
                {
                    lines.Add(new UtilitiesLine
                    {
                        Value = w.F("reallife_net_box_switch_fee", "Leaving {provider} now: {fee}", "provider", p.Provider, "fee", w.Money(p.ExitFee)), Ink = UiRole.WarnText
                    });
                }
            }
            if (!string.IsNullOrEmpty(o.Speed))
            {
                lines.Add(new UtilitiesLine { Value = o.Speed });
            }
            if (contract)
            {
                lines.Add(new UtilitiesLine { Value = w.F("reallife_net_box_contract_note", "A contract holds the price for {months} months. Leaving early costs half of each month left.",
                    "months", f.ContractMonths.ToString(CultureInfo.InvariantCulture)) });
            }
            card.Lines = lines.ToArray();
            if (!o.Own)
            {
                card.SwitchLabel = w.T("reallife_net_box_contract_switch", "On a contract");
                card.SwitchOn = v.Contract;
            }
            card.Actions = Connection(p, o, same && o.Current && (!v.Contract || p.OnContract || o.Own), w);
            return card;
        }

        // Buy or change the plan, cancel the connection, or have it reconnected: what the old app's box offered a place.
        private static UiActionModel[] Connection(UtilityPlaceFact p, UtilityOptionFact o, bool nothingToChange, UtilitiesWords w)
        {
            var actions = new List<UiActionModel>(3);
            if (p.Connected && p.Cut)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_net_box_reconnect", "Reconnect"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count,
                    Command = UtilitiesFace.BuyAction, Arg = 2, Disabled = p.Owes
                });
            }
            if (p.Connected)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_ui_util_cancel_connection", "Cancel the connection"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close, Key = UiAction.Count,
                    Command = UtilitiesFace.BuyAction, Arg = 1
                });
            }
            if (o != null)
            {
                bool change = p.Connected && !p.Cut;
                actions.Add(new UiActionModel
                {
                    Label = w.T(change ? "reallife_net_box_change" : "reallife_net_box_buy", change ? "Change plan" : "Buy"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check,
                    Key = UiAction.Alt, Command = UtilitiesFace.BuyAction, Arg = 0, Disabled = nothingToChange || p.Owes
                });
            }
            return actions.ToArray();
        }
    }
}
