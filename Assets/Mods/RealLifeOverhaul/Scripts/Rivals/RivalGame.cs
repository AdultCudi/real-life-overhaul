using System;
using System.Collections.Generic;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// Everything the rival tycoons read from the game or do to it: the day, the player's worth and businesses, the price
    /// level; money as the game's own transactions, in pieces a float moves exactly; the game's notifications. Moves
    /// themselves go through <see cref="RivalBridges"/>. Asked on the day, a click or a test; never per frame.
    /// </summary>
    public static class RivalGame
    {
        public const string RentKey = "reallife_transaction_rival_rent";
        public const string CounterKey = "reallife_transaction_rival_counter";
        public const string BreakUpKey = "reallife_transaction_rival_breakup";

        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);

        public static GameInstance Save => SaveGameManager.Current;
        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        public static long Cash => SaveGameManager.Current != null ? (long)Math.Floor(Math.Max(0.0, SaveGameManager.Current.Money)) : 0L;

        /// <summary>What the game is today, into the world given.</summary>
        public static void Read(RivalWorld into)
        {
            into.Clear();
            if (Save == null)
            {
                return;
            }
            into.Worth = Worth();
            into.Level = Prices.Level;
            into.FirstFrom = RivalDirector.FirstFrom;
            into.Cycle = RivalBridges.Cycle != null ? RivalCatalogue.Clamp(RivalBridges.Cycle(), 0, 3) : 1;
            into.Market = RivalBridges.Ask(RivalBridges.MarketHere);
            into.Listed = into.Market && RivalBridges.Ask(RivalBridges.Listed);
            into.CompanyDays = into.Listed && RivalBridges.CompanyDays != null ? RivalBridges.CompanyDays() : -1;
            into.YearDays = RivalBridges.YearDays != null ? Math.Max(8, RivalBridges.YearDays()) : 365;
            into.Staff = RivalBridges.Ask(RivalBridges.StaffOn) && RivalBridges.Offer != null;
            into.Issues = RivalBridges.Ask(RivalBridges.IssuesOn);
            into.Court = RivalBridges.Ask(RivalBridges.CourtOn) && RivalBridges.Sue != null;
            into.Paper = RivalBridges.Ask(RivalBridges.PaperOn);
            Books.Businesses(Regs);
            for (int i = 0; i < Regs.Count; i++)
            {
                BuildingRegistration reg = Regs[i];
                into.Shops.Add(new RivalShop
                {
                    Place = BusinessGame.Place(reg),
                    Name = Name(reg),
                    District = GameRegistrations.District(reg) ?? string.Empty,
                    Type = reg.businessTypeName ?? string.Empty,
                    Week = (long)Math.Round(BusinessGame.DailyRevenue(reg) * 7.0),
                    Rented = !reg.BuildingOwnedByPlayer,
                    Rent = reg.BuildingOwnedByPlayer ? 0 : Mathf.Max(0, Mathf.RoundToInt(reg.RentPerDay))
                });
            }
            Regs.Clear();
        }

        /// <summary>The player's net worth: the Finance app's figure through the wiring, else the game's own, else their cash.</summary>
        public static long Worth()
        {
            try
            {
                if (RivalBridges.Worth != null)
                {
                    return RivalBridges.Worth();
                }
                return (long)Math.Floor(PlayerHelper.GetPersonalWealth().CurrentWealth);
            }
            catch (Exception)
            {
                return Cash;
            }
        }

        public static string PlayerName()
        {
            try
            {
                return PlayerHelper.CharacterData != null && !string.IsNullOrEmpty(PlayerHelper.CharacterData.name) ? PlayerHelper.CharacterData.name : RivalText.T("reallife_rival_you");
            }
            catch (Exception)
            {
                return RivalText.T("reallife_rival_you");
            }
        }

        public static string Name(BuildingRegistration reg)
        {
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : "?";
        }

        /// <summary>A district by the game's own key, in words.</summary>
        public static string DistrictName(string district)
        {
            return string.IsNullOrEmpty(district) ? RivalText.T("reallife_rival_district_none") : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>The player's business at a place now, or null (sold, closed, another player's).</summary>
        public static BuildingRegistration Business(string place)
        {
            return string.IsNullOrEmpty(place) ? null : BusinessGame.Find(place);
        }

        /// <summary>A rent rise at the player's business, taken only if the cash is there (an unpaid rise is not owed). True if paid.</summary>
        public static bool ChargeRent(BuildingRegistration reg, long amount, string rival)
        {
            if (amount <= 0 || reg == null || Save == null)
            {
                return false;
            }
            var info = new TransactionInfo(RentKey, new Dictionary<string, string>(1) { { "name", rival ?? string.Empty } });
            return GameManager.ChangeMoneySafe(-(float)Math.Min(amount, 1000000000L), info, null, reg.Address, force: false);
        }

        /// <summary>A counter's cost, taken in pieces if the cash is there for all of it; false, with nothing taken, if not.</summary>
        public static bool Take(long amount, string key, string name)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Save == null || Cash < amount)
            {
                return false;
            }
            Move(-1, amount, key, name);
            return true;
        }

        public static void Give(long amount, string key, string name)
        {
            if (amount > 0 && Save != null)
            {
                Move(1, amount, key, name);
            }
        }

        private static void Move(int sign, long amount, string key, string name)
        {
            for (long left = amount; left > 0;)
            {
                GameManager.ChangeMoneySafe(sign * InvestRules.Piece(ref left), new TransactionInfo(key, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } }), null, null, true);
            }
        }

        /// <summary>A notification of the game's own; a click does what <paramref name="click"/> does, behind the guard.</summary>
        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action click)
        {
            Notifications.Show(type, key, data, 12f, null, click != null ? Guard.Wrap(click) : null, true, false);
        }
    }
}
