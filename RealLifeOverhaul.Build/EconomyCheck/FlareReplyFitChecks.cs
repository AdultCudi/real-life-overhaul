using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

// Answering a review on Flare (a player's report of 24 Nov 2026: "answering reviews seems to do nothing", thanks never did anything and standing your
// ground only ever lost): each answer fits the review, stands or costs a little, by one rule for both interfaces; the effect is saved, bounded, and told.
internal static partial class Program
{
    private static void FlareReplyFitChecks()
    {
        FlareReplyFitTable();
        FlareReplyFitBounds();
        FlareReplyFitFigures();
        FlareReplyFitSaved();
        FlareReplyFitSources();
        FlareReplyFitTexts();
    }

    // What the Guide states, written out apart from the rules: the answer, the stars, whether the poor review is out of line, and what it does.
    private static string ExpectedReply(FlareReplyOption option, int stars, bool unfair)
    {
        switch (option)
        {
            case FlareReplyOption.Thanks:
                return stars >= 4 ? "thanked" : stars == 3 ? "stands" : "raised";
            case FlareReplyOption.Sorry:
                return stars <= 2 && !unfair ? "eased" : "stands";
            default:
                return stars <= 2 ? (unfair ? "eased" : "raised") : "soured";
        }
    }

    private static void FlareReplyFitTable()
    {
        int wrong = 0, thankedGood = 0;
        var seen = new HashSet<string>();
        for (int o = 0; o < FlareReplyRules.Options; o++)
        {
            var option = (FlareReplyOption)o;
            bool anyEffect = false, anyFit = false, anyCost = false;
            for (int stars = 1; stars <= 5; stars++)
            {
                for (int u = 0; u < 2; u++)
                {
                    bool unfair = u == 1 && stars <= 2;
                    int quality = FlareReplyRules.Quality(option, stars, unfair);
                    string got = FlareReplyRules.ResultOf(quality, stars).ToString().ToLowerInvariant();
                    wrong += got == ExpectedReply(option, stars, unfair) ? 0 : 1;
                    FlareFit fit = FlareReplyRules.Fit(option, stars, unfair);
                    anyEffect |= got != "stands";
                    anyFit |= fit == FlareFit.Fits;
                    anyCost |= fit == FlareFit.Costs;
                    thankedGood += option == FlareReplyOption.Thanks && stars >= 4 && got == "thanked" && u == 0 ? 1 : 0;
                    seen.Add(FlareReplyRules.LineKey(option, stars, unfair));
                }
            }
            Check(anyEffect && anyFit, "Flare replies: " + option + " has an effect on some review and fits one (it is never without effect, never always a loss)");
            Check(anyCost || option == FlareReplyOption.Sorry, "Flare replies: " + option + " can miss, so the choice means something");
        }
        Check(wrong == 0, "Flare replies: each answer on each star count does what the Guide states (" + wrong + " differ)");
        Check(thankedGood == 2, "Flare replies: thanks fit a review of 4 and of 5 stars");
        Check(seen.Count == 6, "Flare replies: the sheet says one of six lines under each option (" + seen.Count + ")");
        // A saved quality reads back as what it was: older answers (800 thanks to a good review, 500 sorry to a good one, 900 sorry to three stars) keep their meaning.
        Check(FlareReplyRules.ResultOf(800, 5) == FlareResult.Thanked && FlareReplyRules.ResultOf(500, 5) == FlareResult.Stands && FlareReplyRules.ResultOf(900, 3) == FlareResult.Stands
              && FlareReplyRules.ResultOf(300, 2) == FlareResult.Stands && FlareReplyRules.ResultOf(0, 2) == FlareResult.None && FlareReplyRules.ResultOf(150, 1) == FlareResult.Raised,
            "Flare replies: an answer saved by an older version reads as what it did then, nothing taken back");
        // The tone of a typed reply (the old panel) is the option of the sheet (the kit), and each tone's reply costs or fits as the sheet's option does.
        Check(FlareReplyRules.OptionOf(FlareTone.Cheerful) == FlareReplyOption.Thanks && FlareReplyRules.OptionOf(FlareTone.Proud) == FlareReplyOption.Thanks
              && FlareReplyRules.OptionOf(FlareTone.Honest) == FlareReplyOption.Sorry && FlareReplyRules.OptionOf(FlareTone.Cheeky) == FlareReplyOption.Defend
              && FlareReplyRules.Tone(FlareReplyRules.OptionOf(FlareTone.Cheerful)) == FlareTone.Cheerful && FlareReplyRules.Tone(FlareReplyRules.OptionOf(FlareTone.Honest)) == FlareTone.Honest
              && FlareReplyRules.Tone(FlareReplyRules.OptionOf(FlareTone.Cheeky)) == FlareTone.Cheeky,
            "Flare replies: the old panel's tone and the kit's option are the same answer");
        // Out of line: a poor review of a business rated 4.0 or more over five reviews or more, and no other.
        FlareStarRow high = Row(11, 2, 0, 0, 1);   // 14 reviews, 4.57
        FlareStarRow few = Row(2, 1, 0, 0, 2);     // 5 reviews but 3.2
        FlareStarRow short4 = Row(3, 1, 0, 0, 0);  // 4 reviews, 4.75
        FlareStarRow mid = Row(7, 2, 0, 3, 3);     // 15 reviews, 3.5
        Check(FlareReplyRules.Unfair(1, high) && FlareReplyRules.Unfair(2, high) && !FlareReplyRules.Unfair(3, high) && !FlareReplyRules.Unfair(1, few) && !FlareReplyRules.Unfair(1, short4)
              && !FlareReplyRules.Unfair(1, mid) && !FlareReplyRules.Unfair(1, null),
            "Flare replies: a poor review is out of line only with a rating of 4.0 or more over at least 5 reviews");
    }

