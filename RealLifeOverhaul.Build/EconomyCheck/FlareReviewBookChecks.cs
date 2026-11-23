using System.Collections.Generic;
using RealLifeOverhaul.Flare;

// A business's rating from the feed's review posts is a lasting record: counted once, kept after the feed lets the posts go, saved with the account
// in a bounded way, and an older save without the record reads as before.
internal static partial class Program
{
    private static FlarePost BookReview(int id, int day, int hour, string place, int stars, FlareAuthor by = FlareAuthor.Citizen)
    {
        return new FlarePost { Id = id, Day = day, Hour = hour, Author = by, Who = 1, Source = FlareSource.Review, Place = place, Name = "Shop", Arg = stars };
    }

    private static void FlareReviewBookChecks()
    {
        var places = new List<string> { "Cafe~1" };
        FlareFile f = FlareRules.Open(5, 10, 3, 50);
        f.Posts.Add(BookReview(1, 10, 12, "Cafe~1", 5));
        f.Posts.Add(BookReview(2, 10, 14, "Cafe~1", 4));
        f.Posts.Add(BookReview(3, 10, 15, "Other~2", 1));
        f.Posts.Add(BookReview(4, 10, 16, "Cafe~1", 2, FlareAuthor.Player));
        long noon = FlareLiveRules.Now(10, 13, 0);
        FlareStarRow shown = FlareReviewBook.With(f, null, "Cafe~1", noon);
        Check(shown != null && shown.Total == 1 && shown.Five == 1, "Flare reviews: a review that has gone up counts before its hour is counted, one still to come does not");
        Check(FlareReviewBook.Count(f, FlareLiveRules.Now(10, 20, 0), places) == 2 && FlareReviewBook.Count(f, FlareLiveRules.Now(10, 21, 0), places) == 0,
            "Flare reviews: each review of the player's business is counted once, not another business's and not the player's own");
        // The feed lets the city's posts go after a few days; the stars stay.
        long later = FlareLiveRules.Now(20, 9, 0);
        f.Posts.Add(new FlarePost { Id = 90, Day = 20, Hour = 8, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk });
        FlareLiveRules.PruneCity(f);
        bool gone = true;
        foreach (FlarePost p in f.Posts)
        {
            gone &= p.Source != FlareSource.Review || p.Author == FlareAuthor.Player;
        }
        FlareStarRow kept = FlareReviewBook.With(f, null, "Cafe~1", later);
        Check(gone && kept != null && kept.Total == 2 && kept.Five == 1 && kept.Four == 1 && FlareReplyRules.Average(kept) == 4500,
            "Flare reviews: after the feed has let the posts go the business still has its reviews and its stars");
        // The city's own row is added to, never changed.
        var city = new FlareStarRow { Key = "Cafe|1", Three = 2, Recent = 3000, Earlier = 3500 };
        FlareStarRow both = FlareReviewBook.With(f, city, "Cafe~1", later);
        Check(both != city && both.Total == 4 && city.Total == 2 && both.Recent == 3000, "Flare reviews: the city's row and the record are added in a copy");
        // An answer weighs as the rule says, once.
        FlareFile g = FlareRules.Open(5, 10, 3, 50);
        g.Posts.Add(BookReview(7, 10, 9, "Cafe~1", 1));
        var reply = new FlarePost { Id = 8, Day = 10, Hour = 10, Author = FlareAuthor.Player, Source = FlareSource.Reply, Place = "Cafe~1", ReplyTo = 7 };
        g.Posts.Add(reply);
        long ten = FlareLiveRules.Now(10, 11, 0);
        FlareReviewBook.Answered(g, reply, 900, ten);
        var second = new FlarePost { Id = 9, Day = 10, Hour = 10, Author = FlareAuthor.Player, Source = FlareSource.Reply, Place = "Cafe~1", ReplyTo = 7 };
        g.Posts.Add(second);
        FlareReviewBook.Answered(g, second, 900, ten);   // a second reply to the same review changes nothing
        FlareStarRow eased = FlareReviewBook.With(g, null, "Cafe~1", ten);
        Check(eased != null && eased.Total == 1 && eased.Eased == 1 && FlareReplyRules.Average(eased) == 1000 && FlareReviewBook.Count(g, ten, places) == 0,
            "Flare reviews: a good answer to a poor review is counted once and the review is not counted twice");
        // The save keeps both, and a save without them reads as before.
        FlareFile back = FlareStore.Read(FlareStore.WriteAccount(f), FlareStore.WritePosts(f));
        FlareStarRow read = back != null ? FlareReviewBook.With(back, null, "Cafe~1", later) : null;
        Check(read != null && read.Total == 2 && back.CountedPosts.Count == 2 && FlareReviewBook.Count(back, later, places) == 0,
            "Flare reviews: the record is saved with the account and read back without counting a post again");
        string[] lines = FlareStore.WriteAccount(f).Split('\n');
        var older = new List<string>();
        foreach (string line in lines)
        {
            if (!line.StartsWith("K|") && !line.StartsWith("J|"))
            {
                older.Add(line);
            }
        }
        FlareFile old = FlareStore.Read(string.Join("\n", older), FlareStore.WritePosts(f));
        Check(old != null && old.Counted.Count == 0 && old.CountedPosts.Count == 0, "Flare reviews: an older save without the record reads as before");
        // Bounded: rows and numbers.
        FlareFile big = FlareRules.Open(5, 10, 3, 50);
        var many = new List<string>();
        for (int i = 0; i < FlareReviewBook.RowsMost + 10; i++)
        {
            many.Add("P~" + i);
        }
        for (int i = 0; i < FlareReviewBook.PostsKept + 50; i++)
        {
            big.Posts.Add(BookReview(1000 + i, 10, 8, many[i % many.Count], 3));
        }
        FlareReviewBook.Count(big, FlareLiveRules.Now(10, 23, 0), many);
        Check(big.Counted.Count <= FlareReviewBook.RowsMost && big.CountedPosts.Count <= FlareReviewBook.PostsKept,
            "Flare reviews: the record's rows and numbers are bounded");
        string book = PlayFile("Social", "Flare", "FlareDirector.Use.cs") + PlayFile("Social", "Flare", "FlareDirector.Live.cs") + PlayFile("Social", "Flare", "FlareDirector.City.cs");
        Check(book.Contains("CountReviews();   // before the day's round trims the feed") && book.Contains("CountReviews();         // FlareDirector.Use"),
            "Flare reviews: the feed's reviews are counted before a day's round trims the feed and on the hour");
    }
}
