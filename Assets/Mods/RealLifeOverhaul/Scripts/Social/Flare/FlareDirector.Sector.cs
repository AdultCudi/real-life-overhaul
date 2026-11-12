using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the other tech firms on Flare (Tech/Sector/SectorBridges.Talk, wired in Wiring.FlareCityLinks): a firm of the player's posts
    /// its news on its own business page by the same rules as the game studio's (a launch, a benchmark, an award; an outage, a breach, an incident),
    /// heard by its followers and by the residents of its district who care for digital things, good news drawing them and bad keeping them away
    /// where the population counts the firm; a rival's news (a product shipped, a price cut, a tender won) is the city's own post, a rival
    /// business's, made on the host alone in a Going Public session as the rest of the city's feed. Seeded. COST: nothing per frame; a post heard,
    /// on the firms' own beat.
    /// </summary>
    public sealed partial class FlareDirector
    {
        /// <summary>A tech firm's news on Flare (SectorBridges.Talk): the kind (Tech.SectorNews), the firm's place (null for a rival's), a name and a figure.</summary>
        public void SectorNews(int news, string place, string name, long figure)
        {
            try
            {
                if (!Running || Save == null || news < 0 || news >= (int)Tech.SectorNews.Count)
                {
                    return;
                }
                FlarePost p = FlareCityRules.SectorRival(news) || place == null ? RivalPost(news, place, name, figure) : FirmPost(news, place, name, figure);
                if (p != null)
                {
                    Logger?.Info("Flare: " + p.Name + " posts its news (" + (Tech.SectorNews)news + (string.IsNullOrEmpty(p.Text) ? string.Empty : ", " + p.Text) + ").");
                }
            }
            catch (Exception e)
            {
                Fault("a tech firm's news", e);
            }
        }

        // A firm of the player's: its own business post, as a studio's, heard by its followers and the residents of its district.
        private FlarePost FirmPost(int news, string place, string name, long figure)
        {
            BuildingRegistration reg = FlareGame.Business(place);
            if (reg == null)
            {
                return null;
            }
            FlarePost p = SectorPost(FlareGame.Name(reg), place, news, name, figure);
            PopulationState s = CityOn ? Residents : null;
            if (s == null)
            {
                return p;   // the residents' side does not run: the post is the feed's alone
            }
            int b = _cityPass.BusinessOfPlace(place);
            string home = GameRegistrations.District(reg);
            var post = new CityPost
            {
                Id = p.Id, Account = b >= 0 && b < _cityPass.BusinessAccount.Length ? _cityPass.BusinessAccount[b] : _city.Account(StudioAccount(place)), Business = b,
                Kind = (int)Spend.Digital, Sign = b >= 0 ? FlareCityRules.SectorSign(news) : 0, Quality = 1000, Fit = 1000, Seed = p.Seed, District = home != null ? s.DistrictOf(home) : -1
            };
            CityHear(post, null);   // heard now, or once the day's indexes are made
            return p;
        }

        // A rival's news: the city's own post by the rival business, on the host alone (a guest has the host's).
        private FlarePost RivalPost(int news, string place, string name, long figure)
        {
            if (Following || string.IsNullOrEmpty(name))
            {
                return null;
            }
            BuildingRegistration lost = news == (int)Tech.SectorNews.TenderWon && place != null ? FlareGame.Business(place) : null;
            return SectorPost(name, null, news, lost != null ? FlareGame.Name(lost) : null, figure);
        }

        // The post itself, seeded from the save: a business post with the news's number, its name and figure (a tender's at today's prices, whole).
        private FlarePost SectorPost(string author, string place, int news, string text, long figure)
        {
            int id = _file.NextId++;
            long shown = news == (int)Tech.SectorNews.TenderWon ? figure * Prices.Level / 1000 / 100 : figure;
            var p = new FlarePost
            {
                Id = id, Day = Save.Day, Hour = Save.Hour, Minute = FlareGame.MinuteOfDay % 60, Author = FlareAuthor.Business, Source = FlareSource.BusinessPost, Place = place,
                Name = author, Arg = FlareCityRules.SectorNewsArg + news, Text = text, Who = (int)Math.Max(0, Math.Min(int.MaxValue, shown)), Topic = "tech",
                Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = 300 + _file.Followers / 20
            };
            p.Likes = p.Reach / 12;
            _file.Posts.Add(p);
            FlareRules.Prune(_file);
            ShareChanged(p);
            Changed();
            return p;
        }
    }
}
