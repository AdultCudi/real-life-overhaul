using System;
using System.IO;
using RealLifeOverhaul.Flare;

// Checks for Flare's hour in steps (Social/Flare/FlareHourSteps.cs) and how the director runs them as it ships: every
// hour's six steps in their order, hours in the order they began, a long skip that fills the queue finishing the oldest
// at once, nothing left when cleared; the director runs a step a frame from the frame after the game's hour, does what
// is left first when the game saves, a day begins or Flare stands down, and runs the same work in the same order as the
// hour did in one frame.
internal static partial class Program
{
    private static void FlareHours()
    {
        var steps = new FlareHourSteps();
        Check(!steps.Busy && !steps.Next(out _, out _, out _), "flare hour: nothing waits at first");
        Check(steps.Add(3, 10) && steps.Add(3, 11) && steps.Waiting == 2, "flare hour: hours queue as they begin");
        bool ordered = true;
        for (int hour = 10; hour <= 11; hour++)
        {
            for (int want = 0; want < FlareHourSteps.Steps; want++)
            {
                ordered &= steps.Next(out int d, out int h, out int s) && d == 3 && h == hour && s == want;
            }
        }
        Check(ordered && !steps.Busy, "flare hour: each hour's six steps in their order, the hours in theirs");
        Check(FlareHourSteps.City == 0 && FlareHourSteps.Cases == 1 && FlareHourSteps.Out == 2 && FlareHourSteps.Unread == 3 && FlareHourSteps.Words == 4
              && FlareHourSteps.Milestones == 5 && FlareHourSteps.Steps == 6, "flare hour: the steps are the hour's work in the order it ran in one frame");
        for (int i = 0; i < FlareHourSteps.Most; i++)
        {
            steps.Add(1, i % 24);
        }
        Check(!steps.Add(9, 9) && steps.Waiting == FlareHourSteps.Most, "flare hour: a full queue refuses the next hour, which the director then makes room for");
        int ran = 0;
        int waiting = steps.Waiting;
        while (steps.Waiting == waiting && steps.Next(out _, out _, out _))
        {
            ran++;
        }
        Check(ran == FlareHourSteps.Steps && steps.Add(9, 9), "flare hour: finishing the oldest is its six steps, and the next hour fits");
        steps.Next(out _, out _, out _);
        steps.Clear();
        Check(!steps.Busy && steps.Waiting == 0 && steps.Add(2, 2) && steps.Next(out _, out _, out int first) && first == FlareHourSteps.City,
            "flare hour: cleared, it starts again at an hour's first step");

        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string director = Source(scripts, @"Social\Flare\FlareDirector.cs");
        string onHour = Body(director, "private void OnHour()");
        Check(onHour.Contains("_hourSteps.Add(") && !onHour.Contains("CityHour(") && !onHour.Contains("HourStep("), "flare hour: the game's hour only queues its work");
        string run = Body(director, "private IEnumerator HourSteps()");
        int yield = run.IndexOf("yield return null;", StringComparison.Ordinal);
        int stepAt = run.IndexOf("HourStep(", StringComparison.Ordinal);
        Check(yield >= 0 && stepAt > yield && run.IndexOf("HourStep(", stepAt + 1, StringComparison.Ordinal) < 0, "flare hour: one step a frame, from the frame after the hour");
        string one = Body(director, "private void HourStep(int day, int hour, int step)") + Body(Source(scripts, @"Social\Flare\FlareDirector.Live.cs"), "private void LiveHour(int day, int hour, int step)");
        string[] order = { "CityHour(", "Milestones(", "LiveHour(", "Works(day)", "Invitations(", "Watched(", "Recount(", "TextsAhead(" };
        int at = -1;
        bool inOrder = true;
        foreach (string call in order)
        {
            int next = one.IndexOf(call, StringComparison.Ordinal);
            inOrder &= next > at;
            at = next;
        }
        Check(inOrder, "flare hour: the steps run the hour's work in the order it ran in one frame");
        Check(Body(director, "private void OnSave()").Contains("FinishHours();") && Body(director, "private void OnDay()").Contains("FinishHours();")
              && Body(director, "private void StandDown(bool keep)").Contains("FinishHours();"), "flare hour: the save, a new day and standing down finish the hours first");
        string live = Source(scripts, @"Social\Flare\FlareDirector.Live.cs");
        string liveHour = Body(live, "private void LiveHour(int day, int hour, int step)");
        Check(liveHour.Contains("step == FlareHourSteps.Cases") && liveHour.Contains("step == FlareHourSteps.Words"), "flare hour: the live steps each run in a step of their own");
    }
}
