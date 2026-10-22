using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Texts;

// Every comment a post's count promises written out and shown (the fault: a resident's post with
// a count of 3 showed the player's own reply and "And 2 more replies.", the two comments never written): the asks of each
// comment (FlareCommentAsks: asked again after a miss, the open thread's asked ahead, a cap on the background's share),
// the budget no longer locked down by the background requests the queue holds back itself (TextsHub.BudgetMiss), the
// walk's order (due comments first) and its second's look, the thread written again the moment words come, the times
// of the comments, and the comment intents of a v3 or later pack (a resident's post on a v2 topic is answered on its own
// words, which the card carries).
internal static partial class Program
{
    private static void FlareCommentsAsked()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        CommentAsksTracker();
        CommentAsksBudget();
        CommentAsksThread();
        CommentAsksTimes();
        CommentAsksMap();
        CommentAsksSources();
        Console.WriteLine("  flare comment asks: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- each comment's asks

    private static void CommentAsksTracker()
    {
        var a = new FlareCommentAsks();
        long id = FlareCommentRules.Asked(12, 3);
        Check(a.May(id, false) && !a.Waiting(id) && !a.Worded(id), "Flare comment asks: a comment never asked may be asked");
        a.Asked(id, false);
        Check(!a.May(id, false) && a.Waiting(id) && a.Background == 1, "Flare comment asks: while its request waits it is not asked again by the walk, and it counts in the background's share");
        Check(a.May(id, true), "Flare comment asks: the open thread asks a comment waiting at the background's priority again, ahead of it");
        a.Ended(id, false);
        a.Asked(id, true);
        Check(a.Background == 0 && !a.May(id, true) && !a.May(id, false), "Flare comment asks: the open thread's request is not the background's share, and is not asked a third time while it waits");
        a.Ended(id, false);
        Check(a.May(id, false) && !a.Worded(id), "Flare comment asks: a request back without words (time ran out, refused, a restart) frees the comment to be asked again");
        a.Asked(id, false);
        a.Ended(id, false);
        a.Asked(id, false);
        a.Ended(id, false);
        Check(!a.May(id, false) && !a.May(id, true) && a.Background == 0, "Flare comment asks: at most " + FlareCommentAsks.Most + " asks of one comment a session (then it waits for the next load)");
        long other = FlareCommentRules.Asked(12, 4);
        a.Asked(other, false);
        a.Ended(other, true);
        Check(a.Worded(other) && !a.May(other, true) && !a.Waiting(other), "Flare comment asks: a comment with words is never asked again");
        long kept = FlareCommentRules.Asked(13, 0);
        a.Kept(kept);
        Check(a.Worded(kept) && !a.May(kept, false), "Flare comment asks: words found kept (a load, the host's) are never asked for");
        long waiting = FlareCommentRules.Asked(14, 0);
        a.Asked(waiting, false);
        int gone = a.Prune(post => post == 14);
        Check(gone == 3 && a.Waiting(waiting) && a.Count == 1, "Flare comment asks: the comments of posts that are gone are let go, never one whose request waits (" + gone + " went)");
        a.Clear();
        Check(a.Count == 0 && a.Background == 0 && a.May(waiting, false), "Flare comment asks: another file starts with nothing asked");
        bool distinct = true;
        var ids = new HashSet<long>();
        for (int post = 0; post < 200; post++)
        {
            for (int slot = 0; slot < FlareCommentRules.WrittenMost; slot++)
            {
                distinct &= ids.Add(FlareCommentRules.Asked(post, slot)) && (int)(FlareCommentRules.Asked(post, slot) >> 4) == post;
            }
        }
        Check(distinct, "Flare comment asks: every slot of every post has a number of its own, and the number names its post (sixteen slots in four bits)");
        bool likes = true;
        for (int slot = 0; slot < FlareCommentRules.WrittenMost; slot++)
        {
            long key = FlareCommentRules.LikeKey(7, slot, 'c', 40);
            FlareCommentRules.Parts(key, out int post, out int back, out int kind, out int who);
            likes &= key >= 0 && back == slot && post == 7 && who == 40;
            likes &= FlareCommentRules.DisplayId(7, slot) != FlareCommentRules.DisplayId(7, (slot + 1) % FlareCommentRules.WrittenMost);
        }
        Check(likes, "Flare comment asks: every one of the sixteen slots can be liked and shown under a number of its own");
    }

    // ---------------------------------------------------------------- the budget is not locked down by what the queue held back

    private static void CommentAsksBudget()
    {
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner() };
        int handed = 0;
        for (int i = 0; i < 40; i++)
        {
            hub.Ask(TextsAskOf("bg" + i, "friend.crash", null, w => handed++, TextPriority.Background, 5), 0);
        }
        for (double now = 0; now <= 30; now += 1)
        {
            q.Take(now, false);   // (the game is not quiet: the background waits)
            hub.Look(now);
        }
        Check(handed == 40 && hub.Budget.Step == 0 && q.Floor == TextPriority.Background,
            "texts, the budget: background requests the queue held back (the game not quiet) and that ran out never step the budget down (step " + hub.Budget.Step + ", " + handed + " handed back)");
        var q2 = new TextQueue();
        var hub2 = new TextsHub(q2) { Runner = new PretendRunner() };
        for (int i = 0; i < 40; i++)
        {
            hub2.Ask(TextsAskOf("bg" + i, "friend.crash", null, w => { }, TextPriority.Background, 5), 0);
        }
        q2.SetBudget(120, 120);
        for (double now = 0; now <= 30; now += 1)
        {
            while (q2.Take(now, true) != null)
            {
                // (taken and never answered: the model is stuck)
            }
            hub2.Look(now);
        }
        Check(hub2.Budget.Step > 0, "texts, the budget: requests the model ran and could not answer in time still step it down (step " + hub2.Budget.Step + ")");
        var job = new TextJob { Priority = TextPriority.Background };
        bool back = !hub.BudgetMiss(job);
        job.Ran = true;
        back &= hub.BudgetMiss(job);
        var soon = new TextJob { Priority = TextPriority.Soon };
        back &= hub.BudgetMiss(soon);
        q.Floor = TextPriority.Now;
        back &= !hub.BudgetMiss(soon);
        q.Floor = TextPriority.Background;
        Check(back, "texts, the budget: a miss counts when the model ran it, or when it was a kind the budget lets through; never the background held back, never a kind below the floor");
        var q3 = new TextQueue();
        var taken = new TextJob { Key = "k", Priority = TextPriority.Background, Deadline = 100 };
        q3.Add(taken, 0);
        TextJob got = q3.Take(1, true);
        Check(got == taken && taken.Ran, "texts, the queue: a request the worker takes is marked as run");
    }

