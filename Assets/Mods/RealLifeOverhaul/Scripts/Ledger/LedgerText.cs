using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The City Ledger's words: locale keys written out in the language of the moment; money as the game writes it.</summary>
    public static class LedgerText
    {
        public static string T(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : key.GetLocalization();
        }

        /// <summary>A text with its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = T(key);
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }

        public static string Money(int amount)
        {
            return ((double)amount).ToShortCurrencyFormat();
        }

        /// <summary>A share price in cents as the share prices print it: the figure, without the money's sign.</summary>
        public static string Figure(int cents)
        {
            return (cents / 100.0).ToString("N2", CultureInfo.CurrentCulture);
        }

        /// <summary>A move in basis points with its sign: "+1.25%".</summary>
        public static string Change(int bp)
        {
            return (bp > 0 ? "+" : string.Empty) + (bp / 100f).ToString("0.00", CultureInfo.CurrentCulture) + "%";
        }

        /// <summary>A rate in basis points: "4.25%".</summary>
        public static string Rate(int bp)
        {
            return (bp / 100f).ToString("0.00", CultureInfo.CurrentCulture) + "%";
        }

        public static string Hour(int hour)
        {
            return hour.GetFormattedTime(0f, true);
        }

        /// <summary>The words of a piece, written out now: its kicker, its headline (its own, or the paper's for its kind) and its story.</summary>
        public static void Words(LedgerPiece p, out string kicker, out string headline, out string body)
        {
            string text = Fill(p.Key, p.Data);
            LedgerRules.Split(text, out kicker, out headline, out body);
            string model = LedgerBridges.TextsBody != null ? LedgerBridges.TextsBody(p) : null;   // RLO Texts' brief for the story, when it was written in time
            if (model != null)
            {
                body = model;
            }
            if (headline == null && p.Head != null)
            {
                string head = Fill(p.Head, p.Data);
                headline = head.Length > 0 && head != p.Head ? head : null;
            }
        }
    }
}
