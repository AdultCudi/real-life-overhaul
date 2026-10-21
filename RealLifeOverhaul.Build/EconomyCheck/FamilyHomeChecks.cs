using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The partner at home, from play: texts written while the player slept passed unseen in the skip's hourly batch of notices;
// "Cook together" used no food and played nothing; "Watch together" left the partner standing, and a sofa with the player on
// it counted as taken; in a small home the partner stood at one place doing one thing for days; and the chat at home was
// Family's one line, not the People chat. All of it is the game's side, so it is checked in its source.
internal static partial class Program
{
    private static void FamilyHomeFixes()
    {
        FamilyNightTexts();
        FamilyCookTogether();
        FamilyWatchSeat();
        FamilyMovesAbout();
        FamilyFaceChat();
        FamilyComesHome();
        FamilyRoutine();
        FamilyPlaytestFive();
    }

    private static void FamilyNightTexts()
    {
        string text = ModSource(@"Family\FamilyText.cs");
        string message = MemberText(text, "public static void Message(Contact contact, string key, in Args a, bool notify = true)");
        string send = MemberText(text, "private static void Send(Contact contact, string key, Dictionary<string, string> data, bool notify)");
        string hourly = MemberText(ModSource(@"Family\FamilyCalendar.cs"), "public static void Hourly(int day, int hour)");
        Check(text.Contains("public const int TextFrom = 8, TextTo = 22;") && message.Contains("if (!Awake)") && message.Contains("Morning.Add("),
            "family home: a family text written between 22:00 and 08:00 or in a time skip is kept for the morning");
        Check(hourly.Contains("FamilyText.SendHeld();") && send.Contains("contact.SendMessage(new TextMessage(key, data), notify: notify, sendNotificationInstantly: notify);"),
            "family home: the kept texts go out hourly once it is day, each with its notice at once rather than in the hour's batch");
        Check(FixLineEverywhere("they come in the morning, with a notice", "chegam de manhã, com um aviso", "会在早上带着通知送达", "sie kommen morgens mit einer Mitteilung",
                                "ils arrivent le matin, avec une notification", "llegan por la mañana, con un aviso", "они приходят утром, с уведомлением",
                                "ze komen 's ochtends, met een melding", "sabah bir bildirimle geliyor"),
            "family home: What's new has the night texts' line, with its fix mark, in every language");
    }

    private static void FamilyCookTogether()
    {
        string home = ModSource(@"Family\HomeLife.cs");
        string cook = MemberText(home, "public static DialogEntry Cook()");
        string cooked = MemberText(home, "private static void Cooked(bool served)");
        int asked = cook.IndexOf("FamilyBridges.Home.CookTogether(Partner.Name, Cooked)", StringComparison.Ordinal);
        int shared = cook.IndexOf("Shared(FamilyBalance.HarmonyRoutine);", StringComparison.Ordinal);
        Check(asked > 0 && shared > asked && cooked.Contains("if (!served)") && cooked.Contains("Shared(FamilyBalance.HarmonyRoutine);"),
            "family home: cooking together counts once Home's dish is served, and only the default without Home counts at once");
        string activity = ModSource(@"Home\CookActivity.cs");
        string list = MemberText(ModSource(@"Home\HomeCookList.cs"), "private static void Closed()");
        string refuse = MemberText(ModSource(@"Home\HomeDirector.Routines.cs"), "public static void Refuse(string key, string what, string detail = null)");
        Check(activity.Contains("_together = CookingTogether.Waiting;") && activity.Contains("extra--;   // the partner's plate") && activity.Contains("CookingTogether.Finish(true);"),
            "family home: the meal cooked together is Home's dish from the fridge or the leftovers, and a portion is the partner's");
        Check(list.Contains("if (_closing != Chosen)") && list.Contains("CookingTogether.Finish(false);") && refuse.Contains("CookingTogether.Finish(false);"),
            "family home: a cooking list closed without a dish, or a dish refused, tells Family nothing was cooked");
        Check(MemberText(ModSource(@"Wiring\FamilyLinks.cs"), "public bool CookTogether(string partner, Action<bool> done)").Contains("return CookingTogether.Start(done);"),
            "family home: Family's cooking together goes to Home's cooking");
    }

    private static void FamilyWatchSeat()
    {
        string seating = MemberText(ModSource(@"People\Seating.cs"), "public static Transform Beside(ItemController item, Vector3 facing)");
        string at = ModSource(@"Family\HomeLife.AtHome.cs");
        string sit = MemberText(at, "private static bool SitDown(FamilyDirector director, float now)");
        Check(seating.Contains("!seat.CanSit") && sit.Contains("Seating.Beside(item, Faces[_at])") && !sit.Contains("Seating.Free("),
            "family home: the partner takes a free place on a seat somebody else is on (a sofa shared), by the game's own CanSit");
        string watch = MemberText(ModSource(@"Family\HomeLife.cs"), "public static DialogEntry Watch()");
        Check(watch.Contains("AfterTalk(true);") && MemberText(at, "private static void EndTalk()").Contains("GoAfter(director, Time.time);")
              && MemberText(at, "private static int WatchSeat()").Contains("TvItems"),
            "family home: watching together sends the partner to the seat nearest the television once the conversation is over");
    }

