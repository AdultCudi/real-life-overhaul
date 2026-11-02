using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A personal goal reached pays (the option economy_goal_rewards, on by default): <see cref="GoalRewardRules"/> sizes it
    /// by the goal's tier among the goals of its kind, at the city's prices, booked as the mod's own income ("Personal goal
    /// reward"). The goals reached are the game's own list (GameInstance.completedPersonalGoals, saved): what it held when
    /// the save was read, or while the option was off, pays nothing; each goal added after pays once; a goal the Persona app
    /// hides pays nothing. The HUD's goal card shows the amount. Rivals have no goals. Cost: a count compared a second.
    /// </summary>
    public sealed partial class EconomyDirector
    {
        private const string GoalRewardKey = "reallife_transaction_goal_reward";

        private static List<string> _goalsList;   // the save's list of goals reached that was counted...
        private static int _goalsCounted;         // ...and how much of it
        private static readonly float[] KindValues = new float[16];

        /// <summary>The last reward paid this session and when (Time.unscaledTime); 0 and long ago for none.</summary>
        public static float LastGoalReward { get; private set; }
        public static float LastGoalRewardAt { get; private set; } = -100f;

        // Once a second: each goal reached since the last look paid while the economy runs, else only counted.
        private void KeepGoalRewards(bool pay)
        {
            try
            {
                PayGoals(pay);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the personal goals' rewards");
                _goalsList = null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PayGoals(bool pay)
        {
            List<string> reached = Save != null ? Save.completedPersonalGoals : null;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            if (reached == null || game == null || game.personalGoals == null)
            {
                return;
            }
            if (!ReferenceEquals(reached, _goalsList) || reached.Count < _goalsCounted)
            {
                _goalsList = reached;   // a save read: what it holds was reached before
                _goalsCounted = reached.Count;
                return;
            }
            for (int i = _goalsCounted; i < reached.Count; i++)
            {
                if (pay && Settings.EconomyGoalRewards && reached.IndexOf(reached[i]) == i)
                {
                    Reward(game.personalGoals, reached[i]);
                }
            }
            _goalsCounted = reached.Count;
        }

        // A goal reached: its tier among its kind's goals the Persona app lists, its reward at the city's prices, booked.
        private static void Reward(List<GenericPersonalGoal> goals, string identifier)
        {
            GenericPersonalGoal goal = null;
            for (int i = 0; i < goals.Count && goal == null; i++)
            {
                if (goals[i] != null && goals[i].identifier == identifier)
                {
                    goal = goals[i];
                }
            }
            if (goal == null || goal.isHidden)
            {
                return;
            }
            string kind = !string.IsNullOrEmpty(goal.tierIdentifier) ? goal.tierIdentifier : goal.identifier;
            int n = 0;
            for (int i = 0; i < goals.Count && n < KindValues.Length; i++)
            {
                GenericPersonalGoal other = goals[i];
                if (other != null && !other.isHidden && (other.tierIdentifier == kind || (string.IsNullOrEmpty(other.tierIdentifier) && other.identifier == kind)))
                {
                    KindValues[n++] = other.GetSortValue();
                }
            }
            int tier = GoalRewardRules.Tier(goal.GetSortValue(), KindValues, n);
            int amount = Prices.At(GoalRewardRules.Reward(tier));
            if (amount <= 0 || !BizBooks.Book(amount, new TransactionInfo(GoalRewardKey), null, null, force: true))
            {
                return;
            }
            LastGoalReward = amount;
            LastGoalRewardAt = Time.unscaledTime;
            Logger?.Info("Economy: personal goal " + identifier + " reached (tier " + (tier + 1) + " of its kind): " + amount + " paid.");
        }
    }
}
