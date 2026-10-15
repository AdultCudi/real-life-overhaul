using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The words of the rival tycoons. Every text is a key of en.json, written out in full (or in the catalogue's lists) so
    /// that the locale check finds each one; nothing here is kept. Called when the page shows something, a notice is made
    /// or the paper is written, never otherwise.
    /// </summary>
    public static class RivalText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A move as a short phrase for a notice and the page ("buying up buildings in {district}"), by kind.</summary>
        private static readonly string[] WhatKeys =
        {
            "reallife_rival_what_property", "reallife_rival_what_outlet", "reallife_rival_what_poach", "reallife_rival_what_stake", "reallife_rival_what_bid",
            "reallife_rival_what_lobby", "reallife_rival_what_suit", "reallife_rival_what_press"
        };

        /// <summary>What a move did when it came, a notice and a piece of the paper, by kind.</summary>
        private static readonly string[] CameKeys =
        {
            "reallife_rival_came_property", "reallife_rival_came_outlet", "reallife_rival_came_poach", "reallife_rival_came_stake", "reallife_rival_came_bid",
            "reallife_rival_came_lobby", "reallife_rival_came_suit", "reallife_rival_came_press"
        };

        /// <summary>What a move did when it came, as the paper prints it, by kind.</summary>
        private static readonly string[] PaperCameKeys =
        {
            "reallife_rival_paper_came_property", "reallife_rival_paper_came_outlet", "reallife_rival_paper_came_poach", "reallife_rival_paper_came_stake", "reallife_rival_paper_came_bid",
            "reallife_rival_paper_came_lobby", "reallife_rival_paper_came_suit", "reallife_rival_paper_came_press"
        };

        /// <summary>A counter's outcome as the paper prints it, by kind.</summary>
        private static readonly string[] PaperDoneKeys =
        {
            "reallife_rival_paper_done_outbid", "reallife_rival_paper_done_poach", "reallife_rival_paper_done_press", "reallife_rival_paper_done_lobby", "reallife_rival_paper_done_takeover"
        };

        /// <summary>A counter's outcome, a notice, by kind.</summary>
        private static readonly string[] CounterDoneKeys =
        {
            "reallife_rival_done_outbid", "reallife_rival_done_poach", "reallife_rival_done_press", "reallife_rival_done_lobby", "reallife_rival_done_takeover"
        };

        /// <summary>What a counter does, for its question and the page, by kind.</summary>
        private static readonly string[] CounterAboutKeys =
        {
            "reallife_rival_about_outbid", "reallife_rival_about_poach", "reallife_rival_about_press", "reallife_rival_about_lobby", "reallife_rival_about_takeover"
        };

        /// <summary>Each tycoon's way with words, by their index: a line closing the paper's pieces about them, and one as they come forward.</summary>
        private static readonly string[] QuipKeys = { "reallife_rival_quip_0", "reallife_rival_quip_1", "reallife_rival_quip_2" };
        private static readonly string[] HelloKeys = { "reallife_rival_hello_0", "reallife_rival_hello_1", "reallife_rival_hello_2" };

        public static string Quip(int tycoon) => T(QuipKeys[RivalCatalogue.Clamp(tycoon, 0, QuipKeys.Length - 1)]);
        public static string Hello(int tycoon) => T(HelloKeys[RivalCatalogue.Clamp(tycoon, 0, HelloKeys.Length - 1)]);

        private static readonly string[] WhyKeys =
        {
            null, "reallife_rival_why_out", "reallife_rival_why_rest", "reallife_rival_why_nothing", "reallife_rival_why_no_holding", "reallife_rival_why_short", "reallife_rival_why_money"
        };

        /// <summary>Every key built from a list here, for the offline check of the three locales.</summary>
        public static IEnumerable<string> ListedKeys()
        {
            foreach (string k in WhatKeys) yield return k;
            foreach (string k in QuipKeys) yield return k;
            foreach (string k in HelloKeys) yield return k;
            foreach (string k in CameKeys) yield return k;
            foreach (string k in CounterDoneKeys) yield return k;
            foreach (string k in PaperCameKeys) yield return k;
            foreach (string k in PaperDoneKeys) yield return k;
            foreach (string k in CounterAboutKeys) yield return k;
            for (int i = 1; i < WhyKeys.Length; i++) yield return WhyKeys[i];
        }

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>The names of a text as the game's data for a notice or a piece of the paper: a new dictionary each time (the game keeps hold of it).</summary>
        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }

        public static string N(long value)
        {
            return value.ToString("N0", Inv);
        }

        /// <summary>An amount of money as the game writes it.</summary>
        public static string Money(long amount)
        {
            return ((float)amount).ToCurrencyFormat();
        }

        /// <summary>A share price in cents as the game writes money.</summary>
        public static string MoneyCents(int cents)
        {
            return (cents / 100f).ToCurrencyFormat();
        }

        /// <summary>A text's names as pairs for <see cref="Fill"/>.</summary>
        public static string[] Pairs(Dictionary<string, string> data)
        {
            var pairs = new string[data.Count * 2];
            int i = 0;
            foreach (KeyValuePair<string, string> kv in data)
            {
                pairs[i++] = kv.Key;
                pairs[i++] = kv.Value;
            }
            return pairs;
        }

        /// <summary>A change against a week ago, in money with its sign; empty with nothing to compare.</summary>
        public static string Change(long now, long before)
        {
            if (before <= 0)
            {
                return string.Empty;
            }
            long d = now - before;
            return (d > 0 ? "+" : d < 0 ? "-" : string.Empty) + Money(d < 0 ? -d : d);
        }

        public static string MoveName(RivalMoveKind kind) => T(RivalCatalogue.MoveNames[RivalCatalogue.Clamp((int)kind, 0, RivalCatalogue.MoveNames.Length - 1)]);
        public static string CounterName(RivalCounter kind) => T(RivalCatalogue.CounterNames[RivalCatalogue.Clamp((int)kind, 0, RivalCatalogue.CounterNames.Length - 1)]);
        public static string State(RivalMoveState state) => T(RivalCatalogue.StateNames[RivalCatalogue.Clamp((int)state, 0, RivalCatalogue.StateNames.Length - 1)]);
        public static string Strength(int strength) => T(RivalCatalogue.StrengthNames[RivalCatalogue.Clamp(strength, 0, RivalCatalogue.StrengthNames.Length - 1)]);
        public static string Tier(int tier) => T(RivalCatalogue.TierNames[RivalCatalogue.Clamp(tier, 0, RivalCatalogue.TierNames.Length - 1)]);
        public static string Why(RivalWhy why) => why == RivalWhy.None ? string.Empty : T(WhyKeys[RivalCatalogue.Clamp((int)why, 1, WhyKeys.Length - 1)]);
        public static string CounterAbout(RivalCounter kind) => T(CounterAboutKeys[RivalCatalogue.Clamp((int)kind, 0, CounterAboutKeys.Length - 1)]);
        public static string CounterDoneKey(RivalCounter kind) => CounterDoneKeys[RivalCatalogue.Clamp((int)kind, 0, CounterDoneKeys.Length - 1)];
        public static string CameKey(RivalMoveKind kind) => CameKeys[RivalCatalogue.Clamp((int)kind, 0, CameKeys.Length - 1)];

        /// <summary>A move's notice as it came, the true one of its texts: a lobbying with no inspection called in, a piece with the paper off.</summary>
        public static string CameKey(RivalMove m)
        {
            if (m.Kind == RivalMoveKind.Lobby && m.Second == 0)
            {
                return "reallife_rival_came_lobby_quiet";
            }
            if (m.Kind == RivalMoveKind.Press && (m.Second & 2) == 0)
            {
                return "reallife_rival_came_press_word";
            }
            return CameKey(m.Kind);
        }
        public static string PaperCameKey(RivalMoveKind kind) => PaperCameKeys[RivalCatalogue.Clamp((int)kind, 0, PaperCameKeys.Length - 1)];
        public static string PaperDoneKey(RivalCounter kind) => PaperDoneKeys[RivalCatalogue.Clamp((int)kind, 0, PaperDoneKeys.Length - 1)];

        /// <summary>A move's names for its texts: the rival, the holding, the business, the district, the size and the day it comes.</summary>
        public static Dictionary<string, string> MoveData(RivalMove m)
        {
            TycoonDef t = RivalCatalogue.Tycoons[RivalCatalogue.Clamp(m.Tycoon, 0, RivalCatalogue.TycoonCount - 1)];
            return Data("rival", t.Name, "holding", t.Holding, "business", string.IsNullOrEmpty(m.Name) ? T("reallife_rival_your_business") : m.Name,
                "district", RivalGame.DistrictName(m.District), "size", Size(m), "rent", Permille(m.Second), "days", N(m.Land - m.Announced), "day", N(m.Land),
                "end", N(m.End), "what", What(m), "name", string.IsNullOrEmpty(m.Who) ? T("reallife_rival_your_best") : m.Who, "quip", Quip(m.Tycoon));
        }

        /// <summary>A move as a short phrase ("buying up buildings in Midtown").</summary>
        public static string What(RivalMove m)
        {
            return Fill(WhatKeys[RivalCatalogue.Clamp((int)m.Kind, 0, WhatKeys.Length - 1)], "business", string.IsNullOrEmpty(m.Name) ? T("reallife_rival_your_business") : m.Name,
                "district", RivalGame.DistrictName(m.District), "size", Size(m));
        }

        /// <summary>A move's size in words: a share for property, poaching and lobbying, per cent of customers for an outlet, money for a suit.</summary>
        public static string Size(RivalMove m)
        {
            switch (m.Kind)
            {
                case RivalMoveKind.Property:
                case RivalMoveKind.Poach:
                case RivalMoveKind.Lobby:
                    return Permille(m.Amount);
                case RivalMoveKind.Outlet:
                    return m.Amount.ToString(Inv) + "%";
                case RivalMoveKind.Suit:
                    return Money(m.Amount);
                default:
                    return string.Empty;
            }
        }

        /// <summary>A share given in thousandths, as a per cent ("12%", "7.5%").</summary>
        public static string Permille(long permille)
        {
            return (permille / 10.0).ToString("0.#", Inv) + "%";
        }
    }
}