    private static void FlareReplyFitBounds()
    {
        // Replying to every review of a shop moves its rating by 0.4 of a star at the most, up or down, whatever the mix of stars and the count.
        int worstUp = 0, worstDown = 0, thankedWrong = 0;
        for (int total = 5; total <= 520; total += 3)
        {
            for (int poorShare = 0; poorShare <= 100; poorShare += 5)
            {
                int poor = total * poorShare / 100, rest = total - poor;
                for (int shape = 0; shape < 3; shape++)
                {
                    int one = shape == 0 ? poor : shape == 1 ? poor / 2 : 0, two = poor - one;
                    int five = shape == 2 ? rest : rest / 2, four = rest - five;
                    FlareStarRow eased = Row(five, four, 0, two, one);
                    FlareStarRow cost = Row(five, four, 0, two, one);
                    FlareStarRow thanked = Row(five, four, 0, two, one);
                    int plain = FlareReplyRules.Plain(eased);
                    for (int i = 0; i < one; i++)
                    {
                        FlareReplyRules.Answered(eased, 1, FlareReplyRules.Quality(FlareReplyOption.Sorry, 1));
                        FlareReplyRules.Answered(cost, 1, FlareReplyRules.Quality(FlareReplyOption.Defend, 1));
                    }
                    for (int i = 0; i < two; i++)
                    {
                        FlareReplyRules.Answered(eased, 2, FlareReplyRules.Quality(FlareReplyOption.Sorry, 2));
                        FlareReplyRules.Answered(cost, 2, FlareReplyRules.Quality(FlareReplyOption.Defend, 2));
                    }
                    for (int i = 0; i < five; i++)
                    {
                        FlareReplyRules.Answered(thanked, 5, FlareReplyRules.Quality(FlareReplyOption.Thanks, 5));
                    }
                    for (int i = 0; i < four; i++)
                    {
                        FlareReplyRules.Answered(thanked, 4, FlareReplyRules.Quality(FlareReplyOption.Thanks, 4));
                    }
                    worstUp = Math.Max(worstUp, FlareReplyRules.Average(eased) - plain);
                    worstDown = Math.Max(worstDown, plain - FlareReplyRules.Average(cost));
                    thankedWrong += FlareReplyRules.Average(thanked) == plain ? 0 : 1;
                }
            }
        }
        Check(thankedWrong == 0, "Flare replies: thanking every good review leaves the rating as it was (" + thankedWrong + " mixes differ)");
        Check(worstUp <= FlareReplyRules.MostLift && worstDown <= FlareReplyRules.MostLift && FlareReplyRules.MostLift == 400,
            "Flare replies: answering every review of a shop moves its rating by 0.4 of a star at the most (up " + worstUp + ", down " + worstDown + " thousandths)");
        // The extreme the cap is for: half of the reviews five stars, half one star, every poor one eased would lift 0.67 without it.
        FlareStarRow split = Row(50, 0, 0, 0, 50);
        for (int i = 0; i < 50; i++)
        {
            FlareReplyRules.Answered(split, 1, FlareReplyRules.Quality(FlareReplyOption.Sorry, 1));
        }
        Check(FlareReplyRules.Average(split) == 3000 + FlareReplyRules.MostLift, "Flare replies: the cap holds the extreme at 0.4 of a star (" + FlareReplyRules.Average(split) + ")");
        // A good review's weight is never changed by an answer, so defending or thanking a five star review cannot lift or lower the rating.
        FlareStarRow good = Row(10, 5, 3, 2, 2);
        int before = FlareReplyRules.Average(good);
        FlareReplyRules.Answered(good, 5, FlareReplyRules.Quality(FlareReplyOption.Defend, 5));
        FlareReplyRules.Answered(good, 4, FlareReplyRules.Quality(FlareReplyOption.Defend, 4));
        FlareReplyRules.Answered(good, 3, FlareReplyRules.Quality(FlareReplyOption.Defend, 3));
        Check(FlareReplyRules.Average(good) == before && good.Raised == 0, "Flare replies: an answer that does not fit a review of three stars or more costs talk, never a lift in the rating");
        // One reply to a review counts: the book takes the first and ignores a second (FlareReviewBookChecks), and the sources below hold both interfaces to it.
    }

