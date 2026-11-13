using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Extensions;
using RealLifeOverhaul.Calendar;
using Streets;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's short notices as notice cards at the top centre, mirrored while the game shows them and in the game's own
    /// fade (in quarters): the personal goal reached (PersonalGoalOverlay, with the next goal and the reward the economy
    /// paid), the day's summary (DailySummary: its figure as the game, or the mod's day result, wrote it, each row as it
    /// fades in, the day's best earner, the EconoView link), the hospital (where and when the player collapsed, the bill, a
    /// shift the stay went over, its Continue), the casino boat's message, the skip bridge button and (HudJobSummary) the result popup of a delivery shift or a food delivery.
    /// The game's pieces run as before, hidden by leaf
    /// (each panel's own picture by its renderer, its group left to the game's fades); the hospital's dimmer stays the
    /// game's, so the city dims and clicks are held as the game wants. Every press is the game's own button. Cost: four
    /// reads a second of a few alphas while the HUD shows.
    /// </summary>
    internal static class HudNotes
    {
        private const int Goal = 0, Summary = 1, Hospital = 2, Casino = 3, Bridge = 4, Job = 5, Kinds = 6;
        private const float Seen = 0.02f;   // a panel's alpha from which it counts as showing

        private static readonly UiConceal Hidden = new UiConceal();
        private static readonly UiHudNoticeModel[] Cards = new UiHudNoticeModel[Kinds];
        private static readonly UiHudNoticeModel[] Shown = new UiHudNoticeModel[Kinds + 1];
        private static readonly int[] KindOf = new int[Kinds + 1];
        private static readonly UiInfoRow[] RowScratch = new UiInfoRow[4];

        private static CanvasGroup _goal, _summary, _casino, _hospital;
        private static TMP_Text _goalTitle, _goalWords, _summaryHead, _casinoText, _hospitalText;
        private static Transform _summaryRows;
        private static Button _econo, _continue, _bridge;
        private static UI.DailySummary.DailySummary _daily;
        private static bool _on;

        // The collapse, recorded as the game's hospital sequence starts (GlobalEvents.onHospitalRespawnStarts)...
        private static readonly Action OnCollapse = Guard.Wrap(Collapsed);
        private static Action _collapseSeen;
        private static int _fellDay = -1, _fellMinute;
        private static string _fellPlace;
        // ...and the hospital card's words, worked out once a notice.
        private static bool _stayRead;
        private static string _stayBody, _stayMeta;
        private static readonly string[] StayChips = new string[3];
        private static readonly UiRole[] StayTones = new UiRole[3];
        private static int _stayChipCount;
        // The day summary's best earner, worked out once a statement.
        private static object _bestOf;
        private static string _best;

        static HudNotes()
        {
            for (int i = 0; i < Kinds; i++)
            {
                Cards[i] = new UiHudNoticeModel { Rows = new UiInfoRow[4], Chips = new string[3], ChipTones = new UiRole[3] };
            }
        }

        /// <summary>The game's notices hidden by leaf; false while none of them is built.</summary>
        public static bool Begin(UIs uis)
        {
            if (uis == null)
            {
                return false;
            }
            PersonalGoalOverlay overlay = InstanceBehavior<PersonalGoalOverlay>.Instance;
            Transform goal = overlay != null ? overlay.transform.Find("Panel") : null;
            if (goal != null)
            {
                _goal = goal.GetComponent<CanvasGroup>();
                _goalTitle = Text(goal, "PersonalGoalTitle");
                _goalWords = Text(goal, "Description");
                Panel(goal);
            }
            _daily = uis.dailySummary;
            if (_daily != null && _daily.panel != null)
            {
                Transform panel = _daily.panel.transform;
                _summary = _daily.panel;
                _summaryHead = Text(panel, "Headline/Text (TMP)");
                _summaryRows = panel.Find("Container");
                Transform econo = _summaryRows != null ? _summaryRows.Find("EconoviewButton") : null;
                _econo = econo != null ? econo.GetComponent<Button>() : null;
                Panel(panel);
            }
            if (uis.hospitalizationNotification != null)
            {
                Transform hospital = uis.hospitalizationNotification.transform;
                _hospital = hospital.GetComponent<CanvasGroup>();
                Transform message = hospital.Find("Message");
                _hospitalText = Text(message, "Text");
                Transform go = message != null ? message.Find("Continue") : null;
                _continue = go != null ? go.GetComponent<Button>() : null;
                Hidden.Add(message);   // the dimmer stays the game's: the city dims and holds its clicks while the notice is up
            }
            if (uis.casinoMessage != null)
            {
                Transform message = uis.casinoMessage.transform.Find("Message");
                _casino = message != null ? message.GetComponent<CanvasGroup>() : null;
                _casinoText = Text(message, "Text");
                Panel(message);
            }
            Transform bridge = uis.playerHUD != null ? uis.playerHUD.transform.Find("SkipBridgeUi/SkipBridgeButton") : null;
            _bridge = bridge != null ? bridge.GetComponent<Button>() : null;
            Hidden.Add(bridge);
            if (Hidden.Count == 0)
            {
                return false;
            }
            Hidden.Hide();
            HudJobSummary.Begin();
            _on = true;
            return true;
        }

        // A panel whose own group the game fades: its picture by its renderer and each child by its own group.
        private static void Panel(Transform panel)
        {
            if (panel == null)
            {
                return;
            }
            Hidden.Add(panel.GetComponent<Graphic>());
            for (int i = 0; i < panel.childCount; i++)
            {
                Hidden.Add(panel.GetChild(i));
            }
        }

        /// <summary>The game's notices given back as found.</summary>
        public static void End()
        {
            if (!_on)
            {
                return;
            }
            _on = false;
            HudJobSummary.End();
            Hidden.Clear();
        }

        /// <summary>A new city: the notices are looked for again.</summary>
        public static void Rearm()
        {
            End();
            _goal = _summary = _casino = _hospital = null;
            _goalTitle = _goalWords = _summaryHead = _casinoText = _hospitalText = null;
            _summaryRows = null;
            _econo = _continue = _bridge = null;
            _daily = null;
        }

        /// <summary>The notices read again into the HUD's model (a test's sample first); the signature of what shows.</summary>
        public static int Look(UiHudModel model, bool live, UiHudNoticeModel sample)
        {
            int count = 0, sig = 0;
            if (sample != null)
            {
                KindOf[count] = -1;
                Shown[count++] = sample;
                sig = 7919;
            }
            if (live && _on)
            {
                try
                {
                    sig = unchecked(sig * 31 + Read(ref count));
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    Guard.DoesWithout(e, "the HUD's notice cards");
                }
            }
            model.Notices = Shown;
            model.NoticeCount = count;
            return unchecked(sig * 31 + count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Read(ref int count)
        {
            int sig = 0;
            EventHold.Keep(ref GlobalEvents.onHospitalRespawnStarts, OnCollapse, ref _collapseSeen);
            if (Up(_goal, out float a))
            {
                UiHudNoticeModel c = Card(Goal, a, ref count);
                c.Kicker = Words(_goalWords);
                c.Icon = UiIcon.Trophy;
                c.Title = Words(_goalTitle);
                string next = NextGoal();
                c.Body = next != null && next != c.Title ? UiText.T("reallife_ui_hud_goal_next").Replace("{goal}", next) : null;
                float paid = Economy.EconomyDirector.LastGoalReward;
                c.Figure = paid > 0f && Time.unscaledTime - Economy.EconomyDirector.LastGoalRewardAt < RewardShows ? "+" + paid.ToShortCurrencyFormat() : null;
                sig = unchecked(sig * 31 + Hash(c.Title) + Hash(c.Body) * 3 + Hash(c.Figure) * 5 + Quarter(a));
            }
            if (Up(_summary, out a))
            {
                sig = unchecked(sig * 31 + SummaryCard(Card(Summary, a, ref count)) + Quarter(a));
            }
            UiHudNoticeModel job = HudJobSummary.Look(_daily, out int jobSig);
            if (job != null)
            {
                KindOf[count] = Job;
                Shown[count++] = job;
                sig = unchecked(sig * 31 + jobSig);
            }
            if (_hospital != null && _hospital.gameObject.activeInHierarchy && _hospital.alpha > Seen)
            {
                a = _hospital.alpha;
                UiHudNoticeModel c = Card(Hospital, a, ref count);
                c.Kicker = UiText.T("reallife_ui_hud_hospital");
                c.Icon = UiIcon.Plus;   // a red cross on its tile
                c.IconTone = UiRole.Danger;
                c.Title = UiText.T("reallife_ui_hud_hospital_title");
                c.Body = OneLine(Words(_hospitalText));
                c.Action = UiWidgets.There(_continue) ? UiWidgets.Label(_continue) : null;
                Stay(c);
                sig = unchecked(sig * 31 + Hash(c.Body) + Hash(c.Meta) * 3 + c.ChipCount * 5 + Quarter(a));
            }
            else
            {
                _stayRead = false;   // the next notice works its words out again
            }
            if (Up(_casino, out a))
            {
                UiHudNoticeModel c = Card(Casino, a, ref count);
                c.Kicker = UiText.T("common_casino");
                c.Icon = UiIcon.Coin;
                c.Body = OneLine(Words(_casinoText));
                sig = unchecked(sig * 31 + Hash(c.Body) + Quarter(a));
            }
            if (_bridge != null && _bridge.gameObject.activeInHierarchy)
            {
                UiHudNoticeModel c = Card(Bridge, 1f, ref count);
                c.Kicker = UiText.T("reallife_ui_hud_bridge");
                c.Icon = UiIcon.Car;
                c.Title = UiText.T("reallife_ui_hud_bridge_title");
                c.Action = UiWidgets.Label(_bridge);
                sig = unchecked(sig * 31 + Hash(c.Action) + 5);
            }
            return sig;
        }

        // The day's summary as the game's panel shows it: its headline, its figure under its row's name in the colour the
        // game (or the mod's day result) gave it, its other rows as each fades in, and its EconoView button as the link.
        private static int SummaryCard(UiHudNoticeModel c)
        {
            c.Kicker = Words(_summaryHead);
            c.Icon = UiIcon.Chart;
            int rows = 0, sig = Hash(c.Kicker);
            if (_summaryRows != null)
            {
                bool first = true;
                for (int i = 0; i < _summaryRows.childCount; i++)
                {
                    Transform row = _summaryRows.GetChild(i);
                    CanvasGroup fade = row.GetComponent<CanvasGroup>();
                    if (!row.gameObject.activeSelf || row.name != "Row" || fade == null)
                    {
                        continue;
                    }
                    if (fade.alpha < 0.5f)
                    {
                        first = false;
                        continue;   // the game has not shown this row yet
                    }
                    string name = Words(Text(row, "Name"));
                    TMP_Text value = Text(row, "Value");
                    string figure = Words(value);
                    if (first)
                    {
                        c.BigLabel = name;
                        c.Big = figure;
                        c.BigTone = HudSides.Tone(value != null ? value.color : Color.white, UiRole.None) == UiRole.Danger ? UiRole.DangerText : UiRole.OkText;
                        first = false;
                    }
                    else if (rows < RowScratch.Length)
                    {
                        RowScratch[rows++] = new UiInfoRow { Label = name, Value = figure };
                    }
                    sig = unchecked(sig * 31 + Hash(name) + Hash(figure));
                }
            }
            Array.Copy(RowScratch, c.Rows, rows);
            c.RowCount = rows;
            if (_econo != null && _econo.gameObject.activeInHierarchy)
            {
                c.Link = UiWidgets.Label(_econo);
                c.LinkIcon = UiIcon.ChartLineUp;
            }
            c.Footer = BestEarner();
            return unchecked(sig * 31 + rows + (c.Link != null ? 1 : 0) + Hash(c.Footer) * 3);
        }

        // The day's best earner on the summary's foot: the business with the highest profit in the game's latest statement;
        // none when no business made one.
        private static string BestEarner()
        {
            GameInstance save = SaveGameManager.Current;
            List<Entities.FinancialSummary> days = save != null ? save.financialSummaries : null;
            Entities.FinancialSummary day = days != null && days.Count > 0 ? days[days.Count - 1] : null;
            if (ReferenceEquals(day, _bestOf))
            {
                return _best;
            }
            _bestOf = day;
            _best = null;
            if (day == null || day.businessIncomeStatements == null)
            {
                return null;
            }
            float top = 0f;
            Address at = null;
            for (int i = 0; i < day.businessIncomeStatements.Count; i++)
            {
                Entities.FinancialSummary.BusinessIncomeStatement b = day.businessIncomeStatements[i];
                if (b != null && b.Address != null && b.TotalProfit > top)
                {
                    top = b.TotalProfit;
                    at = b.Address;
                }
            }
            if (at == null)
            {
                return null;
            }
            BuildingRegistration reg = Helpers.BuildingHelper.GetBuildingRegistration(at);
            string name = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : at.ToFormattedString();
            _best = UiText.T("reallife_ui_hud_best_earner").Replace("{name}", name);
            return _best;
        }

        // The goal after the newest one reached, read when due (HudGoalBook); none on a game without the goals.
        private static string NextGoal()
        {
            try
            {
                HudGoalBook.Keep();
                return HudGoalBook.Next;
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the next personal goal on the HUD's goal card");
                return null;
            }
        }

        private const float RewardShows = 7f;   // s: a reward paid this long ago is the card's (the game shows a goal for five)

        // ------------------------------------------------------------------------------------------------- the hospital

        // The game's hospital sequence starts: when and near where the player collapsed, for the hospital card.
        private static void Collapsed()
        {
            try
            {
                Fell();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "where the player collapsed, on the HUD's hospital card");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Fell()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            _fellDay = save.Day;
            _fellMinute = save.Hour * 60 + (int)save.Minute;
            _fellPlace = HudLive.LastBar != null ? HudLive.LastBar.Place : null;   // the place pill as the player last saw it
        }

        // The hospital card's words, once a notice: the collapse in a sentence, the time now, and its chips: the bill the
        // game charged, a rota shift the stay went over, and the energy the game gave back.
        private static void Stay(UiHudNoticeModel c)
        {
            GameInstance save = SaveGameManager.Current;
            if (!_stayRead && save != null)
            {
                _stayRead = true;
                StayWords(save);
            }
            if (_stayBody != null)
            {
                c.Body = _stayBody;
            }
            c.Meta = _stayMeta;
            for (int i = 0; i < _stayChipCount; i++)
            {
                c.Chips[i] = StayChips[i];
                c.ChipTones[i] = StayTones[i];
            }
            c.ChipCount = _stayChipCount;
        }

        private static void StayWords(GameInstance save)
        {
            int now = save.Hour * 60 + (int)save.Minute;
            _stayMeta = CalendarText.Clock(now);
            bool fell = _fellDay > 0 && _fellDay >= save.Day - 2;   // this stay's record, not an older one's
            string time = fell ? CalendarText.Clock(_fellMinute) : null;
            _stayBody = !fell ? null : !string.IsNullOrEmpty(_fellPlace)
                ? UiText.T("reallife_ui_hud_hospital_where").Replace("{place}", _fellPlace).Replace("{time}", time)
                : UiText.T("reallife_ui_hud_hospital_when").Replace("{time}", time);
            int n = 0;
            float bill = LastBill(save);
            if (bill < 0f)
            {
                StayChips[n] = UiText.T("reallife_ui_hud_hospital_bill").Replace("{amount}", "-" + Math.Abs(bill).ToShortCurrencyFormat());
                StayTones[n++] = UiRole.Danger;
            }
            if (fell && CareerState.Employed
                && HudNoteRules.MissedShift(CareerState.RotaDays, CareerState.RotaHour, CareerCatalogue.ShiftHours, _fellDay, _fellMinute, save.Day, now) > 0)
            {
                StayChips[n] = UiText.T("reallife_ui_hud_hospital_missed").Replace("{time}", CareerClock.Time(CareerState.RotaHour));
                StayTones[n++] = UiRole.Warn;
            }
            StayChips[n] = UiText.T("reallife_ui_hud_hospital_energy");
            StayTones[n++] = UiRole.Ok;
            _stayChipCount = n;
            _fellDay = -1;   // used: the next stay records its own
        }

        // The game's newest hospital bill, from its transactions (a walk of the week's once a notice); 0 for none today.
        private static float LastBill(GameInstance save)
        {
            if (save.Transactions == null)
            {
                return 0f;
            }
            float bill = 0f;
            foreach (Transaction t in save.Transactions)
            {
                if (t != null && t.timestamp != null && t.timestamp.Day == save.Day && t.transactionType == "ba:transaction_hospitalbill")
                {
                    bill = t.amount;
                }
            }
            return bill;
        }

        // A kind's card made ready to fill: its words cleared, its fade in quarters.
        private static UiHudNoticeModel Card(int kind, float alpha, ref int count)
        {
            UiHudNoticeModel c = Cards[kind];
            c.Kicker = c.Meta = c.Title = c.Body = c.Figure = c.BigLabel = c.Big = c.BigUnder = c.Footer = c.Link = c.Action = null;
            c.Icon = UiIcon.Info;
            c.IconTone = UiRole.None;
            c.FigureTone = UiRole.OkText;
            c.BigTone = UiRole.OkText;
            c.LinkIcon = UiIcon.None;
            c.RowCount = 0;
            c.ChipCount = 0;
            c.Alpha = Quarter(alpha) / 4f;
            KindOf[count] = kind;
            Shown[count++] = c;
            return c;
        }

        // ------------------------------------------------------------------------------------------------- presses

        /// <summary>A card's link or button: the game's own.</summary>
        public static bool Press(int command, int arg)
        {
            if (arg < 0 || arg >= Shown.Length)
            {
                return false;
            }
            int kind = KindOf[arg];
            if (command == UiHudNotice.LinkCommand && kind == Summary)
            {
                return UiWidgets.Press(_econo);
            }
            if (command == UiHudNotice.ActionCommand)
            {
                if (kind == Hospital)
                {
                    return UiWidgets.Press(_continue);
                }
                if (kind == Bridge)
                {
                    return UiWidgets.Press(_bridge);
                }
                if (kind == Job)
                {
                    return HudJobSummary.Press();
                }
            }
            return kind < 0;   // a test's sample: nothing behind it
        }

        // ------------------------------------------------------------------------------------------------- shared

        private static bool Up(CanvasGroup g, out float alpha)
        {
            alpha = g != null && g.gameObject.activeInHierarchy ? g.alpha : 0f;
            return alpha > Seen;
        }

        // The fade in quarters, never under one: a card that shows at all is seen.
        private static int Quarter(float alpha) => Math.Max(1, Math.Min(4, (int)Math.Round(alpha * 4f)));

        private static string OneLine(string words) => words?.Replace("\n", " ").Replace("  ", " ");

        private static TMP_Text Text(Transform parent, string path)
        {
            Transform t = parent != null ? parent.Find(path) : null;
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private static string Words(TMP_Text t) => t != null ? UiWidgets.Text(t) : null;

        private static int Hash(string s) => s != null ? s.GetHashCode() : 0;
    }
}
