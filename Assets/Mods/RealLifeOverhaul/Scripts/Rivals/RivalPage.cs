using System;
using System.Collections.Generic;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The rivals' page in the Finance app's frame: the rich list, the moves and the player's answers in one filtered
    /// table, and in the box where the player stands, a tycoon with the answers to them, or a move. Also the City
    /// Ledger's page of the rich list (<see cref="ListPage"/>). Written on a click: a few dozen strings; nothing per frame.
    /// </summary>
    public static class RivalPage
    {
        public const int KindList = 0, KindMove = 1, KindAnswer = 2;

        public static readonly string[] Kinds = { "reallife_rival_kind_list", "reallife_rival_kind_move", "reallife_rival_kind_answer" };

        private const string GuideTitle = "reallife_meta_title_rivals";   // the Guide's page every page of the rivals points to at its foot

        /// <summary>The page's table: 0 who or what, 1 kind, 2 worth or where it stands, 3 its change or its day, 4 a note.</summary>
        public static readonly TableSpec Spec = new TableSpec("Finance", "Rivals")
            .Column("reallife_rival_col_what", 560f, TableSort.Text, flexible: true)
            .Column("reallife_rival_col_kind", 260f, TableSort.Text)
            .Column("reallife_rival_col_stands", 300f, TableSort.Number)
            .Column("reallife_rival_col_change", 260f, TableSort.Number)
            .Column("reallife_rival_col_note", 440f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", Kinds)
            .Empty("reallife_rival_empty");

        private static readonly List<RichRow> Rows = new List<RichRow>(RivalCatalogue.RichListSize);

        /// <summary>True while the option is on and the rivals run.</summary>
        public static bool On()
        {
            RivalDirector d = RivalDirector.Instance;
            return Settings.Rivals && d != null && d.File != null;
        }

        public static void Write(InfoBox box, Func<string, TableRow> row, string picked, Action<string> pick, Action back)
        {
            RivalDirector director = RivalDirector.Instance;
            RivalFile f = director != null ? director.File : null;
            if (f == null)
            {
                box.Title(RivalText.T("reallife_rival_title"));
                box.Line(RivalText.T("reallife_rival_off"), CellTone.Yellow);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_rival_back", back);
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            long worth = RivalGame.Worth();
            int level = Prices.Level;
            int day = RivalGame.Day;
            WriteRows(f, row, worth, level);
            bool written = false;
            if (picked != null && picked.StartsWith("who:", StringComparison.Ordinal))
            {
                int who = Number(picked, 4);
                written = who >= 0 && who < f.Tycoons.Length && WriteTycoon(director, f, who, box, picked, pick, back, day);
            }
            else if (picked != null && picked.StartsWith("move:", StringComparison.Ordinal))
            {
                RivalMove m = f.Move(Number(picked, 5));
                written = m != null && WriteMove(director, f, m, box, pick, back);
            }
            if (!written)
            {
                WriteSummary(director, f, box, worth, level, day, back);
            }
        }

        private static void WriteRows(RivalFile f, Func<string, TableRow> row, long worth, int level)
        {
            RivalRules.RichList(f, worth, level, RivalGame.PlayerName(), Rows);
            string kind = RivalText.T(Kinds[KindList]);
            for (int i = 0; i < Rows.Count; i++)
            {
                RichRow r = Rows[i];
                TableRow t = row("who:" + r.Who);
                t.Kind = KindList;
                t.Pick = r.Who >= 0 && r.Who < RivalCatalogue.TycoonCount;
                t.Set(0, RivalText.Fill("reallife_rival_rank_name", "rank", (i + 1).ToString(), "name", r.Name));
                t.Set(1, kind);
                t.Set(2, RivalText.Money(r.Worth), r.Worth);
                t.Set(3, RivalText.Change(r.Worth, r.WeekAgo), r.WeekAgo > 0 ? r.Worth - r.WeekAgo : 0);
                t.Set(4, r.Who == RichRow.Player ? RivalText.T("reallife_rival_note_you") : r.Who >= RichRow.FortuneBase ? RivalText.T("reallife_rival_note_old_money")
                    : RivalCatalogue.Tycoons[r.Who].Holding);
                if (r.Who == RichRow.Player)
                {
                    t.Tones[0] = (byte)TableRow.Green;
                }
                if (r.WeekAgo > 0 && r.Worth != r.WeekAgo)
                {
                    t.Tones[3] = (byte)(r.Worth > r.WeekAgo ? TableRow.Green : TableRow.Red);
                }
            }
            kind = RivalText.T(Kinds[KindMove]);
            for (int i = f.Moves.Count - 1; i >= 0; i--)
            {
                RivalMove m = f.Moves[i];
                TableRow t = row("move:" + m.Id);
                t.Kind = KindMove;
                t.Pick = true;
                t.Set(0, RivalCatalogue.Tycoons[m.Tycoon].Name + ": " + RivalText.What(m));
                t.Set(1, kind);
                t.Set(2, RivalText.State(m.State), (int)m.State);
                int when = m.State == RivalMoveState.Running ? m.End : m.Land;
                t.Set(3, RivalText.Fill("reallife_rival_day", "day", when.ToString()), when);
                t.Set(4, m.District.Length > 0 ? RivalGame.DistrictName(m.District) : string.Empty);
                t.Tones[2] = (byte)(m.State == RivalMoveState.Announced ? TableRow.Yellow
                    : m.State == RivalMoveState.Running ? TableRow.Red
                    : m.State == RivalMoveState.Stopped ? TableRow.Green
                    : TableRow.Plain);
            }
            kind = RivalText.T(Kinds[KindAnswer]);
            for (int i = f.Counters.Count - 1; i >= 0; i--)
            {
                RivalCounterNote c = f.Counters[i];
                TableRow t = row("answer:" + c.Id);
                t.Kind = KindAnswer;
                t.Pick = false;
                t.Set(0, RivalText.Fill("reallife_rival_answer_row", "answer", RivalText.CounterName(c.Kind), "rival", RivalCatalogue.Tycoons[c.Tycoon].Name));
                t.Set(1, kind);
                t.Set(2, RivalText.Money(c.Cost), c.Cost);
                t.Set(3, RivalText.Fill("reallife_rival_day", "day", c.Day.ToString()), c.Day);
                t.Set(4, string.Empty);
            }
        }

        private static void WriteSummary(RivalDirector director, RivalFile f, InfoBox box, long worth, int level, int day, Action back)
        {
            box.Title(RivalText.T("reallife_rival_title"));
            int tier = RivalRules.Tier(worth, level, RivalDirector.FirstFrom);
            box.Chip(RivalText.Tier(tier), tier >= RivalCatalogue.BidTier ? CellTone.Red : tier >= 1 ? CellTone.Yellow : CellTone.Plain);
            if (director.Resting)
            {
                box.Line(RivalText.T("reallife_rival_resting"), CellTone.Yellow);
            }
            int forward = 0;
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                forward += f.Tycoons[i].Active ? 1 : 0;
            }
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_rival_label_rank", RivalText.Fill("reallife_rival_rank_of", "rank", RivalRules.Rank(f, worth, level).ToString(), "count", RivalCatalogue.RichListSize.ToString()));
            box.Row("reallife_rival_label_worth", RivalText.Money(worth));
            box.Row("reallife_rival_label_forward", RivalText.Fill("reallife_rival_forward_of", "count", forward.ToString(), "all", RivalCatalogue.TycoonCount.ToString()));
            box.Row("reallife_rival_label_strength", RivalText.Strength(RivalDirector.Strength));
            RivalMove pending = f.Pending();
            if (pending != null)
            {
                box.Line(RivalText.Fill("reallife_rival_pending_line", "rival", RivalCatalogue.Tycoons[pending.Tycoon].Name, "what", RivalText.What(pending), "day", pending.Land.ToString()),
                    CellTone.Yellow);
            }
            else if (tier == 0)
            {
                box.Note(RivalText.Fill("reallife_rival_quiet_note", "amount", RivalText.Money(RivalRules.At(RivalDirector.FirstFrom, level))));
            }
            else
            {
                box.Note(RivalText.Fill("reallife_rival_next_note", "day", Math.Max(day + 1, f.NextMoveDay).ToString()));
            }
            long breakUp = RivalRules.BreakUpToCome(f, out int breakUpDay);
            if (breakUp > 0)
            {
                box.Line(RivalText.Fill("reallife_rival_breakup_line", "amount", RivalText.Money(breakUp), "day", breakUpDay.ToString()), CellTone.Green);
            }
            box.Note(RivalText.T("reallife_rival_pick_note"));
            UnityEngine.Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_rival_open_list", () => director.ShowList());
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_rival_back", back);
            Meta.Guide.Point(box, GuideTitle);
        }

        private static bool WriteTycoon(RivalDirector director, RivalFile f, int who, InfoBox box, string picked, Action<string> pick, Action back, int day)
        {
            Tycoon t = f.Tycoons[who];
            if (t.ActiveDay < 0)
            {
                return false;
            }
            TycoonDef d = t.Def;
            box.Title(d.Name);
            box.Chip(d.Holding, t.Active ? CellTone.Yellow : CellTone.Green);
            box.Section("reallife_ibox_sec_now");
            if (!t.Active)
            {
                box.Line(RivalText.Fill("reallife_rival_out_line", "day", t.DefeatedDay.ToString()), CellTone.Green);
                Foot(box, pick, back, null);
                return true;
            }
            box.Row("reallife_rival_label_their_worth", RivalText.Money(t.Worth));
            string change = RivalText.Change(t.Worth, t.WeekAgo);
            if (change.Length > 0)
            {
                box.Row("reallife_rival_label_week", change, t.Worth >= t.WeekAgo ? CellTone.Red : CellTone.Green);
            }
            if (director.HoldingShares(who, out long held, out long most, out long _, out int mid))
            {
                box.Row("reallife_rival_label_holding", RivalText.Fill("reallife_rival_holding_value", "held", RivalText.N(held), "most", RivalText.N(most), "price", RivalText.MoneyCents(mid)));
            }
            else
            {
                box.Line(RivalText.T("reallife_rival_holding_none"));
            }
            if (day < t.QuietUntil || day < t.SlowUntil || day < t.BarredUntil)
            {
                box.Line(RivalText.Fill("reallife_rival_checked_line", "quiet", Math.Max(day, t.QuietUntil).ToString(),
                    "slow", Math.Max(day, t.SlowUntil).ToString(), "barred", Math.Max(day, t.BarredUntil).ToString()), CellTone.Green);
            }
            box.Section("reallife_rival_sec_answers");
            var can = new bool[(int)RivalCounter.Count];
            for (int k = 0; k < (int)RivalCounter.Count; k++)
            {
                RivalWhy why = director.CanCounter(who, (RivalCounter)k, out long cost);
                can[k] = why == RivalWhy.None;
                box.RowWords(RivalText.CounterName((RivalCounter)k), can[k] ? RivalText.Money(cost) : RivalText.Why(why), can[k] ? CellTone.Plain : CellTone.Yellow);
            }
            UnityEngine.Transform first = null, second = null;
            for (int k = 0; k < (int)RivalCounter.Count; k++)
            {
                if (!can[k])
                {
                    continue;
                }
                var kind = (RivalCounter)k;
                UnityEngine.Transform rowOf = k < 3 ? first ?? (first = box.ButtonRow()) : second ?? (second = box.ButtonRow());
                InfoBox.Button(rowOf, k % 3, RivalCatalogue.CounterNames[k], () => Ask(director, who, kind, picked, pick));
            }
            Foot(box, pick, back, d.StockId);
            return true;
        }

        private static bool WriteMove(RivalDirector director, RivalFile f, RivalMove m, InfoBox box, Action<string> pick, Action back)
        {
            TycoonDef d = RivalCatalogue.Tycoons[m.Tycoon];
            box.Title(RivalText.MoveName(m.Kind));
            box.Chip(RivalText.State(m.State), m.State == RivalMoveState.Running ? CellTone.Red : m.State == RivalMoveState.Announced ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_rival_label_by", d.Name + ", " + d.Holding);
            box.Line(RivalText.Fill(m.State == RivalMoveState.Announced ? RivalText.CameKey(m.Kind) : RivalText.CameKey(m), RivalText.Pairs(RivalText.MoveData(m))));
            box.Row("reallife_rival_label_announced", m.Announced.ToString());
            box.Row(m.State == RivalMoveState.Running ? "reallife_rival_label_ends" : "reallife_rival_label_comes", (m.State == RivalMoveState.Running ? m.End : m.Land).ToString());
            box.Note(RivalText.T(RivalCatalogue.MoveAbout[RivalCatalogue.Clamp((int)m.Kind, 0, RivalCatalogue.MoveAbout.Length - 1)]));
            Action close = () => pick(null);
            UnityEngine.Transform buttons = box.ButtonRow();
            if (m.Live && f.Tycoons[m.Tycoon].Active)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_rival_open_rival", () => pick("who:" + m.Tycoon));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "common_close", close);
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_rival_back", back);
            Meta.Guide.Point(box, GuideTitle);
            return true;
        }

        // The foot of a tycoon's page: their holding in the Stocks app, closing it and going back, and the Guide's page.
        private static void Foot(InfoBox box, Action<string> pick, Action back, int? stock)
        {
            UnityEngine.Transform buttons = box.ButtonRow();
            if (stock.HasValue && RivalBridges.OpenStock != null)
            {
                int id = stock.Value;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_rival_open_holding", () => RivalBridges.OpenStock?.Invoke(id));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "common_close", () => pick(null));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_rival_back", back);
            Meta.Guide.Point(box, GuideTitle);
        }

        // The game's own question before a counter is used: what it does and what it costs; on yes, done, and any refusal said.
        private static void Ask(RivalDirector director, int who, RivalCounter kind, string picked, Action<string> pick)
        {
            TycoonDef d = RivalCatalogue.Tycoons[who];
            RivalWhy why = director.CanCounter(who, kind, out long cost);
            if (why != RivalWhy.None)
            {
                Notifications.Show(NotificationType.Warning, RivalText.WhyKey(why), null, 8f, null, null, true, false);
                pick(picked);
                return;
            }
            LanguageChangeEventDataHolder title = "reallife_rival_confirm_title".Localize(new { answer = RivalText.CounterName(kind), rival = d.Name });
            LanguageChangeEventDataHolder body;
            if (kind == RivalCounter.Takeover)
            {
                director.TakeoverQuote(who, out long _, out long back);
                body = "reallife_rival_confirm_takeover".Localize(new
                {
                    holding = d.Holding, cost = RivalText.Money(cost), back = RivalText.Money(back), days = RivalCatalogue.BreakUpDays.ToString()
                });
            }
            else
            {
                body = "reallife_rival_confirm_body".Localize(new { about = RivalText.CounterAbout(kind), cost = RivalText.Money(cost), rival = d.Name });
            }
            UiClone.Safe(() => GameConfirm.Show(title, body, () => UiClone.Safe(() =>
            {
                string refused = director.Counter(who, kind);
                if (refused != null)
                {
                    Notifications.Show(NotificationType.Warning, refused, null, 8f, null, null, true, false);
                }
                pick(picked);
            }), null, "reallife_rival_confirm_yes", "common_cancel", false, kind == RivalCounter.Takeover));
        }

        /// <summary>The number after a row id's prefix ("move:3" from 5 is 3); -1 if there is none.</summary>
        private static int Number(string id, int from)
        {
            return id != null && id.Length > from
                && int.TryParse(id.Substring(from), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : -1;
        }

        /// <summary>The rich list as a front page of the City Ledger: the top of the list, the player's place, and the rivals' week.</summary>
        public static RichListPage ListPage(RivalFile f, long worth, int day)
        {
            int level = Prices.Level;
            RivalRules.RichList(f, worth, level, RivalGame.PlayerName(), Rows);
            int rank = RivalRules.Rank(f, worth, level);
            var page = new RichListPage
            {
                Day = day,
                Header = RivalText.T("reallife_rival_list_header"),
                EarLeftLabel = RivalText.T("reallife_rival_list_ear_left"),
                EarLeft = Rows.Count > 0 ? Rows[0].Name : string.Empty,
                EarRightLabel = RivalText.T("reallife_rival_list_ear_right"),
                EarRight = rank.ToString(),
                DateMiddle = RivalText.T("reallife_rival_list_dateline"),
                DateRight = RivalText.T("reallife_rival_list_dateline_right"),
                Kicker = RivalText.T("reallife_rival_list_kicker"),
                Headline = RivalText.Fill(rank == 1 ? "reallife_rival_list_head_you" : "reallife_rival_list_head", "name", Rows.Count > 0 ? Rows[0].Name : string.Empty),
                Stand = RivalText.Fill("reallife_rival_list_stand", "rank", rank.ToString(), "count", Rows.Count.ToString()),
                Byline = RivalText.T("reallife_rival_list_byline"),
                Body = RivalText.T("reallife_rival_list_body"),
                NoticeHead = RivalText.T("reallife_rival_list_notice_head"),
                Notice = RivalText.T("reallife_rival_list_notice")
            };
            var box = new List<string>(Rows.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                box.Add(RivalText.Fill("reallife_rival_list_line", "rank", (i + 1).ToString(), "name", Rows[i].Name, "worth", RivalText.Money(Rows[i].Worth)));
            }
            page.BoxLabels.Add(RivalText.T("reallife_rival_list_box"));
            page.BoxLines.Add(box);
            var rivals = new List<string>(4);
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                Tycoon t = f.Tycoons[i];
                if (t.Active)
                {
                    rivals.Add(RivalText.Fill("reallife_rival_list_rival", "rival", t.Def.Name, "holding", t.Def.Holding, "change",
                        RivalText.Change(t.Worth, t.WeekAgo).Length > 0 ? RivalText.Change(t.Worth, t.WeekAgo) : RivalText.Money(0)));
                }
                else if (t.DefeatedDay >= 0)
                {
                    rivals.Add(RivalText.Fill("reallife_rival_list_rival_out", "rival", t.Def.Name, "holding", t.Def.Holding));
                }
            }
            if (rivals.Count == 0)
            {
                rivals.Add(RivalText.T("reallife_rival_list_none"));
            }
            page.Labels.Add(RivalText.T("reallife_rival_list_section"));
            page.Texts.Add(rivals);
            return page;
        }
    }
}
