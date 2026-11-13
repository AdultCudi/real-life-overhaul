using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Entities;
using Extensions;
using Helpers;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI.Smartphone.Apps.Persona;
using UnityEngine;
using UnityEngine.UI;
using CharacterInfo = UI.Smartphone.Apps.Persona.CharacterInfo;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Persona as a screen of the kit, mirrored from the game's own app (Football Manager's player profile): the head from
    /// the game's facts as it wrote them on opening, the worth's parts and the weekly income as the game's own helpers give
    /// them (the same calls its facts box makes), the needs as the game holds them, what moves the happiness read off the
    /// game's own list (the game's and the mod's moods alike), the courses off the education box (the mod's among them,
    /// under their headings), and the goals in the order the game's two toggles sort them, whose heads press those very
    /// toggles. Cost: a look at a few of the app's counts and words four times a second while it shows (nothing allocated);
    /// a layout only when they changed.
    /// </summary>
    public sealed class PersonaScreen : IUiScreen, IUiPointing
    {
        private const float Every = 0.25f;           // seconds between looks at the game's app
        private const int IncomeDays = 7;            // the days the game's own charts of a weekly income run over
        private const string TemplateName = "StatModifierTemplate";

        private readonly Transform _root;
        private readonly CharacterInfo _info;
        private readonly CharacterStats _stats;
        private readonly EducationInfo _education;
        private readonly PersonalGoalsUI _goals;
        private readonly PersonaView _v = new PersonaView();
        private readonly UiPersonaModel _m = new UiPersonaModel();
        private readonly List<PersonaModifier> _mods = new List<PersonaModifier>(16);
        private readonly List<PersonaCourse> _courses = new List<PersonaCourse>(64);
        private readonly List<PersonaGoal> _goalRows = new List<PersonaGoal>(48);
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private int _tab, _selected = -1, _reveal = -1, _focus = -1;
        private float _offset;

        public PersonaScreen(Transform root)
        {
            _root = root;
            _info = root.GetComponentInChildren<CharacterInfo>(true);
            _stats = root.GetComponentInChildren<CharacterStats>(true);
            _education = root.GetComponentInChildren<EducationInfo>(true);
            _goals = root.GetComponentInChildren<PersonalGoalsUI>(true);
        }

        public string Id => PersonaKit.Id;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            PersonaViews.Persona(_m, UiText.Words, _v);
            UiPersona.Lay(s, _m, frame, dark, keys);
            _offset = _m.Table.Offset;
            _reveal = -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            switch (command)
            {
                case UiAppWindow.NavCommand:
                case UiPersona.LinkCommand:
                    return Show(arg);
                case UiDataTable.First + UiDataTable.Row:
                    return false;   // the lists are read only: a row opens nothing in the game's app either
                case UiDataTable.First + UiDataTable.Head:
                    return Sort(arg);
                case UiChart.First + UiChart.Point:
                    _focus = _focus == arg ? -1 : arg;
                    return true;
                default:
                    return false;
            }
        }

        public void Focused(int command, int arg)
        {
            if (command == UiDataTable.First + UiDataTable.Row && arg != _selected)
            {
                _selected = arg;   // the cursor chooses its row, as a management game's tables follow it
                _dirty = true;
            }
            else if (command == UiChart.First + UiChart.Point && arg != _focus)
            {
                _focus = arg;      // a point under the cursor reads in its card
                _dirty = true;
            }
        }

        public void Pointed(int command, int arg)
        {
            int point = command == UiChart.First + UiChart.Point ? arg : -1;
            if (point != _focus)
            {
                _focus = point;
                _dirty = true;
            }
        }

        public bool Back() => false;   // nothing of the kit's own is open over the page: the game closes its app

        public void Tab(int step)
        {
            Show((_tab + step + 4) % 4);
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            _reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _offset = Math.Max(0f, _offset - notches * UiTokens.S16);
            _dirty = true;
        }

        private bool Show(int tab)
        {
            if (tab < 0 || tab > UiPersona.Goals)
            {
                return false;
            }
            if (tab != _tab)
            {
                _tab = tab;
                _selected = -1;
                _offset = 0f;
                _focus = -1;
            }
            return true;
        }

        // A goals' head is the game's own sort over its list, called by its public method (the one its header buttons call):
        // it cycles the order, which the kit reads back.
        private bool Sort(int column)
        {
            if (_tab != UiPersona.Goals || _goals == null || (column != 1 && column != 3))
            {
                return Done(false, "sort the goals");
            }
            if (column == 1)
            {
                _goals.ToggleCurrentGoalTierSorting();
            }
            else
            {
                _goals.ToggleSteamAchievementTierSorting();
            }
            return Done(true, "sort the goals by " + (column == 1 ? "their tier" : "Steam's tier") + ": handed to the game");
        }

        // ------------------------------------------------------------------------------------------------- the game's state

        // What would change the screen: the facts' words (the game writes them each time the app opens), the needs, the
        // lists' counts, the goals' sort, and the kit's own tab, choice and reading.
        private long Signature()
        {
            long sig = _tab * 31L + _selected;
            sig = sig * 31 + _focus;
            sig = sig * 31 + Identity(Label(GameContract.PersonaName, _info));
            sig = sig * 31 + Identity(Label(GameContract.PersonaBusinesses, _info));
            GameInstance save = SaveGameManager.Current;
            if (save != null)
            {
                sig = sig * 31 + Mathf.RoundToInt(save.Energy);
                sig = sig * 31 + Mathf.RoundToInt(save.Hunger);
                sig = sig * 31 + Mathf.RoundToInt(save.Happiness);
                sig = sig * 31 + (save.happinessModifiers != null ? save.happinessModifiers.Count : -1);
            }
            var list = GameContract.PersonaModifiers.Field?.GetValue(_stats) as Transform;
            sig = sig * 31 + (list != null && list.gameObject.activeSelf ? list.childCount : -1);
            var template = GameContract.PersonaCourseTemplate.Field?.GetValue(_education) as Component;
            sig = sig * 31 + (template != null && template.transform.parent != null ? template.transform.parent.childCount : -1);
            var entries = GameContract.PersonaGoalEntries.Field?.GetValue(_goals) as List<Transform>;
            sig = sig * 31 + (entries != null ? entries.Count : -1);
            sig = sig * 31 + Number(GameContract.PersonaGoalSort, _goals) * 3 + Number(GameContract.PersonaGoalOrder, _goals);
            return sig;
        }

        private static long Identity(object o) => o != null ? RuntimeHelpers.GetHashCode(o) : 0;

        private void Build()
        {
            _v.Tab = _tab;
            _v.Name = Words(GameContract.PersonaName, _info) ?? string.Empty;
            FaceSources.Player(_v.Name);   // the HUD portrait of them, as the game draws it
            _v.Age = Whole(Words(GameContract.PersonaAge, _info));
            _v.Businesses = Whole(Words(GameContract.PersonaBusinesses, _info));
            _v.Employees = Whole(Words(GameContract.PersonaEmployees, _info));
            PersonalWealthData wealth = PlayerHelper.GetPersonalWealth();   // the call the game's facts box makes for its own figures
            _v.Cash = wealth.cash;
            _v.Investments = wealth.totalInvestments;
            _v.Assets = wealth.totalAssets;
            _v.Loans = wealth.totalLoans;
            _v.WeeklyIncome = WeeklyIncome();
            _v.Money = f => f.ToShortCurrencyFormat(abbreviated: true);
            Income();
            GameInstance save = SaveGameManager.Current;
            _v.Energy = save != null ? Mathf.RoundToInt(save.Energy) : 0;
            _v.Hunger = save != null ? Mathf.RoundToInt(save.Hunger) : 0;
            _v.Happiness = save != null ? Mathf.RoundToInt(save.Happiness) : 0;
            Modifiers();
            Courses();
            Goals();
            _v.Selected = _selected;
            _v.Offset = _offset;
            _v.Reveal = _reveal;
            _v.IncomeFocus = _focus;
        }

        // The weekly income as the game's facts box sums it: the profit of the last seven days' summaries.
        private static float WeeklyIncome()
        {
            List<FinancialSummary> days = FinancialSummaryHelper.GetLastFinancialSummaries(7);
            float sum = 0f;
            for (int i = 0; days != null && i < days.Count; i++)
            {
                sum += days[i] != null ? days[i].totalProfit : 0f;
            }
            return sum;
        }

        // The weekly income's last days as the game keeps them for its own chart of the player (today's figure in place of
        // today's entry, as that chart shows it); read only: the game's list is never written here.
        private void Income()
        {
            GameInstance save = SaveGameManager.Current;
            List<Tuple<int, float>> history = save != null ? save.playerWeeklyIncomeHistory : null;
            int today = save != null ? save.Day : 0;
            var values = new List<float>(IncomeDays);
            var days = new List<int>(IncomeDays);
            for (int i = history != null ? history.Count - 1 : -1; i >= 0 && values.Count < IncomeDays - 1; i--)
            {
                Tuple<int, float> e = history[i];
                if (e != null && e.Item1 != today)
                {
                    values.Insert(0, e.Item2);
                    days.Insert(0, e.Item1);
                }
            }
            values.Add(_v.WeeklyIncome);
            days.Add(today);
            bool any = false;
            for (int i = 0; i < values.Count; i++)
            {
                any |= Math.Abs(values[i]) > 0.5f;
            }
            _v.IncomeDays = any ? values.ToArray() : new float[0];   // a line of nothing but zeros says less than the chart's empty words
            _v.IncomeDayNumbers = any ? days.ToArray() : new int[0];
        }

        // What moves the happiness, off the game's own list (copies of its row template), in its order, the mod's moods among
        // the game's: each row's words, its time left (the game writes a dash for none) and its share.
        private void Modifiers()
        {
            _mods.Clear();
            var list = GameContract.PersonaModifiers.Field?.GetValue(_stats) as Transform;
            if (list != null && list.gameObject.activeSelf)
            {
                for (int i = 0; i < list.childCount; i++)
                {
                    Transform row = list.GetChild(i);
                    if (!row.gameObject.activeSelf || row.name == TemplateName)
                    {
                        continue;
                    }
                    string name = Child(row, "Name");
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }
                    string left = Child(row, "TimeLeft");
                    _mods.Add(new PersonaModifier { Name = name, Left = string.IsNullOrEmpty(left) || left == "-" ? null : left, Amount = Share(Child(row, "Amount")) });
                }
            }
            _v.Modifiers = _mods.ToArray();
        }

        // The courses off the education box in its order: the game's diplomas, then the mod's under their headings, each with
        // its name, its status words and where it stands by their key and its tick.
        private void Courses()
        {
            _courses.Clear();
            var template = GameContract.PersonaCourseTemplate.Field?.GetValue(_education) as Component;
            Transform parent = template != null ? template.transform.parent : null;
            string group = null, groupKey = null;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform row = parent.GetChild(i);
                if (row == template.transform || !row.gameObject.activeSelf)
                {
                    continue;
                }
                EducationInfoEntry entry = row.GetComponent<EducationInfoEntry>();
                var header = entry != null ? GameContract.PersonaCourseName.Field?.GetValue(entry) as TextLocalizationComponent : null;
                var status = entry != null ? GameContract.PersonaCourseStatus.Field?.GetValue(entry) as TextLocalizationComponent : null;
                if (header == null || status == null)
                {
                    continue;
                }
                Image back = row.GetComponent<Image>();
                if (back != null && !back.enabled)
                {
                    group = UiWidgets.Text(header.TextContainer);   // the mod's heading over its group of courses (Careers/PersonaEducation)
                    groupKey = header.Key;
                    continue;
                }
                var tick = GameContract.PersonaCourseTick.Field?.GetValue(entry) as GameObject;
                _courses.Add(new PersonaCourse
                {
                    Name = UiWidgets.Text(header.TextContainer), Status = UiWidgets.Text(status.TextContainer),
                    State = PersonaViews.StateOf(status.Key, tick != null && tick.activeSelf), Group = group, GroupKey = groupKey
                });
            }
            _v.Courses = _courses.ToArray();
        }

        // The goals in the order the game's toggles sorted them: each group's goal and words off its entry, the tiers reached
        // here and the highest on Steam from the group, and the sort the toggles hold.
        private void Goals()
        {
            _goalRows.Clear();
            var groups = GameContract.PersonaGoalGroups.Field?.GetValue(_goals) as List<PersonalGoalTierGroup>;
            var entries = GameContract.PersonaGoalEntries.Field?.GetValue(_goals) as List<Transform>;
            int n = groups != null && entries != null ? Math.Min(groups.Count, entries.Count) : 0;
            for (int i = 0; i < n; i++)
            {
                PersonalGoalTierGroup g = groups[i];
                PersonalGoalEntry entry = entries[i] != null ? entries[i].GetComponent<PersonalGoalEntry>() : null;
                if (g == null || entry == null)
                {
                    continue;
                }
                var title = GameContract.PersonaGoalTitle.Field?.GetValue(entry) as TextLocalizationComponent;
                var progress = GameContract.PersonaGoalProgress.Field?.GetValue(entry) as TextLocalizationComponent;
                int tiers = 0;
                for (int t = 0; t < PersonaViews.GoalsTiers; t++)
                {
                    tiers += g.IsTierCompleted(t) ? 1 : 0;
                }
                _goalRows.Add(new PersonaGoal
                {
                    Title = title != null ? UiWidgets.Text(title.TextContainer) : null,
                    Progress = progress != null && progress.gameObject.activeSelf ? UiWidgets.Text(progress.TextContainer) : null,
                    Tiers = tiers, Steam = g.HighestCompletedSteamTierIndex, Kind = g.DisplayGoal != null ? g.DisplayGoal.GetType().Name : null, TierTitles = TierTitles(g)
                });
            }
            _v.Goals = _goalRows.ToArray();
            _v.GoalSort = Number(GameContract.PersonaGoalSort, _goals);
            _v.GoalSortDown = Number(GameContract.PersonaGoalOrder, _goals) == 1;   // the game's SortDirection: 1 descending, 2 ascending
        }

        // What each tier asks, as the game's tooltip on that tier's box says it (the title of the tier's goal), so a player can see what Silver needs before reaching Bronze.
        private static string[] TierTitles(PersonalGoalTierGroup g)
        {
            var titles = new string[PersonaViews.GoalsTiers];
            for (int t = 0; t < titles.Length; t++)
            {
                GenericPersonalGoal tier = g.GetTier(t);
                titles[t] = null;
                if (tier != null)
                {
                    LanguageChangeEventDataHolder title = tier.GetTitle();
                    titles[t] = string.IsNullOrEmpty(title.Key) ? null : Localizor.LocalizorManager.GetLocalization(title.Key, title.Arguments);
                }
            }
            return titles;
        }

        // ------------------------------------------------------------------------------------------------- reading helpers

        private static TMP_Text Label(GameMember member, Component owner) => owner != null ? member.Field?.GetValue(owner) as TMP_Text : null;

        private static string Words(GameMember member, Component owner)
        {
            TMP_Text label = Label(member, owner);
            return label != null ? UiWidgets.Text(label) : null;
        }

        private static string Child(Transform row, string name)
        {
            Transform t = row.Find(name);
            TMP_Text label = t != null ? t.GetComponent<TMP_Text>() : null;
            return label != null ? UiWidgets.Text(label) : null;
        }

        // A whole number the game wrote as words (an age, a count), 0 when it wrote none.
        private static int Whole(string words) =>
            int.TryParse(words, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

        // A modifier's share as the game writes it ("+5%", "-12%").
        private static int Share(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return 0;
            }
            int n = 0, sign = 1;
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (c == '-' || c == '−')
                {
                    sign = -1;
                }
                else if (c >= '0' && c <= '9')
                {
                    n = n * 10 + (c - '0');
                }
            }
            return sign * n;
        }

        // An enum field of the game's as its number (its sort's kind and direction), 0 when it cannot be read.
        private static int Number(GameMember member, object owner)
        {
            object value = owner != null ? member.Field?.GetValue(owner) : null;
            return value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        // One line a press in Player.log, so a report shows each button reaching the game.
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("Persona (kit): " + what + (done ? string.Empty : " was not shown by the game, so nothing was pressed"));
            return done;
        }
    }
}
