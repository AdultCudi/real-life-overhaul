using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What every part of the crime system shares: a random source that does not allocate, money moved through the game's
    /// own transaction call, the toasts that show it, and the fault log.
    /// </summary>
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
            GameManager.ChangeMoneySafe(-amount, new TransactionInfo("reallife_transaction_fine"), null, null, force: true);
            ShowAmount(NotificationType.Error, messageKey, amount, 10f);
        }

        /// <summary>
        /// Money taken by a criminal: a share of the cash in hand between a floor and a cap (at today's prices), never more
        /// than they have. Returns what was taken.
        /// </summary>
        public static int Rob(float share, int atLeast, int atMost, string transactionKey)
        {
            if (SaveGameManager.Current == null)
            {
                return 0;
            }
            float cash = Cash;
            int amount = Mathf.RoundToInt(Mathf.Clamp(cash * share, Prices.At(atLeast), Prices.At(atMost)) / 5f) * 5;
            amount = Mathf.Min(amount, Mathf.FloorToInt(cash));
            if (amount <= 0)
            {
                return 0;
            }
            GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transactionKey), null, null, force: true);
            return amount;
        }

        /// <summary>A cost paid only if it can be afforded in full (a repair, a locksmith). False if it was not paid.</summary>
        public static bool Pay(int amount, string transactionKey)
        {
            return amount > 0 && SaveGameManager.Current != null
                   && GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transactionKey), null, null, force: false);
        }

        public static void Give(int amount, string transactionKey)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transactionKey), null, null, force: true);
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
