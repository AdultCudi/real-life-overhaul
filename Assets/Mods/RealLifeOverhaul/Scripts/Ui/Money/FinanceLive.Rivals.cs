using System;
using System.Collections.Generic;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class FinanceLive
    {
        private readonly List<RichRow> _rich = new List<RichRow>(RivalCatalogue.RichListSize);
        private readonly float[] _seriesB = new float[DaySeriesCatalogue.Days];
        private readonly int[] _daysB = new int[DaySeriesCatalogue.Days];
        private readonly float[] _worthLine = new float[NetWorthRing.Most + 1];
        private WorthSummary[] _sums = new WorthSummary[0];
        private bool[] _kept = new bool[0];

        private static int RivalsCount()
        {
            RivalDirector d = RivalDirector.Instance;
            RivalFile file = d != null ? d.File : null;
            int n = 0;
            for (int i = 0; file != null && i < file.Tycoons.Length; i++)
            {
                n += file.Tycoons[i].Active ? 1 : 0;
            }
            return n;
        }

        private static string TycoonKey(int who) => "t:" + RivalCatalogue.Tycoons[who].Name;

        // ------------------------------------------------------------------------------------------------- Rivals

        // The rich list, the one chosen as the profile, what they did lately, and the answers that do something, only while there is a move to answer.
        private void ReadRivals(FinanceFacts f, FinanceView v)
        {
            FinanceRivalsFacts r = f.Rivals;
            RivalDirector director = RivalDirector.Instance;
            RivalFile file = director != null ? director.File : null;
            UtilitiesWords w = W;
            if (file == null)
            {
                f.RivalsOn = false;
                return;
            }
            long worth = RivalGame.Worth();
            int level = Prices.Level, day = RivalGame.Day;
            string you = RivalGame.PlayerName();
            FaceSources.Player(you);   // the player's own picture on their row
            RivalRules.RichList(file, worth, level, you, _rich);
            DaySeriesBook book = DaySeriesDirector.Book;
            var rows = new List<FinanceRow>(_rich.Count);
            r.Who = new int[_rich.Count];
            WorthBook worthBook = WorthWatch.Book;   // null for a guest: the page is then as it was
            if (_sums.Length < _rich.Count)
            {
                _sums = new WorthSummary[_rich.Count];
                _kept = new bool[_rich.Count];
            }
            int first = -1;
            for (int i = 0; i < _rich.Count; i++)
            {
                RichRow row = _rich[i];
                r.Who[i] = row.Who;
                first = first < 0 && row.Who >= 0 && row.Who < RivalCatalogue.TycoonCount && file.Tycoons[row.Who].Active ? i : first;
                string name = row.Name;
                bool tycoon = row.Who >= 0 && row.Who < file.Tycoons.Length;
                _kept[i] = worthBook != null && worthBook.Standing(row.Who, tycoon && file.Tycoons[row.Who].Active, day, row.Worth, _worthLine, out _sums[i]);
                int dir = row.WeekAgo > 0 ? (row.Worth > row.WeekAgo ? 1 : row.Worth < row.WeekAgo ? -1 : 0) : 0;
                string sub = row.Who == RichRow.Player ? RivalText.T("reallife_rival_note_you") : row.Who >= RichRow.FortuneBase
                    ? RivalText.T("reallife_rival_note_old_money") : RivalCatalogue.Tycoons[row.Who].Holding;
                rows.Add(new FinanceRow
                {
                    Name = string.IsNullOrEmpty(name) ? you : name, Sub = sub, Hue = UiColour.HueOf(name ?? string.Empty),
                    Cells = new[] { Money(row.Worth), string.Empty, RivalText.Change(row.Worth, row.WeekAgo) }, Values = new[] { 0, 0, dir },
                    Line = _kept[i] ? (_sums[i].Points >= 2 ? Copy(_worthLine, _sums[i].Points) : null) : SparkOf(book, SeriesKey(row.Who), day)
                });
            }
            v.Rival = v.Rival >= 0 && v.Rival < rows.Count ? v.Rival : Math.Max(0, first);
            r.Table = FinanceKitTables.Make(FinanceKitTables.RichColumns(w), rows, v.Rival, w.T("reallife_fin_no_rivals", "Nobody yet"), RivalText.T("reallife_rival_empty"), UiIcon.Trophy);
            int rank = RivalRules.Rank(file, worth, level);
            r.Headline = RivalText.Fill("reallife_rival_rank_of", "rank", rank.ToString(Inv), "count", RivalCatalogue.RichListSize.ToString(Inv)) + ", " + Money(worth);
            int who = r.Who.Length > 0 ? r.Who[v.Rival] : RichRow.Player;
            r.Profile = null;
            r.Labels = new string[0];
            r.Values = new string[0];
            r.Moves = new FinanceRivalMove[0];
            r.Answers = new FinanceAnswer[0];
            r.CanFollow = r.CanHolding = false;
            r.MovesTitle = w.T("reallife_fin_moves_title", "Their latest moves");
            r.MovesNone = w.T("reallife_fin_moves_none", "No moves yet.");
            r.AnswersTitle = w.T("reallife_fin_answers_title", "How do you answer?");
            r.ListLabel = RivalBridges.ShowList != null ? RivalText.T("reallife_rival_open_list") : null;
            RichRow chosen = _rich[v.Rival];
            if (who >= 0 && who < RivalCatalogue.TycoonCount)
            {
                Tycoon(r, director, file, who, chosen, w, day);
            }
            else
            {
                r.Profile = new UiProfileModel
                {
                    Name = chosen.Who == RichRow.Player ? you : chosen.Name, Hue = UiColour.HueOf(chosen.Name ?? string.Empty), Rating = -1,
                    Role = chosen.Who == RichRow.Player ? RivalText.T("reallife_rival_note_you") : RivalText.T("reallife_rival_note_old_money"),
                    RoleIcon = chosen.Who == RichRow.Player ? UiIcon.Person : UiIcon.Coin, Status = null,
                    FactLabels = new[] { RivalText.T("reallife_rival_label_their_worth"), RivalText.T("reallife_rival_label_week") },
                        FactValues = new[] { Money(chosen.Worth), RivalText.Change(chosen.Worth, chosen.WeekAgo) }
                };
            }
            if (_kept[v.Rival] && r.Profile != null)
            {
                WorthFact(r.Profile, _sums[v.Rival], w, day);
            }
            r.Chart = RivalChart(book, who, you, chosen.Name, w, day);
        }

        // The days on record as the net worth card sums them, for the entry chosen; with fewer than two days it says so and invents nothing.
        private static void WorthFact(UiProfileModel p, WorthSummary s, UtilitiesWords w, int day)
        {
            string value;
            if (s.Has)
            {
                long c = s.Change;
                value = w.F("reallife_fin_worth_over", "{change} over {days} days", "change", (c > 0 ? "+" : c < 0 ? "-" : string.Empty) + RivalText.Money(c < 0 ? -c : c),
                    "days", Math.Min(Math.Max(1, day - s.FromDay + 1), WorthBook.Window).ToString(Inv));
            }
            else
            {
                value = w.T("reallife_fin_worth_later", "Starts later");
            }
            int n = p.FactLabels != null ? p.FactLabels.Length : 0;
            var labels = new string[n + 1];
            var values = new string[n + 1];
            for (int i = 0; i < n; i++)
            {
                labels[i] = p.FactLabels[i];
                values[i] = p.FactValues[i];
            }
            labels[n] = w.T("reallife_fin_worth_label", "Change on record");
            values[n] = value;
            p.FactLabels = labels;
            p.FactValues = values;
        }

        private static string SeriesKey(int who)
        {
            return who == RichRow.Player ? DaySeriesCatalogue.WorthYou : who >= RichRow.FortuneBase ? DaySeriesCatalogue.Fortune(who - RichRow.FortuneBase) : DaySeriesCatalogue.Tycoon(who);
        }

        // A short spark of the days that exist: a row with fewer than three has none.
        private float[] SparkOf(DaySeriesBook book, string key, int day)
        {
            int n = book != null ? FinanceRules.ExistingDays(book, key, day, 30, true, _values, _days) : 0;
            return n >= 3 ? Copy(_values, n) : null;
        }

        private void Tycoon(FinanceRivalsFacts r, RivalDirector director, RivalFile file, int who, RichRow row, UtilitiesWords w, int day)
        {
            Tycoon t = file.Tycoons[who];
            TycoonDef def = t.Def;
            RivalMove live = null;
            var moves = new List<FinanceRivalMove>(5);
            for (int i = file.Moves.Count - 1; i >= 0 && moves.Count < 5; i--)
            {
                RivalMove m = file.Moves[i];
                if (m.Tycoon != who)
                {
                    continue;
                }
                live = live == null && m.Live ? m : live;
                int when = m.State == RivalMoveState.Running ? m.End : m.Land;
                moves.Add(new FinanceRivalMove { Day = FinanceText.Day(when), What = RivalText.What(m), Live = m.Live });
            }
            r.Moves = moves.ToArray();
            r.Profile = new UiProfileModel
            {
                Name = def.Name, Hue = UiColour.HueOf(def.Name), Role = def.Holding, RoleIcon = UiIcon.Store, Rating = -1,
                Status = !t.Active ? w.T("reallife_fin_state_out", "Out of the game") : live != null ? w.T("reallife_fin_state_moving",
                    "Making a move") : w.T("reallife_fin_state_quiet", "Quiet"),
                StatusTone = !t.Active ? UiRole.Ok : live != null ? UiRole.Warn : UiRole.Muted,
                FactLabels = new[] { RivalText.T("reallife_rival_label_their_worth"), RivalText.T("reallife_rival_label_week") },
                FactValues = new[] { Money(t.Worth > 0 ? t.Worth : row.Worth), RivalText.Change(t.Worth, t.WeekAgo) }
            };
            var labels = new List<string>(2);
            var values = new List<string>(2);
            if (director.HoldingShares(who, out long held, out long most, out long _, out int mid))
            {
                labels.Add(w.F("reallife_fin_your_shares", "Your shares in {holding}", "holding", def.Holding));
                values.Add(RivalText.Fill("reallife_rival_holding_value", "held", RivalText.N(held), "most", RivalText.N(most), "price", RivalText.MoneyCents(mid)));
                r.CanHolding = RivalBridges.OpenStock != null;
                r.HoldingLabel = RivalText.T("reallife_rival_open_holding");
            }
            if (day < t.QuietUntil || day < t.SlowUntil || day < t.BarredUntil)
            {
                labels.Add(w.T("reallife_fin_held_back", "Held back until day"));
                values.Add(Math.Max(day, Math.Max(t.QuietUntil, Math.Max(t.SlowUntil, t.BarredUntil))).ToString(Inv));
            }
            r.Labels = labels.ToArray();
            r.Values = values.ToArray();
            r.Reading = Reading(live, def, w);
            FlareDirector flare = FlareDirector.Instance;
            r.CanFollow = t.Active && flare != null && flare.File != null && !FlareCast.Followed(flare.File, TycoonKey(who));
            r.FollowLabel = w.F("reallife_fin_follow_name", "Follow {name}", "name", def.Name);
            if (t.Active)
            {
                r.Answers = Answers(director, who, live, w, day);
            }
        }

        // The profile's words about the move that is live: what it is, what it does to you and when. Built from the move itself.
        private static string Reading(RivalMove live, TycoonDef def, UtilitiesWords w)
        {
            if (live == null)
            {
                return null;
            }
            string size = RivalText.Size(live);
            string when = live.State == RivalMoveState.Running
                ? w.F("reallife_fin_read_runs", "It runs until day {end}.", "end", live.End.ToString(Inv))
                : w.F("reallife_fin_read_comes", "It comes on day {land}.", "land", live.Land.ToString(Inv));
            string business = string.IsNullOrEmpty(live.Name) ? RivalText.T("reallife_rival_your_business") : live.Name;
            string effect;
            switch (live.Kind)
            {
                case RivalMoveKind.Outlet:
                    effect = w.F("reallife_fin_read_outlet", "About {size} fewer customers at {business}.", "size", size, "business", business);
                    break;
                case RivalMoveKind.PriceRise:
                    effect = w.F("reallife_fin_read_prices", "About {size} fewer customers at your shops while their prices are up.", "size", size);
                    break;
                case RivalMoveKind.Property:
                    effect = w.F("reallife_fin_read_property", "Buildings for sale in {district} cost {size} more.", "district", RivalGame.DistrictName(live.District), "size", size);
                    break;
                case RivalMoveKind.Poach:
                    effect = w.F("reallife_fin_read_poach", "They offer {size} over the going rate to your best person.", "size", size);
                    break;
                case RivalMoveKind.Lobby:
                    effect = w.F("reallife_fin_read_lobby", "Inspectors call more often at your businesses, {size} more.", "size", size);
                    break;
                case RivalMoveKind.Suit:
                    effect = w.F("reallife_fin_read_suit", "They claim {size} from you in court.", "size", size);
                    break;
                case RivalMoveKind.Press:
                    effect = w.F("reallife_fin_read_press", "Customers think a little less of {business}.", "business", business);
                    break;
                case RivalMoveKind.Stake:
                    effect = w.T("reallife_fin_read_stake", "They press your board with the shares they hold.");
                    break;
                case RivalMoveKind.Bid:
                    effect = w.T("reallife_fin_read_bid", "Their bid wins only if you hold too few of your own shares.");
                    break;
                case RivalMoveKind.Borrowed:
                    effect = w.F("reallife_fin_read_borrowed", "They borrowed {size} at a city bank.", "size", size);
                    break;
                default:
                    effect = w.F("reallife_fin_read_shares", "They bought {size} of shares.", "size", size);
                    break;
            }
            return w.F("reallife_fin_read", "{name} is {what}. {effect} {when}", "name", def.Name, "what", RivalText.What(live), "effect", effect, "when", when);
        }

        // The answers that do something now, and only with a move of theirs to answer (a takeover aside, which a move does not need): the
        // Flare campaign and the price cut first, each with its forecast, then the counters.
        private FinanceAnswer[] Answers(RivalDirector director, int who, RivalMove live, UtilitiesWords w, int day)
        {
            var list = new List<FinanceAnswer>(8);
            DaySeriesBook book = DaySeriesDirector.Book;
            bool hitsCustomers = live != null && (live.Kind == RivalMoveKind.PriceRise || live.Kind == RivalMoveKind.Outlet || live.Kind == RivalMoveKind.Press);
            if (hitsCustomers)
            {
                FinanceAnswer campaign = CampaignAnswer(live, w);
                if (campaign != null)
                {
                    list.Add(campaign);
                }
            }
            if (live != null && (live.Kind == RivalMoveKind.PriceRise || live.Kind == RivalMoveKind.Outlet) && book != null && CompanyDirector.Instance != null)
            {
                bool cut = DaySeriesRules.CutOn(book, day);
                bool can = cut || CompanyDirector.Instance.CanCut();
                string forecast = w.F("reallife_fin_cut_forecast", "Forecast: about {customers} more customers a day while it lasts.", "customers", DaySeriesRules.CutCustomers.ToString(Inv));
                list.Add(new FinanceAnswer
                {
                    Title = cut ? w.T("reallife_fin_hold_prices", "End the price cut and hold your prices") : w.F("reallife_fin_cut_prices",
                        "Cut your prices {percent}% for {days} days", "percent", DaySeriesRules.CutPercent.ToString(Inv), "days", DaySeriesRules.CutDays.ToString(Inv)),
                    Hint = !can ? w.T("reallife_fin_cut_none", "Not now: none of your pricing plans has a price the plan sets.")
                        : cut ? w.F("reallife_fin_cut_on", "The cut runs until day {day}. Ending it takes off only the holds it put on.", "day", book.CutUntil.ToString(Inv))
                        : w.T("reallife_fin_cut_hint", "Your own shops' prices go down and the plans it moved are held. They go back by themselves at the end.") + " " + forecast,
                    Act = cut ? FinanceAct.HoldPrices : FinanceAct.CutPrices, On = can
                });
            }
            for (int k = 0; k < (int)RivalCounter.Count; k++)
            {
                var kind = (RivalCounter)k;
                if (live == null && kind != RivalCounter.Takeover)
                {
                    continue;
                }
                RivalWhy why = director.CanCounter(who, kind, out long cost);
                if (why != RivalWhy.None)
                {
                    continue;
                }
                list.Add(new FinanceAnswer
                {
                    Title = RivalText.CounterName(kind), Hint = RivalText.CounterAbout(kind) + " " + w.F("reallife_fin_costs", "Costs {money}.",
                        "money", Money(cost)), Act = FinanceAct.Counter, Arg = k, On = true
                });
            }
            return list.ToArray();
        }

        // The Flare campaign as an answer: a plan of influencers for a week at the business the move hits, with what it costs and the forecast
        // from the company's own quote (what it adds, per cent) and what the business takes a day. Null where Flare is off.
        private FinanceAnswer CampaignAnswer(RivalMove live, UtilitiesWords w)
        {
            FlareDirector flare = FlareDirector.Instance;
            if (flare == null || !flare.Running)
            {
                return null;
            }
            BuildingRegistration reg = CampaignShop(live);
            string title = w.F("reallife_fin_campaign", "Run a Flare campaign at {business}", "business", reg != null ? RivalGame.Name(reg) : RivalText.T("reallife_rival_your_business"));
            string place = reg != null ? BusinessGame.Place(reg) : null;
            int cost = 0, percent = 0;
            string why = null;
            if (place == null || !flare.HireQuote(place, FlareDeal.Plan, -1, out cost, out percent, out why))
            {
                return new FinanceAnswer
                {
                    Title = title, On = false, Act = FinanceAct.Campaign,
                    Hint = w.F("reallife_fin_not_now", "Not now: {reason}", "reason", FlareText.T(why ?? "reallife_flare_hire_no_shop"))
                };
            }
            int days = CompanyCatalogue.CampaignDays[FlareCast.FirstDealKind + (int)FlareDeal.Plan];
            long gain = DaySeriesRules.CampaignForecast((long)Math.Round(BusinessGame.DailyRevenue(reg)), percent, days);
            return new FinanceAnswer
            {
                Title = title, On = true, Act = FinanceAct.Campaign,
                Hint = w.F("reallife_fin_campaign_hint", "Influencers post for {days} days. Costs {cost}. Forecast: {percent}% more customers, about {gain} more takings.",
                    "days", days.ToString(Inv), "cost", Money(cost), "percent", percent.ToString(Inv), "gain", Money(gain))
            };
        }

        // The business a move hits, else the player's best: where a campaign is run.
        private BuildingRegistration CampaignShop(RivalMove live)
        {
            BuildingRegistration reg = live != null ? RivalGame.Business(live.Place) : null;
            if (reg != null)
            {
                return reg;
            }
            Books.Businesses(_shops);
            BuildingRegistration best = null;
            float most = -1f;
            for (int i = 0; i < _shops.Count; i++)
            {
                float took = BusinessGame.DailyRevenue(_shops[i]);
                if (took > most)
                {
                    most = took;
                    best = _shops[i];
                }
            }
            return best;
        }

        // You and the one chosen over the days both have: money in full figures, never a raw number.
        private UiChartModel RivalChart(DaySeriesBook book, int who, string you, string name, UtilitiesWords w, int day)
        {
            string title = w.T("reallife_fin_catching", "Who is catching whom");
            string empty = w.T("reallife_fin_chart_empty", "Not enough days yet"), body = w.T("reallife_fin_chart_empty_body", "The chart fills in as days pass.");
            if (book == null || who == RichRow.Player)
            {
                return FinanceRules.Chart(title, null, null, _days, 0, Money, empty, body);
            }
            int a = FinanceRules.ExistingDays(book, DaySeriesCatalogue.WorthYou, day, 30, false, _values, _days);
            int b = FinanceRules.ExistingDays(book, SeriesKey(who), day, 30, true, _seriesB, _daysB);
            int n = Math.Min(a, b);
            if (n < 2)
            {
                return FinanceRules.Chart(title, null, null, _days, 0, Money, empty, body);
            }
            var mine = new float[n];
            var theirs = new float[n];
            var days = new int[n];
            for (int i = 0; i < n; i++)
            {
                mine[i] = FinanceRules.Dollars(_values[a - n + i]);
                theirs[i] = FinanceRules.Dollars(_seriesB[b - n + i]);
                days[i] = _daysB[b - n + i];
            }
            return FinanceRules.Chart(title, new[] { you, name }, new[] { mine, theirs }, days, n, Money, empty, body);
        }

        // ------------------------------------------------------------------------------------------------- doing

        private bool DoRival(FinanceAct act, FinanceView v, int arg)
        {
            RivalDirector director = RivalDirector.Instance;
            RivalFile file = director != null ? director.File : null;
            if (file == null)
            {
                return false;
            }
            RivalRules.RichList(file, RivalGame.Worth(), Prices.Level, RivalGame.PlayerName(), _rich);
            int who = v.Rival >= 0 && v.Rival < _rich.Count ? _rich[v.Rival].Who : RichRow.Player;
            switch (act)
            {
                case FinanceAct.ShowList: return director.ShowList();
                case FinanceAct.HoldPrices: return ReleaseCut();
                case FinanceAct.CutPrices: return CutPrices();
                case FinanceAct.Campaign: return AskCampaign(v);
            }
            if (who < 0 || who >= RivalCatalogue.TycoonCount)
            {
                return false;
            }
            switch (act)
            {
                case FinanceAct.Follow:
                    {
                        FlareDirector flare = FlareDirector.Instance;
                        if (flare == null || flare.File == null)
                        {
                            return false;
                        }
                        flare.Follow(TycoonKey(who), true);
                        return true;
                    }
                case FinanceAct.OpenHolding:
                    if (RivalBridges.OpenStock == null)
                    {
                        return false;
                    }
                    RivalBridges.OpenStock(RivalCatalogue.Tycoons[who].StockId);
                    return true;
                case FinanceAct.Counter:
                    return AskCounter(director, who, (RivalCounter)Math.Max(0, Math.Min((int)RivalCounter.Count - 1, arg)));
                default:
                    return false;
            }
        }

        // The game's own question before a counter is used: what it does and what it costs; on yes, done, and any refusal said as the old page said it.
        private bool AskCounter(RivalDirector director, int who, RivalCounter kind)
        {
            TycoonDef d = RivalCatalogue.Tycoons[who];
            RivalWhy why = director.CanCounter(who, kind, out long cost);
            if (why != RivalWhy.None)
            {
                Tell(UI.Notification.NotificationType.Warning, RivalText.WhyKey(why));
                return false;
            }
            LanguageChangeEventDataHolder title = "reallife_rival_confirm_title".Localize(new { answer = RivalText.CounterName(kind), rival = d.Name });
            LanguageChangeEventDataHolder body;
            if (kind == RivalCounter.Takeover)
            {
                director.TakeoverQuote(who, out long _, out long back);
                body = "reallife_rival_confirm_takeover".Localize(new { holding = d.Holding, cost = RivalText.Money(cost),
                    back = RivalText.Money(back), days = RivalCatalogue.BreakUpDays.ToString() });
            }
            else
            {
                body = "reallife_rival_confirm_body".Localize(new { about = RivalText.CounterAbout(kind), cost = RivalText.Money(cost), rival = d.Name });
            }
            GameConfirm.Show(title, body, () => Guarded(() =>
            {
                string refused = director.Counter(who, kind);
                if (refused != null)
                {
                    Tell(UI.Notification.NotificationType.Warning, refused);
                }
            }), null, "reallife_rival_confirm_yes", "common_cancel", false, kind == RivalCounter.Takeover);
            return true;
        }

        // A price cut on the player's own shops: asked first, held in the day series' book so it ends by itself. It is on only if it moved a price.
        private bool CutPrices()
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            int day = RivalGame.Day;
            if (book == null || day <= 0 || CompanyDirector.Instance == null || DaySeriesRules.CutOn(book, day))
            {
                return false;
            }
            UtilitiesWords w = W;
            if (!CompanyDirector.Instance.CanCut())
            {
                Tell(UI.Notification.NotificationType.Warning, "reallife_fin_note_cut_none");
                return false;
            }
            GameConfirm.Show(w.T("reallife_fin_cut_title", "Cut your prices?"),
                w.F("reallife_fin_cut_body", "Your own shops' prices go down {percent}% for {days} days and the pricing plans it moves are held while it lasts. "
                    + "They go back by themselves at the end.", "percent", DaySeriesRules.CutPercent.ToString(Inv),
                    "days", DaySeriesRules.CutDays.ToString(Inv)),
                () => Guarded(() =>
                {
                    int moved = DaySeriesGame.StartCut(book, day);
                    if (moved > 0)
                    {
                        Tell(UI.Notification.NotificationType.Success, "reallife_fin_note_cut_done", new Dictionary<string, string> { { "count", moved.ToString(Inv) } });
                    }
                    else
                    {
                        Tell(UI.Notification.NotificationType.Warning, "reallife_fin_note_cut_none");
                    }
                    Moved();
                }), null, "reallife_rival_confirm_yes", "common_cancel", false, false);
            return true;
        }

        private bool ReleaseCut()
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            if (book == null || book.CutFrom <= 0)
            {
                return false;
            }
            DaySeriesGame.EndCut(book);
            return true;
        }

        // The Flare campaign answer: the game's own question with the cost and the forecast, then the hire as Flare's own Hire does it.
        private bool AskCampaign(FinanceView v)
        {
            FlareDirector flare = FlareDirector.Instance;
            RivalDirector director = RivalDirector.Instance;
            RivalFile file = director != null ? director.File : null;
            if (flare == null || !flare.Running || file == null)
            {
                return false;
            }
            RivalMove live = null;
            RivalRules.RichList(file, RivalGame.Worth(), Prices.Level, RivalGame.PlayerName(), _rich);
            int who = v.Rival >= 0 && v.Rival < _rich.Count ? _rich[v.Rival].Who : RichRow.Player;
            for (int i = file.Moves.Count - 1; i >= 0 && live == null; i--)
            {
                live = file.Moves[i].Tycoon == who && file.Moves[i].Live ? file.Moves[i] : null;
            }
            BuildingRegistration reg = CampaignShop(live);
            string place = reg != null ? BusinessGame.Place(reg) : null;
            UtilitiesWords w = W;
            int cost = 0, percent = 0;
            string why = null;
            if (place == null || !flare.HireQuote(place, FlareDeal.Plan, -1, out cost, out percent, out why))
            {
                Tell(UI.Notification.NotificationType.Warning, why ?? "reallife_flare_hire_no_shop");
                return false;
            }
            int days = CompanyCatalogue.CampaignDays[FlareCast.FirstDealKind + (int)FlareDeal.Plan];
            long gain = DaySeriesRules.CampaignForecast((long)Math.Round(BusinessGame.DailyRevenue(reg)), percent, days);
            string name = RivalGame.Name(reg);
            GameConfirm.Show(w.F("reallife_fin_campaign_title", "Run a campaign at {business}?", "business", name),
                w.F("reallife_fin_campaign_body", "Influencers post about {business} for {days} days. It costs {cost}. Forecast: {percent}% more customers, about {gain} more takings.",
                    "business", name, "days", days.ToString(Inv), "cost", Money(cost), "percent", percent.ToString(Inv), "gain", Money(gain)),
                () => Guarded(() =>
                {
                    if (!flare.Hire(place, FlareDeal.Plan, -1))
                    {
                        Tell(UI.Notification.NotificationType.Warning, "reallife_flare_hire_cannot");
                    }
                    Moved();
                }), null, "reallife_rival_confirm_yes", "common_cancel", false, false);
            return true;
        }
    }
}
