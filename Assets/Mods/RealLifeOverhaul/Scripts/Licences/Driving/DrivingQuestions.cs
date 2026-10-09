namespace RealLifeOverhaul
{
    /// <summary>
    /// The driving theory pool: 66 questions about the rules the mod actually polices, in seven
    /// subjects (lights, speed, people on foot, police and emergency vehicles, lanes and the
    /// practical test, vehicles and classes, licences and points). Each key is one line in
    /// en.json: question|right answer|wrong|wrong. The figures in them are CrimeLaw's and
    /// LicenceLaw's: change a fine or a points value there and the question that quotes it here.
    /// </summary>
    internal static class DrivingQuestions
    {
        private static readonly string[] Keys =
        {
            "reallife_lic_q_d01", "reallife_lic_q_d02", "reallife_lic_q_d03",
            "reallife_lic_q_d04", "reallife_lic_q_d05", "reallife_lic_q_d06",
            "reallife_lic_q_d07", "reallife_lic_q_d08", "reallife_lic_q_d09",
            "reallife_lic_q_d10", "reallife_lic_q_d11", "reallife_lic_q_d12",
            "reallife_lic_q_d13", "reallife_lic_q_d14", "reallife_lic_q_d15",
            "reallife_lic_q_d16", "reallife_lic_q_d17", "reallife_lic_q_d18",
            "reallife_lic_q_d19", "reallife_lic_q_d20", "reallife_lic_q_d21",
            "reallife_lic_q_d22", "reallife_lic_q_d23", "reallife_lic_q_d24",
            "reallife_lic_q_d25", "reallife_lic_q_d26", "reallife_lic_q_d27",
            "reallife_lic_q_d28", "reallife_lic_q_d29", "reallife_lic_q_d30",
            "reallife_lic_q_d31", "reallife_lic_q_d32", "reallife_lic_q_d33",
            "reallife_lic_q_d34", "reallife_lic_q_d35", "reallife_lic_q_d36",
            "reallife_lic_q_d37", "reallife_lic_q_d38", "reallife_lic_q_d39",
            "reallife_lic_q_d40", "reallife_lic_q_d41", "reallife_lic_q_d42",
            "reallife_lic_q_d43", "reallife_lic_q_d44", "reallife_lic_q_d45",
            "reallife_lic_q_d46", "reallife_lic_q_d47", "reallife_lic_q_d48",
            "reallife_lic_q_d49", "reallife_lic_q_d50", "reallife_lic_q_d51",
            "reallife_lic_q_d52", "reallife_lic_q_d53", "reallife_lic_q_d54",
            "reallife_lic_q_d55", "reallife_lic_q_d56", "reallife_lic_q_d57",
            "reallife_lic_q_d58", "reallife_lic_q_d59", "reallife_lic_q_d60",
            "reallife_lic_q_d61", "reallife_lic_q_d62", "reallife_lic_q_d63",
            "reallife_lic_q_d64", "reallife_lic_q_d65", "reallife_lic_q_d66",
        };

        // The first question of each subject: lights, speed, people on foot, police and emergency vehicles,
        // lanes and the practical test, vehicles and classes, licences and points.
        private static readonly int[] Groups = { 0, 12, 24, 34, 42, 52, 58 };

        public static readonly QuestionPool Pool = new QuestionPool(Keys, Groups);
    }
}
