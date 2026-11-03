namespace RealLifeOverhaul.DaySeries
{
    /// <summary>
    /// The one saved daily history the money apps draw, and that Flare and the retail prices append to later. One line of the
    /// save, <see cref="Key"/>. Worth is kept in thousands of dollars so a fortune still fits a whole number. A figure this
    /// version does not know is left for whoever appends it: the ids below are the names, and a later job writes its own.
    /// </summary>
    public static class DaySeriesCatalogue
    {
        public const string Key = "reallife:dayseries";
        public const int Version = 1;
        public const int Days = 120;
        public const int LinesMost = 160;     // room for the money series and for flare reach and a price a good, appended later
        public const int NoValue = int.MinValue;
        public const int LoansMost = 48;
        public const int WorthScale = 1000;   // one stored unit of worth is a thousand dollars

        public const string Cash = "cash";           // whole dollars of cash at the end of the day
        public const string Score = "score";         // the credit score
        public const string Fund = "fund";           // the own bank's fund, whole dollars
        public const string Customers = "bb";        // broadband customers
        public const string WorthYou = "worth.y";    // the player's worth, in thousands of dollars

        // A category's net for the day, whole dollars, money in minus money out: "cat." and CashCategory's number.
        public const string CategoryPrefix = "cat.";

        // A tycoon's worth, in thousands: "worth.t" and their index. A fortune: "worth.f" and its index.
        public const string TycoonPrefix = "worth.t";
        public const string FortunePrefix = "worth.f";

        // flare.{account}  Flare reach for one account. Flare appends this. This job does not write it.
        // price.{good}     the retail price of one good, in cents. A later job appends this. This job does not write it.

        public static string Category(int index) => CategoryPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static string Tycoon(int index) => TycoonPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static string Fortune(int index) => FortunePrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A name safe in the save line: the separators are dropped, and an empty name is refused.</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            var chars = new char[name.Length];
            int n = 0;
            for (int i = 0; i < name.Length && n < 80; i++)
            {
                char c = name[i];
                if (c == '|' || c == ';' || c == '~' || c == ',' || c == '=' || c == ':')
                {
                    c = ' ';
                }
                chars[n++] = c;
            }
            return new string(chars, 0, n).Trim();
        }
    }
}
