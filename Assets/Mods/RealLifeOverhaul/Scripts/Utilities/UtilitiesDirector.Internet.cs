using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The utilities' internet part: the providers' month, outages, businesses that need a connection, the grace, and plans bought, changed, cancelled or reconnected.</summary>
    public sealed partial class UtilitiesDirector
    {
        private readonly List<BuildingRegistration> _shopRegs = new List<BuildingRegistration>(12);
        private readonly List<string> _shopPlaces = new List<string>(12);

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
            graced += ShopsGraced(today);
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

        // Speed counts for the online shops for the first time in this save (the switch on, the shops running): those whose
        // building has no working connection join the internet's grace, so a shop that took orders yesterday is not stopped
        // without warning. Told once and kept in the save; asked each hour until the shops can answer, then never again.
        private void MeetSpeed(int today)
        {
            Func<List<BuildingRegistration>, bool> shops = UtilitiesBridges.Shops;
            if (_state.SpeedMet > 0 || !Settings.UtilitiesNetSpeed || shops == null)
            {
                return;
            }
            _shopRegs.Clear();
            if (!shops(_shopRegs))
            {
                return;
            }
            _shopPlaces.Clear();
            for (int i = 0; i < _shopRegs.Count; i++)
            {
                string place = UtilitiesGame.Place(_shopRegs[i]);
                Account a = _state.Find(place, Service.Internet);
                if (a == null || a.State != SupplyState.Active)
                {
                    _shopPlaces.Add(place);
                }
            }
            int added = InternetRules.AddGrace(_state, _shopPlaces, today);
            _state.SpeedMet = Math.Max(1, today);
            _shopRegs.Clear();
            _shopPlaces.Clear();
            Logger?.Info("Utilities: internet speed counts for the online shops from day " + today + "; " + added + " shop(s) with no working connection joined the grace"
                         + (added > 0 ? " until day " + _state.GraceUntil : string.Empty) + ".");
            if (added == 0)
            {
                return;
            }
            var data = new Dictionary<string, string>(3)
            {
                { "count", added.ToString() },
                { "days", InternetCatalogue.GraceDays.ToString() },
                { "day", UtilitiesText.Day(_state.GraceUntil - 1) }
            };
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_shop_grace", "count", data["count"], "days", data["days"], "day", data["day"]), 20f);
            UtilitiesBridges.Record?.Invoke("reallife_net_toast_shop_grace", data);
        }

        // The online shops still in the grace with no working connection, besides those counted as businesses that need one to open.
        private int ShopsGraced(int today)
        {
            Func<List<BuildingRegistration>, bool> shops = UtilitiesBridges.Shops;
            if (_state.GraceUntil <= 0 || shops == null || !Settings.UtilitiesNetSpeed)
            {
                return 0;
            }
            _shopRegs.Clear();
            if (!shops(_shopRegs))
            {
                return 0;
            }
            int count = 0;
            for (int i = 0; i < _shopRegs.Count; i++)
            {
                BuildingRegistration reg = _shopRegs[i];
                string place = UtilitiesGame.Place(reg);
                Account a = _state.Find(place, Service.Internet);
                count += Array.IndexOf(InternetCatalogue.Needs, reg.businessTypeName) < 0 && (a == null || a.State != SupplyState.Active) && InternetRules.InGrace(_state, place, today)
                    ? 1 : 0;
            }
            _shopRegs.Clear();
            return count;
        }

        /// <summary>
        /// A building's internet speed for the work over it, Mbps (NetSpeedRules): the plan's while it works, 0 with none,
        /// one being installed, one cut, or (for an hour asked, 0 or more) an outage then; NetSpeedRules.Grace for a place in
        /// the grace with no working plan, and for an online shop before speed has counted for the shops in this save.
        /// </summary>
        public int Speed(BuildingRegistration reg, int hour, bool shop)
        {
            if (_state == null || reg == null)
            {
                return NetSpeedRules.Unknown;
            }
            string place = UtilitiesGame.Place(reg);
            Account a = _state.Find(place, Service.Internet);
            int line = LeasedLineRules.LineMbps(a, UtilitiesGame.Today);   // a data centre's leased line carries on through its plan's outage or visit
            if (a == null || a.State != SupplyState.Active)
            {
                return line > 0 ? line : InternetRules.InGrace(_state, place, UtilitiesGame.Today) || (shop && _state.SpeedMet == 0) ? NetSpeedRules.Grace : 0;
            }
            if (hour >= 0 && InternetRules.InOutage(a, hour))
            {
                return line;
            }
            Provider p = InternetRules.Get(a.Provider);
            return LeasedLineRules.Speed(p != null && a.Plan >= 0 && a.Plan < p.Mbps.Length ? p.Mbps[a.Plan] : 0, line);
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
            if (InternetCatalogue.IsLine(provider))
            {
                // the pickers list the carriers' fibre and the company's own among the providers; it is bought beside the plan
                return BuyLine(place, plan, out why, provider == InternetCatalogue.OwnLineIndex);
            }
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
            Touch();
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
            LeasedLineRules.Drop(a);   // cancelling the connection ends its line too: the line has its own button for dropping only that
            a.ContractUntil = 0;
            a.OutageFrom = a.OutageUntil = 0;
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_net_toast_cancelled", "place", place.Name), 8f);
            Touch();
            return true;
        }

        /// <summary>A place a leased line may be sold to: a warehouse of the player's fitted out as a data centre, and nothing else (the tech sector says which).</summary>
        public bool LineSite(PlaceInfo place)
        {
            Func<BuildingRegistration, bool> site = UtilitiesBridges.LineSite;
            return site != null && place.Kind != PlaceKind.Home && place.Reg != null && site(place.Reg);
        }

        /// <summary>The installation fee of a line at a place now, money (whole); the wait comes from <see cref="LineDays"/>. <paramref name="own"/>: the company's own line.</summary>
        public int LineFee(PlaceInfo place, int step, bool own = false)
        {
            return UtilitiesRules.Money(LeasedLineRules.InstallCents(step, GameRegistrations.District(place.Reg), Prices.Level, own));
        }

        /// <summary>The days from the order to a line being live at a place.</summary>
        public int LineDays(PlaceInfo place, int step, bool own = false)
        {
            return LeasedLineRules.InstallDays(step, GameRegistrations.District(place.Reg), own);
        }

        /// <summary>
        /// True if the player's own company can carry a place's line: it is running and its network is open in the place's district. The line is then the company's own
        /// (<see cref="InternetCatalogue.OwnLine"/>), beside the carriers', which a data centre may still buy there.
        /// </summary>
        public bool OwnLineHere(PlaceInfo place)
        {
            return _state.Network.Running && InternetRules.Reaches(InternetCatalogue.OwnLineIndex, GameRegistrations.District(place.Reg));
        }

        /// <summary>
        /// A leased line ordered for a data centre: the installation fee now, the line live after its wait and billed with the month's internet from then. The plan stays;
        /// a line already running keeps running until a changed one is live. Refused anywhere that is not a data centre.
        /// </summary>
        public bool BuyLine(PlaceInfo place, int step, out string why, bool own = false)
        {
            why = null;
            if (!LineSite(place))
            {
                why = "reallife_net_line_site";
                return false;
            }
            if (own && !OwnLineHere(place))
            {
                why = "reallife_net_line_own_none";
                return false;
            }
            step = LeasedLineRules.Step(step);
            Account a = _state.Find(place.Place, Service.Internet);
            if (a != null && (a.Owed > 0 || a.State == SupplyState.Cut))
            {
                why = a.Owed > 0 ? "reallife_net_owed_first" : "reallife_net_line_cut";   // a cut connection is reconnected first, as for a plan
                return false;
            }
            if (LeasedLineRules.Ordered(a) == step + 1 && LeasedLineRules.OrderedOwn(a) == own)
            {
                why = "reallife_net_line_same";
                return false;
            }
            int today = UtilitiesGame.Today, fee = LineFee(place, step, own), days = LineDays(place, step, own);
            if (fee > UtilitiesGame.Cash)
            {
                why = "reallife_net_no_cash";
                return false;
            }
            if (fee > 0)
            {
                UtilitiesGame.Pay(fee, "reallife_transaction_util_net_line_install", place.Reg, deductible: true);
            }
            if (a == null || a.State == SupplyState.Closed)
            {
                // a data centre with no plan has the line alone: the account holds it, supplied from the start, and bills only the line
                if (a == null)
                {
                    a = new Account { Place = place.Place, Kind = place.Kind, AutoDebit = true };
                    _state.Accounts.Add(a);
                }
                a.Service = Service.Internet;
                a.State = SupplyState.Active;
                a.Provider = -1;
                a.Plan = 0;
                a.PriceCents = 0;
                a.OpenedDay = today;
                a.ContractUntil = 0;
                a.OutageFrom = a.OutageUntil = 0;
                a.Kind = place.Kind;
            }
            LeasedLineRules.Order(a, step, GameRegistrations.District(place.Reg), today, own);
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_net_toast_line_bought", "place", place.Name, "speed", UtilitiesText.Gbps(LeasedLineCatalogue.Mbps[step]),
                "day", UtilitiesText.Day(today + days)), 10f);
            Logger?.Info("Utilities: " + (own ? "own network" : "leased") + " line at " + place.Place + ": step " + step + " (" + LeasedLineCatalogue.Mbps[step] + " Mbps), "
                         + LeasedLineRules.MonthCents(step, Prices.Level, own)
                         + " cents a month, installation " + fee + " money, live on day " + (today + days) + ".");
            Touch();
            return true;
        }

        /// <summary>A place's leased line given up (and any change not yet live): the month so far stays billed, nothing is owed for leaving. A place with no line is left alone.</summary>
        public bool CancelLine(PlaceInfo place, out string why)
        {
            why = null;
            Account a = _state.Find(place.Place, Service.Internet);
            if (!LeasedLineRules.Has(a))
            {
                return true;
            }
            LeasedLineRules.Drop(a);
            if (a.Provider < 0 && a.State != SupplyState.Closed)
            {
                int today = UtilitiesGame.Today, monthDays = GameCalendar.MonthDays;
                UtilitiesRules.Bill(a, today, monthDays);   // a place that had only the line has no connection left: the month so far is billed and the account closed
                a.State = SupplyState.Closed;
            }
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_net_toast_line_cancelled", "place", place.Name), 8f);
            Touch();
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
            Touch();
            return true;
        }
    }
}
