using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The answers a business can give a review, in order on the reply sheet. Saved as a review note's quality, never as a number of its own.</summary>
    public enum FlareReplyOption
    {
        Thanks = 0,      // "Thank you for coming in..."
        Sorry = 1,       // "We are sorry it was not right..."
        Defend = 2       // "We do not agree..."
    }

    /// <summary>What an answer does to the review it answers: it fits it, stands (nothing happens) or costs a little.</summary>
    public enum FlareFit
    {
        Costs = -1,
        Stands = 0,
        Fits = 1
    }

    /// <summary>What an answered review ended up as, read from the quality and the stars saved with it (so an answer from an older save reads too).</summary>
    public enum FlareResult
    {
        None = 0,        // not answered
        Eased = 1,       // a poor review that counts half, its bad talk fading faster
        Raised = 2,      // a poor review that counts half as much again, its talk soured
        Thanked = 3,     // a good review whose reviewer was thanked: a few more residents hear good things
        Soured = 4,      // an answer that did not fit a review of three stars or more: the talk soured
        Stands = 5       // nothing happened
    }

    /// <summary>
    /// A business's answer to a review, by rule (PLAN part 2: "the player can answer a review; a good answer softens a bad one").
    /// Each answer fits the review, stands or costs a little, by the stars and, for a poor review, by whether it is out of line with the
    /// business's own rating (<see cref="Unfair"/>): thanks fit a review of four or five stars and cost on one of two or fewer; sorry fits a
    /// poor review that is fair and stands otherwise; standing your ground fits only a poor review that is unfair and costs on any other.
    /// What a fit does: a poor review it answers weighs half in the rating and what it put people off with fades faster; thanks to a good
    /// review let a few more residents hear good things. What a cost does: a poor review weighs half as much again, and the talk against the
    /// business grows by a resident in each district. The effect is saved as the answer's quality (<see cref="FitQuality"/>,
    /// <see cref="StandQuality"/>, <see cref="CostQuality"/>), never as a number of its own, so an older save and an older RLO read it as they did.
    /// The answers of the player and of every business of the city go by these rules; a business of the city picks its answer by its seed
    /// (<see cref="NpcAnswer"/>). However many reviews are answered, they move a rating by <see cref="MostLift"/> at the most. Game-free.
    ///
    ///   answer    to 4 or 5 stars   to 3 stars   to 2 or 1 stars, fair   to 2 or 1 stars, unfair
    ///   Thanks        fits            stands            costs                   costs
    ///   Sorry        stands           stands            fits                    stands
    ///   Defend       costs            costs             costs                   fits
    /// </summary>
    public static class FlareReplyRules
    {
        public const int Options = 3;
        public const int Rude = 300;             // thousandths: an answer under this makes things worse
        public const int Weight = 1000;          // thousandths: what a review weighs in the rating
        public const int Eased = 500;            // ...a poor review a good answer eased
        public const int Raised = 1500;          // ...a review a rude answer drew eyes to
        public const int NpcAnswers = 700;       // per mille of its poor reviews a business of the city answers
        public const int NpcRude = 150;          // per mille of those answers that are rude
        public const int FitQuality = 900;       // thousandths: what an answer that fits is worth (at or over FlareUseRules.GoodAnswer)
        public const int StandQuality = 500;     // ...one that stands
        public const int CostQuality = 150;      // ...one that costs (under Rude)
        public const int UnfairFrom = 4000;      // thousandths of a star: a business rated this high or more has poor reviews that are out of line
        public const int UnfairReviews = 5;      // reviews a rating needs before a poor one can be called out of line with it
        public const int MostLift = 400;         // thousandths of a star: the answers move a rating this far from the plain mean at the most, up or down

        /// <summary>Whether a poor review is out of line with a business rated well: two stars or fewer from a rating of four or more over at least five reviews.</summary>
        public static bool Unfair(int stars, FlareStarRow row)
        {
            return stars <= 2 && row != null && row.Total >= UnfairReviews && Average(row) >= UnfairFrom;
        }

        /// <summary>How an answer sits with the review it answers.</summary>
        public static FlareFit Fit(FlareReplyOption option, int stars, bool unfair)
        {
            switch (option)
            {
                case FlareReplyOption.Thanks:
                    return stars >= 4 ? FlareFit.Fits : stars == 3 ? FlareFit.Stands : FlareFit.Costs;
                case FlareReplyOption.Sorry:
                    return stars <= 2 && !unfair ? FlareFit.Fits : FlareFit.Stands;
                default:
                    return stars <= 2 && unfair ? FlareFit.Fits : FlareFit.Costs;
            }
        }

        /// <summary>What an answer is worth, thousandths, by how it fits.</summary>
        public static int Quality(FlareReplyOption option, int stars, bool unfair)
        {
            FlareFit fit = Fit(option, stars, unfair);
            return fit == FlareFit.Fits ? FitQuality : fit == FlareFit.Costs ? CostQuality : StandQuality;
        }

        /// <summary>What an answer is worth, thousandths, by what it says and the stars of the review it answers, the review being a fair one.</summary>
        public static int Quality(FlareReplyOption option, int stars) => Quality(option, stars, false);

        /// <summary>The option a typed reply is taken for, by the tone it is posted in: cheerful and proud thank, honest says sorry, cheeky stands its ground.</summary>
        public static FlareReplyOption OptionOf(FlareTone tone)
        {
            return tone == FlareTone.Cheerful || tone == FlareTone.Proud ? FlareReplyOption.Thanks : tone == FlareTone.Cheeky ? FlareReplyOption.Defend : FlareReplyOption.Sorry;
        }

        /// <summary>What an answered review ended up as, by the quality saved with it and its stars; <see cref="FlareResult.None"/> with no quality.</summary>
        public static FlareResult ResultOf(int quality, int stars)
        {
            if (quality <= 0)
            {
                return FlareResult.None;
            }
            if (Eases(quality, stars))
            {
                return FlareResult.Eased;
            }
            if (Worsens(quality))
            {
                return stars <= 2 ? FlareResult.Raised : FlareResult.Soured;
            }
            return quality >= FlareUseRules.GoodAnswer && stars >= 4 ? FlareResult.Thanked : FlareResult.Stands;
        }

        /// <summary>Which way an answer moves the talk about the business: 1 more residents hear good things, -1 the talk sours, 0 neither (an eased review's bad talk is softened apart).</summary>
        public static int TalkOf(FlareResult result)
        {
            return result == FlareResult.Thanked ? 1 : result == FlareResult.Raised || result == FlareResult.Soured ? -1 : 0;
        }

        /// <summary>The text key that says, for one option on a review, how it fits and what it will do: whole keys, so the catalogue checks each one is written.</summary>
        public static string LineKey(FlareReplyOption option, int stars, bool unfair)
        {
            FlareFit fit = Fit(option, stars, unfair);
            if (fit == FlareFit.Fits)
            {
                return stars >= 4 ? "reallife_flare_kit_option_glad" : option == FlareReplyOption.Defend ? "reallife_flare_kit_option_unfair" : "reallife_flare_kit_option_eases";
            }
            if (fit == FlareFit.Costs)
            {
                return stars <= 2 ? "reallife_flare_kit_option_worsens" : "reallife_flare_kit_option_costs_good";
            }
            return "reallife_flare_kit_option_stands";
        }

        /// <summary>The tone an answer is posted in.</summary>
        public static FlareTone Tone(FlareReplyOption option)
        {
            return option == FlareReplyOption.Thanks ? FlareTone.Cheerful : option == FlareReplyOption.Sorry ? FlareTone.Honest : FlareTone.Cheeky;
        }

        /// <summary>Whether an answer eases the review it answers: a good one to two stars or fewer.</summary>
        public static bool Eases(int quality, int stars) => quality >= FlareUseRules.GoodAnswer && stars <= 2;

        /// <summary>Whether an answer makes things worse.</summary>
        public static bool Worsens(int quality) => quality > 0 && quality < Rude;

        /// <summary>What one review weighs in the rating once answered with this quality, thousandths.</summary>
        public static int WeightOf(int stars, int quality)
        {
            return Eases(quality, stars) ? Eased : Worsens(quality) && stars <= 2 ? Raised : Weight;
        }

        /// <summary>An answer counted into a business's stars, so its rating moves as the rule says.</summary>
        public static void Answered(FlareStarRow row, int stars, int quality)
        {
            if (row == null)
            {
                return;
            }
            stars = stars < 1 ? 1 : stars > 5 ? 5 : stars;
            if (Eases(quality, stars))
            {
                row.Eased++;
                row.EasedStars += stars;
            }
            else if (Worsens(quality) && stars <= 2)
            {
                row.Raised++;
                row.RaisedStars += stars;
            }
        }

        public const int TurnDrop = 500;         // thousandths of a star: the newest stretch this far under the one before, the reviews have turned

        /// <summary>Whether a business's reviews have turned: its newest stretch half a star or more under the one before.</summary>
        public static bool Turning(FlareStarRow row) => row != null && row.Earlier > 0 && row.Recent > 0 && row.Recent + TurnDrop <= row.Earlier;

        /// <summary>A business's rating, thousandths of a star, its answered reviews weighed as the rule says; 0 with no reviews.</summary>
        public static int Average(FlareStarRow row)
        {
            if (row == null || row.Total <= 0)
            {
                return 0;
            }
            long sum = (long)(row.One + row.Two * 2 + row.Three * 3 + row.Four * 4 + row.Five * 5) * Weight;
            long count = (long)row.Total * Weight;
            sum += (long)row.EasedStars * (Eased - Weight) + (long)row.RaisedStars * (Raised - Weight);
            count += (long)row.Eased * (Eased - Weight) + (long)row.Raised * (Raised - Weight);
            if (count <= 0)
            {
                return 0;
            }
            long avg = sum * 1000 / count;
            long plain = Plain(row);
            avg = avg > plain + MostLift ? plain + MostLift : avg < plain - MostLift ? plain - MostLift : avg;
            return avg < 1000 ? 1000 : avg > 5000 ? 5000 : (int)avg;
        }

        /// <summary>A business's rating with every answer left out: the plain mean of its reviews, thousandths; 0 with none.</summary>
        public static int Plain(FlareStarRow row)
        {
            if (row == null || row.Total <= 0)
            {
                return 0;
            }
            return (int)((long)(row.One + row.Two * 2 + row.Three * 3 + row.Four * 4 + row.Five * 5) * 1000 / row.Total);
        }

        /// <summary>The rating of reviews given one by one (stars, and the quality of the answer each had, 0 for none), thousandths; 0 with none.</summary>
        public static int Average(List<int> stars, List<int> qualities)
        {
            long sum = 0, count = 0;
            for (int i = 0; stars != null && i < stars.Count; i++)
            {
                int s = stars[i] < 1 ? 1 : stars[i] > 5 ? 5 : stars[i];
                int w = WeightOf(s, qualities != null && i < qualities.Count ? qualities[i] : 0);
                sum += (long)s * w;
                count += w;
            }
            return count <= 0 ? 0 : (int)(sum * 1000 / count);
        }

        /// <summary>The counts a row's answers come to, worked out again from the review notes kept (an older save's row).</summary>
        public static void Recount(List<FlareReviewNote> notes, List<FlareStarRow> rows)
        {
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                rows[i].Eased = rows[i].EasedStars = rows[i].Raised = rows[i].RaisedStars = 0;
            }
            for (int i = 0; notes != null && i < notes.Count; i++)
            {
                FlareReviewNote n = notes[i];
                if (!string.IsNullOrEmpty(n.Answer))
                {
                    Answered(FlareUseRules.StarsOf(rows, n.Key), n.Stars, n.Quality);
                }
            }
        }

        /// <summary>
        /// A business of the city's answers one of its reviews, or not, by its seed: most poor reviews get a sorry, a few a rude
        /// answer, a good review a thanks now and then. The same rule as the player's options, so its rating moves alike.
        /// </summary>
        public static bool NpcAnswer(int seed, int key, int stars, out FlareReplyOption option)
        {
            int roll = FlareRules.Roll(seed, key, 911);
            option = FlareReplyOption.Thanks;
            if (stars >= 4)
            {
                return roll < 300;
            }
            if (roll >= NpcAnswers)
            {
                return false;
            }
            option = FlareRules.Roll(seed, key, 912) < NpcRude ? FlareReplyOption.Defend : FlareReplyOption.Sorry;
            return true;
        }
    }

    /// <summary>
    /// The lasting record of the review posts of the feed that went up about a business of the player's. The feed keeps a city post only a few
    /// days (FlareLiveRules.CityKeepDays) and fewer when it is full (FlareRules.PostsMost), so a rating read off the posts fell back to nothing;
    /// each review is counted once, by its post's number, into a row of stars per place that is saved with the account, as the city's own rows are
    /// (FlareUseRules.NoteReview): it counts for as long as the business is the player's. The rows and the numbers are bounded.
    /// </summary>
    public static class FlareReviewBook
    {
        public const int RowsMost = 32;       // businesses with a row: the oldest is let go
        public const int PostsKept = 1024;    // numbers of counted posts: twice the feed's posts, so a post still in the feed is never counted again

        /// <summary>Whether a post is a review that somebody else wrote and that has gone up by <paramref name="now"/>.</summary>
        public static bool Counts(FlareFile f, FlarePost p, long now)
        {
            return p.Source == FlareSource.Review && p.Author != FlareAuthor.Player && !string.IsNullOrEmpty(p.Place) && p.At <= now
                   && !(p.Author == FlareAuthor.Network && f.Net.IsHidden(p.AuthorId));
        }

        public static FlareStarRow RowOf(FlareFile f, string place, bool make)
        {
            for (int i = 0; i < f.Counted.Count; i++)
            {
                if (f.Counted[i].Key == place)
                {
                    return f.Counted[i];
                }
            }
            if (!make)
            {
                return null;
            }
            if (f.Counted.Count >= RowsMost)
            {
                f.Counted.RemoveAt(0);
            }
            var row = new FlareStarRow { Key = place };
            f.Counted.Add(row);
            return row;
        }

        public static void Add(FlareStarRow row, int stars)
        {
            switch (FlareRules.Clamp(stars, 1, 5))
            {
                case 1: row.One++; break;
                case 2: row.Two++; break;
                case 3: row.Three++; break;
                case 4: row.Four++; break;
                default: row.Five++; break;
            }
        }

        /// <summary>Counts the posts that have gone up and are about one of <paramref name="places"/>, each once. How many were counted.</summary>
        public static int Count(FlareFile f, long now, List<string> places)
        {
            int n = 0;
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                if (Counts(f, p, now) && places.Contains(p.Place) && !f.CountedPosts.Contains(p.Id))
                {
                    Take(f, p);
                    n++;
                }
            }
            return n;
        }

        private static void Take(FlareFile f, FlarePost p)
        {
            Add(RowOf(f, p.Place, true), p.Arg);
            if (f.CountedPosts.Count >= PostsKept)
            {
                f.CountedPosts.RemoveAt(0);
            }
            f.CountedPosts.Add(p.Id);
        }

        /// <summary>
        /// The stars of a place: the city's row (null for none), the lasting record, and any review post that has gone up and is not counted yet
        /// (the hour has not come round), so nothing shows less in between. The city's row itself is copied before it is added to, never changed.
        /// </summary>
        public static FlareStarRow With(FlareFile f, FlareStarRow city, string place, long now)
        {
            FlareStarRow kept = RowOf(f, place, false);
            FlareStarRow row = null;
            if (kept != null)
            {
                row = CopyOf(city, place);
                row.One += kept.One; row.Two += kept.Two; row.Three += kept.Three; row.Four += kept.Four; row.Five += kept.Five;
                row.Eased += kept.Eased; row.EasedStars += kept.EasedStars; row.Raised += kept.Raised; row.RaisedStars += kept.RaisedStars;
            }
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                if (p.Place == place && Counts(f, p, now) && !f.CountedPosts.Contains(p.Id))
                {
                    row = row ?? CopyOf(city, place);
                    Add(row, p.Arg);
                }
            }
            return row ?? city;
        }

        /// <summary>A reply of the player's to a review post, counted into the business's stars as the answer rules say (the first reply only).</summary>
        public static void Answered(FlareFile f, FlarePost reply, int quality, long now)
        {
            FlarePost review = reply.ReplyTo >= 0 ? f.Post(reply.ReplyTo) : null;
            if (review == null || review.Place != reply.Place || !Counts(f, review, now))
            {
                return;
            }
            int replies = 0;
            for (int i = 0; i < f.Posts.Count; i++)
            {
                replies += f.Posts[i].ReplyTo == review.Id && f.Posts[i].Author == FlareAuthor.Player ? 1 : 0;
            }
            if (replies > 1)
            {
                return;
            }
            if (!f.CountedPosts.Contains(review.Id))
            {
                Take(f, review);
            }
            FlareReplyRules.Answered(RowOf(f, review.Place, true), review.Arg, quality);
        }

        private static FlareStarRow CopyOf(FlareStarRow from, string place)
        {
            return from == null ? new FlareStarRow { Key = place } : new FlareStarRow
            {
                Key = from.Key, One = from.One, Two = from.Two, Three = from.Three, Four = from.Four, Five = from.Five, Recent = from.Recent, Earlier = from.Earlier,
                Eased = from.Eased, EasedStars = from.EasedStars, Raised = from.Raised, RaisedStars = from.RaisedStars
            };
        }
    }
}
