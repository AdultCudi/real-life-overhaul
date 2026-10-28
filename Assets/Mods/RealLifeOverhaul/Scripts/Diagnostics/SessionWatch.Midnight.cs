using System;
using System.Globalization;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The session record, part: the game's midnight. The game runs every feature's day in the one frame it turns the day
    /// in (GameManager.NewDay calls the hour's and the day's events), and the long-frame line names only its three largest
    /// parts; this line names every part of the mod's in that frame, whether the frame was long or not, so a day handler
    /// that grows is seen by name. COST: one reference comparison a frame (the event held), a line a game day.
    /// </summary>
    public sealed partial class SessionWatch
    {
        private const double MidnightWanted = 20.0;   // milliseconds of the mod's in the midnight frame once its code is compiled ahead (Core/DayWarm); RLO-PERF-020
        private const double MidnightPart = 0.1;      // milliseconds a part must take to be named
        private const int MidnightParts = 24;         // parts named at most

        private Action _onDay, _daySeen;
        private int _dayFrame = -1;                   // the frame the game turned the day in
        private int _queuedSeen;                      // DaySpread's count of steps queued, at the last midnight line

        // In OnUpdate, after HotSpots.Settle: the frame before this one complete.
        private void Midnight(float took)
        {
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay ??= Guard.Wrap(FrameGuard, NewDay), ref _daySeen);
            if (_dayFrame < 0 || _dayFrame != Time.frameCount - 1)
            {
                return;
            }
            int frame = _dayFrame;
            _dayFrame = -1;
            double own = HotSpots.FrameMs(frame);
            bool warm = DayWarm.Ready;
            _line.Length = 0;
            _line.Append("Session check (the game's midnight, day ").Append(SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0)
                 .Append("): the mod's scripts took ").Append(own.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms of the frame the game turned the day in (")
                 .Append((took * 1000f).ToString("F0", CultureInfo.InvariantCulture)).Append(" ms in all); by part (a part inside another counts under both): ");
            int named = 0;
            for (int k = 0; k < MidnightParts && HotSpots.FrameTop(frame, k, out int slot, out double ms) && ms >= MidnightPart; k++)
            {
                _line.Append(named++ == 0 ? string.Empty : ", ").Append(HotSpots.Name(slot)).Append(' ').Append(ms.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms");
            }
            if (named == 0)
            {
                _line.Append("none over ").Append(MidnightPart.ToString("F1", CultureInfo.InvariantCulture)).Append(" ms");
            }
            int queued = DaySpread.Shared.Queued - _queuedSeen;
            _queuedSeen = DaySpread.Shared.Queued;
            _line.Append("; left for the frames after: ").Append(queued).Append(" step(s) (Core/DaySpread), ").Append(DayLines.HeldToday).Append(" log line(s) (Core/DayLines)")
                 .Append("; the day's code compiled ahead: ").Append(warm ? "yes" : "not yet (the first seconds after a load)")
                 .Append(" (wanted: under ").Append(MidnightWanted.ToString("F0", CultureInfo.InvariantCulture)).Append(" ms once it is): ")
                 .Append(!warm ? "not judged" : own < MidnightWanted ? "PASS" : "FAIL, over");
            Logger?.Info(_line.ToString());
        }

        private void NewDay()
        {
            _dayFrame = Time.frameCount;
        }

        private void LetGoOfDay()
        {
            if (_onDay != null)
            {
                GlobalEvents.onNewDay -= _onDay;
                _daySeen = null;
            }
        }
    }
}
