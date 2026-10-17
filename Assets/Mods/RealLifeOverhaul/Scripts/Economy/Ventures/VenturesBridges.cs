using System;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>What Ventures asks of the rest of the mod, each with a default so the folder works alone; Wiring/VenturesLinks fills them in.</summary>
    public static class VenturesBridges
    {
        private static IVenturesClimate _climate = SteadyClimate.Instance;

        /// <summary>The city's economy and bank rate. Default: steady, a rate that never moves.</summary>
        public static IVenturesClimate Climate
        {
            get { return _climate; }
            set { _climate = value ?? SteadyClimate.Instance; }
        }

        /// <summary>How many employers the player has worked for, the present one included. Default: none.</summary>
        public static Func<int> JobsHeld;

        /// <summary>True while something of the mod's holds the player, so no meeting can be held (the game's: <see cref="VenturesGame.Busy"/>). Default: never.</summary>
        public static Func<bool> Held;

        public static void Reset()
        {
            _climate = SteadyClimate.Instance;
            JobsHeld = null;
            Held = null;
        }
    }

    /// <summary>The Ventures app's tabs.</summary>
    public static class VenturesPhone
    {
        public const int Investors = 0, Property = 1;
    }

    /// <summary>Ventures' words: every text is a key of en.json, and money is written as the Finance app writes it.</summary>
    public static class VenturesText
    {
        /// <summary>A share in basis points as a percentage: "25%", "23.9%".</summary>
        public static string Share(int shareBp)
        {
            return (shareBp / 100f).ToString(shareBp % 100 == 0 ? "0" : "0.#") + "%";
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

        /// <summary>The business a deal or an offer is about: its name now if the player still runs it, else the name it had.</summary>
        public static string Business(string place, string was)
        {
            BuildingRegistration r = VenturesGame.Business(place);
            return r != null ? VenturesGame.Name(r) : was;
        }
    }
}
