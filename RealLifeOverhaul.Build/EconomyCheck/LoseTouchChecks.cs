using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;

// Losing touch: from a person's page in People the player lets somebody go, after the game's own question, and somebody new
// takes their slot, so a full life (SocialRules.MaxCast) has room again. Family, the old friend, a partner and anyone a home,
// a plan, money or Flare still ties to the player stay, and the page says why. Once every slot is taken, an acquaintance long
// drifted away goes by themselves. A street talk that went well but finds no room says so instead of maybe next time.
internal static partial class Program
{
    private const double LoseRollMs = 2.0;   // the newcomer rolled for a freed slot, 400 names to keep clear of: once, when somebody goes

    private static void LoseTouch()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        LoseBlocks();
        LoseReplace();
        LoseRoundTrip();
        LoseDrifted();
        LoseNewest();
        LoseFlare();
        LoseSources();
        LoseTexts();
        SocialState.Reset();
        Console.WriteLine("  lose touch: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static SocialPerson LoseKnown(SocialOrigin origin, SocialStage stage)
    {
        return new SocialPerson { Index = 20, Name = "Dana Reyes", Origin = origin, Met = true, MetDay = 4, Stage = stage, LastContactDay = 4 };
    }

    // Each reason holds alone, in the order the page gives them; with none, they can go.
    private static void LoseBlocks()
    {
        Check(SocialRules.LoseBlockOf(null, false, false, false) == LoseBlock.NotKnown, "lose touch: nobody is nobody to lose");
        var stranger = LoseKnown(SocialOrigin.Street, SocialStage.Stranger);
        stranger.Met = false;
        Check(SocialRules.LoseBlockOf(stranger, false, false, false) == LoseBlock.NotKnown, "lose touch: somebody not met yet cannot be let go");
        Check(SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Family, SocialStage.Friend), false, false, false) == LoseBlock.Family, "lose touch: Mom is family and stays");
        Check(SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.OldFriend, SocialStage.Friend), false, false, false) == LoseBlock.OldFriend,
            "lose touch: the old friend, the phone's own friend contact, stays");
        Check(SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Gym, SocialStage.Dating), false, false, false) == LoseBlock.Together
              && SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Gym, SocialStage.Partner), false, false, false) == LoseBlock.Together,
            "lose touch: somebody the player is dating or with ends it with a talk, not by drifting apart");
        var moved = LoseKnown(SocialOrigin.Gym, SocialStage.Friend);
        moved.MovedIn = true;
        Check(SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Neighbour, SocialStage.Friend), false, true, false) == LoseBlock.LivesWith
              && SocialRules.LoseBlockOf(moved, false, false, false) == LoseBlock.LivesWith, "lose touch: a roommate, or somebody asked to move in, stays while so");
        Check(SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Work, SocialStage.Acquaintance), true, false, false) == LoseBlock.Plan,
            "lose touch: a plan with them, or them out with the player now, comes first");
        var owed = LoseKnown(SocialOrigin.Work, SocialStage.Friend);
        owed.Debt = 40;
        var owes = LoseKnown(SocialOrigin.Work, SocialStage.Friend);
        owes.Loan = 25;
        Check(SocialRules.LoseBlockOf(owed, false, false, false) == LoseBlock.Money && SocialRules.LoseBlockOf(owes, false, false, false) == LoseBlock.Money,
            "lose touch: money either way between them is settled first");
        Check(SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Online, SocialStage.Friend), false, false, true) == LoseBlock.Flare,
            "lose touch: one of Flare's influencers stays while Flare keeps their account");
        Check(SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Street, SocialStage.Acquaintance), false, false, false) == LoseBlock.None
              && SocialRules.LoseBlockOf(LoseKnown(SocialOrigin.Customer, SocialStage.CloseFriend), false, false, false) == LoseBlock.None,
            "lose touch: an acquaintance, a friend or a close friend with nothing holding them can go");
        Check(SocialCatalogue.LoseBlocks.Length == (int)LoseBlock.Flare + 1 && SocialCatalogue.LoseBlocks[(int)LoseBlock.None] == null
              && SocialCatalogue.LoseBlocks[(int)LoseBlock.NotKnown] == null,
            "lose touch: every reason but none has its line on the page");
        bool lined = true;
        for (int b = (int)LoseBlock.Family; b <= (int)LoseBlock.Flare; b++)
        {
            lined &= !string.IsNullOrEmpty(SocialCatalogue.LoseBlocks[b]);
        }
        Check(lined, "lose touch: family, the old friend, a partner, a home, a plan, money and Flare each say why");
    }

    // A slot given again: somebody new, rolled from the one who leaves, the same on every machine, and nothing of the old one left.
    private static void LoseReplace()
    {
        MoreFill(4242);
        int count = SocialState.Count;
        bool ok = true, differs = true, same = true, origin = true;
        string fault = string.Empty;
        foreach (int slot in new[] { 3, 9, 150, 399 })
        {
            SocialPerson gone = SocialState.Get(slot);
            gone.Met = true;
            gone.Stage = SocialStage.Acquaintance;
            SocialState.Plan.Person = slot;
            SocialState.Offer.Person = slot;
            int seed = SocialRules.ReplaceSeed(SocialState.CastSeed, slot, gone.Seed);
            SocialPerson next = SocialCatalogue.MakeRolled(slot, seed, gone.Origin, -1, 90, MoreYear, MoreDistricts, SocialState.People, SocialState.Count);
            SocialPerson again = SocialCatalogue.MakeRolled(slot, seed, gone.Origin, -1, 90, MoreYear, MoreDistricts, SocialState.People, SocialState.Count);
            same &= MoreText(next) == MoreText(again);
            differs &= next.Seed != gone.Seed && next.Name != gone.Name && seed != SocialRules.SlotSeed(SocialState.CastSeed, slot);
            origin &= next.Origin == gone.Origin && next.Index == slot && !next.Met && next.Memories.Count == 0 && next.Familiarity == 0f;
            if (!SocialState.Replace(next) || SocialState.Get(slot) != next || SocialState.Plan.Active || SocialState.Offer.Active)
            {
                ok = false;
                fault = fault.Length == 0 ? "slot " + slot : fault;
            }
            if (SocialState.NameTaken(next.Name, next))
            {
                ok = false;
                fault = fault.Length == 0 ? "slot " + slot + "'s new name is somebody else's" : fault;
            }
        }
        Check(ok && SocialState.Count == count, "lose touch: whoever leaves gives their slot to somebody new, the cast as long as it was, and their plan and question go with them " + fault);
        Check(differs, "lose touch: the newcomer is not the one who left over again: another face, another name, another roll than the slot's own");
        Check(same, "lose touch: the same cast and the same one leaving roll the same newcomer on every machine");
        Check(origin, "lose touch: the newcomer is met where the one who left would have been, not met yet, with nothing of theirs");
        Check(!SocialState.Replace(new SocialPerson { Index = SocialState.Count }) && !SocialState.Replace(new SocialPerson { Index = -1 }) && !SocialState.Replace(null),
            "lose touch: a slot outside the cast is never given");

        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            SocialCatalogue.MakeRolled(200, SocialRules.ReplaceSeed(SocialState.CastSeed, 200, i + 1), SocialOrigin.Street, -1, 90, MoreYear, MoreDistricts,
                SocialState.People, SocialState.Count);
        }
        double each = watch.Elapsed.TotalMilliseconds / 20.0;
        Check(each < LoseRollMs, "lose touch: a newcomer for a freed slot of a full cast takes " + each.ToString("0.000") + " ms (under " + LoseRollMs + " ms, once)");
    }

    // A save after somebody was let go reads back as it was written, the first eight's record still eight.
    private static void LoseRoundTrip()
    {
        MoreFill(3131);
        SocialPerson gone = SocialState.Get(5);
        SocialPerson next = SocialCatalogue.MakeRolled(5, SocialRules.ReplaceSeed(SocialState.CastSeed, 5, gone.Seed), gone.Origin, -1, 60, MoreYear, MoreDistricts,
            SocialState.People, SocialState.Count);
        SocialState.Replace(next);
        SocialPerson late = SocialState.Get(321);
        SocialState.Replace(SocialCatalogue.MakeRolled(321, SocialRules.ReplaceSeed(SocialState.CastSeed, 321, late.Seed), late.Origin, -1, 60, MoreYear, MoreDistricts,
            SocialState.People, SocialState.Count));
        var data = new Dictionary<string, string>();
        SocialState.Save(data);
        List<string> saved = MoreCast();
        SocialState.Load(data, 61, MoreYear, MoreDistricts, 77);
        bool same = MoreSame(saved, MoreCast(), out string fault);
        Check(same && SocialState.Count == SocialRules.MaxCast && data[MorePeopleKey].Split('\n').Length == SocialRules.BaseCast,
            "lose touch: a save with freed slots loads as it was saved, the first eight's record still eight, so an older version reads it " + fault);
    }

    // Once full, the acquaintance longest without a word goes by themselves, never before 60 days, never somebody held.
    private static void LoseDrifted()
    {
        var people = new SocialPerson[6];
        for (int i = 0; i < people.Length; i++)
        {
            people[i] = LoseKnown(SocialOrigin.Street, SocialStage.Acquaintance);
            people[i].Index = i;
            people[i].Drifted = true;
            people[i].LastContactDay = 100;
        }
        int day = 100 + SocialRules.DriftLoseDays;
        Check(SocialRules.DriftedAway(people[0], day) && !SocialRules.DriftedAway(people[0], day - 1), "lose touch: a drifted acquaintance may go after 60 days without a word, not a day before");
        people[1].Drifted = false;
        people[2].Stage = SocialStage.Friend;
        people[3].Met = false;
        Check(!SocialRules.DriftedAway(people[1], day + 50) && !SocialRules.DriftedAway(people[2], day + 50) && !SocialRules.DriftedAway(people[3], day + 50),
            "lose touch: only an acquaintance who drifted away goes by themselves; a friend, somebody in touch or a stranger waits");
        people[4].LastContactDay = 90;
        people[5].LastContactDay = 80;
        Check(SocialRules.LongestDrifted(people, people.Length, day, null) == 5, "lose touch: of those who may go, the one longest without a word");
        Check(SocialRules.LongestDrifted(people, people.Length, day, p => p.Index == 5) == 4, "lose touch: somebody held by a home, plans, money or Flare is passed over");
        people[0].LastContactDay = 80;
        Check(SocialRules.LongestDrifted(people, people.Length, day, null) == 0, "lose touch: on a tie, the lowest slot, the same on every machine");
        Check(SocialRules.LongestDrifted(people, people.Length, 100, null) == -1 && SocialRules.LongestDrifted(people, 0, day, null) == -1,
            "lose touch: nobody goes when nobody has been gone long enough, or there is nobody");
        people[0] = null;
        Check(SocialRules.LongestDrifted(people, people.Length, day, null) == 5, "lose touch: an empty slot is passed over");
    }

    // The test button's pick: the newest acquaintance who can go.
    private static void LoseNewest()
    {
        var people = new SocialPerson[5];
        for (int i = 0; i < people.Length; i++)
        {
            people[i] = LoseKnown(SocialOrigin.Street, SocialStage.Acquaintance);
            people[i].Index = i;
            people[i].MetDay = 10 + i;
        }
        people[4].Stage = SocialStage.Friend;
        people[3].Debt = 10;
        Func<SocialPerson, LoseBlock> block = p => SocialRules.LoseBlockOf(p, false, false, false);
        Check(SocialRules.NewestLoseable(people, people.Length, block) == 2, "lose touch test: the newest acquaintance nothing holds, not a friend or one owed money");
        people[1].MetDay = 12;
        Check(SocialRules.NewestLoseable(people, people.Length, block) == 2, "lose touch test: on the same day, the later slot");
        Check(SocialRules.NewestLoseable(people, 2, p => LoseBlock.Plan) == -1 && SocialRules.NewestLoseable(people, 0, block) == -1, "lose touch test: nobody when all are held, or none is known");
    }

    // Flare lets them go: their posts and replies, the replies to them, drafts about them, tags, likes and the follow.
    private static void LoseFlare()
    {
        var f = new FlareFile();
        const int who = 6, other = 7;
        f.Following = (1 << who) | (1 << other);
        f.Posts.Add(new FlarePost { Id = 1, Author = FlareAuthor.Friend, Who = who });
        f.Posts.Add(new FlarePost { Id = 2, Author = FlareAuthor.Player, ReplyTo = 1 });
        f.Posts.Add(new FlarePost { Id = 3, Author = FlareAuthor.Player, Tag = who, Likers = (1 << who) | (1 << other), Repliers = 1 << who });
        f.Posts.Add(new FlarePost { Id = 4, Author = FlareAuthor.Friend, Who = other });
        f.Posts.Add(new FlarePost { Id = 5, Author = FlareAuthor.Player, Tag = other });
        f.Drafts.Add(new FlareDraft { Id = 1, Person = who });
        f.Drafts.Add(new FlareDraft { Id = 2, Person = other });
        int removed = FlareRules.Forget(f, who);
        FlarePost tagged = f.Posts.Find(p => p.Id == 3);
        Check(removed == 2 && f.Posts.Count == 3 && f.Posts.Find(p => p.Id == 1) == null && f.Posts.Find(p => p.Id == 2) == null,
            "lose touch: Flare takes down their own posts and the player's replies to them");
        Check(tagged != null && tagged.Tag == -1 && tagged.Likers == 1 << other && tagged.Repliers == 0 && f.Following == 1 << other,
            "lose touch: no post is tagged with them, liked or answered by them, and they no longer follow");
        Check(f.Drafts.Count == 1 && f.Drafts[0].Person == other, "lose touch: nothing done with them is left to post about");
        Check(f.Posts.Find(p => p.Id == 4) != null && f.Posts.Find(p => p.Id == 5).Tag == other, "lose touch: everybody else's posts and tags are as they were");
        var big = new FlareFile();
        big.Following = 1 << 3;
        big.Posts.Add(new FlarePost { Id = 1, Author = FlareAuthor.Player, Tag = 300 });
        Check(FlareRules.Forget(big, 300) == 0 && big.Following == 1 << 3 && big.Posts[0].Tag == -1, "lose touch: somebody past the 31 who can follow is let go without touching the follows");
        Check(FlareRules.Forget(null, 3) == 0 && FlareRules.Forget(big, -1) == 0, "lose touch: no account or nobody: nothing changes");
    }

    // The wiring the page, the meetings and the other features stand on.
    private static void LoseSources()
    {
        string page = ModSource(@"Social\PeopleDialog.cs");
        string history = MemberText(page, "private DialogEntry History(");
        string lose = MemberText(page, "private DialogEntry LoseTouch(");
        Check(history.Contains("\"reallife_social_lose_btn\"") && history.Contains("LoseTouch(at)"), "lose touch: a person's page in People, under More about them, offers Lose touch");
        Check(lose.Contains("SocialDirector.LoseBlockOf(p)") && lose.Contains("SocialCatalogue.LoseBlocks") && lose.Contains("SocialDirector.AskLoseTouch(p)")
              && lose.Contains("finish?.Invoke();"),
            "lose touch: the page says why when somebody cannot go; otherwise the call ends and the game's question box asks");
        string director = ModSource(@"Social\SocialDirector.Lose.cs");
        Check(MemberText(director, "public static void AskLoseTouch(").Contains("GameConfirm.Show(") && !director.Contains("HudConfirm"),
            "lose touch: the question is the game's own box, through Core/GameConfirm");
        string letGo = MemberText(director, "internal static SocialPerson LoseTouch(");
        Check(letGo.Contains("SocialPhone.Remove(person)") && letGo.Contains("Forget(index)") && letGo.Contains("SocialState.Replace(next)")
              && letGo.Contains("SocialRules.ReplaceSeed("),
            "lose touch: letting go takes the contact off the phone, tells Flare and RLO Texts, and gives the slot to somebody new");
        Check(MemberText(director, "private static SocialPerson MakeRoom(").Contains("Settings.SocialRelationships"),
            "lose touch: nobody drifts away by themselves while relationships are kept as they are");
        string meetings = ModSource(@"Social\SocialDirector.Meetings.cs");
        Check(MemberText(meetings, "internal static SocialPerson Newcomer(").Contains("return MakeRoom(origin);") && MemberText(meetings, "public static bool AnyStranger()").Contains("RoomToMake()"),
            "lose touch: a full cast makes room from somebody long drifted away before it says nobody is left to meet");
        Check(ModSource(@"Wiring\RoommateLinks.cs").Contains("SocialLinks.LivesWith = LivesWith;") && ModSource(@"Wiring\FlareLinks.cs").Contains("SocialLinks.OnFlare = OnFlare;")
              && ModSource(@"Wiring\FlareLinks.cs").Contains("SocialLinks.FlareForget = FlareForget;") && ModSource(@"Wiring\Wiring.TextsCalls.cs").Contains("SocialLinks.TextsForget = Forget;"),
            "lose touch: the roommate and Flare say who they still need, and Flare and RLO Texts are told who went");
        Check(ModSource(@"Wiring\RoommateLinks.cs").Contains("SocialLinks.LivesWith = null;") && ModSource(@"Wiring\FlareLinks.cs").Contains("SocialLinks.OnFlare = null;")
              && ModSource(@"Wiring\Wiring.TextsCalls.cs").Contains("SocialLinks.TextsForget = null;"),
            "lose touch: each bridge is taken off when its feature is");
        string street = ModSource(@"Street\StreetDirector.Outcomes.cs");
        Check(MemberText(street, "private StreetLine Number()").Contains("\"reallife_street_note_full\""), "lose touch: a full life's refusal in the street comes with the game's notice of what to do");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains(".AddButton(\"reallife_social_test_lose\", () => SocialDirector.Instance?.TestLoseTouch())"),
            "lose touch: Options, Tests has its button");
    }

    // Every new line in the three languages written by hand, the What's new line in all nine, the Guide in three.
    private static void LoseTexts()
    {
        string folder = LocalesFolder();
        string[] keys =
        {
            "reallife_social_lose_btn", "reallife_social_lose_closing", "reallife_social_lose_ask_title", "reallife_social_lose_ask", "reallife_social_lose_btn_yes",
            "reallife_social_lose_btn_keep", "reallife_social_lost", "reallife_social_lost_drift", "reallife_social_people_full", "reallife_street_note_full",
            "reallife_social_test_lose", "reallife_social_test_lose_none", "reallife_social_test_lose_done", "reallife_social_test_lose_pass", "reallife_social_test_lose_fail"
        };
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string[] news = { "Lose touch:", "Perder contato:", "断了联系：", "Den Kontakt verlieren:", "Perdre le contact :", "Perder el contacto:", "Потерять связь:", "Contact verliezen:", "Bağlantıyı koparmak:" };
        string[] guide = { "Lose touch", "Perder contato", "断了联系" };
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, codes[l] + ".json"));
            string next = words != null ? NewsRow(words, news[l]) : null;
            Check(next != null && next.IndexOf(news[l], StringComparison.Ordinal) < FixAt(next),
                "lose touch: What's new's next row says so in " + codes[l] + ", before the fixes");
            Check(words != null && !words.ContainsKey("reallife_social_people_all_met"), "lose touch: the People page's old last line is gone in " + codes[l]);
            if (l >= 3)
            {
                continue;
            }
            var missing = new List<string>();
            foreach (string key in keys)
            {
                if (!words.TryGetValue(key, out string text) || text.Length == 0 || text.IndexOf('!') >= 0 || text.Contains(" - ") || text.Contains("—") || text.Contains("–"))
                {
                    missing.Add(key);
                }
            }
            foreach (string key in SocialCatalogue.LoseBlocks)
            {
                if (key != null && (!words.TryGetValue(key, out string text) || !text.Contains("{name}")))
                {
                    missing.Add(key);
                }
            }
            for (int n = 1; n <= 3; n++)
            {
                if (!words.ContainsKey("reallife_street_number_full_" + n))
                {
                    missing.Add("reallife_street_number_full_" + n);
                }
            }
            Check(missing.Count == 0, "lose touch: every line in " + codes[l] + ", without an exclamation mark or a dash (" + string.Join(", ", missing) + ")");
            string page = words.TryGetValue("reallife_guide_social", out string g) ? g : string.Empty;
            Check(page.Contains(guide[l]) && page.Contains("{social_lose_days}") && page.Contains("{social_cast}"), "lose touch: the Guide's People page says how, in " + codes[l]);
            Check(words.TryGetValue("reallife_street_note_full", out string note) && note.Length <= 240, "lose touch: the street's notice is a notification's length in " + codes[l]);
        }
    }

    // The What's new row that holds a line: the next row while it is unreleased, the version's own row once the version
    // script has numbered it (a check on the next row alone fails on every release's versioned tree). Null when none has it.
    internal static string NewsRow(Dictionary<string, string> words, string line)
    {
        if (words.TryGetValue("reallife_changes_next", out string next) && next.Contains(line))
        {
            return next;
        }
        foreach (KeyValuePair<string, string> kv in words)
        {
            if (kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !kv.Key.EndsWith("_title", StringComparison.Ordinal) && kv.Value.Contains(line))
            {
                return kv.Value;
            }
        }
        return null;
    }

    // Where a next row's fixes begin, or its length when it has none.
    private static int FixAt(string row)
    {
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: " };
        int at = row.Length;
        foreach (string mark in marks)
        {
            int i = row.IndexOf(mark, StringComparison.Ordinal);
            if (i >= 0 && i < at)
            {
                at = i;
            }
        }
        return at;
    }
}
