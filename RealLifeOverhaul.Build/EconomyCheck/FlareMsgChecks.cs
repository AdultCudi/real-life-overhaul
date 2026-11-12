using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ui;

// The Messages inbox shows the words of every message (the preview in the list and the bubble in the thread), and a person with a contact on
// the phone wears that contact's picture in Flare as Contacts shows it, not a painted face.
internal static partial class Program
{
    private static void FlareMsgChecks()
    {
        var net = new FlareNet();
        net.Dms.Add(new FlareDm { Id = 1, With = "f0", Name = "Mom", At = 100, Text = "Come to dinner on Sunday" });
        net.Dms.Add(new FlareDm { Id = 2, With = "f0", Name = "Mom", At = 110, Mine = true, Text = "I will, thank you" });
        net.Dms.Add(new FlareDm { Id = 3, With = "c:5", Name = "Greta Hayes", At = 120, Text = "Is the shop open today" });
        net.Dms.Add(new FlareDm { Id = 4, With = "c:6", Name = "Joe", At = 130, Key = "reallife_flare_dm_hello" });
        net.Dms.Add(new FlareDm { Id = 5, With = "c:7", Name = "Kim", At = 140 });
        var threads = new List<FlareThread>();
        var thread = new List<FlareDm>();
        FlareLiveRules.Threads(net, 1000, threads);
        FlareLiveRules.Thread(net, "f0", 1000, thread);
        bool previews = true, bubbles = thread.Count == 2;
        for (int i = 0; i < threads.Count; i++)
        {
            FlareDm last = threads[i].Last;
            string kept = last.Text == null ? "kept words" : null;
            string line = FlareTextsMap.DmShown(last, kept, last.Key != null ? "the city's line" : null);
            previews &= last.Text != null ? line == last.Text : line.Length > 0;
        }
        for (int i = 0; i < thread.Count; i++)
        {
            bubbles &= FlareTextsMap.DmShown(thread[i], null, null) == thread[i].Text;
        }
        Check(previews && bubbles, "Flare messages: the preview in the list and the bubble in the thread show the typed words of an incoming and of a sent message");
        Check(FlareTextsMap.DmShown(net.Dms[3], null, "the city's line") == "the city's line" && FlareTextsMap.DmShown(net.Dms[3], "model words", "the city's line") == "model words"
              && FlareTextsMap.DmShown(net.Dms[4], null, null) == string.Empty && FlareTextsMap.DmShown(null, null, null) == string.Empty
              && FlareTextsMap.DmShown(net.Dms[0], "model words", "x") == "Come to dinner on Sunday",
            "Flare messages: typed words first, then the model's, then the city's line by its key, else nothing");

        var people = new List<FlarePerson>
        {
            new FlarePerson { Index = 0, First = "Mom", Contact = "reallife-mom" },
            new FlarePerson { Index = 4, First = "Sam", Contact = "Sam Reed" },
            new FlarePerson { Index = 9, First = "Lou" }
        };
        var file = new FlareFile();
        file.Influencers.Add(new FlareInfluencer { Id = 2, Handle = "@sam", Person = 4 });
        file.Influencers.Add(new FlareInfluencer { Id = 3, Handle = "@free", Person = -1 });
        Check(FlareAvatarRules.ContactOf("f0", people, file) == "reallife-mom" && FlareAvatarRules.ContactOf("p4", people, file) == "Sam Reed"
              && FlareAvatarRules.ContactOf("i:2", people, file) == "Sam Reed",
            "Flare faces: Mom, a person met and an influencer who is one wear their contact's picture");
        Check(FlareAvatarRules.ContactOf("f9", people, file) == null && FlareAvatarRules.ContactOf("i:3", people, file) == null && FlareAvatarRules.ContactOf("c5", people, file) == null
              && FlareAvatarRules.ContactOf("me", people, file) == null && FlareAvatarRules.ContactOf("f77", people, file) == null && FlareAvatarRules.ContactOf(null, people, file) == null,
            "Flare faces: a person with no contact, a citizen, the player and an unknown account keep the painted face");

        var told = new Dictionary<string, string>();
        Check(ContactFaceRules.Pick(true, false) == ContactFaceFrom.Mod && ContactFaceRules.Pick(true, true) == ContactFaceFrom.Mod
              && ContactFaceRules.Pick(false, true) == ContactFaceFrom.Game && ContactFaceRules.Pick(false, false) == ContactFaceFrom.Painted,
            "Contact faces: the mod's photo, else the game's sprite, else the painted face");
        Check(ContactFaceRules.Fresh(told, "Mom", "reallife-mom") && !ContactFaceRules.Fresh(told, "Mom", "reallife-mom") && ContactFaceRules.Fresh(told, "Mom", "other")
              && !ContactFaceRules.Fresh(told, "", "x") && !ContactFaceRules.Fresh(told, "Mom", null),
            "Contact faces: a person is told once under a name, again only for another contact, never for no contact");
    }
}