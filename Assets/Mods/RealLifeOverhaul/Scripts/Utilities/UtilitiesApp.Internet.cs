using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The Utilities app's Internet tab: each place's connection, bought, changed, cancelled or reconnected in the box.</summary>
    public sealed partial class UtilitiesApp
    {
        private void WriteInternet(InfoBox box, UtilitiesState state, ref string selected)
        {
            int today = UtilitiesGame.Today, hour = today * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0), connected = 0;
            PlaceInfo? picked = null;
            IReadOnlyList<PlaceInfo> places = _director.Places;
            for (int i = 0; i < places.Count; i++)
            {
                PlaceInfo place = places[i];
                string id = "net|" + place.Place;
                TableRow row = Row(id);
                row.Pick = true;
                row.Kind = place.Kind == PlaceKind.Home ? UtilitiesTables.Home : UtilitiesTables.Business;
                if (id == _pick)
                {
                    selected = id;
                    picked = place;
                }
                Account a = state.Find(place.Place, Service.Internet);
                bool live = a != null && a.State != SupplyState.Closed;
                Provider p = live ? InternetRules.Get(a.Provider) : null;
                bool needs = UtilitiesDirector.NeedsNet(place);
                bool orders = !needs && ShopAt(place);
                bool down = live && (a.State == SupplyState.Cut || InternetRules.InOutage(a, hour));
                if (live && a.State == SupplyState.Active)
                {
                    connected++;
                }
                row.Set(0, place.Name);
                int line = live ? LeasedLineRules.LineMbps(a, today) : 0;   // a data centre's leased line stands beside its plan: the place has the faster of the two
                int speed = LeasedLineRules.Speed(p != null ? p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)] : 0, line);
                row.Set(1, p != null ? UtilitiesText.T(p.NameKey) : live && LeasedLineRules.Has(a) ? UtilitiesText.T("reallife_net_provider_leased") : UtilitiesText.T("reallife_net_none"),
                    needs && !live ? TableRow.Red : TableRow.Plain);
                row.Set(2, speed > 0 ? UtilitiesText.T("reallife_net_mbps", "mbps", speed.ToString()) : "-", speed > 0 ? speed : double.NaN);
                int month = live ? a.PriceCents + LeasedLineRules.LineCents(a, today, Prices.Level) : 0;
                row.Set(3, live ? UtilitiesText.Money(month / 100f) : "-", live ? month / 100.0 : double.NaN);
                row.Set(4, live ? (InternetRules.InOutage(a, hour) ? UtilitiesText.T("reallife_net_state_outage") : UtilitiesText.State(a, today)) : "-", down ? TableRow.Red : TableRow.Plain);
                row.Set(5, live ? (a.ContractUntil > today ? UtilitiesText.Day(a.ContractUntil) : UtilitiesText.T("reallife_net_month_by_month")) : "-",
                    live && a.ContractUntil > today ? a.ContractUntil : double.NaN);
                row.Set(6, UtilitiesText.T(needs ? "reallife_net_needed" : orders ? "reallife_net_needed_orders" : "reallife_net_not_needed"),
                    (needs || orders) && !live ? TableRow.Red : TableRow.Plain);
            }
            UiClone.Words(_shell.Header, UtilitiesText.T("reallife_net_headline", "count", connected.ToString(), "places", places.Count.ToString()));
            if (!picked.HasValue)
            {
                _pick = null;
                box.Title(UtilitiesText.T("reallife_util_tab_internet"));
                int left = InternetRules.GraceLeft(state, today);
                if (left > 0)
                {
                    box.Line(UtilitiesText.T("reallife_net_box_grace", "left", left.ToString(), "days", InternetCatalogue.GraceDays.ToString(),
                        "day", UtilitiesText.Day(state.GraceUntil - 1), "count", state.Grace.Count.ToString()));
                }
                box.Note(UtilitiesText.T("reallife_net_box_intro"));
                box.Note(UtilitiesText.T("reallife_net_box_needs_note"));
                if (Settings.UtilitiesNetSpeed)
                {
                    box.Note(UtilitiesText.T("reallife_net_box_speed_note"));
                }
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            WritePlace(box, picked.Value, state, today, hour);
        }

        private void WritePlace(InfoBox box, PlaceInfo place, UtilitiesState state, int today, int hour)
        {
            Account a = state.Find(place.Place, Service.Internet);
            bool live = a != null && a.State != SupplyState.Closed;
            string id = GameRegistrations.District(place.Reg);
            string district = !string.IsNullOrEmpty(id) ? id.GetLocalization() : "-";
            box.Title(place.Name);
            if (UtilitiesDirector.NeedsNet(place))
            {
                box.Line(UtilitiesText.T("reallife_net_box_needs"));
                if (InternetRules.InGrace(state, place.Place, today))
                {
                    box.Line(UtilitiesText.T("reallife_net_box_grace_place", "left", InternetRules.GraceLeft(state, today).ToString(), "days", InternetCatalogue.GraceDays.ToString(),
                        "day", UtilitiesText.Day(state.GraceUntil - 1)));
                }
            }
            else if (ShopAt(place))
            {
                box.Line(UtilitiesText.T("reallife_net_box_needs_orders"));
                if (InternetRules.InGrace(state, place.Place, today) && (a == null || a.State != SupplyState.Active))
                {
                    box.Line(UtilitiesText.T("reallife_net_box_grace_shop", "left", InternetRules.GraceLeft(state, today).ToString(), "days", InternetCatalogue.GraceDays.ToString(),
                        "day", UtilitiesText.Day(state.GraceUntil - 1)));
                }
            }
            if (live && !_choosing)
            {
                Provider p = InternetRules.Get(a.Provider);
                box.Row("reallife_util_box_state", InternetRules.InOutage(a, hour) ? UtilitiesText.T("reallife_net_state_outage") : UtilitiesText.State(a, today));
                if (InternetRules.InOutage(a, hour))
                {
                    box.Line(UtilitiesText.T("reallife_net_box_outage", "hour", UtilitiesText.Hour(a.OutageUntil % 24)));
                }
                box.Row("reallife_net_box_price", UtilitiesText.Money(a.PriceCents / 100f));
                box.Row("reallife_net_box_provider", p != null ? UtilitiesText.T(p.NameKey) + " (" + UtilitiesText.T(p.KindKey) + ")" : "-");
                if (p != null)
                {
                    box.Row("reallife_net_box_plan", UtilitiesText.T("reallife_net_mbps", "mbps", p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)].ToString()));
                    SpeedLines(box, place, p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)]);
                }
                box.Row("reallife_net_box_contract", a.ContractUntil > today
                    ? UtilitiesText.T("reallife_net_box_contract_until", "day", UtilitiesText.Day(a.ContractUntil), "fee", UtilitiesText.Money(_director.NetExitFee(place)))
                    : UtilitiesText.T("reallife_net_month_by_month"));
                box.Row("reallife_net_box_district", district);
                if (LeasedLineRules.Has(a))
                {
                    LineRows(box, a, today);
                }
                Transform buttons = box.ButtonRow();
                if (a.State == SupplyState.Cut)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_net_box_reconnect", () => UiClone.Safe(() =>
                    {
                        if (!_director.ReconnectNet(place, out string why) && why != null)
                        {
                            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why), 8f);
                        }
                        Refresh();
                    }));
                }
                else
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_net_box_change", () => UiClone.Safe(() =>
                    {
                        _choosing = true;
                        _provider = Math.Max(0, ReachIndex(place, a.Provider));
                        _plan = a.Plan;
                        _contract = a.ContractUntil > today || _contract;
                        Refresh();
                    }));
                }
                if (LeasedLineRules.Has(a))
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_net_box_line_cancel", () => UiClone.Safe(() =>
                    {
                        if (!_director.CancelLine(place, out string why) && why != null)
                        {
                            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why), 8f);
                        }
                        Refresh();
                    }));
                }
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_net_box_cancel", () => UiClone.Safe(() =>
                {
                    if (!_director.CancelNet(place, out string why) && why != null)
                    {
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why), 8f);
                    }
                    Refresh();
                }));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            box.Row("reallife_net_box_district", district);
            WriteChoice(box, place, a, live, today);
            Meta.Guide.Point(box, GuideTitle);
        }

        // the providers that reach the place, a plan of the one chosen, a contract or not: what it costs, and "Buy"
        private void WriteChoice(InfoBox box, PlaceInfo place, Account a, bool live, int today)
        {
            Reach(place);
            if (_reach.Count == 0)
            {
                box.Line(UtilitiesText.T("reallife_net_box_none_here"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_util_box_back", live ? (Action)(() => UiClone.Safe(() => { _choosing = false; Refresh(); })) : Back);
                return;
            }
            _provider = Math.Max(0, Math.Min(_reach.Count - 1, _provider));
            int provider = _reach[_provider];
            Provider p = InternetRules.Get(provider);
            _plan = Math.Max(0, Math.Min(p.Mbps.Length - 1, _plan));
            box.Headline("reallife_net_box_choose");
            box.Selector(_reach.Count, _provider, i => UtilitiesText.T("reallife_net_provider_option", "provider", UtilitiesText.T(InternetRules.Get(_reach[i]).NameKey),
                "kind", UtilitiesText.T(InternetRules.Get(_reach[i]).KindKey)), i => UiClone.Safe(() =>
            {
                _provider = i;
                _plan = 0;
                Refresh();
            }));
            bool ours = provider == InternetCatalogue.OwnIndex && _director.State.Network.Running;
            bool leased = InternetCatalogue.IsLine(provider), ownLine = provider == InternetCatalogue.OwnLineIndex;
            box.Selector(p.Mbps.Length, _plan, i => ours ? UtilitiesText.T("reallife_net_plan_option_own", "mbps", p.Mbps[i].ToString())
                : leased ? UtilitiesText.T("reallife_net_plan_option_line", "speed", UtilitiesText.Gbps(p.Mbps[i]), "racks", (p.Mbps[i] / LeasedLineCatalogue.RackMbps).ToString(),
                    "price", UtilitiesText.Money(_director.NetPrice(place, provider, i, false) / 100f))
                : UtilitiesText.T("reallife_net_plan_option", "mbps", p.Mbps[i].ToString(), "price", UtilitiesText.Money(_director.NetPrice(place, provider, i, _contract) / 100f)),
                i => UiClone.Safe(() =>
            {
                _plan = i;
                Refresh();
            }));
            if (leased)
            {
                // what the line is: the way to grow a data centre past its plan, and what the district it stands in does to the wait
                box.Note(UtilitiesText.T(ownLine ? "reallife_net_box_line_own_about" : "reallife_net_box_line_about"));
            }
            if (!ours && !leased)
            {
                box.Switch("reallife_net_box_contract_switch", _contract, on => UiClone.Safe(() =>
                {
                    _contract = on;
                    Refresh();
                }));
            }
            bool same = leased ? LeasedLineRules.Ordered(a) == _plan + 1 && LeasedLineRules.OrderedOwn(a) == ownLine : live && a.Provider == provider && a.State != SupplyState.Cut;
            box.Row("reallife_net_box_price", UtilitiesText.Money(_director.NetPrice(place, provider, _plan, _contract && !ours && !leased) / 100f));
            SpeedLines(box, place, p.Mbps[_plan]);
            if (leased && !same)
            {
                LineInstall(box, place, _plan, today, ownLine);
            }
            else if (!same)
            {
                box.Line(ours ? UtilitiesText.T("reallife_net_box_install_own") : _contract ? UtilitiesText.T("reallife_net_box_install_free")
                    : UtilitiesText.T("reallife_net_box_install_fee", "fee", UtilitiesText.Money(InternetCatalogue.InstallFee)));
                box.Line(UtilitiesText.T("reallife_net_box_install_day", "day", UtilitiesText.Day(today + InternetCatalogue.InstallDays)));
                int exit = live ? _director.NetExitFee(place) : 0;
                if (exit > 0)
                {
                    box.Line(UtilitiesText.T("reallife_net_box_switch_fee", "provider", UtilitiesText.ProviderOf(a), "fee", UtilitiesText.Money(exit)));
                }
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, same || leased && LeasedLineRules.Has(a) ? "reallife_net_box_change" : "reallife_net_box_buy", () => UiClone.Safe(() =>
            {
                if (_director.BuyNet(place, provider, _plan, _contract, out string why))
                {
                    _choosing = false;
                }
                else if (why != null)
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why), 8f);
                }
                Refresh();
            }));
            if (live)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_net_box_keep", () => UiClone.Safe(() =>
                {
                    _choosing = false;
                    Refresh();
                }));
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
            }
            if (_contract && !ours && !leased)
            {
                box.Note(UtilitiesText.T("reallife_net_box_contract_note", "months", InternetCatalogue.ContractMonths.ToString()));
            }
        }

        // A leased line ordered at a place, in words: its speed, its state and what it costs a month, and a change under way.
        private static void LineRows(InfoBox box, Account a, int today)
        {
            int live = LeasedLineRules.LiveAt(a, today);
            string price = UtilitiesText.Money(LeasedLineRules.MonthCents(Math.Max(0, LeasedLineRules.Ordered(a) - 1), Prices.Level, LeasedLineRules.OrderedOwn(a)) / 100f);
            if (live > 0)
            {
                box.Row("reallife_net_box_line", UtilitiesText.T("reallife_net_line_live", "speed", UtilitiesText.Gbps(LeasedLineCatalogue.Mbps[live - 1]),
                    "price", UtilitiesText.Money(LeasedLineRules.MonthCents(live - 1, Prices.Level, LeasedLineRules.LiveOwn(a, today)) / 100f)));
            }
            int ordered = LeasedLineRules.Ordered(a);
            int day = a.LineNext > 0 ? a.LineNextDay : a.LineDay;
            if (ordered > 0 && (live == 0 || ordered != live))
            {
                box.Row(live > 0 ? "reallife_net_box_line_change" : "reallife_net_box_line",
                    UtilitiesText.T("reallife_net_line_ordered", "speed", UtilitiesText.Gbps(LeasedLineCatalogue.Mbps[ordered - 1]), "day", UtilitiesText.Day(day), "price", price));
            }
        }

        // What a leased line costs to bring to a place and how long it takes, with what the district has (fibre in the street, only cable, nothing).
        private void LineInstall(InfoBox box, PlaceInfo place, int step, int today, bool own)
        {
            string district = GameRegistrations.District(place.Reg);
            int days = _director.LineDays(place, step, own);
            box.Line(UtilitiesText.T("reallife_net_box_line_install", "fee", UtilitiesText.Money(_director.LineFee(place, step, own)), "days", days.ToString(),
                "day", UtilitiesText.Day(today + days)));
            int has = own ? LeasedLineCatalogue.Fibre : LeasedLineRules.Class(district);
            box.Line(UtilitiesText.T(own ? "reallife_net_box_line_own" : has == LeasedLineCatalogue.Fibre ? "reallife_net_box_line_fibre"
                : has == LeasedLineCatalogue.Cable ? "reallife_net_box_line_cable" : "reallife_net_box_line_dig"));
        }

        // What a plan's speed means for the work there, from the same rules the work goes by (NetSpeedRules): for the player's
        // tech firm or online shop at the place, or what it would mean for one; nothing at a home or while speed does not count.
        private static void SpeedLines(InfoBox box, PlaceInfo place, int mbps)
        {
            if (!Settings.UtilitiesNetSpeed || place.Kind == PlaceKind.Home)
            {
                return;
            }
            Func<BuildingRegistration, int, int> firmWork = UtilitiesBridges.FirmWork, shopMost = UtilitiesBridges.ShopMost;
            int work = firmWork != null ? firmWork(place.Reg, mbps) : -1, most = shopMost != null ? shopMost(place.Reg, mbps) : -1;
            box.Line(work >= 0 ? UtilitiesText.T("reallife_net_speed_firm", "work", UtilitiesText.Percent(work))
                : UtilitiesText.T("reallife_net_speed_tech", "work", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Usual)),
                    "light", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Light)), "heavy", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Heavy)),
                    "heaviest", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Heaviest))));
            box.Line(most >= 0 ? UtilitiesText.T("reallife_net_speed_shop_here", "most", most.ToString())
                : UtilitiesText.T("reallife_net_speed_shop", "share", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Shop))));
        }

        // The player's online shop runs from the place (asked of the shops; none with them off).
        private static bool ShopAt(PlaceInfo place)
        {
            Func<BuildingRegistration, int, int> shopMost = UtilitiesBridges.ShopMost;
            return Settings.UtilitiesNetSpeed && place.Kind != PlaceKind.Home && shopMost != null && shopMost(place.Reg, NetSpeedRules.Unknown) >= 0;
        }

        private void Reach(PlaceInfo place)
        {
            _reach.Clear();
            string district = GameRegistrations.District(place.Reg);
            for (int p = 0; p < InternetRules.Count; p++)
            {
                if (InternetRules.Reaches(p, district))
                {
                    _reach.Add(p);
                }
            }
            if (_director.LineSite(place))
            {
                _reach.Add(InternetCatalogue.LeasedIndex);   // business fibre, sold to a data centre only
                if (_director.OwnLineHere(place))
                {
                    _reach.Add(InternetCatalogue.OwnLineIndex);   // ...and the company's own, where its network is open
                }
            }
        }

        private int ReachIndex(PlaceInfo place, int provider)
        {
            Reach(place);
            return _reach.IndexOf(provider);
        }
    }
}
