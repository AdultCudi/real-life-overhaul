using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Meeting somebody's friends as a game (Social/SocialInterests, SocialFriendsRules, SocialImpression, SocialFriendsStore). Players asked for hints to
// be dropped in dates and for the friends to ask what the player and the person have in common, with right answers giving big trust gains and
// wrong ones the opposite (a player, 26 Nov), and for some trust to be inferred from reputation. Every rule here is game-free: what each person is
// into, which of it the player has heard, the quiz made from it, what an answer is worth, the first standing the friends give, the first
// impression of a name, and the save. --friends29 runs them alone; the age window's checks are in --romance (RomanceChecks.cs).
internal static partial class Program
{
    private const int F29Year = 28;
    private const int F29Districts = 9;

    private static void Friends29()
    {
        int before = _run, failedBefore = _failed;
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        F29Interests(en);
        F29Hints();
        F29Quiz();
        F29Scoring();
        F29Impression();
        F29Save();
        F29Off();
        F29Texts(en);
        F29Sources();
        SocialState.Reset();
        Console.WriteLine("  friends29: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // ---------------------------------------------------------------- what each person is into

    private static void F29Interests(Dictionary<string, string> en)
    {
        int kinds = SocialInterests.Kinds, per = SocialInterests.PerKind;
        Check(kinds == Enum.GetValues(typeof(InterestKind)).Length && kinds == 9 && SocialInterests.AllHeard == 511 && per >= 10,
            "friends: nine kinds of thing a person can be into (a show, a hobby, a sport, music, a dream trip, a food loved, a food hated, a plan, a pet hate) and at least ten of each to be");

        var a = new int[kinds];
        var b = new int[kinds];
        bool same = true, ranges = true, seeds = true;
        var seen = new HashSet<int>[kinds];
        for (int k = 0; k < kinds; k++)
        {
            seen[k] = new HashSet<int>();
        }
        for (int seed = 1; seed <= 3000; seed++)
        {
            SocialInterests.Roll(seed, a);
            SocialInterests.Roll(seed, b);
            for (int k = 0; k < kinds; k++)
            {
                same &= a[k] == b[k] && SocialInterests.ValueOf(seed, k) == a[k];
                ranges &= a[k] >= 0 && a[k] < per;
                seen[k].Add(a[k]);
            }
        }
        bool covered = true;
        for (int k = 0; k < kinds; k++)
        {
            covered &= seen[k].Count == per;
        }
        Check(same && ranges, "friends: a person's things are rolled from their seed alone: the same seed gives the same set, every one of 3000 seeds, each a thing the kind has");
        Check(covered, "friends: across people every thing of every kind is somebody's (not one favourite for everybody)");

        // A stream of its own: the cast is dealt as ever whether or not anybody's things were ever asked for, and a set does not depend on the cast.
        var plain = new SocialPerson[SocialRules.BaseCast];
        var asked = new SocialPerson[SocialRules.BaseCast];
        bool rest = true, apart = true;
        for (int seed = 1; seed <= 200; seed++)
        {
            SocialCatalogue.MakeCast(plain, seed, 3, F29Year, F29Districts);
            SocialCatalogue.MakeCast(asked, seed, 3, F29Year, F29Districts);
            for (int i = 0; i < plain.Length; i++)
            {
                SocialInterests.Roll(SocialInterests.SeedOf(asked[i]), a);
                SocialInterests.Roll(SocialInterests.SeedOf(plain[i]), b);
                apart &= a[0] == b[0] && a[8] == b[8];
            }
            SocialCatalogue.MakeCast(asked, seed, 3, F29Year, F29Districts);   // dealt again after the things were asked for
            for (int i = 0; i < plain.Length; i++)
            {
                rest &= plain[i].Seed == asked[i].Seed && plain[i].Traits == asked[i].Traits && plain[i].Name == asked[i].Name && plain[i].Age == asked[i].Age
                        && plain[i].Job == asked[i].Job && plain[i].District == asked[i].District && plain[i].Birthday == asked[i].Birthday && plain[i].Wants == asked[i].Wants
                        && plain[i].Compatible == asked[i].Compatible && plain[i].FreeFrom == asked[i].FreeFrom;
            }
        }
        Check(rest && apart, "friends: nothing else of the cast moves: a cast dealt after everybody's things were asked for is the cast dealt before (200 seeds), and a set is theirs alone");
        string catalogue = ModSource(@"Social\SocialCatalogue.cs"), interests = ModSource(@"Social\SocialInterests.cs");
        Check(catalogue.Length > 0 && !catalogue.Contains("SocialInterests.") && !catalogue.Contains("InterestSeed") && interests.Contains("new Random(SocialRules.SlotSeed(seed, InterestSalt))")
              && interests.Contains("InterestSalt = 9101"),
            "friends: the cast's own rolls never touch the things (SocialCatalogue does not read them), which come from a generator seeded from the person's seed and a salt of their own");

        // The seed is kept the first time it is needed, so a look given later never changes what they like.
        var person = new SocialPerson { Seed = 777 };
        SocialInterests.Roll(SocialInterests.SeedOf(person), a);
        person.InterestSeed = person.Seed;
        person.Seed = 4242;   // a face replaced later
        SocialInterests.Roll(SocialInterests.SeedOf(person), b);
        bool kept = true;
        for (int k = 0; k < kinds; k++)
        {
            kept &= a[k] == b[k];
        }
        Check(kept && SocialInterests.SeedOf(new SocialPerson { Seed = 9 }) == 9, "friends: the seed their things come from is kept once used, so a new look never changes them; before that it is their own");

        // The texts: every thing has one, written with a capital, and all of them differ.
        if (en == null)
        {
            Check(false, "friends: en.json could not be read");
            return;
        }
        bool texts = true, distinct = true, style = true;
        var all = new HashSet<string>();
        string fault = string.Empty;
        for (int k = 0; k < kinds; k++)
        {
            texts &= SocialInterests.Things[k].Length == per;
            foreach (string key in SocialInterests.Things[k])
            {
                if (!en.TryGetValue(key, out string text) || text.Length == 0)
                {
                    texts = false;
                    fault = fault.Length == 0 ? key : fault;
                    continue;
                }
                distinct &= all.Add(text);
                style &= char.IsUpper(text[0]) && F29Plain(text) && text.Length <= 64;
            }
        }
        Check(texts && distinct && style, "friends: each of the " + kinds * per + " things has its text, a capital first, different from every other, plain (no dash, no mark of surprise, no semicolon) and short enough for a button" + (fault.Length > 0 ? "; " + fault : string.Empty));
    }

    private static bool F29Plain(string text)
    {
        return text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && text.IndexOf('!') < 0 && text.IndexOf(';') < 0 && !text.Contains(" - ") && text.IndexOf('-') < 0;
    }

    // ---------------------------------------------------------------- hints

    private static void F29Hints()
    {
        int all = SocialInterests.AllHeard;
        Check(!SocialFriendsRules.MayHint(SocialStage.Stranger, 0, 0, 10) && SocialFriendsRules.MayHint(SocialStage.Acquaintance, 0, 0, 10)
              && SocialFriendsRules.MayHint(SocialStage.Acquaintance, 0b11, 0, 10) && !SocialFriendsRules.MayHint(SocialStage.Acquaintance, 0b111, 0, 10)
              && SocialFriendsRules.MayHint(SocialStage.Friend, 0b11111, 0, 10) && !SocialFriendsRules.MayHint(SocialStage.Friend, 0b111111, 0, 10)
              && SocialFriendsRules.MayHint(SocialStage.CloseFriend, 0b11111111, 0, 10) && SocialFriendsRules.MayHint(SocialStage.Partner, 0b11111111, 0, 10)
              && !SocialFriendsRules.MayHint(SocialStage.Partner, all, 0, 10),
            "friends: nobody tells a stranger anything, an acquaintance lets slip three things at most, a friend six, anybody closer all nine, and nobody has anything left once all nine are heard");
        Check(!SocialFriendsRules.MayHint(SocialStage.Friend, 0, 10, 10) && SocialFriendsRules.MayHint(SocialStage.Friend, 0, 9, 10),
            "friends: a person lets one thing slip a day at most");
        Check(SocialFriendsRules.HintCap(SocialStage.Stranger) == 0 && SocialFriendsRules.HintCap(SocialStage.Acquaintance) == SocialFriendsRules.HintCapAcquaintance
              && SocialFriendsRules.HintCap(SocialStage.Friend) == SocialFriendsRules.HintCapFriend && SocialFriendsRules.HintCap(SocialStage.Dating) == SocialInterests.Kinds
              && SocialFriendsRules.Chance(HintWhere.Outing) > SocialFriendsRules.Chance(HintWhere.Call) && SocialFriendsRules.Chance(HintWhere.Call) > SocialFriendsRules.Chance(HintWhere.Text)
              && SocialFriendsRules.Chance(HintWhere.Text) > 0f && SocialFriendsRules.Chance(HintWhere.Outing) < 1f,
            "friends: the caps and chances of a hint: more said at a closer stage, most often in an outing's chat, then a call, then a text of its own, never certain");

        // Which kind: never one already heard, the same for the same day, every kind in turn until none is left.
        bool never = true, steady = true, walk = true, empty = true;
        for (int seed = 1; seed <= 300; seed++)
        {
            int heard = 0;
            var order = new HashSet<int>();
            for (int step = 0; step < SocialInterests.Kinds; step++)
            {
                int day = 20 + step;
                int kind = SocialFriendsRules.NextKind(seed, heard, day);
                never &= kind >= 0 && kind < SocialInterests.Kinds && !SocialInterests.Has(heard, kind);
                steady &= kind == SocialFriendsRules.NextKind(seed, heard, day);
                order.Add(kind);
                heard |= 1 << kind;
            }
            walk &= order.Count == SocialInterests.Kinds && heard == all;
            empty &= SocialFriendsRules.NextKind(seed, all, 50) == -1;
        }
        Check(never && steady && walk && empty, "friends: a hint is about a kind not heard yet, the same on every machine for the same person and day, goes through all nine and is none once all are heard (300 seeds)");

        // Another day, another kind: not always the lowest.
        var firsts = new HashSet<int>();
        for (int day = 1; day <= 60; day++)
        {
            firsts.Add(SocialFriendsRules.NextKind(12345, 0, day));
        }
        Check(firsts.Count >= 6, "friends: which kind comes first depends on the day, so two people do not tell their things in the same order");

        // The lines: a few ways to bring one in, one of each pool by a hash that is steady and varies.
        bool pools = SocialInterests.Hints.Length == SocialInterests.Kinds && SocialInterests.Leads.Length >= 4;
        for (int k = 0; k < SocialInterests.Kinds; k++)
        {
            pools &= SocialInterests.Hints[k].Length >= 4 && SocialInterests.Questions[k].Length == 2;
        }
        var picks = new HashSet<int>();
        bool index = true;
        for (int day = 1; day <= 200; day++)
        {
            int i = SocialFriendsRules.Index(5, day, 321, 11);
            index &= i >= 0 && i < 5 && i == SocialFriendsRules.Index(5, day, 321, 11);
            picks.Add(i);
        }
        Check(pools && index && picks.Count == 5 && SocialFriendsRules.Index(1, 5, 5, 5) == 0 && SocialFriendsRules.Index(0, 5, 5, 5) == 0,
            "friends: every kind has at least four ways of letting it slip and two of being asked about, and a way is chosen by a steady hash of the day and the person that varies from day to day");
    }

    // ---------------------------------------------------------------- the quiz

    private static void F29Quiz()
    {
        const int questions = SocialFriendsRules.Questions;
        bool shape = true, truth = true, positions = true, known = true, deterministic = true, distinctKinds = true, distinctChoices = true;
        var at = new HashSet<int>();
        var mine = new int[SocialInterests.Kinds];
        for (int seed = 1; seed <= 1500; seed++)
        {
            SocialInterests.Roll(seed, mine);
            int heard = (seed * 37) & SocialInterests.AllHeard;
            SocialQuiz quiz = SocialFriendsRules.Make(seed, heard, seed % 4);
            SocialQuiz again = SocialFriendsRules.Make(seed, heard, seed % 4);
            var kinds = new HashSet<int>();
            for (int q = 0; q < questions; q++)
            {
                int kind = quiz.Kind[q];
                shape &= kind >= 0 && kind < SocialInterests.Kinds && quiz.Variant[q] >= 0 && quiz.Variant[q] < 2 && quiz.RightAt[q] >= 0 && quiz.RightAt[q] < 3;
                distinctKinds &= kinds.Add(kind);
                var choices = new HashSet<int>(quiz.Choice[q]);
                distinctChoices &= choices.Count == 3;
                foreach (int c in quiz.Choice[q])
                {
                    shape &= c >= 0 && c < SocialInterests.PerKind;
                }
                truth &= quiz.Choice[q][quiz.RightAt[q]] == mine[kind];
                int wrongOnes = 0;
                for (int slot = 0; slot < 3; slot++)
                {
                    wrongOnes += slot != quiz.RightAt[q] && quiz.Choice[q][slot] != mine[kind] ? 1 : 0;
                }
                truth &= wrongOnes == 2;
                known &= quiz.Known[q] == SocialInterests.Has(heard, kind);
                at.Add(quiz.RightAt[q]);
                deterministic &= again.Kind[q] == kind && again.Variant[q] == quiz.Variant[q] && again.RightAt[q] == quiz.RightAt[q] && again.Known[q] == quiz.Known[q]
                                 && again.Choice[q][0] == quiz.Choice[q][0] && again.Choice[q][1] == quiz.Choice[q][1] && again.Choice[q][2] == quiz.Choice[q][2];
            }
            positions &= quiz.Asked == 0 && quiz.Right == 0 && !quiz.Over;
        }
        Check(shape && positions && distinctKinds && distinctChoices, "friends: a quiz is three questions about three different kinds, each with three different answers of that kind (1500 people)");
        Check(truth, "friends: one answer of each question is true (what the person is into) and the other two are not");
        Check(at.Count == 3, "friends: the true answer is not always in the same place");
        Check(known, "friends: a thing the player heard the person let slip is shown as known in the question that asks for it, and nothing else is");
        Check(deterministic, "friends: the same person, the same things heard and the same number of earlier quizzes make the same quiz on every machine");

        // A hint heard leads to a sure answer: hear a kind, and every quiz that asks about it marks it known, with the true answer among the choices.
        bool sure = true, later = true;
        for (int seed = 1; seed <= 200; seed++)
        {
            SocialInterests.Roll(seed, mine);
            int kind = SocialFriendsRules.NextKind(seed, 0, 40);
            int heard = 1 << kind;
            for (int times = 0; times < 12; times++)
            {
                SocialQuiz heardQuiz = SocialFriendsRules.Make(seed, heard, times), unheardQuiz = SocialFriendsRules.Make(seed, 0, times);
                for (int q = 0; q < questions; q++)
                {
                    bool asks = heardQuiz.Kind[q] == kind;
                    sure &= heardQuiz.Known[q] == asks && !unheardQuiz.Known[q] && (!asks || heardQuiz.Choice[q][heardQuiz.RightAt[q]] == mine[kind]);
                    later &= heardQuiz.Kind[q] == unheardQuiz.Kind[q] && heardQuiz.RightAt[q] == unheardQuiz.RightAt[q];   // hearing only marks: it never changes the quiz
                }
            }
        }
        Check(sure && later, "friends: after a hint is heard the question about that kind is a sure answer, shown as known, and hearing it changes nothing else of the quiz (200 people, 12 quizzes each)");

        var variety = new HashSet<string>();
        for (int times = 0; times < 20; times++)
        {
            SocialQuiz q = SocialFriendsRules.Make(99, 0, times);
            variety.Add(q.Kind[0] + "," + q.Kind[1] + "," + q.Kind[2]);
        }
        Check(variety.Count >= 8, "friends: the next quiz about the same person is another one (the kinds asked differ from one to the next)");
        SocialQuiz fresh = SocialFriendsRules.Make(5, 0, -3);
        Check(fresh.Kind[0] >= 0 && SocialFriendsRules.Make(5, 0, 0).Kind[1] == fresh.Kind[1], "friends: a number of earlier quizzes below nothing is read as none");
    }

    // ---------------------------------------------------------------- what an answer is worth

    private static SocialWorth F29Total(SocialStage stage, int right)
    {
        var total = new SocialWorth();
        for (int q = 0; q < SocialFriendsRules.Questions; q++)
        {
            SocialWorth w = SocialFriendsRules.Answered(stage, q < right);
            total.Familiarity += w.Familiarity;
            total.Affection += w.Affection;
            total.Trust += w.Trust;
        }
        SocialWorth done = SocialFriendsRules.Finished(stage, right);
        total.Familiarity += done.Familiarity;
        total.Affection += done.Affection;
        total.Trust += done.Trust;
        return total;
    }

    private static bool F29Near(float a, float b) => Math.Abs(a - b) < 0.001f;

    private static void F29Scoring()
    {
        SocialWorth right = SocialFriendsRules.Answered(SocialStage.Friend, true), wrong = SocialFriendsRules.Answered(SocialStage.Friend, false);
        Check(F29Near(right.Trust, 4f) && F29Near(right.Affection, 3f) && F29Near(right.Familiarity, 1.5f)
              && F29Near(wrong.Trust, -3f) && F29Near(wrong.Affection, -2f) && F29Near(wrong.Familiarity, 0.5f),
            "friends: at a friend a right answer gives trust 4, affection 3 and familiarity 1.5, and a wrong one takes trust 3 and affection 2");
        Check(right.Trust > 0f && right.Affection > 0f && right.Familiarity > 0f && wrong.Trust < 0f && wrong.Affection < 0f && right.Trust > -wrong.Trust,
            "friends: a right answer raises all three figures, a wrong one lowers trust and affection, and a right one is worth more than a wrong one costs");

        // The whole of it by answers right, at a friend: 0 is a lovely disaster, 3 a feat.
        SocialWorth t0 = F29Total(SocialStage.Friend, 0), t1 = F29Total(SocialStage.Friend, 1), t2 = F29Total(SocialStage.Friend, 2), t3 = F29Total(SocialStage.Friend, 3);
        Check(F29Near(t3.Trust, 16f) && F29Near(t3.Affection, 13f) && F29Near(t3.Familiarity, 6.5f) && F29Near(t2.Trust, 5f) && F29Near(t2.Affection, 4f) && F29Near(t1.Trust, -2f)
              && F29Near(t1.Affection, -1f) && F29Near(t0.Trust, -9f) && F29Near(t0.Affection, -2f) && F29Near(t0.Familiarity, 3.5f),
            "friends: three right at a friend is trust +16 and affection +13, two +5 and +4, one -2 and -1, none -9 and -2 (the disaster's laughter gives back some of the affection)");
        Check(t3.Trust > t2.Trust && t2.Trust > t1.Trust && t1.Trust > t0.Trust && t3.Affection > t2.Affection && t2.Affection > t1.Affection && t1.Affection > t0.Affection,
            "friends: each answer right is worth more than the one before in trust and affection, so a right answer is always worth giving");
        SocialWorth none = SocialFriendsRules.Finished(SocialStage.Friend, 1), two = SocialFriendsRules.Finished(SocialStage.Friend, 2);
        Check(none.Familiarity == 0f && none.Trust == 0f && two.Affection == 0f && SocialFriendsRules.Finished(SocialStage.Friend, 3).Trust > 0f
              && SocialFriendsRules.Finished(SocialStage.Friend, 0).Affection > 0f && SocialFriendsRules.Finished(SocialStage.Friend, 0).Trust == 0f,
            "friends: only three right earns the bonus, and only three wrong the laughter's affection (never trust): one or two right add nothing to their answers");

        // The stake: the closer, the more is at stake, either way.
        Check(F29Near(SocialFriendsRules.Stake(SocialStage.Friend), 1f) && F29Near(SocialFriendsRules.Stake(SocialStage.Acquaintance), 1f) && F29Near(SocialFriendsRules.Stake(SocialStage.CloseFriend), 1.25f)
              && F29Near(SocialFriendsRules.Stake(SocialStage.Dating), 1.5f) && F29Near(SocialFriendsRules.Stake(SocialStage.Partner), 1.5f)
              && F29Near(SocialFriendsRules.Answered(SocialStage.Partner, true).Trust, 6f) && F29Near(SocialFriendsRules.Answered(SocialStage.Partner, false).Trust, -4.5f),
            "friends: a close friend's answers are worth a quarter more and a partner's half more, right or wrong");

        QuizVerdict v3 = new SocialQuiz { Right = 3 }.Verdict, v0 = new SocialQuiz { Right = 0 }.Verdict, v9 = new SocialQuiz { Right = 9 }.Verdict, vn = new SocialQuiz { Right = -2 }.Verdict;
        Check(v3 == QuizVerdict.Perfect && v0 == QuizVerdict.Disaster && new SocialQuiz { Right = 1 }.Verdict == QuizVerdict.One && new SocialQuiz { Right = 2 }.Verdict == QuizVerdict.Two
              && v9 == QuizVerdict.Perfect && vn == QuizVerdict.Disaster && new SocialQuiz { Asked = 3 }.Over && !new SocialQuiz { Asked = 2 }.Over,
            "friends: three right is a perfect score, none a disaster, and a verdict always names one of the four");

        // What the friends give a new person who is met, by the answers: more for more right, a lovely disaster warms and cools.
        SocialWorth s3 = SocialFriendsRules.Standing(3), s2 = SocialFriendsRules.Standing(2), s1 = SocialFriendsRules.Standing(1), s0 = SocialFriendsRules.Standing(0);
        Check(F29Near(s3.Trust, 8f) && F29Near(s3.Affection, 10f) && F29Near(s3.Familiarity, 10f) && F29Near(s2.Trust, 4f) && F29Near(s2.Affection, 5f) && s1.Trust == 0f && s1.Affection == 0f
              && F29Near(s0.Affection, 4f) && F29Near(s0.Trust, -4f),
            "friends: three right give the new person a first standing of familiarity +10, affection +10 and trust +8, two half that, one nothing, none affection +4 and trust -4");
        float f, a, t;
        SocialImpression.Start(true, SocialOrigin.Street, 0, 3, out f, out a, out t);
        bool start = F29Near(f, 22f) && F29Near(a, 24f) && F29Near(t, 20f);
        SocialImpression.Start(false, SocialOrigin.Street, 0, 0, out f, out a, out t);
        start &= F29Near(f, 8f) && F29Near(a, 12f) && F29Near(t, 5f);
        SocialImpression.Start(true, SocialOrigin.Helped, 0, 3, out f, out a, out t);
        start &= F29Near(t, 34f) && F29Near(a, 32f);
        Check(start, "friends: the first standing is added to the figures the new person starts with (a warm hello and three right: 22, 24 and 20)");
        string rules = ModSource(@"Social\SocialFriendsRules.cs");
        Check(rules.Contains("Answered(SocialStage stage, bool right)") && SocialFriendsRules.IntroWait(0, 100) == 0 && SocialFriendsRules.IntroWait(100, 100) == SocialFriendsRules.IntroGapDays
              && SocialFriendsRules.IntroWait(100, 104) == SocialFriendsRules.IntroGapDays - 4 && SocialFriendsRules.IntroWait(100, 110) == 0 && SocialFriendsRules.IntroWait(100, 200) == 0
              && SocialFriendsRules.IntroWait(150, 100) == SocialFriendsRules.IntroGapDays,
            "friends: the same person brings friends again only after " + SocialFriendsRules.IntroGapDays + " days, never asking longer for a save set back or for one who never did");

        // Applied to a person: the figures move as the rules say, clamped, and family never falls below its floor.
        var friend = new SocialPerson { Origin = SocialOrigin.Street, Stage = SocialStage.Friend, Familiarity = 40, Affection = 40, Trust = 40, Met = true };
        foreach (bool ok in new[] { true, true, true })
        {
            SocialWorth w = SocialFriendsRules.Answered(friend.Stage, ok);
            SocialRules.Change(friend, w.Familiarity, w.Affection, w.Trust);
        }
        SocialWorth bonus = SocialFriendsRules.Finished(friend.Stage, 3);
        SocialRules.Change(friend, bonus.Familiarity, bonus.Affection, bonus.Trust);
        var wreck = new SocialPerson { Origin = SocialOrigin.Street, Stage = SocialStage.Friend, Familiarity = 3, Affection = 2, Trust = 1, Met = true };
        for (int i = 0; i < 3; i++)
        {
            SocialWorth w = SocialFriendsRules.Answered(wreck.Stage, false);
            SocialRules.Change(wreck, w.Familiarity, w.Affection, w.Trust);
        }
        var mom = new SocialPerson { Origin = SocialOrigin.Family, Stage = SocialStage.Friend, Familiarity = 56, Affection = 46, Trust = 41, Met = true };
        for (int i = 0; i < 3; i++)
        {
            SocialWorth w = SocialFriendsRules.Answered(mom.Stage, false);
            SocialRules.Change(mom, w.Familiarity, w.Affection, w.Trust);
        }
        Check(F29Near(friend.Trust, 56f) && F29Near(friend.Affection, 53f) && F29Near(friend.Familiarity, 46.5f) && wreck.Trust == 0f && wreck.Affection == 0f && mom.Trust == 40f && mom.Affection >= 45f,
            "friends: three right answers move a friend of 40 to trust 56, affection 53 and familiarity 46.5; wrong ones never take a figure under nothing, and Mom's never under what family keeps (" + friend.Familiarity + "/" + friend.Affection + "/" + friend.Trust + ", " + wreck.Affection + "/" + wreck.Trust + ", " + mom.Affection + "/" + mom.Trust + ")");
    }

    // ---------------------------------------------------------------- first impressions

    private static void F29Impression()
    {
        SocialRepute newcomer = SocialRepute.Newcomer;
        Check(SocialImpression.Of(in newcomer) == 0 && newcomer.Public == 55 && newcomer.Integrity == 50 && !newcomer.HasRecord && SocialImpression.Most == 10,
            "friends: a newcomer nobody has heard of (public standing 55, integrity 50, no record) starts with no change to their first trust, and at most 10 either way");

        // Every factor in its own direction, the others as a newcomer's.
        int Of(SocialRepute r) => SocialImpression.Of(in r);
        SocialRepute Base() => SocialRepute.Newcomer;
        SocialRepute good = Base(); good.Public = 100;
        SocialRepute fair = Base(); fair.Public = 70;
        SocialRepute poor = Base(); poor.Public = 20;
        SocialRepute worst = Base(); worst.Public = 0;
        Check(Of(good) == 5 && Of(fair) == 1 && Of(poor) == -3 && Of(worst) == -5 && Of(Base()) == 0,
            "friends: a good public standing raises a first trust (70 gives +1, 100 gives +5) and a bad one lowers it (20 gives -3, 0 gives -5)");
        bool monotonic = true;
        for (int p = 1; p <= 100; p++)
        {
            SocialRepute lower = Base(), higher = Base();
            lower.Public = p - 1;
            higher.Public = p;
            monotonic &= Of(higher) >= Of(lower);
        }
        Check(monotonic, "friends: a better public standing never gives a lower first trust (every step from 0 to 100)");

        SocialRepute known = Base(); known.Recognition = 40;
        SocialRepute prominent = Base(); prominent.Recognition = 60;
        SocialRepute famous = Base(); famous.Recognition = 90;
        SocialRepute unknown = Base(); unknown.Recognition = 20;
        SocialRepute infamous = Base(); infamous.Public = 20; infamous.Recognition = 90;
        SocialRepute mid = Base(); mid.Public = 45; mid.Recognition = 90;
        Check(Of(unknown) == 0 && Of(known) == 1 && Of(prominent) == 2 && Of(famous) == 3 && Of(infamous) == Of(poor) - 3 && Of(mid) == 0,
            "friends: renown raises a first trust for somebody the city thinks well of (+1, +2, +3 by how well known) and lowers it for somebody it thinks badly of; fame between the two changes nothing");
        SocialRepute many = Base(); many.Followers = 2500;
        SocialRepute some = Base(); some.Followers = 250;
        SocialRepute few = Base(); few.Followers = 50;
        SocialRepute notoriousFollowed = Base(); notoriousFollowed.Public = 30; notoriousFollowed.Followers = 5000;
        SocialRepute notorious = Base(); notorious.Public = 30;
        Check(Of(many) == 2 && Of(some) == 1 && Of(few) == 0 && Of(notoriousFollowed) == Of(notorious),
            "friends: Flare followers raise a first trust (+1 from 200, +2 from 2000) unless the city thinks badly of the player, when they change nothing");
        SocialRepute charity = Base(); charity.Integrity = 65;
        SocialRepute saint = Base(); saint.Integrity = 90;
        SocialRepute shady = Base(); shady.Integrity = 35;
        SocialRepute crooked = Base(); crooked.Integrity = 10;
        Check(Of(charity) == 1 && Of(saint) == 2 && Of(shady) == -1 && Of(crooked) == -2 && Of(Base()) == 0,
            "friends: a good name for integrity (charity, good deeds) raises a first trust (+1, +2) and a bad one (bribes) lowers it (-1, -2)");
        SocialRepute record = Base(); record.HasRecord = true;
        SocialRepute charged = Base(); charged.HasRecord = true; charged.Charged = true;
        SocialRepute scandal = Base(); scandal.Scandal = true;
        Check(Of(record) == -3 && Of(charged) == -4 && Of(scandal) == -3,
            "friends: a record takes 3 from a first trust, an open case one more, and a scandal 3");

        // The bounds, over every combination: never past 10 either way, and both bounds are reached.
        int most = int.MinValue, least = int.MaxValue;
        bool bound = true, alone = true;
        foreach (int pub in new[] { 0, 10, 25, 39, 40, 54, 55, 56, 64, 80, 100 })
        {
            foreach (int rec in new[] { 0, 20, 35, 55, 75, 100 })
            {
                foreach (int fol in new[] { 0, 199, 200, 2000, 100000 })
                {
                    foreach (int integrity in new[] { 0, 25, 26, 40, 41, 50, 59, 60, 75, 100 })
                    {
                        for (int flags = 0; flags < 8; flags++)
                        {
                            var r = new SocialRepute
                            {
                                Public = pub, Recognition = rec, Followers = fol, Integrity = integrity, HasRecord = (flags & 1) != 0, Charged = (flags & 2) != 0, Scandal = (flags & 4) != 0
                            };
                            int v = Of(r);
                            bound &= v >= -SocialImpression.Most && v <= SocialImpression.Most;
                            most = Math.Max(most, v);
                            least = Math.Min(least, v);
                            alone &= v == Of(r);
                        }
                    }
                }
            }
        }
        Check(bound && most == SocialImpression.Most && least == -SocialImpression.Most && alone,
            "friends: a first impression is never more than +10 or less than -10 whatever the player's name, and the most and least are reached (" + least + " to " + most + ")");
        SocialRepute extreme = new SocialRepute { Public = 0, Recognition = 100, Followers = 100000, Integrity = 0, HasRecord = true, Charged = true, Scandal = true };
        SocialRepute radiant = new SocialRepute { Public = 100, Recognition = 100, Followers = 100000, Integrity = 100 };
        Check(Of(extreme) == -10 && Of(radiant) == 10, "friends: the worst name there is gives -10 and the best +10, not more");

        // How it reaches the figures: through the one rule every way of meeting uses.
        float f, a, t;
        SocialImpression.Start(true, SocialOrigin.Gym, 6, -1, out f, out a, out t);
        bool figures = F29Near(t, 18f) && F29Near(f, 12f) && F29Near(a, 14f);
        SocialImpression.Start(false, SocialOrigin.Gym, -5, -1, out f, out a, out t);
        figures &= F29Near(t, 4f) && F29Near(f, 8f) && F29Near(a, 8f);
        SocialImpression.Start(false, SocialOrigin.Gym, -10, -1, out f, out a, out t);
        figures &= t == 0f;
        SocialImpression.Start(true, SocialOrigin.Helped, 10, 3, out f, out a, out t);
        figures &= t <= 100f && a <= 100f && f <= 100f && F29Near(t, 44f);
        Check(figures, "friends: the impression is added to the first trust alone (a warm start of 12 with +6 is 18, a cool one of 9 with -5 is 4) and never takes it under nothing");
    }

    // ---------------------------------------------------------------- the save

    private static void F29Save()
    {
        const string key = "reallife:social_friends";
        // A save made before it: no record, and nothing is written for somebody who never heard anything.
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 10, F29Year, F29Districts, 4321);
        bool oldSave = !data.ContainsKey(key);
        SocialState.Save(data);
        oldSave &= !data.ContainsKey(key);
        for (int i = 0; i < SocialState.Count; i++)
        {
            SocialPerson p = SocialState.Get(i);
            oldSave &= p.Heard == 0 && p.InterestHintDay == 0 && p.IntroDay == 0 && p.Quizzes == 0 && p.QuizBest == 0 && p.InterestSeed == 0;
        }
        Check(oldSave && SocialFriendsStore.Told == 0, "friends: a save made before it loads as it always did, nobody has heard anything, and saving it writes no new record (a player who never meets the friends adds nothing to the save)");

        // The things of a person in a save that never knew them: rolled from their own seed, the same on every load.
        var rolled = new int[SocialInterests.Kinds];
        var again = new int[SocialInterests.Kinds];
        SocialPerson mom = SocialState.Get(0);
        SocialInterests.Roll(SocialInterests.SeedOf(mom), rolled);
        SocialState.Load(data, 10, F29Year, F29Districts, 4321);
        SocialInterests.Roll(SocialInterests.SeedOf(SocialState.Get(0)), again);
        bool same = true;
        for (int k = 0; k < rolled.Length; k++)
        {
            same &= rolled[k] == again[k];
        }
        Check(same && SocialInterests.SeedOf(SocialState.Get(0)) == SocialState.Get(0).Seed, "friends: a save that never knew them gets its people's things from their own seeds on every load, the same ones each time");

        // Hearing, then a round trip.
        SocialPerson a = SocialState.Get(0), b = SocialState.Get(3), c = SocialState.Get(5);
        a.Heard = 0b101010101; a.InterestHintDay = 12; a.InterestSeed = a.Seed;
        b.IntroDay = 9; b.Quizzes = 2; b.QuizBest = 3; b.Heard = 1; b.InterestSeed = 777;
        c.Quizzes = 1; c.QuizBest = 0;
        SocialFriendsStore.Told = SocialFriendsStore.ToldHint;
        SocialState.Save(data);
        bool saved = data.TryGetValue(key, out string record) && record.StartsWith("1|1\n", StringComparison.Ordinal) && record.Split('\n').Length == 4;
        string aName = a.Name, bName = b.Name;
        SocialState.Load(data, 10, F29Year, F29Districts, 4321);
        SocialPerson a2 = SocialState.Get(0), b2 = SocialState.Get(3), c2 = SocialState.Get(5), d2 = SocialState.Get(1);
        bool round = a2.Name == aName && a2.Heard == 0b101010101 && a2.InterestHintDay == 12 && a2.IntroDay == 0 && b2.Name == bName && b2.IntroDay == 9 && b2.Quizzes == 2 && b2.QuizBest == 3
                     && b2.Heard == 1 && b2.InterestSeed == 777 && c2.Quizzes == 1 && c2.QuizBest == 0 && d2.Heard == 0 && SocialFriendsStore.Told == SocialFriendsStore.ToldHint;
        Check(saved && round, "friends: what each person has let slip, when, the friends brought and the quizzes sat are saved in a record of their own and read back, with the first notice's mark");
        SocialState.Save(data);
        Check(data[key] == record, "friends: a record read and written again is the same record");

        // Stale or damaged records are ignored, never applied to somebody else.
        var other = new Dictionary<string, string>(data);
        other[key] = record.Replace("|" + bName + "|", "|Somebody Else|");
        SocialState.Load(other, 10, F29Year, F29Districts, 4321);
        bool stale = SocialState.Get(3).Heard == 0 && SocialState.Get(0).Heard == 0b101010101;   // (the one whose name no longer matches takes nothing)
        other[key] = "1|0\n0|" + aName + "|5|1023|-4|-4|-9|-9";
        SocialState.Load(other, 10, F29Year, F29Districts, 4321);
        SocialPerson clamped = SocialState.Get(0);
        stale &= clamped.Heard == SocialInterests.AllHeard && clamped.InterestHintDay == 0 && clamped.IntroDay == 0 && clamped.Quizzes == 0 && clamped.QuizBest == 0;
        other[key] = "1|0\n0|" + aName + "|5|1|3|3|1|99";
        SocialState.Load(other, 10, F29Year, F29Districts, 4321);
        stale &= SocialState.Get(0).QuizBest == SocialFriendsRules.Questions && SocialState.Get(0).Quizzes == 1;
        other[key] = "1|0\nnot a record\n0|" + aName + "|x";
        SocialState.Load(other, 10, F29Year, F29Districts, 4321);
        stale &= SocialState.Get(0).Heard == 0 && SocialState.Loaded;
        Check(stale, "friends: a record for somebody no longer in that slot, a damaged line or out of range numbers never give anybody else's hints or anything past the rules' own limits");

        // A later version's record is written back as it was read.
        var later = new Dictionary<string, string>(data);
        later[key] = "7|1\nwhatever a later version keeps";
        SocialState.Load(later, 10, F29Year, F29Districts, 4321);
        SocialState.Get(0).Heard = 3;
        SocialState.Save(later);
        Check(later[key] == "7|1\nwhatever a later version keeps" && SocialState.Get(0).Heard == 3, "friends: a record from a later version this one cannot read is kept as it was read");

        // A cast made new for a save with nothing: the store is reset too.
        SocialState.Load(new Dictionary<string, string>(), 10, F29Year, F29Districts, 55);
        Check(SocialFriendsStore.Told == 0 && SocialState.Get(0).Heard == 0, "friends: a new cast starts with nothing heard and no notice shown");

        // Determinism across machines: two loads of one save agree on everything, and the things themselves are never in it.
        SocialState.Load(data, 10, F29Year, F29Districts, 4321);
        string first = string.Join(",", SocialState.Get(0).Heard, SocialState.Get(3).Quizzes, SocialState.Get(3).InterestSeed);
        SocialState.Load(data, 10, F29Year, F29Districts, 4321);
        string second = string.Join(",", SocialState.Get(0).Heard, SocialState.Get(3).Quizzes, SocialState.Get(3).InterestSeed);
        Check(first == second && !record.Contains("hobby") && record.Length < 200, "friends: two loads of one save agree, and the record holds only what was heard, never the things themselves");

        // The save's other lines are as they were: nothing of the old records moved.
        string main = data.ContainsKey("reallife:social") ? data["reallife:social"] : string.Empty;
        var plain = new Dictionary<string, string>();
        SocialState.Load(plain, 10, F29Year, F29Districts, 4321);
        SocialState.Save(plain);
        var heardOne = new Dictionary<string, string>();
        SocialState.Load(heardOne, 10, F29Year, F29Districts, 4321);
        SocialState.Get(0).Heard = 1;
        SocialState.Save(heardOne);
        bool samePeople = plain["reallife:social_people"] == heardOne["reallife:social_people"] && plain["reallife:social"] == heardOne["reallife:social"];
        Check(samePeople && main.Length > 0, "friends: the people's own record and the social record are written exactly as before, whatever has been heard (it lives in its own key)");
        SocialState.Reset();
    }

    // ---------------------------------------------------------------- the option off

    private static void F29Off()
    {
        // With 'Meet the friends quiz' off the introduction plays as today, and with 'First impressions' off a meeting gives today's figures. The rules
        // are game-free; the switches are in the game side, so the sources are held to them.
        float f, a, t;
        bool today = true;
        foreach (SocialOrigin origin in Enum.GetValues(typeof(SocialOrigin)))
        {
            foreach (bool warm in new[] { true, false })
            {
                float tf = warm ? 12f : 8f, ta = warm ? 14f : 8f, tt = warm ? 12f : 9f;
                if (origin == SocialOrigin.Helped)
                {
                    tt += 14f;
                    ta += 8f;
                }
                SocialImpression.Start(warm, origin, 0, -1, out f, out a, out t);
                today &= f == tf && a == ta && t == tt;
            }
        }
        Check(today, "friends: with no impression and no quiz the figures a new person starts with are exactly the ones every version gave (warm 12, 14, 12; cool 8, 8, 9; the one who was helped +14 trust and +8 affection), for every way of meeting");

        string life = ModSource(@"Social\SocialLife.cs"), hint = ModSource(@"Social\SocialHintGame.cs"), person = ModSource(@"Social\PersonDialog.cs"), meet = ModSource(@"Social\MeetScene.cs");
        string contact = ModSource(@"Social\SocialDirector.Contact.cs"), meetings = ModSource(@"Social\SocialDirector.Meetings.cs"), dialog = ModSource(@"Social\SceneDialog.cs"), people = ModSource(@"Social\PeopleDialog.cs");
        string impression = MemberText(life, "public static int FirstImpression()");
        Check(impression.Contains("if (!Settings.FirstImpressions || SocialLinks.Repute == null)") && impression.Contains("return 0;") && life.Contains("SocialImpression.Start(warm, person.Origin, impression, standing"),
            "friends: with 'First impressions follow your name' off, or nothing to ask, a first impression is 0 and the figures are the usual");
        Check(hint.Contains("public static bool On => Settings.Social && Settings.FriendsQuiz;") && MemberText(hint, "public static bool Pick(").Contains("if (!On ||")
              && MemberText(hint, "public static string KnownLines(").Contains("if (!On ||"),
            "friends: with 'Meet the friends quiz' off nobody lets anything slip and the People page has no section for it");
        Check(meet.Contains("talk.Quiz = SceneTalk.QuizFor(talk.Mode, _friend);") && dialog.Contains("mode == Introduction && Settings.FriendsQuiz && friend != null && friend.Met ?") && dialog.Contains("_talk.Quiz != null && friend != null ? Quizzed : (Func<DialogEntry>)MeetFirst")
              && meet.Contains("talk.QuizRight"), "friends: the quiz is made only with the option on and a friend there to be asked about; without it the introduction goes straight to the hello it always did");
        Check(person.Contains("Settings.FriendsQuiz ? SocialFriendsRules.IntroWait(_p.IntroDay, SocialLife.Day) : 0") && person.Contains(": \"reallife_social_introduce_yes\"")
              && contact.Contains("(!Settings.FriendsQuiz || SocialFriendsRules.IntroWait(person.IntroDay, day) == 0)") && meetings.Contains("if (Settings.FriendsQuiz)")
              && MemberText(meetings, "public bool QueueIntroduction(").Contains("friend.IntroDay = SocialLife.Day;"),
            "friends: with the quiz off there is no wait between introductions and they are asked for in the words they always had");
        Check(life.Contains("SocialLife.Meet") || life.Contains("public static void Meet(SocialPerson person, bool warm, SocialPerson by = null, int standing = -1)"), "friends: the meeting's rule takes the quiz's standing as an optional argument, so every caller that passes none is as before");

        // Every way of meeting is one rule: nothing but the first eight, a roommate, and SocialLife.Meet sets somebody as met.
        var setters = new List<string>();
        foreach (string file in new[]
                 {
                     @"Social\SocialCatalogue.cs", @"Social\SocialLife.cs", @"Social\SocialDirector.cs", @"Social\SocialDirector.Meetings.cs", @"Social\SocialDirector.Contact.cs", @"Social\SocialDirector.Lose.cs",
                     @"Social\SocialPhone.cs", @"Social\MeetScene.cs", @"Social\SceneDialog.cs", @"Social\PersonDialog.cs", @"Wiring\StreetLinks.cs", @"Wiring\FlareLinks.cs", @"Wiring\FlareInviteLinks.cs",
                     @"Wiring\RoommateLinks.cs", @"Street\StreetDirector.cs", @"Social\Flare\FlareDirector.Residents.cs", @"Social\Flare\FlareDirector.Invites.cs", @"Home\Roommates\RoommateRules.cs"
                 })
        {
            string code = ModSource(file);
            if (Regex.IsMatch(code, @"\.Met = true|Met = true;"))
            {
                setters.Add(file);
            }
        }
        setters.Sort(StringComparer.Ordinal);
        Check(string.Join(",", setters) == @"Home\Roommates\RoommateRules.cs,Social\SocialCatalogue.cs,Social\SocialLife.cs",
            "friends: nothing but the cast's first eight (Mom and Alex are met from the start), a roommate who moves in and SocialLife.Meet marks anybody as met, so the first impression is one rule for every way of meeting (" + string.Join(", ", setters) + ")");
        Check(people.Contains("SocialHintGame.KnownLines(p)") && !ModSource(@"Social\SceneQuiz.cs").Contains("UnityEngine.UI"), "friends: the People page's section and the quiz are the game's own dialogue pages (so both interfaces show them), nothing drawn of the mod's own");
    }

    // ---------------------------------------------------------------- the words

    private static void F29Texts(Dictionary<string, string> en)
    {
        if (en == null)
        {
            Check(false, "friends: en.json could not be read");
            return;
        }
        string Of(string key) => en.TryGetValue(key, out string text) ? text : string.Empty;
        var missing = new List<string>();
        var unplain = new List<string>();
        void Pool(string[] keys, int least, int most, params string[] holds)
        {
            Check(keys.Length >= least && keys.Length <= most, "friends: a pool of " + keys.Length + " lines for " + (keys.Length > 0 ? keys[0] : "nothing") + " has " + least + " to " + most + " variants");
            var seen = new HashSet<string>();
            foreach (string key in keys)
            {
                string text = Of(key);
                if (text.Length == 0)
                {
                    missing.Add(key);
                    continue;
                }
                if (!F29Plain(text) || text.Length > 400 || !seen.Add(text))
                {
                    unplain.Add(key);
                }
                foreach (string must in holds)
                {
                    if (!text.Contains(must))
                    {
                        unplain.Add(key + " lacks " + must);
                    }
                }
            }
        }
        for (int k = 0; k < SocialInterests.Kinds; k++)
        {
            Pool(SocialInterests.Hints[k], 4, 8, "{thing}");
            Pool(SocialInterests.Questions[k], 2, 2, "{friend}");
        }
        Pool(SocialInterests.Leads, 4, 8);
        Pool(SocialInterests.Open, 4, 8, "{first}", "{friend}");
        Pool(SocialInterests.Right, 4, 8);
        Pool(SocialInterests.RightKnown, 4, 8);
        Pool(SocialInterests.Wrong, 4, 8, "{answer}");
        Pool(SocialInterests.VerdictOne, 4, 8);
        Pool(SocialInterests.VerdictTwo, 4, 8);
        Pool(SocialInterests.Perfect, 4, 8);
        Pool(SocialInterests.Disaster, 4, 8);
        for (int s = 0; s < 4; s++)
        {
            Pool(SocialInterests.After[s], 4, 8, "{friend}");
        }
        Pool(SocialInterests.Skip, 4, 8);
        Pool(SocialInterests.Yes, 4, 8, "{other}");
        Pool(SocialInterests.NameGood, 4, 8, "{trust}");
        Pool(SocialInterests.NameBad, 4, 8, "{trust}");
        Check(missing.Count == 0, "friends: every line of every pool is in en.json (" + string.Join(", ", missing) + ")");
        Check(unplain.Count == 0, "friends: every line is plain (no dash, no mark of surprise, no semicolon), different from the others of its pool, short enough for the panel, and holds the values it is given (" + string.Join(", ", unplain) + ")");

        bool short240 = true;
        foreach (string key in SocialInterests.NameGood)
        {
            short240 &= Of(key).Length <= 240;
        }
        foreach (string key in SocialInterests.NameBad)
        {
            short240 &= Of(key).Length <= 240;
        }
        foreach (string key in new[] { "reallife_friends_hint_first", "reallife_friends_wait" })
        {
            short240 &= Of(key).Length > 0 && Of(key).Length <= 240;
        }
        Check(short240 && Of(SocialInterests.NameGood[1]).Contains("{first}"), "friends: the first impression's lines and the notices fit a notification (240 characters)");

        string[] singles =
        {
            "reallife_friends_btn_go", "reallife_friends_btn_skip", "reallife_friends_btn_on", "reallife_friends_q_page", "reallife_friends_q_known", "reallife_friends_know_head",
            "reallife_friends_know_line", "reallife_friends_know_none", "reallife_friends_card_title", "reallife_friends_card_score", "reallife_friends_card_standing", "reallife_friends_card_perfect",
            "reallife_friends_card_disaster", "reallife_social_memory_quiz_aced", "reallife_social_memory_quiz_flop", "reallife_friends_quiz", "reallife_opt_tip_friends_quiz",
            "reallife_romance_younger", "reallife_opt_tip_romance_younger", "reallife_romance_older", "reallife_opt_tip_romance_older", "reallife_social_first_impressions",
            "reallife_opt_tip_social_first_impressions"
        };
        var lacking = new List<string>();
        foreach (string key in singles)
        {
            if (Of(key).Length == 0 || !F29Plain(Of(key).Replace("\n", " ")))
            {
                lacking.Add(key);
            }
        }
        Check(lacking.Count == 0, "friends: the quiz's pages, the card, the memories and the four options have their texts, plain (" + string.Join(", ", lacking) + ")");

        // The options: labels short, tips in one sentence of at most 32 words and 190 characters, and no mod words in a label.
        bool options = true;
        foreach (string id in new[] { "friends_quiz", "romance_younger", "romance_older", "social_first_impressions" })
        {
            string label = Of(id == "friends_quiz" ? "reallife_friends_quiz" : id == "romance_younger" ? "reallife_romance_younger" : id == "romance_older" ? "reallife_romance_older" : "reallife_social_first_impressions");
            string tip = Of("reallife_opt_tip_" + id);
            options &= label.Length > 0 && label.Length <= 60 && label.Split(' ').Length <= 9 && !label.EndsWith(".") && tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32 && tip.EndsWith(".");
        }
        Check(options, "friends: each of the four options has a short label and a tip of one sentence, at most 32 words and 190 characters");
        Check(Of("reallife_romance_younger") == "Partners younger than you, at most" && Of("reallife_romance_older") == "Partners older than you, at most" && Of("reallife_friends_quiz") == "Meet the friends quiz",
            "friends: the options are called what the players asked for");

        // The notice of the quiz: each placeholder is one the code fills.
        Check(Of("reallife_friends_q_page").Contains("{n}") && Of("reallife_friends_q_page").Contains("{total}") && Of("reallife_friends_q_page").Contains("{question}")
              && Of("reallife_friends_q_known").Contains("{friend}") && Of("reallife_friends_q_known").Contains("{answer}") && Of("reallife_friends_know_head").Contains("{count}")
              && Of("reallife_friends_know_line").Contains("{label}") && Of("reallife_friends_know_line").Contains("{thing}") && Of("reallife_friends_card_score").Contains("{right}"),
            "friends: the quiz's page, the known line and the People section hold the values the code fills");

        Check(WhatsNewHas(en, "People now drop hints about what they love and hate, and when they introduce their friends, the friends quiz you on how well you listened.")
              && WhatsNewHas(en, "Somebody you meet now starts with a little more or less trust, by what they have heard of your name.")
              && WhatsNewHas(en, "Two sliders under Friends and social life set how much younger and older than you somebody you could fall for may be.")
              && !WhatsNewHas(en, "Fixed: People now drop hints"),
            "friends: What's new has one plain sentence for each of the three new features, with no fix mark (found in any row)");

        string guide = Of("reallife_guide_social");
        Check(guide.Contains("# Meeting their friends") && guide.Contains("Things you know about them") && guide.Contains("{social_quiz_gap}") && guide.Contains("{social_age_least}") && guide.Contains("{social_age_most}")
              && guide.Contains("{social_impression_most}") && guide.Contains("{social_quiz_right_trust}") && guide.Contains("{social_quiz_wrong_trust}") && guide.Contains("{social_quiz_perfect_trust}")
              && guide.Contains("'Meet the friends quiz'") && guide.Contains("'First impressions follow your name'") && guide.Contains("'Partners younger than you, at most'") && guide.Contains("'Partners older than you, at most'"),
            "friends: the Guide's social page explains the hints, the quiz, the first impression and the age window, with the figures filled from the code");
        string features = ModSource(@"Meta\FeatureList.Life.cs");
        bool facts = true;
        foreach (string fact in new[] { "social_age_least", "social_age_most", "social_quiz_gap", "social_quiz_right_trust", "social_quiz_wrong_trust", "social_quiz_perfect_trust", "social_impression_most" })
        {
            facts &= features.Contains("Fact(\"" + fact + "\", () =>");
        }
        Check(facts && features.Contains("Fact(\"social_quiz_right_trust\", () => Guide.Number(SocialFriendsRules.RightTrust))") && features.Contains("Fact(\"social_impression_most\", () => SocialImpression.Most."),
            "friends: every figure the Guide's page quotes is filled from the rules' own numbers");
    }

    // ---------------------------------------------------------------- the options and the places

    private static void F29Sources()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), features = ModSource(@"Meta\FeatureList.Life.cs");
        Check(settings.Contains("public const string FriendsQuizId = \"friends_quiz\";") && settings.Contains("public static bool FriendsQuiz = true;") && settings.Contains("FriendsQuiz = Bool(modId, FriendsQuizId, FriendsQuiz);")
              && settings.Contains("public const string FirstImpressionsId = \"social_first_impressions\";") && settings.Contains("public static bool FirstImpressions = true;")
              && settings.Contains("FirstImpressions = Bool(modId, FirstImpressionsId, FirstImpressions);"),
            "friends: 'Meet the friends quiz' and 'First impressions follow your name' are saved options, on by default");
        Check(settings.Contains("public static int RomanceYounger = RomanceRules.AgeGap;") && settings.Contains("public static int RomanceOlder = RomanceRules.AgeGap;")
              && settings.Contains("RomanceYounger = Int(modId, RomanceYoungerId, RomanceYounger);") && settings.Contains("RomanceOlder = Int(modId, RomanceOlderId, RomanceOlder);")
              && settings.Contains("RomanceRules.Window(RomanceYounger, RomanceOlder);") && RomanceRules.AgeGap == 8 && RomanceRules.WindowLeast == 2 && RomanceRules.WindowMost == 25,
            "friends: the two age options are saved, 8 years each by default (today's gap) and 2 to 25, and loading hands them to the rules");
        Check(mod.Contains("AddSlider(Settings.RomanceYoungerId, \"reallife_romance_younger\", RomanceRules.WindowLeast, RomanceRules.WindowMost, Settings.RomanceYounger,")
              && mod.Contains("AddSlider(Settings.RomanceOlderId, \"reallife_romance_older\", RomanceRules.WindowLeast, RomanceRules.WindowMost, Settings.RomanceOlder,")
              && mod.Contains("AddToggle(Settings.FriendsQuizId, \"reallife_friends_quiz\", Settings.FriendsQuiz,") && mod.Contains("AddToggle(Settings.FirstImpressionsId, \"reallife_social_first_impressions\", Settings.FirstImpressions,"),
            "friends: the four options are rows of the options page, the sliders from 2 to 25 years");
        // The social section holds ten rows at most, so the quiz and first impressions stand in their own section after it.
        int social = tree.IndexOf("S(\"social\")", StringComparison.Ordinal), texts = tree.IndexOf("S(\"socialtexts\")", StringComparison.Ordinal);
        int knowing = tree.IndexOf("S(\"knowing\")", StringComparison.Ordinal), love = tree.IndexOf("S(\"love\")", StringComparison.Ordinal);
        string section = social >= 0 && texts > social ? tree.Substring(social, texts - social) : string.Empty;
        string know = knowing >= 0 && love > knowing ? tree.Substring(knowing, love - knowing) : string.Empty;
        Check(section.Contains("FreeOpts(\"romance_younger\", \"romance_older\")") && section.Contains("FreeOpts(\"family_interest\")")
              && know.Contains(".Guide(\"social\").Need(\"social\")") && know.Contains("Opts(\"friends_quiz\", \"social_first_impressions\")"),
            "friends: the age sliders stand under Friends and social life beside 'Who you are interested in', the quiz and first impressions under Getting to know people");
        int socialFeature = features.IndexOf("Features.Add(\"social\"", StringComparison.Ordinal), ledgerFeature = features.IndexOf("Features.Add(\"ledger\"", StringComparison.Ordinal);
        string feature = socialFeature >= 0 && ledgerFeature > socialFeature ? features.Substring(socialFeature, ledgerFeature - socialFeature) : string.Empty;
        Check(feature.Contains("Owns(Settings.RomanceYoungerId, Settings.RomanceOlderId)") && feature.Contains("Owns(Settings.FriendsQuizId, Settings.FirstImpressionsId)"),
            "friends: the options belong to the social life in the feature list");

