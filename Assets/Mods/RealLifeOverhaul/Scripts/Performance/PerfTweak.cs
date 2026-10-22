using System;
using BAModAPI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One optimisation of the base game: a single change that can be switched on and off while the game
    /// runs and that puts back exactly what it found. A tweak never throws into the frame: a fault is
    /// logged once, the game's own state is restored and the tweak stays off until the city is loaded again.
    ///
    /// To add one: derive from this class in Performance/Tweaks, record everything you change in Apply and
    /// put it back in Restore, add the instance to <see cref="PerfTweaks.All"/> and its label to en.json.
    /// The options toggle, the saved setting, the probe's status line and the benchmark row follow by themselves.
    /// </summary>
    public abstract class PerfTweak
    {
        /// <summary>Short stable name: the option id (prefixed), the saved setting and the benchmark row.</summary>
        public abstract string Id { get; }

        /// <summary>Key of the options label in en.json. A tweak that can change the picture says so in that label.</summary>
        public abstract string LabelKey { get; }

        /// <summary>On for everyone only where a paired benchmark has shown a saving and the picture cannot change.</summary>
        public abstract bool DefaultOn { get; }

        /// <summary>A measurement for the benchmark only: no option, no label, never on outside a benchmark row.</summary>
        public virtual bool Trial => false;

        /// <summary>
        /// Set by a slider or a choice of its own (Core/Settings), not by a switch: no switch on the options page, and on
        /// whenever <see cref="SettingWants"/> (the setting is not the game's own).
        /// </summary>
        public virtual bool BySetting => false;

        /// <summary>For a tweak set by a setting: whether the setting asks for a change from the game's own now.</summary>
        public virtual bool SettingWants => false;

        /// <summary>Why the item has nothing to change in this game right now (the render pipeline has the feature off), or null.</summary>
        public virtual string NotInUse => null;

        /// <summary>Seconds between calls of <see cref="Tick"/> while the tweak is on; zero for a tweak that needs none.</summary>
        public virtual float Interval => 0f;

        /// <summary>What the player has chosen on the options page.</summary>
        public bool Wanted;

        /// <summary>Set by the benchmark to try a state without touching the player's choice; null outside it.</summary>
        public bool? Forced;

        public bool Active { get; private set; }

        /// <summary>
        /// True while the tweak is still working through the scene in slices (switching on, or putting things
        /// back). The benchmark waits for it before it measures; while it is true the tweak's timer runs fast.
        /// </summary>
        public virtual bool Busy => false;
        public bool Faulted { get; private set; }

        internal float NextTick;

        /// <summary>Its setting changed while it was on: the director takes it off and puts it on again with the new value.</summary>
        internal bool Stale;

        public bool ShouldBeActive => !Faulted && (Forced ?? Wanted);

        /// <summary>Change the game. Called once when the tweak comes on; everything changed is remembered.</summary>
        protected abstract void Apply();

        /// <summary>Undo every change, including on objects that have since been switched off. Must cope with destroyed objects.</summary>
        protected abstract void Restore();

        /// <summary>Upkeep on the tweak's timer, never per frame over everything.</summary>
        protected virtual void Tick(float now) { }

        /// <summary>
        /// A scene was loaded (true) or unloaded (false). Only note it here and look again on the timer, a slice at a
        /// time: this is called in the frame the scene arrives or goes, and scenes come and go while the game is
        /// played, not only behind the loading screen.
        /// </summary>
        internal virtual void WorldChanged(bool loaded) { }

        /// <summary>
        /// The look at the scene this tweak would make when it comes on (a search of the whole scene, or a walk over
        /// every object of the city's scenes: a frame of 80 to 130 ms each), made instead while the loading screen is
        /// still up, if the tweak is wanted then (<see cref="PerfTweaks.Prewarm"/>, through Core/SceneFinds' ScenePrewarm).
        /// What it finds is kept for Apply and the timer; anything a scene brings later is looked at as before. A tweak
        /// with no such look has nothing here.
        /// </summary>
        protected virtual void Prewarm() { }

        internal void RunPrewarm(IModLogger logger)
        {
            if (Active || !ShouldBeActive)
            {
                return;
            }
            try
            {
                Prewarm();
            }
            catch (Exception e)
            {
                logger?.Error("Performance: '" + Id + "' could not look at the scene behind the loading screen; it looks when it comes on, as before. " + e);
            }
        }

        /// <summary>One line for the reports: what the tweak holds changed right now.</summary>
        public virtual string Describe() => string.Empty;

        internal void SetActive(bool on, IModLogger logger)
        {
            if (on == Active)
            {
                return;
            }
            try
            {
                if (on)
                {
                    Active = true;   // before Apply, so a fault half-way still restores
                    Apply();
                }
                else
                {
                    Active = false;
                    Restore();
                }
            }
            catch (Exception e)
            {
                Fault(e, logger);
            }
        }

        internal void RunTick(float now, IModLogger logger)
        {
            try
            {
                Tick(now);
            }
            catch (Exception e)
            {
                Fault(e, logger);
            }
        }

        /// <summary>The city scene is going: put everything back and be ready for the next one.</summary>
        internal void Shutdown(IModLogger logger)
        {
            SetActive(false, logger);
            Forced = null;
            Faulted = false;
        }

        /// <summary>For a tweak's own callbacks (engine or game events), which run outside Apply and Tick: the same ending as a fault there.</summary>
        protected void Abort(Exception e)
        {
            Fault(e, PerfDirector.Logger);
            PerfTweaks.Dirty = true;
        }

        private void Fault(Exception e, IModLogger logger)
        {
            Faulted = true;
            bool wasActive = Active;
            Active = false;
            logger?.Error("Performance: '" + Id + "' met something unexpected, has put the game's own state back and stays off until the city is loaded again. " + e);
            if (wasActive)
            {
                try
                {
                    Restore();
                }
                catch (Exception again)
                {
                    logger?.Error("Performance: '" + Id + "' could not restore everything. " + again);
                }
            }
        }
    }
}
