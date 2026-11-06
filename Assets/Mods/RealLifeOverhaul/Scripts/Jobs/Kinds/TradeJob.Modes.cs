using RealLifeOverhaul.BuildTrades;

namespace RealLifeOverhaul
{
    /// <summary>Which of the trade's games a call goes to: one switch each, so a new mode is a case here and a file of its own.</summary>
    public sealed partial class TradeJob
    {
        private void BeginMode(float now)
        {
            switch (_trade.Mode)
            {
                case TradeMode.Match: BeginMatch(now); break;
                case TradeMode.Judge: BeginJudge(now); break;
                case TradeMode.Survey: BeginSurvey(now); break;
                case TradeMode.Watch: BeginWatch(now); break;
                default: BeginTiming(now); break;
            }
        }

        private void TickMode(float now)
        {
            switch (_trade.Mode)
            {
                case TradeMode.Match: TickMatch(now); break;
                case TradeMode.Judge: TickJudge(now); break;
                case TradeMode.Survey: TickSurvey(now); break;
                case TradeMode.Watch: TickWatch(now); break;
                default: TickTiming(now); break;
            }
        }

        // True once nothing of the shift is left to answer (an inspection's bribe): the shift may end when its units are done.
        private bool Settled() => _trade.Mode != TradeMode.Judge || JudgeSettled();

        private void ClockStartedMode()
        {
            _timing = false;
            _asking = false;
            _reading = false;
            _noise = false;
            _bar?.Show(false);
            Lines(null, null);
            if (_trade.Mode == TradeMode.Survey)
            {
                Ctx.Self.Free();
            }
        }

        private void ClockEndedMode()
        {
            if (_trade.Mode == TradeMode.Survey)
            {
                _point = DoneCount;   // the clock surveyed the next corners in turn
            }
            else if (_trade.Mode == TradeMode.Watch && DoneCount < _units)
            {
                NewRound(UnityEngine.Time.time);
            }
            _stroke = 0;
            _worth = 0f;
            _misses = 0;
        }

        private void EndMode(JobEnd why)
        {
            _asking = false;
            if (_reading)
            {
                _reading = false;
                Ctx.Self.Free();
            }
            Lines(null, null);
        }
    }
}