    // ---------------------------------------------------------------- the thread: what the card counts, minute by minute

    private static void CommentAsksThread()
    {
        // The post of the fault: a resident's review, 3 replies, opened 22 hours after it went up. With all three worded the
        // thread shows the two whose time has come and no "more" line at the card's count of two (the third comes at the day's end).
        var p = CommentPost(385, 3, 600, FlareAuthor.Citizen, FlareSource.Review, 3);
        var plan = new List<FlareComment>();
        FlareCommentRules.Plan(p, 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, plan);
        long now = p.At + 22 * 60;
        int shown = FlareLiveRules.Shown(p.Replies, p, now), due = FlareCommentRules.Due(plan, now);
        Check(plan.Count == 3 && due == shown && FlareCommentRules.More(shown, due) == 0,
            "Flare comment asks: the screenshot's post (3 replies, 22h old) writes out all three, shows the " + due + " due of a count of " + shown + " and no \"more\" line once they are worded");
        Check(FlareCommentRules.More(shown, due - 1) == 1, "Flare comment asks: a due comment whose words have not come yet counts in the \"more\" line until they do");
        bool agree = true;
        for (int total = 0; total <= 60; total++)
        {
            var q = CommentPost(900 + total, total, 600);
            FlareCommentRules.Plan(q, 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, plan);
            for (long t = q.At; t <= q.At + 1500; t += 13)
            {
                int s = FlareLiveRules.Shown(total, q, t), d = FlareCommentRules.Due(plan, t);
                agree &= d + FlareCommentRules.More(s, d) == s && (total > FlareCommentRules.WrittenMost || FlareCommentRules.More(s, d) == 0);
            }
        }
        Check(agree, "Flare comment asks: with every comment worded, the thread's comments plus the \"more\" line equal the card's count at every minute, and \"more\" shows only past sixteen");
    }

