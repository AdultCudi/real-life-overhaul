using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a move of the city's is, as Flare in the city lists it.</summary>
    public enum FlareMoveKind : byte
    {
        Outlet = 0,   // a rival opens a shop of the same kind in the district of one of the player's
        ToLet = 1     // premises the game has to let
    }

    /// <summary>A rival's new shop or the premises to let, as the city page lists them; read when the page is written, never kept.</summary>
    public struct FlareCityMove
    {
        public FlareMoveKind Kind;
        public string District;     // the game's own id of the district, or null for the whole city
        public string Name;         // an outlet: the player's business it competes with
        public string By;           // an outlet: the rival who opens it
        public int Day;             // the day it was announced
        public int Count;           // premises to let
    }

    public static partial class FlareBridges
    {
        /// <summary>The rivals' new shops and the premises to let, into the list given. Default: none.</summary>
        public static Action<List<FlareCityMove>> CityMoves;
    }

    /// <summary>
    /// Flare's director, what the kit face's working pages read and press beyond the notifications: the profile a player writes, a tier passed on the
    /// day's round, the creator income paid, the followers of an account by district, the week of what posts did, the business of the same kind
    /// across the street and the city's moves. Nothing per frame: each is worked out when a page is written, and the district counts once a day.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private int[] _districtCounts = new int[0];
        private string _districtKey;
        private int _districtDay = -1;
        private int _districtTotal;
        private int[] _districtOrder = new int[FlareDeskRules.DistrictsShown];
        private readonly List<FlareCityMove> _moves = new List<FlareCityMove>(8);

        /// <summary>The player writes their profile: the words about them and the picture across its head (null for the newest picture). Kept as the rules clean them.</summary>
        public bool SetProfile(string bio, string banner)
        {
            if (!Running)
            {
                return false;
            }
            _file.Bio = FlareDeskRules.CleanBio(bio);
            _file.Banner = FlareDeskRules.CleanBanner(banner);
            Logger?.Info("Flare: the player wrote their profile (" + (_file.Bio != null ? _file.Bio.Length + " characters" : "no words") + ", " + (_file.Banner != null ? "a banner picked" : "the newest picture across it") + ").");
            Changed();
            return true;
        }

        // A tier passed since last looked at, on the day's round: told once, and kept as a milestone with its day.
        private void TierDay(int day)
        {
            if (!FlareNoticeRules.TierUp(_file, day, out int tier))
            {
                return;
            }
            Notice(NotificationType.Success, "reallife_flare_use_tier", _openFeed, "followers", FlareText.N(_file.Followers));
            Logger?.Info("Flare: the account passed " + (FlareTier)tier + " on day " + day + " with " + _file.Followers + " followers.");
        }

        /// <summary>The last creator income paid and all that were, cents.</summary>
        public void CreatorPaid(out long last, out long total)
        {
            last = CityOn ? _city.CreatorPaid : 0;
            total = CityOn ? _city.CreatorTotal : 0;
        }

        /// <summary>The days until the next creator income, or -1 below the tier that is paid one (Influencer) or without the residents' side.</summary>
        public int CreatorIn()
        {
            if (!CityOn || Save == null || (int)_file.Tier < FlareCityRules.CreatorFrom)
            {
                return -1;
            }
            return _city.CreatorDay < 0 ? FlareCityRules.CreatorDays : Math.Max(0, _city.CreatorDay + FlareCityRules.CreatorDays - Save.Day);
        }

        /// <summary>
        /// The followers of an account in the city by district, largest first: the district of each (its game id), its count, and what the
        /// rest hold together. Worked out once a game day for an account; false without the residents' side or without followers.
        /// </summary>
        public bool FollowersInDistricts(string key, List<string> names, List<int> counts, out int rest, out int total)
        {
            rest = 0;
            total = 0;
            names.Clear();
            counts.Clear();
            PopulationState s = CityOn ? Residents : null;
            if (s == null || Save == null || s.Districts.Count == 0)
            {
                return false;
            }
            if (_districtKey != key || _districtDay != Save.Day || _districtCounts.Length != s.Districts.Count)
            {
                _districtKey = key;
                _districtDay = Save.Day;
                if (_districtCounts.Length != s.Districts.Count)
                {
                    _districtCounts = new int[s.Districts.Count];
                }
                int account = key == "me" ? FlareCityState.PlayerAccount : _city.AccountOf(key);
                _districtTotal = FlareCityRules.FollowersByDistrict(_city, s, account, _districtCounts);
            }
            total = _districtTotal;
            int n = FlareDeskRules.TopDistricts(_districtCounts, _districtOrder, out rest);
            for (int i = 0; i < n; i++)
            {
                names.Add(s.Districts[_districtOrder[i]].Id);
                counts.Add(_districtCounts[_districtOrder[i]]);
            }
            return total > 0;
        }

        /// <summary>The name of a population district by its number, as the game writes it; the whole city for none.</summary>
        public string DistrictLabel(int district)
        {
            PopulationState s = CityOn ? Residents : null;
            return s != null && district >= 0 && district < s.Districts.Count ? FlareText.District(s.Districts[district].Id) : FlareText.District(null);
        }

        /// <summary>The hours a day a business of the player's is open on average, or -1 when the residents do not know it yet.</summary>
        public int OpenHours(string place)
        {
            PopulationState s = CityOn ? Residents : null;
            int b = s != null ? _cityPass.BusinessOfPlace(place) : -1;
            return b >= 0 && b < s.Businesses.Count ? s.Businesses.Hours[b] : -1;
        }

        /// <summary>The business of the same kind in the same district that the residents rate best, with its rating in tenths of a star; false with none.</summary>
        public bool CompetitorOf(string place, out string name, out int tenths)
        {
            name = null;
            tenths = 0;
            PopulationState s = CityOn ? Residents : null;
            int b = s != null ? _cityPass.BusinessOfPlace(place) : -1;
            int c = b >= 0 ? FlareCityRules.Competitor(_city, s, b) : -1;
            if (c < 0)
            {
                return false;
            }
            name = FlareCityGame.NameOf(s.Businesses.Key[c]);
            tenths = _city.Rating(c);
            return true;
        }

        /// <summary>The week of what the posts and ads did in the city: residents who saw, visits it brought and takings (cents). False without the residents' side.</summary>
        public bool WeekInCity(out int seen, out int came, out long spent)
        {
            seen = came = 0;
            spent = 0;
            if (!CityOn || Save == null)
            {
                return false;
            }
            FlareDeskRules.WeekTotals(_city.Funnels, Save.Day, out seen, out came, out spent);
            return true;
        }

        /// <summary>The rivals' new shops and the premises to let; the director's list, filled again on each call.</summary>
        public List<FlareCityMove> CityMoves()
        {
            _moves.Clear();
            try
            {
                FlareBridges.CityMoves?.Invoke(_moves);
            }
            catch (Exception e)
            {
                _moves.Clear();
                Logger?.Error(e);
            }
            return _moves;
        }

        /// <summary>The business of someone else's the player stands inside (a review or a report is about it); false when they stand in none or in their own.</summary>
        public bool StandingIn(out string place, out string name)
        {
            place = name = null;
            BuildingRegistration inside = FlareLiveGame.InsideAny();
            if (inside == null || GoingPublic.Mine(inside))
            {
                return false;
            }
            place = FlareGame.Place(inside);
            name = FlareGame.Name(inside);
            return true;
        }

        /// <summary>The district the player stands in, as the game writes its id; null when they are in none.</summary>
        public string PlayerDistrictNow() => FlareBridges.Ask(FlareBridges.PlayerDistrict, null);

        /// <summary>The hour of the day it is now.</summary>
        public int HourNow => Save != null ? Save.Hour : 12;

        /// <summary>
        /// The ad of another's that shares the reach with an ad at a business of the player's: a rival's or a city business's ad running now in the
        /// same district (the pools they draw on are shared). False with none, or without the residents' side.
        /// </summary>
        public bool SharedAdWith(string place, out string name)
        {
            name = null;
            PopulationState s = CityOn ? Residents : null;
            int b = s != null ? _cityPass.BusinessOfPlace(place) : -1;
            if (b < 0 || b >= s.Businesses.Count || Save == null)
            {
                return false;
            }
            FlareAd other = FlareDeskRules.SharedWith(_city.Ads, s.Businesses.District[b], Save.Day, place);
            name = other != null ? other.Name : null;
            return other != null;
        }

        /// <summary>The kinds of audience bars a hire sheet draws: the district of the influencer's followers in the city, largest first. False without the residents' side.</summary>
        public bool AudienceOf(int influencer, List<string> names, List<int> counts, out int rest, out int total)
        {
            return FollowersInDistricts("i:" + influencer.ToString(CultureInfo.InvariantCulture), names, counts, out rest, out total);
        }
    }
}
