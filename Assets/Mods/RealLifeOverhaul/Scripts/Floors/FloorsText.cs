using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Floors
{
    /// <summary>The floors' words: a place's button and the lift's line, the building taken for places, the test's notices.</summary>
    public static class FloorsText
    {
        public static string T(string key) => FinanceText.T(key);

        public static string T(string key, string name, string value) => FinanceText.T(key, name, value);

        public static string T(string key, string name, string value, string name2, string value2) => FinanceText.T(key, name, value, name2, value2);
    }
}