    private static void FamilyMovesAbout()
    {
        string at = ModSource(@"Family\HomeLife.AtHome.cs");
        string standing = MemberText(at, "private static void Standing(FamilyDirector director, float now, Vector3 player)");
        string activity = MemberText(at, "private static void Activity(FamilyDirector director, int at)");
        Check(ModSource(@"Family\HomeLife.Places.cs").Contains("private const int FloorAtLeast = 4;"),
            "family home: a home with little furniture gets clear floor up to four places for the partner");
        Check(standing.Contains("_nextMove = now + Random.Range(40f, 80f);") && standing.IndexOf("Activity(director, _at);", StringComparison.Ordinal) > 0
              && !activity.Contains("(TimeHelper.CurrentDay + at) % 2"),
            "family home: a partner who cannot walk on does something else where they are, not one thing chosen by the day");
    }

    private static void FamilyFaceChat()
    {
        string chat = MemberText(ModSource(@"Family\FamilyPhone.cs"), "private static DialogEntry Chat()");
        string face = MemberText(ModSource(@"Social\SocialFaceChat.cs"), "private static DialogEntry Reply(SocialPerson person, SocialTopic topic, bool warm, Dictionary<string, string> data, Func<DialogEntry> more, Action leave, int had)");
        Check(chat.Contains("if (Partner.UsesSocial && HomeLife.Present)") && chat.Contains("HomeLife.FaceChat()") && face.Contains("SocialTalk.Answer(person, topic, warm, true, had == 0)"),
            "family home: a chat with the partner at home is People's chat, worth what face to face is");
    }

    private static void FamilyComesHome()
    {
        string at = ModSource(@"Family\HomeLife.AtHome.cs");
        string tick = MemberText(at, "public static void Tick(float now)");
        string comeIn = MemberText(at, "private static bool ComeIn(Vector3 player, out Vector3 spot)");
        Check(tick.Contains("Arrive(now, player, _justEntered || !cameHome);") && comeIn.Contains("FamilyPlaces.Floor(_entry, _entry, 0.25f, 1.6f,")
              && !comeIn.Contains("InView"),
            "family home: the partner is in or out of the building: back from out they come in at the door, found from the door itself, whoever looks");
        Check(tick.Contains("if (_partner.Near(_entry, 0.9f) || now >= _giveUp)") && tick.Contains("if (_partner.Near(_goneAt, 0.9f) || now >= _giveUp || !CitySpots.InView(_partner.Position))"),
            "family home: going out or to bed ends at the door or the bed, or when the walk is over, in view or not (a home seen whole from above)");
        string support = MemberText(ModSource(@"Family\HomeLife.cs"), "public static void Support(int day, int hour)");
        Check(support.Contains("!FamilyPlaces.AtHome() || (Settings.FamilyAtHome && !Present)"),
            "family home: comfort after a bad day, or care when ill, only with the player at home and the partner there");
        string home = ModSource(@"Family\HomeLife.cs");
        Check(home.Contains("FamilyText.Pick(HasBath() ? Encourage : EncourageNoBath, ref _lastEncourage)")
              && !home.Substring(home.IndexOf("string[] EncourageNoBath", StringComparison.Ordinal), 300).Contains("\"reallife_family_encourage_5\""),
            "family home: the comforting line that runs a bath comes only in a home with a bathtub");
        Check(FixLineEverywhere("now comes in when they get home while you are in", "agora entra quando chega em casa", "现在会走进来", "kommt jetzt herein",
                                "entre désormais quand il rentre", "ahora entra cuando llega a casa", "теперь входит, когда возвращается домой",
                                "komt nu binnen als ze thuiskomen", "artık içeri giriyor"),
            "family home: What's new has the coming home line, with its fix mark, in every language");
    }

