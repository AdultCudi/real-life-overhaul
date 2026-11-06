using System;
using RealLifeOverhaul;

// The game-free rules of a driving test that cannot start (Licences/Driving/TestStallRules.cs): the accelerator held while the school's
// car does not move is mended and then called off; a car that goes, or a pedal let up, starts the count again; a candidate at the wheel with
// no examiner aboard seats them after a wait; and a game with time stopped is told from a car that is stuck.
internal static partial class Program
{
    // Looks of 0.25 s with the pedal down on a standing car; returns the stage reached at the look the seconds ran out, the first of each.
    private static StallStage StallFor(TestStallRules rules, float seconds, float pedal, float speed, float x, float z, out float mendAt, out float giveUpAt)
    {
        mendAt = giveUpAt = -1f;
        StallStage last = StallStage.None;
        float t = 0f;
        for (int i = 0; t < seconds; i++)
        {
            t += 0.25f;
            StallStage s = rules.Look(0.25f, pedal, speed, x, z);
            if (s == StallStage.Mend && mendAt < 0f)
            {
                mendAt = t;
            }
            if (s == StallStage.GiveUp && giveUpAt < 0f)
            {
                giveUpAt = t;
            }
            last = s != StallStage.None ? s : last;
        }
        return last;
    }

    private static void TestStall()
    {
        var rules = new TestStallRules();

        // A car that does not move with the accelerator held: mended at six seconds, called off at eighteen, each once.
        rules.Begin();
        StallFor(rules, 40f, 1f, 0f, 10f, 20f, out float mendAt, out float giveUpAt);
        Check(Math.Abs(mendAt - TestStallRules.MendAfter) <= 0.3f, "test stall: a car standing with the accelerator held is mended after " + TestStallRules.MendAfter + " s (was " + mendAt + ")");
        Check(Math.Abs(giveUpAt - TestStallRules.GiveUpAfter) <= 0.3f, "test stall: and the drive is called off after " + TestStallRules.GiveUpAfter + " s (was " + giveUpAt + ")");
        Check(rules.Stage == StallStage.GiveUp, "test stall: the stage stays at give up once reached");
        rules.Begin();
        int mends = 0, gives = 0;
        for (int i = 0; i < 200; i++)
        {
            StallStage s = rules.Look(0.25f, 1f, 0f, 0f, 0f);
            mends += s == StallStage.Mend ? 1 : 0;
            gives += s == StallStage.GiveUp ? 1 : 0;
        }
        Check(mends == 1 && gives == 1, "test stall: each stage is returned once, not at every look after (mend " + mends + ", give up " + gives + ")");

        // No accelerator: nothing is wrong with a car that waits at a light or is not yet asked to go.
        rules.Begin();
        StallFor(rules, 120f, 0f, 0f, 0f, 0f, out mendAt, out giveUpAt);
        Check(mendAt < 0f && giveUpAt < 0f && rules.Stage == StallStage.None, "test stall: a car standing with no accelerator held is never stuck");

        // A tap is not a hold; the count goes on only while the pedal is down, and starts again after a long rest of it.
        rules.Begin();
        StallFor(rules, 4f, 1f, 0f, 0f, 0f, out mendAt, out giveUpAt);
        StallFor(rules, TestStallRules.LetGo - 0.5f, 0f, 0f, 0f, 0f, out mendAt, out giveUpAt);
        StallFor(rules, 4f, 1f, 0f, 0f, 0f, out mendAt, out giveUpAt);
        Check(mendAt > 0f && mendAt < 4f, "test stall: a short rest keeps the seconds already held (the mend came at " + mendAt + " s of the second hold)");
        rules.Begin();
        StallFor(rules, 4f, 1f, 0f, 0f, 0f, out mendAt, out giveUpAt);
        StallFor(rules, TestStallRules.LetGo + 1f, 0f, 0f, 0f, 0f, out mendAt, out giveUpAt);
        StallFor(rules, 4f, 1f, 0f, 0f, 0f, out mendAt, out giveUpAt);
        Check(mendAt < 0f && rules.Held < TestStallRules.MendAfter, "test stall: a long rest of the pedal starts the count again");

        // A car that goes starts everything again, and says how soon it set off.
        rules.Begin();
        StallFor(rules, 4f, 1f, 0f, 5f, 5f, out mendAt, out giveUpAt);
        for (int i = 0; i < 12; i++)
        {
            rules.Look(0.25f, 1f, 3f, 5f + i * 0.75f, 5f);
        }
        Check(rules.TookOff && rules.Held == 0f && rules.Stage == StallStage.None, "test stall: a car that has gone is not stuck and the count is back to nothing");
        Check(rules.SetOffAfter >= 4f && rules.SetOffAfter < 5.5f, "test stall: it says how soon the car set off (" + rules.SetOffAfter + " s)");
        StallFor(rules, 10f, 1f, 0f, 20f, 5f, out mendAt, out giveUpAt);
        Check(Math.Abs(mendAt - TestStallRules.MendAfter) <= 0.3f, "test stall: stopped again further on, the count begins from there");

        // Rolling slowly or being nudged is not standing still: speed or metres.
        rules.Begin();
        StallFor(rules, 30f, 1f, 0.6f, 0f, 0f, out mendAt, out giveUpAt);
        Check(mendAt < 0f, "test stall: a car creeping at more than the standing speed is not stuck");
        rules.Begin();
        float x = 0f;
        for (int i = 0; i < 120; i++)
        {
            x += 0.1f;
            rules.Look(0.25f, 1f, 0.1f, x, 0f);   // 0.4 m/s of travel the speed reading missed
        }
        Check(rules.Stage == StallStage.None, "test stall: a car that is moving over the ground is not stuck whatever its speed reads");

        // The numbers hang together.
        Check(TestStallRules.MendAfter < TestStallRules.GiveUpAfter && TestStallRules.LetGo > 2f && TestStallRules.Went > 0.5f && TestStallRules.PedalAsked > 0.05f,
            "test stall: the mend comes before the give up, a rest of the pedal is longer than a tap, a car must travel further than its own jitter");

        // The examiner who never reaches the car: seated after the wait, never before, never with time stopped or a candidate not at the wheel.
        Check(!TestBoardRules.SeatNow(true, TestBoardRules.WaitForExaminer - 0.1f, false, true), "test board: the examiner is given time to walk to the car");
        Check(TestBoardRules.SeatNow(true, TestBoardRules.WaitForExaminer, false, true), "test board: after the wait with the candidate at the wheel they are taken to be aboard");
        Check(!TestBoardRules.SeatNow(false, 99f, false, true), "test board: nobody is seated before the candidate is at the wheel");
        Check(!TestBoardRules.SeatNow(true, 99f, true, true), "test board: an examiner already aboard is not seated twice");
        Check(!TestBoardRules.SeatNow(true, 99f, false, false), "test board: nothing walks while time is stopped, so no one is seated for a pause");
        Check(TestBoardRules.WaitForExaminer >= 6f && TestBoardRules.WaitForExaminer <= 20f, "test board: the wait is long enough for a walk of a few metres and short enough to be no wait");

        // Time stopped.
        Check(TestBoardRules.Frozen(0f) && TestBoardRules.Frozen(0.005f) && !TestBoardRules.Frozen(0.5f) && !TestBoardRules.Frozen(1f) && !TestBoardRules.Frozen(3f),
            "test board: time is stopped at a scale of nothing, not at a slow game");
        Check(TestBoardRules.LockLeft(true, false, false, false), "test board: time controls locked with nothing open is a lock left behind");
        Check(!TestBoardRules.LockLeft(false, false, false, false), "test board: controls free is nothing left behind (a pause is the player's)");
        Check(!TestBoardRules.LockLeft(true, true, false, false) && !TestBoardRules.LockLeft(true, false, true, false) && !TestBoardRules.LockLeft(true, false, false, true),
            "test board: the map, a menu or a panel being open is why the controls are locked, and they are left to it");
    }
}
