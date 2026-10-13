using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using Parking.UndergroundParking;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game-facing half of the life event catalogue: reads the conditions events can ask
    /// for and carries out what a picked event says should happen.
    /// </summary>
    internal static class LifeEventRunner
    {
        private const string EmptyBusinessType = "ba:businesstype_empty";

        // Energy losses stop here, well clear of the game's fainting and hospital thresholds.
        private const float EnergyFloor = 15f;
        // The game multiplies energy spent by up to 1.25 (unhappy) x 1.5 (starving).
        private const float WorstEnergyMultiplier = 1.9f;
        private const float HungerFloor = 5f;

        public static LifeContext ReadContext()
        {
            GameInstance save = SaveGameManager.Current;
            bool driving = PlayerHelper.IsUsingVehicle;
            bool inside = BuildingManager.IsInsideBuilding;
            bool sick = ModState.IsSick;
            bool weekend = TimeHelper.IsWeekend;
            DayOfWeekOrdered day = TimeHelper.GetDayOfWeek();
            bool hasVehicle = save.VehicleInstances != null && save.VehicleInstances.Count > 0;

            Need flags = Need.None;
            flags |= driving ? Need.Driving : Need.NotDriving;
            if (!driving && inside) flags |= Need.Indoors;
            if (!driving && !inside && !UndergroundParkingManager.IsInsideParking) flags |= Need.OnFoot;
            if (RainHelper.AreRainDropsFalling) flags |= Need.Rain;
            if (!RainHelper.isRaining) flags |= Need.Dry;
            flags |= weekend ? Need.Weekend : Need.Weekday;
            if (day == DayOfWeekOrdered.Monday) flags |= Need.Monday;
            if (day == DayOfWeekOrdered.Friday) flags |= Need.Friday;
            if (day == DayOfWeekOrdered.Saturday) flags |= Need.Saturday;
            if (day == DayOfWeekOrdered.Sunday) flags |= Need.Sunday;
            flags |= sick ? Need.Sick : Need.Healthy;
            if (save.Energy < 35f) flags |= Need.Tired;
            if (save.Energy >= 60f) flags |= Need.Rested;
            if (save.Energy >= 30f) flags |= Need.EnergyReserve;
            if (save.Hunger < 35f) flags |= Need.Hungry;
            if (save.Hunger >= 60f) flags |= Need.Fed;
            if (save.Happiness >= 60f) flags |= Need.Happy;
            if (save.Happiness < 35f) flags |= Need.Unhappy;
            flags |= hasVehicle ? Need.HasVehicle : Need.NoVehicle;
            if (OwnsBusiness(save)) flags |= Need.HasBusiness;
            if (TimeHelper.CurrentDay - ModState.LastMomGiftDay >= 7) flags |= Need.MomGiftDue;
            if (PhoneContacts.Friend != null) flags |= Need.Friend;
            if (PhoneContacts.Mom != null) flags |= Need.Mom;
            if (!HudConfirm.isOpen) flags |= Need.CanPrompt;
            if (TimeHelper.CurrentDay <= 7) flags |= Need.Newcomer;
            if (TimeHelper.CurrentDay >= 15) flags |= Need.Veteran;

            return new LifeContext { Flags = flags, Hour = TimeHelper.CurrentHour, Money = save.Money };
        }

        // Same test the game uses when it counts the player's businesses.
        private static bool OwnsBusiness(GameInstance save)
        {
            if (save.BuildingRegistrations == null)
            {
                return false;
            }
            foreach (BuildingRegistration registration in save.BuildingRegistrations)
            {
                if (registration != null && GoingPublic.Mine(registration) && registration.businessTypeName != EmptyBusinessType
                    && !string.IsNullOrEmpty(registration.BusinessName))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Run(LifeEvent lifeEvent)
        {
            if (!lifeEvent.IsChoice)
            {
                Apply(lifeEvent, Pick(lifeEvent.Outcomes));
                return;
            }

            // The price and the sum the prompt's body quotes, at today's prices, as charged (the answers name no figure:
            // the game's box takes a plain key for them).
            int cost = lifeEvent.CostNow;
            var figures = new { cost = ((float)cost).ToCurrencyFormat(), sum = ((float)lifeEvent.QuoteNow).ToCurrencyFormat() };
            HudConfirm.Show(new LanguageChangeEventDataHolder { Key = lifeEvent.HeaderKey },
                new LanguageChangeEventDataHolder { Key = lifeEvent.BodyKey, Arguments = figures },
                () => Guarded(() =>
                {
                    if (cost > 0 && !GameManager.ChangeMoneySafe(-cost,
                            new TransactionInfo(LifeEvent.TransactionPrefix + lifeEvent.CostTxn), null, null, force: false, showNotification: true))
                    {
                        return;
                    }
                    Apply(lifeEvent, Pick(lifeEvent.Yes));
                }),
                () => Guarded(() => Apply(lifeEvent, Pick(lifeEvent.No))),
                lifeEvent.ConfirmKey, lifeEvent.CancelKey, allowConfirmationSkip: false);
        }

        // Prompt answers arrive later, from the game's UI: the save may be gone by then, and an
        // exception must not escape into the game's button handler.
        private static void Guarded(Action action)
        {
            if (LifeEvents.Instance == null || !ModState.Loaded || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                action();
            }
            catch (Exception e)
            {
                LifeEvents.Logger?.Error(e);
            }
        }

        private static Outcome Pick(Outcome[] outcomes)
        {
            if (outcomes.Length == 0)
            {
                return null;
            }
            float total = 0f;
            foreach (Outcome o in outcomes)
            {
                total += o.Odds;
            }
            float roll = Random.value * total;
            foreach (Outcome o in outcomes)
            {
                roll -= o.Odds;
                if (roll <= 0f)
                {
                    return o;
                }
            }
            return outcomes[outcomes.Length - 1];
        }

        private static void Apply(LifeEvent lifeEvent, Outcome outcome)
        {
            if (outcome == null)
            {
                return;
            }
            LifeEvents host = LifeEvents.Instance;
            GameInstance save = SaveGameManager.Current;

            int amount = RollMoney(outcome, save.Money);
            if (amount != 0)
            {
                LifeEvents.ChangeMoney(amount, LifeEvent.TransactionPrefix + outcome.Txn);
                if (amount > 0 && outcome.Via == Via.Mom)
                {
                    ModState.LastMomGiftDay = TimeHelper.CurrentDay;
                }
            }

            if (outcome.Energy > 0f)
            {
                EnergyHelper.GenerateEnergy(outcome.Energy);
            }
            else if (outcome.Energy < 0f)
            {
                EnergyHelper.SpentEnergyOnce(SafeEnergyLoss(-outcome.Energy));
            }

            // Set after the energy change: spending energy also takes a little hunger with it.
            if (outcome.Hunger != 0f && !save.gameVariables.disableEnergy)
            {
                float hunger = Mathf.Clamp(save.Hunger + outcome.Hunger, Mathf.Min(save.Hunger, HungerFloor), 100f);
                EnergyHelper.SetCurrentHunger(hunger);
            }

            string mood = ModifierFor(outcome.Mood);
            if (mood != null)
            {
                ModState.AddModifier(mood);
            }
            if (outcome.Cures)
            {
                host.Cure(null);
            }

            // What the outcome's own {amount} was, or else what the player just paid for the choice.
            int shown = amount != 0 ? Mathf.Abs(amount) : lifeEvent.CostNow;
            var data = new Dictionary<string, string> { { "amount", ((float)shown).ToCurrencyFormat() } };
            string key = lifeEvent.TextKey(outcome);

            if (outcome.HurtHours > 0)
            {
                host.Injure(outcome.HurtHours, SafeEnergyLoss(outcome.HurtEnergy), key);   // shows the text itself
            }
            else if (outcome.Via == Via.Toast)
            {
                Notifications.Show(ToastType(outcome.Tone), key, data, 10f);
            }
            else
            {
                Contact contact = outcome.Via == Via.Mom ? PhoneContacts.Mom : PhoneContacts.Friend;
                contact?.SendMessage(new TextMessage(key, data, false, true), notify: true, sendNotificationInstantly: true);
            }

            if (outcome.SickMax > 0 && !ModState.IsSick)
            {
                host.MakeSick(Random.Range(outcome.SickMin, outcome.SickMax + 1));
            }
        }

        private static int RollMoney(Outcome outcome, float money)
        {
            if (!outcome.HasMoney)
            {
                return 0;
            }
            int amount;
            // (The catalogue's figures are at the start's prices: what comes is at today's, Core/Prices.)
            if (outcome.MoneyPct != 0f)
            {
                float share = Mathf.Clamp(Mathf.Max(money, 0f) * Mathf.Abs(outcome.MoneyPct), Prices.At((float)outcome.MoneyMin), Prices.At((float)outcome.MoneyMax));
                amount = Mathf.RoundToInt(share / 10f) * 10 * (outcome.MoneyPct < 0f ? -1 : 1);
            }
            else if (outcome.MoneyMin == outcome.MoneyMax)
            {
                amount = Prices.At(outcome.MoneyMin);
            }
            else
            {
                amount = Prices.At(Mathf.RoundToInt(Random.Range(outcome.MoneyMin, outcome.MoneyMax + 1) / 5f) * 5);
            }
            // Never into debt, whatever happened to the player's money since the event was picked.
            return amount < 0 ? -Mathf.Min(-amount, Mathf.FloorToInt(Mathf.Max(money, 0f))) : amount;
        }

        private static float SafeEnergyLoss(float wanted)
        {
            float room = (SaveGameManager.Current.Energy - EnergyFloor) / WorstEnergyMultiplier;
            return Mathf.Clamp(wanted, 0f, Mathf.Max(room, 0f));
        }

        private static NotificationType ToastType(Tone tone)
        {
            switch (tone)
            {
                case Tone.Good: return NotificationType.Success;
                case Tone.Warn: return NotificationType.Warning;
                case Tone.Bad: return NotificationType.Error;
                default: return NotificationType.Info;
            }
        }

        private static string ModifierFor(Mood mood)
        {
            switch (mood)
            {
                case Mood.Up: return ModState.MoodUp;
                case Mood.UpBig: return ModState.MoodUpBig;
                case Mood.Down: return ModState.MoodDown;
                case Mood.DownBig: return ModState.MoodDownBig;
                case Mood.Lucky: return ModState.Lucky;
                case Mood.Generous: return ModState.Generous;
                case Mood.Robbed: return ModState.Robbed;
                case Mood.Social: return ModState.Social;
                case Mood.Family: return ModState.Family;
                case Mood.Helped: return ModState.Helped;
                default: return null;
            }
        }
    }
}
