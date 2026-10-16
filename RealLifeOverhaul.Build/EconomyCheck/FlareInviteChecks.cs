using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Texts;

// Invitations in the player's Flare messages (a player's question, 16 Oct 2026: "If I say up for a coffee will it schedule a
// date?"): the reading of real phrasings both ways, every branch of the decision, the stranger's rule, the answer's time
// before the plan's reminder and panel line, the same answer on a reload, a yes booked exactly once (a second invitation
// pointing to it), the answer's key and RLO Texts intent per outcome and the yes card's facts and must, the written lines in
// three languages with the place and the time, the names of the city's people sorted by sex, and the source guarantees:
// somebody new met online as when they answer a post, the plan made as a call makes it, nothing sent in Going Public,
// nothing per frame.
internal static partial class Program
{
    private static void FlareInvites()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        InvitesReading();
        InvitesDeciding();
        InvitesTiming();
        InvitesOnce();
        InvitesWords();
        InvitesTexts();
        InvitesNames();
        InvitesSources();
        Console.WriteLine("  flare invitations: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- reading

    private static void InvitesReading()
    {
        var yes = new (string Text, PlanKind Kind)[]
        {
            ("you up for a coffee?", PlanKind.Coffee), ("up for a coffee", PlanKind.Coffee), ("coffee?", PlanKind.Coffee), ("COFFEE?", PlanKind.Coffee),
            ("fancy a coffee", PlanKind.Coffee), ("wanna grab a coffee sometime", PlanKind.Coffee), ("u free for coffee later", PlanKind.Coffee),
            ("cofee tmrw?", PlanKind.Coffee), ("coffeeee??", PlanKind.Coffee), ("\u2615?", PlanKind.Coffee), ("let's get coffee", PlanKind.Coffee),
            ("lets grab a cuppa", PlanKind.Coffee), ("how about a coffee this afternoon", PlanKind.Coffee), ("would you like to get a coffee with me", PlanKind.Coffee),
            ("can i buy you a coffee", PlanKind.Coffee), ("coffee on me tomorrow", PlanKind.Coffee), ("Up for a latte?", PlanKind.Coffee),
            ("tea or coffee later?", PlanKind.Coffee), ("shall we get coffee", PlanKind.Coffee), ("drop by for coffee sometime?", PlanKind.Coffee),
            ("Coffee sometime? My treat.", PlanKind.Coffee), ("u up for a coffe l8r", PlanKind.Coffee),
            ("meet up sometime?", PlanKind.Coffee), ("wanna hang out?", PlanKind.Coffee), ("hang out this weekend?", PlanKind.Coffee),
            ("we should catch up soon", PlanKind.Coffee), ("fancy meeting up?", PlanKind.Coffee), ("want to link up later?", PlanKind.Coffee),
            ("lets get together this week", PlanKind.Coffee), ("would you go on a date with me", PlanKind.Coffee), ("wanna chill sometime?", PlanKind.Coffee),
            ("nice to meet you, fancy a coffee?", PlanKind.Coffee),
            ("wanna grab food", PlanKind.Meal), ("dinner tonight?", PlanKind.Meal), ("Fancy dinner on Friday?", PlanKind.Meal), ("lunch tmrw?", PlanKind.Meal),
            ("brekkie?", PlanKind.Meal), ("pizza later?", PlanKind.Meal), ("lets get a bite to eat", PlanKind.Meal), ("can i take you to dinner", PlanKind.Meal),
            ("want to get lunch with me?", PlanKind.Meal), ("\U0001F355 tonight?", PlanKind.Meal), ("dinr 2nite?", PlanKind.Meal),
            ("how about brunch on sunday", PlanKind.Meal), ("dinner date?", PlanKind.Meal), ("dinnner tomorow?", PlanKind.Meal),
            ("fancy a drink later", PlanKind.Drinks), ("drinks later?", PlanKind.Drinks), ("pint after work?", PlanKind.Drinks), ("lets get a beer", PlanKind.Drinks),
            ("\U0001F37B?", PlanKind.Drinks), ("up for a few drinks tonight", PlanKind.Drinks), ("wanna hit the pub", PlanKind.Drinks), ("cocktails on me", PlanKind.Drinks),
            ("drnks l8r?", PlanKind.Drinks), ("fancy a pint w/ me", PlanKind.Drinks),
            ("wanna see a movie?", PlanKind.Cinema), ("cinema tonight?", PlanKind.Cinema), ("movie night?", PlanKind.Cinema), ("fancy the cinema later", PlanKind.Cinema),
            ("wanna catch a film this weekend", PlanKind.Cinema), ("moive tmrw?", PlanKind.Cinema), ("\U0001F37F later?", PlanKind.Cinema), ("lets go see a flick", PlanKind.Cinema),
            ("u free for a walk", PlanKind.Walk), ("walk in the park later?", PlanKind.Walk), ("fancy a stroll?", PlanKind.Walk), ("lets go for a walk", PlanKind.Walk),
            ("wlak tmrw?", PlanKind.Walk), ("how about a walk after work", PlanKind.Walk), ("join me for a walk", PlanKind.Walk),
            ("gym later?", PlanKind.Workout), ("wanna hit the gym together", PlanKind.Workout), ("fancy a workout tomorrow", PlanKind.Workout),
            ("lets work out together", PlanKind.Workout), ("come for a run with me?", PlanKind.Workout), ("join me at the gym", PlanKind.Workout),
            ("yoga sometime?", PlanKind.Workout), ("wanna go for a run", PlanKind.Workout)
        };
        string[] no =
        {
            "how much is a coffee at your place?", "coffee prices are crazy", "what's the price of a pint here", "do you like coffee?", "I love coffee",
            "had a great coffee today", "the coffee at bean there is awful", "where can i get good coffee", "is the cinema open on sundays",
            "did you see the new film", "what's your favourite movie", "i went to the gym yesterday", "i'm at the gym", "drinks were great last night",
            "how was dinner?", "what's for dinner", "i need a coffee", "free coffee at my shop today", "do you sell coffee?", "your coffee is the best",
            "i hate going to the gym", "my gym membership is so expensive", "the walk home was long", "i walk my dog every morning",
            "the food at that place was cold", "movie recommendations?", "talk later?", "help me move?", "nice to meet you", "hello", "how are you",
            "thanks for following", "love your posts", "can you move your car", "hang on a sec?", "is coffee bad for you?", "best coffee in town?",
            "i'll grab a coffee later", "my coffee went cold", "i'm drinking a coffee rn", "which bar is the best", "how's the gym going?",
            "coffee is life", "up to date?", "good to meet you", "where's the nearest gym?", "cinema tickets cost a fortune", "", "?", "\U0001F600"
        };
        var wrong = new List<string>();
        int kinds = 0;
        foreach ((string text, PlanKind kind) in yes)
        {
            if (!FlareInviteRules.Read(text, out PlanKind read) || read != kind)
            {
                wrong.Add("'" + text + "' (" + kind + ")");
            }
            kinds |= 1 << (int)kind;
        }
        Check(wrong.Count == 0, "flare invitations: " + yes.Length + " real invitations are read, each with its outing (wrong: " + string.Join(", ", wrong) + ")");
        Check(yes.Length >= 60 && kinds == (1 << (int)PlanKind.Coffee | 1 << (int)PlanKind.Meal | 1 << (int)PlanKind.Drinks | 1 << (int)PlanKind.Cinema
                                         | 1 << (int)PlanKind.Walk | 1 << (int)PlanKind.Workout),
            "flare invitations: invitations of every kind are among them (coffee, a meal, drinks, the cinema, a walk, a workout; a plain meeting is a coffee)");
        wrong.Clear();
        foreach (string text in no)
        {
            if (FlareInviteRules.Read(text, out _))
            {
                wrong.Add("'" + text + "'");
            }
        }
        Check(wrong.Count == 0, "flare invitations: " + no.Length + " messages that are not invitations are not read as one (a price, a taste, a review, what was done; read: "
                                + string.Join(", ", wrong) + ")");
        Check(yes.Length + no.Length >= 80, "flare invitations: at least 80 real phrasings are read both ways (" + (yes.Length + no.Length) + ")");
        Check(!FlareInviteRules.Read(null, out _), "flare invitations: no text is no invitation");
        Check(FlareInviteRules.OneOff("cinama", "cinema") && FlareInviteRules.OneOff("moive", "movie") && FlareInviteRules.OneOff("drnks", "drinks")
              && !FlareInviteRules.OneOff("drunk", "drink") && !FlareInviteRules.OneOff("toffee", "coffee") && !FlareInviteRules.OneOff("talk", "walk")
              && !FlareInviteRules.OneOff("cinema", "cinema"),
            "flare invitations: a typo is a letter changed (longer words), left out, added or swapped, never the first letter (talk is not walk, toffee not coffee)");
        Check(FlareInviteRules.Mend("Let's grab a COFFEEEE?") == " lets grab a coffee ? ", "flare invitations: the text is mended for reading: small letters, no apostrophe, a letter said twice at most, the question mark a word");
    }

    // ---------------------------------------------------------------- deciding

    // Somebody of the social life, free, a place and an hour found: a yes unless something says otherwise.
    private static FlareInviteAsk InviteKnown()
    {
        return new FlareInviteAsk
        {
            Kind = PlanKind.Coffee, Known = true, Cast = 3, Hour = 15, Standing = 50, Reputation = 50, Roll = 700, From = 10000, PlanAt = 10080,
            Place = "Bean There", Street = "5th Avenue", Number = 12, When = "Monday 18:00", CardWhen = "Monday 18:00", Outing = "coffee"
        };
    }

    // Somebody new on Flare: a city person never met, at three in the afternoon, a place and an hour found.
    private static FlareInviteAsk InviteStranger(int chats, int standing, int roll)
    {
        FlareInviteAsk a = InviteKnown();
        a.Known = false;
        a.Cast = 5;
        a.First = "Hana";
        a.Last = "Hollis";
        a.Sex = 1;
        a.Age = 30;
        a.Chats = chats;
        a.Standing = standing;
        a.Roll = roll;
        return a;
    }

    private static bool Is(FlareInviteAsk a, FlareInviteAnswer want, FlareInviteWhy wantWhy)
    {
        return FlareInviteRules.Decide(a, out FlareInviteWhy why) == want && why == wantWhy;
    }

    private static void InvitesDeciding()
    {
        FlareInviteAsk a = InviteKnown();
        Check(Is(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes), "flare invitations: somebody known, free, with a place and an hour, says yes");
        a = InviteKnown(); a.PlanWithThem = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Plan), "flare invitations: a plan with them on already: maybe, pointing to it");
        a = InviteKnown(); a.PlanOther = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Busy), "flare invitations: a plan with somebody else on (one at a time, as on the phone): maybe");
        a = InviteKnown(); a.Off = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Busy), "flare invitations: outings switched off: nothing can be booked, maybe");
        a = InviteKnown(); a.Asleep = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Night), "flare invitations: their night hours: maybe");
        a = InviteKnown(); a.LowMood = true; a.Roll = FlareInviteRules.MoodNo - 1;
        Check(Is(a, FlareInviteAnswer.No, FlareInviteWhy.Mood), "flare invitations: a rough day: no half the time (the phone's own odds)");
        a.Roll = FlareInviteRules.MoodNo;
        Check(Is(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes), "flare invitations: a rough day: yes the other half");
        a = InviteKnown(); a.Dislikes = true;
        Check(Is(a, FlareInviteAnswer.No, FlareInviteWhy.Dislikes), "flare invitations: somebody who remembers being stood up, a rude word or money not repaid: no");
        a = InviteKnown(); a.NeedsMore = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Later), "flare invitations: an outing their stage does not allow yet (drinks from friends on): maybe");
        a = InviteKnown(); a.NoPlace = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Nowhere), "flare invitations: no place near the player: maybe");
        a = InviteKnown(); a.Street = null;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Nowhere), "flare invitations: a place with no address to book: maybe");
        a = InviteKnown(); a.NoSlot = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Busy), "flare invitations: no hour that suits within two days: maybe");
        a = InviteKnown(); a.PlanAt = a.From - 1;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Busy), "flare invitations: an hour before the yes could be read is never booked: maybe");
        a = InviteKnown(); a.Known = false; a.Influencer = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Influencer), "flare invitations: an influencer says maybe at most (they meet people through friends)");
        a.Standing = FlareInviteRules.DislikeBelow - 1;
        Check(Is(a, FlareInviteAnswer.No, FlareInviteWhy.Dislikes), "flare invitations: an influencer who does not think well of the player: no");

        Check(Is(InviteStranger(0, 50, 500), FlareInviteAnswer.Maybe, FlareInviteWhy.Stranger), "flare invitations: somebody new, hardly talked to: maybe");
        Check(Is(InviteStranger(0, 50, FlareInviteRules.StrangerNo - 1), FlareInviteAnswer.No, FlareInviteWhy.Pass), "flare invitations: somebody new, hardly talked to: no one time in five");
        a = InviteStranger(0, 50, 500); a.Grumpy = true;
        Check(Is(a, FlareInviteAnswer.No, FlareInviteWhy.Pass), "flare invitations: somebody new and grumpy, hardly talked to: no");
        bool never = true;
        for (int roll = 0; roll < 1000; roll++)
        {
            for (int chats = 0; chats < FlareInviteRules.ChatsForYes; chats++)
            {
                never &= FlareInviteRules.Decide(InviteStranger(chats, FlareInviteRules.StandingForYes - 1, roll), out _) != FlareInviteAnswer.Yes;
            }
        }
        Check(never, "flare invitations: somebody never met is maybe at most, whatever the roll, until they have written " + FlareInviteRules.ChatsForYes
                     + " times or the player's standing is " + FlareInviteRules.StandingForYes);
        Check(Is(InviteStranger(FlareInviteRules.ChatsForYes, 50, 100), FlareInviteAnswer.Yes, FlareInviteWhy.Yes), "flare invitations: somebody new talked with a few times: yes");
        Check(Is(InviteStranger(0, FlareInviteRules.StandingForYes, 100), FlareInviteAnswer.Yes, FlareInviteWhy.Yes), "flare invitations: somebody new, the player's name liked in the city: yes");
        Check(Is(InviteStranger(5, 50, FlareInviteRules.StrangerYes), FlareInviteAnswer.Maybe, FlareInviteWhy.Stranger), "flare invitations: somebody new who may say yes still says maybe one time in four");
        a = InviteStranger(5, 50, FlareInviteRules.GrumpyYes - 1); a.Grumpy = true;
        Check(Is(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes), "flare invitations: a grumpy one says yes two times in five");
        a.Roll = FlareInviteRules.GrumpyYes;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Stranger), "flare invitations: ...and maybe otherwise");
        a = InviteStranger(5, 80, 100); a.Taken = true;
        Check(Is(a, FlareInviteAnswer.No, FlareInviteWhy.Taken), "flare invitations: somebody in a relationship with somebody else says no, however well they know the player");
        a = InviteStranger(5, 80, 100); a.Reputation = FlareInviteRules.DislikeBelow - 1;
        Check(Is(a, FlareInviteAnswer.No, FlareInviteWhy.Dislikes), "flare invitations: somebody new who reads the player's account badly says no");
        a = InviteStranger(5, 80, 100); a.Hour = 1;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Night), "flare invitations: somebody new at one in the morning: maybe");
        a.NightOwl = true;
        Check(Is(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes), "flare invitations: ...a night owl is up at one");
        a = InviteStranger(5, 80, 100); a.NobodyNew = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Stranger), "flare invitations: nobody of the social life left to meet: somebody new says maybe");
        a = InviteStranger(5, 80, 100); a.NeedsMore = true;
        Check(Is(a, FlareInviteAnswer.Maybe, FlareInviteWhy.Later), "flare invitations: drinks with somebody new (an acquaintance once met): maybe");
        Check(FlareInviteRules.Asleep(23, false) && FlareInviteRules.Asleep(7, false) && !FlareInviteRules.Asleep(8, false) && FlareInviteRules.Asleep(9, true) && !FlareInviteRules.Asleep(23, true),
            "flare invitations: the city's people sleep from 23 to 8, night owls from 2 to 10 (as the social life's people)");
        int taken = 0;
        for (int i = 0; i < 400; i++)
        {
            taken += FlareInviteRules.Taken(77, i) ? 1 : 0;
        }
        Check(taken > 60 && taken < 140 && FlareInviteRules.Taken(77, 9) == FlareInviteRules.Taken(77, 9), "flare invitations: about a quarter of the city's people are with somebody, the same each time (" + taken + " of 400)");
    }

    // ---------------------------------------------------------------- timing and the reload

    private static void InvitesTiming()
    {
        bool yesSoon = true, othersAsBefore = true, beforePanel = true, beforeCheck = true;
        for (int seed = 1; seed < 60; seed++)
        {
            for (int dm = 0; dm < 200; dm++)
            {
                long sent = 1440L * 9 + 600 + dm;
                long yes = FlareInviteRules.AnswerAt(sent, seed, dm, true), other = FlareInviteRules.AnswerAt(sent, seed, dm, false);
                yesSoon &= yes - sent >= FlareInviteRules.YesLeast && yes - sent < FlareInviteRules.YesLeast + FlareInviteRules.YesSpread && yes < other;
                othersAsBefore &= other == sent + 30 + FlareRules.Roll(seed, dm, 844) % 90;
                long plan = FlareInviteRules.PlanFrom(yes);
                beforePanel &= FlareInviteRules.BeforePanel(yes, plan) && FlareInviteRules.BeforePanel(yes, plan + 600);
                beforeCheck &= yes <= plan - FlareInviteRules.CheckBefore;
            }
        }
        Check(yesSoon, "flare invitations: a yes comes in ten to thirty minutes, always sooner than any other answer would");
        Check(othersAsBefore, "flare invitations: a maybe or a no comes when an answer always came (half an hour to two hours, the same roll)");
        Check(beforePanel && !FlareInviteRules.BeforePanel(1000, 1059) && FlareInviteRules.BeforePanel(1000, 1060),
            "flare invitations: the plan starts at least two hours after its yes: the yes is read before the reminder and the job panel's line an hour before");
        Check(beforeCheck, "flare invitations: ...and before the moment they may call it off (an hour and a half before), never a call-off in the same breath as the yes");
        Check(FlareInviteRules.PanelBefore == SocialRules.EarlyMinutes, "flare invitations: the panel's hour is the social life's own (SocialRules.EarlyMinutes)");

        // The same message at the same minute on the same save: the same roll, decision and answer, after a reload too.
        int r1 = FlareInviteRules.Roll(4242, 1440L * 12 + 615), r2 = FlareInviteRules.Roll(4242, 1440L * 12 + 615);
        bool spread = false;
        for (int m = 0; m < 50; m++)
        {
            spread |= FlareInviteRules.Roll(4242, 1440L * 12 + m) != r1;
        }
        Check(r1 == r2 && r1 >= 0 && r1 < 1000 && spread, "flare invitations: the roll is the save's seed and the message's minute: the same on a reload, another for another minute");
        FlareInviteAsk a = InviteStranger(5, 60, r1), b = InviteStranger(5, 60, r2);
        FlareInviteAnswer x = FlareInviteRules.Decide(a, out FlareInviteWhy wx), y = FlareInviteRules.Decide(b, out FlareInviteWhy wy);
        Check(x == y && wx == wy && FlareInviteRules.Data(a, x, wx) == FlareInviteRules.Data(b, y, wy) && FlareInviteRules.Key(x, wx, a.Kind) == FlareInviteRules.Key(y, wy, b.Kind),
            "flare invitations: the same ask decides the same and writes the same answer");
    }

    // ---------------------------------------------------------------- booking once

    private static void InvitesOnce()
    {
        FlareInviteAsk a = InviteStranger(5, 60, 100);
        a.Kind = PlanKind.Meal;
        a.Place = "Luigi's ~ Bar=Grill";
        string data = FlareInviteRules.Data(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes);
        var yes = new FlareDm { Id = 7, With = "c6", Name = "Hana Hollis", At = 900, Key = FlareInviteRules.Key(FlareInviteAnswer.Yes, FlareInviteWhy.Yes, PlanKind.Meal), Data = data };
        FlareInviteBook book = FlareInviteRules.BookOf(yes);
        Check(book != null && book.Kind == PlanKind.Meal && book.At == a.PlanAt && book.Street == a.Street && book.Number == a.Number && book.Cast == -1
              && book.First == "Hana" && book.Last == "Hollis" && book.Sex == 1 && book.Age == 30 && book.Place == "Luigi's   Bar Grill",
            "flare invitations: a yes keeps its plan in the message (kind, minute, place, somebody new by name, sex and age), read back whole; a name's '~' and '=' kept out");
        a.Known = true;
        Check(FlareInviteRules.BookOf(new FlareDm { Data = FlareInviteRules.Data(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes) }).Cast == a.Cast,
            "flare invitations: a yes from somebody known books with them");
        Check(FlareInviteRules.BookOf(new FlareDm { Data = "outing=coffee~place=X" }) == null && FlareInviteRules.BookOf(null) == null
              && FlareInviteRules.BookOf(new FlareDm { Data = data.Replace("k=1", "k=9") }) == null,
            "flare invitations: a message without a whole plan books nothing");
        var dms = new List<FlareDm>
        {
            new FlareDm { Id = 6, With = "c6", At = 880, Mine = true, Text = "dinner tonight?" },
            yes,
            new FlareDm { Id = 8, With = "c9", At = 950, Key = FlareInviteRules.Key(FlareInviteAnswer.Maybe, FlareInviteWhy.Stranger, PlanKind.Coffee), Data = "outing=coffee" }
        };
        Check(FlareInviteRules.Waiting(yes) && !FlareInviteRules.Waiting(dms[0]) && !FlareInviteRules.Waiting(dms[2]) && FlareInviteRules.Due(dms) == 900 - FlareInviteRules.BookAhead,
            "flare invitations: only a yes waits to be booked, from a moment before it shows");
        Check(FlareInviteRules.WaitingWith(dms, "c6") == yes && FlareInviteRules.WaitingWith(dms, "c9") == null && FlareInviteRules.WaitingWith(dms, null) == yes,
            "flare invitations: a yes on its way is found by its account: a second invitation to them points to it, one to anybody else is busy");
        yes.Data += FlareInviteRules.BookedMark;   // (what the director does once it is booked)
        Check(!FlareInviteRules.Waiting(yes) && FlareInviteRules.Booked(yes) && FlareInviteRules.Due(dms) == long.MaxValue && FlareInviteRules.BookOf(yes) != null,
            "flare invitations: once booked a yes is marked in the save and never booked again; nothing else is due");
        var late = new FlareDm { Id = 9, With = "c3", At = 1000, Key = FlareInviteRules.Key(FlareInviteAnswer.Yes, FlareInviteWhy.Yes, PlanKind.Walk), Data = data };
        string wordsKey = FlareTextsMap.DmKey(late);
        FlareInviteRules.Unbook(late);
        Check(!FlareInviteRules.Waiting(late) && late.Key == FlareInviteRules.KeyStart + "maybe_busy" && late.At == 1001 && FlareTextsMap.DmKey(late) != wordsKey,
            "flare invitations: a yes that can no longer be booked shows as a maybe, under another key than the yes's words, and is never looked at again");
        var chats = new List<FlareDm>
        {
            new FlareDm { With = "c6", At = 100, Key = "reallife_flare_dm_chat" }, new FlareDm { With = "c6", At = 110, Mine = true, Text = "hi" },
            new FlareDm { With = "c6", At = 150, Key = "reallife_flare_dm_answer_chat" }, new FlareDm { With = "c7", At = 150, Key = "reallife_flare_dm_answer_chat" },
            new FlareDm { With = "c6", At = 300, Key = "reallife_flare_dm_answer_chat" }
        };
        Check(FlareInviteRules.Chats(chats, "c6", 200) == 2 && FlareInviteRules.Chats(chats, "c6", 300) == 3 && FlareInviteRules.Chats(chats, "c8", 300) == 0,
            "flare invitations: how much somebody has talked is their messages shown so far, not the player's, not another's, not one still to come");
    }

    // ---------------------------------------------------------------- the answer's key, intent and card

    private static void InvitesWords()
    {
        string[] kinds = { "coffee", "meal", "drinks", "cinema", "walk", "workout" };
        PlanKind[] plans = { PlanKind.Coffee, PlanKind.Meal, PlanKind.Drinks, PlanKind.Cinema, PlanKind.Walk, PlanKind.Workout };
        bool yesKeys = true;
        for (int k = 0; k < kinds.Length; k++)
        {
            string key = FlareInviteRules.Key(FlareInviteAnswer.Yes, FlareInviteWhy.Yes, plans[k]);
            yesKeys &= key == FlareInviteRules.KeyStart + "yes_" + kinds[k] && FlareInviteRules.Intent(key) == "citizen.dm.invite_yes"
                       && FlareInviteRules.AnswerOf(key, out FlareInviteAnswer a) && a == FlareInviteAnswer.Yes;
        }
        Check(yesKeys, "flare invitations: a yes's line is its outing's; its intent citizen.dm.invite_yes");
        bool maybes = true, nos = true;
        foreach (FlareInviteWhy why in Enum.GetValues(typeof(FlareInviteWhy)))
        {
            string m = FlareInviteRules.Key(FlareInviteAnswer.Maybe, why, PlanKind.Coffee), n = FlareInviteRules.Key(FlareInviteAnswer.No, why, PlanKind.Coffee);
            maybes &= m.StartsWith(FlareInviteRules.KeyStart + "maybe_", StringComparison.Ordinal) && FlareInviteRules.Keys().Contains(m)
                      && FlareInviteRules.Intent(m) == "citizen.dm.invite_maybe";
            nos &= n.StartsWith(FlareInviteRules.KeyStart + "no_", StringComparison.Ordinal) && FlareInviteRules.Keys().Contains(n)
                   && FlareInviteRules.Intent(n) == "citizen.dm.invite_no";
        }
        Check(maybes && nos, "flare invitations: every maybe and no has a line of its reason, and the intents citizen.dm.invite_maybe and _no (an influencer worded as a city person: data v3 has no influencer's)");
        Check(FlareInviteRules.Key(FlareInviteAnswer.Maybe, FlareInviteWhy.Plan, PlanKind.Coffee) == FlareInviteRules.KeyStart + "maybe_plan"
              && FlareInviteRules.Key(FlareInviteAnswer.No, FlareInviteWhy.Taken, PlanKind.Coffee) == FlareInviteRules.KeyStart + "no_taken"
              && FlareInviteRules.Intent("reallife_flare_dm_answer_chat") == null && !FlareInviteRules.AnswerOf(null, out _),
            "flare invitations: the plan on already and a relationship have their own lines; any other answer is not an invitation's");

        // The model: the decided answers are asked only of a pack trained on data v3; a v2 pack keeps every written line.
        bool versions = true;
        foreach (string key in FlareInviteRules.Keys())
        {
            versions &= FlareInviteRules.TrainedIntent(key, TextIntents.DataV2) == null
                        && FlareInviteRules.TrainedIntent(key, TextIntents.DataV3) == (key.EndsWith("maybe_plan", StringComparison.Ordinal) ? null : FlareInviteRules.Intent(key))
                        && (FlareInviteRules.TrainedIntent(key, TextIntents.DataV3) == null || TextIntents.Find(FlareInviteRules.TrainedIntent(key, TextIntents.DataV3)).Data == TextIntents.DataV3);
        }
        Check(versions, "flare invitations: an answer asks the model only of a v3 pack, with an intent the mod registers as data v3's (a maybe for the plan on already keeps its written line)");
        var answer = new FlareDm { With = "c6", At = 990, Key = FlareInviteRules.KeyStart + "yes_coffee", Data = "outing=coffee~t_outing=coffee~place=Bean There~time=Monday 18:00~t_time=Monday 18:00" };
        Check(FlareTextsMap.DmWants(answer, 900, TextIntents.DataV3) && !FlareTextsMap.DmWants(answer, 900, TextIntents.DataV2)
              && FlareTextsMap.Dm(answer.Key, FlareDmSpeaker.Citizen, TextIntents.DataV3) == "citizen.dm.invite_yes" && FlareTextsMap.Dm(answer.Key, FlareDmSpeaker.Citizen, TextIntents.DataV2) == null,
            "flare invitations: an invitation's answer wants the model's words of a v3 pack, never of a v2 pack");

        // The yes card (SPEC_v3): the city person's face with their mood, the outing, the place and the time as facts, the place and the time as must.
        TextIntent yesCard = TextIntents.Find("citizen.dm.invite_yes");
        var who = new TextWho();
        var c = new FlareCitizen { Index = 6, First = "Hana", Last = "Hollis", Handle = "@hana_hollis12", Persona = FlarePersona.Foodie };
        FlareTextsMap.Fill(who, c, "Midtown", 77, "Monday 15:10");
        who.Mood = FlareInviteRules.CardMood(answer.Key, "a bit low");
        var slots = new TextSlots();
        FlareInviteRules.Slots(answer, slots);
        var sheet = new TextSheet();
        bool built = yesCard != null && TextIntents.Build(yesCard, who, slots, new List<TextSaid> { new TextSaid("Alex", "you up for a coffee?") }, sheet);
        Check(built && sheet.Facts.Count == 3 && sheet.Facts[0] == "outing: coffee" && sheet.Facts[1] == "place: Bean There" && sheet.Facts[2] == "time: Monday 18:00"
              && sheet.Must.Count == 2 && sheet.Must[0] == "Bean There" && sheet.Must[1] == "Monday 18:00" && sheet.Write == "flare dm reply"
              && sheet.Who == "Hana (@hana_hollis12), " + FlareTextsMap.Age(c, 77) + ", lives in Midtown" && sheet.Role == "a person on Flare you messaged" && sheet.Mood == "fine"
              && sheet.News == "Hana says yes to your invitation; the plan is booked",
            "flare invitations: the yes card has the outing, the place and the time ('Monday 18:00') as facts, the place and the time as must, the city person's face, a mood fit for a yes");
        string card = built ? TextCard.Card(sheet) : string.Empty;
        Check(card.Contains("outing: coffee") && card.Contains("place: Bean There") && card.Contains("time: Monday 18:00") && card.Contains("Alex: you up for a coffee?") && card.Contains("mood: fine"),
            "flare invitations: the card written out carries them and the player's message");
        var none = new TextSlots();
        none.Set("outing", "coffee");
        Check(!TextIntents.Build(yesCard, who, none, null, new TextSheet()), "flare invitations: a yes card with no place or time is never built (the written line)");
        bool outings = FlareInviteRules.CardOuting(PlanKind.Coffee) == "coffee" && FlareInviteRules.CardOuting(PlanKind.Meal) == "dinner" && FlareInviteRules.CardOuting(PlanKind.Drinks) == "drinks"
                       && FlareInviteRules.CardOuting(PlanKind.Cinema) == "the cinema" && FlareInviteRules.CardOuting(PlanKind.Walk) == "a walk"
                       && FlareInviteRules.CardOuting(PlanKind.Workout) == "a workout";
        Check(outings, "flare invitations: the card names the outing as agreed: coffee, dinner, drinks, the cinema, a walk, a workout");

        // The reasons: each maybe and no by the training's own words.
        string K(string rest) => FlareInviteRules.KeyStart + rest;
        Check(FlareInviteRules.CardReason(K("maybe_stranger"), null) == "they barely know you" && FlareInviteRules.CardReason(K("maybe_influencer"), null) == "they barely know you"
              && FlareInviteRules.CardReason(K("maybe_later"), null) == "they barely know you" && FlareInviteRules.CardReason(K("maybe_night"), null) == "they are busy at the moment"
              && FlareInviteRules.CardReason(K("maybe_busy"), null) == "they are busy at the moment" && FlareInviteRules.CardReason(K("maybe_nowhere"), null) == "they are busy at the moment"
              && FlareInviteRules.CardReason(K("no_taken"), null) == "they are seeing someone" && FlareInviteRules.CardReason(K("no_mood"), null) == "they are too busy"
              && FlareInviteRules.CardReason(K("no_pass"), null) == "they are not interested" && FlareInviteRules.CardReason(K("no_dislikes"), "t_outing=coffee") == "they are not interested"
              && FlareInviteRules.CardReason(K("no_dislikes"), "t_outing=coffee~t_rude=1") == "you were rude to them before"
              && FlareInviteRules.CardReason(K("yes_coffee"), null) == null && FlareInviteRules.CardReason(K("maybe_plan"), null) == null && FlareInviteRules.CardReason("reallife_flare_dm_answer_chat", null) == null,
            "flare invitations: every maybe and no gives the card one of data v3's reasons, word for word (a grudge 'you were rude to them before' only when they remember something)");
        var grudge = new FlareInviteAsk { Kind = PlanKind.Coffee, Known = true, Dislikes = true, Outing = "coffee" };
        FlareInviteAnswer g = FlareInviteRules.Decide(grudge, out FlareInviteWhy gWhy);
        var stranger = new FlareInviteAsk { Kind = PlanKind.Coffee, Standing = 10, Reputation = 50, Outing = "coffee" };
        FlareInviteAnswer s = FlareInviteRules.Decide(stranger, out FlareInviteWhy sWhy);
        Check(g == FlareInviteAnswer.No && gWhy == FlareInviteWhy.Dislikes && FlareInviteRules.Value(FlareInviteRules.Data(grudge, g, gWhy), "t_rude") == "1"
              && s == FlareInviteAnswer.No && sWhy == FlareInviteWhy.Dislikes && FlareInviteRules.Value(FlareInviteRules.Data(stranger, s, sWhy), "t_rude") == null,
            "flare invitations: a no from somebody who remembers something keeps t_rude in its data; a stranger put off by a low name does not");
        Check(FlareInviteRules.CardMood(K("yes_coffee"), "having a rough day") == "fine" && FlareInviteRules.CardMood(K("yes_coffee"), "having a great day") == "having a great day"
              && FlareInviteRules.CardMood(K("no_mood"), "in a good mood") == "having a rough day" && FlareInviteRules.CardMood(K("maybe_busy"), "a bit low") == "a bit low",
            "flare invitations: the card's mood fits the decision (a yes never on a low day, a no for a rough day says so)");
        bool cards = true;
        foreach (string intent in new[] { "citizen.dm.invite_yes", "citizen.dm.invite_maybe", "citizen.dm.invite_no" })
        {
            TextIntent t = TextIntents.Find(intent);
            cards &= t != null && t.Voice == "citizen" && t.Channel == "flare_reply" && t.Use == TextUse.People && t.MaxLen == 160 && t.Face == TextFace.CitizenDmMood && !t.Exclaims;
        }
        Check(cards, "flare invitations: every intent an answer can have is registered (a city person's message reply with their mood, 160 characters, no exclamation mark)");
    }

    // ---------------------------------------------------------------- the written lines

    private static void InvitesTexts()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("flare invitations", langs, codes))
        {
            return;
        }
        var keys = new List<string>(FlareInviteRules.Keys()) { "reallife_flare_note_invite_booked", "reallife_flare_test_invitee", "reallife_flare_test_invitee_done" };
        var lacking = new List<string>();
        foreach (string key in keys)
        {
            string l = LanguagesLacking(langs, codes, key);
            if (l.Length > 0)
            {
                lacking.Add(key + " (" + l + ")");
            }
        }
        Check(lacking.Count == 0, "flare invitations: every line is in every language with the same names in curly brackets (" + string.Join(", ", lacking) + ")");
        bool placeTime = true;
        foreach (string key in FlareInviteRules.Keys())
        {
            if (key.Contains("_yes_") || key.EndsWith("maybe_plan", StringComparison.Ordinal))
            {
                for (int l = 0; l < 3; l++)
                {
                    placeTime &= langs[l][key].Contains("{place}") && langs[l][key].Contains("{time}");
                }
            }
        }
        Check(placeTime, "flare invitations: every yes (and the maybe that points to the plan on) names the place and the time in en, pt and zh-cn, without the model");
        bool plain = true;
        foreach (string key in keys)
        {
            string en = langs[0][key];
            plain &= en.IndexOf('!') < 0 && en.IndexOf('\u2014') < 0 && en.IndexOf('\u2013') < 0 && !en.Contains(" - ");
        }
        Check(plain, "flare invitations: the English lines have no exclamation mark and no dash");
        Check(langs[0]["reallife_flare_note_invite_booked"].Length + 90 <= 240, "flare invitations: the notice of a booking fits the game's toast with its names filled (240 characters)");
        Check(langs[0]["reallife_changes_next"].Contains("Flare message") && langs[0]["reallife_guide_flare"].Contains("Invite somebody out in a message")
              && langs[0]["reallife_guide_social"].Contains("An invitation in a Flare message books an outing"),
            "flare invitations: What's new and the Guide's Flare and Social pages say an invitation in a Flare message can book an outing");
    }

    // Every one of the city's people is met as who they are (a city person who says yes joins People of their own sex).
    private static void InvitesNames()
    {
        var people = new List<FlareCitizen>();
        bool own = true;
        int women = 0, all = 0;
        for (int seed = 1; seed < 80; seed++)
        {
            FlareLiveRules.CitizensOf(seed * 7919, null, people);
            for (int i = 0; i < people.Count; i++)
            {
                int sex = FlareInviteRules.SexOf(people[i]);
                own &= sex == (FlareLiveRules.CitizenFemale(seed * 7919, i) ? 1 : 0) && sex == (FlareAvatarRules.Citizen(seed * 7919, i).Female ? 1 : 0);
                women += sex;
                all++;
            }
        }
        Check(own && women > all / 3 && women < all * 2 / 3, "flare invitations: each of the city's people is met with their own sex, the one their card and face have");
        Check(TextsSource(@"Social\Flare\FlareDirector.Invites.cs").Contains("a.Sex = FlareInviteRules.SexOf(c);"), "flare invitations: the invitee's sex is the city person's own");
    }

    // ---------------------------------------------------------------- source guarantees

    private static void InvitesSources()
    {
        string invites = TextsSource(@"Social\Flare\FlareDirector.Invites.cs");
        string links = TextsSource(@"Wiring\FlareInviteLinks.cs");
        string live = TextsSource(@"Social\Flare\FlareDirector.Live.cs");
        string director = TextsSource(@"Social\Flare\FlareDirector.cs");
        string phone = TextsSource(@"Social\SocialDialogs.cs");
        string flareLinks = TextsSource(@"Wiring\FlareLinks.cs");
        Check(links.Contains("p.Origin = SocialOrigin.Online;") && links.Contains("SocialLife.Meet(p, true);") && flareLinks.Contains("stranger.Origin = SocialOrigin.Online;")
              && flareLinks.Contains("SocialLife.Meet(stranger, true);") && links.Contains("ContactPictures.Person(SocialPhone.IdOf(p)"),
            "flare invitations: somebody new who says yes is met online as when they answer a post (SocialOrigin.Online, SocialLife.Meet), with their face under their name");
        Check(links.Contains("SocialLife.MakePlan(p, b.Kind, b.At, venue, false, out string reason)") && phone.Contains("SocialLife.MakePlan(_p, kind, time, venue, theirIdea, out string reason)")
              && links.Contains("SocialPlaces.Find(a.Kind, SocialDirector.Where(), PlaceFrom, PlaceTo") && phone.Contains("SocialPlaces.Find(kind, SocialDirector.Where(), 40f, 1800f")
              && links.Contains("SocialLife.NextSlot(p, a.Kind, a.From)") && links.Contains("PlaceFrom = 40f") && links.Contains("PlaceTo = 1800f"),
            "flare invitations: the place, the hour and the plan are found and made exactly as a call makes them (its reminder, panel line, marker, late and stood-up rules follow)");
        Check(links.Contains("SocialState.Plan.Active") && links.Contains("Known(b.First, b.Last) ?? Stranger(b.Sex)") && links.Contains("SocialDirector.Expected(p)")
              && links.Contains("FlareCast.IsOne(file, p.Index)"),
            "flare invitations: a booking never makes a second plan, finds somebody met meanwhile under their name, and never takes the person a friend is bringing or a star");
        Check(invites.Contains("m.Data += FlareInviteRules.BookedMark;") && invites.Contains("FlareInviteRules.Unbook(m);") && invites.Contains("if (FlareInviteRules.Waiting(m) &&"),
            "flare invitations: the director books only a yes still waiting and marks it either way (never twice)");
        int shared = live.IndexOf("Shared_Dm(with.Substring(2), text);", StringComparison.Ordinal), invite = live.IndexOf("Invite(with, text, now)", StringComparison.Ordinal);
        Check(shared > 0 && invite > shared && !invites.Contains("Shared_") && !invites.Contains("FlareNetShare") && !links.Contains("GoingPublic") && !invites.Contains("GoingPublic"),
            "flare invitations: Going Public: a message to another player is sent as typed; an invitation to the city's people is the player's own: nothing sent, nothing shared");
        Check(!invites.Contains("void Update") && !invites.Contains("LateUpdate") && !links.Contains("void Update") && director.Contains("InvitesTick();")
              && invites.Contains("if (_inviteDue == long.MaxValue || Now < _inviteDue)"),
            "flare invitations: nothing per frame: one comparison on Flare's one-second timer while no yes is due");
        Check(TextsSource(@"Social\Flare\FlareDirector.Texts.cs").Contains("InviteCard(answer, data, r);") && TextsSource(@"Social\Flare\FlareTextsMap.cs").Contains("FlareInviteRules.TrainedIntent(key, data)"),
            "flare invitations: RLO Texts asks an invitation's answer with its own intent and values");
        Check(TextsSource(@"Core\RealLifeOverhaulMod.cs").Contains("reallife_flare_test_invitee") && invites.Contains("public void TestInvitee()"),
            "flare invitations: the Tests page has an entry for somebody talked with, to invite out without waiting");
    }
}
