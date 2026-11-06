using System.Collections;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The day-end panel's and the top bar's figure: the day's result, with the mod's money (<see cref="DayRules"/>), once a day.</summary>
    public sealed partial class EconomyDirector
    {
        private const float ResultStep = 0.05f;   // s between looks at the day-end panel while it fades in: finer than the row's own fade, which starts when the game writes its figure
        private const float ResultGiveUp = 8f;    // s that the panel is waited for; it may never come (a job's own summary, a death)
        private const int ResultFrames = 6;       // frames after the panel is ready that the labels are looked at again, whichever of the game and the mod writes last
        private bool _resultShown;                // the top bar shows the day's result with the mod's money, not the game's figure
        private float _resultGame;                // ...and the game's own figure it goes back to

        // The day has ended and its statement is up to date: the panel and the top bar are written when the game has written them.
        private void ShowDayResult(int day)
        {
            if (Settings.EconomyDayResult && Save != null)
            {
                StartCoroutine(Guard.Routine(DayResultRoutine(day, true)));
            }
        }

        // The top bar after a load: it shows yesterday's figure of the game until the first day ends.
        private void ShowDayResultAtStart()
        {
            if (Settings.EconomyDayResult && Save != null && Save.Day > 1)
            {
                StartCoroutine(Guard.Routine(DayResultRoutine(Save.Day - 1, false)));
            }
        }

        // The option was switched off: the top bar goes back to the game's own figure at once.
        private void KeepDayResult()
        {
            if (_resultShown && !Settings.EconomyDayResult)
            {
                _resultShown = false;
                DayResult.WriteBar(_resultGame);
            }
        }

        private IEnumerator DayResultRoutine(int day, bool panel)
        {
            var step = new WaitForSecondsRealtime(ResultStep);
            float waited = 0f;
            if (panel)
            {
                // the panel fades in over a second; the game writes its figure the moment that ends, and the row that holds it fades in after
                while (!DayResult.PanelReady() && waited < ResultGiveUp)
                {
                    yield return step;
                    waited += ResultStep;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(1f);   // the top bar sets itself when the game is loaded
            }
            if (!Settings.EconomyDayResult || !DayResult.Compute(day, out DayFigure figure, out string problem))
            {
                yield break;
            }
            float mod = figure.Mod;
            string panelText = DayResult.PanelText(mod), barText = DayResult.BarText(mod);
            bool onPanel = panel && DayResult.PanelReady();
            for (int frame = 0; frame < ResultFrames; frame++)
            {
                if (onPanel && !DayResult.PanelShows(panelText))
                {
                    DayResult.WritePanel(mod);
                }
                if (!DayResult.BarShows(barText))
                {
                    DayResult.WriteBar(mod);
                }
                yield return null;
            }
            _resultShown = true;
            _resultGame = figure.Game;
            if (problem == null && !DayResult.BarShows(barText))
            {
                problem = "the top bar does not show it";
            }
            if (problem == null && onPanel && !DayResult.PanelShows(panelText))
            {
                problem = "the day-end panel does not show it";
            }
            if (Verbose || problem != null)
            {
                Logger?.Info(DayRules.Verdict(figure, problem));
            }
        }
    }
}
