using System;
using System.Text;
using RealLifeOverhaul;

// Checks for the long-frame line's account of where a stutter went (Diagnostics/FrameRules.cs): the cause the figures
// point to (the main thread's own work, a wait for the last frame to be shown, another program, time outside the
// engine's steps), and the words the line gives the processor, the page faults and the engine's own figures, with a
// measure that is not available said in its place. Made-up frames like a real one of 378 ms. Part of
// the work against long frames (HitchCheck.cs). Compiled as it ships.
internal static partial class Program
{
    private static void Frames()
    {
        FrameCauses();
        FrameWords();
        FrameCost();
    }

    // A frame of 378 ms that the step timing covered whole: which parts are given, the rest unknown.
    private static FrameFigures Stutter(double wait, double outside, double thread, double others)
    {
        FrameFigures f = FrameFigures.Unknown(378.0);
        f.StepsMs = 378.0 - outside;
        f.WaitMs = wait;
        f.ThreadMs = thread;
        f.ProcessMs = thread >= 0.0 ? thread * 3.0 : -1.0;
        f.Busy = others >= 0.0 ? Math.Min(1.0, others + 0.05) : -1.0;
        f.Others = others;
        f.Faults = 300;
        f.Processors = 24;
        return f;
    }

    private static void FrameCauses()
    {
        Check(FrameRules.Cause(Stutter(362.0, 1.0, 16.0, 0.05)) == FrameCause.Display, "frames: a long wait for the last frame to be shown, the main thread idle and the machine free, is the graphics card or the display");
        Check(FrameRules.Cause(Stutter(362.0, 1.0, 16.0, 0.9)) == FrameCause.Starved, "frames: the same wait while other programs held the processor says so");
        Check(FrameRules.Cause(Stutter(2.0, 360.0, 16.0, 0.9)) == FrameCause.OtherProgram, "frames: time outside the steps, the main thread hardly run and the machine held by others, is another program");
        Check(FrameRules.Cause(Stutter(2.0, 10.0, 31.0, 0.85)) == FrameCause.OtherProgram, "frames: inside the steps but the main thread hardly run while others held the machine is another program too");
        Check(FrameRules.Cause(Stutter(2.0, 360.0, 16.0, 0.1)) == FrameCause.Outside, "frames: time outside the steps with the processor free is outside the engine");
        Check(FrameRules.Cause(Stutter(2.0, 1.0, 360.0, 0.9)) == FrameCause.Work, "frames: a main thread that ran most of the frame is its own work, whatever else ran");
        Check(FrameRules.Cause(Stutter(150.0, 1.0, 230.0, 0.1)) == FrameCause.Work, "frames: the thread's own time is weighed before the wait");
        Check(FrameRules.Cause(Stutter(100.0, 100.0, 100.0, 0.1)) == FrameCause.Spread, "frames: nothing at half the frame or more is spread over the steps");
        Check(FrameRules.Cause(FrameFigures.Unknown(378.0)) == FrameCause.Spread, "frames: with nothing measured, no cause is guessed");
        Check(FrameRules.Cause(Stutter(362.0, 1.0, -1.0, -1.0)) == FrameCause.Display, "frames: without the system's figures a long wait is still a wait");
        Check(FrameRules.Cause(FrameFigures.Unknown(0.0)) == FrameCause.Spread, "frames: a frame of no length has no cause");
        FrameFigures f = Stutter(362.0, 1.0, 16.0, 0.05);
        Check(Math.Abs(f.StepsMs + f.OutsideMs - f.SpanMs) < 1e-9 && FrameFigures.Unknown(10.0).OutsideMs < 0.0, "frames: the steps and the time outside them add up to the frame; untimed steps leave it unknown");
        bool distinct = true;
        for (int a = 0; a < 6; a++)
        {
            for (int b = a + 1; b < 6; b++)
            {
                distinct &= FrameRules.Short((FrameCause)a) != FrameRules.Short((FrameCause)b);
            }
        }
        Check(distinct, "frames: each cause has words of its own in the minute's counts");
    }

    private static string Words(in FrameFigures f, bool missing = false)
    {
        var line = new StringBuilder(512);
        string why = missing ? "not available (this system has no GetThreadTimes)" : "not measured yet";
        FrameRules.Describe(line, f, why, missing ? "not available (this system has no GetSystemTimes)" : "not measured yet",
            missing ? "not available (this system has no K32GetProcessMemoryInfo)" : "not measured yet", "not offered by this build");
        return line.ToString();
    }

    private static string Gave(string text) => " (" + text + ")";

    private static bool Plain(string text)
    {
        return text.IndexOf('\u2014') < 0 && text.IndexOf('\u2013') < 0 && !text.Contains(" - ") && text.IndexOf('!') < 0;
    }

    private static void FrameWords()
    {
        string starved = Words(Stutter(362.0, 1.0, 16.0, 0.9));
        Check(starved.Contains("the main thread ran 16 ms of it") && starved.Contains("the game 48 ms over all its threads")
              && starved.Contains("the machine's 24 processors 95% busy (other programs 90%)"), "frames: the line gives the main thread's time, the game's and the machine's load" + Gave(starved));
        Check(starved.Contains("page faults in it: 300;") && !starved.Contains("brought in"), "frames: a few hundred page faults are only counted" + Gave(starved));
        Check(starved.Contains("the engine's own figures: not offered by this build"), "frames: a build without the engine's frame timing says so" + Gave(starved));
        Check(starved.Contains("so: the game waited for the last frame to be shown while other programs held the processor"), "frames: the line ends with the cause" + Gave(starved));
        FrameFigures paged = Stutter(2.0, 1.0, 360.0, 0.1);
        paged.Faults = 45000;
        string work = Words(paged);
        Check(work.Contains("45000 (memory was being brought in") && work.Contains("so: the main thread was working for most of it"), "frames: tens of thousands of page faults say memory was being brought in" + Gave(work));
        FrameFigures gpu = Stutter(362.0, 1.0, 16.0, 0.05);
        gpu.GpuMs = 250.0;
        gpu.RenderThreadMs = 12.0;
        string card = Words(gpu);
        Check(card.Contains("the graphics card 250.0 ms and the render thread 12.0 ms at most") && card.Contains("so: the game waited for the graphics card"), "frames: the engine's own figures name the graphics card" + Gave(card));
        string missing = Words(FrameFigures.Unknown(378.0), true);
        Check(missing.Contains("the main thread's time not available (this system has no GetThreadTimes)") && missing.Contains("the machine's load not available (this system has no GetSystemTimes)")
              && missing.Contains("page faults in it: not available (this system has no K32GetProcessMemoryInfo)"), "frames: a measure the system lacks is said to be not available" + Gave(missing));
        bool plain = Plain(starved) && Plain(work) && Plain(card) && Plain(missing);
        for (int c = 0; c < 6; c++)
        {
            plain &= Plain(FrameRules.Short((FrameCause)c));
        }
        Check(plain, "frames: the line's words have no dashes and no exclamation marks");
    }

    // The cause is reckoned for every frame over 33 ms: nothing allocated, and quick.
    private static void FrameCost()
    {
        FrameFigures f = Stutter(362.0, 1.0, 16.0, 0.9);
        FrameRules.Cause(f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < 10000; i++)
        {
            f.WaitMs = i % 400;
            sum += (int)FrameRules.Cause(f);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0 && sum > 0, "frames: reckoning the cause allocates nothing (" + allocated + " bytes over 10000 frames)");
    }
}
