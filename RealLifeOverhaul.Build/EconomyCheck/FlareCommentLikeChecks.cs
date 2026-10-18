using System;
using RealLifeOverhaul.Flare;

// Checks for liking a comment under a post that is not the player's (FlareCommentRules.LikeKey and ToggleLike, the "C"
// lines of FlareStore): a comment can be liked as a post can. Held here: the like toggles; the
// key is the post, the slot and who (never the number the comment is shown under); it survives the save, which an older
// version reads as before; a post that is gone, pruned or dropped with a session's host takes its likes with it; the kept
// likes have a bound; the like touches nothing else and is sent nowhere; and no row of any kind in Flare has a button
// that does nothing (source checks).
internal static partial class Program
{
    private static void FlareCommentLikeChecks()
    {
        CommentLikeToggle();
        CommentLikeSave();
        CommentLikeGone();
        CommentLikeSource();
    }

    private static FlareFile CommentLikeFile()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Posts.Add(new FlarePost { Id = 7, Author = FlareAuthor.Citizen, Who = 3, Source = FlareSource.CityTalk, Day = 2, Hour = 9, Replies = 20 });
        f.Posts.Add(new FlarePost { Id = 9, Author = FlareAuthor.Business, Name = "Kettle", Source = FlareSource.CityTalk, Day = 2, Hour = 10, Replies = 20 });
        return f;
    }

    private static void CommentLikeToggle()
    {
        FlareFile f = CommentLikeFile();
        int followers = f.Followers, reputation = f.Reputation, likes = f.TotalLikes;
        Check(!FlareCommentRules.Liked(f, 7, 2, 'c', 40) && FlareCommentRules.ToggleLike(f, 7, 2, 'c', 40) && FlareCommentRules.Liked(f, 7, 2, 'c', 40)
              && !FlareCommentRules.Liked(f, 7, 1, 'c', 40) && !FlareCommentRules.Liked(f, 9, 2, 'c', 40) && !FlareCommentRules.Liked(f, 7, 2, 'f', 40) && !FlareCommentRules.Liked(f, 7, 2, 'c', 41),
            "Flare comment likes: pressing the heart likes that comment and no other");
        Check(!FlareCommentRules.ToggleLike(f, 7, 2, 'c', 40) && !FlareCommentRules.Liked(f, 7, 2, 'c', 40) && f.CommentLikes.Count == 0, "Flare comment likes: pressing it again takes the like back");
        long a = FlareCommentRules.LikeKey(7, 2, 'c', 40);
        Check(a >= 0 && a == FlareCommentRules.LikeKey(7, 2, 'c', 40) && a != FlareCommentRules.LikeKey(7, 3, 'c', 40) && a != FlareCommentRules.LikeKey(8, 2, 'c', 40)
              && a != FlareCommentRules.LikeKey(7, 2, 'i', 40) && a != FlareCommentRules.LikeKey(7, 2, 'c', 39) && FlareCommentRules.PostOf(a) == 7,
            "Flare comment likes: the key is the post, the slot and who, the same each time and different for each");
        int post, slot, kind, who;
        FlareCommentRules.Parts(FlareCommentRules.LikeKey(123456, 15, 'i', (1 << 26) - 1), out post, out slot, out kind, out who);
        Check(post == 123456 && slot == 15 && kind == 2 && who == (1 << 26) - 1 && FlareCommentRules.FromParts(post, slot, kind, who) == FlareCommentRules.LikeKey(123456, 15, 'i', (1 << 26) - 1),
            "Flare comment likes: the key's parts come back whole at the largest values");
        Check(!FlareCommentRules.ToggleLike(f, 99, 0, 'c', 1) && !FlareCommentRules.ToggleLike(f, 7, -1, 'c', 1) && !FlareCommentRules.ToggleLike(f, 7, 16, 'c', 1)
              && !FlareCommentRules.ToggleLike(f, 7, 0, 'x', 1) && !FlareCommentRules.ToggleLike(f, 7, 0, 'c', -1) && !FlareCommentRules.ToggleLike(f, 7, 0, 'c', 1 << 26)
              && FlareCommentRules.FromParts(7, 0, 3, 1) == -1 && f.CommentLikes.Count == 0,
            "Flare comment likes: no post, a slot, kind or who out of range keeps nothing");
        for (int i = 0; i < FlareCommentRules.LikesKept + 40; i++)
        {
            FlareCommentRules.ToggleLike(f, i % 2 == 0 ? 7 : 9, i % 16, 'c', i);
        }
        int last = FlareCommentRules.LikesKept + 39;
        Check(f.CommentLikes.Count == FlareCommentRules.LikesKept && FlareCommentRules.Liked(f, last % 2 == 0 ? 7 : 9, last % 16, 'c', last) && !FlareCommentRules.Liked(f, 7, 0, 'c', 0),
            "Flare comment likes: the kept likes have a bound, and the oldest goes first");
        FlareFile g = CommentLikeFile();
        FlareCommentRules.ToggleLike(g, 7, 0, 'f', 2);
        Check(g.Followers == followers && g.Reputation == reputation && g.TotalLikes == likes && g.Posts[0].Likes == 0 && !g.Posts[0].Liked && g.Posts[0].PlayerLikes == 0 && g.Posts[0].Reposts == 0 && g.ReplyLikes.Count == 0,
            "Flare comment likes: a like changes no followers, standing or counts of the post");
        Check(FlareCommentRules.IsDisplay(FlareCommentRules.DisplayId(0, 0)) && FlareCommentRules.IsDisplay(FlareCommentRules.DisplayId(1000000, 15)) && !FlareCommentRules.IsDisplay(-1000 - 1000000 * 32 - 31)
              && !FlareCommentRules.IsDisplay(-1) && !FlareCommentRules.IsDisplay(0) && !FlareCommentRules.IsDisplay(int.MaxValue),
            "Flare comment likes: a comment's number is told from a friend's reply's and a post's");
        Check(FlareCommentRules.KindOf(FlareAuthor.Friend) == 'f' && FlareCommentRules.KindOf(FlareAuthor.Influencer) == 'i' && FlareCommentRules.KindOf(FlareAuthor.Citizen) == 'c',
            "Flare comment likes: the author a comment is shown as gives back its kind");
    }

    private static void CommentLikeSave()
    {
        FlareFile f = CommentLikeFile();
        FlareCommentRules.ToggleLike(f, 7, 2, 'c', 40);
        FlareCommentRules.ToggleLike(f, 9, 0, 'i', 3);
        FlareRules.ToggleReplyLike(f, 7, 1);
        string account = FlareStore.WriteAccount(f), posts = FlareStore.WritePosts(f);
        FlareFile back = FlareStore.Read(account, posts);
        Check(back != null && back.CommentLikes.Count == 2 && FlareCommentRules.Liked(back, 7, 2, 'c', 40) && FlareCommentRules.Liked(back, 9, 0, 'i', 3) && !FlareCommentRules.Liked(back, 9, 0, 'c', 3)
              && back.ReplyLikes.Count == 1 && FlareStore.WriteAccount(back) == account,
            "Flare comment likes: through the save, the liked comments stay liked and nothing else is");
        Check(account.Split('\n')[0] == FlareStore.WriteAccount(CommentLikeFile()).Split('\n')[0] && account.Contains("\nC|7|2|0|40") && account.Contains("\nC|9|0|2|3"),
            "Flare comment likes: they are lines of their own after the header, which an older version does not read");
        string older = string.Join("\n", Array.FindAll(account.Split('\n'), l => !l.StartsWith("C|", StringComparison.Ordinal)));
        FlareFile old = FlareStore.Read(older, posts);
        Check(old != null && old.CommentLikes.Count == 0 && old.ReplyLikes.Count == 1 && old.Posts.Count == 2, "Flare comment likes: a save from before, without them, loads with none");
        FlareFile damaged = FlareStore.Read(account + "\nC|x|1|0|2\nC|7\nC|7|99|0|1\nC|7|1|5|1\nC|7|1|0|-2\nC|7|2|0|40", posts);
        Check(damaged != null && damaged.CommentLikes.Count == 2, "Flare comment likes: a damaged or repeated line is skipped");
    }

    private static void CommentLikeGone()
    {
        FlareFile f = CommentLikeFile();
        FlareCommentRules.ToggleLike(f, 7, 2, 'c', 40);
        FlareCommentRules.ToggleLike(f, 9, 1, 'c', 5);
        f.Posts.RemoveAt(0);
        string account = FlareStore.WriteAccount(f);
        Check(!account.Contains("C|7|") && account.Contains("C|9|1|0|5"), "Flare comment likes: a post that is gone is not written with its likes");
        Check(FlareCommentRules.DropLikes(f) == 1 && f.CommentLikes.Count == 1 && FlareCommentRules.Liked(f, 9, 1, 'c', 5), "Flare comment likes: a post that is gone takes its likes with it");
        FlareFile h = CommentLikeFile();
        FlareCommentRules.ToggleLike(h, 7, 2, 'c', 40);
        string posts = FlareStore.WritePosts(h);
        FlareFile loaded = FlareStore.Read(FlareStore.WriteAccount(h), posts.Substring(posts.IndexOf("\nP", StringComparison.Ordinal) + 1));   // the first post is missing from the posts
        Check(loaded != null && loaded.CommentLikes.Count == 0, "Flare comment likes: loading drops the likes of a post the save no longer has");
        FlareFile old = CommentLikeFile();
        FlareCommentRules.ToggleLike(old, 7, 0, 'c', 1);
        old.Posts[1].Day = 30;   // a later post: the first is far older than the city's posts are kept
        FlareRules.Prune(old);
        Check(old.Post(7) == null && old.CommentLikes.Count == 0, "Flare comment likes: an old city post pruned takes its likes with it");
        FlareFile guest = CommentLikeFile();
        guest.Posts[1].Shared = true;
        guest.Posts[1].NetId = 44;
        guest.Net.HostKey = "a";
        FlareCommentRules.ToggleLike(guest, 9, 0, 'c', 1);
        FlareCommentRules.ToggleLike(guest, 7, 0, 'c', 1);
        Check(FlareNetShare.NewHost(guest, "b") && guest.CommentLikes.Count == 1 && FlareCommentRules.Liked(guest, 7, 0, 'c', 1),
            "Flare comment likes: a session's shared post dropped with a new host takes its likes with it");
        FlareFile own = CommentLikeFile();
        FlareCommentRules.ToggleLike(own, 7, 0, 'c', 1);
        Check(FlareNetShare.DropOwnCity(own, 2) == 2 && own.CommentLikes.Count == 0, "Flare comment likes: the guest's own city posts dropped take their likes with them");
    }

    private static void CommentLikeSource()
    {
        string cells = ReviewSource("Social/Flare/FlareApp.Cells.cs"), screens = ReviewSource("Social/Flare/FlareApp.Screens.cs"), live = (ReviewSource("Social/Flare/FlareDirector.Live.cs") + ReviewSource("Social/Flare/FlareDirector.Cases.cs") + ReviewSource("Social/Flare/FlareDirector.Posting.cs"));
        string like = FlareMethod(live, "public void LikeComment");
        Check(like.Length > 0 && like.Contains("FlareCommentRules.ToggleLike") && !like.Contains("Shared_") && !like.Contains("Affection") && !like.Contains("Reputation") && !like.Contains("Followers") && !like.Contains("Net."),
            "Flare comment likes: the like is kept in the file and sent nowhere, with no effect on the game");
        string click = FlareMethod(screens, "private void LikeClicked");
        Check(click.Contains("LikeComment") && click.Contains("FlareCommentRules.IsDisplay(row.Post.Id)") && click.IndexOf("LikeComment", StringComparison.Ordinal) < click.IndexOf("LikeReply", StringComparison.Ordinal),
            "Flare comment likes: pressing the heart of a comment reaches the director, before a friend's reply is looked for");
        string comments = FlareMethod(screens, "private int Comments(FlarePost p, long now)");
        Check(comments.Contains("Slot = c.Slot") && comments.Contains("Liked = _director.IsCommentLiked(p.Id, c.Slot, c.Kind, c.Who)"), "Flare comment likes: a comment is shown liked when it is, and carries its slot");
        // No dead button on any row: every row made for display (a negative number) is one with a slot (a friend's reply or
        // a comment), whose heart works; answer and share are hidden on them; and the rows with a post of the file's work.
        Check(cells.Contains("bool display = p.Id < 0;") && cells.Contains("FlareKit.Show(_replyHit, !display)") && cells.Contains("FlareKit.Show(_repostHit, !display)")
              && cells.Contains("FlareKit.Show(_likeHit, !display || p.Slot >= 0)"),
            "Flare comment likes: no comment or friend's reply shows an answer or share button that does nothing");
        int made = 0, slotted = 0;
        for (int at = screens.IndexOf("new FlarePost", StringComparison.Ordinal); at >= 0; at = screens.IndexOf("new FlarePost", at + 1, StringComparison.Ordinal))
        {
            string body = screens.Substring(at, Math.Min(500, screens.Length - at));
            made++;
            slotted += body.Contains("Slot = ") ? 1 : 0;
        }
        Check(made == 2 && slotted == 2, "Flare comment likes: every row the thread makes for display has a slot, so its heart works (" + slotted + " of " + made + ")");
        string reply = FlareMethod(screens, "private void ReplyClicked"), repost = FlareMethod(screens, "private void RepostClicked"), post = FlareMethod(screens, "private void PostClicked");
        Check(reply.Contains("Id >= 0") && repost.Contains("Id >= 0") && post.Contains("Id >= 0"), "Flare comment likes: answer, share and open act on every post of the file's");
        int bubbleAt = cells.IndexOf("private sealed class BubbleCell", StringComparison.Ordinal), profileAt = cells.IndexOf("private sealed class ProfileCell", StringComparison.Ordinal);
        string bubble = bubbleAt >= 0 && profileAt > bubbleAt ? cells.Substring(bubbleAt, profileAt - bubbleAt) : string.Empty;
        Check(bubble.Length > 0 && !bubble.Contains("Action(") && !bubble.Contains("Pill(") && !bubble.Contains("FlareKit.Click"), "Flare comment likes: a message's bubble has no button at all");
    }
}
