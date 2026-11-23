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

    /// <summary>
    /// A business's answer to a review, by rule (PLAN part 2: "the player can answer a review; a good answer softens a bad one").
    /// An answer is worth a quality in thousandths by what it says and the stars it answers. A good answer (at or over
    /// FlareUseRules.GoodAnswer) to a poor review (two stars or fewer) eases it: the review then weighs half in the rating, and
    /// what it put people off with fades faster. A rude answer (under <see cref="Rude"/>) draws eyes to it: the review weighs half
    /// again, and the talk against the business grows. The player and every business of the city's answer by these rules; a
    /// business of the city's picks its answer by its seed (<see cref="NpcAnswer"/>). Game-free.
    ///
    ///   answer    to 5 or 4 stars   to 3 stars   to 2 or 1 stars
    ///   Thanks          800             600            300
    ///   Sorry           500             900            900
    ///   Defend          150             150            150
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

        /// <summary>What an answer is worth, thousandths, by what it says and the stars of the review it answers.</summary>
        public static int Quality(FlareReplyOption option, int stars)
        {
            switch (option)
            {
                case FlareReplyOption.Thanks:
                    return stars >= 4 ? 800 : stars == 3 ? 600 : 300;
                case FlareReplyOption.Sorry:
                    return stars >= 4 ? 500 : 900;
                default:
                    return 150;
            }
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
            return Eases(quality, stars) ? Eased : Worsens(quality) ? Raised : Weight;
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
            else if (Worsens(quality))
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
            return avg < 1000 ? 1000 : avg > 5000 ? 5000 : (int)avg;
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
