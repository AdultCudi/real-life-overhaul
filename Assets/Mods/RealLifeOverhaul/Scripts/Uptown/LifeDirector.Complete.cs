using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Every building finished on the district's ground (the player's, a hired build, a rival's, the city's developers', a
    /// redevelopment's) checked as a whole building of the game's (plans/build-mode.md section 4): at the moment it stands in
    /// play and again at every load, one "Uptown check (a whole building, ...)" line each in Player.log with every item PASS or
    /// FAIL (CompleteRules), and a notice when any fails. A city's building with no owner the game can name is given the rival
    /// the game's own generator would give it first (never in a Going Public session). COST: nothing per frame; a pass over
    /// the save's registrations for each building checked, on a stand and once a load behind the loading screen.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly List<string> OwnerRivals = new List<string>(16);
        private static bool _incompleteSaid;

        /// <summary>One building standing on a lot checked now; false when it fails an item or does not stand. The test's and the bridge's way in.</summary>
        public static bool CompleteNow(string lotId, string when)
        {
            FinishedBuilding f = _state != null ? _state.On(lotId) : null;
            MadeBuilding made = MadeBuildings.Get(lotId);
            if (f == null || made == null || !Standing.Contains(made))
            {
                Logger?.Warn("Uptown check (a whole building, " + when + ", lot " + (lotId ?? "(none)") + "): no finished building stands there now: FAIL");
                return false;
            }
            return CompleteOne(made, f, when);
        }

        /// <summary>Every finished building standing now checked; returns how many failed an item.</summary>
        public static int CompleteAll(string when)
        {
            int failed = 0, checkedCount = 0;
            for (int i = 0; _state != null && i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                FinishedBuilding f = _state.On(made.Spec.Id);
                if (f == null)
                {
                    continue;   // a building of the plan from the first day: the generator's, checked by the district's census
                }
                checkedCount++;
                failed += CompleteOne(made, f, when) ? 0 : 1;
            }
            if (checkedCount > 0)
            {
                Logger?.Info("Uptown: " + checkedCount + " finished building(s) checked as whole buildings at " + when + ", " + failed + " with an item failed.");
            }
            if (failed > 0 && !_incompleteSaid)
            {
                _incompleteSaid = true;   // once a session: Player.log names every item
                LifeGame.SayIncomplete(failed);
            }
            return failed;
        }

        private static bool CompleteOne(MadeBuilding made, FinishedBuilding f, string when)
        {
            try
            {
                BuildingsLot lot = default;
                int planNumber = FindLot(made.Spec.Id, out lot) ? lot.Number : -1;
                if (f.Owner == FinishOwner.City && !GoingPublic.InSession)
                {
                    LifeGame.MendOwner(made, OwnerRivals);   // the owner the game's own generator would have given it
                }
                CompleteFacts facts = LifeGame.CompleteFacts(made, f.Owner == FinishOwner.Player, f.Type, f.Size, planNumber);
                int passed = CompleteRules.Passed(facts);
                string line = CompleteRules.Line(when, made.Address, facts, passed);
                if (CompleteRules.All(passed))
                {
                    Logger?.Info(line);
                    return true;
                }
                Logger?.Warn(line);
                return false;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }
    }
}
