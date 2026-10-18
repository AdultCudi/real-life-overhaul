using System.Globalization;
using System.Text;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.Events;

namespace RealLifeOverhaul
{
    /// <summary>A gig's offer in words for the Gig Line and the card, so nothing the dice decided is hidden: "a generous client, a busy day".</summary>
    public static class GigWords
    {
        private static readonly StringBuilder Text = new StringBuilder(96);

        /// <summary>The sky now, as the weather pay sees it (fair when the weather option is off).</summary>
        public static GigWeather Sky()
        {
            switch (Weather.Spell)
            {
                case Spell.Ice: return GigWeather.Ice;
                case Spell.HeatWave: return GigWeather.Heat;
                case Spell.ColdSnap: return GigWeather.Cold;
            }
            switch (Weather.Sky)
            {
                case Events.Sky.Storm: return GigWeather.Storm;
                case Events.Sky.Rain: return GigWeather.Rain;
                default: return GigWeather.Fair;
            }
        }

        /// <summary>"a generous client, a busy day": null for a shift that has no offer.</summary>
        public static string List(in GigOffer offer)
        {
            if (offer.Flat)
            {
                return null;
            }
            Text.Length = 0;
            Add(offer.Client == GigClient.Tight ? "reallife_gig_client_tight" : offer.Client == GigClient.Generous ? "reallife_gig_client_generous" : "reallife_gig_client_fair");
            if (offer.Day == GigDay.Busy)
            {
                Add("reallife_gig_day_busy");
            }
            if (offer.Crowd == GigCrowd.Quiet)
            {
                Add("reallife_gig_crowd_quiet");
            }
            else if (offer.Crowd == GigCrowd.Rush)
            {
                Add("reallife_gig_crowd_rush");
            }
            switch (offer.Weather)
            {
                case GigWeather.Rain: Add("reallife_gig_weather_rain"); break;
                case GigWeather.Storm: Add("reallife_gig_weather_storm"); break;
                case GigWeather.Heat: Add("reallife_gig_weather_heat"); break;
                case GigWeather.Cold: Add("reallife_gig_weather_cold"); break;
                case GigWeather.Ice: Add("reallife_gig_weather_ice"); break;
            }
            return Text.ToString();
        }

        private static void Add(string key)
        {
            Text.Append(Text.Length > 0 ? ", " : string.Empty).Append(key.GetLocalization());
        }

        /// <summary>The Gig Line's sentence about the offer: its share of the usual fee and what is behind it.</summary>
        public static string Quote(in GigOffer offer)
        {
            string list = List(offer);
            return list == null ? string.Empty : JobText.T("reallife_gig_quote", "fee", ((int)(offer.Fee * 100f + 0.5f)).ToString(), "words", list);
        }

        /// <summary>One diagnostics line for an offer: the seed and every figure drawn from it.</summary>
        public static void Log(IModLogger logger, in GigOffer offer, int day, int hour)
        {
            if (logger == null)
            {
                return;
            }
            logger.Info("Jobs: offer for '" + offer.Id + "' (day " + day + ", hour " + hour + ", seed " + offer.Seed + (offer.Flat ? ", flat: the table's figures" : string.Empty) + "): "
                        + offer.Client + " client x" + offer.ClientFactor.ToString("F3", CultureInfo.InvariantCulture) + ", day " + offer.Day + " x"
                        + offer.DayFactor.ToString("F3", CultureInfo.InvariantCulture) + ", weather " + offer.Weather
                        + " x" + offer.WeatherFactor.ToString("F2", CultureInfo.InvariantCulture) + ", fee x" + offer.Fee.ToString("F3", CultureInfo.InvariantCulture)
                        + ", size " + offer.Units + ", crowd " + offer.Crowd + " x" + offer.CrowdFactor.ToString("F2", CultureInfo.InvariantCulture)
                        + ", tipper at " + offer.TipperAt + " (" + offer.TipperUnits.ToString("F2", CultureInfo.InvariantCulture) + " units), speed bonus "
                        + offer.SpeedUnits.ToString("F2", CultureInfo.InvariantCulture) + " units, redo at " + offer.RedoAt + ".");
        }
    }
}
