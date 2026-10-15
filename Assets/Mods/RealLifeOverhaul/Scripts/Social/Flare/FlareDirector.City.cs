using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>A listed company's news as Flare hears it (Wiring/FlareLinks reads the stock market's own): its results, or the day's biggest move.</summary>
    public struct FlareMarketNote
    {
        public int Stock;
        public string Name;
        public bool Mover;           // the day's biggest move (else: results)
        public bool Beat;            // results better than the market expected
        public int MoveBp;
        public bool Held;            // the player holds shares in it
    }

    /// <summary>
    /// The rest of the city on Flare, each through one bridge Wiring/FlareLinks fills in (FlareBridges): somebody the
    /// player has still to meet answering a post and becoming a contact; the partner proud, cool or jealous of a post
    /// with somebody they could fall for, and minding never being posted; a friend whose texts wait minding the player
    /// posting all day; the stock market (companies' results in the Feed, the day's biggest mover in Trending, a post
    /// about shares the player holds with a disclosure, selling them soon after it went viral fined as market abuse, a
    /// buzz in the market for a viral trade or a brand's scandal); the City Ledger's own account; a post raising an issue
    /// in the council and the feed's mood pulling on the mayor's approval; a Celebrity's day in court. Each is small,
    /// capped and logged once, when it happens.
    ///
    /// COST: nothing per frame. The day: a few comparisons and the market's news of the day (a short list); the hour: the
    /// viral posts about shares (four at most) and, once a morning, the paper's headlines.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly List<FlareMarketNote> _market = new List<FlareMarketNote>(16);
        private readonly List<string> _headlines = new List<string>(8);
        private string _moverName;
        private int _moverBp, _moverDay = -1;

        /// <summary>The day's biggest mover on the market, for Trending; null if there is none.</summary>
        public string MoverName => Save != null && _moverDay >= Save.Day - 1 ? _moverName : null;
        public int MoverBp => _moverBp;

        // The city's part of the day.
        private void CityDay(int day)
        {
            int bad = FlareRules.Count(_file.NewsDay >= day - 1 ? _file.NewsToday : 0);
            int before = _file.MayorMood;
            if (FlareRules.Mood(_file, bad) != before && Meta.Dev.Verbose)
            {
                Logger?.Info("Flare check (the day): the feed's mood about the city's running " + before + " to " + _file.MayorMood + " (" + bad + " kind(s) of bad news).");
            }
            if (FlareRules.MeetsOnline(_file, day, _file.LastPostDay == day - 1) && FlareBridges.MeetOnline != null)
            {
                string name = FlareBridges.MeetOnline();
                if (name != null)
                {
                    _file.OnlineDay = day;
                    Logger?.Info("Flare: " + name + " answered a post of the player's and they swapped numbers: a new contact, met online.");
                }
            }
            bool partner = false;
            int waiting = -1;
            for (int i = 0; i < _people.Count; i++)
            {
                partner |= _people[i].Partner;
                waiting = waiting < 0 && _people[i].Unanswered && _people[i].Follows ? _people[i].Index : waiting;
            }
            if (FlareRules.NeverPosted(_file, day, partner) && FlareBridges.PartnerMinds != null)
            {
                FlareBridges.PartnerMinds();
                Logger?.Info("Flare: the player's partner has not been in a post for " + FlareRules.NeverPostedDays + " days while others have, and minds it.");
            }
            int yesterday = 0;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                yesterday += _file.Posts[i].Author == FlareAuthor.Player && _file.Posts[i].Day == day - 1 ? 1 : 0;
            }
            if (FlareRules.Neglect(_file, day, yesterday, waiting >= 0) && FlareBridges.FriendMinds != null)
            {
                FlareBridges.FriendMinds(waiting);
                Logger?.Info("Flare: " + PersonName(waiting) + " texted and is still waiting, and saw the player post " + yesterday + " times yesterday.");
            }
            FlareRules.AbuseDay(_file, day);
            Market(day);
        }

        // The market's news of the day: the companies post their results, the biggest move is Trending, results of a company the player holds can be posted.
        private void Market(int day)
        {
            _market.Clear();
            if (Following)
            {
                return;   // a guest that follows the host's feed has the market's posts from it
            }
            try
            {
                FlareBridges.Market?.Invoke(_market);
            }
            catch (Exception)
            {
                _market.Clear();
            }
            for (int i = 0; i < _market.Count; i++)
            {
                FlareMarketNote n = _market[i];
                if (n.Mover)
                {
                    _moverName = n.Name;
                    _moverBp = n.MoveBp;
                    _moverDay = day;
                    continue;
                }
                int id = _file.NextId++;
                _file.Posts.Add(new FlarePost
                {
                    Id = id, Day = day, Hour = 8, Author = FlareAuthor.Company, Source = FlareSource.Results, Name = n.Name, Arg = n.Beat ? 1 : 0,
                    Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = 5000, Likes = 40 + FlareRules.Roll(_file.Seed, id, 3) / 10
                });
                if (n.Held)
                {
                    Made(FlareRules.Draft(_file, FlareSource.Shares, day, 8, -1, null, n.Name, n.Stock));
                }
            }
            FlareRules.Prune(_file);
        }

        // On the hour: the morning paper's headlines on the City Ledger's account, and shares sold soon after a viral post about them.
        private void CityHour(int day, int hour)
        {
            if (hour >= 7 && _file.LedgerDay != day && FlareBridges.Headlines != null && !Following)
            {
                _file.LedgerDay = day;
                _headlines.Clear();
                try
                {
                    FlareBridges.Headlines(_headlines);
                }
                catch (Exception)
                {
                    _headlines.Clear();
                }
                for (int i = 0; i < _headlines.Count && i < 3; i++)
                {
                    int id = _file.NextId++;
                    _file.Posts.Add(new FlarePost
                    {
                        Id = id, Day = day, Hour = 7, Minute = 5 + i * 9, Author = FlareAuthor.Ledger, Source = FlareSource.Headline, Name = _headlines[i], Topic = "cityledger",
                        Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000, Reach = 8000, Likes = 60 + FlareRules.Roll(_file.Seed, id, 5) / 8
                    });
                }
                Shared_Changed(null);
                FlareRules.Prune(_file);
            }
            FollowedNotice(day, hour);
            for (int i = _file.ViralShares.Count - 1; i >= 0 && FlareBridges.Sold != null; i--)
            {
                FlareViralShares v = _file.ViralShares[i];
                if (FlareBridges.Sold(v.Stock, v.Day) && FlareRules.Abuse(_file, v.Stock, day) != null)
                {
                    FlareBridges.MarketAbuse?.Invoke(v.Name);
                    Logger?.Info("Flare: shares of " + v.Name + " sold within " + FlareRules.AbuseDays + " days of a viral post about them: a case of market abuse.");
                }
            }
        }

        // After a post: what it does to the partner, the council and the market.
        private void CityInfluence(FlarePost p, FlareDraft d)
        {
            if (p.Tag >= 0 && FlareBridges.PartnerSees != null)
            {
                FlarePerson tagged, partner = default(FlarePerson);
                bool has = false;
                for (int i = 0; i < _people.Count; i++)
                {
                    if (_people[i].Partner)
                    {
                        partner = _people[i];
                        has = true;
                    }
                }
                if (has && Person(p.Tag, out tagged) && tagged.Compatible && !tagged.Partner)
                {
                    FlareRules.PartnerView view = FlareRules.ViewOf(partner.Taste);
                    FlareBridges.PartnerSees(partner.Index, (int)view);
                    Logger?.Info("Flare: " + partner.First + " saw the post with " + tagged.First + " and is " + view.ToString().ToLowerInvariant() + " about it.");
                }
            }
            if (p.Source == FlareSource.Issue && FlareBridges.Issue != null)
            {
                int weight = FlareRules.IssueWeight(_file.Followers);
                FlareBridges.Issue(d.Arg, weight);
                _file.MayorMood = FlareRules.Clamp(_file.MayorMood - FlareRules.MoodIssuePost, -100, 100);
                Logger?.Info("Flare: the post about " + (FlareIssue)d.Arg + " raises it in the council by " + weight + " thousandths of a point; the feed's mood is " + _file.MayorMood + ".");
            }
            if (p.Source == FlareSource.Campaign && FlareBridges.Campaign != null)
            {
                int weight = FlareRules.IssueWeight(_file.Followers) * (p.Viral ? FlareRules.ViralTimes : 1);
                FlareBridges.Campaign(weight);
                Logger?.Info("Flare: the post about the player's campaign is worth " + weight + " thousandths of a point to it.");
            }
            if (p.Viral && p.Place != null && FlareBridges.MarketBuzz != null)
            {
                FlareBridges.MarketBuzz(p.Place, 1);
                Logger?.Info("Flare: the viral post about " + p.Name + " is a buzz on the stock market for its trade.");
            }
        }

        /// <summary>What happened in the city today and was worth posting about (issues), beside local news (FlareDirector.Cast.cs).</summary>
        private void IssueFrom(int bit)
        {
            if (Save == null || _file.IssueDay == Save.Day)
            {
                return;
            }
            FlareIssue issue = bit == FlareCast.NewsCrime || bit == FlareCast.NewsBreakIn ? FlareIssue.Safety : FlareIssue.Roads;
            if (bit == FlareCast.NewsCrime || bit == FlareCast.NewsBreakIn || bit == FlareCast.NewsAccident)
            {
                _file.IssueDay = Save.Day;
                Made(FlareRules.Draft(_file, FlareSource.Issue, Save.Day, Save.Hour, -1, null, null, (int)issue));
            }
        }

        /// <summary>For the council: the feed's mood about the city's running, points of the mayor's approval a day.</summary>
        public float ApprovalPull()
        {
            return Running ? FlareRules.ApprovalPull(_file.MayorMood) / 10f : 0f;
        }

        /// <summary>For the courts: what a lawyer asks more of the player, thousandths.</summary>
        public int Fame()
        {
            return Running ? FlareRules.Fame(_file) : 0;
        }
    }
}