    // The real figures of one answered review for a business with 20, 100 and 500 reviews, and what the page shows of them.
    private static void FlareReplyFitFigures()
    {
        int[] counts = { 1, 5, 25 };
        int[] low = new int[3], high = new int[3];
        string[] shownBefore = new string[3], shownAfter = new string[3];
        for (int i = 0; i < 3; i++)
        {
            int k = counts[i];   // 20, 100 and 500 reviews at 3.5: one star k, two stars 2k, three stars 3k, four stars 14k
            FlareStarRow row = Row(0, 14 * k, 3 * k, 2 * k, k);
            int before = FlareReplyRules.Average(row);
            FlareReplyRules.Answered(row, 1, FlareReplyRules.Quality(FlareReplyOption.Sorry, 1));
            int after = FlareReplyRules.Average(row);
            low[i] = before;
            high[i] = after;
            shownBefore[i] = (before / 1000f).ToString("0.00", CultureInfo.InvariantCulture);
            shownAfter[i] = (after / 1000f).ToString("0.00", CultureInfo.InvariantCulture);
        }
        Check(low[0] == 3500 && high[0] - low[0] >= 60 && high[0] - low[0] <= 70 && high[1] - low[1] >= 10 && high[1] - low[1] <= 14 && high[2] - low[2] >= 2 && high[2] - low[2] <= 3,
            "Flare replies: a one star review answered with sorry lifts a rating of 3.5 by " + (high[0] - low[0]) + " thousandths among 20 reviews, " + (high[1] - low[1]) + " among 100, "
            + (high[2] - low[2]) + " among 500 (the Guide says 0.06, 0.01, 0.002)");
        Check(shownBefore[0] != shownAfter[0] && shownBefore[1] != shownAfter[1] && shownBefore[2] == shownAfter[2],
            "Flare replies: at two decimals the rating shows the change among 20 and 100 reviews (" + shownBefore[0] + " to " + shownAfter[0] + ", " + shownBefore[1] + " to " + shownAfter[1]
            + ") and says it is too small among 500 (" + shownBefore[2] + ")");
    }

