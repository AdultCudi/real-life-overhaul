using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What an answer to a review of the player's business does, in one place for both interfaces: a reply typed in the old panel and a
    /// reply chosen on the kit's sheet both end in <see cref="ReviewAnswered"/> (a review post of the feed) or <see cref="ReplyToNote"/> (a
    /// resident's review), which work out how the answer fits the review (FlareReplyRules), move the rating's weight, the talk and the
    /// customers once for each review, and tell the player in figures what it did. Cost: a walk over the feed's posts for one press; nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private const int GladHeard = 1000;   // thousandths of a resident in each district who hear good things of a business after a thank you that fits

        /// <summary>The quality an option is worth on a review of the business at a place now: a poor review out of line with its rating is told apart.</summary>
        public int QualityFor(FlareReplyOption option, int stars, string place)
        {
            return FlareReplyRules.Quality(option, stars, UnfairAt(stars, place));
        }

        /// <summary>Whether a poor review of a business is out of line with the rating the player sees for it.</summary>
        public bool UnfairAt(int stars, string place)
        {
            return stars <= 2 && !string.IsNullOrEmpty(place) && FlareReplyRules.Unfair(stars, StarsWithPosts(place, Now));
        }

        /// <summary>
        /// What the old panel's reply sheet says about the tone chosen for a reply to a review of the player's business: how the answer fits it
        /// and what it will do. Null for a post that is not such a review, or one already answered.
        /// </summary>
        public string ReplyHint(FlarePost review, FlareTone tone)
        {
            if (!Running || review == null || review.Source != FlareSource.Review || review.Author == FlareAuthor.Player || string.IsNullOrEmpty(review.Place)
                || FlareGame.Business(review.Place) == null || Answered(review.Id))
            {
                return null;
            }
            int stars = FlareRules.Clamp(review.Arg, 1, 5);
            return FlareText.T(FlareReplyRules.LineKey(FlareReplyRules.OptionOf(tone), stars, UnfairAt(stars, review.Place)));
        }

        /// <summary>How the player answered a review post, by the quality kept with the answer; <see cref="FlareResult.None"/> when it has no kept answer.</summary>
        public FlareResult ResultOfPost(int reviewId, int stars)
        {
            for (int i = _file != null ? _file.Answers.Count - 1 : -1; i >= 0; i--)
            {
                if (_file.Answers[i].Person == reviewId && reviewId >= 0)
                {
                    return FlareReplyRules.ResultOf(_file.Answers[i].Quality, stars);
                }
            }
            return FlareResult.None;
        }

        // A reply of the player's under a review post of one of their businesses: the one answer to it counts, as the rules say.
        private void ReviewAnswered(FlarePost reply, FlareCompose c)
        {
            FlarePost review = reply.ReplyTo >= 0 ? _file.Post(reply.ReplyTo) : null;
            if (review == null || review.Source != FlareSource.Review || review.Author == FlareAuthor.Player || string.IsNullOrEmpty(review.Place) || review.Place != reply.Place
                || FlareGame.Business(review.Place) == null || RepliesTo(review.Id) != 1)
            {
                return;
            }
            int stars = FlareRules.Clamp(review.Arg, 1, 5);
            long now = Now;
            int before = FlareReplyRules.Average(StarsWithPosts(review.Place, now));
            int quality = c != null && c.Quality >= 0 ? c.Quality : QualityFor(FlareReplyRules.OptionOf(reply.Tone), stars, review.Place);
            if (_file.Answers.Count >= 64)
            {
                _file.Answers.RemoveAt(0);
            }
            _file.Answers.Add(new FlareReviewNote { Key = review.Place, Person = review.Id, Day = reply.Day, Quality = quality, Answer = reply.Text, Stars = stars });
            FlareReviewBook.Answered(_file, reply, quality, now);
            FlareResult result = FlareReplyRules.ResultOf(quality, stars);
            AnswerTalk(review.Place, result, reply, CurrentDay);
            Told(review.Name, stars, result, before, FlareReplyRules.Average(StarsWithPosts(review.Place, now)), StarsWithPosts(review.Place, now));
            if (!_batchQuiet)
            {
                Logger?.Info("Flare: the player answered a " + stars + " star review of " + review.Name + " (" + result + ", worth " + quality + ").");
            }
        }

        // How many replies of the player's sit under a review: the first is the one that counts.
        private int RepliesTo(int review)
        {
            int n = 0;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                n += _file.Posts[i].ReplyTo == review && _file.Posts[i].Author == FlareAuthor.Player ? 1 : 0;
            }
            return n;
        }

        // The talk an answer moves, once for each review: bad talk softened and a polite answer heard, a few more residents hearing good
        // things after a thank you that fits, or the talk soured by an answer that did not.
        private void AnswerTalk(string place, FlareResult result, FlarePost reply, int day)
        {
            if (result == FlareResult.Eased)
            {
                SoftenPlace(place);
                if (reply != null && reply.Tone != FlareTone.Cheeky && !CityAnswer(place, reply))
                {
                    Buzz(place, 1, FlareLiveRules.ReviewDays, day, FlareGame.Business(place));
                }
                return;
            }
            int talk = FlareReplyRules.TalkOf(result);
            if (talk < 0)
            {
                Worse(place, day);
            }
            else if (talk > 0)
            {
                Glad(place, day);
            }
        }

        // A thank you that fits: a few more of the district's residents hear good things of the business (the mirror of a rude answer).
        private void Glad(string place, int day)
        {
            int b = CityOn && place != null ? _cityPass.BusinessOfPlace(place) : -1;
            if (b >= 0 && b < _city.BusinessCount)
            {
                for (int d = 0; d < _city.Districts; d++)
                {
                    FlareCityRules.Impress(_city, b, d, 1, GladHeard);
                }
                return;
            }
            if (place != null && _file != null)
            {
                Buzz(place, 1, FlareLiveRules.ReviewDays, day, FlareGame.Business(place));
            }
        }

        // The player is told what the answer did, in words and figures: the rating before and after, or why it did not move.
        private void Told(string business, int stars, FlareResult result, int before, int after, FlareStarRow row)
        {
            if (_batchQuiet)
            {
                return;   // a bulk answer tells once for all of them (FlareDirector.Batch)
            }
            string key;
            switch (result)
            {
                case FlareResult.Eased: key = "reallife_flare_reply_done_eased"; break;
                case FlareResult.Raised: key = "reallife_flare_reply_done_raised"; break;
                case FlareResult.Thanked: key = "reallife_flare_reply_done_thanked"; break;
                case FlareResult.Soured: key = "reallife_flare_reply_done_soured"; break;
                default: key = "reallife_flare_reply_done_stands"; break;
            }
            string was = (before / 1000f).ToString("0.00", CultureInfo.InvariantCulture), now = (after / 1000f).ToString("0.00", CultureInfo.InvariantCulture);
            string count = (row != null ? row.Total : 0).ToString(CultureInfo.InvariantCulture);
            string rating = before <= 0 ? string.Empty
                : was != now ? FlareText.Fill("reallife_flare_reply_rating_moved", "before", was, "after", now)
                : result == FlareResult.Eased || result == FlareResult.Raised ? FlareText.Fill("reallife_flare_reply_rating_same", "before", was, "count", count)
                : FlareText.T("reallife_flare_reply_rating_left");
            Tell(NotificationType.Info, key, null, "business", business ?? "?", "stars", stars.ToString(CultureInfo.InvariantCulture), "rating", rating);
        }

        private string NameOfPlace(string place)
        {
            List<FlareShop> shops = MyShops();
            for (int i = 0; i < shops.Count; i++)
            {
                if (shops[i].Place == place)
                {
                    return Clean(shops[i].Name);
                }
            }
            return place;
        }
    }
}
