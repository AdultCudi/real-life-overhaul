namespace RealLifeOverhaul
{
    /// <summary>Production promotion bounds, used by live capture and portable fixtures.</summary>
    public static class BusinessCompanion
    {
        /// <summary>
        /// The footfall terms the business feature writes with. Read only during a capture, nothing per frame.
        /// </summary>
        public static object Terms()
        {
            return CompanionSnapshot.Row("issueMin", BusinessCatalogue.MostDip, "issueMax", BusinessCatalogue.MostBoost,
                "ambientMin", BusinessCatalogue.AmbientMostDip, "ambientMax", BusinessCatalogue.AmbientMostBoost,
                "interiorMin", BusinessCatalogue.InteriorMostDip, "interiorMax", BusinessCatalogue.InteriorMostBoost,
                "promotionCoefficient", "0.75", "promotionMin", 0, "promotionMax", 100,
                "rounding", "float32-add-half-truncate");
        }
    }
}
