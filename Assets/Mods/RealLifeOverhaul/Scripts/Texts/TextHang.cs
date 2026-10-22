using System;

namespace RealLifeOverhaul.Texts
{
    /// <summary>What a look at the server's slot (GET /slots) said, after a reply that did not come in its time.</summary>
    public enum TextSlotLook
    {
        Idle,          // it answered and its slot is free: the request given up was cancelled, the server is well (a slow moment)
        Busy,          // it answered and its slot is still at work on the request given up
        Silent,        // no answer within the look's time: llama.cpp answers it from the loop that runs the model, so that loop is stuck
        Unreadable     // an answer without the slot's state (an error, or another server's reply): no verdict from it
    }

    /// <summary>
    /// When a server that did not answer is taken as hung, and how often it is started again. A slow machine looks alike
    /// for one reply, so a missed reply is only a hang if the server's slot is still at work, or it does not answer, after
    /// a probe of a few replies' time at the measured speed (a well server cancels within a second of the connection
    /// closing). A hung or stopped server is started again in the background, at most <see cref="RestartsMost"/> times a
    /// session; the written lines are used meanwhile. Game-free.
    /// COST: arithmetic; the probe is the worker's, a few local requests after a missed reply, never on the game's thread.
    /// </summary>
    public static class TextHang
    {
        public const int RestartsMost = 3;          // restarts a session at most (a server that hangs each time is not started for ever)
        public const double ProbeReplies = 3.0;     // the probe waits this many ordinary replies at the measured speed...
        public const double ProbeLeast = 6.0;       // ...but at least this many seconds: the cancel within llama.cpp's 1 s of polling, then its batch
        public const double ProbeMost = 30.0;       // ...and at most this many, for a machine past the speed test's cap
        public const double LookEvery = 0.5;        // seconds between two looks at the slot within a probe

        /// <summary>Seconds a probe waits for the slot to come free, by the speed measured; the most when null or capped.</summary>
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

        /// <summary>The state of slot 0, the runner's only one, from llama.cpp's answer to GET /slots; null is no answer.</summary>
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

        /// <summary>True while a hung or stopped server may be started again, after this many restarts this session.</summary>
        public static bool MayRestart(int restarts) => restarts >= 0 && restarts < RestartsMost;

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
