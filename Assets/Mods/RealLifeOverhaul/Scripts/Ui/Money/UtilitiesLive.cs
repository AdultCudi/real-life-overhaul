using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Utilities;
using Streets;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Utilities screen's facts from the save and carries each button to the feature that already owns it, as the old
    /// app's box did: the same director methods, the same refusals as notices, the same money. Rebuilt only when the utilities
    /// or the day series change, or after a button. Nothing a frame.
    /// </summary>
    public sealed class UtilitiesLive : IUtilitiesSource, IUiTabWant
    {
        private int _wanted = -1;

        public static readonly UtilitiesLive Instance = new UtilitiesLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly List<Account> _accounts = new List<Account>(24);
        private readonly List<PlaceInfo> _places = new List<PlaceInfo>(16);
        private readonly int[] _raw = new int[60];
        private int _local;

        private UtilitiesLive() { }

        /// <summary>A notice or a button (Tech's Internet plan) asked for a tab: the page shows it the next time it lays out.</summary>
        public static void Want(int tab)
        {
            Instance._wanted = tab;
            Instance._local++;
        }

        public int TakeWanted()
        {
            int tab = _wanted;
            _wanted = -1;
            return tab;
        }

        /// <summary>The words the screen writes with: the game's texts and money as the game writes it.</summary>
        public static UtilitiesWords Words() => new UtilitiesWords(UiText.Words, UtilitiesText.Money, () => UtilitiesText.Culture);

        public int Generation => (UtilitiesDirector.Instance != null ? UtilitiesDirector.Instance.Revision : 0) + DaySeriesDirector.Generation + _local;

        public void Read(UtilitiesFacts f)
        {
            UtilitiesDirector d = UtilitiesDirector.Instance;
            UtilitiesState state = d != null ? d.State : null;
            _accounts.Clear();
            _places.Clear();
            f.Ready = state != null && SaveGameManager.Current != null;
            if (!f.Ready)
            {
                f.Accounts = new UtilityAccountFact[0];
                f.Places = new UtilityPlaceFact[0];
                return;
            }
            int today = UtilitiesGame.Today;
            f.Day = today;
            f.Office = d.OfficeName();
            f.ByPhone = d.ByPhone;
            f.InstallFee = InternetCatalogue.InstallFee;
            f.InstallDays = InternetCatalogue.InstallDays;
            f.ContractMonths = InternetCatalogue.ContractMonths;
            f.GraceLeft = InternetRules.GraceLeft(state, today);
            f.GraceDays = InternetCatalogue.GraceDays;
            f.GraceUntilDay = state.GraceUntil - 1;
            ReadAccounts(f, d, state, today);
            ReadPlaces(f, d, state, today);
            ReadNetwork(f.Network, d, state, today);
        }

        // ------------------------------------------------------------------------------------------------- accounts

        private void ReadAccounts(UtilitiesFacts f, UtilitiesDirector d, UtilitiesState state, int today)
        {
            for (int i = 0; i < state.Accounts.Count; i++)
            {
                _accounts.Add(state.Accounts[i]);
            }
            _accounts.Sort((a, b) => b.Owed != a.Owed ? b.Owed.CompareTo(a.Owed) : string.CompareOrdinal(a.Place, b.Place));
            var list = new UtilityAccountFact[_accounts.Count];
            int owed = 0, month = 0;
            for (int i = 0; i < list.Length; i++)
            {
                Account a = _accounts[i];
                int sofar = UtilitiesRules.Money(a.Accrued);
                owed += a.Owed;
                month += sofar;
                bool late = a.Owed > 0 && a.DueDay > 0 && today > a.DueDay;
                AccountOffer offer = UtilitiesRules.Offer(a);
                var fact = new UtilityAccountFact
                {
                    Key = a.Place + "|" + (int)a.Service, Place = d.NameOf(a), Service = (int)a.Service, Home = a.Kind == PlaceKind.Home, Provider = UtilitiesText.ProviderOf(a),
                    State = BillState(a, late), StateText = UtilitiesText.State(a, today), SoFar = sofar, Owed = a.Owed, DueDay = a.DueDay, AutoDebit = a.AutoDebit,
                    CanDebit = a.State != SupplyState.Closed, LastBill = a.LastBill, LastBillDay = a.LastBillDay, Deposit = a.Deposit,
                    DepositLeft = Math.Max(0, UtilitiesCatalogue.DepositBackBills - a.OnTime),
                    Offer = offer == AccountOffer.Reconnect ? UtilityOffer.Reconnect : offer == AccountOffer.None ? UtilityOffer.None : UtilityOffer.Apply
                };
                if (a.Service != Service.Internet)
                {
                    fact.Units = (a.Units / 1000f).ToString(a.Service == Service.Power ? "0.0" : "0.00", Inv);
                }
                if (fact.Offer != UtilityOffer.None)
                {
                    fact.ApplyCost = d.ApplicationCost(a, out fact.ApplyDeposit, out fact.ApplyFee);
                }
                list[i] = fact;
            }
            f.Accounts = list;
            f.Owed = owed;
            f.Month = month;
        }

        private static UtilityBillState BillState(Account a, bool late)
        {
            switch (a.State)
            {
                case SupplyState.Pending: return UtilityBillState.Pending;
                case SupplyState.Temporary: return UtilityBillState.Temporary;
                case SupplyState.Cut: return UtilityBillState.Cut;
                case SupplyState.Closed: return UtilityBillState.Closed;
                default: return a.Owed > 0 ? late ? UtilityBillState.Overdue : UtilityBillState.Due : UtilityBillState.Paid;
            }
        }

        // ------------------------------------------------------------------------------------------------- places and their plans

        private void ReadPlaces(UtilitiesFacts f, UtilitiesDirector d, UtilitiesState state, int today)
        {
            int hour = today * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0);
            IReadOnlyList<PlaceInfo> places = d.Places;
            var list = new UtilityPlaceFact[places.Count];
            bool running = state.Network.Running;
            for (int i = 0; i < list.Length; i++)
            {
                PlaceInfo place = places[i];
                _places.Add(place);
                Account a = state.Find(place.Place, Service.Internet);
                bool live = a != null && a.State != SupplyState.Closed;
                Provider p = live ? InternetRules.Get(a.Provider) : null;
                bool needs = UtilitiesDirector.NeedsNet(place);
                string id = GameRegistrations.District(place.Reg);
                var fact = new UtilityPlaceFact
                {
                    Key = place.Place, Name = place.Name, Home = place.Kind == PlaceKind.Home, Connected = live, Needs = needs, Orders = !needs && ShopAt(place),
                    District = !string.IsNullOrEmpty(id) ? id.GetLocalization() : string.Empty, Grace = InternetRules.InGrace(state, place.Place, today),
                    Owes = a != null && a.Owed > 0
                };
                if (live)
                {
                    fact.Cut = a.State == SupplyState.Cut;
                    fact.Down = !fact.Cut && InternetRules.InOutage(a, hour);
                    fact.Pending = a.State == SupplyState.Pending;
                    fact.ProviderIndex = a.Provider;
                    fact.Provider = p != null ? UtilitiesText.T(p.NameKey) : UtilitiesText.T("reallife_net_none");
                    fact.Mbps = p != null ? p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)] : 0;
                    fact.PriceCents = a.PriceCents;
                    fact.StateText = UtilitiesText.State(a, today);
                    fact.OnContract = a.ContractUntil > today;
                    fact.ContractText = fact.OnContract
                        ? UtilitiesText.T("reallife_net_box_contract_until", "day", UtilitiesText.Day(a.ContractUntil), "fee", UtilitiesText.Money(d.NetExitFee(place)))
                        : UtilitiesText.T("reallife_net_month_by_month");
                    fact.ExitFee = d.NetExitFee(place);
                }
                fact.Options = Options(d, state, place, id, a, live, running);
                list[i] = fact;
            }
            f.Places = list;
        }

        // Every plan of every provider that reaches the place, the cheapest providers' plans in the catalogue's order, the company's last.
        private static UtilityOptionFact[] Options(UtilitiesDirector d, UtilitiesState state, PlaceInfo place, string district, Account a, bool live, bool running)
        {
            var options = new List<UtilityOptionFact>(12);
            for (int p = 0; p < InternetRules.Count; p++)
            {
                if (!InternetRules.Reaches(p, district))
                {
                    continue;
                }
                Provider provider = InternetRules.Get(p);
                for (int i = 0; i < provider.Mbps.Length; i++)
                {
                    options.Add(new UtilityOptionFact
                    {
                        Provider = p, Plan = i, ProviderName = UtilitiesText.T(provider.NameKey), Kind = UtilitiesText.T(provider.KindKey), Mbps = provider.Mbps[i],
                        ContractCents = d.NetPrice(place, p, i, true), MonthlyCents = d.NetPrice(place, p, i, false), Own = p == InternetCatalogue.OwnIndex && running,
                        Current = live && a.Provider == p && a.Plan == i, Speed = SpeedWords(place, provider.Mbps[i])
                    });
                }
            }
            return options.ToArray();
        }

        // What a plan's speed means for the work there, from the same rules the work goes by: nothing at a home or while speed does not count.
        private static string SpeedWords(PlaceInfo place, int mbps)
        {
            if (!Settings.UtilitiesNetSpeed || place.Kind == PlaceKind.Home)
            {
                return null;
            }
            Func<BuildingRegistration, int, int> firmWork = UtilitiesBridges.FirmWork, shopMost = UtilitiesBridges.ShopMost;
            int work = firmWork != null ? firmWork(place.Reg, mbps) : -1, most = shopMost != null ? shopMost(place.Reg, mbps) : -1;
            string first = work >= 0 ? UtilitiesText.T("reallife_net_speed_firm", "work", UtilitiesText.Percent(work))
                : UtilitiesText.T("reallife_net_speed_tech", "work", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Usual)),
                    "light", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Light)), "heavy", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Heavy)),
                    "heaviest", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Heaviest)));
            string second = most >= 0 ? UtilitiesText.T("reallife_net_speed_shop_here", "most", most.ToString(Inv))
                : UtilitiesText.T("reallife_net_speed_shop", "share", UtilitiesText.Percent(NetSpeedRules.Factor(mbps, NetSpeedRules.Shop)));
            return first + " " + second;
        }

        // The player's online shop runs from the place (asked of the shops; none with them off).
        private static bool ShopAt(PlaceInfo place)
        {
            Func<BuildingRegistration, int, int> shopMost = UtilitiesBridges.ShopMost;
            return Settings.UtilitiesNetSpeed && place.Kind != PlaceKind.Home && shopMost != null && shopMost(place.Reg, NetSpeedRules.Unknown) >= 0;
        }

        // ------------------------------------------------------------------------------------------------- the broadband company

        private void ReadNetwork(UtilityNetworkFact f, UtilitiesDirector d, UtilitiesState state, int today)
        {
            Network n = state.Network;
            int level = Prices.Level, buy = EndScaleRules.Buying(Settings.EndScale), monthDays = GameCalendar.MonthDays;
            BuildingRegistration hq = UtilitiesGame.Headquarters();
            f.State = n.State == CompanyState.Running ? 1 : n.State == CompanyState.Sold ? 2 : 0;
            f.FoundedDay = n.FoundedDay;
            f.SoldDay = n.SoldDay;
            f.SalePrice = n.SalePrice;
            f.HasHeadquarters = hq != null;
            f.HeadquartersName = hq != null ? (string.IsNullOrEmpty(hq.BusinessName) ? hq.Address.ToFormattedString() : hq.BusinessName) : string.Empty;
            f.Licence = BroadbandRules.LicenceFee(level, buy);
            f.DigLeast = BroadbandRules.BuildCostRange(level, buy, false);
            f.DigMost = BroadbandRules.BuildCostRange(level, buy, true);
            f.BuildMonths = BroadbandCatalogue.BuildMonths;
            f.BuyerName = UtilitiesText.T(BroadbandCatalogue.SoldNameKey);
            var districts = new UtilityDistrictFact[Network.Districts];
            for (int i = 0; i < districts.Length; i++)
            {
                bool open = n.Open(i, today), digging = n.Digging(i);
                int upkeep = (int)EndScaleRules.Mix((long)BroadbandRules.BuildCost(i, level) * BroadbandCatalogue.UpkeepPerMille / 1000,
                    (long)BroadbandRules.BuildCost(i, level, EndScaleRules.Full) * BroadbandCatalogue.UpkeepPerMilleScale / 1000, buy);
                districts[i] = new UtilityDistrictFact
                {
                    Name = BroadbandCompany.District(i), State = open ? 2 : digging ? 1 : 0, Homes = MarketShown(n, i, buy, open || digging),
                    Customers = n.Running ? BroadbandRules.Connections(n, i) : 0, ReadyDay = n.ReadyDay(i), DigCost = BroadbandRules.BuildCost(i, level, buy),
                    ExtendCost = BroadbandRules.ExtendCost(n, i, level, today, buy), Upkeep = upkeep
                };
            }
            f.Districts = districts;
            int connections = n.Running ? BroadbandRules.Connections(n) : 0;
            f.Customers = connections;
            f.TakingsDay = n.Running ? BroadbandRules.DayTakings(n, level, monthDays, false, true) : 0;
            f.UpkeepDay = 0;
            if (n.Running)
            {
                BroadbandRules.DayCosts(n, level, today, monthDays, out int staff, out int network, out int marketing);
                f.UpkeepDay = staff + network + marketing;
            }
            var plans = new UtilityPlanFact[BroadbandCatalogue.Mbps.Length];
            for (int k = 0; k < plans.Length; k++)
            {
                plans[k] = new UtilityPlanFact
                {
                    Mbps = BroadbandCatalogue.Mbps[k], PriceCents = BroadbandRules.PlanCents(n, k, level), Customers = (int)((long)connections * BroadbandCatalogue.TierMix[k] / 1000)
                };
            }
            f.Plans = plans;
            var rivals = new UtilityRivalFact[InternetCatalogue.Providers.Length];
            for (int i = 0; i < rivals.Length; i++)
            {
                Provider p = InternetCatalogue.Providers[i];
                int least = int.MaxValue, most = 0;
                for (int k = 0; k < p.Mbps.Length; k++)
                {
                    least = Math.Min(least, p.Mbps[k]);
                    most = Math.Max(most, p.Mbps[k]);
                }
                rivals[i] = new UtilityRivalFact
                {
                    Name = UtilitiesText.T(p.NameKey), Kind = UtilitiesText.T(p.KindKey), Districts = p.Districts.Length, FromMbps = least == int.MaxValue ? 0 : least, ToMbps = most,
                    FromCents = InternetRules.MonthCents(i, 0, false, true, level, state.PromoOf(i))
                };
            }
            f.Rivals = rivals;
            f.PriceShare = n.PriceShare;
            f.PriceLeast = BroadbandCatalogue.PriceLeast;
            f.PriceMost = BroadbandCatalogue.PriceMost;
            f.PriceStep = BroadbandCatalogue.PriceStep;
            f.Maintenance = n.Maintenance;
            f.MaintenanceLeast = BroadbandCatalogue.MaintenanceLeast;
            f.MaintenanceMost = BroadbandCatalogue.MaintenanceMost;
            f.MaintenanceStep = BroadbandCatalogue.MaintenanceStep;
            f.Marketing = n.Marketing;
            f.OutagePerMille = BroadbandRules.OutagePerMille(n.Maintenance);
            f.MarketingBudgets = new int[BroadbandCatalogue.Marketing.Length];
            for (int i = 0; i < f.MarketingBudgets.Length; i++)
            {
                f.MarketingBudgets[i] = i == 0 ? 0 : BroadbandRules.MarketingMoney(i, level);
            }
            f.SaleValue = n.Running ? BroadbandRules.SaleNet(n) : 0L;
            f.Owed = n.Owed;
            Series(f, n, today);
        }

        // A district's homes and businesses the company reaches once dug, or would if dug now (at scale, the whole district).
        private static int MarketShown(Network n, int d, int buy, bool built)
        {
            return built ? BroadbandRules.MarketOf(n, d)
                : (int)((long)EndScaleRules.Mix(BroadbandCatalogue.Market[d], BroadbandRules.MarketAtScale(d), buy) * BroadbandRules.PeopleOf(n, d) / 1000);
        }

        // The customers over the last sixty days, from the day the company opened: as many points as there are days of history.
        private void Series(UtilityNetworkFact f, Network n, int today)
        {
            f.Series = new int[0];
            f.SeriesFirstDay = today;
            DaySeriesBook book = DaySeriesDirector.Book;
            if (book == null || !n.Running || today <= 0)
            {
                return;
            }
            book.Window(DaySeriesCatalogue.Customers, today, _raw.Length, _raw);
            int first = -1;
            for (int i = 0; i < _raw.Length; i++)
            {
                int day = today - (_raw.Length - 1 - i);
                if (_raw[i] != DaySeriesCatalogue.NoValue && day >= n.FoundedDay)
                {
                    first = i;
                    break;
                }
            }
            if (first < 0)
            {
                return;
            }
            var values = new int[_raw.Length - first];
            int last = 0;
            for (int i = first; i < _raw.Length; i++)
            {
                last = _raw[i] != DaySeriesCatalogue.NoValue ? _raw[i] : last;
                values[i - first] = last;
            }
            f.Series = values;
            f.SeriesFirstDay = today - (_raw.Length - 1 - first);
        }

        // ------------------------------------------------------------------------------------------------- the buttons

        public bool Do(UtilitiesAct act, int a, int b, int c, int d)
        {
            UtilitiesDirector director = UtilitiesDirector.Instance;
            if (director == null || director.State == null)
            {
                return false;
            }
            bool done;
            try
            {
                done = Run(director, act, a, b, c, d);
            }
            catch (Exception e)
            {
                UtilitiesDirector.Logger?.Error(e);
                done = false;
            }
            _local++;
            return done;
        }

        private bool Run(UtilitiesDirector director, UtilitiesAct act, int a, int b, int c, int d)
        {
            BroadbandCompany company = director.Company;
            string why = null;
            bool done;
            int today = UtilitiesGame.Today;
            switch (act)
            {
                case UtilitiesAct.Pay:
                    if (a < 0 || a >= _accounts.Count)
                    {
                        return false;
                    }
                    done = director.Pay(_accounts[a], today);
                    why = done ? null : "reallife_util_desk_no_cash";
                    break;
                case UtilitiesAct.Apply:
                    if (a < 0 || a >= _accounts.Count)
                    {
                        return false;
                    }
                    Account account = _accounts[a];
                    done = director.ApplyByPhone(account, out why);
                    if (done)
                    {
                        UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_toast_applied", "service", UtilitiesText.Service(account.Service),
                            "place", director.NameOf(account), "day", UtilitiesText.Day(account.ConnectDay)), 10f);
                    }
                    else
                    {
                        why = why ?? "reallife_util_desk_not_now";
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why, "office", director.OfficeName()), 8f);
                        why = null;
                    }
                    break;
                case UtilitiesAct.Debit:
                    if (a < 0 || a >= _accounts.Count || _accounts[a].State == SupplyState.Closed)
                    {
                        return false;
                    }
                    _accounts[a].AutoDebit = b == 1;
                    done = true;
                    break;
                case UtilitiesAct.Buy:
                    done = a >= 0 && a < _places.Count && director.BuyNet(_places[a], b, c, d == 1, out why);
                    break;
                case UtilitiesAct.Cancel:
                    done = a >= 0 && a < _places.Count && director.CancelNet(_places[a], out why);
                    break;
                case UtilitiesAct.Reconnect:
                    done = a >= 0 && a < _places.Count && director.ReconnectNet(_places[a], out why);
                    break;
                case UtilitiesAct.Found:
                    done = company != null && company.Found(out why);
                    break;
                case UtilitiesAct.Dig:
                    done = company != null && company.Build(a, out why);
                    break;
                case UtilitiesAct.Extend:
                    done = company != null && company.Extend(a, out why);
                    break;
                case UtilitiesAct.Prices:
                    company?.SetPrices(a);
                    done = company != null;
                    break;
                case UtilitiesAct.Maintenance:
                    company?.SetMaintenance(a);
                    done = company != null;
                    break;
                case UtilitiesAct.Marketing:
                    company?.SetMarketing(a);
                    done = company != null;
                    break;
                case UtilitiesAct.Sell:
                    done = company != null && company.Sell(out why);
                    break;
                default:
                    return false;
            }
            if (!done && why != null)
            {
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why), 8f);
            }
            return done;
        }
    }
}
