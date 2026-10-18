namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// A staged scene with people in it (the proposal, the wedding, the pushchair walk), one at a time. The director ticks it
    /// five times a second and calls <see cref="End"/> exactly once on every way out; End gives back every person and prop.
    /// </summary>
    internal abstract class FamilyScene
    {
        public bool Done;
        public abstract string Name { get; }
        /// <summary>Stages it where the player stands. False with a locale key if it cannot be done here; nothing is left out then.</summary>
        public abstract bool Begin(FamilyDirector director, out string refusal);
        public abstract void Tick(float now);
        public abstract void End();
    }
}
