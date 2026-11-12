using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        private const int AlertsMost = 6, DoneMost = 8, SparkDays = 14;

        private int _sparkDay = -1;
        private readonly List<float> _spark = new List<float>(SparkDays);

        private void ReadOverview(CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            CompanyOverviewFacts o = f.Overview;
            CompanyState s = d.State;
            if (v.Sub[CompanyView.Overview] == CompanyView.SubBusiness)
            {
                CompanyShop shop = d.ShopAt(v.Pick[CompanyView.Overview]);
                f.Business = shop != null ? BusinessCard(d, shop, w) : null;
                if (f.Business == null)
                {
                    v.Sub[CompanyView.Overview] = CompanyView.SubMain;
                }
            }
            int managers = d.ManagersOn && d.ManagerData != null ? d.ManagerData.Managers.Count : 0;
            string where = CompanyGame.HeadAddress();
            o.Line = string.IsNullOrEmpty(where)
                ? w.F("reallife_co_head_line_none", "{staff} staff, {managers} managers, run since Day {day}", "staff", s.Staff.Count.ToString(), "managers", managers.ToString(), "day", Math.Max(1,
                    s.FirstDay).ToString())
                : w.F("reallife_co_head_line", "Head office {place}, {staff} staff, {managers} managers, run since Day {day}", "place", where, "staff", s.Staff.Count.ToString(),
                    "managers", managers.ToString(), "day", Math.Max(1, s.FirstDay).ToString());
            ReadBusinesses(o, d, v, w);
            ReadResult(o, d, w);
            ReadWaiting(o, f, d, v, w);
            ReadDone(o, d, w);
            ReadCosts(o, d, w);
            ReadManagersToday(o, d, w);
            ReadTools(o, w);
            if (d.Shops.Count == 1)
            {
                o.Shop = BusinessCard(d, d.Shops[0], w, false);   // one business: its panel stands where a table of one row would
            }
            if (managers == 0 && s.Staff.Count > 0 && d.ManagersOn)
            {
                o.Next = new CompanyWaiting
                {
                    Name = w.T("reallife_co_next_title", "Next: hire a manager"), Line = w.T("reallife_co_next_line", "A manager answers what you hand over, and asks you what is past their limit."),
                    Icon = UiIcon.People, OpenLabel = w.T("reallife_co_open_managers", "Open Managers"), Open = CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers)
                };
            }
        }

        private static void ReadBusinesses(CompanyOverviewFacts o, CompanyDirector d, CompanyView v, Words w)
        {
            IReadOnlyList<CompanyShop> shops = d.Shops;
            // The districts the businesses stand in: the day table can be shown for one of them (the old table's district filter; a company keeps up to 64 businesses).
            var districts = new List<string>(8);
            for (int i = 0; i < shops.Count; i++)
            {
                string name = CompanyText.District(shops[i].District);
                if (!string.IsNullOrEmpty(name) && !districts.Contains(name))
                {
                    districts.Add(name);
                }
            }
            int pick = districts.Count > 1 ? CompanyCatalogue.Clamp(v.Filter & 63, 0, districts.Count) : 0;
            if (districts.Count > 1)
            {
                var options = new List<string>(districts.Count + 1) { w.T("reallife_co_f_all_districts", "All districts") };
                options.AddRange(districts);
                o.Districts = new CompanyFilterBar { Options = options.ToArray(), Chosen = pick, Shift = 0, Bits = 6, Step = options.Count > 4 };
                o.DistrictFilter = v.Filter;
            }
            string only = pick > 0 ? districts[pick - 1] : null;
            var list = new List<CompanyBusiness>(shops.Count);
            long takings = 0, costs = 0;
            bool any = false;
            for (int i = 0; i < shops.Count; i++)
            {
                CompanyShop shop = shops[i];
                if (only != null && CompanyText.District(shop.District) != only)
                {
                    continue;
                }
                int profit = shop.Profit, cost = shop.Takings - shop.Profit;
                list.Add(new CompanyBusiness
                {
                    Place = shop.File.Place, Name = shop.Name, Mark = CompanyFace.Initials(shop.Name), HasBooks = shop.HasBooks,
                    Takings = shop.HasBooks ? w.Money(shop.Takings) : null, Costs = shop.HasBooks ? w.Money(cost) : null,
                    Result = shop.HasBooks ? Signed(profit, w) : null, ResultTone = profit > 0 ? 1 : profit < 0 ? -1 : 0, Waiting = ShopWaiting(d, shop) > 0
                });
                if (shop.HasBooks)
                {
                    any = true;
                    takings += shop.Takings;
                    costs += cost;
                }
            }
            o.Businesses = list.ToArray();
            o.TotalLabel = only != null ? w.F("reallife_co_in_district", "In {district}", "district", only) : w.T("reallife_co_all_businesses", "All businesses");
            if (any)
            {
                long result = takings - costs;
                o.TotalTakings = w.Money((int)takings);
                o.TotalCosts = w.Money((int)costs);
                o.TotalResult = Signed((int)result, w);
                o.TotalTone = result > 0 ? 1 : result < 0 ? -1 : 0;
            }
        }

        private static string Signed(int amount, Words w) => (amount > 0 ? "+" : amount < 0 ? "-" : string.Empty) + w.Money(Math.Abs(amount));

        // How many things at a business wait for the owner: the same count the old app's page makes.
        private static int ShopWaiting(CompanyDirector d, CompanyShop shop)
        {
            CompanyState s = d.State;
            ShopFile file = shop.File;
            int day = d.Today, waiting = 0;
            if (d.On(CompanyPart.Staff))
            {
                waiting += (file.StrikeFrom > 0 ? 1 : 0) + (file.OvertimeAsk > 0 ? 1 : 0);
                for (int i = 0; i < s.Staff.Count; i++)
                {
                    waiting += s.Staff[i].Place == file.Place ? (s.Staff[i].AskDay > 0 ? 1 : 0) + (s.Staff[i].PoachDay > 0 ? 1 : 0) : 0;
                }
            }
            waiting += d.On(CompanyPart.Licences) && LicenceRules.Missing(file, shop.Needs, day) != 0 ? 1 : 0;
            for (int i = 0; d.On(CompanyPart.Cover) && i < s.Claims.Count; i++)
            {
                waiting += s.Claims[i].Place == file.Place && s.Claims[i].Stage == ClaimStage.Open ? 1 : 0;
            }
            return waiting;
        }

        // The day's result of all the businesses together, and the line of the days before it that the game has a statement for.
        private void ReadResult(CompanyOverviewFacts o, CompanyDirector d, Words w)
        {
            o.ResultLabel = w.T("reallife_co_result_label", "Yesterday's result");
            int day = d.Today - 1, result;
            if (!CompanyGame.DayResult(day, out result))
            {
                o.Result = null;
                o.ResultNote = w.F("reallife_co_after_day", "after day {day}", "day", (d.Today + 1).ToString());
                o.Spark = null;
                return;
            }
            o.Result = Signed(result, w);
            o.ResultTone = result > 0 ? 1 : result < 0 ? -1 : 0;
            if (_sparkDay != d.Today)
            {
                _sparkDay = d.Today;
                _spark.Clear();
                for (int k = SparkDays; k >= 1; k--)
                {
                    int r;
                    if (CompanyGame.DayResult(d.Today - k, out r))
                    {
                        _spark.Add(r);
                    }
                }
            }
            o.Spark = _spark.Count >= 2 ? _spark.ToArray() : null;
            if (o.Spark == null)
            {
                o.ResultNote = w.F("reallife_co_after_day", "after day {day}", "day", (d.Today + 1).ToString());
            }
        }

        // What waits for the owner: the questions held at their level (the first is the Lead), then the planner's rows.
        private void ReadWaiting(CompanyOverviewFacts o, CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            var rows = new List<CompanyWaiting>(8);
            if (d.HierarchyOn || d.LeasesOn)
            {
                Escalation first = null;
                var rest = new List<Escalation>(8);
                var held = new List<Escalation>(8);
                if (d.HierarchyOn)
                {
                    HierarchyState h = d.HierarchyData;
                    for (int i = 0; i < h.Escalations.Count; i++)
                    {
                        Escalation e = h.Escalations[i];
                        if (e.Open && e.Level >= EscalationRules.LevelOwner)
                        {
                            held.Add(e);
                        }
                    }
                }
                d.LeaseQuestions(held);   // a landlord's offer is the owner's to answer with the hierarchy off, or with no question raised for it
                for (int i = 0; i < held.Count; i++)
                {
                    Escalation e = held[i];
                    if (first == null && !v.Later.Contains(e.Id))
                    {
                        first = e;
                    }
                    else
                    {
                        rest.Add(e);
                    }
                }
                if (first != null)
                {
                    o.Lead = QuestionOf(d, first, w, false);
                    o.Lead.Kicker = o.Lead.Asker != null
                        ? w.F("reallife_co_needs_you", "Needs you now, {manager}, {place}", "manager", o.Lead.Asker, "place", PlaceNameOf(d, first.Place))
                        : w.F("reallife_co_needs_you_plain", "Needs you now, {place}", "place", PlaceNameOf(d, first.Place));
                }
                for (int i = 0; i < rest.Count && rows.Count < AlertsMost; i++)
                {
                    Escalation e = rest[i];
                    CompanyQuestion q = QuestionOf(d, e, w, false);
                    rows.Add(new CompanyWaiting
                    {
                        Name = q.Asker != null ? q.Asker + ", " + PlaceNameOf(d, e.Place) : q.Title, Line = q.Headline, Hue = q.AskerHue, Ring = UiRole.Warn,
                        Icon = q.Asker != null ? UiIcon.None : UiIcon.IdBadge, OpenLabel = w.T("reallife_co_open", "Open"), Open = CompanyAct.Of(CompanyDo.Go, "card", e.Id)
                    });
                }
            }
            IReadOnlyList<CompanyAlert> alerts = d.Alerts;
            for (int i = 0; i < alerts.Count && rows.Count < AlertsMost; i++)
            {
                CompanyAlert a = alerts[i];
                int tab = AlertTab(a.A, out UiIcon icon);
                rows.Add(new CompanyWaiting
                {
                    Name = a.Title, Line = a.What + (string.IsNullOrEmpty(a.Money) ? string.Empty : ", " + a.Money), Icon = icon, OpenLabel = w.T("reallife_co_open", "Open"),
                    Open = CompanyAct.Of(CompanyDo.Go, null, tab)
                });
            }
            o.Waiting = rows.ToArray();
            int total = rows.Count + (o.Lead != null ? 1 : 0);
            o.WaitingCount = alerts.Count > rows.Count ? (alerts.Count + (o.Lead != null ? 1 : 0)).ToString() : total.ToString();
        }

        // The planner's row kinds (CompanyDirector.Alert's first figure): the tab they belong to and the glyph that says what they are about.
        private static int AlertTab(int kind, out UiIcon icon)
        {
            switch (kind)
            {
                case 1: icon = UiIcon.TrendDown; return CompanyView.Overview;
                case 2:
                case 5:
                case 11: icon = UiIcon.ShieldCheck; return CompanyView.Cover;
                case 3:
                case 14: icon = UiIcon.Receipt; return CompanyView.Tax;
                case CompanyAlertRota: icon = UiIcon.Calendar; return CompanyView.Rota;
                default: icon = UiIcon.Info; return CompanyView.Overview;
            }
        }

        private const int CompanyAlertRota = 20;   // RotaAlertKind (CompanyDirector.Rota)

        // What the managers did today, newest first, each with what it cost or saved where the decision says.
        private static void ReadDone(CompanyOverviewFacts o, CompanyDirector d, Words w)
        {
            o.DoneNote = w.T("reallife_co_done_none", "Nothing yet. A manager's work for you shows here, with what it cost or saved.");
            ManagerState ms = d.ManagerData;
            if (ms == null)
            {
                return;
            }
            var list = new List<CompanyDone>(DoneMost);
            for (int i = ms.Decisions.Count - 1; i >= 0 && list.Count < DoneMost; i--)
            {
                Decision dec = ms.Decisions[i];
                if (dec.Day < d.Today - 1)
                {
                    break;
                }
                list.Add(DoneOf(dec, d, w));
            }
            o.Done = list.ToArray();
        }

        internal static CompanyDone DoneOf(Decision dec, CompanyDirector d, Words w)
        {
            int tone;
            string money = DecisionMoney(dec, w, out tone);
            return new CompanyDone
            {
                When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", dec.Day.ToString(), "hour", dec.Hour.ToString("00") + ":00"),
                Text = (string.IsNullOrEmpty(dec.Who) ? string.Empty : dec.Who + ": ") + CompanyApp.DecisionWords(dec), Money = money, Tone = tone,
                Open = CompanyAct.Of(CompanyDo.Go, "dec:" + dec.Id, CompanyView.Managers, CompanyView.SubDecision)
            };
        }

        // What a decision cost or saved, as its figures say (ManagerDecisions): an empty string where it moved no money.
        private static string DecisionMoney(Decision dec, Words w, out int tone)
        {
            tone = 0;
            switch ((ManagerTask)dec.Task)
            {
                case ManagerTask.Overtime:
                    tone = dec.A == 1 ? -1 : 0;
                    return dec.A == 1 ? "-" + w.Money(dec.B) : string.Empty;
                case ManagerTask.Claims:
                    tone = dec.D == 1 && dec.E > 0 ? 1 : 0;
                    return dec.D == 1 && dec.E > 0 ? "+" + w.Money(dec.E) : string.Empty;
                case ManagerTask.Marketing:
                    tone = -1;
                    return "-" + w.Money(dec.B);
                case ManagerTask.Raises:
                    if (dec.F == (int)RaiseAnswer.Bonus && dec.G > 0)
                    {
                        tone = -1;
                        return "-" + w.Money(dec.G);
                    }
                    tone = dec.A > 0 ? -1 : 0;
                    return dec.A > 0 ? "-" + w.PerHour(dec.A) : string.Empty;
                case ManagerTask.Leases:
                    int more = dec.C - dec.B;
                    tone = more > 0 ? -1 : more < 0 ? 1 : 0;
                    return more != 0 ? (more > 0 ? "-" : "+") + w.F("reallife_co_row_a_month", "{amount} a month", "amount", w.Money(Math.Abs(more))) : string.Empty;
                default:
                    return string.Empty;
            }
        }

        // Where the costs went: the game's own statements of yesterday for the player's businesses, in four lines.
        private static void ReadCosts(CompanyOverviewFacts o, CompanyDirector d, Words w)
        {
            int wages, stock, rent, marketing;
            if (!CompanyGame.StatementCosts(d.Today - 1, d.Shops, out wages, out stock, out rent, out marketing))
            {
                o.CostLabels = new string[0];
                o.CostValues = new string[0];
                return;
            }
            o.CostLabels = new[]
            {
                w.F("reallife_co_cost_wages_staff", "Wages, {count} staff", "count", d.State.Staff.Count.ToString()), w.T("reallife_co_cost_stock", "Stock and ingredients"),
                w.T("reallife_co_cost_rent", "Rent and bills"), w.T("reallife_co_cost_marketing", "Marketing")
            };
            o.CostValues = new[] { w.Money(wages), w.Money(stock), w.Money(rent), w.Money(marketing) };
            o.CostTotal = w.Money(wages + stock + rent + marketing);
        }

        // The managers' day: who they are, where, and how many they decided and asked of the owner this month.
        private static void ReadManagersToday(CompanyOverviewFacts o, CompanyDirector d, Words w)
        {
            ManagerState ms = d.ManagerData;
            if (ms == null)
            {
                return;
            }
            var list = new List<CompanyManagerToday>(8);
            for (int i = 0; i < ms.Managers.Count && list.Count < 12; i++)
            {
                ManagerFile m = ms.Managers[i];
                // A district's manager stands in the list as a place's does: the sidebar counts both, and the list holds as many as it counts.
                ManagedPlace mp = m.Region ? null : d.ManagedAt(m.Place);
                int asked = d.HierarchyOn ? OpenAsksOf(d.HierarchyData, m.Place) : 0;
                list.Add(new CompanyManagerToday
                {
                    Name = m.Name, Place = m.Region ? CompanyDirector.AreaName(m.Place) : mp != null ? mp.Place.Name : d.ManagedName(m.Place), Hue = UiColour.HueOf(m.Name),
                    Ring = CompanyDirector.Sick(m) ? UiRole.Warn : UiRole.Ok,
                    Figures = w.F("reallife_co_decided_asked", "{decided} decided, {asked} asked you", "decided", m.Decided.ToString(), "asked", asked.ToString()),
                    Open = CompanyAct.Of(CompanyDo.Go, (m.Region ? "reg:" : "mgr:") + m.Place, CompanyView.Managers)
                });
            }
            o.Managers = list.ToArray();
        }

        private static int OpenAsksOf(HierarchyState h, string place)
        {
            int n = 0;
            for (int i = 0; i < h.Escalations.Count; i++)
            {
                n += h.Escalations[i].Open && h.Escalations[i].Level >= EscalationRules.LevelOwner && h.Escalations[i].Place == place ? 1 : 0;
            }
            return n;
        }

        // Cleaning, the player's own bank and the company's line in Stocks: only where each is there.
        private static void ReadTools(CompanyOverviewFacts o, Words w)
        {
            var tools = new List<CompanyButton>(3);
            if (CompanyGame.CanClean)
            {
                tools.Add(new CompanyButton { Label = w.T("reallife_co_open_cleaning", "Open cleaning"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Package,
                    Act = CompanyAct.Of(CompanyDo.OpenCleaning) });
            }
            if (CompanyGame.CanOpenBank)
            {
                tools.Add(new CompanyButton { Label = w.T("reallife_co_open_bank", "Open your bank"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Bank, Act = CompanyAct.Of(CompanyDo.OpenBank) });
            }
            string line = CompanyGame.ListingLine();
            if (!string.IsNullOrEmpty(line))
            {
                // (the share's price is the Stocks app's: the button opens it, the line is not repeated on a page that is about the company's people and books)
                tools.Add(new CompanyButton { Label = w.T("reallife_co_open_stocks", "Open in Stocks"), Kind = UiButtonKind.Secondary, Icon = UiIcon.ChartLineUp,
                    Act = CompanyAct.Of(CompanyDo.OpenStocks) });
            }
            o.Tools = tools.ToArray();
        }

        private static string PlaceNameOf(CompanyDirector d, string place)
        {
            ManagedPlace mp = string.IsNullOrEmpty(place) ? null : d.ManagedAt(place);
            return mp != null ? mp.Place.Name : string.IsNullOrEmpty(place) ? string.Empty : d.ShopName(place);
        }

        // ------------------------------------------------------------------------------------------------- a business's page

        private static readonly UiIcon[] CoverIcons = { UiIcon.Fire, UiIcon.Lock, UiIcon.ShieldCheck };

        // A business as a card: as its own page (a head with the figures beside the name, then two columns: how it stands at the left, its licences, cover, campaign and what can be
        // done at the right) or, with <paramref name="page"/> false, as the panel of a company that has one business (its figures and the same rows, in one column).
        private CompanyCard BusinessCard(CompanyDirector d, CompanyShop shop, Words w, bool page = true)
        {
            CompanyState s = d.State;
            ShopFile file = shop.File;
            int day = d.Today;
            int waiting = ShopWaiting(d, shop);
            var c = new CompanyCard();
            var labels = new List<string>(4);
            var values = new List<string>(4);
            // The panel of a one business company stands under a head that has its staff and its result: it says neither again.
            if (page)
            {
                labels.Add(w.T("reallife_co_f_staff", "Staff"));
                values.Add(shop.Staff.ToString());
            }
            if (shop.HasBooks)
            {
                labels.Add(w.T("reallife_co_f_takings", "Takings"));
                values.Add(w.Money(shop.Takings));
                if (page)
                {
                    labels.Add(w.T("reallife_co_f_profit", "Profit"));
                    values.Add(Signed(shop.Profit, w));
                }
            }
            if (shop.Staff > 0)
            {
                labels.Add(w.T("reallife_co_f_standing", "Standing"));
                values.Add((shop.Standing / shop.Staff).ToString());
            }
            var blocks = new List<CompanyBlock>(24);
            if (page)
            {
                c.Kicker = w.T("reallife_co_business", "Business");
                c.Title = shop.Name;
                c.Sub = CompanyText.District(shop.District);
                c.Icon = UiIcon.Store;
                c.Chips = new[] { waiting > 0 ? CompanyText.T("reallife_co_state_waiting", "count", waiting.ToString()) : CompanyText.T("reallife_co_state_in_order") };
                c.ChipTones = new[] { waiting > 0 ? UiRole.Warn : UiRole.Ok };
                c.Back = new[] { new CompanyButton { Label = w.T("reallife_co_back", "Back"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Back, Act = CompanyAct.Of(CompanyDo.Go, "",
                    CompanyView.Overview) } };
                c.FactLabels = labels.ToArray();
                c.FactValues = values.ToArray();
            }
            else
            {
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = labels.ToArray(), Values = values.ToArray() });
            }
            blocks.Add(Sec(w.T("reallife_co_sec_state", "How it stands")));
            if (d.On(CompanyPart.Staff) && file.StrikeFrom > 0)
            {
                blocks.Add(Note(CompanyText.T(file.Striking(day) ? "reallife_co_shop_strike_on" : "reallife_co_shop_strike_called", "day", StaffRules.StrikeShown(file, day).ToString()),
                    UiRole.DangerText));
            }
            ManagedPlace mp = d.ManagedAt(file.Place);
            blocks.Add(Row(w.T("reallife_co_r_manager", "Manager"), mp != null && mp.Own != null ? mp.Own.Name : CompanyText.T("reallife_co_none")));
            blocks.Add(Row(w.T("reallife_co_r_district_income", "District income"), shop.Income.ToString()));
            LeaseFile lease = d.LeaseAt(file.Place);
            if (lease != null)
            {
                string rent = w.Money(d.Monthly(LeaseRules.RentAt(lease.Rent, lease.Running)));
                blocks.Add(Row(w.T("reallife_co_r_lease", "Lease"), lease.Hold ? w.F("reallife_co_lease_hold", "Month to month, {rent} a month", "rent", rent)
                    : w.F("reallife_co_lease_renews", "Day {day}, {rent} a month", "day", lease.End.ToString(), "rent", rent)));
            }
            string rival;
            int takes;
            if (CompanyGame.RivalNear(shop, out rival, out takes))
            {
                blocks.Add(Row(w.T("reallife_co_r_rival", "Nearest rival"), w.F("reallife_co_rival_line", "{name}, about {amount} a day (the game's estimate)", "name", rival, "amount",
                    w.Money(takes))));
            }
            if (!shop.HasBooks)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_shop_no_books"), UiRole.None));
            }
            if (file.Hide > 0 && Settings.EconomyCompanyHide)
            {
                blocks.Add(Row(w.T("reallife_co_r_hidden", "Cash off the books"), file.Hide + "%"));
            }
            if (d.On(CompanyPart.Licences))
            {
                var items = new List<CompanyItem>(8);
                for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
                {
                    if (!LicenceRules.Needed(shop.Needs, k))
                    {
                        continue;
                    }
                    Licence l = file.Permits[k];
                    bool valid = LicenceRules.Valid(l, day), soon = valid && l.Until - day < CompanyCatalogue.LicenceRemindDays, pending = !valid && l.Pending;
                    items.Add(new CompanyItem
                    {
                        Title = CompanyText.Capital(CompanyText.Licence(k)), Icon = LicenceIcons[Math.Min(k, LicenceIcons.Length - 1)], Tint = UiRole.None, Press = true,
                        Sub = valid ? CompanyText.Day(l.Until) : pending ? CompanyText.Day(l.Decide) : null,
                        Chip = CompanyText.T(pending ? "reallife_co_row_lic_pending" : !valid ? "reallife_co_row_lic_missing" : l.Auto ? "reallife_co_row_lic_auto" : soon ? "reallife_co_row_lic_soon"
                            : "reallife_co_row_lic_valid"),
                        ChipTone = pending ? UiRole.Warn : !valid ? UiRole.Danger : soon && !l.Auto ? UiRole.Warn : UiRole.Ok, Grade = -1,
                        Act = CompanyAct.Of(CompanyDo.Go, "lic:" + file.Place + ":" + k, CompanyView.Tax, CompanyView.SubLicence)
                    });
                }
                if (items.Count > 0)
                {
                    blocks.Add(Sec(w.T("reallife_co_r_licences", "Licences")));
                    blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = items.ToArray() });
                }
            }
            if (d.On(CompanyPart.Cover))
            {
                var items = new List<CompanyItem>(3);
                for (int k = 0; k < file.Cover.Length; k++)
                {
                    bool held = file.Cover[k].Active;
                    items.Add(new CompanyItem
                    {
                        Title = CompanyText.Capital(CompanyText.Cover(k)), Icon = CoverIcons[Math.Min(k, CoverIcons.Length - 1)], Tint = UiRole.None, Press = true, Grade = -1,
                        Chip = CompanyText.T(held ? "reallife_ibox_chip_covered" : "reallife_ibox_chip_not_covered"), ChipTone = held ? UiRole.Ok : UiRole.Warn,
                        Act = CompanyAct.Of(CompanyDo.Go, "pol:" + file.Place + ":" + k, CompanyView.Cover)
                    });
                }
                if (items.Count > 0)
                {
                    blocks.Add(Sec(w.T("reallife_co_r_cover", "Cover held")));
                    blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = items.ToArray() });
                }
            }
            if (page)
            {
                // The licences with how it stands and the cover at the left, the campaign, what can be done and the people at the right.
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            }
            if (d.On(CompanyPart.Marketing))
            {
                blocks.Add(Link(w.T("reallife_co_r_campaign", "Campaign"), file.Push >= 0
                    ? CompanyText.T(file.Pushing(day) ? "reallife_co_shop_campaign_on" : "reallife_co_shop_campaign_booked", "campaign", CompanyText.Campaign(file.Push), "day",
                        (file.Pushing(day) ? file.PushEnd - 1 : file.PushFrom).ToString()) : CompanyText.T("reallife_co_none"), CompanyAct.Of(CompanyDo.Go, "biz:"
                            + file.Place, CompanyView.Marketing)));
            }
            blocks.Add(Sec(w.T("reallife_co_sec_do", "What you can do here")));
            var buttons = new List<CompanyButton>(3)
            {
                new CompanyButton { Label = w.T("reallife_co_open_bizman", "Open in BizMan"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Locate, Act = CompanyAct.Of(CompanyDo.BizMan, file.Place) }
            };
            if (CompanyGame.SaleOffered(file.Place, out string saleWhy))
            {
                // A sale the game would refuse is shown off with its reason, so it is not found only inside the Sell page.
                buttons.Add(new CompanyButton { Label = w.T("reallife_co_sell", "Sell this business"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Handshake,
                    Act = CompanyAct.Of(CompanyDo.SellBusiness, file.Place), Disabled = saleWhy != null, Why = saleWhy });
            }
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Buttons, Buttons = buttons.ToArray() });
            if (d.On(CompanyPart.Staff))
            {
                // The people of the business, under what can be done: the rows that fit the room, and a count of the rest. The panel of a one business company keeps four, at the left under the
                // licences, where that column would end short.
                CompanyItem[] crew = CrewItems(d, file.Place, w, false, page ? 12 : 4);
                if (crew.Length > 0)
                {
                    blocks.Add(Sec(w.T("reallife_co_staff_people", "Your people"), CountOf(w, crew.Length, shop.Staff)));
                    blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = crew });
                }
            }
            if (!page)
            {
                CompanyFace.AsideSections(blocks, w.T("reallife_co_r_licences", "Licences"), w.T("reallife_co_staff_people", "Your people"));   // the panel of a one business company
            }
            c.Blocks = blocks.ToArray();
            return c;
        }

        // ------------------------------------------------------------------------------------------------- small builders

        internal static CompanyBlock Sec(string title, string right = null) => new CompanyBlock { Kind = CompanyBlockKind.Section, Text = title, Value = right };

        internal static CompanyBlock Row(string label, string value, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Row, Text = label, Value = value, Tone = tone };

        internal static CompanyBlock Link(string label, string value, CompanyAct act, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Link, Text = label, Value = value,
            Act = act, Tone = tone };

        internal static CompanyBlock Note(string text, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Note, Text = text, Tone = tone };

        internal static CompanyBlock Buttons(params CompanyButton[] buttons) => new CompanyBlock { Kind = CompanyBlockKind.Buttons, Buttons = buttons };

        internal static CompanyButton Btn(string label, UiButtonKind kind, CompanyAct act, UiAction key = UiAction.Count, UiIcon icon = UiIcon.None, bool arms = false, bool disabled = false,
            string why = null) =>
            new CompanyButton { Label = label, Kind = kind, Act = act, Key = key, Icon = icon, Arms = arms, Disabled = disabled, Why = why };
    }
}
