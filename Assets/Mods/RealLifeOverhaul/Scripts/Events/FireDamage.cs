using System;
using System.Collections.Generic;
using Extensions;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// What a fire costs when one of the player's own businesses burned, through the game's own means only: closed for some
    /// hours with its "temporarily closed" switch and reopened by it, and a repair bill as an ordinary transaction. Both
    /// scale with how soon water was on it, less for security, a warning and a 911 call; stock is never touched, and a
    /// save's only business is spared in its first ten days.
    /// </summary>
    internal static class FireDamage
    {
        internal const float RepairLeast = 350f, RepairMost = 1800f;   // a fire's repair bill, at the start's prices
        private const int GraceDays = 10;                               // days

        /// <summary>The business a fire closed, as "street|number", and the game minute it reopens; null when none. Saved in modData.</summary>
        internal static string ClosedKey;
        internal static float ReopenAt;

        public static void Apply(BuildingRegistration business, float secondsToWater, bool warned, bool called)
        {
            try
            {
                var save = SaveGameManager.Current;
                if (save == null || business == null)
                {
                    return;
                }
                List<BuildingRegistration> own = BuildingHelper.GetPlayerBuildingRegistrations(GoingPublic.Mine);
                int businesses = 0;
                for (int i = 0; own != null && i < own.Count; i++)
                {
                    businesses += own[i] != null && own[i].HasEstablishedBusiness ? 1 : 0;
                }
                if (businesses <= 1 && TimeHelper.CurrentDay <= GraceDays)
                {
                    Notifications.Show(NotificationType.Info, "reallife_fire_own_spared", null, 10f);
                    EventDirector.Logger?.Info("Fire: the player's only business, in the save's first days: no closure and no bill.");
                    return;
                }
                // 30 s to water is as good as it gets, two minutes as bad.
                float slow = Mathf.InverseLerp(30f, 120f, secondsToWater);
                float security = business.securityLevelPercentage > 1f ? business.securityLevelPercentage / 100f : business.securityLevelPercentage;
                float relief = 1f - Mathf.Clamp01(security) * 0.4f - (warned ? 0.15f : 0f) - (called ? 0.1f : 0f);
                int hours = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(3f, 10f, slow) * relief), 2, 10);
                int bill = Mathf.RoundToInt(Prices.At(Mathf.Lerp(RepairLeast, RepairMost, slow) * relief) / 10f) * 10;   // at today's prices
                GameManager.ChangeMoneySafe(-bill, new TransactionInfo("reallife_transaction_fire_repair"), null, business.Address, force: true);
                FireLosses.Raise(business, bill);   // for whoever insures it
                bool closedByUs = false;
                if (!business.temporarilyClosed && ClosedKey == null)
                {
                    business.TemporarilyClose(true);
                    ClosedKey = business.StreetName + "|" + business.StreetNumber;
                    ReopenAt = TimeHelper.NowInMinutes() + hours * 60f;
                    closedByUs = true;
                }
                var data = new Dictionary<string, string>(2) { { "amount", ((float)bill).ToCurrencyFormat() }, { "hours", hours.ToString() } };
                Notifications.Show(NotificationType.Error, closedByUs ? "reallife_fire_own_bill" : "reallife_fire_own_bill_only", data, 14f);
                EventDirector.Logger?.Info(FormattableString.Invariant(
                    $"Fire: the player's business at {business.StreetName} {business.StreetNumber}: water after {secondsToWater:F0} s, security {security:P0}, warned {warned}, ")
                                           + $"called {called}: repair bill {bill}, "
                                           + (closedByUs ? $"closed for {hours} h (the game's own temporary closure)." : "not closed (it already was, or another closure is running)."));
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
            }
        }

        /// <summary>Once a game hour: reopens the business a fire closed when its time is up.</summary>
        public static void Hourly()
        {
            if (ClosedKey == null || TimeHelper.NowInMinutes() < ReopenAt)
            {
                return;
            }
            string key = ClosedKey;
            ClosedKey = null;
            ReopenAt = 0f;
            try
            {
                List<BuildingRegistration> own = BuildingHelper.GetPlayerBuildingRegistrations(GoingPublic.Mine);
                for (int i = 0; own != null && i < own.Count; i++)
                {
                    BuildingRegistration business = own[i];
                    if (business != null && business.StreetName + "|" + business.StreetNumber == key)
                    {
                        if (business.temporarilyClosed)
                        {
                            business.TemporarilyClose(false);
                            Notifications.Show(NotificationType.Success, "reallife_fire_own_reopened", null, 9f);
                        }
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
            }
        }
    }
}
