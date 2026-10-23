using System.Collections.Generic;
using Helpers;
using Localizor;

namespace RealLifeOverhaul.Economy
{
    internal static partial class CompanyGame
    {
        public static object CompanionStatementCategories()
        {
            var rows = new List<object>();
            for (int kind = 0; kind < StatementLines.Length; kind++)
            {
                rows.Add(CompanionSnapshot.Row("costKind", kind, "costName", ((CostKind)kind).ToString().ToLowerInvariant(), "statementCategory", StatementLines[kind]));
            }
            return rows;
        }

        /// <summary>The owning type predicates and permit rules, with optional late hours separate from mandatory types.</summary>
        public static CompanionModule CompanionRequirements(int index)
        {
            var types = new List<object>();
            foreach (string typeId in BusinessTypeHelper.BusinessTypeNames)
            {
                string type = typeId.StartsWith("ba:businesstype_") ? typeId.Substring("ba:businesstype_".Length) : typeId;
                types.Add(CompanionSnapshot.Row("typeId", typeId,
                    "licenceNeedsMask", LicenceRules.Needs(Is(type, FoodTypes), Is(type, AlcoholTypes), false)));
            }
            var licences = new List<object>();
            for (int kind = 0; kind < CompanyCatalogue.LicenceFee.Length; kind++)
            {
                licences.Add(CompanionSnapshot.Row("kind", kind, "feeCents", (long)LicenceRules.Fee(kind, index) * 100,
                    "termDays", CompanyCatalogue.LicenceTerm[kind], "closesWhenMissing", LicenceRules.Shuts(kind)));
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-opening-requirements-v1",
                "types", types, "licences", licences,
                "lateOpeningHours", CompanionSnapshot.Row("before", CompanyCatalogue.EarlyHour,
                    "after", CompanyCatalogue.LateHour, "permitKind", (int)LicenceKind.Late),
                "permitValidity", "untilDay-inclusive", "newSiteGraceDays", CompanyCatalogue.LicenceGraceDays,
                "paymentBasis", "startup-then-term-renewal"));
        }

        public static object CompanionTaxLabels()
        {
            var rows = new List<object>();
            for (int kind = 0; kind < CompanyText.CostKeys.Length; kind++)
            {
                rows.Add(CompanionSnapshot.Row("label", CompanyText.CostKeys[kind].GetLocalization(),
                    "kind", kind == (int)CostKind.Premiums || kind == (int)CostKind.Accountant
                        || kind == (int)CostKind.Wages ? "recurring" : "once"));
            }
            return rows;
        }
    }
}
