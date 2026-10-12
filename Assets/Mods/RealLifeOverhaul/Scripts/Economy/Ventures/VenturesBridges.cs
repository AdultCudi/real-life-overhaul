using System;
using System.Collections.Generic;
using Extensions;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// What investors ask of the rest of the mod. Each is optional and has a default, so
    /// Scripts/Economy/Ventures works alone; Scripts/Wiring/VenturesLinks.cs fills them in. Ventures calls no other
    /// feature directly. (The legal record, the courts for a civil claim and "is the player held" are the economy's
    /// own bridges, <see cref="EconomyBridges"/>, and are used as they are.)
    /// </summary>
    public static class VenturesBridges
    {
        private static IVenturesClimate _climate = SteadyClimate.Instance;

        /// <summary>The city's economy: a slowdown, steady or a boom, and the bank rate. Default: steady, a rate that never moves.</summary>
        public static IVenturesClimate Climate
        {
            get { return _climate; }
            set { _climate = value ?? SteadyClimate.Instance; }
        }

        /// <summary>How many employers the player has worked for, the present one included. Default: none.</summary>
        public static Func<int> JobsHeld;

        /// <summary>
        /// True while something of the mod's has hold of the player that a meeting cannot be held through: custody,
        /// prison, a hearing, a driving test, the police, a bus ride. (The game's own holds are asked of the game:
        /// <see cref="VenturesGame.Busy"/>.) Default: never.
        /// </summary>
        public static Func<bool> Held;

        public static void Reset()
        {
            _climate = SteadyClimate.Instance;
            JobsHeld = null;
            Held = null;
        }
    }

    /// <summary>The Ventures app as the rest of the mod may use it: open it, on a tab.</summary>
    public static class VenturesPhone
    {
        public const int Investors = 0, Property = 1;

        /// <summary>Opens the Ventures app on a tab. Does nothing while Ventures is off or the app is not on the phone.</summary>
        public static void Open(int tab)
        {
            VenturesDirector director = VenturesDirector.Instance;
            if (director != null)
            {
                director.Open(tab);
            }
        }
    }

    /// <summary>
    /// Ventures' words: every text is a key of en.json, money is written as the Finance app writes it and a share
    /// price as the game writes money with its decimals. Texts are built when a page is written or a notification
    /// goes out, never on a timer.
    /// </summary>
    public static class VenturesText
    {
        /// <summary>A share in basis points as a percentage: "25%", "23.9%".</summary>
        public static string Share(int shareBp)
        {
            return (shareBp / 100f).ToString(shareBp % 100 == 0 ? "0" : "0.#") + "%";
        }

        /// <summary>A move in basis points with its sign: "+1.2%", "-0.8%", "0.0%".</summary>
        public static string Change(int bp)
        {
            return (bp > 0 ? "+" : string.Empty) + (bp / 100f).ToString("0.0") + "%";
        }

        /// <summary>A share price, with its cents.</summary>
        public static string Price(int cents)
        {
            return (cents / 100f).ToCurrencyFormat();
        }

        public static string Kind(Investor investor)
        {
            return FinanceText.T(VenturesCatalogue.KindKeys[(int)investor.Kind]);
        }

        public static string Temper(Investor investor)
        {
            return FinanceText.T(VenturesCatalogue.TemperKeys[(int)investor.Temper]);
        }

        public static string SectorName(Sector sector)
        {
            return FinanceText.T(VenturesCatalogue.SectorKeys[(int)sector]);
        }

        /// <summary>The sectors an investor likes, as a list in words.</summary>
        public static string Likes(Investor investor)
        {
            string text = string.Empty;
            for (int s = 0; s < (int)Sector.Count; s++)
            {
                if (investor.LikesSector((Sector)s))
                {
                    text += (text.Length > 0 ? ", " : string.Empty) + SectorName((Sector)s);
                }
            }
            return text;
        }

        public static string Mood(Deal deal)
        {
            return FinanceText.T(VenturesCatalogue.MoodKeys[VenturesCatalogue.MoodWord(deal.Mood)]);
        }

        /// <summary>True for an investor who will not back the founder again (they lost interest before they had their money back).</summary>
        public static bool Never(Investor investor, int day)
        {
            return investor.ClosedUntil - day > VenturesCatalogue.ColdClosedDays / 2;
        }

        public static string Cause(int cause)
        {
            return FinanceText.T(Market.CauseKeys[cause > 0 && cause < Market.CauseKeys.Length ? cause : 0]);
        }

        /// <summary>The business a deal or an offer is about: its name now if the player still runs it, else the name it had.</summary>
        public static string Business(string place, string was)
        {
            BuildingRegistration r = VenturesGame.Business(place);
            return r != null ? VenturesGame.Name(r) : was;
        }
    }
}
