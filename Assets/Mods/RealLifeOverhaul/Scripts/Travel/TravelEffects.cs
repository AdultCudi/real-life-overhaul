using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.DayNightCycle;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What travel does to the player, all of it through the game's own systems: money (the
    /// game's transactions), hunger and energy (EnergyHelper), and happiness modifiers. The
    /// modifier types are travel's own; they are registered the way ModState registers the
    /// mod's others and carry its prefix, so ModState takes them out of the save while it is
    /// written and puts them back on load: a save never holds a modifier the base game does
    /// not know.
    /// </summary>
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

        // Energy losses stop here, clear of the game's fainting and hospital thresholds; the game multiplies energy spent by up to 1.9.
        private const float EnergyFloor = 15f;
        private const float WorstEnergyMultiplier = 1.9f;

        private static readonly (string type, int amount, int hours)[] Modifiers =
        {
            (Rested, 8, 72), (Comfort, 6, 24), (RoughNight, -5, 10), (Relaxed, 4, 6),
            (JetLag, -4, 36), (Burnout, -6, 6), (Together, 8, 48), (TradeFair, 4, 48)
        };

        /// <summary>
        /// Adds travel's modifier types to the game's registry. Called when the travel component
        /// is made (before ModState reads a save, so modifiers stashed in it find their types)
        /// and again when the save is ready. False if the registry could not be reached: mood
        /// effects are then left out, everything else works.
        /// </summary>
        public static bool Register(BAModAPI.IModLogger logger)
        {
            try
            {
                FieldInfo field = typeof(HappinessHelper).GetField("Modifiers", BindingFlags.NonPublic | BindingFlags.Static);
                var registry = field?.GetValue(null) as Dictionary<string, HappinessModifier>;
                if (registry == null)
                {
                    return false;
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
                return true;
            }
            catch (Exception e)
            {
                logger?.Error(e);
                return false;
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

        /// <summary>Takes a price if the player has it. False (and the game's own "not enough money" toast) if not.</summary>
        public static bool Pay(int amount, string transaction)
        {
            return amount <= 0 || GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, force: false, showNotification: true);
        }

        /// <summary>A bill that is owed whatever the balance (a stay on deposit), or money coming back.</summary>
        public static void Settle(int amount, string transaction)
        {
            if (amount != 0)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transaction), null, null, force: true);
            }
        }

        /// <summary>Small money from an event: a loss never takes more than the player has.</summary>
        public static int Pocket(int amount, string transaction)
        {
            float money = SaveGameManager.Current.Money;
            if (amount < 0)
            {
                amount = -Mathf.Min(-amount, Mathf.FloorToInt(Mathf.Max(money, 0f)));
            }
            Settle(amount, transaction);
            return amount;
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

        /// <summary>Food: hunger goes up by an amount, never past full.</summary>
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

        /// <summary>The game's ordered weekday (1 Monday to 7 Sunday) of a day number.</summary>
        public static int Weekday(int day)
        {
            return (int)TimeHelper.GetDayOfWeek(day);
        }

        /// <summary>
        /// The hotel's day: a night belongs to the day it began on, so until six in the morning
        /// it is still "yesterday" at the desk.
        /// </summary>
        public static int StayDay()
        {
            return TimeHelper.CurrentHour < 6 ? TimeHelper.CurrentDay - 1 : TimeHelper.CurrentDay;
        }

        public static string DayName(int day)
        {
            return TimeHelper.GetDayOfWeek(day).GetLocalization();
        }

        public static Timestamp At(int day, int hour)
        {
            return new Timestamp(day, hour, 0f);
        }
    }
}