    private static void FlareReplyFitSaved()
    {
        // The effect reaches the stored rating and survives a save and a load: the city's rows and notes, and the account's book.
        var c = new FlareCityState { Seed = 5 };
        FlareStarRow row = Row(4, 3, 1, 2, 2);   // 12 reviews, 3.4: a poor review is fair here
        row.Key = "cafe";
        c.StarRows.Add(row);
        int plain = FlareReplyRules.Plain(row);
        var note = new FlareReviewNote { Key = "cafe", Person = 7, Stars = 1, Day = 3, Why = 1 };
        c.ReviewNotes.Add(note);
        int quality = FlareReplyRules.Quality(FlareReplyOption.Sorry, 1, FlareReplyRules.Unfair(1, row));
        note.Answer = "reallife_flare_reply_sorry";
        note.Quality = quality;
        FlareReplyRules.Answered(FlareUseRules.StarsOf(c.StarRows, "cafe"), 1, quality);
        int shown = FlareReplyRules.Average(row);
        FlareCityState back = FlareCityStore.Decode(FlareCityStore.Encode(c), out _);
        FlareStarRow read = back != null && back.StarRows.Count == 1 ? back.StarRows[0] : null;
        FlareReviewNote readNote = back != null && back.ReviewNotes.Count == 1 ? back.ReviewNotes[0] : null;
        Check(shown > plain && read != null && FlareReplyRules.Average(read) == shown && readNote != null && readNote.Quality == quality && readNote.Answer == note.Answer
              && FlareReplyRules.ResultOf(readNote.Quality, readNote.Stars) == FlareResult.Eased,
            "Flare replies: a sorry reaches the stored rating (" + plain + " to " + shown + ") and the review's mark, and both come back from a save");
        // The account's book: an answered feed review keeps its answer in the stored rating, and the kept answer names its review.
        FlareFile f = FlareRules.Open(5, 10, 3, 50);
        f.Posts.Add(new FlarePost { Id = 7, Day = 10, Hour = 9, Author = FlareAuthor.Citizen, Who = 1, Source = FlareSource.Review, Place = "Cafe~1", Name = "Cafe", Arg = 1 });
        var reply = new FlarePost { Id = 8, Day = 10, Hour = 10, Author = FlareAuthor.Player, Source = FlareSource.Reply, Place = "Cafe~1", ReplyTo = 7 };
        f.Posts.Add(reply);
        long when = FlareLiveRules.Now(10, 11, 0);
        FlareReviewBook.Answered(f, reply, FlareReplyRules.FitQuality, when);
        f.Answers.Add(new FlareReviewNote { Key = "Cafe~1", Person = 7, Stars = 1, Day = 10, Quality = FlareReplyRules.FitQuality, Answer = "words" });
        FlareFile again = FlareStore.Read(FlareStore.WriteAccount(f), FlareStore.WritePosts(f));
        FlareStarRow kept = again != null ? FlareReviewBook.With(again, null, "Cafe~1", when) : null;
        Check(kept != null && kept.Eased == 1 && again.Answers.Count == 1 && again.Answers[0].Person == 7 && again.Answers[0].Quality == FlareReplyRules.FitQuality,
            "Flare replies: an answered feed review keeps its weight and its mark through the account's save");
        // The pieces a save made before this change holds read as before.
        var older = new List<FlareReviewNote> { new FlareReviewNote { Key = "cafe", Stars = 2, Answer = "x", Quality = 900 }, new FlareReviewNote { Key = "cafe", Stars = 5, Answer = "x", Quality = 150 } };
        FlareStarRow oldRow = Row(1, 0, 0, 1, 0);
        oldRow.Key = "cafe";
        FlareReplyRules.Recount(older, new List<FlareStarRow> { oldRow });
        Check(oldRow.Eased == 1 && oldRow.Raised == 0, "Flare replies: an older save's answers are counted again by the same rule, a rude answer to a good review no longer counted as weight");
    }