    // ---------------------------------------------------------------- the comments' own times

    private static void CommentAsksTimes()
    {
        var plan = new List<FlareComment>();
        bool own = true, spread = true;
        for (int k = 0; k < 200; k++)
        {
            var p = CommentPost(2000 + k, 1 + k % 16, 900);
            FlareCommentRules.Plan(p, 77, FlareLiveRules.Citizens, null, FlareTaste.None, null, plan);
            long now = p.At + 22 * 60;
            for (int i = 0; i < plan.Count; i++)
            {
                var shownAs = new FlarePost();
                FlareLiveRules.SetAt(shownAs, plan[i].At);   // (as the thread puts it: FlareApp.Screens.Comments)
                own &= shownAs.At == plan[i].At && plan[i].At > p.At && plan[i].At <= p.At + 1440;
                spread &= i == 0 || plan[i].At >= plan[i - 1].At;
            }
            spread &= plan.Count < 3 || plan[plan.Count - 1].At - plan[0].At >= 30;
        }
        Check(own, "Flare comment asks: each comment is shown at its own minute (after the post, within its day), never at the minute the thread is opened");
        Check(spread, "Flare comment asks: the comments come in order over the hours, not all at once");
        string screens = (TextsSource("Social/Flare/FlareApp.Screens.cs") + TextsSource("Social/Flare/FlareApp.Profile.cs") + TextsSource("Social/Flare/FlareApp.Business.cs") + TextsSource("Social/Flare/FlareApp.Shown.cs") + TextsSource("Social/Flare/FlareApp.Clicks.cs"));
        Check(screens.Contains("FlareLiveRules.SetAt(comment, c.At);") && !screens.Contains("FlareLiveRules.SetAt(comment, now"),
            "Flare comment asks sources: a comment's time label is its own arrival (\"3h\", \"45m\"), from the plan");
    }

    // ---------------------------------------------------------------- a v3 or later pack: the post's own words

