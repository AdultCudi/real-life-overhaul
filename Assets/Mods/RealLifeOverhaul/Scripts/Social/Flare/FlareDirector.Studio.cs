using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the game studio on Flare (the brief's sixth step; Tech/Studio/StudioBridges, wired in Wiring.FlareCityLinks): a studio's news
    /// (an announcement, a trailer, the expo, previews, a launch, the critics' score, an update, an add-on, an award) is the studio's own post, heard
    /// by its followers and residents who play games; a devlog brings hype from the residents it excites; a crowdfunding campaign brings pledges
    /// from the residents who follow the studio or the player and play games, and from followers beyond the city. Without the residents' side, the
    /// player's followers stand in. COST: nothing per frame; a post heard on the studio's own beat.
    /// </summary>
    public sealed partial class FlareDirector
    {
        /// <summary>A studio's news on Flare (StudioBridges.Talk): the kind, the studio's place, the game, a figure (its score).</summary>
        public void StudioNews(int news, string place, string game, long figure)
        {
            try
            {
                FlarePost p = StudioPost(place, FlareCityRules.StudioNewsArg + Math.Max(0, Math.Min(news, 9)), game, figure);
                if (p != null)
                {
                    CityHear(StudioCityPost(p, place), null);   // heard now, or once the day's indexes are made
                    Logger?.Info("Flare: the studio " + p.Name + " posts its news about " + game + " (" + news + ").");
                }
            }
            catch (Exception e)
            {
                Fault("a studio's news", e);
            }
        }

        /// <summary>A devlog on Flare (StudioBridges.Devlog): the studio's post, shared by the player; the hype it brought, thousandths of the audience.</summary>
        public int StudioDevlog(string place, string game)
        {
            try
            {
                FlarePost p = StudioPost(place, FlareCityRules.DevlogArg, game, 0);
                if (p == null)
                {
                    return 0;
                }
                PopulationState s = StudioResidents();
                int interested = s != null ? StudioHeard(s, p, place, true) : 0;
                int hype = s != null ? FlareCityRules.DevlogHype(interested, FlareCityRules.Gamers(_city, s)) : FlareCityRules.DevlogHypeFollowers(_file.Followers);
                Logger?.Info("Flare: a devlog of " + game + " by " + p.Name + ": " + (s != null ? interested + " residents who play games were interested" : _file.Followers + " followers") + ", hype "
                             + hype + " thousandths.");
                return hype;
            }
            catch (Exception e)
            {
                Fault("a devlog", e);
                return 0;
            }
        }

        /// <summary>A crowdfunding campaign on Flare (StudioBridges.Crowdfund): the cents pledged, never more than the goal.</summary>
        public long StudioCrowdfund(string place, long goal)
        {
            try
            {
                FlarePost p = StudioPost(place, FlareCityRules.CrowdfundArg, null, goal / 100);
                if (p == null)
                {
                    return 0;
                }
                PopulationState s = StudioResidents();
                if (s != null)
                {
                    StudioHeard(s, p, place, true);
                }
                int beyond = Math.Max(0, _file.Followers - _city.CityFollowers);
                long pledged = s != null
                    ? FlareCityRules.Pledges(_city, s, FlareCityState.PlayerAccount, _city.Account(StudioAccount(place)), beyond, goal, p.Seed, _cityPass.Scratch)
                    : FlareCityRules.PledgesFollowers(_file.Followers, goal);
                Logger?.Info("Flare: " + p.Name + "'s crowdfunding campaign: " + pledged / 100 + " pledged of " + goal / 100
                             + (s != null ? ", by the residents who play games and the player's followers beyond the city." : "."));
                return pledged;
            }
            catch (Exception e)
            {
                Fault("a crowdfunding campaign", e);
                return 0;
            }
        }

        private static string StudioAccount(string place) => "s:" + place;

        // A studio's post as the residents hear it: by its followers, and residents of its district who come across it, those who play games interested.
        private CityPost StudioCityPost(FlarePost p, string place)
        {
            PopulationState s = Residents;
            BuildingRegistration reg = FlareGame.Business(place);
            string home = reg != null ? GameRegistrations.District(reg) : null;
            return new CityPost
            {
                Id = p.Id, Account = _city.Account(StudioAccount(place)), Business = -1, Kind = FlareCityRules.GamesKind, Sign = 0, Quality = 1000, Fit = 1000, Seed = p.Seed,
                District = home != null && s != null ? s.DistrictOf(home) : -1
            };
        }

        // The residents when a studio's post can be heard now: not while the day's pass runs (it holds the scratch and the follows), nor before its first indexes.
        private PopulationState StudioResidents()
        {
            return CityOn && !_cityPass.Busy && _city.Indexed >= 0 ? Residents : null;
        }

        // The studio's own post: a business post of the player's studio, in the feed at once.
        private FlarePost StudioPost(string place, int arg, string game, long figure)
        {
            BuildingRegistration reg = Running && Save != null && place != null ? FlareGame.Business(place) : null;
            if (reg == null)
            {
                return null;
            }
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Day = Save.Day, Hour = Save.Hour, Minute = FlareGame.MinuteOfDay % 60, Author = FlareAuthor.Business, Source = FlareSource.BusinessPost, Place = place,
                Name = FlareGame.Name(reg), Arg = arg, Text = game, Who = (int)Math.Max(0, Math.Min(int.MaxValue, figure)), Topic = "games",
                Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = 300 + _file.Followers / 20
            };
            p.Likes = p.Reach / 12;
            _file.Posts.Add(p);
            FlareRules.Prune(_file);
            ShareChanged(p);
            Changed();
            return p;
        }

        // The studio's post heard by its followers and residents who play games (and, shared by the player, the player's): the interested, in all.
        private int StudioHeard(PopulationState s, FlarePost p, string place, bool founder)
        {
            CityPost post = StudioCityPost(p, place);
            int interested = FlareCityRules.Hear(_city, s, post, Save.Day, _cityPass.AccountBiz, _cityPass.Scratch, null).Interested;
            if (founder)
            {
                post.Account = FlareCityState.PlayerAccount;
                post.Quality = FlareCityRules.FounderShareQuality;
                post.Seed = p.Seed + 1;
                interested += FlareCityRules.Hear(_city, s, post, Save.Day, _cityPass.AccountBiz, _cityPass.Scratch, null).Interested;
            }
            return interested;
        }
    }
}
