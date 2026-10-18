using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

// Checks for liking a friend's reply under one of the player's posts (FlareRules.ReplyKey and ToggleReplyLike, the
// "R" lines of FlareStore): the like toggles, the key does not depend on the made-up negative number, it survives the
// save, a save from before reads as before, a post that is gone takes its likes with it, the like touches nothing else
// and goes nowhere, and no row of a friend's reply has a button that does nothing.
internal static partial class Program
{
    private static void FlareReplyLikeChecks()
    {
        FlareReplyLikeToggle();
        FlareReplyLikeSave();
        FlareReplyLikeGone();
        FlareReplyLikeSource();
    }

    private static FlareFile ReplyLikeFile()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Posts.Add(new FlarePost { Id = 7, Author = FlareAuthor.Player, Source = FlareSource.Photo, Day = 2, Hour = 9 });
        f.Posts.Add(new FlarePost { Id = 9, Author = FlareAuthor.Player, Source = FlareSource.Photo, Day = 2, Hour = 10 });
        return f;
    }

    private static void FlareReplyLikeToggle()
    {
        FlareFile f = ReplyLikeFile();
        int followers = f.Followers, reputation = f.Reputation, likes = f.TotalLikes;
        Check(!FlareRules.ReplyLiked(f, 7, 2) && FlareRules.ToggleReplyLike(f, 7, 2) && FlareRules.ReplyLiked(f, 7, 2) && !FlareRules.ReplyLiked(f, 7, 1) && !FlareRules.ReplyLiked(f, 9, 2),
            "Flare reply likes: pressing the heart likes that friend's reply and no other");
        Check(!FlareRules.ToggleReplyLike(f, 7, 2) && !FlareRules.ReplyLiked(f, 7, 2) && f.ReplyLikes.Count == 0, "Flare reply likes: pressing it again takes the like back");
        Check(FlareRules.ReplyKey(7, 2) != FlareRules.ReplyKey(7, 3) && FlareRules.ReplyKey(7, 2) != FlareRules.ReplyKey(8, 2) && FlareRules.ReplyKey(7, 2) == FlareRules.ReplyKey(7, 2)
              && FlareRules.ReplyKey(7, 31) != FlareRules.ReplyKey(8, 0), "Flare reply likes: the key is the post and the place, the same each time and different for each");
        Check(!FlareRules.ToggleReplyLike(f, 99, 0) && !FlareRules.ToggleReplyLike(f, 7, -1) && !FlareRules.ToggleReplyLike(f, 7, 32) && f.ReplyLikes.Count == 0, "Flare reply likes: no post, no place or a place out of range keeps nothing");
        for (int i = 0; i < FlareRules.ReplyLikesMost + 20; i++)
        {
            FlareRules.ToggleReplyLike(f, 7, i % 32);
            FlareRules.ToggleReplyLike(f, 9, i % 32);
        }
        Check(f.ReplyLikes.Count <= FlareRules.ReplyLikesMost, "Flare reply likes: the kept likes have a bound");
        FlareFile g = ReplyLikeFile();
        FlareRules.ToggleReplyLike(g, 7, 0);
        Check(g.Followers == followers && g.Reputation == reputation && g.TotalLikes == likes && g.Posts[0].Likes == 0 && !g.Posts[0].Liked && g.Posts[0].PlayerLikes == 0 && g.Posts[0].Reposts == 0,
            "Flare reply likes: a like changes no followers, standing or counts of the post");
    }

    private static void FlareReplyLikeSave()
    {
        FlareFile f = ReplyLikeFile();
        FlareRules.ToggleReplyLike(f, 7, 2);
        FlareRules.ToggleReplyLike(f, 9, 0);
        string account = FlareStore.WriteAccount(f), posts = FlareStore.WritePosts(f);
        FlareFile back = FlareStore.Read(account, posts);
        Check(back != null && back.ReplyLikes.Count == 2 && FlareRules.ReplyLiked(back, 7, 2) && FlareRules.ReplyLiked(back, 9, 0) && !FlareRules.ReplyLiked(back, 9, 2) && FlareStore.WriteAccount(back) == account,
            "Flare reply likes: through the save, the liked replies stay liked and nothing else is");
        Check(account.Split('\n')[0] == FlareStore.WriteAccount(ReplyLikeFile()).Split('\n')[0] && account.Contains("\nR|"), "Flare reply likes: they are lines of their own after the header, which an older version does not read");
        string older = string.Join("\n", Array.FindAll(account.Split('\n'), l => !l.StartsWith("R|", StringComparison.Ordinal)));
        FlareFile old = FlareStore.Read(older, posts);
        Check(old != null && old.ReplyLikes.Count == 0 && old.Posts.Count == 2, "Flare reply likes: a save from before, without them, loads with none");
        FlareFile damaged = FlareStore.Read(account + "\nR|x\nR\nR|-4\nR|" + FlareRules.ReplyKey(7, 2) + "\nR|" + FlareRules.ReplyKey(9, 0), posts);
        Check(damaged != null && damaged.ReplyLikes.Count == 2, "Flare reply likes: a damaged or repeated line is skipped");
    }

    private static void FlareReplyLikeGone()
    {
        FlareFile f = ReplyLikeFile();
        FlareRules.ToggleReplyLike(f, 7, 2);
        FlareRules.ToggleReplyLike(f, 9, 1);
        f.Posts.RemoveAt(0);
        Check(!FlareStore.WriteAccount(f).Contains("R|" + FlareRules.ReplyKey(7, 2)) && FlareStore.WriteAccount(f).Contains("R|" + FlareRules.ReplyKey(9, 1)), "Flare reply likes: a post that is gone is not written with its likes");
        Check(FlareRules.DropReplyLikes(f) == 1 && f.ReplyLikes.Count == 1 && FlareRules.ReplyLiked(f, 9, 1), "Flare reply likes: a post that is gone takes its likes with it");
        FlareFile h = ReplyLikeFile();
        FlareRules.ToggleReplyLike(h, 7, 2);
        string posts = FlareStore.WritePosts(h), account = FlareStore.WriteAccount(h);
        FlareFile loaded = FlareStore.Read(account, posts.Substring(posts.IndexOf("\nP", StringComparison.Ordinal) + 1));   // the first post is missing from the posts
        Check(loaded != null && loaded.ReplyLikes.Count == 0, "Flare reply likes: loading drops the likes of a post the save no longer has");
    }

    private static void FlareReplyLikeSource()
    {
        string cells = (ReviewSource("Social/Flare/FlareApp.Cells.cs") + ReviewSource("Social/Flare/FlareApp.Cards.cs")), screens = (ReviewSource("Social/Flare/FlareApp.Screens.cs") + ReviewSource("Social/Flare/FlareApp.Profile.cs") + ReviewSource("Social/Flare/FlareApp.Business.cs") + ReviewSource("Social/Flare/FlareApp.Shown.cs") + ReviewSource("Social/Flare/FlareApp.Clicks.cs")), live = (ReviewSource("Social/Flare/FlareDirector.Live.cs") + ReviewSource("Social/Flare/FlareDirector.Cases.cs") + ReviewSource("Social/Flare/FlareDirector.Posting.cs"));
        string like = FlareMethod(live, "public void LikeReply");
        Check(like.Length > 0 && like.Contains("ToggleReplyLike") && !like.Contains("Shared_") && !like.Contains("Affection") && !like.Contains("Reputation") && !like.Contains("Followers") && !like.Contains("Net."),
            "Flare reply likes: the like is kept in the file and sent nowhere, with no effect on the game");
        string click = FlareMethod(screens, "private void LikeClicked");
        Check(click.Contains("LikeReply") && click.Contains("Slot >= 0"), "Flare reply likes: pressing the heart of a friend's reply reaches the director");
        Check(screens.Contains("Liked = _director.IsReplyLiked(p.Id, i)") && screens.Contains("Slot = i"), "Flare reply likes: a friend's reply is shown liked when it is");
        Check(cells.Contains("FlareKit.Show(_replyHit, !display)") && cells.Contains("FlareKit.Show(_repostHit, !display)") && cells.Contains("bool display = p.Id < 0;"),
            "Flare reply likes: a friend's reply shows no answer or share button that does nothing");
    }
}
