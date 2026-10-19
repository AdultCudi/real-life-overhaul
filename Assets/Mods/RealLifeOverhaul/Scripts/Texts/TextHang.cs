using System;

namespace RealLifeOverhaul.Texts
{
    /// <summary>What a look at the server's slot (GET /slots) said, after a reply that did not come in its time.</summary>
    public enum TextSlotLook
    {
        Idle,          // it answered and its slot is free: the request given up was cancelled, the server is well (a slow moment)
        Busy,          // it answered and its slot is still at work on the request given up
        Silent,        // no answer within the look's time (llama.cpp answers it from the loop that runs the model: that loop is stuck)
        Unreadable     // an answer without the slot's state (an error, or another server's reply): no verdict from it
    }

    /// <summary>
    /// When a server that did not answer is taken as hung, and how often it is started again. llama.cpp b11662 can hang
    /// on one request and stay hung: its loop never finishes the request and takes no other, and every later request
    /// times out (the v6 pilot: one request of 200 with both Qwen 3.5 0.8B models, on the processor and on a GPU; a
    /// restart answered it at once). A slow machine looks alike for one reply, so a reply that does not come is never a
    /// hang by itself:
    ///
    ///   probe     after a reply that did not come in its time, the worker asks the server for its slot (GET /slots) until
    ///             <see cref="ProbeSeconds"/>. A well server has cancelled the request within a second of the mod closing
    ///             its connection (llama.cpp's own polling) and finished the batch it was in, so its slot is free: a slow
    ///             moment, nothing more. A slot still at work past the probe's time, or no answer at all, is a hang
    ///   time      the probe waits <see cref="ProbeReplies"/> ordinary replies at this machine's measured speed
    ///             (TextSpeedResult.Estimate), within <see cref="ProbeLeast"/> and <see cref="ProbeMost"/> seconds: a slow
    ///             machine's longest batch fits in it, so a slow machine is never taken as hung and never reloaded
    ///   restart   a hung server, or one that stopped by itself, is killed and started again by the worker, in the
    ///             background, at most <see cref="RestartsMost"/> times a session (one runner: the pack, device and threads as
    ///             started); the written lines are used meanwhile. Past that the runner stops, says so in the log, and the
    ///             written lines stay until the pack is switched off and on
    ///
    /// Game-free (EconomyCheck, TextsHangChecks.cs). COST: arithmetic; the probe is the worker's, a few local requests
    /// after a missed reply, never on the game's thread.
    /// </summary>
    public static class TextHang
    {
        public const int RestartsMost = 3;          // restarts a session at most (a server that hangs each time is not started for ever)
        public const double ProbeReplies = 3.0;     // the probe waits this many ordinary replies at the measured speed...
        public const double ProbeLeast = 6.0;       // ...but at least this many seconds (the cancel within llama.cpp's 1 s of polling, then its batch)
        public const double ProbeMost = 30.0;       // ...and at most this many (a machine past the speed test's cap)
        public const double LookEvery = 0.5;        // seconds between two looks at the slot within a probe

        /// <summary>Seconds a probe waits for the slot to come free, by the speed measured (null or capped: the most).</summary>
        public static double ProbeSeconds(TextSpeedResult speed)
        {
            if (speed == null || speed.Capped)
            {
                return ProbeMost;
            }
            double estimate = speed.Estimate;
            if (double.IsNaN(estimate) || double.IsInfinity(estimate) || estimate >= double.MaxValue)
            {
                return ProbeMost;
            }
            return Math.Max(ProbeLeast, Math.Min(ProbeMost, estimate * ProbeReplies));
        }

        /// <summary>
        /// The slot's state from llama.cpp's answer to GET /slots (a list of slots, each with "id" and "is_processing"):
        /// slot 0's, the only one the runner starts (-np 1). Null (no answer) is <see cref="TextSlotLook.Silent"/>.
        /// </summary>
        public static TextSlotLook Read(string slots)
        {
            if (slots == null)
            {
                return TextSlotLook.Silent;
            }
            int first = 0;
            while (first < slots.Length && char.IsWhiteSpace(slots[first]))
            {
                first++;
            }
            if (first == slots.Length || slots[first] != '[')
            {
                return TextSlotLook.Unreadable;
            }
            const string field = "\"is_processing\"";
            int at = slots.IndexOf(field, StringComparison.Ordinal);
            if (at < 0)
            {
                return TextSlotLook.Unreadable;
            }
            int i = at + field.Length;
            while (i < slots.Length && (char.IsWhiteSpace(slots[i]) || slots[i] == ':'))
            {
                i++;
            }
            if (string.CompareOrdinal(slots, i, "true", 0, 4) == 0)
            {
                return TextSlotLook.Busy;
            }
            return string.CompareOrdinal(slots, i, "false", 0, 5) == 0 ? TextSlotLook.Idle : TextSlotLook.Unreadable;
        }

        /// <summary>True when the probe ends with the server hung: its slot still at work past the probe's time, or no answer.</summary>
        public static bool Hung(TextSlotLook last) => last == TextSlotLook.Busy || last == TextSlotLook.Silent;

        /// <summary>True when the probe may end early: the slot is free, or the server answered in a way that gives no verdict.</summary>
        public static bool Settled(TextSlotLook look) => look == TextSlotLook.Idle || look == TextSlotLook.Unreadable;

        /// <summary>True while a hung or stopped server may be started again, with this many restarts made this session.</summary>
        public static bool MayRestart(int restarts) => restarts >= 0 && restarts < RestartsMost;

        /// <summary>The look as the log names it.</summary>
        public static string Name(TextSlotLook look)
        {
            switch (look)
            {
                case TextSlotLook.Idle: return "its slot free";
                case TextSlotLook.Busy: return "its slot still at work";
                case TextSlotLook.Silent: return "no answer";
                default: return "an answer without the slot's state";
            }
        }
    }
}
