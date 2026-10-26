using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The online shops' director, part: each site's building's internet connection. Its plan sets the day's bound on
    /// orders (OnlineRules.Most), an outage stops the hour's orders, and a shop with no connection takes none. Read on the
    /// game's hour for each site (two lookups in the utilities' accounts), never per frame.
    /// </summary>
    public sealed partial class OnlineDirector
    {
        private readonly HashSet<string> _netNone = new HashSet<string>();   // sites told they have no connection; not saved, so told again once after a load

        private static int Speed(BuildingRegistration reg, int hour)
        {
            return OnlineBridges.Mbps != null && reg != null ? OnlineBridges.Mbps(reg, hour) : NetSpeedRules.Unknown;
        }

        // The connection for an hour: a change of plan works the day's orders out again at once, an outage holds the hour's
        // orders back, and losing or getting back a connection is told once each way.
        private void NetHour(WebPlace place, int hour)
        {
            Site site = place.Site;
            int plan = Speed(place.Reg, -1);
            site.Down = plan > 0 && Speed(place.Reg, hour) == 0;
            if (plan != site.Mbps)
            {
                site.Mbps = plan;
                Replan(place);
            }
            string key = place.Shop.Place;
            if (plan == 0 && _netNone.Add(key))
            {
                Tell(NotificationType.Warning, "reallife_online_note_no_net", new Dictionary<string, string> { { "name", place.Shop.Name }, { "business", place.Business } },
                    TabShops, "shop:" + key);
            }
            else if (plan != 0 && _netNone.Remove(key) && plan > 0)
            {
                Tell(NotificationType.Success, "reallife_online_note_net_back", new Dictionary<string, string>
                {
                    { "name", place.Shop.Name }, { "most", OnlineRules.Most(place.Shop.Quality, plan).ToString() }
                }, TabShops, "shop:" + key);
            }
        }

        /// <summary>A building's plan speed for the orders over it, as the day's orders read it (NetSpeedRules); for the page that opens a shop.</summary>
        public static int PlanMbps(BuildingRegistration reg)
        {
            return Speed(reg, -1);
        }

        /// <summary>The orders a day the online shop at a building could take at a speed; -1 with no online shop there.</summary>
        public int MostAt(BuildingRegistration reg, int mbps)
        {
            if (!_running || reg == null)
            {
                return -1;
            }
            for (int i = 0; i < _places.Count; i++)
            {
                if (ReferenceEquals(_places[i].Reg, reg))
                {
                    return OnlineRules.Most(_places[i].Shop.Quality, mbps);
                }
            }
            return -1;
        }

        /// <summary>Fills in the buildings the player's online shops run from; false while the feature is not running.</summary>
        public bool Buildings(List<BuildingRegistration> into)
        {
            into.Clear();
            if (!_running || _broken)
            {
                return false;
            }
            for (int i = 0; i < _places.Count; i++)
            {
                if (_places[i].Reg != null)
                {
                    into.Add(_places[i].Reg);
                }
            }
            return true;
        }
    }
}
