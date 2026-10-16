using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Street;

// Talking to anybody in the street (the owner, 16 Oct 2026: "Can you start the interacting with all NPCs?"): who somebody is
// (seeded, the same all day), whether they stop and why not (each reason, the option's frequency), warmth and patience, what
// is offered at each point, what each choice comes to, numbers and invitations (decided as on Flare), word of mouth and its
// cap, the day's memory and its bound, the reading of the player's own words both ways, the way to a place, the written
// lines in three languages with the values each needs, the save line, the timing and the hold, Going Public and nothing per
// frame (by source).
internal static partial class Program
{
    private static void StreetTalk()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        StreetWho();
        StreetApproaches();
        StreetChoicesOffered();
        StreetOutcomes();
        StreetNumbersAndInvites();
        StreetMouthAndStore();
        StreetMemoryOfTheDay();
        StreetReading();
        StreetWays();
        StreetWords();
        StreetTiming();
        StreetSources();
        Console.WriteLine("  street talk: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static StreetFacts StreetDay(int hour = 14, bool weekend = false)
    {
        var f = new StreetFacts { Day = 12, Hour = hour, Weekend = weekend, Standing = 55, Player = "Alex", SocialOn = true };
        return f;
    }

    private static StreetChat StreetChatWith(StreetStranger s, StreetFacts f, StreetMemo memo = null)
    {
        memo = memo ?? new StreetMemo { Key = s.Key, Day = f.Day };
        return new StreetChat { Who = s, Memo = memo, Approach = StreetRules.Approach(s, memo, f, 1, 0) };
    }

    // ---------------------------------------------------------------- who they are

    private static void StreetWho()
    {
        StreetStranger a = StreetRules.Identity(4242, 7, 991, true, 34, 6, -1), b = StreetRules.Identity(4242, 7, 991, true, 34, 6, -1);
        Check(a.Name == b.Name && a.Job == b.Job && a.District == b.District && a.Traits == b.Traits && a.Mood == b.Mood && a.Style == b.Style && a.Seed == b.Seed
              && a.Compatible == b.Compatible && a.Taken == b.Taken && a.Handle == b.Handle, "street talk: the same figure on the same day is the same person (name, job, home, traits, mood, style)");
        StreetStranger tomorrow = StreetRules.Identity(4242, 8, 991, true, 34, 6, -1);
        Check(tomorrow.Name == a.Name && tomorrow.Job == a.Job && tomorrow.District == a.District && tomorrow.Traits == a.Traits,
            "street talk: while the game keeps the figure, the next day it is still the same person; only the day's mood and plans change");
        Check(Array.IndexOf(SocialCatalogue.Women, a.First) >= 0 && Array.IndexOf(SocialCatalogue.Surnames, a.Last) >= 0, "street talk: a woman of the game's has a woman's name of the cast's lists");
        StreetStranger man = StreetRules.Identity(4242, 7, 992, false, 70, 6, -1);
        Check(Array.IndexOf(SocialCatalogue.Men, man.First) >= 0 && man.Job == SocialCatalogue.Jobs.Length - 1, "street talk: a man has a man's name; from 66 they are retired");
        Check(StreetRules.Identity(1, 1, 1, false, 9, 6, -1).Age == 18 && StreetRules.Identity(1, 1, 1, false, 140, 6, -1).Age == 85, "street talk: ages are kept to 18 to 85");
        var firsts = new HashSet<string>();
        bool ranges = true, pairs = true;
        int moods = 0;
        for (int k = 0; k < 600; k++)
        {
            StreetStranger s = StreetRules.Identity(77, 3, 1000 + k, k % 2 == 0, 20 + k % 50, 5, k % 7 == 0 ? 2 : -1);
            firsts.Add(s.First);
            ranges &= s.District >= 0 && s.District < 5 && s.Mood >= -2 && s.Mood <= 2 && s.Style >= 0 && s.Style < StreetRules.Styles.Length && s.Job >= 0 && s.Job < SocialCatalogue.Jobs.Length
                      && s.Handle.StartsWith("@" + s.First.ToLowerInvariant(), StringComparison.Ordinal);
            bool pair = false;
            foreach (SocialTrait[] p in SocialCatalogue.TraitPairs)
            {
                pair |= s.Traits == (p[0] | p[1]);
            }
            pairs &= pair;
            moods += s.Mood == StreetRules.Identity(77, 4, 1000 + k, k % 2 == 0, 20 + k % 50, 5, -1).Mood ? 0 : 1;
        }
        Check(firsts.Count >= 30, "street talk: 600 people have many different names (" + firsts.Count + ")");
        Check(ranges, "street talk: every district, mood, style, job and handle is in range");
        Check(pairs, "street talk: their two traits are a pair that goes together (the cast's own pairs)");
        Check(moods > 200, "street talk: moods change from day to day (" + moods + " of 600)");
        Check(StreetRules.Seed(1, 2, 3) == StreetRules.Seed(1, 2, 3) && StreetRules.Seed(1, 2, 3) != StreetRules.Seed(1, 2, 4) && StreetRules.Seed(int.MaxValue, int.MaxValue, int.MaxValue) >= 0,
            "street talk: seeds are deterministic, differ by figure, and are never negative");
    }

    // ---------------------------------------------------------------- whether they stop

    private static int StreetBrushes(StreetFacts f, int option, int phone, Func<StreetStranger, bool> which = null)
    {
        int n = 0;
        for (int k = 0; k < 2000; k++)
        {
            StreetStranger s = StreetRules.Identity(55, f.Day, 5000 + k, k % 2 == 0, 25 + k % 40, 6, -1);
            if (which != null && !which(s))
            {
                continue;
            }
            n += StreetRules.Approach(s, null, f, option, phone).Brush != StreetBrush.None ? 1 : 0;
        }
        return n;
    }

    private static void StreetApproaches()
    {
        StreetFacts f = StreetDay();
        StreetStranger s = StreetRules.Identity(55, 12, 5001, true, 30, 6, -1);
        Check(StreetRules.Approach(s, new StreetMemo { Rude = true }, f, 0, 0).Brush == StreetBrush.Rude, "street talk: somebody the player was rude to today has no time for them");
        StreetFacts wanted = StreetDay();
        wanted.Wanted = true;
        wanted.Recognition = 100;
        int wantedSeen = 0;
        for (int k = 0; k < 200; k++)
        {
            StreetApproach a = StreetRules.Approach(StreetRules.Identity(55, 12, 7000 + k, true, 30, 6, -1), null, wanted, 0, 0);
            wantedSeen += a.Recognised && a.Brush != StreetBrush.Wanted ? 1 : 0;
        }
        Check(wantedSeen == 0, "street talk: whoever knows the face of a player with a warrant out will not stop");
        StreetFacts rain = StreetDay();
        rain.Raining = true;
        Check(StreetRules.Approach(s, new StreetMemo { Talks = 1, Rapport = 2 }, rain, 2, 2).Brush == StreetBrush.None, "street talk: somebody who liked talking to the player earlier today always stops again");
        int rarely = StreetBrushes(f, 0, 0), sometimes = StreetBrushes(f, 1, 0), often = StreetBrushes(f, 2, 0);
        Check(rarely < sometimes && sometimes < often && often < 2000, "street talk: the option sets how often people have no time: rarely " + rarely + ", sometimes " + sometimes + ", often " + often + " of 2000");
        StreetFacts rush = StreetDay(8);
        int rushCommuters = StreetBrushes(rush, 1, 0, x => x.Commuter), dayCommuters = StreetBrushes(f, 1, 0, x => x.Commuter);
        Check(rushCommuters > dayCommuters * 2, "street talk: on their way to work at the rush hour, people have far less time (" + rushCommuters + " against " + dayCommuters + ")");
        Check(StreetBrushes(StreetDay(8, true), 1, 0, x => x.Commuter) < rushCommuters, "street talk: there is no rush hour at the weekend");
        Check(StreetBrushes(StreetDay(2), 1, 0) > sometimes, "street talk: in the small hours more people will not stop");
        Check(StreetBrushes(rain, 1, 0) > sometimes, "street talk: in the rain more people will not stop");
        int call = StreetBrushes(f, 1, 2), texting = StreetBrushes(f, 1, 1);
        Check(call > texting && texting > sometimes, "street talk: somebody on a call stops less than somebody texting, who stops less than somebody without a phone (" + call + ", " + texting + ", " + sometimes + ")");
        StreetFacts dirty = StreetDay();
        dirty.Unwashed = true;
        Check(StreetBrushes(dirty, 1, 0) > sometimes, "street talk: a player who has not washed is brushed off more");
        bool bounded = true, liked = true;
        StreetFacts loved = StreetDay();
        loved.Standing = 90;
        StreetFacts hated = StreetDay();
        hated.Standing = 10;
        int warmLoved = 0, warmHated = 0;
        for (int k = 0; k < 500; k++)
        {
            StreetStranger p = StreetRules.Identity(55, 12, 9000 + k, k % 2 == 0, 30, 6, -1);
            StreetApproach a = StreetRules.Approach(p, null, f, 1, 0);
            bounded &= a.Warmth >= -3 && a.Warmth <= 3 && a.Patience >= StreetRules.PatienceLeast && a.Patience <= StreetRules.PatienceMost;
            warmLoved += StreetRules.Approach(p, null, loved, 1, 0).Warmth;
            warmHated += StreetRules.Approach(p, null, hated, 1, 0).Warmth;
        }
        liked &= warmLoved > warmHated + 400;
        Check(bounded, "street talk: warmth stays within -3 to 3 and patience within " + StreetRules.PatienceLeast + " to " + StreetRules.PatienceMost + " turns");
        Check(liked, "street talk: a player the city likes is met more warmly than one it does not (" + warmLoved + " against " + warmHated + ")");
        StreetFacts famous = StreetDay();
        famous.Recognition = 100;
        famous.Standing = 80;
        int famousOpen = 0;
        for (int k = 0; k < 200; k++)
        {
            StreetApproach a = StreetRules.Approach(StreetRules.Identity(55, 12, 9900 + k, true, 30, 6, -1), null, famous, 0, 0);
            famousOpen += a.Recognised && a.Opening == StreetLine.OpenFamous ? 1 : 0;
        }
        Check(famousOpen > 120, "street talk: a well-known, well-liked player is recognised by most and greeted by name (" + famousOpen + " of 200)");
        Check(StreetRules.Approach(s, new StreetMemo { Talks = 1, Rapport = 1 }, f, 1, 0).Opening == StreetLine.OpenAgain, "street talk: met earlier today, they say so");
        Check(StreetRules.BrushLine(StreetBrush.Work) == StreetLine.BrushWork && StreetRules.BrushLine(StreetBrush.Phone) == StreetLine.BrushPhone && StreetRules.BrushLine(StreetBrush.Rude) == StreetLine.BrushRude,
            "street talk: each reason to have no time has its own lines");
    }

    // ---------------------------------------------------------------- what is offered

    private static void StreetChoicesOffered()
    {
        StreetFacts f = StreetDay();
        StreetStranger s = StreetRules.Identity(55, 12, 5003, true, 30, 6, -1);
        StreetChat c = StreetChatWith(s, f);
        var list = new List<StreetAsk>();
        StreetRules.Choices(c, f, true, list);
        Check(list.Count > 0 && list[0] == StreetAsk.SmallTalk && !list.Contains(StreetAsk.Number) && !list.Contains(StreetAsk.Invite) && list.Contains(StreetAsk.Say)
              && list.Contains(StreetAsk.Directions) && !list.Contains(StreetAsk.Election) && !list.Contains(StreetAsk.Business),
            "street talk: at first, small talk is offered first; no number or invitation before a first exchange, no election without one, no business without one");
        c.Turns = 1;
        StreetRules.Choices(c, f, false, list);
        Check(list.Contains(StreetAsk.Number) && list.Contains(StreetAsk.Invite) && !list.Contains(StreetAsk.Say), "street talk: after a first exchange the number and a coffee are offered; without the game's text box, no words of the player's own");
        f.SocialOn = false;
        StreetRules.Choices(c, f, true, list);
        Check(!list.Contains(StreetAsk.Number) && !list.Contains(StreetAsk.Invite), "street talk: without the social life no number and no invitation is offered");
        f.SocialOn = true;
        f.Election = true;
        f.Shops.Add("Sam's Deli");
        StreetRules.Choices(c, f, true, list);
        Check(list.Contains(StreetAsk.Election) && list.Contains(StreetAsk.Business), "street talk: the election while one is fought, the player's business when they have one");
        c.Done[(int)StreetAsk.SmallTalk] = true;
        c.Memo.Flirted = c.Memo.Joked = c.Memo.Complimented = c.Memo.Mentioned = c.Memo.Invited = c.Memo.Named = true;
        StreetRules.Choices(c, f, true, list);
        Check(!list.Contains(StreetAsk.SmallTalk) && !list.Contains(StreetAsk.Flirt) && !list.Contains(StreetAsk.Joke) && !list.Contains(StreetAsk.Compliment)
              && !list.Contains(StreetAsk.Business) && !list.Contains(StreetAsk.Invite) && !list.Contains(StreetAsk.Name) && list.Contains(StreetAsk.Directions),
            "street talk: what was done is not offered again (a flirt, a joke, a compliment, the business, an invitation, the name once a day); the way somewhere always is");
        StreetChat warm = StreetChatWith(s, f, new StreetMemo { Rapport = 5 });
        warm.Turns = 1;
        StreetRules.Choices(warm, f, true, list);
        Check(list.Count > 1 && list[0] == StreetAsk.Number && list[1] == StreetAsk.Invite, "street talk: once they warm to the player, the number and the invitation come first");
        int distinct = new HashSet<StreetAsk>(list).Count;
        Check(distinct == list.Count, "street talk: nothing is offered twice in one round");
    }

    // ---------------------------------------------------------------- what it comes to

    private static void StreetOutcomes()
    {
        StreetFacts f = StreetDay();
        StreetStranger s = StreetRules.Identity(55, 12, 5004, true, 30, 6, -1);
        StreetChat cold = StreetChatWith(s, f, new StreetMemo { Rapport = -4 });
        StreetOutcome rude = StreetRules.Answer(cold, StreetAsk.Rude, f, 1);
        Check(rude.Ends && rude.Line == StreetLine.WalkOff && rude.Rapport < 0, "street talk: rude to somebody already cool, they walk off");
        StreetChat fine = StreetChatWith(s, f, new StreetMemo { Rapport = 3 });
        StreetOutcome first = StreetRules.Answer(fine, StreetAsk.Rude, f, 1);
        Check(!first.Ends && first.Line == StreetLine.RudeCold && first.Rapport == -3, "street talk: rude once to somebody warm, a cold answer");
        fine.Memo.Rude = true;
        Check(StreetRules.Answer(fine, StreetAsk.Rude, f, 1).Ends, "street talk: rude twice, they walk off");
        StreetChat warm = StreetChatWith(s, f, new StreetMemo { Rapport = 6 });
        Check(StreetRules.Answer(warm, StreetAsk.Compliment, f, 2).Line == StreetLine.ComplimentYes && StreetRules.Answer(cold, StreetAsk.Compliment, f, 2).Line == StreetLine.ComplimentNo,
            "street talk: a compliment lands with somebody warm, not with somebody cold");
        var taken = new StreetStranger { First = "Dana", Last = "Reyes", Taken = true, Compatible = true, Seed = 5 };
        var free = new StreetStranger { First = "Dana", Last = "Reyes", Taken = false, Compatible = true, Seed = 5 };
        var notMine = new StreetStranger { First = "Dana", Last = "Reyes", Taken = false, Compatible = false, Seed = 5 };
        StreetChat ct = new StreetChat { Who = taken, Memo = new StreetMemo { Rapport = 6 } }, cf = new StreetChat { Who = free, Memo = new StreetMemo { Rapport = 6 } },
            cn = new StreetChat { Who = notMine, Memo = new StreetMemo { Rapport = 6 } };
        Check(StreetRules.Answer(ct, StreetAsk.Flirt, f, 3).Line == StreetLine.FlirtTaken && StreetRules.Answer(cf, StreetAsk.Flirt, f, 3).Line == StreetLine.FlirtYes
              && StreetRules.Answer(cn, StreetAsk.Flirt, f, 3).Line == StreetLine.FlirtNo, "street talk: a flirt: seeing somebody, they say so; free and a match, it works; not a match, no");
        Check(StreetRules.Answer(cf, StreetAsk.Flirt, f, 3).Rapport > StreetRules.Answer(cf, StreetAsk.SmallTalk, f, 3).Rapport, "street talk: a flirt that works counts for more than small talk");
        Check(StreetRules.Answer(cold, StreetAsk.AboutThem, f, 1).Line == StreetLine.AboutCool && StreetRules.Answer(warm, StreetAsk.AboutThem, f, 1).Line == StreetLine.AboutJob,
            "street talk: somebody cool will not say what they do; somebody warm does");
        Check(StreetRules.Answer(warm, StreetAsk.Name, f, 1).Line == StreetLine.NameGive && StreetRules.Answer(cold, StreetAsk.Name, f, 1).Line == StreetLine.NameWithhold,
            "street talk: their name for somebody warm, not for somebody cold");
        warm.Memo.Named = true;
        Check(StreetRules.Answer(warm, StreetAsk.Name, f, 1).Line == StreetLine.NameKnown, "street talk: asked again, they say they told the player already");
        StreetFacts e = StreetDay();
        Check(StreetRules.Answer(warm, StreetAsk.Election, e, 1).Line == StreetLine.ElectionNone, "street talk: no election, nothing to vote on");
        e.Election = true;
        e.Candidates.Add("Ruth Novak");
        StreetLine vote = StreetRules.Answer(warm, StreetAsk.Election, e, 1).Line;
        Check((vote == StreetLine.ElectionFor || vote == StreetLine.ElectionUnsure) && StreetRules.Candidate(warm.Who, 1) == 0 && StreetRules.Candidate(warm.Who, 0) == -1,
            "street talk: during an election they name one of those standing, or have not decided");
        e.PriceTrend = 1;
        StreetLine up = StreetRules.Answer(warm, StreetAsk.Economy, e, 1).Line;
        e.PriceTrend = -1;
        StreetLine down = StreetRules.Answer(warm, StreetAsk.Economy, e, 1).Line;
        e.PriceTrend = 0;
        Check(up == StreetLine.EconomyUp && down == StreetLine.EconomyDown && StreetRules.Answer(warm, StreetAsk.Economy, e, 1).Line == StreetLine.EconomyFlat,
            "street talk: prices as the city's economy has them: rising, falling or flat");
        e.PoliceNear = true;
        StreetOutcome threat = StreetRules.Threat(e);
        e.PoliceNear = false;
        Check(threat.Ends && threat.Police && threat.Line == StreetLine.ThreatLeave && !StreetRules.Threat(e).Police, "street talk: a threat sends them away; an officer near hears it, none far");
        Check(StreetRules.Said(warm, StreetSayKind.Goodbye).Ends && StreetRules.Said(warm, StreetSayKind.Nonsense).Line == StreetLine.SaidOdd && !StreetRules.Said(warm, StreetSayKind.Other).Ends,
            "street talk: the player's own goodbye ends it; nonsense is not understood; anything else is just talk");
        StreetChat spent = StreetChatWith(s, f);
        spent.Turns = spent.Approach.Patience;
        Check(StreetRules.Spent(spent) && !StreetRules.Spent(StreetChatWith(s, f)), "street talk: once they have given their turns they go");
    }

    // ---------------------------------------------------------------- numbers and invitations

    private static void StreetNumbersAndInvites()
    {
        StreetFacts f = StreetDay();
        StreetStranger s = StreetRules.Identity(55, 12, 5005, true, 30, 6, -1);
        s.Mood = 0;
        StreetChat c = StreetChatWith(s, f, new StreetMemo());
        c.Approach.Warmth = 0;
        c.Rapport = 3;
        Check(StreetRules.Number(c, f, false) == StreetLine.NumberYes, "street talk: after a good talk they give the player their number");
        Check(StreetRules.Number(c, f, true) == StreetLine.NumberLater, "street talk: nobody of the social life left to meet: they would rather bump into the player again");
        f.SocialOn = false;
        Check(StreetRules.Number(c, f, false) == StreetLine.NumberLater, "street talk: without the social life, no number");
        f.SocialOn = true;
        c.Rapport = 1;
        Check(StreetRules.Number(c, f, false) == StreetLine.NumberLater, "street talk: a talk that went only a little way: maybe next time");
        f.Standing = 80;
        Check(StreetRules.Number(c, f, false) == StreetLine.NumberYes, "street talk: a player the city likes needs less of a talk for a number");
        f.Standing = 55;
        c.Rapport = -1;
        Check(StreetRules.Number(c, f, false) == StreetLine.NumberNo, "street talk: a talk that went badly: no");
        c.Memo.Swapped = true;
        Check(StreetRules.Number(c, f, false) == StreetLine.NumberAlready, "street talk: numbers swapped already: they say so");

        // Invitations: decided by Flare's own rules with the talk standing in for messages exchanged.
        StreetChat good = StreetChatWith(s, f, new StreetMemo());
        good.Approach.Warmth = 0;
        good.Rapport = 3;
        s.Taken = false;
        FlareInviteAsk a = StreetRules.Invite(good, f, false, 1000);
        Check(a.Kind == PlanKind.Coffee && a.First == s.First && a.Last == s.Last && a.Sex == 1 && a.From == 1000 + StreetRules.InviteFrom && a.Chats >= FlareInviteRules.ChatsForYes,
            "street talk: an invitation is a coffee, for this person, from the margin on, a good talk counting as enough messages");
        Check(StreetRules.Invite(good, f, true, 1000).Kind == PlanKind.Meal, "street talk: after a flirt that went well it is dinner");
        a.Place = "Bean There";
        a.Street = "5th Avenue";
        a.Number = 12;
        a.PlanAt = 1200;
        a.When = "Monday 18:00";
        a.Roll = 0;
        a.Hour = 14;
        a.Cast = 3;
        FlareInviteAnswer yes = FlareInviteRules.Decide(a, out FlareInviteWhy why);
        Check(yes == FlareInviteAnswer.Yes && StreetRules.InviteLine(yes, why) == StreetLine.InviteYes, "street talk: a good talk, a free hour and a place: yes (" + yes + ", " + why + ")");
        a.Taken = true;
        FlareInviteAnswer no = FlareInviteRules.Decide(a, out why);
        Check(no == FlareInviteAnswer.No && StreetRules.InviteLine(no, why) == StreetLine.InviteNoTaken, "street talk: seeing somebody: no, and they say why");
        a.Taken = false;
        StreetChat poor = StreetChatWith(s, f, new StreetMemo());
        poor.Approach.Warmth = 0;
        FlareInviteAsk b = StreetRules.Invite(poor, f, false, 1000);
        b.Place = "Bean There";
        b.Street = "5th Avenue";
        b.Roll = 900;
        FlareInviteAnswer barely = FlareInviteRules.Decide(b, out why);
        Check(barely != FlareInviteAnswer.Yes, "street talk: somebody the player has hardly talked to does not say yes (" + barely + ", " + why + ")");
        b.Off = true;
        b.Chats = FlareInviteRules.ChatsForYes;
        FlareInviteAnswer off = FlareInviteRules.Decide(b, out why);
        Check(off == FlareInviteAnswer.Maybe && StreetRules.InviteLine(off, why) == StreetLine.InviteMaybeBusy, "street talk: outings switched off: not right now");
        bool lines = true;
        foreach (FlareInviteWhy w in Enum.GetValues(typeof(FlareInviteWhy)))
        {
            StreetLine m = StreetRules.InviteLine(FlareInviteAnswer.Maybe, w), n = StreetRules.InviteLine(FlareInviteAnswer.No, w);
            lines &= m >= StreetLine.InviteMaybeStranger && m <= StreetLine.InviteNo && n >= StreetLine.InviteMaybeStranger && n <= StreetLine.InviteNo;
        }
        Check(lines, "street talk: every reason of the decision has a line of the invitation's");
        Check(StreetRules.Business(new StreetChat { Who = s, Memo = new StreetMemo { Mentioned = true } }, 1) == StreetLine.BusinessAgain, "street talk: told about the business already, they say so");
        StreetChat keen = StreetChatWith(s, f, new StreetMemo { Rapport = 5 });
        StreetChat bored = StreetChatWith(s, f, new StreetMemo { Rapport = -5 });
        Check(StreetRules.Told(StreetRules.Business(keen, 1)) && !StreetRules.Told(StreetRules.Business(bored, 1)), "street talk: somebody who takes to the business will go; somebody cool will not");
    }

    // ---------------------------------------------------------------- word of mouth and the save

    private static void StreetMouthAndStore()
    {
        var m = new StreetMouth();
        int counted = 0;
        for (int i = 0; i < 10; i++)
        {
            counted += m.Tell("5th Avenue~12", 20) ? 1 : 0;
        }
        Check(counted == Math.Min(StreetRules.MouthPerDay, StreetRules.MouthMost / StreetRules.MouthPercent) && m.Percent("5th Avenue~12", 20) <= StreetRules.MouthMost,
            "street talk: word of mouth for one business is capped at " + StreetRules.MouthMost + "% and " + StreetRules.MouthPerDay + " people a day (" + counted + " counted)");
        Check(!m.Tell("Broadway~3", 20), "street talk: the day's people that count are for all the player's businesses together");
        Check(m.Tell("Broadway~3", 21), "street talk: a new day counts again");
        Check(m.Percent("5th Avenue~12", 20 + StreetRules.MouthDays - 1) > 0 && m.Percent("5th Avenue~12", 20 + StreetRules.MouthDays) == 0, "street talk: word of mouth lasts " + StreetRules.MouthDays + " days");
        Check(!m.Tell(null, 22) && !m.Tell(string.Empty, 22), "street talk: no business, no word of mouth");
        string line = StreetStore.Write(m);
        StreetMouth back = StreetStore.Read(line);
        Check(back != null && back.Told.Count == m.Told.Count && back.Counted == m.Counted && back.CountedDay == m.CountedDay && back.Percent("Broadway~3", 21) == m.Percent("Broadway~3", 21)
              && StreetStore.Write(back) == line, "street talk: the save's line reads back as written");
        Check(StreetStore.Read("v=2^c=1|1") == null && StreetStore.Read("v=1^t=5th~1|x|1") == null && StreetStore.Read("v=1^t=5th~1|1|9") == null && StreetStore.Read("v=1^zz") == null,
            "street talk: a line of a later version or one that cannot be read gives nothing (and is kept as read)");
        StreetMouth empty = StreetStore.Read(null);
        Check(empty != null && empty.Told.Count == 0 && StreetStore.Read("v=1^c=4|1^q=later") != null, "street talk: no line is nothing told; a record of a later version's kind is passed over");
        var old = new StreetMouth();
        old.Tell("Pearl Street~2", 50);
        old.Forget(40);
        Check(old.Told.Count == 0, "street talk: a day later than today (an older save loaded) is dropped");
    }

    // ---------------------------------------------------------------- the day's memory

    private static void StreetMemoryOfTheDay()
    {
        var memory = new StreetMemory();
        Check(memory.Find(5, 3) == null, "street talk: nobody remembers the player before they talk");
        StreetMemo m = memory.Get(5, 3);
        m.Rude = true;
        Check(memory.Find(5, 3) != null && memory.Find(5, 3).Rude, "street talk: what happened is remembered for the day");
        for (int i = 0; i < StreetRules.MemoryMost + 10; i++)
        {
            memory.Get(100 + i, 3);
        }
        Check(memory.Count == StreetRules.MemoryMost && memory.Find(5, 3) == null && memory.Find(100 + StreetRules.MemoryMost + 9, 3) != null,
            "street talk: at most " + StreetRules.MemoryMost + " people are remembered; the one talked to longest ago goes first");
        memory.Get(200, 3);
        Check(memory.Find(200, 4) == null && memory.Count == 0, "street talk: the next day everybody has forgotten");
        var chat = new StreetChat { Memo = new StreetMemo { Rapport = 9 }, Rapport = 5 };
        StreetRules.Close(chat);
        Check(chat.Memo.Talks == 1 && chat.Memo.Rapport == StreetRules.RapportMost, "street talk: a talk adds to the day's memory, within bounds");
        var c2 = new StreetChat();
        for (int i = 0; i < 10; i++)
        {
            c2.Said(i % 2 == 0, "line " + i);
        }
        Check(c2.History.Count == 6 && c2.History[5] == "S:line 9" && c2.History[0] == "P:line 4", "street talk: the last six lines are kept for the card, the player's and theirs told apart");
    }

    // ---------------------------------------------------------------- the player's own words

    private static void StreetReading()
    {
        var shops = new List<string> { "Sam's Deli", "Glow Beauty" };
        var cases = new (string Text, StreetSayKind Kind)[]
        {
            ("I'll hurt you", StreetSayKind.Threat), ("give me your wallet", StreetSayKind.Threat), ("watch your back mate", StreetSayKind.Threat), ("i know where you live", StreetSayKind.Threat),
            ("you're an idiot", StreetSayKind.Rude), ("shut up", StreetSayKind.Rude), ("get lost", StreetSayKind.Rude), ("you stink", StreetSayKind.Rude), ("nobody asked you", StreetSayKind.Rude),
            ("fancy a coffee?", StreetSayKind.Invite), ("drinks later?", StreetSayKind.Invite), ("wanna grab dinner sometime", StreetSayKind.Invite), ("up for a walk?", StreetSayKind.Invite),
            ("can i get your number", StreetSayKind.Number), ("whats your number?", StreetSayKind.Number), ("swap numbers?", StreetSayKind.Number),
            ("where is the nearest cafe?", StreetSayKind.Directions), ("is there a gym near here", StreetSayKind.Directions), ("how do i get to sam's deli", StreetSayKind.Directions),
            ("where's a good pub", StreetSayKind.Directions), ("looking for a supermarket", StreetSayKind.Directions), ("where is bean there", StreetSayKind.Directions),
            ("who will you vote for", StreetSayKind.Election), ("what do you think of the mayor", StreetSayKind.Election),
            ("prices are mad these days", StreetSayKind.Economy), ("how's the rent round here", StreetSayKind.Economy),
            ("have you been to sam's deli?", StreetSayKind.Business), ("come to my shop sometime", StreetSayKind.Business),
            ("what's good around here", StreetSayKind.Area), ("anything to do nearby", StreetSayKind.Area),
            ("what's your name", StreetSayKind.Name), ("who are you", StreetSayKind.Name),
            ("you're cute", StreetSayKind.Flirt), ("are you single", StreetSayKind.Flirt), ("you have beautiful eyes", StreetSayKind.Flirt),
            ("nice jacket", StreetSayKind.Compliment), ("i like your style", StreetSayKind.Compliment),
            ("knock knock", StreetSayKind.Joke), ("haha", StreetSayKind.Joke),
            ("what do you do for work", StreetSayKind.AboutThem), ("how are you", StreetSayKind.AboutThem), ("how's it going", StreetSayKind.AboutThem),
            ("bye", StreetSayKind.Goodbye), ("see you later", StreetSayKind.Goodbye), ("take care", StreetSayKind.Goodbye),
            ("thanks", StreetSayKind.Thanks), ("thank you so much", StreetSayKind.Thanks),
            ("hello", StreetSayKind.Greeting), ("hi there", StreetSayKind.Greeting), ("good morning", StreetSayKind.Greeting),
            ("lovely day isn't it", StreetSayKind.SmallTalk), ("the weather is awful", StreetSayKind.SmallTalk),
            ("", StreetSayKind.Nonsense), ("???", StreetSayKind.Nonsense), ("123", StreetSayKind.Nonsense),
            ("i like turtles", StreetSayKind.Other), ("this is my first time in the city", StreetSayKind.Other), ("this", StreetSayKind.Other)
        };
        var wrong = new List<string>();
        foreach ((string text, StreetSayKind kind) in cases)
        {
            StreetSayKind got = StreetSay.Read(text, shops).Kind;
            if (got != kind)
            {
                wrong.Add("'" + text + "' read as " + got + ", not " + kind);
            }
        }
        Check(wrong.Count == 0, "street talk: " + cases.Length + " things a player says are read as what they are (" + string.Join("; ", wrong) + ")");
        StreetSaid cafe = StreetSay.Read("where is the nearest cafe?", shops), gym = StreetSay.Read("is there a gym near here", shops), named = StreetSay.Read("where is bean there", shops);
        Check(cafe.Place == StreetPlace.Coffee && gym.Place == StreetPlace.Gym && named.Place == StreetPlace.Count && named.Target == "bean there",
            "street talk: the way asked: a kind of place, or a business by its name");
        Check(StreetSay.Read("have you been to sam's deli?", shops).Shop == 0 && StreetSay.Read("glow beauty is mine", shops).Shop == 1 && StreetSay.Read("hello", shops).Shop == -1,
            "street talk: the player's own business is known by its name");
        Check(StreetSay.Read("fancy a coffee?", shops).Outing == PlanKind.Coffee && StreetSay.Read("drinks later?", shops).Outing == PlanKind.Drinks, "street talk: an invitation's outing as Flare reads it");
        Check(StreetSay.Names("bean there", "Bean There Cafe") && StreetSay.Names("Sams Deli", "Sam's Deli") && !StreetSay.Names("bar", "Barbosa Studios") && !StreetSay.Names("", "Bean There"),
            "street talk: a business asked for by name: the same letters, or most of its name");
        Check(StreetSay.AskOf(StreetSayKind.Invite) == StreetAsk.Invite && StreetSay.AskOf(StreetSayKind.Rude) == StreetAsk.Rude && StreetSay.AskOf(StreetSayKind.Other) == StreetAsk.Count
              && StreetSay.AskOf(StreetSayKind.Threat) == StreetAsk.Count, "street talk: words count as the choice they are read as; just talk and a threat as no choice");
        string longText = new string('a', 500);
        Check(StreetSay.Read(longText, shops).Kind == StreetSayKind.Other, "street talk: a long line is read up to " + StreetSay.Most + " characters");
    }

    // ---------------------------------------------------------------- the way

    private static void StreetWays()
    {
        Check(StreetRules.Way(0f) == 0 && StreetRules.Way(90f) == 1 && StreetRules.Way(180f) == 2 && StreetRules.Way(-90f) == 3 && StreetRules.Way(350f) == 0 && StreetRules.Way(-170f) == 2,
            "street talk: the way to a place from the player's facing: ahead, right, behind, left");
        Check(StreetRules.Minutes(10f) == 1 && StreetRules.Minutes(750f) == 10 && StreetRules.Directions(true, StreetRules.FarMetres + 1f) == StreetLine.DirFar
              && StreetRules.Directions(true, 100f) == StreetLine.DirFound && StreetRules.Directions(false, 0f) == StreetLine.DirNone,
            "street talk: how far, in minutes on foot, and a place far off said so");
        Check(StreetRules.IsPlace("ba:businesstype_coffeeshop", StreetPlace.Coffee) && StreetRules.IsPlace("ba:businesstype_fastfoodrestaurant", StreetPlace.Food)
              && StreetRules.IsPlace("ba:businesstype_bank", StreetPlace.Bank) && !StreetRules.IsPlace("ba:businesstype_coffeeshop", StreetPlace.Bank)
              && !StreetRules.IsPlace("ba:businesstype_bankheadquarters", StreetPlace.Coffee) && !StreetRules.IsPlace(null, StreetPlace.Coffee) && !StreetRules.IsPlace("x", StreetPlace.Count),
            "street talk: a kind of place is the game's business types of that kind");
        Check(StreetRules.PlaceTypes.Length == (int)StreetPlace.Count && StreetLines.Places.Length == (int)StreetPlace.Count && StreetLines.Whats.Length == (int)StreetPlace.Count && StreetLines.Ways.Length == 4,
            "street talk: every kind of place has its types, its button and its words");
    }

    // ---------------------------------------------------------------- the written lines, three languages

    private static void StreetWords()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("street talk", words, codes))
        {
            return;
        }
        Check(StreetLines.Lines.Length == (int)StreetLine.Count && StreetLines.Asks.Length == (int)StreetAsk.Count, "street talk: every kind of line and every choice has its words");
        // The values each kind's lines may name: what the conversation fills for it (StreetDirector.Talk.cs).
        var always = new HashSet<string> { "first", "player", "job", "district", "shop" };
        var extra = new Dictionary<StreetLine, string[]>
        {
            { StreetLine.AreaPlaces, new[] { "place", "place_two" } }, { StreetLine.AreaEvent, new[] { "event" } },
            { StreetLine.DirFound, new[] { "place", "street", "way", "walk", "what" } }, { StreetLine.DirFar, new[] { "place", "street", "way", "walk", "what" } },
            { StreetLine.DirNone, new[] { "what" } }, { StreetLine.InviteYes, new[] { "place", "time" } }, { StreetLine.ElectionFor, new[] { "candidate" } }
        };
        var lacking = new List<string>();
        var unfilled = new List<string>();
        int keys = 0;
        for (int i = 0; i < StreetLines.Lines.Length; i++)
        {
            Check(StreetLines.Lines[i].Length >= 1, "street talk: the line " + (StreetLine)i + " has written lines");
            for (int j = 0; j < StreetLines.Lines[i].Length; j++)
            {
                string key = StreetLines.Lines[i][j];
                keys++;
                string lack = LanguagesLacking(words, codes, key);
                if (lack.Length > 0)
                {
                    lacking.Add(key + " (" + lack + ")");
                }
                if (!words[0].TryGetValue(key, out string en))
                {
                    continue;
                }
                foreach (System.Text.RegularExpressions.Match m in Names.Matches(en))
                {
                    string name = m.Value.Trim('{', '}');
                    if (!always.Contains(name) && !(extra.TryGetValue((StreetLine)i, out string[] more) && Array.IndexOf(more, name) >= 0))
                    {
                        unfilled.Add(key + " {" + name + "}");
                    }
                }
            }
        }
        var other = new List<string>(StreetLines.Asks);
        other.AddRange(StreetLines.Places);
        other.AddRange(StreetLines.Whats);
        other.AddRange(StreetLines.Ways);
        other.AddRange(new[]
        {
            StreetLines.AskRain, StreetLines.AskNight, StreetLines.AskDinner, StreetLines.Opener, "reallife_street_btn_goodbye", "reallife_street_btn_leave", "reallife_street_btn_somewhere_else",
            "reallife_street_btn_never_mind", "reallife_street_btn_talk", "reallife_street_btn_say", "reallife_street_say_placeholder", "reallife_street_stranger", "reallife_street_walk_one",
            "reallife_street_walk_many", "reallife_street_note_police", "reallife_street_note_booked", "reallife_street_note_mouth", "reallife_street_header", "reallife_street_enabled",
            "reallife_street_opt_brush", "reallife_street_brush_0", "reallife_street_brush_1", "reallife_street_brush_2", "reallife_street_test_talk", "reallife_street_test_forget",
            "reallife_street_test_nobody", "reallife_street_test_forgot", "reallife_meta_title_street", "reallife_guide_street_about", "reallife_guide_street", "reallife_opt_sec_street",
            "reallife_opt_tip_street_talk", "reallife_opt_tip_street_brush", "reallife_social_desc_street", "reallife_social_welcome_street", "reallife_social_meet_street_1", "reallife_social_meet_street_2"
        });
        foreach (string key in other)
        {
            keys++;
            string lack = LanguagesLacking(words, codes, key);
            if (lack.Length > 0)
            {
                lacking.Add(key + " (" + lack + ")");
            }
        }
        Check(lacking.Count == 0, "street talk: all " + keys + " texts are in every language with the English's names in curly brackets (" + string.Join(", ", lacking) + ")");
        Check(unfilled.Count == 0, "street talk: every name in curly brackets in a line is one the conversation fills for that kind (" + string.Join(", ", unfilled) + ")");
        bool marks = true;
        for (int l = 0; l < codes.Length && l < 3; l++)
        {
            foreach (string[] pool in StreetLines.Lines)
            {
                foreach (string key in pool)
                {
                    marks &= words[l].TryGetValue(key, out string v) && v.IndexOf('!') < 0 && v.IndexOf("...", StringComparison.Ordinal) < 0 && v.Length <= 160;
                }
            }
        }
        Check(marks, "street talk: no line has an exclamation mark or three dots, and each is short enough to be said (160 characters)");
        string next = words[0].TryGetValue("reallife_changes_next", out string n) ? n : string.Empty;
        Check(next.Contains("Street talk:") && words[1]["reallife_changes_next"].Contains("Conversa na rua") && words[2]["reallife_changes_next"].Contains("街头交谈"),
            "street talk: What's new says it in three languages");
        bool picks = true;
        for (int i = 0; i < StreetLines.Lines.Length; i++)
        {
            string last = null;
            for (int t = 0; t < 20; t++)
            {
                string k = StreetLines.Pick((StreetLine)i, 1234, t, last);
                picks &= Array.IndexOf(StreetLines.Lines[i], k) >= 0 && (StreetLines.Lines[i].Length == 1 || k != last) && k == StreetLines.Pick((StreetLine)i, 1234, t, last);
                last = k;
            }
        }
        Check(picks, "street talk: a line is one of its kind's, the same for the same person and turn, never the one they said last");
    }

    // ---------------------------------------------------------------- timing

    private static void StreetTiming()
    {
        Check(StreetRules.Hold <= 1.5 && StreetRules.Hold > StreetRules.Beat && StreetRules.Beat >= StreetRules.TurnSeconds * 0.5f, "street talk: a line waits at most 1.5 s for its words, after a beat for the turn ("
                                                                                                                    + StreetRules.Hold + " s, " + StreetRules.Beat + " s)");
        Check(StreetRules.Tick >= 0.2f && StreetRules.Dwell >= StreetRules.Tick && StreetRules.LetGo > StreetRules.Reach && StreetRules.WalkAway > StreetRules.LetGo,
            "street talk: four looks a second at most, an offer only after the player stood there a moment, and a step of give before it goes");
        TimeStreetRules(out double perChoice, out long allocated);
        Check(perChoice < 0.05, "street talk: working out who somebody is, their approach and the choices takes " + perChoice.ToString("0.0000") + " ms (under 0.05 ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void TimeStreetRules(out double ms, out long allocated)
    {
        StreetFacts f = StreetDay();
        var list = new List<StreetAsk>(16);
        var watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        const int n = 2000;
        for (int i = 0; i < n; i++)
        {
            StreetStranger s = StreetRules.Identity(9, 9, i, i % 2 == 0, 40, 6, -1);
            var c = new StreetChat { Who = s, Memo = null, Approach = StreetRules.Approach(s, null, f, 1, 0) };
            StreetRules.Choices(c, f, true, list);
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.Elapsed.TotalMilliseconds / n;
    }

    // ---------------------------------------------------------------- the sources

    private static void StreetSources()
    {
        string director = TextsSource(@"Street\StreetDirector.cs"), talk = TextsSource(@"Street\StreetDirector.Talk.cs"), game = TextsSource(@"Street\StreetGame.cs");
        string links = TextsSource(@"Wiring\StreetLinks.cs");
        string all = director + talk + game + links + TextsSource(@"Street\StreetRules.cs") + TextsSource(@"Street\StreetSay.cs") + TextsSource(@"Street\StreetModel.cs") + TextsSource(@"Street\StreetStore.cs")
                     + TextsSource(@"Street\StreetDirector.Tests.cs") + TextsSource(@"Street\StreetGuide.cs") + TextsSource(@"Street\StreetLines.cs") + TextsSource(@"Street\StreetBridges.cs");
        int update = director.IndexOf("private void OnUpdate()", StringComparison.Ordinal);
        string body = update >= 0 ? director.Substring(update, Math.Min(900, director.Length - update)) : string.Empty;
        Check(body.Contains("if (now < _next)") && body.IndexOf("if (now < _next)", StringComparison.Ordinal) < body.IndexOf("Look(now)", StringComparison.Ordinal),
            "street talk: per frame, one time comparison before anything else");
        Check(director.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && director.Contains("HotSpots.Time(HotLook)"), "street talk: its Update and its look are timed (HotSpots)");
        Check(!all.Contains("FindObjectsOfType") && !all.Contains("FindObjectsByType") && !all.Contains("GameObject.Find") && !all.Contains("Physics.") && !all.Contains("System.Linq"),
            "street talk: no scene search, no physics query and no LINQ anywhere in it");
        Check(game.Contains("PerfGame.Pedestrians") && game.Contains("GoingPublic.RemotePlayers(Remote)") && game.Contains("Theirs(p)"),
            "street talk: the people are the game's own walkers; another player's figure in a Going Public session is never one");
        Check(!all.Contains("SharedCity") && !all.Contains("TextsShare") && !all.Contains("Shared = true") && !all.Contains("Changes(") && !all.Contains("Network"),
            "street talk: nothing is sent to a Going Public session: the conversations are the player's own");
        Check(game.Contains("GoingPublic.Mine(reg)") && links.Contains("SocialLife.MakePlan(") && links.Contains("SocialLife.Meet(p,"),
            "street talk: only the player's own businesses get word of mouth; a number and a plan go through the social life's own entries");
        Check(talk.Contains("FlareInviteRules.Decide(a, out FlareInviteWhy why)") && talk.Contains("StreetSay.Read(words, _facts.Shops)"),
            "street talk: an invitation is decided by Flare's own rules; the player's words are read with fixed rules");
        Check(talk.Contains("_openBy = now + (float)StreetRules.Hold") && talk.Contains("now >= _openBy ||"), "street talk: the panel opens at the latest when the hold is over, words or not");
        Check(game.Contains("ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog") && game.Contains("textInputUi.Show(") && director.Contains("new DoorPanel("),
            "street talk: the game's own panels: the box over a person, its dialogue panel, its text box");
        Check(game.Contains("p.enabled = _ped;") && game.Contains("agent.isStopped = _stopped;") && talk.Contains("_hold.Release();"),
            "street talk: a stopped walker is given back as found on every way out");
        Check(TextsSource(@"Core\RealLifeOverhaulMod.cs").Contains("o.AddStreetTalk()") && TextsSource(@"Core\RealLifeOverhaulMod.cs").Contains("Guard.Add<Street.StreetDirector>(_root)")
              && TextsSource(@"Meta\FeatureList.cs").Contains("Street.StreetGuide.Register();") && TextsSource(@"Wiring\Wiring.cs").Contains("StreetLinks.Connect")
              && TextsSource(@"World\NearbyInteraction.cs").Contains("Street.StreetDirector.TakesInteractKey"),
            "street talk: its options, its director, its feature entry, its wiring and its say over the interact key are in place");
        Check(TextsSource(@"Social\SocialModel.cs").Contains("Online, Event, Street }") && TextsSource(@"Social\SocialModel.cs").Contains("p.Origin > SocialOrigin.Street")
              && SocialCatalogue.OriginDesc.Length == (int)SocialOrigin.Street + 1 && SocialCatalogue.OriginWelcome.Length == (int)SocialOrigin.Street + 1 && SocialCatalogue.MeetOpen.Length == (int)SocialOrigin.Street + 1,
            "street talk: met in the street is a place of meeting of the social life's own, at the end, with its words (an older version reads it as an old friend)");
    }
}