    private static void CommentAsksMap()
    {
        string Of(FlareAuthor author, FlareSource source, int arg, int data, int niche = -1) => FlareTextsMap.Comment(new FlarePost { Author = author, Source = source, Arg = arg }, niche, data);
        int[] talks = { (int)RealLifeOverhaul.Flare.FlareTalk.Weather, (int)RealLifeOverhaul.Flare.FlareTalk.Food, (int)RealLifeOverhaul.Flare.FlareTalk.Night,
            (int)RealLifeOverhaul.Flare.FlareTalk.Sport, (int)RealLifeOverhaul.Flare.FlareTalk.Commute, (int)RealLifeOverhaul.Flare.FlareTalk.Everyday,
            (int)RealLifeOverhaul.Flare.FlareTalk.Prices, (int)RealLifeOverhaul.Flare.FlareTalk.Crime, (int)RealLifeOverhaul.Flare.FlareTalk.Event };
        bool words = true, v2 = true;
        for (int i = 0; i < talks.Length; i++)
        {
            words &= Of(FlareAuthor.Citizen, FlareSource.CityTalk, talks[i] * 10, 5) == "citizen.comment.words";
            v2 &= Of(FlareAuthor.Citizen, FlareSource.CityTalk, talks[i] * 10, TextIntents.DataV2).StartsWith("citizen.thread.", StringComparison.Ordinal);
        }
        Check(words && v2, "Flare comments map: with a v3 or later pack a resident's post on a v2 topic is answered on its own words (citizen.comment.words); a v2 pack keeps citizen.thread.*");
        Check(Of(FlareAuthor.Business, FlareSource.BusinessPost, 0, 5) == "citizen.comment.words" && Of(FlareAuthor.Influencer, FlareSource.Niche, 0, 5, (int)FlareNiche.Food) == "citizen.comment.words"
              && Of(FlareAuthor.Friend, FlareSource.Life, (int)FlareRules.LifeTopic.Food * 10, 5) == "citizen.comment.words",
            "Flare comments map: a shop's sale, a food influencer's post and a friend's post are answered on their words too");
        Check(Of(FlareAuthor.Citizen, FlareSource.Review, 3, 5) == "citizen.comment.review_bad" && Of(FlareAuthor.Citizen, FlareSource.Review, 4, 5) == "citizen.comment.review_good"
              && Of(FlareAuthor.Official, FlareSource.Official, (int)FlareOfficialPost.Patrols * 10, 5) == "citizen.react.patrols"
              && Of(FlareAuthor.Citizen, FlareSource.CityTalk, (int)RealLifeOverhaul.Flare.FlareTalk.Accident * 10, 5) == "citizen.comment.accident"
              && Of(FlareAuthor.Network, FlareSource.Opened, 0, 5) == "citizen.reply.opened",
            "Flare comments map: a resident's review keeps its own kind (the business and its stars, as trained), the city's news its reaction, an accident its v3 kind, another player's post its reaction");
        var food = new FlarePost { Author = FlareAuthor.Friend, Source = FlareSource.Life, Arg = (int)FlareRules.LifeTopic.Food * 10 };
        Check(FlareTextsMap.CommentTaste(food, -1) == FlareTaste.Food && FlareTextsMap.CommentTaste(new FlarePost { Author = FlareAuthor.Official, Source = FlareSource.Official }, -1) == FlareTaste.None,
            "Flare comments map: a friend still comments only on a topic they care about, whatever intent words it");
        var r = new TextRequest();
        var post = new FlareCommentPost { First = "Dana", Handle = "@dana_nyc", Words = "Tried the new noodle place on 5th Avenue. Worth the queue." };
        bool built = FlareTextsMap.CommentCard(r, "citizen.comment.words", post);
        Check(built && r.Intent == "citizen.comment.words" && r.History.Count == 1 && r.History[0].Said == post.Words && r.Slots.Get("other") == "Dana",
            "Flare comments map: the card of a comment on a post's words carries the post's own words in its history, and its poster");
        var resident = new FlarePost { Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Arg = (int)RealLifeOverhaul.Flare.FlareTalk.Food * 10, Who = 3, Replies = 3, Seed = 9 };
        Check(FlareTextsMap.Comment(resident, -1, 5) != null && FlareCommentRules.Has(resident), "Flare comments map: every resident's post with replies has comments with a v5 pack");
    }

    // ---------------------------------------------------------------- the sources

    private static void CommentAsksSources()
    {
        string director = TextsSource("Social/Flare/FlareDirector.Comments.cs"), flare = TextsSource("Social/Flare/FlareDirector.cs"), app = TextsSource("Social/Flare/FlareApp.cs"),
            hub = TextsSource("Texts/TextsHub.cs");
        Check(director.Contains("AskComments(first, now, true, true);") && director.Contains("AskComments(p, now, pass == 0, false);")
              && director.IndexOf("AskComments(first, now, true, true);", StringComparison.Ordinal) < director.IndexOf("if (now == _commentsLooked && !_commentsFreed)", StringComparison.Ordinal),
            "Flare comment asks sources: the open thread's due comments are asked first, ahead of the walk; the walk asks the due comments before those still to come");
        Check(director.Contains("_commentsFreed = true;") && director.Contains("_view?.ThreadChanged(parent.Id);") && flare.Contains("CommentsSecond();")
              && app.Contains("public void ThreadChanged(int postId)"),
            "Flare comment asks sources: a request back frees the next ask on the director's second, and words that come show at once in an open thread");
        Check(director.Contains("_commentAsks.Ended(asked, words != null);") && !director.Contains("_commentsAsked.Add("),
            "Flare comment asks sources: a request back without words is asked again (no longer once a session)");
        Check(!director.Contains("void Update") && !director.Contains("LateUpdate") && hub.Contains("Over(i, !w.ViaService && BudgetMiss(w.Job), TextShownWhy.Slow);"),
            "Flare comment asks sources: nothing per frame; the hub counts a miss through BudgetMiss");
    }
}
