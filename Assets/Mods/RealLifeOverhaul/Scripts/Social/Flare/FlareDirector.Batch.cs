using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Answering the waiting reviews of the player's businesses in one press ("Answer all", one business or every business) and for a manager at
    /// the day's round (the manager's duty, Economy/Company). Every answer goes through exactly the path a hand answer does
    /// (<see cref="ReplyToNote"/>, <see cref="ReplyToReview"/>, so <see cref="FlareReplyRules"/> and its caps), chosen by <see cref="FlareBatchRules"/>
    /// and worded by <see cref="FlareReplyWords"/>; the single answers' notices are held back and one notice says what the press did, with the rating
    /// before and after. Only the player's own businesses are asked, and nothing is sent that a hand answer would not send. Cost: a walk over the waiting
    /// reviews of a business for a preview, a press or a round; nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private sealed class BatchItem
        {
            public FlareReviewNote Note;   // a resident's review, or
            public FlarePost Post;         // a review post of the feed
            public int Stars, Day, Key;
        }

        private readonly List<BatchItem> _batchItems = new List<BatchItem>(32);
        private readonly List<int> _batchStars = new List<int>(32);
        private readonly HashSet<int> _batchAnswered = new HashSet<int>();
        private readonly FlareBatchPlan _batchPlan = new FlareBatchPlan();
        private readonly FlareBatchSummary _batchSum = new FlareBatchSummary();
        private readonly System.Text.StringBuilder _batchText = new System.Text.StringBuilder(160);
        private int _batchCount;
        private bool _batchQuiet;

        /// <summary>Whether Flare can answer reviews in bulk now: it is running and the player has a business.</summary>
        public bool BatchOn => Running && Save != null && MyShops().Count > 0;

        // The reviews of a place that wait for the player: residents' reviews first, then the feed's review posts, each in the order they were kept, none older than fromDay.
        private int BatchCollect(string place, int fromDay)
        {
            _batchCount = 0;
            _batchStars.Clear();
            string key = CityOn ? FlareCityPass.PlaceToKey(place) : null;
            for (int i = 0; key != null && i < _city.ReviewNotes.Count; i++)
            {
                FlareReviewNote n = _city.ReviewNotes[i];
                if (n.Key == key && string.IsNullOrEmpty(n.Answer) && n.Day >= fromDay)
                {
                    BatchAdd(n, null, n.Stars, n.Day, n.Person * 31 + n.Day);
                }
            }
            if (_file != null)
            {
                _batchAnswered.Clear();
                for (int i = 0; i < _file.Posts.Count; i++)
                {
                    FlarePost p = _file.Posts[i];
                    if (p.Author == FlareAuthor.Player && p.ReplyTo >= 0)
                    {
                        _batchAnswered.Add(p.ReplyTo);
                    }
                }
                long now = Now;
                for (int i = 0; i < _file.Posts.Count; i++)
                {
                    FlarePost p = _file.Posts[i];
                    if (p.Source == FlareSource.Review && p.Place == place && p.Day >= fromDay && FlareReviewBook.Counts(_file, p, now) && !_batchAnswered.Contains(p.Id))
                    {
                        BatchAdd(null, p, p.Arg, p.Day, p.Id);
                    }
                }
            }
            return _batchCount;
        }

        private void BatchAdd(FlareReviewNote note, FlarePost post, int stars, int day, int key)
        {
            while (_batchItems.Count <= _batchCount)
            {
                _batchItems.Add(new BatchItem());
            }
            BatchItem it = _batchItems[_batchCount++];
            it.Note = note;
            it.Post = post;
            it.Stars = FlareRules.Clamp(stars, 1, 5);
            it.Day = day;
            it.Key = key;
            _batchStars.Add(it.Stars);
        }

        /// <summary>How many reviews of a business of the player's (every business with a null place) wait for an answer.</summary>
        public int ReviewsWaiting(string place)
        {
            if (!Running || Save == null)
            {
                return 0;
            }
            List<FlareShop> shops = MyShops();
            int n = 0;
            for (int i = 0; i < shops.Count; i++)
            {
                if (place == null || shops[i].Place == place)
                {
                    n += BatchCollect(shops[i].Place, int.MinValue);
                }
            }
            return n;
        }

        /// <summary>
        /// What "Answer all" would do for a business (every business with a null place), the figures the sheet shows before anything is answered; the
        /// director's own summary, worked out again on each call and changed by nothing.
        /// </summary>
        public FlareBatchSummary BatchPreview(string place)
        {
            _batchSum.Clear();
            if (!Running || Save == null)
            {
                return _batchSum;
            }
            List<FlareShop> shops = MyShops();
            int room = FlareBatchRules.Most;
            for (int i = 0; i < shops.Count; i++)
            {
                if (place != null && shops[i].Place != place)
                {
                    continue;
                }
                BatchCollect(shops[i].Place, int.MinValue);
                FlareBatchRules.Plan(StarsWithPosts(shops[i].Place, Now), _batchStars, room, _batchPlan);
                room -= _batchPlan.Ways.Count;
                _batchSum.Add(_batchPlan, shops[i].Name);
            }
            return _batchSum;
        }

        /// <summary>
        /// The player answers every waiting review of a business (every business with a null place) the way the rule says, and one notice tells what it did.
        /// At most <see cref="FlareBatchRules.Most"/> answers a press; what is left waits for the next. The summary, or null when nothing could be answered.
        /// </summary>
        public FlareBatchSummary BatchAnswer(string place)
        {
            _batchSum.Clear();
            if (!Running || Save == null)
            {
                return null;
            }
            try
            {
                List<FlareShop> shops = MyShops();
                int room = FlareBatchRules.Most;
                _batchQuiet = true;
                for (int i = 0; i < shops.Count && room > 0; i++)
                {
                    if (place == null || shops[i].Place == place)
                    {
                        room -= BatchRound(shops[i], int.MinValue, room, _batchSum);
                    }
                }
            }
            catch (Exception e)
            {
                Fault("answering reviews", e);
                return null;
            }
            finally
            {
                _batchQuiet = false;
            }
            if (_batchSum.Answers == 0 && _batchSum.Left == 0)
            {
                return null;
            }
            if (_batchSum.Answers > 0)
            {
                if (Meta.Dev.Verbose)
                {
                    BatchCheck(place);
                }
                BatchTold(_batchSum, place == null && _batchSum.Businesses > 1);
                Logger?.Info("Flare: the player answered " + _batchSum.Answers + " reviews in one press (" + _batchSum.Thanks + " thanks, " + _batchSum.Sorry + " sorry, " + _batchSum.Mixed
                             + " in between), " + _batchSum.Left + " left for them, rating " + _batchSum.Before + " to " + _batchSum.After + ".");
                Changed();
            }
            return _batchSum;
        }

        /// <summary>
        /// A manager's round at a business: its waiting reviews no older than <see cref="FlareBatchRules.AutoDays"/> are answered by the same rule, no notice per
        /// answer. <paramref name="slip"/> is the manager's mistake: they get to none of them today. False when Flare cannot; the figures in <paramref name="into"/>.
        /// </summary>
        internal bool ManagerAnswers(string place, bool slip, FlareBatchSummary into)
        {
            into.Clear();
            if (!Running || Save == null)
            {
                return false;
            }
            FlareShop shop = ShopAt(place);
            if (shop.Place == null)
            {
                return false;
            }
            try
            {
                _batchQuiet = true;
                int from = Save.Day - FlareBatchRules.AutoDays;
                if (slip)
                {
                    BatchCollect(place, from);
                    FlareBatchRules.Plan(StarsWithPosts(place, Now), _batchStars, FlareBatchRules.Most, _batchPlan);
                    into.Businesses = _batchPlan.Ways.Count > 0 ? 1 : 0;
                    into.Skipped = _batchPlan.Answers;   // none answered today: the reviews wait their turn
                    into.Left = _batchPlan.Left;
                    into.BeforeSum = into.AfterSum = _batchPlan.Before;
                    into.Name = shop.Name;
                    return true;
                }
                BatchRound(shop, from, FlareBatchRules.Most, into);
                if (into.Answers > 0)
                {
                    Changed();
                }
                return true;
            }
            catch (Exception e)
            {
                Fault("a manager answering reviews", e);
                return false;
            }
            finally
            {
                _batchQuiet = false;
            }
        }

        /// <summary>The rating the page shows for a business of the player's, thousandths (0 with no review): the figure a manager's week is measured by.</summary>
        internal int RatingOf(string place) => Running ? FlareReplyRules.Average(StarsWithPosts(place, Now)) : 0;

        /// <summary>How many waiting reviews of a business the rule leaves to the player, because a poor one is out of line with its rating: the ones to stand your ground on.</summary>
        internal int FlaggedAt(string place)
        {
            if (!Running || Save == null)
            {
                return 0;
            }
            BatchCollect(place, int.MinValue);
            FlareBatchRules.Plan(StarsWithPosts(place, Now), _batchStars, int.MaxValue, _batchPlan);
            return _batchPlan.Left;
        }

        private FlareShop ShopAt(string place)
        {
            List<FlareShop> shops = MyShops();
            for (int i = 0; i < shops.Count; i++)
            {
                if (shops[i].Place == place)
                {
                    return shops[i];
                }
            }
            return default(FlareShop);
        }

        // One business's waiting reviews answered: the plan worked out, then each answer made as a hand answer is. How many answers were made.
        private int BatchRound(FlareShop shop, int fromDay, int room, FlareBatchSummary into)
        {
            BatchCollect(shop.Place, fromDay);
            if (_batchCount == 0)
            {
                return 0;
            }
            FlareBatchRules.Plan(StarsWithPosts(shop.Place, Now), _batchStars, room, _batchPlan);
            int set = FlareReplyWords.SetOf(shop.Niche), previous = -1, made = 0;
            for (int i = 0; i < _batchPlan.Ways.Count; i++)
            {
                FlareBatchWay way = _batchPlan.Ways[i];
                if (way == FlareBatchWay.Left)
                {
                    continue;
                }
                BatchItem it = _batchItems[i];
                int variant = FlareReplyWords.Variant(way, _file.Seed, it.Key, previous);
                previous = variant;
                string key = FlareReplyWords.Key(way, set, variant);
                FlareReplyOption option = FlareBatchRules.Option(way);
                bool done;
                if (it.Note != null)
                {
                    done = ReplyToNote(it.Note, option, key);   // kept as its key: the name and the business are filled in where it is shown
                }
                else
                {
                    string words = FlareText.Fill(key, "name", FlareBatchRules.FirstName(AuthorName(it.Post)), "business", shop.Name ?? "?");
                    done = ReplyToReview(it.Post.Id, option, words, FlareReplyWords.Tone(way, variant));
                }
                if (done)
                {
                    made++;
                }
                else
                {
                    switch (way)
                    {
                        case FlareBatchWay.Thanks: _batchPlan.Thanks--; break;
                        case FlareBatchWay.Sorry: _batchPlan.Sorry--; break;
                        default: _batchPlan.Mixed--; break;
                    }
                }
            }
            _batchPlan.After = FlareReplyRules.Average(StarsWithPosts(shop.Place, Now));   // what the rating really is now, not what the plan guessed
            into.Add(_batchPlan, shop.Name);
            return made;
        }

        // The self-check line: the rating the press left is within the cap of the plain mean of the reviews, however many were answered.
        private void BatchCheck(string place)
        {
            List<FlareShop> shops = MyShops();
            bool ok = true;
            for (int i = 0; i < shops.Count; i++)
            {
                FlareStarRow row = place == null || shops[i].Place == place ? StarsWithPosts(shops[i].Place, Now) : null;
                ok &= row == null || row.Total == 0 || Math.Abs(FlareReplyRules.Average(row) - FlareReplyRules.Plain(row)) <= FlareReplyRules.MostLift;
            }
            Logger?.Info("Flare check (answer all): " + _batchSum.Answers + " answered (" + _batchSum.Thanks + " thanks, " + _batchSum.Sorry + " sorry, " + _batchSum.Mixed + " in between), "
                         + _batchSum.Left + " left for the player, rating " + _batchSum.Before + " to " + _batchSum.After + ", every rating within " + FlareReplyRules.MostLift
                         + " of the plain mean: " + (ok ? "PASS" : "FAIL"));
        }

        /// <summary>Test: eight reviews of the first business of the player's go up at once, one to five stars, so "Answer all" has something to answer.</summary>
        public void TestReviewsMany()
        {
            if (!TestReady("Flare many reviews"))
            {
                return;
            }
            Guarded(() =>
            {
                var shops = FlareGame.Businesses();
                if (shops.Count == 0)
                {
                    Tell(NotificationType.Warning, "reallife_flare_test_review_none", null);
                    return;
                }
                FlareBiz b = FlareLiveGame.Biz(shops[0], true);
                int[] stars = { 5, 5, 4, 3, 2, 1, 5, 1 };
                for (int i = 0; i < stars.Length; i++)
                {
                    int id = _file.NextId++;
                    _file.Posts.Add(new FlarePost
                    {
                        Id = id, Day = Save.Day, Hour = Save.Hour, Minute = FlareGame.MinuteOfDay % 60, Author = FlareAuthor.Citizen,
                        Who = FlareLiveRules.PickCitizen(FlareLiveRules.CitySeed(_file), id, 541), Source = FlareSource.Review, Arg = stars[i],
                        Place = b.Place, Name = b.Name, District = b.District, Topic = FlareLiveRules.NicheTag(b.Niche), Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000,
                        Reach = 200, Likes = FlareRules.Roll(_file.Seed, id, 3) % 20
                    });
                }
                Tell(NotificationType.Info, "reallife_flare_test_reviews_many_up", _openBusinessTab, "business", b.Name);
                Recount();
                Changed();
            });
        }

        /// <summary>The preview or the notice's list of what was or will be answered: "12 thanks, 3 sorry, 2 for the middling ones", the ones left last; empty for none.</summary>
        public string BatchParts(FlareBatchSummary s, bool withLeft)
        {
            _batchText.Length = 0;
            BatchPart(s.Thanks, "reallife_flare_batch_p_thanks");
            BatchPart(s.Sorry, "reallife_flare_batch_p_sorry");
            BatchPart(s.Mixed, "reallife_flare_batch_p_mixed");
            if (withLeft)
            {
                BatchPart(s.Left, "reallife_flare_batch_p_left");
            }
            return _batchText.ToString();
        }

        private void BatchPart(int count, string key)
        {
            if (count <= 0)
            {
                return;
            }
            if (_batchText.Length > 0)
            {
                _batchText.Append(", ");
            }
            _batchText.Append(FlareText.Fill(key, "count", count.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>The sentence that says how many poor reviews look out of line and are left to the player; empty for none.</summary>
        public static string BatchLeftLine(int left)
        {
            return left <= 0 ? string.Empty : FlareText.Fill(left == 1 ? "reallife_flare_batch_left_one" : "reallife_flare_batch_left", "count", left.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The rating before and after, as a sentence for one business or for the average of several.</summary>
        public static string BatchRating(FlareBatchSummary s)
        {
            string was = (s.Before / 1000f).ToString("0.00", CultureInfo.InvariantCulture), now = (s.After / 1000f).ToString("0.00", CultureInfo.InvariantCulture);
            bool many = s.Businesses > 1;
            if (s.Before <= 0)
            {
                return string.Empty;
            }
            if (was != now)
            {
                return FlareText.Fill(many ? "reallife_flare_batch_rating_all_moved" : "reallife_flare_reply_rating_moved", "before", was, "after", now);
            }
            return many ? FlareText.Fill("reallife_flare_batch_rating_all_same", "after", now) : FlareText.T("reallife_flare_reply_rating_left");
        }

        // The one notice for a press: what was answered, the rating before and after, and the poor reviews left to the player.
        private void BatchTold(FlareBatchSummary s, bool all)
        {
            Tell(NotificationType.Info, all ? "reallife_flare_batch_done_all" : "reallife_flare_batch_done", _openBusinessTab,
                "business", s.Name ?? "?", "count", s.Answers.ToString(CultureInfo.InvariantCulture), "parts", BatchParts(s, false), "rating", BatchRating(s), "left", BatchLeftLine(s.Left));
        }
    }
}
