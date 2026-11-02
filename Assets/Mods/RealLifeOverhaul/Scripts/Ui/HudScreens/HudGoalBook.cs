using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Extensions;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's personal goals as the HUD shows them, grouped as the Persona app groups them: the goals it lists (not
    /// hidden) by kind (the tier identifier; a kind's three lowest goals are its tiers), each kind's goal now its first open
    /// tier. The objectives' band shows the first kinds' goals now in the game's list order with their progress, and how
    /// many goals are done; the goal card names the newest reached goal's next tier. Read when a goal is reached and each
    /// new game hour, a bank goal's figure once a second: the goals' own sums (some walk every building) run a few times an
    /// hour. Cost: nothing allocated between reads but the words shown.
    /// </summary>
    internal static class HudGoalBook
    {
        public const int Most = 3;   // the goals the band shows
        private const int MostKinds = 256, Tiers = 3;

        public static readonly string[] Titles = new string[Most];
        public static readonly string[] Progress = new string[Most];

        /// <summary>The goals shown, up to <see cref="Most"/>.</summary>
        public static int Count { get; private set; }

        /// <summary>The goals the Persona app lists that are reached, and all it lists.</summary>
        public static int Done { get; private set; }
        public static int Total { get; private set; }

        /// <summary>The goal after the newest one reached (its kind's next tier, else the first goal now), in the game's words; null for none.</summary>
        public static string Next { get; private set; }

        private static readonly GenericPersonalGoal[] Shown = new GenericPersonalGoal[Most];
        private static readonly GenericPersonalGoal[,] Tier = new GenericPersonalGoal[MostKinds, Tiers];
        private static readonly int[] TierCount = new int[MostKinds];
        private static readonly Dictionary<string, int> KindAt = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, GenericPersonalGoal> ById = new Dictionary<string, GenericPersonalGoal>(StringComparer.Ordinal);
        private static readonly HashSet<string> Reached = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, PropertyInfo[]> ArgsOf = new Dictionary<Type, PropertyInfo[]>();

        private static List<string> _list;    // the save's goals reached when last read...
        private static int _count = -1;       // ...how many it held...
        private static int _hour = -1;        // ...and the game's hour then
        private static float _bankAt;         // when the bank goals' figures were last read
        private static string _sign;          // the game's currency sign, as its money is written

        /// <summary>Forgets what was read: another city.</summary>
        public static void Forget()
        {
            _list = null;
            _count = _hour = -1;
            Count = Done = Total = 0;
            Next = null;
            Array.Clear(Shown, 0, Shown.Length);
        }

        /// <summary>Read again when a goal was reached or an hour went by, a bank goal's figure each second; true if anything shown changed.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Keep()
        {
            GameInstance save = SaveGameManager.Current;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            if (save == null || save.completedPersonalGoals == null || game == null || game.personalGoals == null)
            {
                bool had = Count > 0 || Total > 0;
                Forget();
                return had;
            }
            int hour = save.Day * 24 + save.Hour;
            if (!ReferenceEquals(save.completedPersonalGoals, _list) || save.completedPersonalGoals.Count != _count || hour != _hour)
            {
                _list = save.completedPersonalGoals;
                _count = _list.Count;
                _hour = hour;
                _bankAt = Time.unscaledTime;
                Read(game.personalGoals, _list);
                return true;
            }
            if (Time.unscaledTime - _bankAt < 1f)
            {
                return false;
            }
            _bankAt = Time.unscaledTime;
            bool changed = false;
            for (int i = 0; i < Count; i++)
            {
                if (Shown[i] is BankBalanceGoal)
                {
                    string now = ProgressOf(Shown[i]);
                    changed |= now != Progress[i];
                    Progress[i] = now;
                }
            }
            return changed;
        }

        // The goals by kind in the game's list order, each kind's lowest three as its tiers, as the Persona app sorts them in.
        private static void Read(List<GenericPersonalGoal> goals, List<string> reached)
        {
            Reached.Clear();
            for (int i = 0; i < reached.Count; i++)
            {
                if (reached[i] != null)
                {
                    Reached.Add(reached[i]);
                }
            }
            KindAt.Clear();
            ById.Clear();
            int kinds = 0, done = 0, total = 0;
            for (int i = 0; i < goals.Count; i++)
            {
                GenericPersonalGoal goal = goals[i];
                if (goal == null || goal.isHidden)
                {
                    continue;
                }
                total++;
                done += goal.identifier != null && Reached.Contains(goal.identifier) ? 1 : 0;
                if (goal.identifier != null)
                {
                    ById[goal.identifier] = goal;
                }
                string kind = !string.IsNullOrEmpty(goal.tierIdentifier) ? goal.tierIdentifier : !string.IsNullOrEmpty(goal.identifier) ? goal.identifier : goal.name;
                if (!KindAt.TryGetValue(kind, out int k))
                {
                    if (kinds >= MostKinds)
                    {
                        continue;
                    }
                    k = kinds++;
                    KindAt[kind] = k;
                    TierCount[k] = 0;
                }
                Insert(k, goal);
            }
            Done = done;
            Total = total;
            int shown = 0;
            for (int k = 0; k < kinds && shown < Most; k++)
            {
                GenericPersonalGoal now = OpenTier(k);
                if (now != null)
                {
                    Shown[shown] = now;
                    Titles[shown] = TitleOf(now);
                    Progress[shown] = now.HasProgress() ? ProgressOf(now) : null;
                    shown++;
                }
            }
            for (int i = shown; i < Most; i++)
            {
                Shown[i] = null;
                Titles[i] = Progress[i] = null;
            }
            Count = shown;
            Next = NextAfter(reached);
        }

        // A goal among its kind's tiers by its sort value, the highest dropped past the third (PersonalGoalTierGroup).
        private static void Insert(int k, GenericPersonalGoal goal)
        {
            float value = goal.GetSortValue();
            int at = TierCount[k];
            for (int t = 0; t < TierCount[k]; t++)
            {
                if (value < Tier[k, t].GetSortValue())
                {
                    at = t;
                    break;
                }
            }
            if (at >= Tiers)
            {
                return;
            }
            for (int t = Math.Min(TierCount[k], Tiers - 1); t > at; t--)
            {
                Tier[k, t] = Tier[k, t - 1];
            }
            Tier[k, at] = goal;
            TierCount[k] = Math.Min(Tiers, TierCount[k] + 1);
        }

        private static GenericPersonalGoal OpenTier(int k)
        {
            for (int t = 0; t < TierCount[k]; t++)
            {
                GenericPersonalGoal goal = Tier[k, t];
                if (goal.identifier == null || !Reached.Contains(goal.identifier))
                {
                    return goal;
                }
            }
            return null;
        }

        // The newest goal reached that the Persona app lists: its kind's next open tier, else the first goal now.
        private static string NextAfter(List<string> reached)
        {
            for (int i = reached.Count - 1; i >= 0; i--)
            {
                if (reached[i] == null || !ById.TryGetValue(reached[i], out GenericPersonalGoal goal))
                {
                    continue;
                }
                string kind = !string.IsNullOrEmpty(goal.tierIdentifier) ? goal.tierIdentifier : goal.identifier;
                GenericPersonalGoal next = KindAt.TryGetValue(kind, out int k) ? OpenTier(k) : null;
                return next != null ? TitleOf(next) : Count > 0 ? Titles[0] : null;
            }
            return Count > 0 ? Titles[0] : null;
        }

        // A goal's title as the game's Persona app writes it.
        private static string TitleOf(GenericPersonalGoal goal)
        {
            LanguageChangeEventDataHolder title = goal.GetTitle();
            if (string.IsNullOrEmpty(title.Key))   // a struct of the game's: never null
            {
                return null;
            }
            return title.Arguments != null ? LocalizorManager.GetLocalization(title.Key, title.Arguments) : LocalizorManager.GetLocalization(title.Key);
        }

        // A goal's progress from the game's own figures: a money figure alone ("$31,400"), any other "1 of 2".
        private static string ProgressOf(GenericPersonalGoal goal)
        {
            LanguageChangeEventDataHolder progress = goal.GetProgress();
            object args = progress.Arguments;
            if (args == null)
            {
                return null;
            }
            PropertyInfo[] props = Props(args.GetType());
            string current = Words(props[0] != null ? props[0].GetValue(args) : null);
            string amount = Words(props[1] != null ? props[1].GetValue(args) : null);
            if (_sign == null)
            {
                _sign = 0f.ToShortCurrencyFormat().Replace("0", string.Empty).Trim();   // "$0": the sign alone
            }
            return HudNoteRules.Progress(current, amount, _sign, UiText.T("reallife_ui_hud_goal_of"));
        }

        // The "current" and "amount" of a goal's figures (an anonymous type of the game's), looked up once a type.
        private static PropertyInfo[] Props(Type type)
        {
            if (!ArgsOf.TryGetValue(type, out PropertyInfo[] props))
            {
                props = new[] { type.GetProperty("current"), type.GetProperty("amount") };
                ArgsOf[type] = props;
            }
            return props;
        }

        private static string Words(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string s:
                    return s;
                case IConvertible n:
                    return n.ToSingle(CultureInfo.InvariantCulture).ToFormattedNumber();
            }
            return value.ToString();
        }
    }
}
