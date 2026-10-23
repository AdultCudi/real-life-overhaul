using System;
using BAModAPI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One optimisation of the game, switched while it runs and putting back exactly what it found; a fault restores the
    /// game's state and keeps it off until the city loads again. Joins <see cref="PerfTweaks.All"/>.
    /// </summary>
    public abstract class PerfTweak
    {
        /// <summary>Short stable name: the option id (prefixed), the saved setting and the benchmark row.</summary>
        public abstract string Id { get; }

        /// <summary>Key of the options label; a tweak that can change the picture says so there.</summary>
        public abstract string LabelKey { get; }

        /// <summary>On for everyone only where a paired benchmark has shown a saving and the picture cannot change.</summary>
        public abstract bool DefaultOn { get; }

        /// <summary>A measurement for the benchmark only: no option, no label, never on outside a benchmark row.</summary>
        public virtual bool Trial => false;

        /// <summary>Set by a slider or choice of its own (Core/Settings), with no switch: on whenever <see cref="SettingWants"/>.</summary>
        public virtual bool BySetting => false;

        /// <summary>For a tweak set by a setting: the setting differs from the game's own.</summary>
        public virtual bool SettingWants => false;

        /// <summary>Why it has nothing to change in this game now (the pipeline has the feature off), or null.</summary>
        public virtual string NotInUse => null;

        /// <summary>Seconds between calls of <see cref="Tick"/> while the tweak is on; zero for a tweak that needs none.</summary>
        public virtual float Interval => 0f;

        public bool Wanted;

        /// <summary>Set by the benchmark to try a state without touching the player's choice; null outside it.</summary>
        public bool? Forced;

        public bool Active { get; private set; }

        /// <summary>Still working through the scene in slices; the benchmark waits, and the tweak's timer runs fast.</summary>
        public virtual bool Busy => false;
        public bool Faulted { get; private set; }

        internal float NextTick;

        /// <summary>Its setting moved while it was on: the director takes it off and on again.</summary>
        internal bool Stale;

        public bool ShouldBeActive => !Faulted && (Forced ?? Wanted);

        /// <summary>Called once when the tweak comes on; everything changed is remembered.</summary>
        protected abstract void Apply();

        /// <summary>Undo every change, also on objects switched off since; must cope with destroyed objects.</summary>
        protected abstract void Restore();

        protected virtual void Tick(float now) { }

        /// <summary>A scene loaded or unloaded: only noted, looked at again on the timer.</summary>
        internal virtual void WorldChanged(bool loaded) { }

        /// <summary>The tweak's whole-scene look (80 to 130 ms), made behind the loading screen when wanted; kept for Apply.</summary>
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

        /// <summary>One line for the reports: what the tweak holds changed now.</summary>
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

        /// <summary>The city scene is going: everything back, ready for the next one.</summary>
        internal void Shutdown(IModLogger logger)
        {
            SetActive(false, logger);
            Forced = null;
            Faulted = false;
        }

        /// <summary>For the tweak's own engine or game callbacks: the same ending as a fault in Apply or Tick.</summary>
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
