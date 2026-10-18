using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// Faults found in Scripts/Careers: the director went on offering sittings after the guard had switched education off and
// disabled it, though ShiftSkip calls a sitting off on a disabled host; and the test buttons' picks rely on the store never
// keeping a course the catalogue has lost. What only the game's side can do is checked in its source.
internal static partial class Program
{
    private static void CareersFixes()
    {
        CareersStandsDown();
        CareersLostCourse();
    }

    private static void CareersStandsDown()
    {
        string director = ModSource(@"Careers\CareersDirector.cs");
        string tick = MemberText(director, "private IEnumerator Tick()");
        int off = tick.IndexOf("if (FrameGuard.Off)", System.StringComparison.Ordinal);
        int slow = tick.IndexOf("Slow();", System.StringComparison.Ordinal);
        Check(off > 0 && slow > off && tick.IndexOf("StandDown(", off, System.StringComparison.Ordinal) > off,
            "careers fix: once the guard has switched education off, the timer stands the director down before its next round");
        string stand = MemberText(director, "private void StandDown(string why)");
        Check(stand.Contains("_broken = true;") && stand.Contains("CloseSitting();") && stand.Contains("StopWork(false);") && stand.Contains("StopStudy(false, false);")
              && MemberText(director, "private void Fault(string where, Exception e)").Contains("StandDown("),
            "careers fix: standing down, after a fault or the guard, closes the sitting and stops work and study, as a fault always did");
        string offer = MemberText(director, "internal string Offer(Sitting sitting)");
        int refused = offer.IndexOf("if (_broken || !isActiveAndEnabled)", System.StringComparison.Ordinal);
        int shown = offer.IndexOf("ShiftSkip.Offer(this, sitting)", System.StringComparison.Ordinal);
        Check(refused > 0 && shown > refused, "careers fix: no sitting is offered while the director is off or disabled");
        // Why: the game's panel starts a sitting through its host, and calls it off on one that is disabled.
        string skip = MemberText(ModSource(@"Jobs\ShiftSkip.cs"), "private void Start()");
        Check(skip.Contains("!_host.isActiveAndEnabled") && skip.Contains("Cancel();"), "careers fix: ShiftSkip still needs its host enabled at Start, which the refusal above is for");
    }

    private static void CareersLostCourse()
    {
        var f = new StudyFile { Seed = 5, FirstDay = 2, LastDay = 9 };
        f.Courses.Add(new Enrolment { Course = 9999, Start = 3, Instalments = 1 });
        f.Courses.Add(new Enrolment { Course = StudyCatalogue.FirstAid, Start = 4, Instalments = 1 });
        StudyFile back = CareersStore.Read(CareersStore.Write(f), out int _);
        Check(StudyCatalogue.Find(9999) == null && back.Courses.Count == 1 && back.Courses[0].Course == StudyCatalogue.FirstAid && back.Courses[0].Def != null,
            "careers fix: a course the catalogue no longer has is not read back, so every course under way has its Def (the test buttons rely on it)");
        var none = new List<Enrolment>(back.Courses);
        none.RemoveAll(e => e.Def == null);
        Check(none.Count == back.Courses.Count, "careers fix: no course under way is read without its Def");
    }
}
