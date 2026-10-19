using System;

internal static partial class Program
{
    // Family life's "we have not really talked in days" and its warmth count Social's calls, texts, outings and meetings with the
    // partner while Social supplies them (Family/TalkRules, Family/HomeLife.Daily, Wiring/FamilyLinks).
    private static void PartnerTalk()
    {
        Check(RealLifeOverhaul.Family.TalkRules.LastTalked(10, 8, 0) == 10 && RealLifeOverhaul.Family.TalkRules.LastTalked(8, 10, 0) == 10
              && RealLifeOverhaul.Family.TalkRules.LastTalked(0, 0, 0) == 0 && RealLifeOverhaul.Family.TalkRules.LastTalked(5, 5, -1) == 5,
            "partner talk: with no contact in Social the last talk is family life's own, as before");
        Check(RealLifeOverhaul.Family.TalkRules.LastTalked(10, 8, 15) == 15 && RealLifeOverhaul.Family.TalkRules.LastTalked(3, 4, 9) == 9
              && RealLifeOverhaul.Family.TalkRules.LastTalked(10, 12, 11) == 12,
            "partner talk: a later call, text, outing or meeting in Social counts as the last talk, an earlier one changes nothing");

        string home = ModSource(@"Family\HomeLife.cs");
        string daily = MemberText(home, "public static void Daily(int day)");
        string contact = MemberText(home, "private static int SocialContactDay()");
        Check(daily.Contains("int quiet = day - TalkRules.LastTalked(s.LastTalkDay, s.LastSharedDay, SocialContactDay());")
              && !daily.Contains("Mathf.Max(s.LastTalkDay, s.LastSharedDay)")
              && contact.Contains("social != null && social.Active && social.TryGetPartner(out PersonInfo partner) ? partner.LastContactDay : 0"),
            "partner talk: the daily neglect and warmth read Social's last contact with the partner, only while Social supplies them");
        string links = ModSource(@"Wiring\FamilyLinks.cs");
        string info = MemberText(links, "private static PersonInfo Info(SocialPerson p)");
        Check(info.Contains("LastContactDay = p.LastContactDay") && ModSource(@"Family\FamilyBridges.cs").Contains("public int LastContactDay;"),
            "partner talk: the bridge hands family life the Social partner's last contact day");
        Check(FixLineEverywhere("a call, text or outing with your partner in Social now counts as talking", "agora conta como conversa",
                                "现在算作交谈", "zählen jetzt als Gespräch", "compte maintenant comme une conversation", "ahora cuenta como hablar",
                                "теперь считаются разговором", "telt nu als praten", "artık konuşma sayılıyor"),
            "partner talk: What's new names the fix in every language");
    }
}
