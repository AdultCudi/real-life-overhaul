using System;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// A look another feature wears over a finished building (DistrictBuildings.LookGround, LookStand, LookGone, LooksLoading;
    /// Scripts/Corners: a corner building's model over the copy engine's building of its type): the ground its building beneath
    /// stands on, the look stood as the building is made at a load or in play (before it joins the game's lists, so its door is
    /// where the look's entrance is when the game first reads it), and taken away when it is knocked down. Each call caught: a
    /// fault leaves the building in the copy engine's own look, said once in Player.log.
    /// COST: nothing per frame; one call a building wearing a look, at its load or its stand.
    /// </summary>
    public sealed partial class LifeDirector
    {
        /// <summary>The look a finished building on a lot wears (its record's), or null.</summary>
        public static string LookOn(string lotId)
        {
            FinishedBuilding f = _state != null ? _state.On(lotId) : null;
            return f != null && !string.IsNullOrEmpty(f.Look) ? f.Look : null;
        }

        // The declaration of a building wearing a look: its building beneath on the ground the look gives it (off the frontage pass,
        // which places only a lot's own ground), else as any other.
        private static void LookGround(MadeSpec spec, FinishedBuilding f, BuildingsLot ground)
        {
            DistrictBuildings.LookGroundCall ask = DistrictBuildings.LookGround;
            if (spec == null || f == null || string.IsNullOrEmpty(f.Look) || ask == null)
            {
                return;
            }
            try
            {
                if (ask(f.Look, f.Lot, ground, out MadeLot copy) && copy.Width > 0f && copy.Depth > 0f)
                {
                    spec.Lots = new[] { copy };
                    spec.Preferred = 0;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Uptown: the ground of the building wearing " + f.Look + " on lot " + f.Lot + " could not be read; it stands on its lot's own ground.");
            }
        }

        // The look over a building made at a load or in play, before it joins the game's lists; why not in Player.log.
        private static void WearLook(MadeBuilding made)
        {
            Func<MadeBuilding, string, string> stand = DistrictBuildings.LookStand;
            FinishedBuilding f = made != null && made.Spec != null ? _state.On(made.Spec.Id) : null;
            if (f == null || string.IsNullOrEmpty(f.Look) || stand == null)
            {
                return;
            }
            try
            {
                string why = stand(made, f.Look);
                if (why == DistrictBuildings.LookWaits)
                {
                    _looksWait = true;   // asked once a second (LooksTicked) until its parts are read
                    Logger?.Info("Uptown: the building at " + made.Address + " waits for " + f.Look + "'s parts: " + why + ".");
                    return;
                }
                if (why != null)
                {
                    Logger?.Info("Uptown: the building at " + made.Address + " keeps the copy engine's look, not " + f.Look + ": " + why + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Uptown: the look " + f.Look + " over the building at " + made.Address + " failed; it keeps the copy engine's look.");
            }
        }

        private static bool _looksWait;   // a look waits for its parts: LooksTicked once a second (OnUpdate)

        // Once a second while a look waits for its parts.
        private static void LooksTicked()
        {
            Func<bool> tick = DistrictBuildings.LooksTick;
            try
            {
                _looksWait = tick != null && !tick();
            }
            catch (Exception e)
            {
                _looksWait = false;
                Logger?.Error(e);
            }
        }

        // A building knocked down: the look over it taken away with it.
        private static void LookGone(MadeBuilding made)
        {
            Action<MadeBuilding> gone = DistrictBuildings.LookGone;
            if (made == null || gone == null)
            {
                return;
            }
            try
            {
                gone(made);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A load's buildings about to be set in place: the looks let go what they kept of the last city, behind the loading screen.
        private static void LooksLoading()
        {
            Action loading = DistrictBuildings.LooksLoading;
            if (loading == null)
            {
                return;
            }
            try
            {
                loading();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
