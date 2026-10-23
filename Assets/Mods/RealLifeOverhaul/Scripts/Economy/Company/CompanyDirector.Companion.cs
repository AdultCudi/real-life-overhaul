using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class CompanyDirector
    {
        internal string CompanionPartStatus(CompanyPart part)
        {
            return !Wanted(part) ? "disabled" : _broken || _partBroken[(int)part] ? "failed"
                : !_running ? "unavailable" : "ready";
        }

        /// <summary>Commitments and optional new-site policy terms; never refreshes staff or financial statements.</summary>
        public CompanionModule CompanionCompany()
        {
            if (_broken)
            {
                return new CompanionModule("failed");
            }
            if (!_running || _state == null)
            {
                return new CompanionModule("unavailable");
            }
            var staffByPlace = new Dictionary<string, List<object>>(StringComparer.Ordinal);
            foreach (StaffFile staff in _state.Staff)
            {
                if (string.IsNullOrEmpty(staff.Place))
                {
                    continue;
                }
                if (!staffByPlace.TryGetValue(staff.Place, out List<object> list))
                {
                    list = new List<object>();
                    staffByPlace.Add(staff.Place, list);
                }
                list.Add(CompanionSnapshot.Row("employeeId", staff.Id, "raiseHourlyCents", staff.Raise));
            }
            var allowances = new Dictionary<string, long>(StringComparer.Ordinal);
            if (_mgr != null && ManagersOn)
            {
                foreach (ManagerFile manager in _mgr.Managers)
                {
                    string place = manager.Region ? manager.Base : manager.Place;
                    if (!string.IsNullOrEmpty(place))
                    {
                        allowances.TryGetValue(place, out long amount);
                        allowances[place] = amount + (long)ManagerRules.Allowance(manager.Role, manager.Skill, _index) * 100;
                    }
                }
            }
            var rows = new List<object>();
            bool hiding = false;
            foreach (CompanyShop shop in _shops)
            {
                ShopFile file = shop.File;
                if (shop.Reg == null || file == null)
                {
                    continue;
                }
                hiding |= file.Hide > 0;
                staffByPlace.TryGetValue(file.Place, out List<object> raises);
                var policies = new List<object>();
                for (int kind = 0; kind < file.Cover.Length; kind++)
                {
                    Policy policy = file.Cover[kind];
                    var quotes = new List<object>();
                    for (int insurer = 0; insurer < CompanyCatalogue.InsurerPrice.Length; insurer++)
                    {
                        quotes.Add(CompanionSnapshot.Row("insurer", insurer,
                            "eligible", CoverRules.WillWrite(policy, insurer),
                            "dayCents", (long)CoverRules.Premium(kind, insurer, shop.Average, shop.Income,
                                CoverRules.Carried(policy), _index) * 100));
                    }
                    policies.Add(CompanionSnapshot.Row("kind", kind, "insurer", policy.Insurer,
                        "active", CoverRules.Covers(policy, Today), "dailyCents", (long)policy.Premium * 100,
                        "fromDay", policy.From, "untilDay", policy.Until, "claims", policy.Claims,
                        "quoteInputs", CompanionSnapshot.Row("averageTakingsCents", (long)shop.Average * 100,
                            "income", shop.Income, "index", _index, "carriedClaims", CoverRules.Carried(policy)),
                        "quotes", quotes));
                }
                var licences = new List<object>();
                for (int kind = 0; kind < file.Permits.Length; kind++)
                {
                    licences.Add(CompanionSnapshot.Row("kind", kind, "untilDay", file.Permits[kind].Until,
                        "valid", LicenceRules.Valid(file.Permits[kind], Today), "autoRenew", file.Permits[kind].Auto));
                }
                allowances.TryGetValue(file.Place, out long allowance);
                rows.Add(CompanionSnapshot.Row("address", CompanionValues.Address(shop.Reg.StreetName, shop.Reg.StreetNumber),
                    "raises", raises ?? new List<object>(), "sickPayPolicy", _state.SickPolicy, "managerDailyAllowanceCents", allowance,
                    "overtime", CompanionSnapshot.Row("active", file.Overtime(Today), "fromDay", file.OvertimeFrom,
                        "untilDay", file.OvertimeEnd, "dailyCents", (long)file.OvertimeCost * 100),
                    "cover", policies, "licences", licences, "hiddenCashShareThousandths", file.Hide * 10,
                    "campaign", CompanionSnapshot.Row("kind", file.Push, "active", file.Pushing(Today),
                        "fromDay", file.PushFrom, "untilDay", file.PushEnd, "paidCents", (long)file.PushCost * 100,
                        "percent", file.PushPercent, "discountGivenCents", (long)file.PushGiven * 100)));
            }
            var states = new Dictionary<string, object>();
            for (int index = 0; index < (int)CompanyPart.Count; index++)
            {
                CompanyPart part = (CompanyPart)index;
                states[part.ToString().ToLowerInvariant()] = CompanionPartStatus(part);
            }
            TaxFile tax = _state.Tax;
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-company-v1", "sites", rows, "partStates", states,
                "parts", CompanionSnapshot.Row("staff", On(CompanyPart.Staff), "cover", On(CompanyPart.Cover),
                    "marketing", On(CompanyPart.Marketing), "tax", On(CompanyPart.Tax), "licences", On(CompanyPart.Licences)),
                "newHireWageFactorThousandths", Prices.Level, "sickShareThousandthsByPolicy", CompanyCatalogue.SickShare,
                "sickFallbackDayHours", CompanyCatalogue.SickDayHours, "newSiteDefaults", CompanionSnapshot.Row("raiseHourlyCents", 0,
                    "sickPayPolicy", _state.SickPolicy, "cover", "none", "campaign", "none", "hiddenCashShareThousandths", 0),
                "accountant", CompanionSnapshot.Row("tier", tax.Accountant, "monthlyFeeCents",
                    tax.Accountant < 0 ? 0L : (long)TaxRules.Fee(tax.Accountant, _index, hiding) * 100,
                    "nextDay", tax.AccountantNext, "monthDays", GameCalendar.MonthDays),
                "coverTerms", CompanionSnapshot.Row("baseMoney", CompanyCatalogue.CoverBase,
                    "takingsShareThousandths", CompanyCatalogue.CoverTakingsShare,
                    "insurerPriceThousandths", CompanyCatalogue.InsurerPrice,
                    "incomePerPoint", CompanyCatalogue.CoverIncomePerPoint,
                    "districtMinThousandths", CompanyCatalogue.CoverDistrictLeast,
                    "districtMaxThousandths", CompanyCatalogue.CoverDistrictMost,
                    "claimLoadingThousandths", CompanyCatalogue.ClaimLoading,
                    "termDays", CompanyCatalogue.TermDays(GameCalendar.DaysPerYear)),
                "statementInclusion", CompanyGame.CompanionStatementCategories()));
        }
    }
}
