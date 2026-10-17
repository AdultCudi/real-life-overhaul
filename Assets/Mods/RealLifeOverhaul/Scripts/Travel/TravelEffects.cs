using System;
using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What travel does to the player through the game's systems; ModState keeps its moods out of the save.</summary>
    public static class TravelEffects
    {
        public const string Rested = "reallife_happinessmodifier_travel_rested";         // back from a holiday
        public const string Comfort = "reallife_happinessmodifier_travel_comfort";       // a night in a luxury room
        public const string RoughNight = "reallife_happinessmodifier_travel_roughnight"; // a bad night in a cheap one
        public const string Relaxed = "reallife_happinessmodifier_travel_relaxed";       // an afternoon doing nothing
        public const string JetLag = "reallife_happinessmodifier_travel_jetlag";
        public const string Burnout = "reallife_happinessmodifier_travel_burnout";       // too long without a break
        public const string Together = "reallife_happinessmodifier_travel_together";     // a trip with somebody
        public const string TradeFair = "reallife_happinessmodifier_travel_tradefair";

        private const float EnergyFloor = 15f;              // energy losses stop here, clear of fainting and the hospital
        private const float WorstEnergyMultiplier = 1.9f;   // the most the game multiplies energy spent by

        private static readonly (string type, int amount, int hours)[] Modifiers =
        {
            (Rested, 8, 72), (Comfort, 6, 24), (RoughNight, -5, 10), (Relaxed, 4, 6),
            (JetLag, -4, 36), (Burnout, -6, 6), (Together, 8, 48), (TradeFair, 4, 48)
        };

        /// <summary>Adds travel's modifier types to the game's registry, before ModState restores any from a save.</summary>
        public static void Register(BAModAPI.IModLogger logger)
        {
            try
            {
                var registry = GameContract.HappinessRegistry.Field?.GetValue(null) as Dictionary<string, HappinessModifier>;
                if (registry == null)
                {
                    return;
                }
                foreach ((string type, int amount, int hours) in Modifiers)
                {
                    if (registry.ContainsKey(type))
                    {
                        continue;
                    }
                    var modifier = ScriptableObject.CreateInstance<HappinessModifier>();
                    modifier.type = type;
                    modifier.amount = amount;
                    modifier.hoursDuration = hours;
                    modifier.maxHoursDuration = -1;
                    registry[type] = modifier;
                }
            }
            catch (Exception e)
            {
                logger?.Error(e);
            }
        }

        /// <summary>One of the mod's shared moods: -2 to 2, 0 for none.</summary>
        public static void Mood(int mood)
        {
            switch (mood)
            {
                case 2: ModState.AddModifier(ModState.MoodUpBig); break;
                case 1: ModState.AddModifier(ModState.MoodUp); break;
                case -1: ModState.AddModifier(ModState.MoodDown); break;
                case -2: ModState.AddModifier(ModState.MoodDownBig); break;
            }
        }

        /// <summary>Takes a price if the player has it; if not, false and the game's own "not enough money" notice.</summary>
        public static bool Pay(int amount, string transaction)
        {
            return amount <= 0 || GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, force: false, showNotification: true);
        }

        /// <summary>A bill owed whatever the balance (a stay on deposit), or money coming back.</summary>
        public static void Settle(int amount, string transaction)
        {
            if (amount != 0)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transaction), null, null, force: true);
            }
        }

        // An event's money: a loss never takes more than the player has.
        private static void Pocket(int amount, string transaction)
        {
            float money = SaveGameManager.Current.Money;
            if (amount < 0)
            {
                amount = -Mathf.Min(-amount, Mathf.FloorToInt(Mathf.Max(money, 0f)));
            }
            Settle(amount, transaction);
        }

        public static void Energy(float amount)
        {
            if (amount > 0f)
            {
                EnergyHelper.GenerateEnergy(amount);
            }
            else if (amount < 0f)
            {
                float room = (SaveGameManager.Current.Energy - EnergyFloor) / WorstEnergyMultiplier;
                float loss = Mathf.Clamp(-amount, 0f, Mathf.Max(room, 0f));
                if (loss > 0f)
                {
                    EnergyHelper.SpentEnergyOnce(loss);
                }
            }
        }

        public static void Feed(float amount)
        {
            GameInstance save = SaveGameManager.Current;
            if (amount > 0f && !save.gameVariables.disableEnergy)
            {
                EnergyHelper.SetCurrentHunger(Mathf.Min(save.Hunger + amount, 100f));
            }
        }

        public static void Apply(TravelEvent e)
        {
            if (e == null)
            {
                return;
            }
            Mood(e.Mood);
            if (e.Money != 0)
            {
                Pocket(e.Money, "reallife_transaction_travel_event");
            }
            Energy(e.Energy);
        }

        /// <summary>The game's ordered weekday of a day number: 1 Monday to 7 Sunday.</summary>
        public static int Weekday(int day) => (int)TimeHelper.GetDayOfWeek(day);

        /// <summary>The hotel's day: a night belongs to the day it began, so until 06:00 it is still yesterday.</summary>
        public static int StayDay() => TimeHelper.CurrentHour < 6 ? TimeHelper.CurrentDay - 1 : TimeHelper.CurrentDay;

        public static string DayName(int day) => TimeHelper.GetDayOfWeek(day).GetLocalization();
    }
}
