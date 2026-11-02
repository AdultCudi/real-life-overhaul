using System;
using System.Runtime.CompilerServices;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's short notices as notice cards at the top centre, mirrored while the game shows them and in the game's own
    /// fade (in quarters): the personal goal reached (PersonalGoalOverlay), the day's summary (DailySummary: its figure as
    /// the game, or the mod's day result, wrote it, each row as it fades in, the EconoView link), the hospital (its words and
    /// its Continue), the casino boat's message and the skip bridge button. The game's pieces run as before, hidden by leaf
    /// (each panel's own picture by its renderer, its group left to the game's fades); the hospital's dimmer stays the
    /// game's, so the city dims and clicks are held as the game wants. Every press is the game's own button. Cost: four
    /// reads a second of a few alphas while the HUD shows.
    /// </summary>
    internal static class HudNotes
    {
        private const int Goal = 0, Summary = 1, Hospital = 2, Casino = 3, Bridge = 4, Kinds = 5;
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

        static HudNotes()
        {
            for (int i = 0; i < Kinds; i++)
            {
                Cards[i] = new UiHudNoticeModel { Rows = new UiInfoRow[4] };
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
            if (Up(_goal, out float a))
            {
                UiHudNoticeModel c = Card(Goal, a, ref count);
                c.Kicker = Words(_goalWords);
                c.Icon = UiIcon.Trophy;
                c.Title = Words(_goalTitle);
                sig = unchecked(sig * 31 + Hash(c.Title) + Quarter(a));
            }
            if (Up(_summary, out a))
            {
                sig = unchecked(sig * 31 + SummaryCard(Card(Summary, a, ref count)) + Quarter(a));
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
                sig = unchecked(sig * 31 + Hash(c.Body) + Quarter(a));
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
            return unchecked(sig * 31 + rows + (c.Link != null ? 1 : 0));
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