        string life = ModSource(@"Social\SocialLife.cs"), links = ModSource(@"Wiring\LifeLinks.cs");
        Check(life.Contains("public static Func<SocialRepute> Repute;") && links.Contains("SocialLinks.Repute = Repute;") && links.Contains("SocialLinks.Repute = null;")
              && MemberText(links, "private static SocialRepute Repute()").Contains("Standing.Public") && links.Contains("Standing.HasRecord") && links.Contains("Standing.Recognition") && links.Contains("Standing.Integrity")
              && links.Contains("FameCity.InScandal") && links.Contains("flare.File.Followers"),
            "friends: the first impression asks the measures the mod already has (Law's public standing and record, Standing's recognition, integrity and scandal, the Flare followers) through a bridge connected and disconnected with the social life's others");
        Check(ModSource(@"Social\SocialState.cs").Contains("SocialFriendsStore.Save(data, People, Count);") && ModSource(@"Social\SocialState.cs").Contains("SocialFriendsStore.Load(data, People, Count);")
              && ModSource(@"Social\SocialState.cs").Contains("SocialFriendsStore.Reset();"), "friends: the save's reading, writing and reset include the friends' record");
        string keys = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\reference\keys.md")));
        Check(keys.Contains("`reallife:social_friends`"), "friends: the save key is listed in docs/reference/keys.md");
        string rules = ModSource(@"Social\SocialModel.cs");
        Check(rules.Contains("QuizAced") && rules.Contains("QuizFlop") && ModSource(@"Social\SocialCatalogue.cs").Contains("case MemoryCode.QuizAced: return \"reallife_social_memory_quiz_aced\";")
              && ModSource(@"Social\SocialCatalogue.cs").Contains("case MemoryCode.QuizFlop: return \"reallife_social_memory_quiz_flop\";"),
            "friends: the two new memories (the perfect score, the disaster) are named in the People page's history");
        string quiz = ModSource(@"Social\SceneQuiz.cs");
        Check(quiz.Contains("SocialLife.Remember(_friend, MemoryCode.QuizAced)") && quiz.Contains("SocialLife.Remember(_friend, MemoryCode.QuizFlop)") && quiz.Contains("SocialFriendsRules.Answered(_friend.Stage, right)")
              && quiz.Contains("SocialFriendsRules.Finished(_friend.Stage, right)") && quiz.Contains("entry.CancelTextOverride = answers[2];"),
            "friends: the quiz pays each answer at once, remembers a perfect score and a disaster, and offers the three answers on the panel's three buttons");
    }
}
