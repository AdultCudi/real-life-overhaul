using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A meal cooked with the partner (Family's "Cook together", through Wiring/FamilyLinks): the cooking list as ever, once
    /// the conversation is over, from what the home's fridges hold or the leftovers; the player cooks it at the cooker with
    /// the cooking's own animation (CookActivity), and the partner eats a portion that would otherwise be put by. Family hears
    /// once: served, or nothing cooked (the list closed without a dish, a dish that could not be started, cooking stopped).
    /// COST: nothing per frame; a few fields on a click.
    /// </summary>
    public static class CookingTogether
    {
        private static Action<bool> _done;
        private static float _askBy;   // real time by which the conversation must be over and the list asked for; 0: asked

        /// <summary>True from the asking until Family has heard how it went.</summary>
        public static bool Waiting => _done != null;

        /// <summary>Asks for the cooking list once the player is free; false, and nothing asked, where Home cannot cook here.</summary>
        public static bool Start(Action<bool> done)
        {
            HomeDirector host = HomeDirector.Instance;
            if (host == null || done == null || !HomeState.Loaded || !Settings.HomeLife || !Settings.HomeRoutines || !HomeTidy.AtHome())
            {
                return false;
            }
            Finish(false);   // (an earlier one never settled)
            _done = done;
            _askBy = UnityEngine.Time.unscaledTime + 120f;   // the list once the conversation is closed (Tick)
            return true;
        }

        /// <summary>Per frame from the director while one waits: the cooking list asked for once no conversation or menu is open.</summary>
        public static void Tick(HomeDirector host)
        {
            if (_askBy <= 0f)
            {
                return;
            }
            if (_done == null)
            {
                _askBy = 0f;
                return;
            }
            if (DialogController.current == null && !ThirdPersonCamera.IsUiOpen())
            {
                _askBy = 0f;
                host.Wish(HomeWish.Cook);
            }
            else if (UnityEngine.Time.unscaledTime > _askBy)
            {
                _askBy = 0f;
                Finish(false);   // the conversation never closed: nothing cooked
            }
        }

        /// <summary>Family hears how it went, once; nothing if no meal together was asked for.</summary>
        public static void Finish(bool served)
        {
            Action<bool> done = _done;
            _done = null;
            if (done == null)
            {
                return;
            }
            try
            {
                done(served);
            }
            catch (Exception e)
            {
                HomeDirector.Logger?.Error(e);
            }
        }
    }
}
