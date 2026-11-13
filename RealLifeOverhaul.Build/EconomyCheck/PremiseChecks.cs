using System;
using System.Collections.Generic;
using IoPath = System.IO.Path;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;
using PopOwner = RealLifeOverhaul.Population.Owner;

// Three reports of one kind: the mod spoke of what was not there, or stood something where it must not be. A player with no
// business was warned every few days that a rival was talking down a business of theirs, and asked by Mom, again and again,
// for a tour of it; another found a chess table on a crossing in the road. These checks hold the rules that mend them (a
// rival's attack needs a target, the tour needs a business and is asked once, nobody texts the line they texted last, a
// standing site is on pavement and nothing else), and read every sent line's English for what it takes for granted, so a
// new line cannot speak of a business, staff, a partner, children, a job or shares without saying what holds it.
// EconomyCheck --premises runs them alone.
internal static partial class Program
{
    private static void PremiseChecks()
    {
        RivalAttackChecks();
        ReviewWarnChecks();
        TourChecks();
        AskSaveChecks();
        NoRepeatChecks();
        SiteGroundChecks();
        PremiseSweep();
        PremiseTexts();
    }

    // ---------------------------------------------------------------- A: the rival's attack

    private static void RivalAttackChecks()
    {
        byte city = (byte)PopOwner.City, mine = (byte)PopOwner.Player, rival = (byte)PopOwner.Rival, guest = (byte)PopOwner.Guest;
        // A city with no business of the player's: slots 0 to 3 in district 1, 4 and 5 in district 2, 6 another player's in district 1.
        byte[] owner = { city, rival, city, city, city, rival, guest };
        byte[] district = { 1, 1, 1, 1, 2, 2, 1 };
        string[] key = { "A|1", "A|2", "A|3", "A|4", "B|1", "B|2", "A|9" };
        bool never = true, neverGuest = true, sameDistrict = true, neverSelf = true, seeded = true;
        var reached = new HashSet<int>();
        int attacks = 0;
        for (int day = 1; day <= 600; day++)
        {
            int rolled = FlareUseRules.RivalMove(77, day);
            int actor = day % 6;   // never the guest's slot
            int target = rolled == FlareUseRules.RivalAttack ? FlareUseRules.RivalTarget(77, day, actor, owner, district, key, key.Length) : -1;
            seeded &= target == (rolled == FlareUseRules.RivalAttack ? FlareUseRules.RivalTarget(77, day, actor, owner, district, key, key.Length) : -1);
            if (target >= 0)
            {
                attacks++;
                reached.Add(target);
                neverGuest &= owner[target] != guest;
                sameDistrict &= district[target] == district[actor];
                neverSelf &= target != actor;
            }
            bool players = target >= 0 && owner[target] == mine;
            int move = FlareUseRules.RivalMoveFor(rolled, target >= 0 && !players);
            never &= !FlareUseRules.RivalTold(move, players, true);
        }
        Check(never && attacks > 100, "rival's attack: over 600 days in a city where the player has no business, no notice of an attack is ever told (" + attacks + " attacks, all on the city's own)");
        Check(neverGuest && sameDistrict && neverSelf && seeded && reached.Count == 6,
            "rival's attack: its target is a business of the attacker's own district, never itself and never another player's, the same for the same day, and every neighbour is reached in time ("
            + reached.Count + " of 6)");

        // The player's business is drawn like anybody's; one that is theirs no longer is no target.
        owner[2] = mine;
        int onMine = 0, onOthers = 0;
        for (int day = 1; day <= 600; day++)
        {
            int target = FlareUseRules.RivalTarget(77, day, 0, owner, district, key, key.Length);
            onMine += target == 2 ? 1 : 0;
            onOthers += target == 1 || target == 3 ? 1 : 0;
        }
        Check(onMine > 120 && onOthers > 240, "rival's attack: the player's business is run down by the rule its neighbours are (" + onMine + " days of 600 on theirs, " + onOthers + " on the two beside it)");
        Check(FlareUseRules.RivalMoveFor(FlareUseRules.RivalAttack, false) == 0 && FlareUseRules.RivalMoveFor(FlareUseRules.RivalAttack, true) == FlareUseRules.RivalAttack
              && FlareUseRules.RivalMoveFor(0, false) == 0 && FlareUseRules.RivalMoveFor(1, false) == 1,
            "rival's attack: with nobody to run down the day's move is an ordinary post; a post and a promotion are as they were");
        Check(FlareUseRules.RivalTold(FlareUseRules.RivalAttack, true, true) && !FlareUseRules.RivalTold(FlareUseRules.RivalAttack, false, true)
              && !FlareUseRules.RivalTold(FlareUseRules.RivalAttack, true, false) && !FlareUseRules.RivalTold(0, true, true) && !FlareUseRules.RivalTold(1, true, true),
            "rival's attack: the player is told only of an attack on a business of theirs whose post and message went up");
        byte[] alone = { city, city };
        byte[] apart = { 1, 2 };
        string[] two = { "A|1", "B|1" };
        Check(FlareUseRules.RivalTarget(5, 9, 0, alone, apart, two, 2) == -1 && FlareUseRules.RivalTarget(5, 9, 5, alone, apart, two, 2) == -1
              && FlareUseRules.RivalTarget(5, 9, 0, null, apart, two, 2) == -1 && FlareUseRules.RivalTarget(5, 9, 0, new[] { city, guest }, new byte[] { 1, 1 }, two, 2) == -1
              && FlareUseRules.RivalTarget(5, 9, 0, new[] { city, city }, new byte[] { 1, 1 }, new[] { "A|1", null }, 2) == -1,
            "rival's attack: no neighbour, a slot off the table, no table, only another player's beside it, or a slot with no building: no target");

        // The message it leaves: unanswered until the player writes back, and it names the business.
        var net = new FlareNet();
        FlareLiveRules.Dm(net, "k:Speedy Bites", "Speedy Bites", FlareLiveRules.Now(28, 9, 30), false, null, FlareLiveRules.RivalDmKey, "business=Bloom Cafe");
        bool open = FlareLiveRules.Attacked(net, "k:Speedy Bites") && FlareLiveRules.AttackedBusiness(net, "k:Speedy Bites") == "Bloom Cafe"
                    && !FlareLiveRules.Attacked(net, "k:Burger Barn") && !FlareLiveRules.Attacked(net, null) && !FlareLiveRules.Attacked(null, "k:Speedy Bites");
        FlareLiveRules.Dm(net, "k:Speedy Bites", "Speedy Bites", FlareLiveRules.Now(28, 10, 0), true, "The door is open.", null, null);
        bool answered = !FlareLiveRules.Attacked(net, "k:Speedy Bites");
        FlareLiveRules.Dm(net, "k:Speedy Bites", "Speedy Bites", FlareLiveRules.Now(31, 9, 30), false, null, FlareLiveRules.RivalDmKey, "other=1~business=Bloom Two");
        Check(open && answered && FlareLiveRules.AttackedBusiness(net, "k:Speedy Bites") == "Bloom Two",
            "rival's attack: its message stands unanswered until the player writes back in that conversation, names the business, and a later attack opens it again");

        string use = ModSource(@"Social\Flare\FlareDirector.Use.cs");
        string day0 = MemberText(use, "private void RivalDay(int day, int hour)");
        Check(!use.Contains("PlayerPlace()") && !use.Contains("_file.Shops[0]") && !day0.Contains("Tell(NotificationType.Warning, \"reallife_flare_use_attack\"")
              && day0.Contains("FlareUseRules.RivalTarget(_city.Seed, day, actor, s.Businesses.Owner, s.Businesses.District, s.Businesses.Key, s.Businesses.Count)")
              && day0.Contains("bool owned = players && ShopOfSlot(target, out mine);") && day0.Contains("FlareUseRules.RivalMoveFor(rolled, target >= 0 && (!players || owned))")
              && day0.Contains("if (FlareUseRules.RivalTold(move, owned, posted))")
              && day0.Contains("Notice(NotificationType.Warning, \"reallife_flare_use_attack\", _openMessages, \"rival\", FlareCityGame.NameOf(place), \"business\", mine.Name);"),
            "rival's attack: the day asks the rule for its target, takes a business of the player's only as it is theirs today (never the first shop the account ever saw), and tells it through the Flare notices' switch, named, opening the messages");
        Check(day0.Contains("if (!CityOn || _city == null || _city.RivalDay == day)") && day0.Contains("_city.RivalDay = day;") && Occurs(use, "\"reallife_flare_use_attack\"") == 1,
            "rival's attack: a rival's day is played once, so its notice comes once for a post, from one place in the code");
        string kit = ModSource(@"Social\Flare\FlareDirector.Kit.cs");
        string attack = MemberText(kit, "private bool RivalAttack(string name, string district, FlareShop mine, int day, int hour)");
        string words = ModSource(@"Social\Flare\FlareText.Live.cs");
        Check(attack.Contains("string.IsNullOrEmpty(mine.Place)") && attack.Contains("Text = Clean(mine.Name)") && attack.Contains("FlareLiveRules.RivalDmKey") && attack.Contains("return true;")
              && words.Contains("if (p.Arg == FlareDirector.RivalAttackArg && !string.IsNullOrEmpty(p.Text))") && words.Contains("Fill(RivalAttackKey, \"name\", p.Name, \"business\", p.Text, \"district\", district)"),
            "rival's attack: its post names the business it runs down, and its message does");
        string posting = ModSource(@"Social\Flare\FlareDirector.Posting.cs");
        string send = MemberText(posting, "public bool SendDm(string with, string text)");
        Check(send.IndexOf("string attacked = AttackedPlace(with);", StringComparison.Ordinal) > 0
              && send.IndexOf("AnswerRival(attacked, text);", StringComparison.Ordinal) < send.IndexOf("FlareLiveRules.Dm(_file.Net, with, AccountName(with), now, true, text, null, null);", StringComparison.Ordinal)
              && MemberText(use, "public bool AnswerRival(string place, string text)").Contains("FlareGame.Business(place) == null")
              && ModSource(@"Ui\Flare\FlareScreen.Sheet.cs").Contains("FlareLiveRules.Attacked(file.Net, with)"),
            "rival's attack: an answer in the rival's conversation goes up for the city from either Flare, for a business the player still has");
    }

