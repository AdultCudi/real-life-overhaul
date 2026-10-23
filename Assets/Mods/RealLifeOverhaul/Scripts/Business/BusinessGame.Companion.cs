using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    internal static partial class BusinessGame
    {
        /// <summary>Reads the last applied promotion even after normal saving puts the own value back.</summary>
        public static CompanionModule CompanionFootfall(object tolerance, Func<BuildingRegistration, int> city,
            Func<BuildingRegistration, int> company)
        {
            if (!ReferenceEquals(_writtenSave, SaveGameManager.Current))
            {
                return new CompanionModule("unavailable");
            }
            var rows = new List<object>();
            foreach (BuildingRegistration reg in SaveGameManager.Current.BuildingRegistrations)
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress)
                {
                    continue;
                }
                bool known = Written.TryGetValue(reg, out Wrote value) && value.Play.Known && reg.promotion != null;
                rows.Add(CompanionSnapshot.Row("address", CompanionValues.Address(reg.StreetName, reg.StreetNumber),
                    "status", known ? "ready" : "unavailable", "ownPromotion", known ? (object)value.Play.Own : null,
                    "writtenPromotion", known ? (object)value.Play.Total : null,
                    "registrationPromotionAtCapture", reg.promotion != null ? (object)reg.promotion.total : null,
                    "cityPercent", city != null ? city(reg) : 0, "companyPercent", company != null ? company(reg) : 0,
                    "issuePercent", known ? (object)value.Play.Percent : null,
                    "ambientPercent", known ? (object)value.Play.Ambient : null,
                    "interiorPercent", known ? (object)value.Play.Interior : null,
                    "baseMultiplier", known ? CompanionValues.Decimal(value.Play.Multiplier) : null));
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-footfall-v1", "sites", rows,
                "terms", BusinessCompanion.Terms(), "priceTolerance", tolerance, "futureAssumption", "captured-city-settings-held-constant"));
        }

        public static List<object> CompanionOutside()
        {
            return CompanionTransactions.Rows(new Dictionary<string, string>
            {
                ["reallife_transaction_biz_protection"] = "racket", ["reallife_transaction_biz_repair"] = "repair",
                ["reallife_transaction_biz_bribe"] = "other", ["reallife_transaction_biz_lawyer"] = "other",
                ["reallife_transaction_biz_cleaning"] = "other", ["reallife_transaction_biz_firecheck"] = "other",
                ["reallife_transaction_biz_express"] = "other", ["reallife_transaction_biz_surcharge"] = "other",
                ["reallife_transaction_biz_switch"] = "other", ["reallife_transaction_biz_accountant"] = "tax.accountant",
                ["reallife_transaction_biz_taxrefund"] = "tax.rebate", ["reallife_transaction_biz_taxadjust"] = "tax.backtax",
                ["reallife_transaction_biz_firefine"] = "other", ["reallife_transaction_biz_healthfine"] = "other"
            });
        }

        public static CompanionModule CompanionClosures()
        {
            if (!BusinessState.Loaded)
            {
                return new CompanionModule("unavailable");
            }
            var rows = new List<object>();
            foreach (Closure closure in BusinessState.Closures)
            {
                if (!BusinessState.SplitPlace(closure.Place, out string street, out int number))
                {
                    throw new ArgumentException("Unknown closure address");
                }
                rows.Add(CompanionSnapshot.Row("address", CompanionValues.Address(street, number),
                    "source", closure.Source.ToString().ToLowerInvariant(), "fromDay", null,
                    "untilDay", closure.Day, "untilHour", closure.Hour, "end", "observed-hold",
                    "mayRenew", closure.Source == ClosureSource.Utilities));
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("rows", rows,
                "futureEvents", "risk-not-predicted", "coverage", "owner-holds"));
        }
    }
}