    private static void FamilyRoutine()
    {
        string at = ModSource(@"Family\HomeLife.AtHome.cs");
        string places = ModSource(@"Family\HomeLife.Places.cs");
        Check(MemberText(places, "private static int KindOf(ItemController item)").Contains("Is(name, CookItems) ? CookKind")
              && places.Contains("FamilyPlaces.Floor(at, player, 0.25f, 1.8f, out Vector3 spot, out int type)"),
            "family home: the home's furniture is read for cooking, sport, the bed, a desk and plants, close to it as a small flat's gaps allow");
        string draw = MemberText(at, "private static float Draw(int kind, int hour)");
        Check(draw.Contains("case CookKind: return hour >= 17 && hour < 21 ? 6f") && draw.Contains("case BedKind: return 0f;")
              && MemberText(at, "private static void Standing(FamilyDirector director, float now, Vector3 player)").Contains("int next = Next(_at, TimeHelper.CurrentHour);"),
            "family home: the partner goes where the hour draws them (dinner at the cooker, sport in the morning, the sofa at night), never to bed by day");
        Check(MemberText(at, "private static void Standing(FamilyDirector director, float now, Vector3 player)").Contains("(Chores.Pending || PartnerVoice.AnyAsk)")
              && MemberText(at, "private static bool ComeOver(FamilyDirector director, float now, Vector3 player)").Contains("_at = ByYouAt;"),
            "family home: the partner comes over and waits facing the player only with something to say");
        string sitting = MemberText(at, "private static void Sitting(FamilyDirector director, float now, Vector3 player, bool staying)");
        string home = ModSource(@"Family\HomeLife.cs");
        Check(sitting.Contains("controller.Character.isSittingOn != null") && sitting.Contains("Watched();") && sitting.Contains("WaitedInVain();")
              && MemberText(home, "public static DialogEntry Watch()").Contains("AfterTalk(true);")
              && !MemberText(home, "public static DialogEntry Watch()").Contains("WatchLines"),
            "family home: watching together is had when the player sits down with them, and an hour waited in vain is a text and a small cooling");
        string lie = MemberText(ModSource(@"CityLife\CityPerson.cs"), "public bool Lie(EntityController bed, Transform at)");
        Check(lie.Contains("Character.LayOnBed(bed, at);") && lie.Contains("agent.enabled = false;")
              && MemberText(at, "private static bool LieDown(FamilyDirector director, float now)").Contains("(!_bedForTwo && frame.Occupied)")
              && at.Contains("if (where == Whereabouts.Asleep && LieDown(director, now))") && at.Contains("PutToBed(director, now);"),
            "family home: asleep, the partner lies in the home's bed by the game's own LayOnBed, and makes way from a bed for one while the player is in it");
        string putToBed = MemberText(at, "private static void PutToBed(FamilyDirector director, float now)");
        Check(putToBed.Contains("now < _bedTry") && !MemberText(at, "private static void ReleasePartner()").Contains("_bedTry")
              && MemberText(at, "private static bool LieDown(FamilyDirector director, float now)").Contains("_bedTry = now + BedRetry;"),
            "family home: a bed they could not lie in is tried again after a while, not a partner put there and let go each second");
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        bool guide = codes.Length == ModLanguages.All.Length;
        for (int l = 0; guide && l < codes.Length; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_family", out string g) ? g : string.Empty;
            guide &= page.Split('\n').Length == 26 && (codes[l] != "en" || (page.Contains("At night they sleep in your bed") && page.Contains("asks for a chore only when it is needed")
                     && !page.Contains("They never lie down") && !page.Contains("they stand in the kitchen or the living room")));
        }
        Check(guide, "family home: the Guide's family page tells of the partner's routine, nights in bed and real chores, its 26 lines in every language");
        Check(at.Contains("case Mode.Sleeping:") && MemberText(at, "private static void WakeUp(FamilyDirector director, float now, Vector3 player)").Contains("_partner.Rise();"),
            "family home: in the morning the partner gets up out of bed");
        Check(ModSource(@"Family\FamilyBalance.cs").Contains("public const float HarmonyWaitedInVain = -2f;"),
            "family home: waiting in vain costs two points of harmony");
    }

    private static void FamilyPlaytestFive()
    {
        string at = ModSource(@"Family\HomeLife.AtHome.cs");
        Check(at.Contains("_entry = Door(manager, player);") && MemberText(at, "private static Vector3 Door(BuildingManager manager, Vector3 player)").Contains("best.playerSpawnPoint.position"),
            "family home: the partner comes in and leaves by the flat's own door (the game's way in), not where the player stood when the home was seen");
        string sitting = MemberText(at, "private static void Sitting(FamilyDirector director, float now, Vector3 player, bool staying)");
        Check(at.Contains("private const float WatchMinutes = 90f;") && sitting.Contains("_watchUntil = minute + WatchMinutes;") && sitting.Contains("over = true;")
              && sitting.Contains("|| over ||"),
            "family home: watching ends on the game's clock, after the programme or the hour waited, wherever the player is");
        Check(MemberText(ModSource(@"Home\CookingTogether.cs"), "public static void Tick(HomeDirector host)").Contains("DialogController.current == null && !ThirdPersonCamera.IsUiOpen()")
              && ModSource(@"Home\HomeDirector.cs").Contains("CookingTogether.Tick(this);")
              && ModSource(@"Family\FamilyPhone.Choices.cs").Contains("FamilyText.Say(HomeLife.CookAsk)"),
            "family home: cooking together opens the list once the conversation is closed, and is asked by the time of day");
        string any = MemberText(at, "private static void Any(CityProps props, string first, string second, string third = null)");
        Check(any.Contains("Cleaned(CleanSweep);") && at.Contains("private const float CleanPerDay = 8f;")
              && ModSource(@"Wiring\FamilyLinks.cs").Contains("return Roommates.RoommateGame.Tidy(points);"),
            "family home: the partner's sweeping and wiping clean the home as a tidy roommate's cleaning does, up to eight points a day");
        string kin = MemberText(ModSource(@"Family\Relatives.cs"), "public static string NextOfKin()");
        Check(kin.Contains("bool spouse = FamilyState.Bond == Bond.Married;") && kin.Contains("return spouse ? Partner.Name : FamilyText.T(\"reallife-mom\");"),
            "family home: next of kin is Mom until the wedding, not somebody the player is dating");
    }
}
