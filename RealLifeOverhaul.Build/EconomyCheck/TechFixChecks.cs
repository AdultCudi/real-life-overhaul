using System;
using System.IO;
using RealLifeOverhaul.Tech;

// The tech sector (Scripts/Tech): the faults found when the folder was cleaned up, each checked against what the old code
// did wrong. A board that cannot hire keeps its chief executive and says no more than it did; a pitch whose meeting never
// opens uses none of the raise's meetings; the utilities' answers are read through readers that take a fault as the
// default, so it cannot switch the sector off; an interview the player did not end is not called held.
internal static partial class Program
{
    private static void TechFixes()
    {
        TechChiefKept();
        TechPitchCounted();
        TechUtilitiesGuarded();
        TechInterviewEnds();
    }

    // Two targets missed with a majority on the board: it replaces the chief. With no room on the payroll it warns, and the next miss tries again.
    private static void TechChiefKept()
    {
        var c = new Cap { VcSeats = 3, BoardSeats = 5 };
        VentureRules.Target(c, 1000000L, 0);
        int first = VentureRules.Review(c, 0L, c.TargetDay);
        int second = VentureRules.Review(c, 0L, c.TargetDay);
        Check(first == 2 && second == 3 && c.Pressure == 2, "tech, the board: " + VentureCatalogue.MissesToReplace + " targets missed in a row replace the chief executive (" + first + ", " + second + ")");
        VentureRules.ChiefKept(c);
        Check(c.Pressure == 1, "tech, the board: a board that could hire nobody shows as having warned, not as having replaced (" + c.Pressure + ")");
        int next = VentureRules.Review(c, 0L, c.TargetDay);
        Check(next == 3, "tech, the board: kept for want of room, the chief executive is replaced at the next target missed (" + next + ")");

        string venture = ModSource(@"Tech\TechDirector.Venture.cs");
        string replace = MemberText(venture, "private void ReplaceChief(Cap c, FirmPlace firm, int day)");
        int room = replace.IndexOf("_talent.People.Count >= TalentStore.PeopleMost", StringComparison.Ordinal);
        int kept = replace.IndexOf("VentureRules.ChiefKept(c);", StringComparison.Ordinal);
        int back = replace.IndexOf("return;", StringComparison.Ordinal);
        int letGo = replace.IndexOf("LetGo(was, false)", StringComparison.Ordinal), made = replace.IndexOf("_talent.NextPro++", StringComparison.Ordinal);
        int added = replace.IndexOf("_talent.People.Add(chief);", StringComparison.Ordinal), told = replace.IndexOf("reallife_vc_note_chief_replaced", StringComparison.Ordinal);
        Check(replace.Length > 0 && room >= 0 && kept > room && back > kept && letGo > back && made > back && added > made && told > added
              && Occurs(replace, "TalentStore.PeopleMost") == 1 && replace.IndexOf("c.Ceo = chief.Id;", StringComparison.Ordinal) > added,
            "tech, the board: the room on the payroll is asked before anyone is laid off or made, and a new chief is announced only once added");
    }

    // The meeting's count goes up only once its window has opened.
    private static void TechPitchCounted()
    {
        string deals = ModSource(@"Tech\TechDirector.Deals.cs");
        string meet = MemberText(deals, "internal bool MeetVc(string place)");
        int opened = meet.IndexOf("if (!_pitch.Player(", StringComparison.Ordinal), counted = meet.IndexOf("c.Meetings++", StringComparison.Ordinal);
        Check(meet.Length > 0 && opened >= 0 && counted > opened && Occurs(meet, "c.Meetings++") == 1,
            "tech, venture: a pitch uses one of the raise's meetings only when the meeting's window opened");
    }

    // No part of the tech sector asks the utilities bare: a throw there is the default answer, never a fault of the day's pass.
    private static void TechUtilitiesGuarded()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Tech"));
        string[] bare = { "TechBridges.Supplied(", "TechBridges.Supplied ", "TechBridges.Supplied?", "TechBridges.Mbps(", "TechBridges.Mbps ", "TechBridges.Mbps?",
                          "TechBridges.UtilityCents(", "TechBridges.UtilityCents ", "TechBridges.UtilityCents?" };
        string found = null;
        int files = 0;
        foreach (string file in Directory.Exists(folder) ? Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories) : new string[0])
        {
            files++;
            string code = File.ReadAllText(file);
            for (int i = 0; i < bare.Length && found == null; i++)
            {
                found = code.Contains(bare[i]) ? System.IO.Path.GetFileName(file) + ": " + bare[i] : null;
            }
        }
        string infra = ModSource(@"Tech\TechDirector.Infra.cs");
        Check(files > 50 && found == null && infra.Contains("TechBridges.SuppliedAt(reg, 0)") && infra.Contains("TechBridges.FastEnough(reg, InfraCatalogue.NetNeeded)")
              && infra.Contains("TechBridges.UtilityCentsAt("), "tech, bridges: the utilities' power, internet and tariff are asked only through readers that take a fault as the default (" + (found ?? files + " files") + ")");
        string bridges = ModSource(@"Tech\TechBridges.cs");
        foreach (string reader in new[] { "bool SuppliedAt(", "bool FastEnough(", "long UtilityCentsAt(" })
        {
            string body = MemberText(bridges, "public static " + reader);
            Check(body.Contains("try") && body.Contains("catch (Exception)"), "tech, bridges: " + reader.Substring(reader.IndexOf(' ') + 1) + ") answers its default when the utilities throw");
        }
    }

    // Stopping the hiring part, a fault or another window ends an interview without "interviewed"; the player's own close says it.
    private static void TechInterviewEnds()
    {
        string meeting = ModSource(@"Tech\Hiring\InterviewMeeting.cs");
        string end = MemberText(meeting, "public void End()"), closed = MemberText(meeting, "private void Closed()"), watch = MemberText(meeting, "public void Watch()");
        string free = MemberText(meeting, "private void Free(bool held)"), picked = MemberText(meeting, "private void Picked(int id)");
        int held = free.IndexOf("if (held)", StringComparison.Ordinal), said = free.IndexOf("reallife_hire_note_interviewed", StringComparison.Ordinal);
        Check(end.Contains("Stop(false);") && closed.Contains("Free(true);") && watch.Contains("Free(false);") && picked.Contains("Stop(true);")
              && held >= 0 && said > held && Occurs(meeting, "reallife_hire_note_interviewed") == 1,
            "tech, hiring: \"interviewed\" is said only when the player ended the interview, never when the hiring part or a fault stopped it");
    }
}