    private static void ReviewWarnChecks()
    {
        Check(FlareUseRules.WarnsPlayer((byte)PopOwner.Player) && !FlareUseRules.WarnsPlayer((byte)PopOwner.City) && !FlareUseRules.WarnsPlayer((byte)PopOwner.Rival)
              && !FlareUseRules.WarnsPlayer((byte)PopOwner.Guest),
            "reviews turned: only a business of the player's own marks the warning, never the city's, a rival's or another player's");
        string reviews = ModSource(@"Social\Flare\City\FlareCityRules.Reviews.cs");
        string use = MemberText(ModSource(@"Social\Flare\FlareDirector.Use.cs"), "private void UseHour(int day, int hour)");
        Check(reviews.Contains("&& FlareUseRules.WarnsPlayer(bt.Owner[b]))") && use.Contains("_city.WarnKey = null;") && use.Contains("if (FlareGame.Business(warned) != null)")
              && use.Contains("Notice(NotificationType.Warning, \"reallife_flare_use_turn\", _openBusinessTab);") && !use.Contains("Tell(NotificationType.Warning, \"reallife_flare_use_turn\""),
            "reviews turned: the mark is set for the player's business alone, cleared when it is read, and told once, of a business they still have");
    }

    // ---------------------------------------------------------------- B: the tour, and the same line twice

