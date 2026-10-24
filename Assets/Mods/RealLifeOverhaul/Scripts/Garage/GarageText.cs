using Extensions;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Texts of the Garage: a locale key in, the line out, with its {names} filled in. Builds strings: for when something changes, never per frame.</summary>
    public static class GarageText
    {
        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, string name, string value)
        {
            return T(key).Replace("{" + name + "}", value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return T(key).Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return T(key, name, value, name2, value2).Replace("{" + name3 + "}", value3);
        }

        public static string Money(float amount)

        {
            return amount.ToCurrencyFormat();
        }

        public static string Signed(float factor)
        {
            int percent = Mathf.RoundToInt((factor - 1f) * 100f);
            return (percent > 0 ? "+" : string.Empty) + percent;
        }
    }
}
