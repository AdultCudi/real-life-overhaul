using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>Portable tariff terms from the same catalogue and rules that bill the player's accounts.</summary>
    public static class UtilitiesCompanion
    {
        /// <summary>The tariff terms for a price level and a year of the given length.</summary>
        public static Dictionary<string, object> Terms(int level, int year)
        {
            var seasons = new List<object>();
            for (int day = 1; day <= year; day++)
            {
                seasons.Add(CalendarRules.Season(day, year));
            }
            return CompanionSnapshot.Row("priceLevel", level, "yearDays", year,
                "monthDays", CalendarRules.MonthDays(year), "seasonByDayOfYear", seasons,
                "unitCents", UtilitiesCatalogue.UnitCents, "standingCents", UtilitiesCatalogue.StandingCents,
                "powerPerSqm", UtilitiesCatalogue.PowerPerSqm, "waterPerSqm", UtilitiesCatalogue.WaterPerSqm,
                "powerSeasonThousandths", UtilitiesCatalogue.PowerSeason,
                "waterSeasonThousandths", UtilitiesCatalogue.WaterSeason,
                "closedShareThousandths", UtilitiesCatalogue.ClosedShare,
                "temporarySurchargeThousandths", UtilitiesCatalogue.TemporarySurcharge,
                "temporaryDays", UtilitiesCatalogue.TemporaryDays, "connectDays", UtilitiesCatalogue.ConnectDays,
                "depositMonthsByGrade", UtilitiesCatalogue.DepositMonths,
                "depositBackBills", UtilitiesCatalogue.DepositBackBills,
                "rentedStartup", "active-no-deposit", "boughtStartup", "temporary-then-apply",
                "moneyRounding", "cents-half-up-to-whole", "accrualBasis", "daily-cents",
                "dailyCapCents", int.MaxValue / 4,
                "priceLevelMin", PriceRules.Least, "priceLevelMax", PriceRules.Most,
                "homePowerBase", UtilitiesCatalogue.HomePowerBase,
                "homePowerPerSqm", UtilitiesCatalogue.HomePowerPerSqm,
                "homeWaterPerPerson", UtilitiesCatalogue.HomeWaterPerPerson,
                "extraPersonShareThousandths", UtilitiesCatalogue.ExtraPersonShare);
        }

        /// <summary>One utility account as the contract writes it.</summary>
        public static object AccountRow(Account account, object address, UtilitiesState state)
        {
            return CompanionSnapshot.Row("address", address, "service", account.Service.ToString().ToLowerInvariant(),
                "state", account.State.ToString().ToLowerInvariant(), "owned", account.Owned,
                "accruedCents", account.Accrued, "fromDay", state.MonthStart, "lastDay", state.LastDay,
                "owedCents", (long)account.Owed * 100, "dueDay", account.DueDay,
                "lastBillCents", (long)account.LastBill * 100, "lastBillDay", account.LastBillDay,
                "depositCents", (long)account.Deposit * 100, "connectDay", account.ConnectDay,
                "temporaryUntilDay", account.TemporaryUntil, "provider", account.Provider, "plan", account.Plan,
                "businessPlan", account.BusinessPlan, "contractUntilDay", account.ContractUntil,
                "monthCents", account.PriceCents, "outageFromHour", account.OutageFrom,
                "outageUntilHour", account.OutageUntil);
        }

        /// <summary>The internet providers' plans and prices at a price level.</summary>
        public static object Internet(int level, int monthDays, UtilitiesState state)
        {
            var providers = new List<object>();
            for (int i = 0; i < InternetRules.Count; i++)
            {
                Provider provider = InternetRules.Get(i);
                if (provider == null)
                {
                    continue;
                }
                var plans = new List<object>();
                for (int plan = 0; plan < provider.MonthCents.Length; plan++)
                {
                    plans.Add(CompanionSnapshot.Row("plan", plan, "mbps", provider.Mbps[plan],
                        "businessContractMonthCents", InternetRules.MonthCents(i, plan, true, true, level, state.PromoOf(i)),
                        "businessMonthlyMonthCents", InternetRules.MonthCents(i, plan, true, false, level, state.PromoOf(i))));
                }
                providers.Add(CompanionSnapshot.Row("provider", i, "id", provider.Id,
                    "districts", provider.Districts, "plans", plans));
            }
            return CompanionSnapshot.Row("providers", providers, "installDays", InternetCatalogue.InstallDays,
                "installFeeCents", (long)InternetCatalogue.InstallFee * 100,
                "contractInstallFeeCents", 0, "contractDays", InternetCatalogue.ContractMonths * monthDays,
                "needs", InternetCatalogue.Needs, "futureOutageAssumption", "no-outage-days",
                "dailyRounding", "floor-month-cents-plus-half-month-divided-by-month",
                "accrualIncludesOtherProviderSeatCharges", true);
        }
    }
}