    private static void TourChecks()
    {
        bool none = true;
        for (int day = 1; day <= 400; day++)
        {
            none &= !SocialAskRules.MayAskTour(false, 0, 0, 0, day) && !SocialAskRules.MayAskTour(false, 300, 0, 0, day);
        }
        Check(none, "the tour: nobody asks for a tour of a business the player has not got, on any day");
        Check(!SocialAskRules.MayAskTour(true, 0, 0, 0, 9) && !SocialAskRules.MayAskTour(true, SocialAskRules.TourHeardDays - 1, 0, 0, 9)
              && SocialAskRules.MayAskTour(true, SocialAskRules.TourHeardDays, 0, 0, 9),
            "the tour: asked only once the business has been theirs long enough for word of it to get round (" + SocialAskRules.TourHeardDays + " days)");
        int asks = 0, askedDay = 0;
        for (int day = 10; day < 10 + 3 * SocialAskRules.TourAgainDays; day++)
        {
            if (SocialAskRules.MayAskTour(true, 50, askedDay, 0, day))
            {
                asks++;
                askedDay = day;
            }
        }
        Check(asks == 3 && !SocialAskRules.MayAskTour(true, 50, 10, 0, 10 + SocialAskRules.TourAgainDays - 1) && SocialAskRules.MayAskTour(true, 50, 10, 0, 10 + SocialAskRules.TourAgainDays),
            "the tour: unanswered or turned down, the same person asks again only after " + SocialAskRules.TourAgainDays + " days (" + asks + " asks in " + 3 * SocialAskRules.TourAgainDays + " days)");
        Check(!SocialAskRules.MayAskTour(true, 50, 0, 12, 13) && !SocialAskRules.MayAskTour(true, 500, 12, 12, 400),
            "the tour: somebody shown round does not ask when they get the tour again");
        Check(SocialAskRules.BusinessKind == (int)PlanKind.Business && SocialAskRules.FallbackKind == (int)PlanKind.Coffee
              && SocialAskRules.InviteKind((int)PlanKind.Business, false) == (int)PlanKind.Coffee && SocialAskRules.InviteKind((int)PlanKind.Business, true) == (int)PlanKind.Business
              && SocialAskRules.InviteKind((int)PlanKind.Meal, false) == (int)PlanKind.Meal && !SocialAskRules.MayWish((int)PlanKind.Business, false)
              && SocialAskRules.MayWish((int)PlanKind.Business, true) && SocialAskRules.MayWish((int)PlanKind.Walk, false),
            "the tour: somebody whose wish it is asks for a coffee while it may not be asked, and nobody wishes for it of a player with no business");
        Check(LineNeeds.Of("reallife_social_invite_business") == LineNeed.Business, "the tour: its line needs a business in Core/LineNeeds");

        string contact = ModSource(@"Social\SocialDirector.Contact.cs");
        string invite = MemberText(contact, "private void Invite(SocialPerson person, float real)");
        string may = MemberText(contact, "private static bool MayAskTour(SocialPerson person)");
        Check(invite.Contains("var kind = (PlanKind)SocialAskRules.InviteKind((int)person.Wants, MayAskTour(person));") && invite.Contains("person.TourAskDay = SocialLife.Day;")
              && invite.Contains("person.TourAskDay = askedBefore;") && may.Contains("int held = SocialPlaces.BusinessHeldDays(SocialLife.Day);")
              && may.Contains("SocialAskRules.MayAskTour(held >= 0, held, person.TourAskDay, person.TourDay, SocialLife.Day)")
              && MemberText(contact, "private void Daily(int day)").Contains("!SocialAskRules.MayWish(k, business)"),
            "the tour: the invitation asks the rule, with the player's businesses read from the game, and keeps the day it was asked; the day's wishes leave it out with no business");
        string places = MemberText(ModSource(@"Social\SocialPlaces.cs"), "public static int BusinessHeldDays(int day)");
        Check(places.Contains("Economy.Books.Businesses(Own);") && places.Contains("FoundingGame.Of(Own[i])") && places.Contains("held = -1;"),
            "the tour: a business is the player's own by Finance's test (theirs alone in a Going Public session), with the day it began; what cannot be read is not there");
        string dialog = ModSource(@"Social\PersonDialog.cs");
        Check(MemberText(dialog, "private DialogEntry Asked()").Contains("tour ? 0f : 40f, tour ? TourReach : 1800f")
              && ModSource(@"Social\OutingScene.After.cs").Contains("_who.TourDay = SocialLife.Day;"),
            "the tour: ringing them finds the player's business wherever they ring from, and the tour itself marks them shown round");
        string state = ModSource(@"Social\SocialState.cs");
        Check(state.Contains("text.Append(Field).Append(Clean(p.LastLine)).Append(Field).Append(p.TourAskDay).Append(Field).Append(p.TourDay);")
              && MemberText(state, "private static void ReadAsks(SocialPerson p, string[] f)").Contains("if (f.Length < 51)")
              && state.IndexOf("ReadFollow(p, f);", StringComparison.Ordinal) < state.IndexOf("ReadAsks(p, f);", StringComparison.Ordinal),
            "the tour: the line sent last and the tour's two days are kept in the save after the older fields, and a save without them still loads");
    }

