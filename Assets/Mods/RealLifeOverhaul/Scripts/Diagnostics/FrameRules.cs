using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What a long frame's line can tell apart: the main thread's own work, a wait for the frame before it to be shown, another program, time outside the engine's steps.</summary>
    public enum FrameCause
    {
        Spread = 0,        // over the engine's steps, none of the others
        Work = 1,          // the main thread ran most of the frame: the engine's own work or a script's
        Display = 2,       // it waited for the last frame to be shown: the graphics card, the render thread or the display
        Starved = 3,       // it waited for the last frame to be shown while other programs held the processor
        OtherProgram = 4,  // it did not run, and other programs held the processor
        Outside = 5,       // the time was outside the engine's steps, with the processor free
        Waited = 6,        // the main thread hardly ran inside the engine's steps and nothing else held the processor: blocked, not working
        TimeJump = 7,      // the engine's frame time was far longer than any stretch of the machine's clock between its marks: the game's time jumped
    }

    /// <summary>How the engine's time for a frame sits against the mod's own clock (see <see cref="FrameRules.Reading"/>).</summary>
    public enum FrameTime
    {
        Unmeasured = 0,   // the steps are not timed
        Whole = 1,        // the mod's clock between its marks gives the engine's figure: the steps above are that frame's
        Later = 2,        // the figure is the stretch between the engine's own reads of the time, a little after the mod's mark
        Earlier = 3,      // the figure is that of the frame before the one the mod's last mark closes: the engine counts it a frame late
        Jump = 4,         // no stretch of the mod's clock gives the figure: the engine's time moved more than the machine's clock
    }

    /// <summary>What the game showed during a frame, as the session record tells a stutter the player saw from one they did not.</summary>
    public enum FrameScreen
    {
        Clear = 0,     // the game was being played and seen
        Black = 1,     // the game's own black screen (UI.UiFader) was drawn at full strength as the frame began: a walk into or out of a building, sleep
        Loading = 2,   // the game's loading screen (UI.Load.LoadingScreen) was up, or still fading out after the save had loaded
        Fading = 3,    // the game's own black screen was fading out or back in, the picture showing through it: seen (its fades at a door take 0.2 s each way)
    }

    /// <summary>The figures of one frame, as the session record has them (Diagnostics/FrameSteps, Diagnostics/MachineClock). A figure below zero is not known.</summary>
    public struct FrameFigures
    {
        public double SpanMs;          // the frame, from one time update to the next, by the mod's own clock
        public double StepsMs;         // in all the engine's steps that were timed (the wait among them)
        public double WaitMs;          // of them, waiting for the last frame to be shown
        public double ThreadMs;        // the main thread's own processor time in the frame
        public double ProcessMs;       // the game's processor time in the frame, every thread of it
        public double Busy;            // how busy the machine's processors were, 0 to 1
        public double Others;          // of that, other programs' share, 0 to 1
        public long Faults;            // the game's page faults in the frame
        public int Processors;
        public double GpuMs;           // the engine's own figures (FrameTimingManager), the worst of the last few frames it has
        public double RenderThreadMs;
        public bool TimeJumped;        // the engine's frame time is far longer than the machine's clock saw pass (<see cref="FrameTime.Jump"/>)

        public double OutsideMs => StepsMs < 0.0 ? -1.0 : SpanMs > StepsMs ? SpanMs - StepsMs : 0.0;   // below zero when the steps were not timed

        /// <summary>Every figure unknown: what a frame has before anything was measured.</summary>
        public static FrameFigures Unknown(double spanMs)
        {
            return new FrameFigures
            {
                SpanMs = spanMs, StepsMs = -1.0, WaitMs = -1.0, ThreadMs = -1.0, ProcessMs = -1.0, Busy = -1.0, Others = -1.0, Faults = -1L, GpuMs = -1.0, RenderThreadMs = -1.0,
            };
        }
    }

    /// <summary>
    /// The game-free part of a long frame's line: the cause the figures point to, and the words for the processor, the
    /// page faults and the cause. Arithmetic over a dozen numbers, checked offline; only a written line is allocated.
    /// </summary>
    public static class FrameRules
    {
        public const double ThreadWorked = 0.6;   // share of the frame the main thread must have run for the frame to be its own work
        public const double MostOf = 0.5;          // share of the frame the wait, or the time outside the steps, must be to be the cause
        public const double OthersHeld = 0.6;      // share of the machine's processors other programs must have held to be the cause
        public const double Idle = 0.2;            // share of the frame under which the main thread's own time says it was blocked, not working (its clock ticks every 15.6 ms)
        public const double AgreeMs = 2.0;         // milliseconds by which two clocks may differ over one frame and still agree
        public const double AgreeShare = 0.1;      // ...or this share of the longer figure
        public const long Paging = 20000;          // page faults in one frame from which the line says memory was being brought in (some 80 MB of 4 KB pages)

        /// <summary>The cause the figures point to. Unknown figures are left out of the reasoning, never guessed.</summary>
        public static FrameCause Cause(in FrameFigures f)
        {
            if (f.TimeJumped)
            {
                return FrameCause.TimeJump;
            }
            double span = f.SpanMs;
            if (span <= 0.0)
            {
                return FrameCause.Spread;
            }
            bool othersHeld = f.Others >= OthersHeld;
            if (f.ThreadMs >= 0.0 && f.ThreadMs >= ThreadWorked * span)
            {
                return FrameCause.Work;
            }
            if (f.WaitMs >= MostOf * span)
            {
                return othersHeld ? FrameCause.Starved : FrameCause.Display;
            }
            bool idle = f.ThreadMs >= 0.0 && f.ThreadMs < MostOf * span;   // the main thread did not run for most of it
            if (othersHeld && (idle || f.StepsMs < 0.0 || f.OutsideMs >= MostOf * span))
            {
                return FrameCause.OtherProgram;
            }
            if (f.StepsMs >= 0.0 && f.OutsideMs >= MostOf * span)
            {
                return FrameCause.Outside;
            }
            if (f.ThreadMs >= 0.0 && f.StepsMs >= 0.0 && f.ThreadMs < Idle * span)
            {
                return FrameCause.Waited;   // a script's time in such a frame is the wait it was caught in, not its own work
            }
            return FrameCause.Spread;
        }

        /// <summary>
        /// Which stretch of the mod's own clock the engine's frame time (Time.unscaledDeltaTime) is. The mod marks the clock just after
        /// the engine's wait for the last frame, the engine may read its time a few steps later, and a stall between the two (or one the engine
        /// counts a frame late) is then in the engine's figure and in no frame of the mod's clock. Negative figures are not known.
        /// </summary>
        public static FrameTime Reading(double tookMs, double spanMs, double readMs, double beforeMs)
        {
            if (spanMs < 0.0)
            {
                return FrameTime.Unmeasured;
            }
            if (Agree(tookMs, spanMs))
            {
                return FrameTime.Whole;
            }
            if (readMs >= 0.0 && Agree(tookMs, readMs))
            {
                return FrameTime.Later;
            }
            if (beforeMs >= 0.0 && Agree(tookMs, beforeMs))
            {
                return FrameTime.Earlier;
            }
            return FrameTime.Jump;
        }

        /// <summary>Two figures of one frame from two clocks: within 2 ms, or a tenth, of each other.</summary>
        public static bool Agree(double aMs, double bMs)
        {
            return Math.Abs(aMs - bMs) <= Math.Max(AgreeMs, AgreeShare * Math.Max(aMs, bMs));
        }

        /// <summary>The clause after the steps in a long frame's line that says which stretch the engine's figure is, or nothing when the clocks agree.</summary>
        public static void Say(StringBuilder line, FrameTime reading, double tookMs, double spanMs, double readMs, double beforeMs)
        {
            switch (reading)
            {
                case FrameTime.Whole:
                    if (Math.Abs(spanMs - tookMs) >= AgreeMs)
                    {
                        line.Append(" (by the steps' own clock the frame took ").Append(spanMs.ToString("F0")).Append(" ms)");
                    }
                    break;
                case FrameTime.Later:
                    line.Append(" (the engine reads its time a few steps after the mod's mark, in Initialization.PlayerUpdateTime, and by the clock between its reads the frame took ")
                        .Append(readMs.ToString("F0")).Append(" ms, against ").Append(spanMs.ToString("F0")).Append(" ms between the marks: ")
                        .Append("the rest went in the engine's first steps of the frame, between the two)");
                    break;
                case FrameTime.Earlier:
                    line.Append(" (the engine counts this time a frame late: the steps above are those of the frame before, which took ").Append(beforeMs.ToString("F0"))
                        .Append(" ms by the steps' own clock; the frame after it took ").Append(spanMs.ToString("F0")).Append(" ms)");
                    break;
                case FrameTime.Jump:
                    line.Append(" (the engine's frame time is longer than the machine's clock saw pass: ").Append(spanMs.ToString("F0")).Append(" ms between the mod's marks");
                    if (readMs >= 0.0)
                    {
                        line.Append(", ").Append(readMs.ToString("F0")).Append(" ms between the engine's reads of its time");
                    }
                    if (beforeMs >= 0.0)
                    {
                        line.Append(", ").Append(beforeMs.ToString("F0")).Append(" ms in the frame before");
                    }
                    line.Append(")");
                    break;
            }
        }

        /// <summary>The cause in a few words, for the line a minute's counts.</summary>
        public static string Short(FrameCause cause)
        {
            switch (cause)
            {
                case FrameCause.Work: return "the main thread's own work";
                case FrameCause.Display: return "waits for the last frame to be shown";
                case FrameCause.Starved: return "such waits while other programs held the processor";
                case FrameCause.OtherProgram: return "other programs holding the processor";
                case FrameCause.Outside: return "time outside the engine's steps";
                case FrameCause.Waited: return "the main thread blocked inside the engine's steps";
                case FrameCause.TimeJump: return "the game's time jumping (more than the machine's clock saw pass)";
                default: return "spread over the engine's steps";
            }
        }

        /// <summary>"; the processor: ...; page faults in it: ...; the engine's own figures: ...; so: ...", a missing measure said in its place.</summary>
        public static void Describe(StringBuilder line, in FrameFigures f, string threadMissing, string machineMissing, string faultsMissing, string timingMissing)
        {
            line.Append("; the processor: ");
            if (f.ThreadMs >= 0.0)
            {
                line.Append("the main thread ran ").Append(f.ThreadMs.ToString("F0")).Append(" ms of it");
            }
            else
            {
                line.Append("the main thread's time ").Append(threadMissing);
            }
            if (f.ProcessMs >= 0.0)
            {
                line.Append(", the game ").Append(f.ProcessMs.ToString("F0")).Append(" ms over all its threads");
            }
            if (f.Busy >= 0.0)
            {
                line.Append(", the machine's ").Append(f.Processors).Append(" processors ").Append((f.Busy * 100.0).ToString("F0")).Append("% busy");
                if (f.Others >= 0.0)
                {
                    line.Append(" (other programs ").Append((f.Others * 100.0).ToString("F0")).Append("%)");
                }
            }
            else
            {
                line.Append(", the machine's load ").Append(machineMissing);
            }
            line.Append("; page faults in it: ");
            if (f.Faults >= 0L)
            {
                line.Append(f.Faults);
                if (f.Faults >= Paging)
                {
                    line.Append(" (memory was being brought in: paged back from the disk, or touched for the first time)");
                }
            }
            else
            {
                line.Append(faultsMissing);
            }
            line.Append("; the engine's own figures: ");
            if (f.GpuMs >= 0.0)
            {
                line.Append("the graphics card ").Append(f.GpuMs.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms and the render thread ")
                    .Append(Max0(f.RenderThreadMs).ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" ms at most in the last frames it has (they come a few frames late)");
            }
            else
            {
                line.Append(timingMissing);
            }
            line.Append("; so: ");
            switch (Cause(f))
            {
                case FrameCause.Work:
                    line.Append("the main thread was working for most of it (the engine's own work, or a script's: the steps above say which)");
                    break;
                case FrameCause.Display:
                    line.Append(f.GpuMs >= MostOf * f.SpanMs
                        ? "the game waited for the graphics card, which took that long over a frame"
                        : "the game waited for the last frame to be shown (the render thread handing it to the graphics card, the graphics card, or the display)");
                    break;
                case FrameCause.Starved:
                    line.Append("the game waited for the last frame to be shown while other programs held the processor (the render thread was likely kept off it)");
                    break;
                case FrameCause.OtherProgram:
                    line.Append("another program held the processor: the game's main thread was hardly let run");
                    break;
                case FrameCause.Outside:
                    line.Append("the time was outside the engine's steps with the processor free (the window's messages, the system or the graphics driver held the game)");
                    break;
                case FrameCause.TimeJump:
                    line.Append("the game's frame time jumped: the machine's clock did not see that much time pass, so nothing took that long, "
                                + "but everything on screen moved on in one big step, which looks like a stutter");
                    break;
                case FrameCause.Waited:
                    line.Append("the main thread was blocked, not working: it hardly ran inside the steps above and no other program held the processor "
                                + "(a wait on the graphics driver, the disk or another thread), so the time a script is given in this frame is the wait it was caught in, not its work");
                    break;
                default:
                    line.Append("it was spread over the engine's steps above");
                    break;
            }
        }

        /// <summary>
        /// What the screen was during a frame, from the looks at its start and at the next frame's: the loading screen at
        /// either look; else black if the black screen was at full strength as it began; else fading (seen) if it faded at
        /// either look. Only <see cref="Unseen"/> screens hide a stutter.
        /// </summary>
        public static FrameScreen Screen(bool faded, bool fadedBefore, bool coveredBefore, bool loading, bool loadingBefore)
        {
            return loading || loadingBefore ? FrameScreen.Loading
                : coveredBefore ? FrameScreen.Black
                : faded || fadedBefore ? FrameScreen.Fading
                : FrameScreen.Clear;
        }

        /// <summary>True for a screen on which a long frame is not seen as a stutter: the loading screen, or the black screen at full strength.</summary>
        public static bool Unseen(FrameScreen screen)
        {
            return screen == FrameScreen.Loading || screen == FrameScreen.Black;
        }

        /// <summary>The words of the long-frame line for the screen (constant texts).</summary>
        public static string ScreenWords(FrameScreen screen)
        {
            switch (screen)
            {
                case FrameScreen.Loading:
                    return "its screen was black or fading (its loading screen, up or fading out after the save had loaded: not seen as a stutter)";
                case FrameScreen.Black:
                    return "its screen was black (its own walk into or out of a building: not seen as a stutter)";
                case FrameScreen.Fading:
                    return "its screen was fading, the picture showing through (its own walk into or out of a building: seen as a stutter)";
                default:
                    return "its screen was clear";
            }
        }

        private static double Max0(double v) => v > 0.0 ? v : 0.0;
    }
}
