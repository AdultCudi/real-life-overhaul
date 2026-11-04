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
            int first = -1;
            for (int i = 0; i < _rich.Count; i++)
            {
                RichRow row = _rich[i];
                r.Who[i] = row.Who;
                first = first < 0 && row.Who >= 0 && row.Who < RivalCatalogue.TycoonCount && file.Tycoons[row.Who].Active ? i : first;
                string name = row.Name;
                int dir = row.WeekAgo > 0 ? (row.Worth > row.WeekAgo ? 1 : row.Worth < row.WeekAgo ? -1 : 0) : 0;
                string sub = row.Who == RichRow.Player ? RivalText.T("reallife_rival_note_you") : row.Who >= RichRow.FortuneBase
                    ? RivalText.T("reallife_rival_note_old_money") : RivalCatalogue.Tycoons[row.Who].Holding;
                rows.Add(new FinanceRow
                {
                    Name = string.IsNullOrEmpty(name) ? you : name, Sub = sub, Hue = UiColour.HueOf(name ?? string.Empty),
                    Cells = new[] { Money(row.Worth), string.Empty, RivalText.Change(row.Worth, row.WeekAgo) }, Values = new[] { 0, 0, dir },
                    Line = SparkOf(book, SeriesKey(row.Who), day)
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
            r.Chart = RivalChart(book, who, you, chosen.Name, w, day);
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
            FlareDirector flare = FlareDirector.Instance;
            r.CanFollow = t.Active && flare != null && flare.File != null && !FlareCast.Followed(flare.File, TycoonKey(who));
            r.FollowLabel = w.F("reallife_fin_follow_name", "Follow {name}", "name", def.Name);
            if (t.Active)
            {
                r.Answers = Answers(director, who, live, w, day);
            }
        }

        // The answers that do something now, and only with a move of theirs to answer (a takeover aside, which a move does not need).
        private FinanceAnswer[] Answers(RivalDirector director, int who, RivalMove live, UtilitiesWords w, int day)
        {
            var list = new List<FinanceAnswer>(6);
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
            DaySeriesBook book = DaySeriesDirector.Book;
            if (live != null && (live.Kind == RivalMoveKind.PriceRise || live.Kind == RivalMoveKind.Outlet) && book != null && CompanyDirector.Instance != null)
            {
                bool cut = DaySeriesRules.CutOn(book, day);
                list.Add(new FinanceAnswer
                {
                    Title = cut ? w.T("reallife_fin_hold_prices", "End the price cut and hold your prices") : w.F("reallife_fin_cut_prices",
                        "Cut your prices {percent}% for {days} days", "percent", DaySeriesRules.CutPercent.ToString(Inv), "days", DaySeriesRules.CutDays.ToString(Inv)),
                    Hint = cut ? w.F("reallife_fin_cut_on", "The cut runs until day {day}. Ending it puts the pricing plans back to what they were.", "day", book.CutUntil.ToString(Inv))
                        : w.T("reallife_fin_cut_hint", "Your own shops' prices go down and your pricing plans are held. They go back by themselves at the end."),
                    Act = cut ? FinanceAct.HoldPrices : FinanceAct.CutPrices, On = true
                });
            }
            return list.ToArray();
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

        // A price cut on the player's own shops: asked first, held in the day series' book so it ends by itself.
        private bool CutPrices()
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            int day = RivalGame.Day;
            if (book == null || day <= 0 || CompanyDirector.Instance == null || DaySeriesRules.CutOn(book, day))
            {
                return false;
            }
            UtilitiesWords w = W;
            GameConfirm.Show(w.T("reallife_fin_cut_title", "Cut your prices?"),
                w.F("reallife_fin_cut_body", "Your own shops' prices go down {percent}% for {days} days and your pricing plans are held while it lasts. "
                    + "They go back by themselves at the end.", "percent", DaySeriesRules.CutPercent.ToString(Inv),
                    "days", DaySeriesRules.CutDays.ToString(Inv)),
                () => Guarded(() =>
                {
                    book.CutFrom = day;
                    book.CutUntil = day + DaySeriesRules.CutDays;
                    CompanyDirector.Instance.CutPlans(DaySeriesRules.CutPercent);
                }), null, "reallife_rival_confirm_yes", "common_cancel", false, false);
            return true;
        }

        private bool ReleaseCut()
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            if (book == null || book.CutFrom <= 0 || CompanyDirector.Instance == null)
            {
                return false;
            }
            CompanyDirector.Instance.ReleaseCut(book.HeldAlready);
            book.CutFrom = 0;
            book.CutUntil = 0;
            book.HeldAlready = string.Empty;
            return true;
        }
    }
}