    private static void FlareReplyFitSources()
    {
        string use = ModSource(@"Social\Flare\FlareDirector.Use.cs"), posting = ModSource(@"Social\Flare\FlareDirector.Posting.cs"), kit = ModSource(@"Social\Flare\FlareDirector.Kit.cs");
        string replies = ModSource(@"Social\Flare\FlareDirector.Replies.cs"), sheet = ModSource(@"Ui\Flare\FlareScreen.Sheet.cs"), compose = ModSource(@"Social\Flare\FlareApp.Compose.cs");
        string desk = ModSource(@"Ui\Flare\FlareScreen.Desk.cs"), screens = ModSource(@"Ui\UiScreens.Flare.cs");
        string noteUse = FlareMethod(use, "private void NoteUse(FlarePost p, FlareCompose c)");
        string reply = FlareMethod(posting, "private FlarePost Reply(FlareCompose c, string text, int day, out string why)");
        string toReview = FlareMethod(kit, "public bool ReplyToReview(int postId, FlareReplyOption option, string words, FlareTone? tone = null)");
        string toNote = FlareMethod(kit, "public bool ReplyToNote(FlareReviewNote note, FlareReplyOption option, string words)");
        string answered = FlareMethod(replies, "private void ReviewAnswered(FlarePost reply, FlareCompose c)");
        string talk = FlareMethod(replies, "private void AnswerTalk(string place, FlareResult result, FlarePost reply, int day)");
        // The old panel's reply and the kit's reply end in one method, which works out the effect by FlareReplyRules and tells the player.
        Check(noteUse.Contains("ReviewAnswered(p, c);") && !noteUse.Contains("SoftenPlace") && !noteUse.Contains("AnswerQuality") && !use.Contains("public bool AnswerReview("),
            "Flare replies: a reply typed in the old panel counts through the one rule, not by its length, and softens nothing by itself");
        Check(toReview.Contains("QualityFor(option, stars, review.Place)") && toReview.Contains("Compose(c, out _)") && !toReview.Contains("Worse(") && !toReview.Contains("Soften"),
            "Flare replies: the kit's reply to a feed review goes through Compose and so through the same method as the old panel's");
        Check(answered.Contains("FlareReplyRules.OptionOf(reply.Tone)") && answered.Contains("FlareReviewBook.Answered(_file, reply, quality, now)") && answered.Contains("RepliesTo(review.Id) != 1")
              && answered.Contains("AnswerTalk(") && answered.Contains("Told(") && answered.Contains("FlareGame.Business(review.Place) == null"),
            "Flare replies: the one answer to a review of the player's own business counts once into the stored rating, moves the talk and is told");
        Check(!reply.Contains("CityAnswer") && !reply.Contains("Buzz(") && !reply.Contains("Applied"), "Flare replies: posting a reply moves nothing by itself, the rule does");
        Check(toNote.Contains("FlareReplyRules.Quality(option, stars, FlareReplyRules.Unfair(stars, shown))") && toNote.Contains("AnswerTalk(place, result, null, CurrentDay)") && toNote.Contains("Told(")
              && toNote.Contains("!string.IsNullOrEmpty(note.Answer)") && toNote.Contains("FlareReplyRules.Answered(FlareUseRules.StarsOf(_city.StarRows, note.Key), stars, quality)"),
            "Flare replies: a resident's review is answered once, counted into the city's stored rating, and its talk moved by the same method");
        Check(talk.Contains("SoftenPlace(place)") && talk.Contains("Worse(place, day)") && talk.Contains("Glad(place, day)") && talk.Contains("FlareReplyRules.TalkOf(result)"),
            "Flare replies: eased, soured and thanked talk are moved in one place");
        Check(replies.Contains("FlareReplyRules.LineKey(FlareReplyRules.OptionOf(tone)") && sheet.Contains("FlareReplyRules.LineKey(option, stars, FlareReplyRules.Unfair(stars, row))")
              && compose.Contains("_director.ReplyHint(parent, (FlareTone)tone)") && sheet.Contains("_flare.StarsWithPosts(r.Post.Place, _flare.Now)") && sheet.Contains("_flare.StarsWithPosts(shop.Place, _flare.Now)"),
            "Flare replies: both interfaces say before the answer how it fits, and the kit's figures are of the rating the page shows");
        Check(desk.Contains("ResultLine(_flare.ResultOfPost(p.Id, r.Stars))") && desk.Contains("ResultLine(FlareReplyRules.ResultOf(n.Quality, r.Stars))"),
            "Flare replies: an answered review shows how it was answered, a feed review's and a resident's");
        // Everything that moves Sour for a reply is reached through AnswerTalk, the business answer of the city's own, or the rival-answer path.
        string all = use + ModSource(@"Social\Flare\FlareDirector.Cases.cs") + replies + kit;
        int softens = 0;
        for (int at = all.IndexOf("SoftenPlace(", StringComparison.Ordinal); at >= 0; at = all.IndexOf("SoftenPlace(", at + 1, StringComparison.Ordinal))
        {
            softens++;
        }
        Check(softens == 4, "Flare replies: the talk of a business is softened by four callers only (the method, the answer rule, a business answering a review of the player's, a careful answer to a rival) (" + softens + ")");
        foreach (string key in new[]
                 {
                     "reallife_flare_kit_option_glad", "reallife_flare_kit_option_unfair", "reallife_flare_kit_option_costs_good", "reallife_flare_kit_result_eased", "reallife_flare_kit_result_raised",
                     "reallife_flare_kit_result_thanked", "reallife_flare_kit_result_soured", "reallife_flare_kit_result_stands"
                 })
        {
            Check(screens.Contains("\"" + key + "\""), "Flare replies: the kit's screen lists the text " + key);
        }
    }

