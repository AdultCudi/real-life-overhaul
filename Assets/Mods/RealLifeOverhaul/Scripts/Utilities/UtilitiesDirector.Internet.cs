using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The utilities' internet part: the providers' month, outages, businesses that need a connection, the grace, and plans bought, changed, cancelled or reconnected.</summary>
    public sealed partial class UtilitiesDirector
    {
        // The providers' month: each rival's price walks (InternetRules.NextPromo), held down where the player's company has
        // taken customers (BroadbandRules.AnswerCap); a plan out of contract is priced again; the market moves.
        private void NewMonth(int day, int level)
        {
            int answered = 0;
            for (int p = 0; p < InternetCatalogue.Providers.Length && p < _state.Promo.Length; p++)
            {
                int walked = InternetRules.NextPromo(_state.Seed, _state.Month, p, _state.Promo[p]);
                _state.Promo[p] = Math.Max(InternetCatalogue.PromoLeast, Math.Min(walked, BroadbandRules.AnswerCap(_state.Network, p)));
                answered += _state.Promo[p] < walked ? 1 : 0;
            }
            _state.Month++;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Service == Service.Internet && a.State != SupplyState.Closed && a.ContractUntil <= day && a.Provider >= 0)
                {
                    a.PriceCents = InternetRules.MonthCents(a.Provider, a.Plan, a.BusinessPlan, false, level, _state.PromoOf(a.Provider));
                }
            }
            _company.Month(day, level, GameCalendar.MonthDays, answered);
        }

        // today's outages, drawn from the save's seed: each connection of a provider that fails is down for some hours
        private void Outages(int today)
        {
            for (int p = 0; p < InternetRules.Count; p++)
            {
                if (!InternetRules.Outage(_state.Seed, today, p, false, out int from, out int hours))
                {
                    continue;
                }
                InternetRules.Outage(_state.Seed, today, p, true, out _, out int repaired);
                int affected = 0;
                for (int i = 0; i < _state.Accounts.Count; i++)
                {
                    Account a = _state.Accounts[i];
                    if (a.Service == Service.Internet && a.Provider == p && a.State == SupplyState.Active)
                    {
                        a.OutageFrom = today * 24 + from;
                        a.OutageUntil = a.OutageFrom + (a.BusinessPlan ? repaired : hours);
                        affected++;
                    }
                }
                if (p == InternetCatalogue.OwnIndex && _state.Network.Running)
                {
                    // the player's own network: its customers are not billed for the day (BroadbandCompany.Day)
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_bb_toast_outage", "hour", UtilitiesText.Hour(from), "hours", hours.ToString()), 12f);
                }
                else if (affected > 0)
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_outage", "provider", UtilitiesText.T(InternetRules.Get(p).NameKey),
                        "hour", UtilitiesText.Hour(from), "hours", hours.ToString()), 12f);
                }
            }
        }

        // a business that cannot open without a connection and has none, said once a morning; those in the grace counted in one line
        private void Unconnected()
        {
            int today = UtilitiesGame.Today, graced = 0;
            _index.Build(_state);
            for (int p = 0; p < _places.Count; p++)
            {
                PlaceInfo place = _places[p];
                bool needs = NeedsNet(place);
                Account a = needs ? _index.Find(_state, place.Place, Service.Internet) : null;
                if (needs && !Online(a, today * 24 + 12))
                {
                    if (a == null || a.State == SupplyState.Closed || a.State == SupplyState.Cut)
                    {
                        if (InternetRules.InGrace(_state, place.Place, today))
                        {
                            graced++;
                            continue;
                        }
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_needed", "place", place.Name), 10f);
                    }
                }
            }
            if (graced > 0 && InternetRules.GraceLeft(_state, today) < InternetCatalogue.GraceDays)   // on its first day the notice that began it has just said so
            {
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_grace_left", "left", InternetRules.GraceLeft(_state, today).ToString(),
                    "days", InternetCatalogue.GraceDays.ToString(), "count", graced.ToString(), "day", UtilitiesText.Day(_state.GraceUntil - 1)), 12f);
            }
        }

        // The internet newly in this save: the player's own businesses that need a connection now may open without one for
        // a week on the game's clock; told once, and kept in the save.
        private void BeginGrace(int today)
        {
            var needing = new List<string>(4);
            for (int p = 0; p < _places.Count; p++)
            {
                if (NeedsNet(_places[p]))
                {
                    needing.Add(_places[p].Place);
                }
            }
            int count = InternetRules.BeginGrace(_state, needing, today);
            if (count == 0)
            {
                return;
            }
            var data = new Dictionary<string, string>(3)
            {
                { "count", count.ToString() },
                { "days", InternetCatalogue.GraceDays.ToString() },
                { "day", UtilitiesText.Day(_state.GraceUntil - 1) }
            };
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_grace_start", "count", data["count"], "days", data["days"], "day", data["day"]), 20f);
            UtilitiesBridges.Record?.Invoke("reallife_net_toast_grace_start", data);
            Logger?.Info("Utilities: the internet is new in this save: a grace period for " + count + " business(es) that need a connection, until day " + _state.GraceUntil + ".");
        }

        // a step back of the clock, or a guest's clock set to the host's (any leap of more than a day with Going Public on),
        // is not days played: the grace moves with it
        private void GraceClock(int today)
        {
            int seen = _graceSeen;
            _graceSeen = today;
            if (InternetRules.MoveGrace(_state, seen, today, GoingPublic.Role != GoingPublicRole.Absent))
            {
                Logger?.Info("Utilities: the clock went from day " + seen + " to day " + today + " without those days played; the internet's grace period now ends on day " + _state.GraceUntil + ".");
            }
        }

        /// <summary>A business that cannot open without a connection (web design, software, the headquarters).</summary>
        public static bool NeedsNet(PlaceInfo place)
        {
            return place.Kind != PlaceKind.Home && place.Reg != null && Array.IndexOf(InternetCatalogue.Needs, place.Reg.businessTypeName) >= 0;
        }

        /// <summary>A place has a working connection at an hour (hours from the save's first day).</summary>
        public bool Online(PlaceInfo place, int hour)
        {
            return Online(_state.Find(place.Place, Service.Internet), hour);
        }

        private static bool Online(Account a, int hour)
        {
            return a != null && a.State == SupplyState.Active && !InternetRules.InOutage(a, hour);
        }

        /// <summary>A plan's price a month at a place now, cents (a business plan at a business).</summary>
        public int NetPrice(PlaceInfo place, int provider, int plan, bool contract)
        {
            return InternetRules.MonthCents(provider, plan, place.Kind != PlaceKind.Home, contract, Prices.Level, provider >= 0 && provider < _state.Promo.Length ? _state.Promo[provider] : 1000);
        }

        /// <summary>The fee for leaving the place's connection now (a contract left early); nothing without one.</summary>
        public int NetExitFee(PlaceInfo place)
        {
            Account a = _state.Find(place.Place, Service.Internet);
            return a != null && a.State != SupplyState.Closed ? InternetRules.ExitFee(a, UtilitiesGame.Today, GameCalendar.MonthDays) : 0;
        }

        /// <summary>A plan bought or changed: another provider costs the exit fee and a visit, the same one changes at once.</summary>
        public bool BuyNet(PlaceInfo place, int provider, int plan, bool contract, out string why)
        {
            why = null;
            int today = UtilitiesGame.Today, monthDays = GameCalendar.MonthDays;
            if (!InternetRules.Reaches(provider, GameRegistrations.District(place.Reg)))
            {
                why = "reallife_net_no_reach";
                return false;
            }
            Account a = _state.Find(place.Place, Service.Internet);
            if (a != null && a.Owed > 0)
            {
                why = "reallife_net_owed_first";
                return false;
            }
            bool ours = provider == InternetCatalogue.OwnIndex && _state.Network.Running;
            contract &= !ours;   // no contract with one's own company, nor installation fee: its engineers do it
            bool live = a != null && a.State != SupplyState.Closed && a.State != SupplyState.Cut;
            bool same = live && a.Provider == provider;
            int exit = live && !same ? InternetRules.ExitFee(a, today, monthDays) : 0;
            int install = same || contract || ours ? 0 : InternetCatalogue.InstallFee;
            int price = NetPrice(place, provider, plan, contract);
            if (exit + install > UtilitiesGame.Cash)
            {
                why = "reallife_net_no_cash";
                return false;
            }
            if (exit > 0)
            {
                UtilitiesGame.Pay(exit, "reallife_transaction_util_net_exit", place.Reg, deductible: true);
            }
            if (install > 0)
            {
                UtilitiesGame.Pay(install, "reallife_transaction_util_net_install", place.Reg, deductible: true);
            }
            if (a == null)
            {
                a = new Account { Place = place.Place, Kind = place.Kind, AutoDebit = true };
                _state.Accounts.Add(a);
            }
            if (same)
            {
                // the same provider: the plan and price change today; a contract runs on, or begins if one is taken now
                a.Plan = plan;
                a.PriceCents = price;
                if (contract && a.ContractUntil <= today)
                {
                    a.ContractUntil = today + InternetCatalogue.ContractMonths * Math.Max(1, monthDays);
                }
                UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_net_toast_changed", "place", place.Name, "provider", UtilitiesText.T(InternetRules.Get(provider).NameKey),
                    "mbps", InternetRules.Get(provider).Mbps[plan].ToString(), "price", UtilitiesText.Money(price / 100f)), 10f);
            }
            else
            {
                if (a.Accrued > 0)
                {
                    UtilitiesRules.Bill(a, today, monthDays);   // what the old connection came to this month, billed now
                }
                InternetRules.SignUp(a, provider, plan, place.Kind != PlaceKind.Home, contract, today, monthDays, price);
                a.Kind = place.Kind;
                UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_net_toast_bought", "place", place.Name, "provider", UtilitiesText.T(InternetRules.Get(provider).NameKey),
                    "mbps", InternetRules.Get(provider).Mbps[plan].ToString(), "day", UtilitiesText.Day(a.ConnectDay)), 10f);
            }
            Logger?.Info("Utilities: internet at " + place.Place + ": " + InternetRules.Get(provider).Id + " plan " + plan + (contract ? " on contract" : " month by month") + ", " + price
                         + " cents a month" + (exit > 0 ? ", exit fee " + exit : string.Empty) + (install > 0 ? ", installation " + install : string.Empty) + ".");
            _view?.Changed();
            return true;
        }

        /// <summary>A place's connection cancelled: the exit fee of a contract left early, the month so far billed, and it is closed.</summary>
        public bool CancelNet(PlaceInfo place, out string why)
        {
            why = null;
            Account a = _state.Find(place.Place, Service.Internet);
            if (a == null || a.State == SupplyState.Closed)
            {
                return true;
            }
            int today = UtilitiesGame.Today, monthDays = GameCalendar.MonthDays;
            int exit = InternetRules.ExitFee(a, today, monthDays);
            if (exit > 0 && !UtilitiesGame.Pay(exit, "reallife_transaction_util_net_exit", place.Reg, deductible: true))
            {
                why = "reallife_net_no_cash";
                return false;
            }
            UtilitiesRules.Bill(a, today, monthDays);
            a.State = SupplyState.Closed;
            a.ContractUntil = 0;
            a.OutageFrom = a.OutageUntil = 0;
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_net_toast_cancelled", "place", place.Name), 8f);
            _view?.Changed();
            return true;
        }

        /// <summary>A connection cut for an unpaid bill, with nothing owed now, reconnected the next morning.</summary>
        public bool ReconnectNet(PlaceInfo place, out string why)
        {
            why = null;
            Account a = _state.Find(place.Place, Service.Internet);
            if (a == null || a.State != SupplyState.Cut)
            {
                return false;
            }
            if (a.Owed > 0)
            {
                why = "reallife_net_owed_first";
                return false;
            }
            a.State = SupplyState.Pending;
            a.CutForDebt = false;
            a.ConnectDay = UtilitiesGame.Today + 1;
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_net_toast_reconnect", "place", place.Name), 8f);
            _view?.Changed();
            return true;
        }
    }
}
