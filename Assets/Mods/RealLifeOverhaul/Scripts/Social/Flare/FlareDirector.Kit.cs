using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, what the kit face asks of it beyond the old panel's: the notifications with the account's own records,
    /// the inbox and a conversation, a business's offer taken or turned down in its conversation, a review answered from the reply
    /// sheet's options (FlareReplyRules) and the city's businesses answering theirs by the same rules, a rival's promoted post and
    /// its attack in the feed, and the messages a hire, an offer and a customer write. Cost: nothing per frame; a walk over the
    /// account's records when a page opens, and a little on the day's and the hour's steps.
    /// </summary>
    public sealed partial class FlareDirector
    {
        public const int KitNoticesMost = 60;
        public const int RivalAttackArg = 7;   // the business post of a rival who runs the player's business down (FlareText's BusinessPostKeys)

        private readonly List<FlareNotice> _kitNotices = new List<FlareNotice>(64);
        private readonly List<FlareThread> _threads = new List<FlareThread>(16);
        private readonly List<FlareDm> _thread = new List<FlareDm>(32);

        /// <summary>The kit's notifications, newest first, at most <see cref="KitNoticesMost"/>; the director's list, filled again on each call.</summary>
        public List<FlareNotice> KitNotices()
        {
            _kitNotices.Clear();
            if (!Running || Save == null)
            {
                return _kitNotices;
            }
            MyPlaces();
            FlareLiveRules.AllNotices(_file, Now, MeKey, _myPlaces, CityOn ? _city.Offers : null, _reactions, _kitNotices, KitNoticesMost, CityOn ? _city.Ads : null);
            return _kitNotices;
        }

        /// <summary>Game minutes up to which the notifications have been seen.</summary>
        public long NoticesSeenAt => Running ? _file.Net.NoticeSeen : 0;

        /// <summary>The inbox: a conversation for each account, newest first; the director's list, filled again on each call.</summary>
        public List<FlareThread> KitThreads()
        {
            _threads.Clear();
            if (Running && Save != null)
            {
                FlareLiveRules.Threads(_file.Net, Now, _threads);
            }
            return _threads;
        }

        /// <summary>One conversation, oldest first; the director's list, filled again on each call.</summary>
        public List<FlareDm> KitThread(string with)
        {
            _thread.Clear();
            if (Running && Save != null)
            {
                FlareLiveRules.Thread(_file.Net, with, Now, _thread);
            }
            return _thread;
        }

        /// <summary>Messages from others not read yet.</summary>
        public int UnreadMessages => Running && Save != null ? FlareLiveRules.UnreadDms(_file.Net, Now) : 0;

        /// <summary>The player's businesses' places, as Flare writes a place.</summary>
        public List<string> MyPlaces()
        {
            if (_myPlaces.Count == 0)
            {
                List<BuildingRegistration> mine = FlareGame.Businesses();
                for (int i = 0; i < mine.Count; i++)
                {
                    _myPlaces.Add(FlareGame.Place(mine[i]));
                }
            }
            return _myPlaces;
        }

        private readonly List<FlareShop> _kitShops = new List<FlareShop>(8);

        /// <summary>The player's businesses with their names, kinds and districts; the director's list, filled again on each call.</summary>
        public List<FlareShop> MyShops()
        {
            _kitShops.Clear();
            if (Running && _file != null)
            {
                FlareGame.Shops(_file, _kitShops);
            }
            return _kitShops;
        }

        /// <summary>
        /// The photos a post can carry, as picture keys ("f:" and the file): the player's own newest moments first, then the city's
        /// photo bank, the business's front first, then its district, then the newest of the city; at most <paramref name="most"/>.
        /// </summary>
        public int BankPhotos(string place, string district, List<string> into, int most)
        {
            into.Clear();
            for (int i = _file != null ? _file.Drafts.Count - 1 : -1; i >= 0 && into.Count < Math.Min(2, most); i--)
            {
                FlareDraft d = _file.Drafts[i];
                if ((d.Source == FlareSource.Photo || d.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(d.Name))
                {
                    into.Add("f:" + d.Name);
                }
            }
            if (_bank == null || _photoFolder == null || Settings.FlareCityPhotos <= 0)
            {
                return into.Count;
            }
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = _bank.Photos.Count - 1; i >= 0 && into.Count < most; i--)
                {
                    FlareBankPhoto ph = _bank.Photos[i];
                    bool front = place != null && ph.Place == place, near = district != null && ph.District == district;
                    if (!_bankFiles.Contains(ph.Id) || (pass == 0 && !front) || (pass == 1 && (front || !near)) || (pass == 2 && (front || near)))
                    {
                        continue;
                    }
                    into.Add("f:" + System.IO.Path.Combine(_photoFolder, FlareCityPhotoRules.FileName(ph.Id)));
                }
            }
            return into.Count;
        }

        // ------------------------------------------------------------------------------------------------ offers

        /// <summary>The player takes a business's offer in its conversation: the first post goes up and is paid, and they say so there.</summary>
        public bool TakeOffer(int id)
        {
            FlareOffer o = OfferOf(id);
            if (o == null || !AcceptOffer(id))
            {
                return false;
            }
            long now = Now;
            string with = FlareLiveRules.OfferKey(id);
            FlareLiveRules.Dm(_file.Net, with, o.Name, now, true, null, "reallife_flare_dm_offer_yes", null);
            FlareLiveRules.Dm(_file.Net, with, o.Name, now + 12, false, null, "reallife_flare_dm_offer_thanks", "fee=" + FlareText.Money((int)(o.Fee / 100)));
            Logger?.Info("Flare: the player takes " + o.Name + "'s offer of " + o.Posts + " paid post(s) in its messages.");
            Changed();
            return true;
        }

        /// <summary>The player turns an offer down in its conversation: it closes, and the business says it understands.</summary>
        public bool DeclineOffer(int id)
        {
            FlareOffer o = OfferOf(id);
            if (o == null || Save == null || !o.Open(Save.Day))
            {
                return false;
            }
            o.Until = Save.Day - 1;   // closed: it can no longer be taken
            long now = Now;
            string with = FlareLiveRules.OfferKey(id);
            FlareLiveRules.Dm(_file.Net, with, o.Name, now, true, null, "reallife_flare_dm_offer_no", null);
            FlareLiveRules.Dm(_file.Net, with, o.Name, now + 12, false, null, "reallife_flare_dm_offer_noted", null);
            Logger?.Info("Flare: the player turns down " + o.Name + "'s offer of paid posts.");
            Changed();
            return true;
        }

        // A business's offer writes to the player as it comes in, its fee and posts named.
        private void OfferDm(FlareOffer offer, int day)
        {
            if (offer == null || _file == null)
            {
                return;
            }
            FlareLiveRules.Dm(_file.Net, FlareLiveRules.OfferKey(offer.Id), offer.Name, FlareLiveRules.Now(day, FlareLiveRules.OfferHour, 0), false, null, "reallife_flare_dm_offer",
                "fee=" + FlareText.Money((int)(offer.Fee / 100)) + "~posts=" + offer.Posts + "~business=" + Clean(offer.Name));
        }

        private static string Clean(string name) => string.IsNullOrEmpty(name) ? "?" : name.Replace('~', ' ');

        // ------------------------------------------------------------------------------------------------ campaigns

        // A hire booked: its lead influencer writes to say so, the first day named.
        private void HiredDm(FlareHire h)
        {
            if (h == null || h.Who.Count == 0)
            {
                return;
            }
            FlareInfluencer lead = _file.Influencer(h.Who[0]);
            if (lead == null)
            {
                return;
            }
            FlareLiveRules.Dm(_file.Net, "i:" + lead.Id, lead.Handle, Now + 20, false, null, "reallife_flare_dm_campaign_hired", "business=" + Clean(h.Name) + "~day=" + h.From);
        }

        // The day's posts of the deals: each influencer who posted writes what it brought, or that it fell flat.
        private void CampaignDms(int day, int from)
        {
            for (int i = from; i < _file.PastHires.Count; i++)
            {
                FlarePastHire p = _file.PastHires[i];
                FlareInfluencer inf = _file.Influencer(p.Who);
                if (p.Day != day || inf == null)
                {
                    continue;
                }
                FlareLiveRules.Dm(_file.Net, "i:" + inf.Id, inf.Handle, FlareLiveRules.Now(day, FlareLiveRules.NoticeHour, 10), false, null,
                    p.Flopped ? "reallife_flare_dm_campaign_flop" : "reallife_flare_dm_campaign_post",
                    "business=" + Clean(p.Name) + "~visitors=" + p.Visitors + "~money=" + FlareText.Money(p.Cents / 100));
            }
        }

        // ------------------------------------------------------------------------------------------------ reviews

        /// <summary>
        /// The player answers a review post of the city's people about one of their businesses with an option of the reply sheet:
        /// the answer goes up under it, and the rating and the talk move as FlareReplyRules says. False when it cannot be answered.
        /// </summary>
        public bool ReplyToReview(int postId, FlareReplyOption option, string words)
        {
            FlarePost review = Running ? _file.Post(postId) : null;
            if (review == null || review.Source != FlareSource.Review || string.IsNullOrEmpty(words) || review.Place == null || FlareGame.Business(review.Place) == null
                || Answered(postId))
            {
                return false;
            }
            int stars = FlareRules.Clamp(review.Arg, 1, 5);
            int quality = FlareReplyRules.Quality(option, stars);
            var c = new FlareCompose
            {
                Kind = FlareSource.Reply, ReplyTo = postId, Text = words, Tone = FlareReplyRules.Tone(option), ToneChosen = true, Quality = quality
            };
            if (!Compose(c, out _))
            {
                return false;
            }
            if (FlareReplyRules.Worsens(quality))
            {
                Worse(review.Place, Save != null ? Save.Day : review.Day);
            }
            return true;
        }

        /// <summary>Whether the player has answered a review post already.</summary>
        public bool Answered(int postId)
        {
            for (int i = 0; Running && i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (p.ReplyTo == postId && p.Author == FlareAuthor.Player)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The player answers a review a resident wrote of one of their businesses (Flare in the city) with an option of the reply
        /// sheet: kept with the review, its weight in the rating set by FlareReplyRules, the talk softened or soured. False when it cannot.
        /// </summary>
        public bool ReplyToNote(FlareReviewNote note, FlareReplyOption option, string words)
        {
            if (!CityOn || note == null || !string.IsNullOrEmpty(note.Answer) || string.IsNullOrEmpty(words))
            {
                return false;
            }
            string place = PlaceOfKey(note.Key);
            if (place == null || FlareGame.Business(place) == null)
            {
                return false;   // only the player's own business answers here
            }
            int quality = FlareReplyRules.Quality(option, note.Stars);
            note.Answer = words;
            note.Quality = quality;
            FlareReplyRules.Answered(FlareUseRules.StarsOf(_city.StarRows, note.Key), note.Stars, quality);
            if (FlareReplyRules.Eases(quality, note.Stars))
            {
                SoftenPlace(place);
            }
            else if (FlareReplyRules.Worsens(quality))
            {
                Worse(place, Save != null ? Save.Day : note.Day);
            }
            Logger?.Info("Flare in the city: the player answers a " + note.Stars + " star review (" + option + ", worth " + quality + ").");
            Changed();
            return true;
        }

        // A rude answer: the people who read it think worse of the business.
        private void Worse(string place, int day)
        {
            int b = CityOn && place != null ? _cityPass.BusinessOfPlace(place) : -1;
            if (b >= 0 && b < _city.BusinessCount)
            {
                int n = _city.Districts;
                for (int d = 0; d < n; d++)
                {
                    _city.Sour[b * n + d] += RudeSour;
                }
                return;
            }
            if (place != null && _file != null)
            {
                FlareRules.AddBuzz(_file, place, -1, day, FlareLiveRules.ReviewDays);
                Rewrite(place, false);
            }
        }

        private const int RudeSour = 1000;   // thousandths of a resident in each district who think worse of a business after a rude answer

        // The city's businesses answer their newest reviews by the player's rules, so their ratings move alike; never the player's or another player's.
        private void NpcAnswers(PopulationState s, int day)
        {
            int answered = 0;
            for (int i = _city.ReviewNotes.Count - 1; i >= 0 && answered < 24; i--)
            {
                FlareReviewNote n = _city.ReviewNotes[i];
                if (n.Day < day - 1)
                {
                    break;
                }
                if (!string.IsNullOrEmpty(n.Answer))
                {
                    continue;
                }
                string place = PlaceOfKey(n.Key);
                int b = place != null ? _cityPass.BusinessOfPlace(place) : -1;
                if (b < 0 || b >= s.Businesses.Count || s.Businesses.Owner[b] == (byte)Owner.Player || s.Businesses.Owner[b] == (byte)Owner.Guest)
                {
                    continue;
                }
                FlareReplyOption option;
                if (!FlareReplyRules.NpcAnswer(_city.Seed ^ day, n.Person, n.Stars, out option))
                {
                    continue;
                }
                n.Quality = FlareReplyRules.Quality(option, n.Stars);
                n.Answer = ReplyKeys[(int)option];   // its words are the reply sheet's own, in the reader's language
                FlareReplyRules.Answered(FlareUseRules.StarsOf(_city.StarRows, n.Key), n.Stars, n.Quality);
                if (b < _city.BusinessCount)
                {
                    int dn = _city.Districts;
                    for (int d = 0; d < dn; d++)
                    {
                        if (FlareReplyRules.Eases(n.Quality, n.Stars))
                        {
                            _city.Sour[b * dn + d] = FlareUseRules.Soften(_city.Sour[b * dn + d], n.Quality);
                        }
                        else if (FlareReplyRules.Worsens(n.Quality))
                        {
                            _city.Sour[b * dn + d] += RudeSour;
                        }
                    }
                }
                answered++;
            }
        }

        /// <summary>The reply sheet's words for each option, by key; a business of the city's answers with them too.</summary>
        public static readonly string[] ReplyKeys = { "reallife_flare_reply_thanks", "reallife_flare_reply_sorry", "reallife_flare_reply_defend" };

        // ------------------------------------------------------------------------------------------------ rivals

        /// <summary>Whether a business of the city's post bought its reach: that day's move of its business was a paid promotion.</summary>
        public bool RivalPaid(FlarePost p)
        {
            if (!CityOn || p == null || p.Author != FlareAuthor.Business || p.Place == null || _city.RivalBoosts.Count == 0)
            {
                return false;
            }
            string key = FlareCityPass.PlaceToKey(p.Place);
            for (int i = 0; key != null && i < _city.RivalBoosts.Count; i++)
            {
                FlareBoost b = _city.RivalBoosts[i];
                if (b.Post == p.Day && b.Paid && b.Place == key)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The reviews residents left of a business of the player's (Flare in the city), newest first, at most <paramref name="most"/>.</summary>
        public int ReviewNotesOf(string place, List<FlareReviewNote> into, int most)
        {
            into.Clear();
            string key = CityOn ? FlareCityPass.PlaceToKey(place) : null;
            for (int i = key != null ? _city.ReviewNotes.Count - 1 : -1; i >= 0 && into.Count < most; i--)
            {
                if (_city.ReviewNotes[i].Key == key)
                {
                    into.Add(_city.ReviewNotes[i]);
                }
            }
            return into.Count;
        }

        /// <summary>A resident who reviewed a business, by name.</summary>
        public string ReviewerName(int person)
        {
            PopulationState s = CityOn ? Residents : null;
            return ResidentName(person, s != null && FlareCityViews.FemaleOf(s, _cityPass.PersonSlot, person));
        }

        // A rival's promotion as the city sees it: a business post of its own in the feed, marked Promoted when it paid for reach.
        private FlarePost RivalPromo(string place, string name, string district, int day, int hour, bool paid)
        {
            if (Following || string.IsNullOrEmpty(name) || _file == null)
            {
                return null;
            }
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = hour, Minute = FlareRules.Roll(_city.Seed, day, 83) % 60, Author = FlareAuthor.Business, Source = FlareSource.BusinessPost, Place = place,
                Name = name, Arg = FlareRules.Roll(_city.Seed, day, 84) % 2, District = district, Topic = paid ? "deal" : "shoplocal",
                Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = (paid ? 2000 : 400) + FlareRules.Roll(_city.Seed, day, 85)
            };
            p.Likes = p.Reach / 14;
            p.Replies = p.Reach / 160;
            _file.Posts.Add(p);
            FlareRules.Prune(_file);
            ShareChanged(p);
            return p;
        }

        // A rival runs the player's business down: its own post saying so, and a message to the player, who can answer it.
        private void RivalAttack(string name, string district, string mine, int day, int hour)
        {
            if (Following || string.IsNullOrEmpty(name) || _file == null)
            {
                return;
            }
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = hour, Minute = FlareRules.Roll(_city.Seed, day, 86) % 60, Author = FlareAuthor.Business, Source = FlareSource.BusinessPost, Name = name,
                Arg = RivalAttackArg, District = district, Topic = "shoplocal", Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29),
                Reach = 600 + FlareRules.Roll(_city.Seed, day, 87)
            };
            p.Likes = p.Reach / 12;
            p.Replies = p.Reach / 120;
            _file.Posts.Add(p);
            FlareRules.Prune(_file);
            ShareChanged(p);
            BuildingRegistration reg = FlareGame.Business(mine);
            FlareLiveRules.Dm(_file.Net, "k:" + name, name, FlareLiveRules.Now(day, hour, 30), false, null, "reallife_flare_dm_rival",
                "business=" + Clean(reg != null ? FlareGame.Name(reg) : mine));
        }
    }
}
