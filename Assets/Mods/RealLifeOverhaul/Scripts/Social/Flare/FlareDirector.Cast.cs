using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The city's influencers, brands and candidates on Flare (FlareCast): the cast made on the first day, their month
    /// and their daily posts, hiring them for a business (the company's campaign of the deal's kind, booked through
    /// Wiring/FlareLinks), what a hire does when it goes late, off message or backfires, a hired influencer's scandal,
    /// a star the player can meet, and following accounts.
    ///
    /// NOTIFICATIONS (the owner's rule): nothing notifies unless the player follows it. The accounts they follow send
    /// one notification a day at most, however many posted ("3 new posts from accounts you follow"); the others only
    /// show in the Feed. What needs the player (a hired influencer's scandal, a viral moment) is one notification a day
    /// at most (FlareRules.MayNotify); the option switches all of it off.
    ///
    /// COST: nothing per frame. The day: a walk over ten influencers and the player's businesses; the month: the same
    /// and a look for staff.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly List<string> _districts = new List<string>(16);
        private readonly List<string> _rivals = new List<string>(8);
        private readonly List<string> _candidates = new List<string>(4);
        private readonly List<string> _brands = new List<string>(8);
        private readonly List<FlareShop> _shops = new List<FlareShop>(16);
        private readonly List<FlarePost> _visits = new List<FlarePost>(4);
        private readonly List<FlareCastNews> _castNews = new List<FlareCastNews>(16);
        private readonly List<int> _plan = new List<int>(3);
        private readonly int[] _heat = new int[(int)FlareNiche.Business + 1];

        /// <summary>The brands' and the candidates' accounts, as read when the app opened.</summary>
        public IReadOnlyList<string> BrandAccounts => _brands;
        public IReadOnlyList<string> CandidateAccounts => _candidates;

        // The hire being quoted or booked: the company asks Flare for its figures while it books (CompanyBridges.Outside).
        private string _hirePlace;
        private int _hireKind = -1;
        private readonly List<int> _hireWho = new List<int>(3);

        // A new account, or one from before the influencers: the cast is made, by its own seed.
        private void Cast(FlareFile file, int day)
        {
            if (file.CastSeed == 0)
            {
                unchecked
                {
                    file.CastSeed = file.Seed * 31 + 7;
                }
            }
            FlareGame.Districts(_districts);
            if (file.Influencers.Count == 0)
            {
                FlareCast.Make(file, day, _districts);
                file.MonthDay = day;
                Logger?.Info("Flare: the city's influencers: " + CastLine(file) + ".");
            }
        }

        private static string CastLine(FlareFile file)
        {
            var b = new System.Text.StringBuilder(400);
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                FlareInfluencer n = file.Influencers[i];
                b.Append(i > 0 ? "; " : string.Empty).Append(n.Handle).Append(" (").Append(n.Niche).Append(", ").Append(n.Style).Append(", ").Append(n.Followers).Append(" followers, ")
                    .Append(n.Engagement).Append(" bp, reliability ").Append(n.Reliability).Append(n.Quit ? ", quit" : string.Empty).Append(n.Star ? ", a star" : string.Empty).Append(')');
            }
            return b.ToString();
        }

        // The cast's part of the day: hires that ran out, the month when it is due, the day's posts, the one notification for followed accounts.
        private void CastDay(int day)
        {
            for (int i = _file.Hires.Count - 1; i >= 0; i--)
            {
                FlareHire h = _file.Hires[i];
                if (!h.Ended && day > h.To)
                {
                    h.Ended = true;
                }
                if (h.Ended && day - h.To > FlareCast.MonthDays)
                {
                    _file.Hires.RemoveAt(i);
                }
                else if (h.On(day) && day == h.From)
                {
                    Started(h, day);
                }
            }
            if (_file.MonthDay < 0 || day - _file.MonthDay >= FlareCast.MonthDays)
            {
                Month(day);
            }
            FlareGame.Shops(_file, _shops);
            FlareGame.Rivals(_rivals);
            Names(FlareBridges.Candidates, _candidates);
            Names(FlareBridges.Brands, _brands);
            _visits.Clear();
            FlareCast.Day(_file, day, _shops, _rivals, _candidates, _brands, _visits);
            for (int i = 0; i < _visits.Count; i++)
            {
                int now = FlareRules.AddBuzz(_file, _visits[i].Place, FlareCast.TalkVisit, day, FlareRules.BuzzDays);
                Logger?.Info("Flare: " + HandleOf(_visits[i].Who) + " visited " + _visits[i].Name + " and posted about it: " + now + "% more customers there.");
                Rewrite(_visits[i].Place, true);
            }
        }

        // The one notification of a day for the accounts the player follows, in the morning once the paper and the market have posted.
        private void FollowedNotice(int day, int hour)
        {
            if (hour < 8 || _file.FollowNoticeDay == day)
            {
                return;
            }
            int followed = FlareCast.FollowedPosts(_file, day);
            if (FlareCast.FollowNotice(_file, day, followed, Settings.SocialMediaNotices))
            {
                Tell(NotificationType.Info, followed == 1 ? "reallife_flare_note_followed_one" : "reallife_flare_note_followed", _openFeedTab, "count", followed.ToString());
            }
        }

        private static void Names(Action<List<string>> bridge, List<string> into)
        {
            into.Clear();
            try
            {
                bridge?.Invoke(into);
            }
            catch (Exception)
            {
                into.Clear();
            }
        }

        // The influencers' month: what the city does draws newcomers; each grows, fades, quits, becomes a star or meets a scandal.
        private void Month(int day)
        {
            FlareGame.Shops(_file, _shops);
            Array.Clear(_heat, 0, _heat.Length);
            for (int i = 0; i < _shops.Count; i++)
            {
                _heat[(int)_shops[i].Niche] = Math.Min(1000, _heat[(int)_shops[i].Niche] + 250);   // the player's own businesses make a niche busy
            }
            if (FlareBridges.Ask(FlareBridges.NightLate, false))
            {
                _heat[(int)FlareNiche.Nightlife] = Math.Min(1000, _heat[(int)FlareNiche.Nightlife] + 300);   // the council lets the night go on later
            }
            FlareGame.Districts(_districts);
            _castNews.Clear();
            FlareCast.Month(_file, day, _heat, _people, FlareGame.HasStaff(), _districts, _castNews);
            for (int i = 0; i < _castNews.Count; i++)
            {
                CastNews(_castNews[i], day);
            }
        }

        private void CastNews(FlareCastNews news, int day)
        {
            FlareInfluencer inf = _file.Influencer(news.Id);
            if (inf == null)
            {
                return;
            }
            switch (news.Kind)
            {
                case FlareCastNews.What.Arrived:
                    Logger?.Info("Flare: a new influencer, " + inf.Handle + " (" + inf.Niche + ", " + inf.Style + ", " + inf.Followers + " followers"
                                 + (inf.Person >= 0 ? ", the player's friend " + PersonName(inf.Person) : inf.Staff ? ", one of the player's staff" : string.Empty) + ").");
                    break;
                case FlareCastNews.What.Star:
                    Logger?.Info("Flare: " + inf.Handle + " is a star: " + inf.Followers + " followers.");
                    if (inf.Person < 0 && FlareBridges.MeetStar != null)
                    {
                        inf.Person = FlareBridges.MeetStar(inf.Handle);
                        if (inf.Person >= 0)
                        {
                            Logger?.Info("Flare: " + inf.Handle + " turns out to be somebody the player can meet in the social life (person " + inf.Person + ").");
                        }
                    }
                    break;
                case FlareCastNews.What.Quit:
                    Logger?.Info("Flare: " + inf.Handle + " has stopped posting (" + news.Before + " followers).");
                    break;
                case FlareCastNews.What.Scandal:
                    Scandalised(inf, day);
                    break;
                case FlareCastNews.What.Grew:
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Flare check (the month): " + inf.Handle + " " + news.Before + " to " + news.After + " followers.");
                    }
                    break;
            }
        }

        // An influencer's scandal: the paper has it, and a business that hired them now is hurt with them (and its brand).
        private void Scandalised(FlareInfluencer inf, int day)
        {
            Logger?.Info("Flare: " + inf.Handle + " is caught in a scandal: reputation " + inf.Reputation + ", " + inf.Followers + " followers left.");
            if (_file.StoryDay != day && FlareBridges.Story != null)
            {
                _file.StoryDay = day;
                FlareBridges.Story("reallife_flare_news_influencer_scandal");
            }
            for (int i = 0; i < _file.Hires.Count; i++)
            {
                FlareHire h = _file.Hires[i];
                if (!h.On(day) || !h.Who.Contains(inf.Id))
                {
                    continue;
                }
                int now = FlareRules.AddBuzz(_file, h.Place, FlareCast.TalkBackfire, day, FlareRules.BuzzDays);
                FlareBridges.BrandScandal?.Invoke(h.Place);
                Logger?.Info("Flare: " + h.Name + " hired " + inf.Handle + ": the talk turns against it (" + now + "%), and its brand hears of it.");
                Rewrite(h.Place, false);
                if (FlareRules.MayNotify(_file, day, Settings.SocialMediaNotices))
                {
                    Tell(NotificationType.Warning, "reallife_flare_note_hire_scandal", _openBusinessTab, "handle", inf.Handle, "business", h.Name);
                }
            }
        }

        // A hire's first day: a late influencer's first post does not come; one who backfires turns the talk against the business.
        private void Started(FlareHire h, int day)
        {
            if (h.Late)
            {
                int now = FlareRules.AddBuzz(_file, h.Place, -Math.Min(h.Percent, FlareRules.BuzzMost), day, 1);
                Logger?.Info("Flare: the hire at " + h.Name + " starts a day late: the first day's customers are lost (" + now + "%).");
            }
            if (h.Backfired)
            {
                int now = FlareRules.AddBuzz(_file, h.Place, FlareCast.TalkBackfire, day, FlareRules.BuzzDays);
                FlareBridges.BrandScandal?.Invoke(h.Place);
                Logger?.Info("Flare: the hire at " + h.Name + " backfires: the post goes down badly (" + now + "%), and its brand hears of it.");
            }
            if (h.Late || h.Backfired)
            {
                Rewrite(h.Place, true);
            }
        }

        private string HandleOf(int id)
        {
            FlareInfluencer inf = _file != null ? _file.Influencer(id) : null;
            return inf != null ? inf.Handle : "?";
        }

        // ---------------------------------------------------------------- what the city's news had today

        /// <summary>Something was reported in the city today (Wiring/FlareLinks): local news may post about it.</summary>
        public void CityNews(int bit)
        {
            if (Running && Save != null)
            {
                if (_file.NewsDay != Save.Day)
                {
                    _file.NewsDay = Save.Day;
                    _file.NewsToday = 0;
                }
                _file.NewsToday |= bit;
                IssueFrom(bit);
            }
        }

        // ---------------------------------------------------------------- hiring, for the app

        /// <summary>Who would be in a deal: the one picked, or for a plan the two or three best for the business. False if the deal cannot be made.</summary>
        private bool Who(FlareShop shop, FlareDeal deal, int influencer, int day, List<int> into)
        {
            into.Clear();
            if (deal == FlareDeal.Plan)
            {
                return FlareCast.PlanPick(_file, shop.Niche, shop.District, day, into) > 0;
            }
            if (deal == FlareDeal.Launch && day - shop.OpenedDay > 14)
            {
                return false;
            }
            FlareInfluencer inf = _file.Influencer(influencer);
            if (inf == null || !inf.Active)
            {
                return false;
            }
            into.Add(inf.Id);
            return true;
        }

        /// <summary>The company's question while a hire is quoted or booked: its fee (at the start's prices) and its reach, thousandths of the kind's own figure.</summary>
        public bool HireFigures(int kind, string place, out int fee, out int reach)
        {
            fee = 0;
            reach = 1000;
            if (!Running || kind != _hireKind || place != _hirePlace || _hireWho.Count == 0)
            {
                return false;
            }
            FlareShop shop;
            if (!ShopOf(place, out shop))
            {
                return false;
            }
            int sum = 0;
            for (int i = 0; i < _hireWho.Count; i++)
            {
                FlareInfluencer inf = _file.Influencer(_hireWho[i]);
                if (inf == null)
                {
                    continue;
                }
                fee += FlareCast.Fee(inf, (FlareDeal)(kind - FlareCast.FirstDealKind));
                sum += FlareCast.Reach(inf, shop.Niche, shop.District);
            }
            reach = FlareRules.Clamp(sum, FlareCast.ReachLeast, FlareCast.ReachMost);
            return fee > 0;
        }

        private bool ShopOf(string place, out FlareShop shop)
        {
            FlareGame.Shops(_file, _shops);
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].Place == place)
                {
                    shop = _shops[i];
                    return true;
                }
            }
            shop = default(FlareShop);
            return false;
        }

        /// <summary>What a deal at a business would cost today and bring, as the company quotes it. False, with the reason's key, if it cannot be made.</summary>
        public bool HireQuote(string place, FlareDeal deal, int influencer, out int cost, out int percent, out string why)
        {
            return HireQuote(place, deal, influencer, out cost, out percent, out why, false);
        }

        // With 'keep' the deal stays in hand for the booking that follows; otherwise the company is offered nothing once the quote is had.
        private bool HireQuote(string place, FlareDeal deal, int influencer, out int cost, out int percent, out string why, bool keep)
        {
            cost = percent = 0;
            why = null;
            FlareShop shop;
            if (!Running || Save == null || !ShopOf(place, out shop))
            {
                why = "reallife_flare_hire_no_shop";
                return false;
            }
            if (!Who(shop, deal, influencer, Save.Day, _hireWho))
            {
                why = deal == FlareDeal.Plan ? "reallife_flare_hire_no_plan" : deal == FlareDeal.Launch ? "reallife_flare_hire_no_launch" : "reallife_flare_hire_no_one";
                return false;
            }
            _hirePlace = place;
            _hireKind = FlareCast.FirstDealKind + (int)deal;
            bool quoted = FlareBridges.Quote != null && FlareBridges.Quote(place, _hireKind, out cost, out percent, out why);
            if (!quoted && why == null)
            {
                why = "reallife_flare_hire_no_company";
            }
            if (!keep)
            {
                _hireKind = -1;
                _hirePlace = null;
            }
            return quoted;
        }

        /// <summary>The deal is made: booked and paid as the company's campaign, then kept here with how it will go.</summary>
        public bool Hire(string place, FlareDeal deal, int influencer)
        {
            int cost, percent;
            string why;
            if (!HireQuote(place, deal, influencer, out cost, out percent, out why, true) || FlareBridges.Book == null)
            {
                _hireKind = -1;
                _hirePlace = null;
                return false;
            }
            try
            {
                return FlareBridges.Book(place, _hireKind);   // the company calls back (Hired) once it has booked and paid
            }
            finally
            {
                _hireKind = -1;
                _hirePlace = null;
            }
        }

        /// <summary>For the company, through Wiring/FlareLinks: a hire has been booked and paid. It is kept, and how it goes is rolled now.</summary>
        public void Hired(string place, int kind, int from, int to, int percent)
        {
            if (!Running || Save == null || kind != _hireKind || _hireWho.Count == 0)
            {
                return;
            }
            try
            {
                FlareShop shop;
                ShopOf(place, out shop);
                var h = new FlareHire { Id = _file.NextId++, Kind = kind, Place = place, Name = shop.Name ?? place, From = from, To = to, Percent = percent };
                h.Who.AddRange(_hireWho);
                FlareInfluencer lead = _file.Influencer(h.Who[0]);
                if (lead != null)
                {
                    FlareCast.Outcome(h, lead, _file.CastSeed);
                }
                _file.Hires.Add(h);
                Logger?.Info("Flare: hired " + WhoLine(h) + " for " + h.Name + " (" + (FlareDeal)(kind - FlareCast.FirstDealKind) + ", days " + from + " to " + to + ", " + percent + "% more customers"
                             + (h.Late ? ", the first post a day late" : string.Empty) + (h.OffMessage ? ", off message" : string.Empty) + (h.Backfired ? ", it will backfire" : string.Empty) + ").");
                Changed();
            }
            catch (Exception e)
            {
                Fault("a hire", e);
            }
        }

        public string WhoLine(FlareHire h)
        {
            var b = new System.Text.StringBuilder(64);
            for (int i = 0; i < h.Who.Count; i++)
            {
                b.Append(i > 0 ? ", " : string.Empty).Append(HandleOf(h.Who[i]));
            }
            return b.ToString();
        }

        /// <summary>The player ends a deal (after a scandal): no more posts, and no more of the talk the scandal brings.</summary>
        public void EndHire(int id)
        {
            for (int i = 0; Running && i < _file.Hires.Count; i++)
            {
                FlareHire h = _file.Hires[i];
                if (h.Id == id && !h.Ended)
                {
                    h.Ended = true;
                    for (int k = _file.Buzz.Count - 1; k >= 0; k--)
                    {
                        if (_file.Buzz[k].Place == h.Place && _file.Buzz[k].Percent < 0)
                        {
                            _file.Buzz.RemoveAt(k);   // the talk against it stops with the deal
                        }
                    }
                    Logger?.Info("Flare: the deal with " + WhoLine(h) + " for " + h.Name + " is ended by the player.");
                    Rewrite(h.Place, false);
                    Changed();
                    return;
                }
            }
        }

        /// <summary>The player follows an account or stops; saved with the save.</summary>
        public void Follow(string key, bool on)
        {
            if (Running && FlareCast.Follow(_file, key, on))
            {
                Logger?.Info("Flare: the player " + (on ? "follows " : "no longer follows ") + key + ".");
                Changed();
            }
        }
    }
}
