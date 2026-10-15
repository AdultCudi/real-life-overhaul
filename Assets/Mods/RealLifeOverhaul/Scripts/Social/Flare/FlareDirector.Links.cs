using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What a post does outside Flare, each through one bridge that Wiring/FlareLinks fills in (FlareBridges): talk about a
    /// business is a share of its customers for three days, inside business pressure's own bounds and Flare's cap of 6%;
    /// a viral post about one raises the game's own hype for a product it sells (once a fortnight) and gives the City
    /// Ledger a piece (once a day); a friend tagged is closer for it; an honest answer to a scandal wins back part of what
    /// it cost the brand; an arrest trends; a picture posted while wanted draws the police's eye for a day; a post on a
    /// rota shift is a complaint when the boss follows the player. Each is logged once, when it happens.
    ///
    /// Also here: what the company asks of a sponsored post (its quote) and is told when one is booked, what business
    /// pressure asks of a business's customers (<see cref="FootfallOf"/>), and what brands ask of a scandal.
    /// COST: nothing per frame; <see cref="FootfallOf"/> is a walk over the talk there is (none, most days) each time a
    /// business's figure is written.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly List<string> _talked = new List<string>(8);

        // After a post: everything it does elsewhere.
        private void Influence(FlarePost p, FlareDraft d)
        {
            int day = p.Day;
            int buzz = FlareRules.BuzzOf(p);
            if (buzz != 0 && FlareLiveRules.FirstToday(_file.Net, day, FlareLiveRules.TalkKey(p.Place)))   // (a business talked about once a day, however many posts)
            {
                int now = FlareRules.AddBuzz(_file, p.Place, buzz, day, FlareRules.BuzzDays);
                Logger?.Info("Flare: the post about " + p.Name + " is talked about: " + now + "% more customers there until day " + (day + FlareRules.BuzzDays - 1) + ".");
                Rewrite(p.Place, false);
            }
            if (p.Viral)
            {
                Viral(p, day);
            }
            if (p.Tag >= 0 && FlareBridges.Tagged != null && FlareLiveRules.FirstToday(_file.Net, day, FlareLiveRules.FriendKey('T', p.Tag)))   // (closer once a day)
            {
                FlareBridges.Tagged(p.Tag, p.Source == FlareSource.Selfie && d.Person == p.Tag, !p.Bad);
            }
            if (p.Source == FlareSource.Scandal && p.Tone == FlareTone.Honest && d.Arg > 0 && FlareBridges.Answered != null)
            {
                int back = FlareBridges.Answered(p.Place, d.Arg);
                Logger?.Info("Flare: the honest answer about " + p.Name + " won back " + back + " of the " + d.Arg + " hundredths of recognition it could.");
            }
            if ((p.Source == FlareSource.Selfie || p.Source == FlareSource.Photo) && _file.NoticedDay != day && FlareBridges.Ask(FlareBridges.Wanted, false))
            {
                _file.NoticedDay = day;
                FlareBridges.Noticed?.Invoke();
                Logger?.Info("Flare: a picture posted while wanted: the police watch more closely for the rest of the day.");
            }
            int shift = FlareBridges.Ask(FlareBridges.OnShift, -1);
            if (shift >= 0 && _file.ComplaintDay != day && FlareRules.BossFollows(_file.Seed, shift, _file.Tier))
            {
                _file.ComplaintDay = day;
                FlareBridges.Complaint?.Invoke();
                Logger?.Info("Flare: the boss follows the player and saw a post made on shift: a complaint at the next review.");
            }
        }

        // A viral post: the game's own hype for a product of the business it was about, and a piece in the morning paper.
        private void Viral(FlarePost p, int day)
        {
            BuildingRegistration viral = p.Place != null ? FlareGame.Business(p.Place) : null;
            if (viral != null)
            {
                _file.ViralNiche = (int)FlareCast.NicheOf(viral.businessTypeName);   // a viral business draws an influencer of its kind for a month
                _file.ViralNicheDay = day;
            }
            if (p.Place != null && FlareRules.MayHype(_file, day))
            {
                string product = FlareGame.Hype(FlareGame.Business(p.Place));
                if (product != null)
                {
                    _file.HypeDay = day;
                    Logger?.Info("Flare: the viral post about " + p.Name + " has the city talking about " + product + ": the game's own hype for it in the district.");
                }
            }
            if (_file.StoryDay != day && FlareBridges.Story != null)
            {
                _file.StoryDay = day;
                FlareBridges.Story("reallife_flare_news_viral");
                Logger?.Info("Flare: a viral post is in tomorrow's City Ledger.");
            }
        }

        // An arrest trends: followers and reputation (once a day, from Local on).
        private void Trended(int day)
        {
            int before = _file.Reputation;
            int lost = FlareRules.Arrest(_file, day);
            if (lost > 0)
            {
                Logger?.Info("Flare: the arrest is trending: " + lost + " followers lost, reputation " + before + " to " + _file.Reputation + ".");
            }
        }

        // The businesses talked about now, into the list given (before the day takes some talk away).
        private void Talked(List<string> into)
        {
            into.Clear();
            for (int i = 0; i < _file.Buzz.Count; i++)
            {
                if (!into.Contains(_file.Buzz[i].Place))
                {
                    into.Add(_file.Buzz[i].Place);
                }
            }
        }

        // A business's figure written again by business pressure's own writer, now that the talk about it has changed.
        private void Rewrite(string place, bool newDay)
        {
            BuildingRegistration reg = FlareBridges.Footfall != null ? FlareGame.Business(place) : null;
            if (reg != null)
            {
                FlareBridges.Footfall(reg, newDay);
            }
        }

        private void Rewrite(List<string> places, bool newDay)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (_file.BuzzAt(places[i], Save.Day) != 0 || newDay)
                {
                    Rewrite(places[i], newDay);
                }
            }
        }

        // ---------------------------------------------------------------- what the others ask

        /// <summary>
        /// What Flare's talk adds to a business's customers now, per cent (asked each time business pressure writes the
        /// figure: Wiring/FlareLinks), and a point more in a district the City Office's cleaners are going round. Nothing
        /// while Flare is not running or nobody talks about a business.
        /// </summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            FlareDirector d = Instance;
            if (d == null || !d.Running || reg == null || SaveGameManager.Current == null || (d._file.Buzz.Count == 0 && d._file.Net.Watches.Count == 0))
            {
                return 0;
            }
            int day = SaveGameManager.Current.Day;
            int clean = d._file.Net.Watches.Count > 0 && d._file.Net.WatchIn(reg.Neighborhood, day, FlareOrder.Cleanup) != FlareOrder.None ? FlareLiveRules.CleanFootfall : 0;
            return (d._file.Buzz.Count > 0 ? d._file.BuzzAt(FlareGame.Place(reg), day) : 0) + clean;
        }

        /// <summary>For brands: how a scandal at a shop spreads, thousandths of its loss, by the account's reputation.</summary>
        public int ScandalScale()
        {
            return Running ? FlareRules.ScandalScale(_file.Reputation) : 1000;
        }

        /// <summary>For brands: a scandal has cost a brand at a shop: an answer can be posted, and an honest one wins back part of it.</summary>
        public void Scandal(string place, string name, int lost, int scale)
        {
            Happened(FlareSource.Scandal, -1, place, name, FlareRules.Answered(lost, scale));
            if (Running)
            {
                FlareBridges.MarketBuzz?.Invoke(place, -1);   // the talk reaches the market for its trade
            }
        }

        /// <summary>For the company: a sponsored post at a business can be booked now (from Local on), at these shares of its base cost and of its customers.</summary>
        public bool SponsoredQuote(out int cost, out int reach)
        {
            reach = Running ? FlareRules.SponsoredReach(_file.Followers) : 0;
            cost = reach > 0 ? FlareRules.SponsoredCost(reach, _file.Reputation) : 1000;
            if (reach == 0)
            {
                reach = 1000;
                return false;
            }
            return true;
        }

        /// <summary>For the company: a sponsored post has been booked and paid for a business: it is posted on the account.</summary>
        public void Sponsored(string place, int from, int to, int percent)
        {
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                BuildingRegistration reg = FlareGame.Business(place);
                int id = _file.NextId++;
                _file.Posts.Add(new FlarePost
                {
                    Id = id, Day = Save.Day, Hour = Save.Hour, Author = FlareAuthor.Player, Source = FlareSource.Sponsored, Tone = FlareTone.Cheerful, Place = place,
                    Name = reg != null ? FlareGame.Name(reg) : place, Arg = percent, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29),
                    Quality = 1000, Reach = FlareRules.Reach(_file.Followers, 1000, 1000)
                });
                FlareRules.Prune(_file);
                Logger?.Info("Flare: a sponsored post for " + place + " is posted: days " + from + " to " + to + ", " + percent + "% more customers, as the company's campaign measures it.");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a sponsored post", e);
            }
        }

        /// <summary>For the council: what the account's word adds to an endorsement, thousandths of the most it may.</summary>
        public int EndorseShare()
        {
            return Running ? FlareRules.EndorseShare(_file.Followers, _file.Reputation) : 0;
        }
    }
}
