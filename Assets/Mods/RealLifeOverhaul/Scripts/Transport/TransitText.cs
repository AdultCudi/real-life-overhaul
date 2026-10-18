using Extensions;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>Texts for transport: a locale key with its {names} filled in. Builds strings: on a change only, never per frame.</summary>
    public static class TransitText
    {
        public static string T(string key, params string[] pairs)
        {
            string text = JobKeys.Fill(key.GetLocalization());
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }
    }
}