    private static void FlareReplyFitTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var keys = new List<string>();
        for (int o = 0; o < FlareReplyRules.Options; o++)
        {
            for (int s = 1; s <= 5; s++)
            {
                keys.Add(FlareReplyRules.LineKey((FlareReplyOption)o, s, true));
                keys.Add(FlareReplyRules.LineKey((FlareReplyOption)o, s, false));
            }
        }
        keys.AddRange(new[]
        {
            "reallife_flare_kit_result_eased", "reallife_flare_kit_result_raised", "reallife_flare_kit_result_thanked", "reallife_flare_kit_result_soured", "reallife_flare_kit_result_stands",
            "reallife_flare_reply_rating_moved", "reallife_flare_reply_rating_same", "reallife_flare_reply_rating_left"
        });
        int missing = 0;
        foreach (string key in keys)
        {
            missing += en.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text) ? 0 : 1;
        }
        Check(missing == 0, "Flare replies: every line the sheet and a reviewed card can say is written (" + missing + " missing)");
        int longest = 0;
        string[] done = { "eased", "raised", "thanked", "soured", "stands" };
        bool allThere = true;
        foreach (string d in done)
        {
            allThere &= en.TryGetValue("reallife_flare_reply_done_" + d, out string text);
            if (en.TryGetValue("reallife_flare_reply_done_" + d, out text))
            {
                string filled = text.Replace("{business}", "Corner Cafe And Bakery Number One").Replace("{stars}", "1").Replace("{rating}", en["reallife_flare_reply_rating_same"].Replace("{before}", "3.50").Replace("{count}", "500"));
                longest = Math.Max(longest, filled.Length);
            }
        }
        Check(allThere && longest <= 240, "Flare replies: each notice after a reply fits in 240 characters with a long business name and the longest rating line (" + longest + ")");
        string guide = en.TryGetValue("reallife_guide_flare", out string g) ? g : string.Empty;
        Check(guide.Contains("# Answering a review") && guide.Contains("0.4 of a star") && guide.Contains("4.0 or more over at least 5 reviews") && guide.Contains("about 0.06 among 20 reviews, 0.01 among 100 and 0.002 among 500")
              && guide.Contains("<b>Thank them</b>, <b>Say sorry</b> and <b>Stand your ground</b>") && guide.Contains("<b>Cheerful</b> or <b>Proud</b> thanks, <b>Honest</b> says sorry, <b>Cheeky</b> stands your ground")
              && !guide.Contains("politely (not cheeky)"),
            "Flare replies: the Guide's Flare page states the rules and the figures the code holds");
        Check(WhatsNewHas(en, "Answering a review on Flare used different rules in the old and the new interface"), "Flare replies: What's new tells of the fix and the rule");
        string docs = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\flare-city.md")));
        Check(docs.Contains("Answering a review") && docs.Contains("FlareReplyRules.MostLift") && docs.Contains("FlareDirector.ReviewAnswered"), "Flare replies: docs/features/flare-city.md holds the rules");
    }
}
