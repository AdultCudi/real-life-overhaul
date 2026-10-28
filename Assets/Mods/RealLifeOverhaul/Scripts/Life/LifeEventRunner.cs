using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Entities;
using Extensions;
using Helpers;
using Localizor.LanguageChangeEvent;
using Parking.UndergroundParking;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The game's side of the life events: reads the conditions events ask for and carries out a picked event.</summary>
    internal static class LifeEventRunner
    {
        private const string EmptyBusinessType = "ba:businesstype_empty";

        private const float EnergyFloor = 15f;              // points: losses stop well clear of fainting and the hospital
        private const float WorstEnergyMultiplier = 1.9f;   // the game's most on energy spent: 1.25 unhappy times 1.5 starving
        private const float HungerFloor = 5f;               // points

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
            flags |= If(!driving && inside, Need.Indoors);
            flags |= If(!driving && !inside && !UndergroundParkingManager.IsInsideParking, Need.OnFoot);
            flags |= If(RainHelper.AreRainDropsFalling, Need.Rain);
            flags |= If(!RainHelper.isRaining, Need.Dry);
            flags |= weekend ? Need.Weekend : Need.Weekday;
            flags |= If(day == DayOfWeekOrdered.Monday, Need.Monday);
            flags |= If(day == DayOfWeekOrdered.Friday, Need.Friday);
            flags |= If(day == DayOfWeekOrdered.Saturday, Need.Saturday);
            flags |= If(day == DayOfWeekOrdered.Sunday, Need.Sunday);
            flags |= sick ? Need.Sick : Need.Healthy;
            flags |= If(save.Energy < 35f, Need.Tired);
            flags |= If(save.Energy >= 60f, Need.Rested);
            flags |= If(save.Energy >= 30f, Need.EnergyReserve);
            flags |= If(save.Hunger < 35f, Need.Hungry);
            flags |= If(save.Hunger >= 60f, Need.Fed);
            flags |= If(save.Happiness >= 60f, Need.Happy);
            flags |= If(save.Happiness < 35f, Need.Unhappy);
            flags |= hasVehicle ? Need.HasVehicle : Need.NoVehicle;
            flags |= If(OwnsBusiness(save), Need.HasBusiness);
            if (LifeAccounts.HasHome())
            {
                flags |= Need.HasHome;
            }
            flags |= If(TimeHelper.CurrentDay - ModState.LastMomGiftDay >= 7, Need.MomGiftDue);
            flags |= If(PhoneContacts.Friend != null, Need.Friend);
            flags |= If(PhoneContacts.Mom != null, Need.Mom);
            flags |= If(!HudConfirm.isOpen, Need.CanPrompt);
            flags |= If(TimeHelper.CurrentDay <= 7, Need.Newcomer);
            flags |= If(TimeHelper.CurrentDay >= 15, Need.Veteran);

            return new LifeContext { Flags = flags, Hour = TimeHelper.CurrentHour, Money = save.Money };
        }

        private static Need If(bool holds, Need need) => holds ? need : Need.None;

        // The test the game uses when it counts the player's businesses.
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

            // The price and the sum the body quotes, at today's prices as charged; the answers take plain keys.
            int cost = lifeEvent.CostNow;
            var figures = new { cost = ((float)cost).ToCurrencyFormat(), sum = ((float)lifeEvent.QuoteNow).ToCurrencyFormat() };
            GameConfirm.Show(new LanguageChangeEventDataHolder { Key = lifeEvent.HeaderKey },
                new LanguageChangeEventDataHolder { Key = lifeEvent.BodyKey, Arguments = figures },
                () => Guarded(() =>
                {
                    if (cost > 0 && !RealLifeOverhaul.Economy.BizBooks.Book(-cost,
                            new TransactionInfo(LifeEvent.TransactionPrefix + lifeEvent.CostTxn), null, null, force: false, showNotification: true))
                    {
                        return;
                    }
                    Apply(lifeEvent, Pick(lifeEvent.Yes));
                }),
                () => Guarded(() => Apply(lifeEvent, Pick(lifeEvent.No))),
                lifeEvent.ConfirmKey, lifeEvent.CancelKey, allowConfirmationSkip: false);
        }

        // Answers arrive later from the game's UI: the save may be gone, and nothing may escape into its button handler.
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

            // After the energy change: spending energy also takes a little hunger.
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

            // The outcome's own {amount}, or else what the player just paid for the choice.
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
            // The catalogue's figures are at the start's prices; what comes is at today's.
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
            // Never into debt, whatever happened to the money since the event was picked, and never past what the event is
            // written with (LifeMoneyRules): a purse past an int once read as int.MinValue here and took 2.1 billion.
            return LifeMoneyRules.Settle(amount, money, outcome.Worst, Prices.Level);
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
