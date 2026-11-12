using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The words and money of the Brands, Ventures and Web shop pages: a key's words in the game's language (the English where the offline checks and the previews read a key), a
    /// text with its names filled in, and money as the game writes it. The live sources and the samples hand the same builders their own.
    /// </summary>
    public sealed class VentWords
    {
        private readonly UiWords _words;
        private readonly Func<float, string> _money;

        public VentWords(UiWords words, Func<float, string> money)
        {
            _words = words;
            _money = money;
        }

        public string T(string key, string english) => _words(key, english);

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public string F(string key, string english, params string[] pairs)
        {
            string text = _words(key, english);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        private Func<long, string> _price;

        /// <summary>A price kept in cents with its cents (4.50); the money words where none is set.</summary>
        public string Price(long cents) => _price != null ? _price(cents) : _money(cents / 100f);

        /// <summary>Sets how a price in cents is written.</summary>
        public VentWords WithPrice(Func<long, string> price)
        {
            _price = price;
            return this;
        }

        public string Money(float money) => _money(money);

        /// <summary>A sum as a table's column wants it: the game's own short money words (6.25M).</summary>
        public string Short(long money) => _money((float)money);

        /// <summary>A share in basis points as per cent: 25%, 23.9%.</summary>
        public static string Share(int shareBp) => (shareBp / 100f).ToString(shareBp % 100 == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";

        /// <summary>A hue from a name, so the same person has the same face tint on every page.</summary>
        public static float HueOf(string name)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; name != null && i < name.Length; i++)
                {
                    h = (h ^ name[i]) * 16777619u;
                }
                return (h % 360u) / 360f;
            }
        }
    }
}
