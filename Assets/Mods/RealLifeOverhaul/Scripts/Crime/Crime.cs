using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What the crime system shares: a random source that does not allocate, money through the game's transactions, toasts.</summary>
    internal static class Crime
    {
        public static IModLogger Logger;

        /// <summary>UnityEngine.Random as a delegate made once: the dealing code takes one and must not allocate.</summary>
        public static readonly Func<float> Random01 = () => Random.value;

        public static void Fault(Exception e)
        {
            Logger?.Error(e);
        }

        public static bool IsNight
        {
            get
            {
                int hour = TimeHelper.CurrentHour;
                return hour >= 21 || hour < 5;
            }
        }

        /// <summary>What the player has in hand, never negative.</summary>
        public static float Cash => SaveGameManager.Current != null ? Mathf.Max(0f, SaveGameManager.Current.Money) : 0f;

        /// <summary>A police fine, charged in full even into the red (as the game allows), always with a toast naming the amount.</summary>
        public static void Fine(int amount, string messageKey)
        {
            if (amount <= 0 || SaveGameManager.Current == null)
            {
                return;
            }
            RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo("reallife_transaction_fine"), null, null, force: true);
            ShowAmount(NotificationType.Error, messageKey, amount, 10f);
        }

        /// <summary>A criminal takes a share of the cash in hand, within a floor and a cap at today's prices. Returns what was taken.</summary>
        public static int Rob(float share, int atLeast, int atMost, string transactionKey)
        {
            if (SaveGameManager.Current == null)
            {
                return 0;
            }
            float cash = Cash;
            int amount = Mathf.RoundToInt(Mathf.Clamp(cash * share, Prices.At(atLeast), Prices.At(atMost)) / 5f) * 5;
            amount = Mathf.Min(amount, WholeMoney.Floor(cash));
            if (amount <= 0)
            {
                return 0;
            }
            RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo(transactionKey), null, null, force: true);
            return amount;
        }

        /// <summary>
        /// A hold-up empties the till of the player's own shop (<see cref="CrimeRules.TillTake"/>). The game keeps one purse
        /// for the player and every business, so the money leaves it, booked at the shop's address and in the shop's day as
        /// theft (BizMan's Theft line). Returns what was taken; 0 and nothing booked if there was nothing to take.
        /// </summary>
        public static int RobTill(BuildingRegistration shop)
        {
            if (SaveGameManager.Current == null || shop == null)
            {
                return 0;
            }
            int amount = CrimeRules.TillTake(BusinessGame.DailyRevenue(shop), Cash, Prices.Level);
            if (amount <= 0)
            {
                return 0;
            }
            RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo("reallife_transaction_holdup_till"), null, shop.Address, force: true);
            BusinessGame.BookTheft(shop, amount);
            Logger?.Info("Crime: a hold-up emptied the till of " + shop.BusinessName + ": " + amount + " (a day's takings " + BusinessGame.DailyRevenue(shop).ToString("F0") + ").");
            return amount;
        }

        /// <summary>
        /// A charge for something already done that the player cannot refuse (a late fee, as a tow or a roadside call):
        /// charged in full even into the red, as the game allows, so it is always paid.
        /// </summary>
        public static void Charge(int amount, string transactionKey)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo(transactionKey), null, null, force: true);
            }
        }

        /// <summary>A cost paid only if it can be afforded in full (a repair, a locksmith); a choice the player can do without. False if it was not paid.</summary>
        public static bool Pay(int amount, string transactionKey)
        {
            return amount > 0 && SaveGameManager.Current != null
                   && RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo(transactionKey), null, null, force: false);
        }

        public static void Give(int amount, string transactionKey)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(amount, new TransactionInfo(transactionKey), null, null, force: true);
            }
        }

        /// <summary>A toast whose text has an {amount} in it.</summary>
        public static void ShowAmount(NotificationType type, string key, float amount, float seconds = 9f)
        {
            // A new dictionary each time: the game keeps it for as long as the toast is listed.
            var data = new Dictionary<string, string>(1) { { "amount", amount.ToCurrencyFormat() } };
            Notifications.Show(type, key, data, seconds);
        }

        /// <summary>A toast whose {key} is whatever the action is bound to now.</summary>
        public static void ShowKey(NotificationType type, string key, KeyAction action, float seconds = 8f, string name = null, string value = null)
        {
            var data = new Dictionary<string, string>(2) { { "key", Keys.Label(action) } };
            if (name != null)
            {
                data[name] = value ?? string.Empty;
            }
            Notifications.Show(type, key, data, seconds);
        }

        public static void Show(NotificationType type, string key, float seconds = 8f)
        {
            Notifications.Show(type, key, null, seconds);
        }

        /// <summary>The player's call led to an arrest.</summary>
        public static void ThankCaller()
        {
            ModState.AddModifier(ModState.Helped);
            Law.Reputation.Reported();
            Notifications.Show(NotificationType.Success, "reallife_crime_report_thanks", null, 8f);
        }

        /// <summary>The point a given distance short of a target, on the line from somewhere to it.</summary>
        public static Vector3 Short(Vector3 target, Vector3 from, float shortBy)
        {
            Vector3 back = from - target;
            back.y = 0f;
            return back.sqrMagnitude < shortBy * shortBy ? from : target + back.normalized * shortBy;
        }

        public static Vector3 RandomHeading()
        {
            float angle = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
