namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The class a car wears on the showroom's pages: the glyph by body and the colour by class. The class is the game's own as the mod already reads it
    /// (<see cref="CarCatalogue"/>: the vehicle type's table, else its tier, cargo room and luxury flag), so a van or a truck is a work vehicle, a hatchback is
    /// cheap, a saloon or an SUV middle, a luxury car or a sports car dear. A type the mod leaves alone is placed by its price band. Game-free: the Live pages
    /// pass the kind they read from the game, the previews the kind of their sample cars, so one rule colours the same car the same way in both.
    /// </summary>
    public static class CarClass
    {
        public const int Cheap = 20000, Middle = 45000;

        /// <summary>True for a vehicle made to carry or work: it wears the truck and the industry colour.</summary>
        public static bool Work(CarKind kind) => kind == CarKind.Van || kind == CarKind.Truck;

        public static UiIcon Glyph(CarKind kind) => Work(kind) ? UiIcon.Truck : UiIcon.Car;

        /// <summary>The colour of the car's glyph: work in the industry colour, then amber for the cheap, blue for the middle and violet for the dear.</summary>
        public static UiRole Tint(CarKind kind, int price)
        {
            switch (kind)
            {
                case CarKind.Van:
                case CarKind.Truck: return UiRole.CatIndustry;
                case CarKind.Economy: return UiRole.Chart4;
                case CarKind.Saloon:
                case CarKind.Utility: return UiRole.Chart5;
                case CarKind.Luxury:
                case CarKind.Sports: return UiRole.Chart7;
                default: return price < Cheap ? UiRole.Chart4 : price < Middle ? UiRole.Chart5 : UiRole.Chart7;
            }
        }
    }
}
