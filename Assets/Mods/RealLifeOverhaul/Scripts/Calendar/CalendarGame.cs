using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UI.Load;
using Vehicles;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>The only part of the calendar that touches the game: its clock and the save's own dated things, read only.</summary>
    public static class CalendarGame
    {
        public const string SourceId = "game";
        private const int RowTaxYear = 1, RowFurniture = 2, RowVehicle = 3, RowBirthday = 4;

        public static int Today => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        /// <summary>No city to read: no save, a load under way, or the mod's state not read yet.</summary>
        public static bool Loading()
        {
            return SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded;
        }

        public static readonly ICalendarSource Source = new GameSource();

        private sealed class GameSource : ICalendarSource
        {
            public string Id => SourceId;
            public bool Available => SaveGameManager.Current != null;

            public void Collect(int fromDay, int toDay, CalendarList into)
            {
                TaxYear(fromDay, toDay, into);
                Furniture(into);
                Vehicles(into);
                Birthday(fromDay, toDay, into);
                BuildingRent(into);
            }
        }

        // The game closes the tax year on each day that is a whole number of years (TaxHelper.RunTaxPeriodBoundary).
        private static void TaxYear(int fromDay, int toDay, CalendarList into)
        {
            int year = GameCalendar.DaysPerYear;
            for (int day = MonthViewRules.FirstFrom(year, year, Math.Max(1, fromDay)); day <= toDay; day += year)
            {
                CalendarEntry e = into.Add(SourceId, RowTaxYear, day, CalendarGroup.Money, day);
                if (e == null)
                {
                    continue;
                }
                e.Sign = -1;
                e.Title = CalendarText.T("reallife_calendar_tax_year_title");
                e.Value = CalendarText.T("reallife_calendar_tax_year_value", "amount", CalendarText.Money(TaxHelper.MinimumIncomeForTaxes));
            }
        }

        // Furniture and equipment bought for delivery: paid when it comes (FurnitureDeliveryHelper.PayDelivery).
        private static void Furniture(CalendarList into)
        {
            List<FurnitureDeliveryContract> deliveries = SaveGameManager.Current.FurnitureDeliveryContracts;
            for (int i = 0; deliveries != null && i < deliveries.Count; i++)
            {
                FurnitureDeliveryContract d = deliveries[i];
                if (d == null || d.toAddress == null || !Mine(d.toAddress))
                {
                    continue;
                }
                CalendarEntry e = into.Add(SourceId, RowFurniture, i, CalendarGroup.Money, d.dayOfDelivery);
                if (e == null)
                {
                    continue;
                }
                e.Start = Math.Max(0, Math.Min(23, d.hourOfDelivery)) * 60;
                e.Sign = -1;
                e.Amount = Math.Max(0f, Price(d));
                e.Title = CalendarText.T("reallife_calendar_furniture_title", "address", d.toAddress.ToFormattedString());
                e.Value = e.Amount > 0f ? CalendarText.Money(e.Amount) : null;
            }
        }

        // The game sums the items with a query of its own.
        private static float Price(FurnitureDeliveryContract d)
        {
            try
            {
                return d.itemsToDeliver != null ? d.TotalDeliveryPrice : d.deliveryFee;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        // A vehicle bought for delivery: it is paid for already, and comes on its day and hour.
        private static void Vehicles(CalendarList into)
        {
            List<VehicleDeliveryContract> deliveries = SaveGameManager.Current.vehicleDeliveryContracts;
            for (int i = 0; deliveries != null && i < deliveries.Count; i++)
            {
                VehicleDeliveryContract d = deliveries[i];
                if (d == null || d.deliveryAddress == null || !Mine(d.deliveryAddress))
                {
                    continue;
                }
                CalendarEntry e = into.Add(SourceId, RowVehicle, i, CalendarGroup.Time, d.deliveryDay);
                if (e == null)
                {
                    continue;
                }
                e.Start = Math.Max(0, Math.Min(23, d.deliveryHour)) * 60;
                e.Title = CalendarText.T("reallife_calendar_vehicle_title", "vehicle", string.IsNullOrEmpty(d.vehicleTypeName) ? string.Empty : d.vehicleTypeName.GetLocalization(),
                    "address", d.deliveryAddress.ToFormattedString());
                e.Value = CalendarText.Clock(e.Start);
            }
        }

        // Never another player's building in a shared city.
        private static bool Mine(Address address)
        {
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(address);
            return reg != null && GoingPublic.Mine(reg);
        }

        private static void Birthday(int fromDay, int toDay, CalendarList into)
        {
            GameInstance save = SaveGameManager.Current;
            if (save.gameVariables != null && save.gameVariables.disableAging)
            {
                return;
            }
            CharacterData data = Player(save);
            int year = GameCalendar.DaysPerYear, today = save.Day;
            if (data == null || year <= 0)
            {
                return;
            }
            int first = MonthViewRules.OwnBirthdayFrom(today, data.ageInDays, year);
            for (int day = MonthViewRules.FirstFrom(first, year, fromDay); day <= toDay; day += year)
            {
                CalendarEntry e = into.Add(SourceId, RowBirthday, day, CalendarGroup.Time, day);
                if (e == null)
                {
                    continue;
                }
                int age = (data.ageInDays + day - today) / year;
                e.Title = CalendarText.T("reallife_calendar_birthday_own_title");
                e.Value = CalendarText.T("reallife_calendar_birthday_own_age", "age", age.ToString());
            }
        }

        private static CharacterData Player(GameInstance save)
        {
            try
            {
                return PlayerHelper.CharacterData ?? (save.charactersData != null && save.charactersData.Count > 0 ? save.charactersData[0] : null);
            }
            catch (Exception)
            {
                return save.charactersData != null && save.charactersData.Count > 0 ? save.charactersData[0] : null;
            }
        }

        // What RealEstateHelper.UpdatePlayerRealEstate pays each night.
        private static void BuildingRent(CalendarList into)
        {
            List<RealEstate> estate = SaveGameManager.Current.realEstate;
            float rent = 0f;
            for (int i = 0; estate != null && i < estate.Count; i++)
            {
                RealEstate r = estate[i];
                Building building = r != null ? r.Building : null;
                if (building == null || building.IsHamptonsHouse())
                {
                    continue;
                }
                rent += Math.Max(0f, r.DailyIncome);
            }
            into.Night(SourceId, CalendarText.T("reallife_calendar_night_rent_in"), 1, rent);
        }
    }
}
