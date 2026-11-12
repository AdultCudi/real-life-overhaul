using System;
using System.Collections.Generic;
using System.Globalization;
using Buildings;
using Extensions;
using RealLifeOverhaul.Economy;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// The one file of insurance that touches the game: the clock, the player's cash through the mod's one booking call, the player's homes and the notices. Nothing per
    /// frame: read on a day's pass, a claim or a press.
    /// </summary>
    public static class InsuranceGame
    {
        public const string HomeKey = "reallife_transaction_ins_home";      // a home's premium
        public const string CarKey = "reallife_transaction_ins_car";        // a car's tier moved up (a premium and a renewal are the Garage's own line)
        private const string Residential = "ba:buildingtype_residential";

        private static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => Save != null;

        public static int Day => Save != null ? Save.Day : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        public static int MonthDays => GameCalendar.MonthDays;

        public static int Level => Prices.Level;

        /// <summary>The player's cash in whole money, never under nothing.</summary>
        public static long Cash => Save != null ? (long)Math.Floor(Mathf.Max(0f, Mathf.Min(Save.Money, 1e12f))) : 0L;

        /// <summary>Money out of the player's cash, whole money, through the mod's one call; false, nothing taken, if the cash does not cover it.</summary>
        public static bool Pay(long amount, string key)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (amount > Cash || amount > int.MaxValue / 2)
            {
                return false;
            }
            return BizBooks.Book(-(float)amount, new TransactionInfo(key), null, null, force: false, showNotification: false);
        }

        /// <summary>A home's address, written as the utilities, the Company and Brands write a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The player's homes today: residential buildings of their own, never another player's in a Going Public session.</summary>
        public static void Homes(List<HomeLook> into)
        {
            into.Clear();
            GameInstance save = Save;
            if (save == null || save.BuildingRegistrations == null)
            {
                return;
            }
            foreach (BuildingRegistration reg in save.BuildingRegistrations)
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasValidAddress)
                {
                    continue;
                }
                Building building = GameRegistrations.BuildingOf(reg);
                if (building == null || building.BuildingType != Residential)
                {
                    continue;
                }
                into.Add(new HomeLook { Place = Place(reg), Name = reg.Address.ToFormattedString(), District = building.Neighbourhood, Sqm = Mathf.Max(0, building.totalSqm) });
            }
        }

        // ---- words

        public static string Money(long amount) => ((float)amount).ToCurrencyFormat();

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action click = null)
        {
            Notifications.Show(type, key, data, 10f, null, click != null ? Guard.Wrap(click) : null, true, false);
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var d = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                d[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return d;
        }
    }
}
