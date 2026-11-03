using System;
using System.Collections.Generic;
using RealLifeOverhaul.Photos;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What a post pays, once an hour: the dry run becomes visitors for a few hours, reviews are answered, and a rival
    /// posts by the same rules. Nothing per frame. The hour's milestone step calls <see cref="UseHour"/>.
    /// </summary>
    public sealed partial class FlareDirector
    {
        /// <summary>The page a test opens the kit on, or -1 for home.</summary>
        public int KitPage = -1;

        /// <summary>The last dry run, for the screen. Not saved.</summary>
        public FlareExpect LastExpect;

        private string _picturePlace;
        private string _sidecarPic;
        private string _sidecarPlace;

        /// <summary>What this post would do. Nothing is saved.</summary>
        public FlareExpect Preview(FlareCompose c)
        {
            LastExpect = BuildExpect(c, Save != null ? Save.Hour : 12);
            return LastExpect;
        }

        private void NoteUse(FlarePost p, FlareCompose c)
        {
            if (p == null || _file == null || c == null)
            {
                return;
            }
            int hour = c.ScheduleHour >= 0 ? c.ScheduleHour : (Save != null ? Save.Hour : 12);
            if (!string.IsNullOrEmpty(p.Place) && p.Author == FlareAuthor.Player && p.Source != FlareSource.Reply)
            {
                FlareExpect ex = BuildExpect(c, hour);
                LastExpect = ex;
                if (FlareUseRules.KeepsBoost(c.PromoteReach, ex.Visitors, GuestPlace(p.Place)))
                {
                    int from = (Save != null ? Save.Day : 0) * 1440 + hour * 60;
                    if (c.ScheduleHour >= 0 && Save != null && c.ScheduleHour < Save.Hour)
                    {
                        from += 1440;
                    }
                    if (_file.Boosts.Count >= 24)
                    {
                        _file.Boosts.RemoveAt(0);
                    }
                    _file.Boosts.Add(new FlareBoost
                    {
                        Post = p.Id, Place = p.Place, FromMinute = from, Hours = ex.Hours, Visitors = ex.Visitors, Cents = ex.Cents, Percent = ex.Percent,
                        Hour = hour, Flags = (c.Pic != null || p.Pic != null ? 1 : 0) | (ex.Flopped ? 4 : 0) | (ex.WentViral ? 8 : 0) | (c.PromoteReach > 0 ? FlareBoost.PaidFlag : 0),
                        PicPlace = _picturePlace
                    });
                }
                FlareUseRules.NoteReach(_file.ReachDays, ref _file.ReachDay, Save != null ? Save.Day : 0, ex.Reach);
            }
            if (p.Source == FlareSource.Reply && !string.IsNullOrEmpty(p.Place))
            {
                int quality = c.Quality >= 0 ? c.Quality : FlareUseRules.AnswerQuality(p.Text);   // the reply sheet's option, else judged by its words
                if (_file.Answers.Count >= 64)
                {
                    _file.Answers.RemoveAt(0);
                }
                _file.Answers.Add(new FlareReviewNote { Key = p.Place, Day = p.Day, Quality = quality, Answer = p.Text, Stars = p.Arg });
                if (quality >= FlareUseRules.GoodAnswer)
                {
                    SoftenPlace(p.Place);
                }
            }
            int tier = (int)_file.Tier;
            if (tier > _file.TierSeen && tier >= (int)FlareTier.Local)
            {
                _file.TierSeen = tier;
                Tell(NotificationType.Success, "reallife_flare_use_tier", _openFeed, "followers", FlareText.N(_file.Followers));
            }
        }

        private FlareExpect BuildExpect(FlareCompose c, int hour)
        {
            int followers = _file != null ? _file.Followers : 0;
            bool photo = c != null && (!string.IsNullOrEmpty(c.Pic));
            string where = PicturePlace(c);
            var brief = new FlareBrief
            {
                Followers = followers, Hour = hour, Photo = photo, PhotoHere = photo && where != null && c.Place != null && where == c.Place,
                SameDistrict = c != null && c.Place != null ? 1 : -1, BasketCents = FlareUseRules.BasketDefault,
                Districts = c != null && c.PromoteMask != 0 ? Math.Max(1, FlareUseRules.MaskCount(c.PromoteMask)) : 1,
                PriceLevel = 1000, ReachPaid = c != null ? c.PromoteReach : 0, AdDays = c != null && c.PromoteDays > 0 ? c.PromoteDays : 1,
                Reliability = -1, Seed = _file != null ? _file.Seed : 1, RollKey = c != null ? c.Draft : 1,
                Guest = c != null && GuestPlace(c.Place)
            };
            int dayCustomers = DayCustomers(c != null ? c.Place : null);
            brief.DayCustomers = dayCustomers;
            return FlareUseRules.Expect(in brief);
        }

        // Life's sidecar place when the picture's file is there, otherwise the place the compose named.
        private string PicturePlace(FlareCompose c)
        {
            string saved = c != null ? c.Place : null;
            _picturePlace = FlareUseRules.PhotoPlace(ReadSidecar(c != null ? c.Pic : null), saved);
            return _picturePlace;
        }

        private string ReadSidecar(string pic)
        {
            if (pic == _sidecarPic)
            {
                return _sidecarPlace;
            }
            _sidecarPic = pic;
            _sidecarPlace = null;
            string path = FlarePictures.PathOf(pic);
            PhotoSidecar side;
            if (path != null && PhotoSidecarFile.Read(path, out side) && !string.IsNullOrEmpty(side.Place))
            {
                _sidecarPlace = side.Place;
            }
            return _sidecarPlace;
        }

        /// <summary>The stars kept for a business, by its place or by the population's key. Null when none have been written.</summary>
        public FlareStarRow StarsFor(string place)
        {
            if (!CityOn || string.IsNullOrEmpty(place))
            {
                return null;
            }
            FlareStarRow row = FlareUseRules.StarsOf(_city.StarRows, place);
            if (row != null || place.IndexOf('~') < 0)
            {
                return row;
            }
            return FlareUseRules.StarsOf(_city.StarRows, place.Replace('~', '|'));
        }

        /// <summary>Demand, per cent, for the kind a live trend or the newest placed post is about. Not a function of how many posts there are.</summary>
        public int PostDemand()
        {
            int kind = -1;
            if (CityOn)
            {
                for (int i = 0; i < _city.Trends.Count; i++)
                {
                    FlareTrend t = _city.Trends[i];
                    if (t != null && t.Kind != TrendKind.Genre)
                    {
                        kind = t.What;
                        break;
                    }
                }
            }
            if (kind < 0 && _file != null)
            {
                PopulationState s = Residents;
                if (s != null)
                {
                    for (int i = _file.Posts.Count - 1; i >= 0; i--)
                    {
                        string place = _file.Posts[i].Place;
                        if (string.IsNullOrEmpty(place))
                        {
                            continue;
                        }
                        int b = _cityPass.BusinessOfPlace(place);
                        if (b >= 0 && b < s.Businesses.Count)
                        {
                            kind = s.Businesses.Kind[b];
                            break;
                        }
                    }
                }
            }
            return FlareUseRules.TrendDemand(FlareUseRules.KindShare(kind));
        }

        /// <summary>Trends the city is running, for the city page.</summary>
        public int TrendRows => CityOn ? _city.Trends.Count : 0;

        /// <summary>Offers the city has open, for the city page.</summary>
        public int OfferRows => CityOn ? _city.Offers.Count : 0;

        private int DayCustomers(string place)
        {
            PopulationState s = Residents;
            int b = s != null && place != null ? _cityPass.BusinessOfPlace(place) : -1;
            if (b < 0 || b >= s.Businesses.Count)
            {
                return 0;
            }
            int room = s.Businesses.Room[b];
            return room > 0 ? Math.Max(40, room / 2) : 0;
        }

        private bool GuestPlace(string place)
        {
            PopulationState s = Residents;
            int b = s != null && place != null ? _cityPass.BusinessOfPlace(place) : -1;
            return b >= 0 && b < s.Businesses.Count && s.Businesses.Owner[b] == (byte)Owner.Guest;
        }

        private void SoftenPlace(string place)
        {
            if (!CityOn || _city == null || place == null)
            {
                return;
            }
            int b = _cityPass.BusinessOfPlace(place);
            if (b < 0 || b >= _city.BusinessCount)
            {
                return;
            }
            int n = _city.Districts;
            for (int d = 0; d < n; d++)
            {
                _city.Sour[b * n + d] = FlareUseRules.Soften(_city.Sour[b * n + d], FlareUseRules.GoodAnswer);
            }
            FlareCityRules.Answer(_city, b);
        }

        private void UseHour(int day, int hour)
        {
            if (_file == null)
            {
                return;
            }
            int minute = day * 1440 + hour * 60;
            for (int i = 0; i < _file.Boosts.Count; i++)
            {
                FlareBoost b = _file.Boosts[i];
                int visitors, cents;
                FlareUseRules.Credit(b, minute, out visitors, out cents);
                if ((visitors > 0 || cents > 0) && CityOn && _city != null)
                {
                    FlareFunnel f = CityFunnel(FunnelKind.Post, b.Post, b.Place, day);
                    if (f != null)
                    {
                        f.Came += visitors;
                        f.Spent += cents;
                    }
                }
            }
            if (hour == 9)
            {
                RivalDay(day, hour);
            }
            if (CityOn && _city != null && !string.IsNullOrEmpty(_city.WarnKey) && _file.ReviewWarnDay != day)
            {
                _file.ReviewWarnDay = day;
                Tell(NotificationType.Warning, "reallife_flare_use_turn", _openBusinessTab);
            }
        }

        // A rival posts, promotes or attacks, by the same dry run. Another player's business is never the target.
        private void RivalDay(int day, int hour)
        {
            if (!CityOn || _city == null || _city.RivalDay == day)
            {
                return;
            }
            PopulationState s = Residents;
            if (s == null || s.Businesses.Count == 0)
            {
                return;
            }
            _city.RivalDay = day;
            int move = FlareUseRules.RivalMove(_city.Seed, day);
            int actor = -1;
            int guard = 0;
            while (actor < 0 && guard < 8)
            {
                int pick = FlareRules.Roll(_city.Seed, day, 80 + guard) % s.Businesses.Count;
                byte owner = s.Businesses.Owner[pick];
                if (owner != (byte)Owner.Player && owner != (byte)Owner.Guest && s.Businesses.Key[pick] != null)
                {
                    actor = pick;
                }
                guard++;
            }
            if (actor < 0)
            {
                return;
            }
            string place = s.Businesses.Key[actor];
            int followers = 400 + FlareRules.Roll(_city.Seed, day, 81) % 4000;
            FlareInfluencer hired = null;
            if (move == 1 && _file != null && _file.Influencers.Count > 0)
            {
                int who = FlareRules.Roll(_city.Seed, day, 82) % _file.Influencers.Count;
                FlareInfluencer inf = _file.Influencers[who];
                if (inf != null && inf.Active && (inf.BookedUntil < day || string.IsNullOrEmpty(inf.BookedBy)))
                {
                    hired = inf;
                }
            }
            var brief = new FlareBrief
            {
                Followers = followers, Hour = hour, Photo = true, PhotoHere = true, SameDistrict = 1,
                DayCustomers = Math.Max(40, s.Businesses.Room[actor] / 2), ReachPaid = move == 1 ? 2000 : 0, AdDays = 1,
                Reliability = hired != null ? hired.Reliability : -1, Style = hired != null ? (int)hired.Style : 0,
                Seed = hired != null ? hired.Seed : _city.Seed, RollKey = hired != null ? hired.Id : day
            };
            FlareExpect ex = FlareUseRules.Expect(in brief);
            if (move == 2)
            {
                string mine = PlayerPlace();
                if (mine != null)
                {
                    FlareRules.AddBuzz(_file, mine, -2, day, 2);
                    int b = _cityPass.BusinessOfPlace(mine);
                    if (b >= 0 && b < _city.BusinessCount)
                    {
                        int n = _city.Districts;
                        int d = s.Businesses.District[actor];
                        if (d >= 0 && d < n)
                        {
                            _city.Sour[b * n + d] += 2000;
                        }
                    }
                }
            }
            else if (_city.RivalBoosts.Count < 16)
            {
                _city.RivalBoosts.Add(new FlareBoost
                {
                    Post = day, Place = place, FromMinute = day * 1440 + hour * 60, Hours = ex.Hours, Visitors = ex.Visitors, Cents = ex.Cents,
                    Percent = ex.Percent, Hour = hour, Flags = 1 | 2 | 16 | (move == 1 ? FlareBoost.PaidFlag : 0) | (ex.Flopped ? 4 : 0) | (ex.WentViral ? 8 : 0)
                });
                if (hired != null)
                {
                    hired.BookedUntil = day + 2;
                    hired.BookedBy = place;
                }
            }
            if (move == 2)
            {
                Tell(NotificationType.Warning, "reallife_flare_use_attack", _openFeed);
            }
            RivalSeen(s, actor, move, day, hour);
            NpcAnswers(s, day);
        }

        // What the city sees of the rival's move: its post, promoted when it bought reach, or its post against the player's business.
        private void RivalSeen(PopulationState s, int actor, int move, int day, int hour)
        {
            string key = s.Businesses.Key[actor];
            BuildingRegistration reg = FlareCityGame.Registration(key);
            string name = FlareCityGame.NameOf(key);
            int d = s.Businesses.District[actor];
            string district = d >= 0 && d < s.Districts.Count ? s.Districts[d].Id : null;
            if (move == 2)
            {
                string mine = PlayerPlace();
                if (mine != null)
                {
                    RivalAttack(name, district, mine, day, hour);
                }
                return;
            }
            RivalPromo(reg != null ? FlareGame.Place(reg) : null, name, district, day, hour, move == 1);
        }

        private string PlayerPlace()
        {
            if (_file == null || _file.Shops.Count == 0)
            {
                return null;
            }
            return _file.Shops[0].Place;
        }

        /// <summary>The player answers one review. A careful answer softens it.</summary>
        public bool AnswerReview(string place, int stars, string text)
        {
            if (!Running || _file == null || string.IsNullOrEmpty(place) || string.IsNullOrEmpty(text))
            {
                return false;
            }
            int quality = FlareUseRules.AnswerQuality(text);
            if (_file.Answers.Count >= 64)
            {
                _file.Answers.RemoveAt(0);
            }
            _file.Answers.Add(new FlareReviewNote { Key = place, Stars = stars, Day = Save != null ? Save.Day : 0, Quality = quality, Answer = text });
            if (quality >= FlareUseRules.GoodAnswer)
            {
                SoftenPlace(place);
            }
            Changed();
            return true;
        }

        /// <summary>A public answer to a rival. The talk about the player's business eases when the answer is careful.</summary>
        public bool AnswerRival(string place, string text)
        {
            if (!Running || string.IsNullOrEmpty(text))
            {
                return false;
            }
            var c = new FlareCompose { Kind = FlareSource.Life, Text = text, Place = place, Tone = FlareTone.Honest, ToneChosen = true };
            if (!Compose(c, out _))
            {
                return false;
            }
            if (FlareUseRules.AnswerQuality(text) >= FlareUseRules.GoodAnswer)
            {
                SoftenPlace(place);
            }
            return true;
        }

        /// <summary>A hire's quote: visitors, takings and the chance it flops. Nothing is saved.</summary>
        public FlareExpect HireExpect(FlareInfluencer inf, string place)
        {
            FlareShop shop;
            int same = place == null || !ShopOf(place, out shop) || inf == null || string.IsNullOrEmpty(shop.District) || string.IsNullOrEmpty(inf.District) ? -1
                : string.Equals(shop.District, inf.District, StringComparison.Ordinal) ? 1 : 0;   // their followers live where they do
            var brief = new FlareBrief
            {
                Followers = inf != null ? inf.Followers : 0, Hour = 12, Photo = true, PhotoHere = place != null, SameDistrict = same,
                Reliability = inf != null ? inf.Reliability : 50, Style = inf != null ? (int)inf.Style : 0, Seed = inf != null ? inf.Seed : 1, RollKey = inf != null ? inf.Id : 1,
                Guest = GuestPlace(place), DayCustomers = DayCustomers(place)
            };
            LastExpect = FlareUseRules.Expect(in brief);
            return LastExpect;
        }
    }
}