    // The save round trip: the line sent last and the tour's two days come back as saved; a record of 48 fields (from before
    // them) loads with the tour neither asked for nor given and any line free to come first; a damaged day is let go.
    private static void AskSaveChecks()
    {
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 30, 112, 9, 4747);
        SocialPerson p = SocialState.Get(2);
        p.Met = true;
        p.LastLine = "reallife_social_text_home_2";
        p.TourAskDay = 41;
        p.TourDay = 44;
        SocialState.Save(data);
        string[] records = data["reallife:social_people"].Split('\n');
        SocialState.Load(data, 31, 112, 9, 1);
        SocialPerson back = SocialState.Get(2);
        Check(back != null && back.LastLine == "reallife_social_text_home_2" && back.TourAskDay == 41 && back.TourDay == 44,
            "the tour: the line sent last and the tour's two days are read back as saved");
        var older = new string[records.Length];
        var damaged = new string[records.Length];
        for (int i = 0; i < records.Length; i++)
        {
            string[] g = records[i].Split('|');
            older[i] = string.Join("|", g, 0, 48);
            g[49] = "-7";
            g[50] = "x";
            damaged[i] = string.Join("|", g);
        }
        data["reallife:social_people"] = string.Join("\n", older);
        SocialState.Load(data, 31, 112, 9, 1);
        SocialPerson old = SocialState.Get(2);
        bool olderRead = old != null && old.LastLine == null && old.TourAskDay == 0 && old.TourDay == 0 && SocialState.Get(0) != null && SocialState.Get(0).Name == "Mom";
        data["reallife:social_people"] = string.Join("\n", damaged);
        SocialState.Load(data, 31, 112, 9, 1);
        SocialPerson hurt = SocialState.Get(2);
        Check(olderRead && hurt != null && hurt.TourAskDay == 0 && hurt.TourDay == 0 && hurt.LastLine == "reallife_social_text_home_2",
            "the tour: a save from before those fields loads with the tour neither asked for nor given, and a damaged day is let go");
    }

    private static void NoRepeatChecks()
    {
        string[][] pools =
        {
            new[] { "invite_only" }, new[] { "a1", "a2" }, new[] { "b1", "b2", "b3" }, new[] { "c1", "c2", "c3", "c4" }, new[] { "report_only" }
        };
        var rng = new Random(1311);
        string last = null;
        int sent = 0, held = 0, twice = 0, reported = 0;
        for (int i = 0; i < 20000; i++)
        {
            string[] pool = pools[rng.Next(pools.Length)];
            string key = pool[rng.Next(pool.Length)];
            bool must = ReferenceEquals(pool, pools[4]);
            string line = SocialAskRules.Next(last, key, pool, must, rng.Next(), null);
            if (line == null)
            {
                held++;
                continue;
            }
            if (line == last)
            {
                twice += must ? 0 : 1;
                reported += must ? 1 : 0;
            }
            sent++;
            last = line;
        }
        Check(twice == 0 && sent > 15000 && held > 100 && reported > 100,
            "no line twice running: over 20,000 texts from one person, of their own accord they never send the line they sent last (" + sent + " sent, " + held
            + " held back with no other line to send; a report of something that happened is sent again, " + reported + " times)");
        Func<string, bool> onlyFirst = line => line == "a1";
        Check(SocialAskRules.Next("a1", "a1", pools[1], false, 0, onlyFirst) == null && SocialAskRules.Next("a1", "a1", pools[1], true, 0, onlyFirst) == "a1"
              && SocialAskRules.Next("a2", "a2", pools[1], false, 7, onlyFirst) == "a1" && SocialAskRules.Next(null, "a1", null, false, 0, null) == "a1"
              && SocialAskRules.Next("a1", null, pools[1], false, 0, null) == null && SocialAskRules.Next("x", "invite_only", null, false, 0, null) == "invite_only"
              && SocialAskRules.Next("invite_only", "invite_only", null, false, 0, null) == null,
            "no line twice running: the line sent instead is one that fits the player; with none, nothing goes, but a report does; an invitation's one line is not sent twice running");
        var seen = new HashSet<string>();
        for (int roll = 0; roll < 64; roll++)
        {
            seen.Add(SocialAskRules.Next("c1", "c1", pools[3], false, roll, null));
        }
        Check(seen.Count == 3 && !seen.Contains("c1") && SocialAskRules.Next("c1", "c1", pools[3], false, int.MinValue, null) != null,
            "no line twice running: every other line of the pool can be the one sent instead, whatever the roll");

        // A friend's comments under the player's posts: the walk FlareDirector.FriendLine does, post by post.
        bool repeated = false;
        var variants = new HashSet<int>();
        for (int person = 0; person < 12; person++)
        {
            int before = -1;
            for (int post = 1; post <= 400; post++)
            {
                int line = FlareRules.ReplyLine(FlareRules.ReplyRoll(post * 1009 + 17, person), 3, before);
                repeated |= line == before || line < 0 || line > 2;
                variants.Add(line);
                before = line;
            }
        }
        Check(!repeated && variants.Count == 3 && FlareRules.ReplyLine(FlareRules.ReplyRoll(5, 1), 3, -1) == FlareRules.ReplyRoll(5, 1) % 3 && FlareRules.ReplyLine(int.MinValue, 3, -1) >= 0
              && FlareRules.ReplyLine(7, 1, 0) == 0 && FlareRules.ReplyLine(7, 0, 0) == 0,
            "no line twice running: over 400 posts a friend never comments the same words twice running under the player's posts, every line of the kind is used, and a post with no earlier comment gets the line it always did");
        Check(FlareLiveRules.Replied("R:f3:95;L:f2:5", 3) && !FlareLiveRules.Replied("R:f3:95;L:f2:5", 2) && FlareLiveRules.Replied("L:f3:12;R:p0:7;L:c7:95", 0)
              && !FlareLiveRules.Replied("L:f3:12;R:p0:7;S:c7:95", 7) && !FlareLiveRules.Replied("L:f3:12;R:p0:7", 3) && !FlareLiveRules.Replied("R:f13:5", 1) && !FlareLiveRules.Replied("R:f13:5", 3)
              && FlareLiveRules.Replied("R:f13:5", 13) && FlareLiveRules.Replied("L:s:Amy;R:f30:5", 30) && !FlareLiveRules.Replied("R:c7:95", 7)
              && !FlareLiveRules.Replied(null, 0) && !FlareLiveRules.Replied(string.Empty, 0) && !FlareLiveRules.Replied("R:f3:95", -1) && !FlareLiveRules.Replied("R:f:5", 0) && !FlareLiveRules.Replied("R:f3", 3),
            "no line twice running: which posts a friend or partner replied to is read from the reactors line as the line reads it, citizens and likes left out");
        string direct = ModSource(@"Social\Flare\FlareDirector.Texts.cs");
        string friendLine = MemberText(direct, "public string FriendLine(FlarePost p, int index, FlarePerson person)");
        Check(friendLine.Contains("FlareLiveRules.Replied(q.Reactors, index)") && friendLine.Contains("last = FlareText.ReplyKey(FlareRules.ReplyOf(person, q), q, index, last);")
              && friendLine.Contains("FlareText.T(FlareText.ReplyKey(FlareRules.ReplyOf(person, p), p, index, last))")
              && ModSource(@"Social\Flare\FlareApp.Screens.cs").Contains("Text = words ?? _director.FriendLine(p, index, people[k]),")
              && ModSource(@"Ui\Flare\FlareScreen.Fill.cs").Contains("Text = words ?? _flare.FriendLine(p, index, people[k]),"),
            "no line twice running: both Flare screens draw a friend's written comment through the walk over their earlier comments");

        string life = MemberText(ModSource(@"Social\SocialLife.cs"), "public static bool SendText(SocialPerson person, string key, Dictionary<string, string> data, bool must = false)");
        string lines = ModSource(@"Social\SocialCatalogue.cs");
        Check(life.Contains("key = SocialLines.After(person.LastLine, key, must);") && Occurs(life, "person.LastLine = key;") == 2 && life.IndexOf("if (key == null)", StringComparison.Ordinal) > 0
              && lines.Contains("Pools[pool[index]] = pool;") && lines.Contains("return SocialAskRules.Next(last, key, key != null && Pools.TryGetValue(key, out string[] pool) ? pool : null, must, Dice.Next(), Fits);"),
            "no line twice running: every text of the social life goes through the rule with the line that person sent last, kept on them, and the pool the line was picked from");
    }

    // ---------------------------------------------------------------- C: where a standing site may be

    private static StreetGroundFacts Ground(bool walk = true, bool crossing = false, bool road = false, float lane = -1f, bool bus = false, bool parking = false, bool door = false, float reach = 0.6f)
    {
        return new StreetGroundFacts
        {
            Walkable = walk, CrossingUnder = crossing, RoadUnder = road, LaneKnown = lane >= 0f, LaneDistance = lane, BusZone = bus, ParkingUnder = parking, DoorNear = door, Reach = reach
        };
    }

    private static void SiteGroundChecks()
    {
        Check(SiteGroundRules.Verdict(Ground()) == StreetGround.Pavement && SiteGroundRules.Stands(Ground(lane: 9f)) && SiteGroundRules.Stands(Ground(lane: SiteGroundRules.LaneHalf + 0.6f)),
            "site ground: pavement is accepted, beside a road too, from a lane's edge and the site's own reach away");
        Check(SiteGroundRules.Verdict(Ground(crossing: true)) == StreetGround.Crossing && SiteGroundRules.Verdict(Ground(crossing: true, road: true, lane: 0.4f)) == StreetGround.Crossing
              && !SiteGroundRules.Stands(Ground(crossing: true, lane: 30f)),
            "site ground: a crossing is refused though it is walking ground, with or without a road read under it, however far the nearest lane point");
        Check(SiteGroundRules.Verdict(Ground(road: true)) == StreetGround.Carriageway && SiteGroundRules.Verdict(Ground(lane: 0f)) == StreetGround.Lane
              && SiteGroundRules.Verdict(Ground(lane: SiteGroundRules.LaneHalf + 0.59f)) == StreetGround.Lane && SiteGroundRules.Verdict(Ground(lane: 1.7f, reach: 0f)) == StreetGround.Pavement
              && SiteGroundRules.Verdict(Ground(lane: 1.6f, reach: 0f)) == StreetGround.Lane,
            "site ground: the road's own surface is refused, and so is anything whose reach comes within a lane's half width (" + SiteGroundRules.LaneHalf + " m) of a lane's line");
        Check(SiteGroundRules.Verdict(Ground(bus: true)) == StreetGround.BusZone && SiteGroundRules.Verdict(Ground(parking: true)) == StreetGround.Driveway
              && SiteGroundRules.Verdict(Ground(door: true)) == StreetGround.Doorway && SiteGroundRules.Verdict(Ground(walk: false)) == StreetGround.OffWalk
              && SiteGroundRules.Verdict(Ground(walk: false, crossing: true, road: true)) == StreetGround.OffWalk,
            "site ground: a bus zone, parking or a driveway, a doorway and ground nobody walks are each refused, by name");
        bool only = true;
        for (int bits = 0; bits < 128; bits++)
        {
            StreetGroundFacts f = Ground((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0 ? 0.5f : -1f, (bits & 16) != 0, (bits & 32) != 0, (bits & 64) != 0);
            only &= SiteGroundRules.Stands(f) == (bits == 1);
        }
        Check(only, "site ground: of every mix of the seven readings only walking ground with nothing else under or beside it is pavement");
        var words = new HashSet<string>();
        foreach (StreetGround g in (StreetGround[])Enum.GetValues(typeof(StreetGround)))
        {
            words.Add(SiteGroundRules.Words(g));
        }
        Check(words.Count == Enum.GetValues(typeof(StreetGround)).Length, "site ground: each answer has its own words for the log line");
        StreetGroundFacts leaning = Ground(road: true, parking: true, lane: 3.4f, reach: 0.25f);
        leaning.Kerbside = true;
        StreetGroundFacts onCrossing = leaning, inZone = leaning, inLane = leaning;
        onCrossing.CrossingUnder = true;
        inZone.BusZone = true;
        inLane.LaneDistance = 1f;
        Check(SiteGroundRules.Kerbside("car_lean") && !SiteGroundRules.Kerbside("chess") && !SiteGroundRules.Kerbside(SiteGroundRules.LeisureChess) && !SiteGroundRules.Kerbside("coffee_cart")
              && SiteGroundRules.Stands(leaning) && SiteGroundRules.Verdict(onCrossing) == StreetGround.Crossing && SiteGroundRules.Verdict(inZone) == StreetGround.BusZone
              && SiteGroundRules.Verdict(inLane) == StreetGround.Lane && SiteGroundRules.Verdict(Ground(road: true, reach: 0.25f)) == StreetGround.Carriageway,
            "site ground: somebody leaning on a parked car stands at the kerb's edge with the parking and the road's edge in reach, and still never on a crossing, in a lane or in a bus zone; nothing else is let near");

        // Every kind of site: the living city's catalogue and the rule's list are the same, and Leisure's table is in it.
        string catalogue = ModSource(@"CityLife\CityCatalogue.cs");
        var ids = new List<string>();
        foreach (Match m in Regex.Matches(catalogue, "Id = \"([a-z_]+)\""))
        {
            ids.Add(m.Groups[1].Value);
        }
        var missing = new List<string>();
        foreach (string id in ids)
        {
            if (!SiteGroundRules.Covers(id))
            {
                missing.Add(id);
            }
        }
        var stale = new List<string>();
        bool reaches = true;
        foreach (string kind in SiteGroundRules.Kinds)
        {
            if (kind != SiteGroundRules.LeisureChess && !ids.Contains(kind))
            {
                stale.Add(kind);
            }
            reaches &= SiteGroundRules.Reach(kind) >= SiteGroundRules.StandReach && SiteGroundRules.Reach(kind) <= 1.5f;
        }
        Check(ids.Count >= 30 && missing.Count == 0 && stale.Count == 0 && reaches && SiteGroundRules.Covers(SiteGroundRules.LeisureChess) && !SiteGroundRules.Covers("nothing")
              && SiteGroundRules.Kinds.Length == new HashSet<string>(SiteGroundRules.Kinds).Count,
            "site ground: every kind of the living city's catalogue is covered by the rule, with Leisure's chess table, each with a reach (" + ids.Count + " kinds"
            + (missing.Count > 0 ? "; not covered: " + string.Join(", ", missing) : string.Empty) + (stale.Count > 0 ? "; no longer there: " + string.Join(", ", stale) : string.Empty) + ")");

        string spots = ModSource(@"CityLife\CitySpots.cs");
        string read = MemberText(spots, "public static StreetGround Read(Vector3 spot, float reach, bool kerbside, bool door = false)");
        Check(MemberText(spots, "public static bool OnPavement(Vector3 place, out Vector3 spot)").Contains("&& (_forSite == 0 || Stands(spot, SiteGroundRules.StandReach)))")
              && MemberText(spots, "public static bool Pavement(Vector3 near, float radius, out Vector3 spot)").Contains("if (_forSite == 0 || Stands(spot, SiteGroundRules.StandReach))")
              && MemberText(spots, "public static bool Stand(Vector3 place, float room, out Vector3 spot)").Contains("return OnPavement(place, out spot) && Clear(spot, room);")
              && MemberText(spots, "public static bool PavementRing(Vector3 centre, float minDistance, float maxDistance, bool unseen, out Vector3 spot)").Contains("if (!Pavement(centre +")
              && MemberText(spots, "public static SiteScope ForSite(string kind)").Contains("_forSite++;") && spots.Contains("_forSite = _forSite > 0 ? _forSite - 1 : 0;"),
            "site ground: while a site finds its places, every one it asks for (a point of pavement, a place to stand, a place on the ring) is walking ground that the rule calls pavement");
        Check(read.Contains("_roadMask, QueryTriggerInteraction.Collide)") && MemberText(spots, "private static void Setup()").Contains("_roadMask = LayerMask.GetMask(\"Roads\", \"Crosswalk\") | _parkingMask;")
              && read.Contains("layer == _crosswalkLayer || hit.CompareTag(\"Crosswalk\")") && read.Contains("!hit.isTrigger && hit.GetComponentInParent<EntityController>() == null")
              && read.Contains("RoadMap.NearestLane(spot, out float lane, out _, out _)") && read.Contains("facts.BusZone = BusZones.Holds(spot, Vector3.forward, reach, reach);")
              && read.Contains("facts.RoadUnder = count >= RoadOverlaps.Length;") && read.Contains("RoadOverlaps[i] = null;") && read.Contains("return SiteGroundRules.Verdict(in facts);")
              && read.Contains("facts.DoorNear = door && Physics.CheckSphere(spot + Vector3.up, DoorKeepOut, _doorMask") && spots.Contains("string kind, bool setUp = false) => Read(centre, SiteGroundRules.Reach(kind), SiteGroundRules.Kerbside(kind), setUp);"),
            "site ground: the ground is read from the game's own road, crossing and parking layers under the footprint (a street tree on the roads' layer is no road), the traffic system's lanes, the bus zones and, as a site is set up, what the player can use, and judged by the rule");
        string director = ModSource(@"CityLife\CityLifeDirector.cs");
        string open = MemberText(director, "private bool Open(CityEntry entry, Vector3 anchor, Vector3 origin, float now, bool raining, bool test)");
        int asked = open.IndexOf("StreetGround ground = CitySpots.Site(centre, entry.Id, true);", StringComparison.Ordinal);
        int scope = open.IndexOf("using CitySpots.SiteScope places = CitySpots.ForSite(entry.Id);", StringComparison.Ordinal);
        Check(asked > 0 && asked < open.IndexOf("bool begun = activity.Begin(_site);", StringComparison.Ordinal) && open.Contains("GroundAt = now + SiteGroundRules.RecheckSeconds")
              && scope > 0 && scope < open.IndexOf("if (!activity.Find(_site))", StringComparison.Ordinal)
              && director.Contains("under = CitySpots.Site(activity.Centre, running.Entry.Id);") && director.Contains("alive = under == StreetGround.Pavement;")
              && director.Contains("\"its place is \" + SiteGroundRules.Words(under) + \" now, not pavement\""),
            "site ground: a site is asked for as a whole before anything of it is put out, and again every " + SiteGroundRules.RecheckSeconds
            + " s while it is out: one found in the road is taken in, with one log line");
        string chess = ModSource(@"Leisure\Activities\ChessGame.cs");
        Check(MemberText(spots, "public static bool ChessClear(Vector3 table, Vector3 along)").Contains("&& Site(table, SiteGroundRules.LeisureChess, true) == StreetGround.Pavement;")
              && chess.Contains("StreetGround under = CitySpots.Site(Table, SiteGroundRules.LeisureChess);") && chess.Contains("is taken in: its place is \"")
              && MemberText(chess, "public bool PutOut(Vector3 anchor, Vector3 player, out TestRefusal why)").Contains("using CitySpots.SiteScope places = CitySpots.ForSite(SiteGroundRules.LeisureChess);")
              && Occurs(chess, "using CitySpots.SiteScope places = CitySpots.ForSite(SiteGroundRules.LeisureChess);") == 2,
            "site ground: both chess scenes ask it of the table and its stools, and the table the player can sit at is looked at again while it is out");
        var loose = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(IoPath.GetDirectoryName(ModSourcePath(@"CityLife\Activities\ChessActivity.cs")), "*.cs"))
        {
            string code = System.IO.File.ReadAllText(file);
            if (code.Contains("public override bool Find(CitySite site)") && !code.Contains("CitySpots."))
            {
                loose.Add(IoPath.GetFileName(file));
            }
        }
        Check(loose.Count == 0, "site ground: every activity that finds its own place asks CitySpots for it" + (loose.Count > 0 ? " (not: " + string.Join(", ", loose) + ")" : string.Empty));
    }

    // ---------------------------------------------------------------- the sweep: what a sent line takes for granted

    // What a line can take for granted of the player, by the words that say so.
    private static readonly KeyValuePair<string, Regex>[] PremiseWords =
    {
        Premise("business", @"\byour (own |little |new |first )?(business|businesses|shop|shops|store|stores|company|firm)\b|\bbusiness(es)? of yours\b|\bshops? of yours\b"),
        Premise("staff", @"\byour (staff|employees?|managers?)\b"),
        Premise("partner", @"\byour (wife|husband|partner|wedding)\b"),
        Premise("children", @"\byour (kids?|child|children|son|daughter|baby)\b"),
        Premise("job", @"\byour (job|boss|employer|shift)\b"),
        Premise("degree", @"\byour (degree|diploma|classes)\b"),
        Premise("followers", @"\byour followers\b"),
        Premise("shares", @"\byour (shares|shareholders|share price)\b")
    };

    // The families of lines the mod sends of its own accord: texts, notices, posts, comments and what people say.
    private static readonly string[] SentFamilies =
    {
        "reallife_ev_", "reallife_family_", "reallife_social_", "reallife_street_", "reallife_flare_use_", "reallife_flare_note_", "reallife_flare_dm_", "reallife_flare_biz_post_",
        "reallife_news_", "reallife_crime_", "reallife_text_", "reallife_season_", "reallife_macro_note_", "reallife_rival_came_", "reallife_biz_", "reallife_fire_", "reallife_hier_note_",
        "reallife_travel_summary_", "reallife_fame_q_", "reallife_econ_note_", "reallife_party_note_", "reallife_career_note_", "reallife_household_note_", "reallife_roommate_",
        "reallife_dealer_note_", "reallife_estate_note_", "reallife_vc_note_", "reallife_exit_note_", "reallife_hc_note_", "reallife_life_note_", "reallife_own_note_", "reallife_job_end_",
        "reallife_career_"
    };

    // The sweep's table: a line or a family of lines whose premise is held where it is sent, with what holds it. A line
    // of a sent family that names one of the things above and is neither here, nor in Core/LineNeeds with a business, nor a
    // life event that needs one, fails. The longest entry that begins a key answers for it.
    private static readonly string[][] PremiseHeld =
    {
        new[] { "reallife_job_end_", "job", "sent as the job in hand ends (Jobs)" },
        new[] { "reallife_fire_", "business", "the fire is at a building of the player's own (Events/FireScene, the _own lines)" },
        new[] { "reallife_social_stage_partner", "partner", "the word for somebody at the partner stage" },
        new[] { "reallife_social_outing_business", "business", "the outing's name: offered only at a business of the player's (SocialPlaces.Suits)" },
        new[] { "reallife_social_memory_business", "business", "remembered after that outing" },
        new[] { "reallife_social_hint_customer", "business", "where somebody was met: a customer outside the player's shop (their origin)" },
        new[] { "reallife_social_topic_arrest_b", "business", "a turn of phrase: none of your business" },
        new[] { "reallife_street_age_cool_4", "business", "a turn of phrase: none of your business" },
        new[] { "reallife_social_follow_listed", "business", "taken up on a call after a text about the company's listing" },
        new[] { "reallife_social_follow_married", "partner", "taken up after the wedding's own text" },
        new[] { "reallife_social_follow_mom_married", "partner", "taken up after the wedding's own text" },
        new[] { "reallife_social_follow_friend_wedding_invite", "partner", "taken up after the wedding invitation's text" },
        new[] { "reallife_social_follow_birth_missed", "children", "taken up after the birth's text" },
        new[] { "reallife_travel_summary_business", "business", "added only when the days away have business statements (Travel/Holidays.Trip)" },
        new[] { "reallife_text_career_welcome", "job", "the Career contact's first message, about what it is for" },
        new[] { "reallife_career_", "job,degree", "the Career app and its notes: a shift, a boss or classes only for somebody employed or enrolled" },
        new[] { "reallife_text_business_welcome", "business", "the Business desk's first message, about what it is for" },
        new[] { "reallife_biz_", "business,staff", "the Business desk's issues open only at a business of the player's (Business/BusinessDirector)" },
        new[] { "reallife_family_at_home", "partner", "a test's name" },
        new[] { "reallife_family_test_partner_made", "partner", "a test's notice" },
        new[] { "reallife_family_errand_wedding", "partner", "the wedding day's errand: booked with a partner" },
        new[] { "reallife_family_prompt_planner", "partner", "the wedding planner's prompt: open to the engaged" },
        new[] { "reallife_family_plan_", "partner", "the wedding plan's own lines" },
        new[] { "reallife_family_care_partner", "partner", "said of a partner who lives in" },
        new[] { "reallife_family_no_lives_in", "partner", "a refusal that says the partner must live in first" },
        new[] { "reallife_family_car_released", "partner", "about a car the player gave their partner" },
        new[] { "reallife_family_page_budget_in", "partner", "the household budget's row" },
        new[] { "reallife_family_errand_adopt_meet", "children", "the adoption's errand" },
        new[] { "reallife_family_prompt_adopt_meet", "children", "the adoption's prompt" },
        new[] { "reallife_family_adopt_matched", "children", "the agency's text after the papers were filed" },
        new[] { "reallife_family_passage_adopt", "children", "the adoption's passage of time" },
        new[] { "reallife_family_birth_missed", "children", "sent when the birth happened without the player" },
        new[] { "reallife_family_study_enabled", "children", "an option's name" },
        new[] { "reallife_family_honeymoon_body", "business", "a box the player opens: says the businesses run on, true of none" },
        new[] { "reallife_family_divorce_ask", "business", "a box the player opens: says the businesses are not touched, true of none" },
        new[] { "reallife_family_divorce_theirs", "business", "says the businesses are untouched, true of none" },
        new[] { "reallife_econ_note_takings_", "business", "a named business's takings held against its own loan" },
        new[] { "reallife_macro_note_", "business", "only for goods the player's businesses stock (CityEconomyDirector.News, the Buys mask)" },
        new[] { "reallife_roommate_", "partner", "the roommate's page and refusals, about a partner who has moved in" },
        new[] { "reallife_fame_q_", "business,staff", "an interview's questions: the hard one is about the part of the player's standing that has fallen (FamePress.Weakest)" },
        new[] { "reallife_vc_note_", "business,shares", "notes about a firm the player founded or backed" },
        new[] { "reallife_exit_note_", "shares", "notes about the listing of the player's own firm" },
        new[] { "reallife_dealer_note_sold_staff", "staff", "a sale made by the dealership's staff" },
        new[] { "reallife_life_note_conflict_found", "business", "a dismissal over a business in the employer's trade, which the player has" },
        new[] { "reallife_own_note_colleagues_joined", "business", "colleagues joining the firm the player has just founded" },
        new[] { "reallife_rival_came_", "business", "a tycoon's move: an outlet, poaching or lobbying needs shops of the player's, a stake or a bid a listed company (RivalRules.Moves)" },
        new[] { "reallife_text_justice_suit_rival", "business", "a tycoon's claim, filed against a player worth suing: 'business dealings' in general" },
        new[] { "reallife_text_justice_suit_build", "business", "a claim over a building the player's company built" },
        new[] { "reallife_flare_dm_answer_collab", "business", "an influencer's answer to the player's own message" },
        new[] { "reallife_estate_note_manager_broke", "staff", "the estate manager the player hired" },
        new[] { "reallife_household_note_quit", "staff", "household staff the player hired" },
        new[] { "reallife_hier_note_week", "business,staff", "only in a week with problems opened or settled by managers (CompanyDirector.Escalations)" },
        new[] { "reallife_flare_use_tier", "followers", "told as the account reaches a tier" },
        new[] { "reallife_flare_use_turn", "business", "FlareUseRules.WarnsPlayer: the player's own business alone, and still theirs when told" },
        new[] { "reallife_news_pay_body", "staff", "shown with a count of the player's staff under the going rate, above none" },
        new[] { "reallife_party_note_", "staff", "a manager's staff event at a named business" },
        new[] { "reallife_hc_note_", "job", "an accident or a sick day at the player's employer" }
    };

    private static KeyValuePair<string, Regex> Premise(string thing, string pattern) =>
        new KeyValuePair<string, Regex>(thing, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    private static void PremiseSweep()
    {
        Dictionary<string, string> en = LoadWords(IoPath.Combine(LocalesFolder(), "en.json"));
        Check(en != null, "premise sweep: the English is read");
        if (en == null)
        {
            return;
        }
        var events = new Dictionary<string, Need>(StringComparer.Ordinal);
        foreach (LifeEvent e in LifeEventCatalogue.All)
        {
            events["reallife_ev_" + e.Id] = e.Needs;
        }
        var silent = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        int naming = 0, byNeed = 0, byEvent = 0, byTable = 0;
        foreach (KeyValuePair<string, string> row in en)
        {
            bool sent = false;
            foreach (string family in SentFamilies)
            {
                sent |= row.Key.StartsWith(family, StringComparison.Ordinal);
            }
            if (!sent)
            {
                continue;
            }
            foreach (KeyValuePair<string, Regex> thing in PremiseWords)
            {
                if (!thing.Value.IsMatch(row.Value))
                {
                    continue;
                }
                naming++;
                string owner = LineOwner(row.Key);
                if (thing.Key == "business" && (LineNeeds.Of(owner) & LineNeed.Business) != 0)
                {
                    byNeed++;
                    continue;
                }
                if ((thing.Key == "business" || thing.Key == "staff") && events.TryGetValue(owner, out Need needs) && (needs & Need.HasBusiness) != 0)
                {
                    byEvent++;
                    continue;
                }
                string[] held = null;
                foreach (string[] h in PremiseHeld)
                {
                    if (row.Key.StartsWith(h[0], StringComparison.Ordinal) && Array.IndexOf(h[1].Split(','), thing.Key) >= 0 && (held == null || h[0].Length > held[0].Length))
                    {
                        held = h;
                    }
                }
                if (held != null)
                {
                    byTable++;
                    used.Add(held[0]);
                }
                else
                {
                    silent.Add(row.Key + " (" + thing.Key + ")");
                }
            }
        }
        Check(naming >= 100 && silent.Count == 0,
            "premise sweep: every sent line whose English takes a business, staff, a partner, children, a job, a degree, followers or shares for granted says what holds it ("
            + naming + " namings: " + byNeed + " through Core/LineNeeds, " + byEvent + " life events that need a business, " + byTable + " in the table"
            + (silent.Count > 0 ? "; silent: " + string.Join(", ", silent) : string.Empty) + ")");
        var stale = new List<string>();
        var blank = new List<string>();
        foreach (string[] h in PremiseHeld)
        {
            if (!used.Contains(h[0]))
            {
                stale.Add(h[0]);
            }
            if (h.Length != 3 || h[2].Length < 12)
            {
                blank.Add(h[0]);
            }
        }
        Check(stale.Count == 0 && blank.Count == 0, "premise sweep: every line of the table still answers for a line that is there, and says what holds it"
            + (stale.Count > 0 ? " (stale: " + string.Join(", ", stale) + ")" : string.Empty) + (blank.Count > 0 ? " (blank: " + string.Join(", ", blank) + ")" : string.Empty));

        // The lines found speaking of a business with nothing to hold them, each through Core/LineNeeds now.
        Check(LineNeeds.Of("reallife_family_grown_call_2") == LineNeed.Business && LineNeeds.Of("reallife_street_open_famous_3") == LineNeed.Business
              && LineNeeds.Of("reallife_social_follow_fr09") == LineNeed.Business && LineNeeds.Of("reallife_social_follow_fm04") == LineNeed.Business
              && LineNeeds.Of("reallife_season_turn") == LineNeed.Business,
            "premise sweep: a grown child's message about your shops, a stranger's greeting about your businesses, a call taken up on a text about your business and the season's news need a business");
        Func<LineNeed, bool> before = LineNeeds.Read;
        try
        {
            LineNeed has = LineNeed.None;
            LineNeeds.Read = needs => (has & needs) == needs;
            bool without = true, with = false;
            string last = null;
            var said = new List<string>();
            for (int k = 0; k < 400; k++)
            {
                string line = RealLifeOverhaul.Street.StreetLines.Pick(RealLifeOverhaul.Street.StreetLine.OpenFamous, 5000 + k, k % 3, last, said);
                without &= line != "reallife_street_open_famous_3" && line != null && line != last;
                last = line;
            }
            has = LineNeed.Business;
            for (int k = 0; k < 400; k++)
            {
                with |= RealLifeOverhaul.Street.StreetLines.Pick(RealLifeOverhaul.Street.StreetLine.OpenFamous, 5000 + k, k % 3, null, said) == "reallife_street_open_famous_3";
            }
            Check(without && with, "premise sweep: a stranger never greets somebody with no business by their businesses, in 400 greetings, and does somebody with one");
        }
        finally
        {
            LineNeeds.Read = before;
        }
        Check(MemberText(ModSource(@"Seasons\SeasonDirector.cs"), "private void News(int day, int hour, int year, int salt)").Contains("if (LineNeeds.Fits(TurnKey))")
              && ModSource(@"Social\PersonDialog.CallBack.cs").Contains("if (!LineNeeds.Fits(topic.Open[0]))")
              && ModSource(@"Street\StreetLines.cs").Contains("if (!LineNeeds.Fits(pool[at]))"),
            "premise sweep: the season's news, the call back and the street's lines ask Core/LineNeeds before they speak");
    }

    private static void PremiseTexts()
    {
        Dictionary<string, string> en = LoadWords(IoPath.Combine(LocalesFolder(), "en.json"));
        if (en == null)
        {
            return;
        }
        en.TryGetValue("reallife_flare_use_attack", out string notice);
        en.TryGetValue("reallife_flare_biz_post_7", out string post);
        Check(notice != null && notice.Contains("{rival}") && notice.Contains("{business}") && notice.Length <= 240 && !notice.Contains("a business of yours"),
            "premise texts: the attack's notice names the rival and the business, within a notification's length");
        Check(post != null && post.Contains("{business}") && post.Contains("{name}"), "premise texts: the attack's post names the business and the rival");
        Check(WhatsNewHas(en, "Flare kept warning that a rival was talking down a business of yours") && WhatsNewHas(en, "asked again and again for a tour of a business you did not have")
              && WhatsNewHas(en, "could be set up on a crossing in the road") && WhatsNewHas(en, "does not write the same comment twice running under your Flare posts")
              && WhatsNewHas(en, "reviews of a business of yours had turned when the business was somebody else's"),
            "premise texts: What's new has the fixes' lines in English, in some row");
    }
}
