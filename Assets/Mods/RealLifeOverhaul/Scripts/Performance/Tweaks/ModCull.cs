namespace RealLifeOverhaul
{
    /// <summary>The mod's things in the street not drawn beyond 200 m (<see cref="ModCulling"/>); on by default, as the far plane is 300 m.</summary>
    public sealed class ModCull : PerfTweak
    {
        public override string Id => "mod_cull";
        public override string LabelKey => "reallife_perf_mod_cull";
        public override bool DefaultOn => true;
        public override float Interval => 2f;

        protected override void Apply() => ModCulling.Enable(true);

        protected override void Tick(float now) => ModCulling.Keep();

        protected override void Restore() => ModCulling.Enable(false);

        public override string Describe() => ModCulling.Describe();
    }
}
