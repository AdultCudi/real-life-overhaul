namespace RealLifeOverhaul.Interiors
{
    /// <summary>Portable terms from the exact production constants, shared by live capture and fixtures.</summary>
    public static class InteriorCompanion
    {
        /// <summary>The interior terms from the production constants and the parts in use.</summary>
        public static object Terms(InteriorParts parts)
        {
            return CompanionSnapshot.Row("standardScaleThousandths", parts.StandardScale,
                    "decorMille", parts.DecorMille, "priceScaleThousandths", parts.PriceScale, "basket", parts.Basket,
                    "interiorShareThousandths", InteriorRules.InteriorShare,
                    "shortPointsPerPercent", InteriorRules.ShortPointsPerPercent,
                    "shortFootfallMaxPercent", InteriorRules.ShortFootfallMost,
                    "overPointsPerPercent", InteriorRules.OverPointsPerPercent,
                    "overFootfallMaxPercent", InteriorRules.OverFootfallMost,
                    "bearsOverMaxThousandths", InteriorRules.BearsOverMost,
                    "basketFullAt", InteriorRules.BasketFullAt, "basketMostPoorThousandths", InteriorRules.BasketMostPoor,
                    "basketMostRichThousandths", InteriorRules.BasketMostRich,
                    "standardLeast", InteriorRules.StandardLeast, "standardMost", InteriorRules.StandardMost,
                    "graceDays", InteriorRules.GraceDays, "rampDays", InteriorRules.RampDays,
                    "categoryIds", InteriorRules.Ids, "itemPoints", InteriorRules.ItemPoints,
                    "capShareThousandths", InteriorRules.CapShare, "repeatShareThousandths", InteriorRules.RepeatShare,
                    "repeatMost", InteriorRules.RepeatMost, "targetLeast", InteriorRules.TargetLeast,
                    "targetFromSqm", InteriorRules.TargetFromSqm, "targetSqmPerPoint", InteriorRules.TargetSqmPerPoint, "targetMost", InteriorRules.TargetMost,
                    "incomeLeast", InteriorRules.IncomeLeast, "incomeMost", InteriorRules.IncomeMost,
                    "footfallLeast", InteriorRules.FootfallLeast, "footfallMost", InteriorRules.FootfallMost,
                    "maxDecorMille", InteriorRules.Strength(InteriorRules.StrengthMost) * InteriorRules.DecorMille / 1000,
                    "officeBoostMost", InteriorRules.OfficeBoostMost, "basketOfficeMost", InteriorRules.BasketOfficeMost,
                    "varietyFrom", InteriorRules.VarietyFrom, "varietyMost", InteriorRules.VarietyMost);
        }
    }
}
