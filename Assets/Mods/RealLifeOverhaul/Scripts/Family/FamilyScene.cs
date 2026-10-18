namespace RealLifeOverhaul.Family
{
    /// <summary>A staged scene with people (proposal, wedding, walk), one at a time; <see cref="End"/> is called once on every way out.</summary>
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
