using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private const int PageExits = 3;
        private static int _counterStep = 1, _bankStep, _approachStep;

        /// <summary>The whole keys of the ways to counter an offer and to hold the roadshow, so the text checks can find each one.</summary>
        public static readonly string[] CounterKeys = { "reallife_exit_counter_0", "reallife_exit_counter_1", "reallife_exit_counter_2" };
        public static readonly string[] RoadKeys = { "reallife_exit_road_0", "reallife_exit_road_1", "reallife_exit_road_2" };

        private sealed partial class Page
        {
            // A firm's page of exits: the offers for it, its own shares sold later, and the way to the market.
            private void ExitsPage(FirmPlace firm)
            {
                string place = firm.File.Place;
                ExitFile exits = _d.Exits;
                FirmExit e = exits != null ? exits.Exit(place, false) : null;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_cap");
                _m.Head = new UiHeadModel { Name = TechText.Fill("reallife_exit_title", "firm", firm.File.Name), Icon = UiIcon.Handshake, Role = TechText.FirmName(firm.File.K) };
                if (exits == null)
                {
                    var none = new TechCard(_runs, TechText.T("reallife_ui_tech_exit_offers"));
                    none.Say(TechText.T("reallife_exit_off"));
                    _m.Left = new[] { none.Build() };
                    return;
                }
                _m.Left = new[] { OffersCard(firm, e), SharesCard(firm) };
                _m.Right = new[] { MarketCard(firm, e) };
            }

            private UiFactCardModel OffersCard(FirmPlace firm, FirmExit e)
            {
                string place = firm.File.Place;
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_exit_offers"));
                if (e == null || e.Offers.Count == 0)
                {
                    card.Say(TechText.Fill("reallife_exit_offer_none", "firm", firm.File.Name, "least", Cents(_d.Level(ExitCatalogue.OfferMrrLeast))));
                    if (e != null && e.SoldDay > 0)
                    {
                        card.Say(TechText.Fill("reallife_exit_sold_line", "buyer", e.SoldTo, "amount", Cents(_d.Level(e.SoldFor)), "day", e.SoldDay.ToString(Inv)));
                    }
                    return card.Build();
                }
                for (int i = 0; i < e.Offers.Count; i++)
                {
                    ExitOffer o = e.Offers[i];
                    int id = o.Id;
                    card.Say(TechText.Fill(o.Hire ? "reallife_exit_hire_line" : "reallife_exit_offer_line", "buyer", o.Name, "amount", Cents(_d.Level(o.Price)), "firm", firm.File.Name,
                        "day", o.Lapse.ToString(Inv)), UiRole.OkText);
                    SaleResult r = _d.ExitPreview(place, o);
                    string after = r.Owed <= 0 ? TechText.Fill("reallife_exit_offer_free", "you", Cents(_d.Level(Math.Max(0L, r.Player))))
                        : r.Gap > 0 ? TechText.Fill("reallife_exit_offer_gap", "owed", Cents(_d.Level(r.Owed)), "paid", Cents(_d.Level(r.Paid)), "gap", Cents(_d.Level(r.Gap)))
                        : TechText.Fill("reallife_exit_offer_after", "investors", Cents(_d.Level(r.Paid)), "you", Cents(_d.Level(Math.Max(0L, r.Player))));
                    card.Say(after, r.Gap > 0 ? UiRole.WarnText : UiRole.None);
                    string title = TechText.Fill("reallife_exit_confirm_accept_title", "firm", firm.File.Name);
                    string body = TechText.Fill(o.Hire ? "reallife_exit_confirm_hire_body" : "reallife_exit_confirm_accept_body", "firm", firm.File.Name, "buyer", o.Name,
                        "amount", Cents(_d.Level(o.Price)));
                    card.Button(TechText.T("reallife_exit_button_accept"), i == 0 ? UiButtonKind.Primary : UiButtonKind.Secondary, UiAction.Count, Ask(title, body,
                        () => _d.ExitAccept(place, id)));
                    card.Button(TechText.T("reallife_exit_button_decline"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.ExitDecline(place, id)));
                }
                ExitOffer first = e.Offers[0];
                if (!first.Countered && !first.Hire)
                {
                    _counterStep = Math.Max(0, Math.Min(CounterKeys.Length - 1, _counterStep));
                    int at = _counterStep, offer = first.Id;
                    card.Step(TechText.T("reallife_ui_tech_exit_counter"), TechText.T(CounterKeys[at]), null, at > 0, at < CounterKeys.Length - 1,
                        Go(v => _counterStep = at - 1), Go(v => _counterStep = at + 1));
                    card.Button(TechText.T("reallife_exit_button_counter"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.ExitCounter(place, offer, at)));
                }
                return card.Build();
            }

            private UiFactCardModel SharesCard(FirmPlace firm)
            {
                string place = firm.File.Place;
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_exit_shares"));
                int locked = _d.ExitLockLeft(place);
                long room = _d.ExitRoom(place, out long cents, out int discount);
                if (locked > 0)
                {
                    card.Say(TechText.Fill("reallife_exit_shares_locked", "days", locked.ToString(Inv)), UiRole.WarnText);
                }
                else if (room > 0)
                {
                    card.Say(TechText.Fill("reallife_exit_shares_room", "shares", TechText.Whole(room), "amount", Cents(_d.Level(cents)), "discount", TechText.Mille(discount)));
                    card.Button(TechText.T("reallife_exit_button_sell"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.ExitSell(place)));
                }
                else
                {
                    card.Say(TechText.Fill("reallife_exit_shares_none", "days", ExitCatalogue.SecondaryDays.ToString(Inv), "lock", ExitCatalogue.LockUpDays.ToString(Inv)));
                }
                return card.Build();
            }

            private UiFactCardModel MarketCard(FirmPlace firm, FirmExit e)
            {
                string place = firm.File.Place;
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_exit_market"));
                long mrr = FirmsRules.Arr(_f, place) / 12;
                if (e == null || e.Stage == ListStage.None || e.Stage == ListStage.CalledOff)
                {
                    if (e != null && e.Stage == ListStage.CalledOff)
                    {
                        card.Say(TechText.Fill("reallife_exit_market_called", "firm", firm.File.Name, "day", e.Called.ToString(Inv)), UiRole.WarnText);
                    }
                    if (mrr < ExitCatalogue.ListMrrLeast)
                    {
                        card.Say(TechText.Fill("reallife_exit_market_not_ready", "least", Cents(_d.Level(ExitCatalogue.ListMrrLeast)), "now", Cents(_d.Level(mrr)), "firm", firm.File.Name));
                    }
                    else
                    {
                        IReadOnlyList<string> banks = _d.ExitBanks();
                        _bankStep = Math.Max(0, Math.Min(banks.Count - 1, _bankStep));
                        int at = _bankStep, n = banks.Count;
                        card.Say(TechText.T("reallife_exit_market_start"));
                        long fee = ExitRules.Retainer(FirmValueNow(firm));
                        card.Step(TechText.T("reallife_ui_tech_exit_bank"), TechText.Fill("reallife_exit_bank_line", "bank", banks[at], "fee", Cents(_d.Level(fee))), null, n > 1, n > 1,
                            Go(v => _bankStep = (at + n - 1) % n), Go(v => _bankStep = (at + 1) % n));
                        card.Button(TechText.T("reallife_exit_button_bankers"), UiButtonKind.Primary, UiAction.Count, Do(() => _d.ExitBankers(place, at)));
                    }
                }
                else if (e.Stage == ListStage.Bankers)
                {
                    card.Say(TechText.Fill("reallife_exit_market_bankers", "bank", e.BankName, "fee", Cents(_d.Level(e.Fee))));
                    _approachStep = Math.Max(0, Math.Min(RoadKeys.Length - 1, _approachStep));
                    int at = _approachStep;
                    card.Step(TechText.T("reallife_ui_tech_exit_road"), TechText.T(RoadKeys[at]), null, at > 0, at < RoadKeys.Length - 1, Go(v => _approachStep = at - 1),
                        Go(v => _approachStep = at + 1));
                    card.Button(TechText.T("reallife_exit_button_road"), UiButtonKind.Primary, UiAction.Count, Do(() => _d.ExitRoadshow(place, at)));
                    card.Button(TechText.T("reallife_exit_button_withdraw"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.ExitWithdraw(place)));
                }
                else if (e.Stage == ListStage.Roadshow)
                {
                    card.Say(TechText.Fill("reallife_exit_market_wait", "demand", TechText.Mille(e.Demand), "day", e.PriceDay.ToString(Inv), "low", Cents(_d.Level(e.Low)),
                        "high", Cents(_d.Level(e.High))));
                    card.Button(TechText.T("reallife_exit_button_withdraw"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.ExitWithdraw(place)));
                }
                else
                {
                    card.Say(TechText.Fill("reallife_exit_market_listed", "firm", firm.File.Name, "price", Cents(_d.Level(e.Price)), "gross", Cents(_d.Level(e.Gross)), "net",
                        Cents(_d.Level(e.Net)),
                        "bank", e.BankName), UiRole.OkText);
                    int locked = _d.ExitLockLeft(place);
                    card.Say(locked > 0 ? TechText.Fill("reallife_exit_shares_locked", "days", locked.ToString(Inv)) : TechText.T("reallife_exit_market_free"), locked > 0
                        ? UiRole.WarnText : UiRole.None);
                }
                card.Button(TechText.T("reallife_exit_button_whole"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.ListWholeCompany(place)));
                return card.Build();
            }

            private long FirmValueNow(FirmPlace firm)
            {
                _d.FirmValue(firm, out long value, out _);
                return value;
            }

            // The founder and the office of a city startup: who they are, where to find them. Under a startup's own page.
            private UiFactCardModel[] StartupPlace(Startup s)
            {
                FounderRec r = _d.FounderOf(s.Id);
                if (r == null)
                {
                    return new UiFactCardModel[0];
                }
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_exit_place"));
                card.Say(TechText.Fill(r.Linked ? "reallife_exit_founder_city" : "reallife_exit_founder_invented", "founder", s.Founder, "age", r.Age.ToString(Inv), "skill",
                    r.Skill.ToString(Inv)));
                if (r.Hired > 0)
                {
                    card.Say(TechText.Fill("reallife_exit_team", "count", r.Hired.ToString(Inv)));
                }
                if (_d.StartupFloor(s, out string tower, out int floor, out string street, out int number))
                {
                    card.Say(TechText.Fill("reallife_exit_office", "floor", floor.ToString(Inv), "tower", tower, "address", FirmsGame.AddressWords(street, number)));
                    card.Button(TechText.T("common_setdestination"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.StartupDestination(s)));
                }
                else
                {
                    card.Say(TechText.T("reallife_exit_office_none"));
                }
                return new[] { card.Build() };
            }
        }
    }
}
